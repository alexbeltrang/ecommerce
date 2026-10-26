using System.Reflection;
using System.Text.Json;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Api.Controllers;
using MercadoCercano.Application.Orders;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Orders;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Tests;

public sealed partial class OrderServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static string Token() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public void ControllersHaveExplicitRolesAndPublicMutationsHaveRateLimits()
    {
        Assert.Equal(RolePolicies.OrderOperations, typeof(AdminOrdersController).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(RolePolicies.Administrator, typeof(AdminCheckoutSettingsController).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        foreach (var controller in new[] { typeof(AdminOrdersController), typeof(AdminCheckoutSettingsController) })
        {
            Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.All(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                action => Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>()));
        }
        Assert.NotNull(typeof(CheckoutController).GetCustomAttribute<AllowAnonymousAttribute>());
        foreach (var (action, policy) in new[]
        {
            (nameof(CheckoutController.Quote), RateLimitPolicies.CheckoutQuote),
            (nameof(CheckoutController.Create), RateLimitPolicies.OrderCreation),
            (nameof(CheckoutController.Track), RateLimitPolicies.OrderTracking),
            (nameof(CheckoutController.Recover), RateLimitPolicies.OrderTracking),
            (nameof(CheckoutController.Get), RateLimitPolicies.OrderTracking)
        })
            Assert.Equal(policy, typeof(CheckoutController).GetMethod(action)!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        Assert.True(typeof(CheckoutController).GetCustomAttribute<ResponseCacheAttribute>()?.NoStore);
        Assert.Equal(new[] { "ProductId", "Quantity" }, typeof(CartLineRequest).GetProperties().Select(property => property.Name).Order().ToArray());
    }

    [Fact]
    public async Task MissingSettingsAreUnavailableAndDevelopmentSeedDoesNotOverwriteEdits()
    {
        await using var data = await TestOrders.CreateAsync(seed: false);
        await Fails(CheckoutError.Unavailable, () => data.Settings.GetAsync(Ct));
        await CheckoutSettingsService.SeedDevelopmentAsync(data.Db);
        var first = await data.Settings.GetAsync(Ct);
        Assert.True(first.Values.IsTestConfiguration);
        Assert.Equal(5000, first.Values.LocalDeliveryFee);
        Assert.Equal(15000, first.Values.CarrierShippingFee);
        await data.ConfigureAsync(first.Values with { LocalDeliveryFee = 7500 });
        await CheckoutSettingsService.SeedDevelopmentAsync(data.Db);
        Assert.Equal(7500, (await data.Settings.GetAsync(Ct)).Values.LocalDeliveryFee);
        Assert.Single(await data.Db.StoreSettings.Where(setting => setting.Key == CheckoutSettingsService.SettingKey).ToListAsync());
    }

    [Fact]
    public async Task SettingsUseVersionsAuditChangesAndRejectInvalidCoverage()
    {
        await using var data = await TestOrders.CreateAsync();
        var initial = await data.Settings.GetAsync(Ct);
        await data.ConfigureAsync(initial.Values with { LocalDeliveryFee = 7000 });
        data.Db.ChangeTracker.Clear();
        await Fails(CheckoutError.Conflict, () => data.Settings.SaveAsync(new() { Values = initial.Values, UpdatedAtUtc = initial.UpdatedAtUtc }, data.UserId, Ct));
        Assert.Single(await data.Db.AuditLogs.Where(log => log.Action == "CheckoutSettings.Update").ToListAsync());
        foreach (var invalid in new[]
        {
            initial.Values with { LocalDeliveryFee = -1 },
            initial.Values with { CarrierShippingFee = OrderService.MaximumAmount + 1 },
            initial.Values with { PickupEnabled = false, LocalDeliveryEnabled = false, CarrierShippingEnabled = false },
            initial.Values with { PickupMunicipality = " " },
            initial.Values with { LocalMunicipalities = [] },
            initial.Values with { CarrierNationwide = false, CarrierMunicipalities = [] },
            initial.Values with { LocalMunicipalities = ["Bogota", "BOGOTA"] },
            initial.Values with { LocalMunicipalities = [" Bogota"] },
            initial.Values with { PaymentMethods = [] },
            initial.Values with { PaymentMethods = ["BankTransfer", "BankTransfer"] },
            initial.Values with { PaymentMethods = ["1"] }
        })
            await Fails(CheckoutError.Validation, () => data.ConfigureAsync(invalid));
        await data.ConfigureAsync(initial.Values with { PickupEnabled = false, PickupMunicipality = "", PickupInstructions = "" });
    }

    [Fact]
    public async Task CorruptStoredConfigurationDoesNotBecomeClientValidationOrSuccess()
    {
        await using var data = await TestOrders.CreateAsync();
        await data.Db.StoreSettings.Where(setting => setting.Key == CheckoutSettingsService.SettingKey)
            .ExecuteUpdateAsync(update => update.SetProperty(setting => setting.Value, "{}"));
        await Fails(CheckoutError.Unavailable, () => data.Settings.GetAsync(Ct));
        var invalid = JsonSerializer.Serialize(CheckoutSettingsService.TestDefaults() with { PaymentMethods = ["Bad"] });
        await data.Db.StoreSettings.Where(setting => setting.Key == CheckoutSettingsService.SettingKey)
            .ExecuteUpdateAsync(update => update.SetProperty(setting => setting.Value, invalid));
        await Fails(CheckoutError.Unavailable, () => data.Settings.GetAsync(Ct));
    }

    [Theory]
    [InlineData("CoordinatedPickup", null, 0)]
    [InlineData("LocalDelivery", "Municipio de prueba", 5000)]
    [InlineData("CarrierShipping", "Bogota", 15000)]
    public async Task QuoteUsesCurrentPricesPromotionsAndConfiguredDelivery(string method, string? city, long shipping)
    {
        await using var data = await TestOrders.CreateAsync();
        await data.Db.Products.Where(product => product.Id == data.ProductId).ExecuteUpdateAsync(update =>
            update.SetProperty(product => product.PromotionalPrice, 700)
                .SetProperty(product => product.PromotionStartUtc, DateTime.UtcNow.AddDays(-1))
                .SetProperty(product => product.PromotionEndUtc, DateTime.UtcNow.AddDays(1)));
        var quote = await data.Call(service => service.QuoteAsync(data.Selection(2, method, city), Ct));
        Assert.Equal(2000, quote.Subtotal);
        Assert.Equal(600, quote.DiscountTotal);
        Assert.Equal(shipping, quote.ShippingCost);
        Assert.Equal(1400 + shipping, quote.GrandTotal);
        Assert.Equal(700, Assert.Single(quote.Items).UnitPrice);
        Assert.Matches("^[A-F0-9]{64}$", quote.Fingerprint);
        Assert.Equal(10, await data.Stock());
        Assert.Equal(0, await data.Db.Orders.CountAsync());
    }

    [Fact]
    public async Task ExpiredPromotionAndDisabledPaymentsOrCoverageAreRespected()
    {
        await using var data = await TestOrders.CreateAsync();
        await data.Db.Products.Where(product => product.Id == data.ProductId).ExecuteUpdateAsync(update =>
            update.SetProperty(product => product.PromotionalPrice, 500)
                .SetProperty(product => product.PromotionStartUtc, DateTime.UtcNow.AddDays(-2))
                .SetProperty(product => product.PromotionEndUtc, DateTime.UtcNow.AddDays(-1)));
        Assert.Equal(1000, (await data.Call(service => service.QuoteAsync(data.Selection(), Ct))).GrandTotal);
        await Fails(CheckoutError.Validation, () => data.Call(service => service.QuoteAsync(data.Selection(1, "LocalDelivery", "Fuera de cobertura"), Ct)));
        await data.ConfigureAsync(CheckoutSettingsService.TestDefaults() with
        {
            CarrierNationwide = false, CarrierMunicipalities = ["Bogota"], PaymentMethods = ["BankTransfer"], PickupEnabled = false
        });
        await Fails(CheckoutError.Validation, () => data.Call(service => service.QuoteAsync(data.Selection(), Ct)));
        await Fails(CheckoutError.Validation, () => data.Call(service => service.QuoteAsync(data.Selection(1, "CarrierShipping", "Cali"), Ct)));
        await Fails(CheckoutError.Validation, () => data.Call(service => service.QuoteAsync(data.Selection(1, "CarrierShipping", "Bogota") with { PaymentMethod = "DigitalWallet" }, Ct)));
    }

    [Fact]
    public async Task CartValidationRejectsDuplicatesUnavailableProductsAndNonpositiveQuantities()
    {
        await using var data = await TestOrders.CreateAsync();
        var selection = data.Selection();
        foreach (var invalid in new[]
        {
            selection with { Items = [] },
            selection with { Items = [new() { ProductId = data.ProductId, Quantity = 0 }] },
            selection with { Items = [new() { ProductId = data.ProductId, Quantity = -1 }] },
            selection with { Items = [new() { ProductId = Guid.Empty, Quantity = 1 }] },
            selection with { Items = [selection.Items[0], selection.Items[0]] },
            selection with { Items = Enumerable.Range(0, 51).Select(_ => new CartLineRequest { ProductId = Guid.NewGuid(), Quantity = 1 }).ToArray() },
            selection with { DeliveryMethod = "1" }, selection with { PaymentMethod = "1" }
        })
            await Fails(CheckoutError.Validation, () => data.Call(service => service.QuoteAsync(invalid, Ct)));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.QuoteAsync(data.Selection(11), Ct)));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.QuoteAsync(selection with { Items = [new() { ProductId = Guid.NewGuid(), Quantity = 1 }] }, Ct)));
        await data.Db.Products.Where(product => product.Id == data.ProductId).ExecuteUpdateAsync(update => update.SetProperty(product => product.IsActive, false));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.QuoteAsync(selection, Ct)));
        await data.Db.Products.Where(product => product.Id == data.ProductId).ExecuteUpdateAsync(update => update.SetProperty(product => product.IsActive, true));
        await data.Db.Categories.ExecuteUpdateAsync(update => update.SetProperty(category => category.IsActive, false));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.QuoteAsync(selection, Ct)));
    }

    [Fact]
    public async Task AmountLimitIsExactAndOverflowCannotProduceAnOrder()
    {
        await using var data = await TestOrders.CreateAsync();
        await data.Db.Products.Where(product => product.Id == data.ProductId).ExecuteUpdateAsync(update => update.SetProperty(product => product.Price, OrderService.MaximumAmount));
        Assert.Equal(OrderService.MaximumAmount, (await data.Call(service => service.QuoteAsync(data.Selection(), Ct))).GrandTotal);
        await Fails(CheckoutError.Validation, () => data.Call(service => service.QuoteAsync(data.Selection(2), Ct)));
        await Fails(CheckoutError.Validation, () => data.Call(service => service.QuoteAsync(data.Selection(1, "LocalDelivery", "Municipio de prueba"), Ct)));
        await data.Db.Products.Where(product => product.Id == data.ProductId).ExecuteUpdateAsync(update =>
            update.SetProperty(product => product.StockQuantity, int.MaxValue).SetProperty(product => product.Price, long.MaxValue));
        await Fails(CheckoutError.Validation, () => data.Call(service => service.QuoteAsync(data.Selection(int.MaxValue), Ct)));
        Assert.Equal(0, await data.Db.Orders.CountAsync());
    }

    [Fact]
    public async Task CreationSavesSnapshotsAndPendingPaymentWithoutReservingStock()
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync(data.Selection(2, "LocalDelivery", "Municipio de prueba"));
        var response = await data.Call(service => service.CreateAsync(request, Ct));
        var stored = await data.Db.Orders.AsNoTracking().Include(order => order.Items).Include(order => order.Payments).Include(order => order.StatusHistory).SingleAsync();
        Assert.Equal(OrderStatus.PendingConfirmation, stored.Status);
        Assert.Equal(PaymentStatus.Pending, stored.PaymentStatus);
        Assert.Equal("Cliente privado", stored.CustomerNameSnapshot);
        Assert.Equal("Direccion privada 123", stored.AddressSnapshot);
        Assert.Equal(request.IdempotencyKey.ToString("N"), stored.IdempotencyKey);
        Assert.NotEqual(request.TrackingToken, stored.TrackingTokenHash);
        Assert.Equal(64, stored.TrackingTokenHash!.Length);
        Assert.Equal(64, stored.RequestFingerprint!.Length);
        Assert.NotNull(stored.TermsAcceptedAtUtc);
        Assert.Contains("PRUEBA", stored.CheckoutNoticeSnapshot);
        Assert.True(stored.IsTestOrder);
        Assert.False(stored.ShippingCostConfirmed);
        Assert.Equal(400, Assert.Single(stored.Items).UnitCostSnapshot);
        Assert.Equal(7000, stored.GrandTotal);
        Assert.Equal(7000, Assert.Single(stored.Payments).Amount);
        Assert.Equal(PaymentStatus.Pending, Assert.Single(stored.Payments).Status);
        Assert.Single(stored.StatusHistory);
        Assert.Equal(10, await data.Stock());
        Assert.Empty(await data.Db.InventoryMovements.ToListAsync());
        Assert.Single(await data.Db.Customers.ToListAsync());
        Assert.Single(await data.Db.Addresses.ToListAsync());
        Assert.Single(await data.Db.AuditLogs.Where(log => log.Action == "Order.Create").ToListAsync());
        var json = JsonSerializer.Serialize(response);
        foreach (var privateValue in new[] { request.CustomerName, request.Phone, request.Email!, request.Address!, request.Notes!, request.TrackingToken, request.IdempotencyKey.ToString() })
            Assert.DoesNotContain(privateValue, json);
        var properties = typeof(OrderResponse).GetProperties().Select(property => property.Name);
        Assert.DoesNotContain("CustomerName", properties);
        Assert.DoesNotContain("TrackingTokenHash", properties);
        Assert.DoesNotContain("IdempotencyKey", properties);
        Assert.DoesNotContain("UnitCost", typeof(OrderLineResponse).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task GuestOrdersDoNotOverwriteAnotherCustomersContactData()
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync();
        await data.Call(service => service.CreateAsync(request, Ct));
        await data.Call(service => service.CreateAsync(request with { IdempotencyKey = Guid.NewGuid(), TrackingToken = Token(), CustomerName = "Otro comprador" }, Ct));
        Assert.Equal(2, await data.Db.Customers.CountAsync());
        Assert.Contains(await data.Db.Customers.Select(customer => customer.FullName).ToArrayAsync(), name => name == request.CustomerName);
        Assert.Equal(10, await data.Stock());
    }

    [Fact]
    public async Task RetryAndRecoveryReturnOriginalEvenAfterPricesSettingsAndStockChange()
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync();
        var first = await data.Call(service => service.CreateAsync(request, Ct));
        await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(product => product.Price, 3000).SetProperty(product => product.StockQuantity, 0));
        await data.ConfigureAsync(CheckoutSettingsService.TestDefaults() with { PaymentInstructions = "Nuevas instrucciones" });
        var repeated = await data.Call(service => service.CreateAsync(request, Ct));
        Assert.Equal(first.OrderNumber, repeated.OrderNumber);
        Assert.Equal(1000, repeated.GrandTotal);
        Assert.Equal(first.PaymentInstructions, repeated.PaymentInstructions);
        var recovered = await data.Call(service => service.RecoverAsync(new() { IdempotencyKey = request.IdempotencyKey, TrackingToken = request.TrackingToken }, Ct));
        Assert.Equal(first.OrderNumber, recovered.OrderNumber);
        Assert.Single(await data.Db.Orders.ToListAsync());
        Assert.Single(await data.Db.Customers.ToListAsync());
        Assert.Single(await data.Db.Payments.ToListAsync());
    }

    [Fact]
    public async Task ReusedAttemptWithChangedPayloadConflictsAndDifferentSecretIsNotFound()
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync();
        await data.Call(service => service.CreateAsync(request, Ct));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CreateAsync(request with { Notes = "Otros datos" }, Ct)));
        await Fails(CheckoutError.NotFound, () => data.Call(service => service.CreateAsync(request with { TrackingToken = Token() }, Ct)));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CreateAsync(request with { IdempotencyKey = Guid.NewGuid() }, Ct)));
        Assert.Single(await data.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task TrackingNeedsSecretAndNeverTreatsAttemptIdentifierAsAuthorization()
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync();
        var order = await data.Call(service => service.CreateAsync(request, Ct));
        Assert.Equal(order.OrderNumber, (await data.Call(service => service.TrackAsync(new() { OrderNumber = order.OrderNumber.ToLowerInvariant(), TrackingToken = request.TrackingToken }, Ct))).OrderNumber);
        var wrong = await Fails(CheckoutError.NotFound, () => data.Call(service => service.TrackAsync(new() { OrderNumber = order.OrderNumber, TrackingToken = Token() }, Ct)));
        var absent = await Fails(CheckoutError.NotFound, () => data.Call(service => service.TrackAsync(new() { OrderNumber = "MC-NOT-FOUND", TrackingToken = request.TrackingToken }, Ct)));
        Assert.Equal(wrong.Message, absent.Message);
        await Fails(CheckoutError.NotFound, () => data.Call(service => service.RecoverAsync(new() { IdempotencyKey = request.IdempotencyKey, TrackingToken = Token() }, Ct)));
        await Fails(CheckoutError.NotFound, () => data.Call(service => service.RecoverAsync(new() { IdempotencyKey = Guid.NewGuid(), TrackingToken = request.TrackingToken }, Ct)));
        await Fails(CheckoutError.NotFound, () => data.Call(service => service.TrackAsync(new() { OrderNumber = order.OrderNumber, TrackingToken = "" }, Ct)));
    }

    [Theory]
    [InlineData("price")]
    [InlineData("name")]
    [InlineData("configuration")]
    public async Task StaleQuoteCannotCreateAnOrder(string change)
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync();
        if (change == "price")
            await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(product => product.Price, 2000));
        else if (change == "name")
            await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(product => product.Name, "Nuevo nombre"));
        else
            await data.ConfigureAsync(CheckoutSettingsService.TestDefaults() with { TermsText = "Nuevas condiciones de prueba" });
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CreateAsync(request, Ct)));
        Assert.Empty(await data.Db.Orders.ToListAsync());
        Assert.Empty(await data.Db.Customers.ToListAsync());
        Assert.Empty(await data.Db.Payments.ToListAsync());
    }

    [Fact]
    public async Task InvalidContactTermsAndDeliveryCannotCreateGuestRecords()
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync(data.Selection(1, "LocalDelivery", "Municipio de prueba"));
        foreach (var invalid in new[]
        {
            request with { TermsAccepted = false }, request with { CustomerName = " " },
            request with { Phone = "123" }, request with { Phone = "1234567abc" },
            request with { Address = " " }, request with { TrackingToken = "not-a-secret" },
            request with { IdempotencyKey = Guid.Empty }
        })
            await Fails(CheckoutError.Validation, () => data.Call(service => service.CreateAsync(invalid, Ct)));
        await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity, 0));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CreateAsync(request, Ct)));
        Assert.Empty(await data.Db.Customers.ToListAsync());
    }

    [Fact]
    public async Task TestOrdersAreBlockedOutsideDevelopmentButRealConfigurationWorks()
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync();
        await Fails(CheckoutError.Unavailable, () => data.Call(service => service.QuoteAsync(data.Selection(), Ct), allowTests: false));
        await Fails(CheckoutError.Unavailable, () => data.Call(service => service.CreateAsync(request, Ct), allowTests: false));
        var testOrder = await data.Call(service => service.CreateAsync(request, Ct));
        var admin = await data.Find(testOrder.OrderNumber);
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.ConfirmAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc }, data.UserId, Ct), allowTests: false));
        await data.ConfigureAsync(CheckoutSettingsService.TestDefaults() with { IsTestConfiguration = false });
        var realRequest = await data.RequestAsync();
        var realOrder = await data.Call(service => service.CreateAsync(realRequest, Ct), allowTests: false);
        Assert.False(realOrder.IsTestOrder);
        Assert.True((await data.Db.Orders.AsNoTracking().SingleAsync(order => order.OrderNumber == realOrder.OrderNumber)).ShippingCostConfirmed);
    }

    [Fact]
    public async Task ConfirmationDeductsExactlyOncePreservesHistoricalPriceAndDoesNotCreditPayment()
    {
        await using var data = await TestOrders.CreateAsync();
        var response = await data.Call(async service => await service.CreateAsync(await data.RequestAsync(data.Selection(3)), Ct));
        var admin = await data.Find(response.OrderNumber);
        await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(product => product.Price, 4000));
        var confirmed = await data.Call(service => service.ConfirmAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc }, data.UserId, Ct));
        Assert.Equal("Confirmed", confirmed.Order.Status);
        Assert.Equal(3000, confirmed.Order.GrandTotal);
        Assert.Equal("Pending", confirmed.Order.PaymentStatus);
        Assert.Equal(7, await data.Stock());
        await data.Call(service => service.ConfirmAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc }, data.UserId, Ct));
        Assert.Equal(7, await data.Stock());
        var movement = Assert.Single(await data.Db.InventoryMovements.ToListAsync());
        Assert.Equal(InventoryMovementType.Sale, movement.MovementType);
        Assert.Equal(10, movement.PreviousQuantity);
        Assert.Equal(-3, movement.QuantityDelta);
        Assert.Equal(7, movement.NewQuantity);
        Assert.Equal(2, await data.Db.OrderStatusHistory.CountAsync());
        Assert.Single(await data.Db.AuditLogs.Where(log => log.Action == "Order.Confirm").ToListAsync());
    }

    [Fact]
    public async Task PendingOrdersCanCompeteButConfirmationCannotOversell()
    {
        await using var data = await TestOrders.CreateAsync();
        var first = await data.Call(async service => await service.CreateAsync(await data.RequestAsync(data.Selection(6)), Ct));
        var second = await data.Call(async service => await service.CreateAsync(await data.RequestAsync(data.Selection(6)), Ct));
        var admin1 = await data.Find(first.OrderNumber);
        var admin2 = await data.Find(second.OrderNumber);
        await data.Call(service => service.ConfirmAsync(admin1.Id, new() { UpdatedAtUtc = admin1.UpdatedAtUtc }, data.UserId, Ct));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.ConfirmAsync(admin2.Id, new() { UpdatedAtUtc = admin2.UpdatedAtUtc }, data.UserId, Ct)));
        Assert.Equal(4, await data.Stock());
        Assert.Equal("PendingConfirmation", (await data.Find(second.OrderNumber)).Order.Status);
        Assert.Single(await data.Db.InventoryMovements.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentConnectionsSerializeConfirmationAndCannotOversell()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mercado-cercano-order-{Guid.NewGuid():N}.db");
        try
        {
            await using var data = await TestOrders.CreateAsync(filePath: path);
            var first = await data.Call(async service => await service.CreateAsync(await data.RequestAsync(data.Selection(6)), Ct));
            var second = await data.Call(async service => await service.CreateAsync(await data.RequestAsync(data.Selection(6)), Ct));
            var admin1 = await data.Find(first.OrderNumber);
            var admin2 = await data.Find(second.OrderNumber);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<CheckoutError?> Confirm(AdminOrderResponse order) => Task.Run(async () =>
            {
                await start.Task;
                await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = path, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString());
                await using var context = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options);
                var service = new OrderService(context, new CheckoutSettingsService(context), true, data.EvidenceStore);
                try
                {
                    await service.ConfirmAsync(order.Id, new() { UpdatedAtUtc = order.UpdatedAtUtc }, data.UserId, Ct);
                    return (CheckoutError?)null;
                }
                catch (CheckoutException error) when (error.Error == CheckoutError.Conflict) { return error.Error; }
            });
            var operations = new[] { Confirm(admin1), Confirm(admin2) };
            start.SetResult();
            var results = await Task.WhenAll(operations).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Single(results, result => result is null);
            Assert.Single(results, result => result == CheckoutError.Conflict);
            Assert.Equal(4, await data.Stock());
            Assert.Single(await data.Db.InventoryMovements.ToListAsync());
            Assert.Single(await data.Db.Orders.Where(order => order.Status == OrderStatus.Confirmed).ToListAsync());
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }

    [Fact]
    public async Task MultilineConfirmationFailsAtomicallyAndStaleVersionsCannotMutate()
    {
        await using var data = await TestOrders.CreateAsync();
        var second = await data.AddProduct();
        var selection = data.Selection() with { Items = [new() { ProductId = data.ProductId, Quantity = 2 }, new() { ProductId = second, Quantity = 1 }] };
        var request = await data.RequestAsync(selection);
        var order = await data.Call(service => service.CreateAsync(request, Ct));
        var admin = await data.Find(order.OrderNumber);
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.ConfirmAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc.AddSeconds(-1) }, data.UserId, Ct)));
        await data.Db.Products.Where(product => product.Id == second).ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity, 0));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.ConfirmAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc }, data.UserId, Ct)));
        Assert.Equal(10, await data.Stock());
        Assert.Empty(await data.Db.InventoryMovements.ToListAsync());
        Assert.Equal("PendingConfirmation", (await data.Find(order.OrderNumber)).Order.Status);
        Assert.Equal(1, await data.Db.OrderStatusHistory.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationRestocksOnlyPreviouslyConfirmedOrdersAndNeverTwice(bool confirm)
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync(data.Selection(3));
        var response = await data.Call(service => service.CreateAsync(request, Ct));
        var admin = await data.Find(response.OrderNumber);
        if (confirm) admin = await data.Call(service => service.ConfirmAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc }, data.UserId, Ct));
        await Fails(CheckoutError.Validation, () => data.Call(service => service.CancelAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc, Reason = " " }, data.UserId, Ct)));
        var cancelled = await data.Call(service => service.CancelAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc, Reason = "Sin disponibilidad. Motivo publico." }, data.UserId, Ct));
        Assert.Equal("Cancelled", cancelled.Order.Status);
        Assert.Equal(10, await data.Stock());
        await data.Call(service => service.CancelAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc }, data.UserId, Ct));
        Assert.Equal(10, await data.Stock());
        Assert.Equal(confirm ? 2 : 0, await data.Db.InventoryMovements.CountAsync());
        Assert.Single(await data.Db.AuditLogs.Where(log => log.Action == "Order.Cancel").ToListAsync());
        var publicOrder = await data.Call(service => service.TrackAsync(new() { OrderNumber = response.OrderNumber, TrackingToken = request.TrackingToken }, Ct));
        Assert.Contains(publicOrder.History, history => history.Reason == "Sin disponibilidad. Motivo publico.");
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.ConfirmAsync(admin.Id, new() { UpdatedAtUtc = cancelled.UpdatedAtUtc }, data.UserId, Ct)));
    }

    [Fact]
    public async Task CancellationRejectsStaleVersionsProcessedPaymentsAndRestockOverflow()
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync();
        var response = await data.Call(service => service.CreateAsync(request, Ct));
        var admin = await data.Find(response.OrderNumber);
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CancelAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc.AddSeconds(-1), Reason = "Cancelar" }, data.UserId, Ct)));
        admin = await data.Call(service => service.ConfirmAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc }, data.UserId, Ct));
        await data.Db.Orders.ExecuteUpdateAsync(update => update.SetProperty(order => order.PaymentStatus, PaymentStatus.Paid));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CancelAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc, Reason = "Cancelar" }, data.UserId, Ct)));
        await data.Db.Orders.ExecuteUpdateAsync(update => update.SetProperty(order => order.PaymentStatus, PaymentStatus.Pending));
        await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity, int.MaxValue));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CancelAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc, Reason = "Cancelar" }, data.UserId, Ct)));
        Assert.Equal(int.MaxValue, await data.Stock());
        Assert.Equal("Confirmed", (await data.Find(response.OrderNumber)).Order.Status);
        Assert.Single(await data.Db.InventoryMovements.ToListAsync());
    }

    [Fact]
    public async Task AdministrationFiltersEscapesWildcardsAndHandlesHugePages()
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync();
        var response = await data.Call(service => service.CreateAsync(request with { CustomerName = "Cliente 100%" }, Ct));
        var found = await data.Call(service => service.ListAsync(new() { Search = "%" }, Ct));
        Assert.Equal(1, found.Total);
        Assert.Equal(response.OrderNumber, Assert.Single(found.Items).Order.OrderNumber);
        Assert.Equal("Cliente 100%", Assert.Single(found.Items).CustomerName);
        Assert.Empty((await data.Call(service => service.ListAsync(new() { Status = "Confirmed" }, Ct))).Items);
        var huge = await data.Call(service => service.ListAsync(new() { Page = int.MaxValue, PageSize = 50 }, Ct));
        Assert.Equal(1, huge.Total);
        Assert.Empty(huge.Items);
    }

    private static async Task<CheckoutException> Fails(CheckoutError error, Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<CheckoutException>(action);
        Assert.Equal(error, exception.Error);
        return exception;
    }

    [Fact]
    public async Task AdministratorCanCreateInitialRealConfigurationWithoutDevelopmentSeeding()
    {
        await using var data = await TestOrders.CreateAsync(seed: false);
        var saved = await data.Settings.SaveAsync(new()
        {
            Values = CheckoutSettingsService.TestDefaults() with { IsTestConfiguration = false }, UpdatedAtUtc = null
        }, data.UserId, Ct);
        Assert.False(saved.Values.IsTestConfiguration);
        Assert.Equal(1000, (await data.Call(service => service.QuoteAsync(data.Selection(), Ct), allowTests: false)).GrandTotal);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public async Task BasicActionsCannotBypassAdvancedFulfillmentStates(OrderStatus status)
    {
        await using var data = await TestOrders.CreateAsync();
        var request = await data.RequestAsync();
        var order = await data.Call(service => service.CreateAsync(request, Ct));
        var admin = await data.Find(order.OrderNumber);
        await data.Db.Orders.ExecuteUpdateAsync(update => update.SetProperty(stored => stored.Status, status));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.ConfirmAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc }, data.UserId, Ct)));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CancelAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc, Reason = "Cancelar" }, data.UserId, Ct)));
        Assert.Equal(10, await data.Stock());
        Assert.Empty(await data.Db.InventoryMovements.ToListAsync());
    }

    private sealed partial class TestOrders : IAsyncDisposable
    {
        private readonly string evidenceDirectory = Path.Combine(Path.GetTempPath(), "mercado-cercano-payment-" + Guid.NewGuid().ToString("N"));
        public IPrivatePaymentEvidenceStore EvidenceStore => new FilePaymentEvidenceStore(evidenceDirectory);
        private readonly SqliteConnection connection;
        private TestOrders(string? path) => connection = new(new SqliteConnectionStringBuilder
        { DataSource = path ?? ":memory:", ForeignKeys = true, Pooling = false }.ToString());
        public StoreDbContext Db { get; private set; } = null!;
        public CheckoutSettingsService Settings => new(Db);
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid ProductId { get; private set; }
        private Guid categoryId;
        public static async Task<TestOrders> CreateAsync(bool seed = true, string? filePath = null)
        {
            var data = new TestOrders(filePath);
            await data.connection.OpenAsync();
            data.Db = data.NewContext();
            await data.Db.Database.MigrateAsync();
            data.Db.Users.Add(new User
            {
                Id = data.UserId, Name = "Operador prueba", Email = "orders@example.test",
                NormalizedEmail = "ORDERS@EXAMPLE.TEST", PasswordHash = "not-a-real-password", Role = UserRole.Administrator
            });
            var category = new Category { Name = "Pruebas", Slug = "pruebas", IsActive = true };
            data.categoryId = category.Id;
            data.Db.Categories.Add(category);
            await data.Db.SaveChangesAsync();
            data.ProductId = await data.AddProduct();
            if (seed) await CheckoutSettingsService.SeedDevelopmentAsync(data.Db);
            data.Db.ChangeTracker.Clear();
            return data;
        }
        public async Task<Guid> AddProduct()
        {
            var id = Guid.NewGuid();
            Db.Products.Add(new Product
            {
                Id = id, CategoryId = categoryId, Sku = id.ToString("N"), Slug = id.ToString("N"), Name = "Producto de prueba",
                Description = "Producto ficticio para pruebas locales", Price = 1000, Cost = 400, Unit = "unidad", StockQuantity = 10, IsActive = true
            });
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return id;
        }
        public StoreDbContext NewContext() => new(new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options);
        public async Task<T> Call<T>(Func<OrderService, Task<T>> action, bool allowTests = true)
        {
            await using var context = NewContext();
            return await action(new(context, new CheckoutSettingsService(context), allowTests, EvidenceStore));
        }
        public QuoteRequest Selection(int quantity = 1, string method = "CoordinatedPickup", string? municipality = null) => new()
        {
            Items = [new() { ProductId = ProductId, Quantity = quantity }], DeliveryMethod = method, Municipality = municipality, PaymentMethod = "BankTransfer"
        };
        public async Task<CreateOrderRequest> RequestAsync(QuoteRequest? selection = null)
        {
            selection ??= Selection();
            var quote = await Call(service => service.QuoteAsync(selection, Ct));
            return new()
            {
                Selection = selection, QuoteFingerprint = quote.Fingerprint, IdempotencyKey = Guid.NewGuid(), TrackingToken = Token(),
                CustomerName = "Cliente privado", Phone = "3005551234", Email = "privado@example.test", Address = "Direccion privada 123",
                Neighborhood = "Barrio privado", AddressReferences = "Referencia privada", Notes = "Observacion privada", TermsAccepted = true
            };
        }
        public async Task<CheckoutSettingsResponse> ConfigureAsync(CheckoutSettings values)
        {
            Db.ChangeTracker.Clear();
            var previous = await Settings.GetAsync(Ct);
            return await Settings.SaveAsync(new() { Values = values, UpdatedAtUtc = previous.UpdatedAtUtc }, UserId, Ct);
        }
        public Task<int> Stock() => Db.Products.Where(product => product.Id == ProductId).Select(product => product.StockQuantity).SingleAsync();
        public async Task<AdminOrderResponse> Find(string number)
        {
            var id = await Db.Orders.Where(order => order.OrderNumber == number).Select(order => order.Id).SingleAsync();
            return await Call(service => service.GetAsync(id, Ct));
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
            if (Directory.Exists(evidenceDirectory)) Directory.Delete(evidenceDirectory, recursive: true);
        }
    }
}
