using System.Data.Common;
using System.Reflection;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Api.Controllers;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Orders;
using MercadoCercano.Application.Promotions;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Orders;
using MercadoCercano.Infrastructure.Persistence;
using MercadoCercano.Infrastructure.Promotions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MercadoCercano.Infrastructure.Tests;

public sealed partial class OrderServiceTests
{
    private static PromotionWriteRequest Rule(string? code = null, long value = 10) => new()
    {
        Name = "Beneficio de prueba", Code = code, DiscountType = "Percentage", DiscountValue = value,
        StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-1), EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1), IsActive = true
    };

    private static async Task<PromotionResponse> SaveRule(TestOrders data, PromotionWriteRequest request, Guid? id = null)
    {
        await using var context = data.NewContext();
        return await new PromotionService(context).SaveAsync(id, request, data.UserId, Ct);
    }

    [Fact]
    public void PromotionsRequireAdministratorAndDoNotAllowAnonymousActions()
    {
        var type = typeof(AdminPromotionsController);
        Assert.Equal(RolePolicies.Administrator, type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.True(type.GetCustomAttribute<ResponseCacheAttribute>()?.NoStore);
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.All(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            action => Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>()));
    }

    [Fact]
    public async Task PromotionAdministrationNormalizesCodesAuditsAndChecksVersions()
    {
        await using var data = await TestOrders.CreateAsync();
        var request = Rule(" bienvenida_10 ");
        var created = await SaveRule(data, request);
        Assert.Equal("BIENVENIDA_10", created.Code);
        Assert.Equal("Active", created.State);
        var duplicate = await Assert.ThrowsAsync<CatalogException>(() => SaveRule(data, Rule("bienvenida_10")));
        Assert.Equal(CatalogError.Conflict, duplicate.Error);
        var updated = await SaveRule(data, request with { IsActive = false, UpdatedAtUtc = created.UpdatedAtUtc }, created.Id);
        Assert.Equal("Inactive", updated.State);
        var stale = await Assert.ThrowsAsync<CatalogException>(() => SaveRule(data,
            request with { UpdatedAtUtc = created.UpdatedAtUtc }, created.Id));
        Assert.Equal(CatalogError.Conflict, stale.Error);
        Assert.Equal(2, await data.Db.AuditLogs.CountAsync(log => log.EntityType == nameof(Promotion)));
        var service = new PromotionService(data.Db);
        Assert.Single((await service.ListAsync(new() { Kind = "coupon", Search = "_10", IsActive = false }, Ct)).Items);
        Assert.Empty((await service.ListAsync(new() { Kind = "automatic" }, Ct)).Items);
        Assert.Empty((await service.ListAsync(new() { Page = int.MaxValue }, Ct)).Items);
    }

    [Fact]
    public async Task PromotionAdministrationRejectsInvalidRulesAndReferences()
    {
        await using var data = await TestOrders.CreateAsync();
        var valid = Rule();
        var category = await data.Db.Products.Select(item => item.CategoryId).FirstAsync();
        foreach (var invalid in new[]
        {
            valid with { Name = " " }, valid with { Code = "a!" }, valid with { Code = "niño" },
            valid with { DiscountValue = 0 }, valid with { DiscountValue = 101 },
            valid with { DiscountType = "1" }, valid with { MinimumOrderAmount = -1 },
            valid with { MaximumDiscountAmount = 0 }, valid with { MaxUses = 0 },
            valid with { StartsAtUtc = null }, valid with { EndsAtUtc = valid.StartsAtUtc },
            valid with { ProductId = data.ProductId, CategoryId = category },
            valid with { ProductId = Guid.NewGuid() }, valid with { CategoryId = Guid.NewGuid() }
        })
        {
            var error = await Assert.ThrowsAsync<CatalogException>(() => SaveRule(data, invalid));
            Assert.Equal(CatalogError.Validation, error.Error);
        }
        Assert.Empty(await data.Db.Promotions.ToListAsync());
        Assert.Empty(await data.Db.AuditLogs.Where(item => item.EntityType == nameof(Promotion)).ToListAsync());
    }

    [Theory]
    [InlineData(15, 20, "CUPON20", 160, false)]
    [InlineData(25, 20, null, 200, true)]
    [InlineData(20, 20, "CUPON20", 160, false)]
    public async Task CheckoutChoosesOneBestBenefitAfterProductOffersAndExcludesShipping(
        long automaticPercent, long couponPercent, string? expectedCode, long additional, bool notice)
    {
        await using var data = await TestOrders.CreateAsync();
        await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(item => item.PromotionalPrice, (long?)800));
        await SaveRule(data, Rule(value: automaticPercent));
        await SaveRule(data, Rule("CUPON20", couponPercent));
        var quote = await data.Call(service => service.QuoteAsync(data.Selection(method: "LocalDelivery", municipality: "Municipio de prueba")
            with { CouponCode = " cupon20 " }, Ct));
        Assert.Equal(expectedCode, quote.Promotion?.Code);
        Assert.Equal(additional, quote.Promotion?.DiscountTotal);
        Assert.Equal(200 + additional, quote.DiscountTotal);
        Assert.Equal(5000, quote.ShippingCost);
        Assert.Equal(5800 - additional, quote.GrandTotal);
        Assert.Equal(notice, quote.CouponNotice is not null);
        Assert.Equal(additional, quote.Items.Sum(item => item.PromotionDiscountTotal));
        Assert.All(await data.Db.Promotions.ToListAsync(), item => Assert.Equal(0, item.UsedCount));
        Assert.Empty(await data.Db.PromotionUsages.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScopedMinimumUsesOnlyMatchingEffectiveMerchandise(bool byCategory)
    {
        await using var data = await TestOrders.CreateAsync();
        var other = await data.AddProduct();
        var category = new Category { Name = "Otra", Slug = "otra" };
        data.Db.Categories.Add(category);
        await data.Db.SaveChangesAsync();
        await data.Db.Products.Where(item => item.Id == other).ExecuteUpdateAsync(update => update.SetProperty(item => item.CategoryId, category.Id));
        var ownCategory = await data.Db.Products.Where(item => item.Id == data.ProductId).Select(item => item.CategoryId).SingleAsync();
        var rule = Rule("ALCANCE", 50) with
        {
            ProductId = byCategory ? null : data.ProductId, CategoryId = byCategory ? ownCategory : null,
            MinimumOrderAmount = 1500
        };
        await SaveRule(data, rule);
        var selection = data.Selection() with
        {
            CouponCode = "ALCANCE",
            Items = [new() { ProductId = data.ProductId, Quantity = 1 }, new() { ProductId = other, Quantity = 2 }]
        };
        await Fails(CheckoutError.Validation, () => data.Call(service => service.QuoteAsync(selection, Ct)));
        selection = selection with { Items = [new() { ProductId = data.ProductId, Quantity = 2 }, new() { ProductId = other, Quantity = 2 }] };
        var quote = await data.Call(service => service.QuoteAsync(selection, Ct));
        Assert.Equal(1000, quote.Promotion?.DiscountTotal);
        Assert.Equal(0, quote.Items.Single(item => item.ProductId == other).PromotionDiscountTotal);
        await data.Db.Products.Where(item => item.Id == data.ProductId).ExecuteUpdateAsync(update => update.SetProperty(item => item.PromotionalPrice, (long?)700));
        await Fails(CheckoutError.Validation, () => data.Call(service => service.QuoteAsync(selection, Ct)));
    }

    [Theory]
    [InlineData("Percentage", 33, null, 333)]
    [InlineData("Percentage", 100, 127L, 127)]
    [InlineData("FixedAmount", 9000, null, 1010)]
    [InlineData("FixedAmount", 9000, 250L, 250)]
    public async Task DiscountsFloorWholeCopRespectCapsAndNeverExceedMerchandise(string type, long value, long? cap, long expected)
    {
        await using var data = await TestOrders.CreateAsync();
        await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(item => item.Price, 1010));
        await SaveRule(data, Rule(value: value) with { DiscountType = type, MaximumDiscountAmount = cap });
        var quote = await data.Call(service => service.QuoteAsync(data.Selection(), Ct));
        Assert.Equal(expected, quote.Promotion?.DiscountTotal);
        Assert.Equal(1010 - expected, quote.GrandTotal);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("exhausted")]
    [InlineData("minimum")]
    public async Task UnavailableCouponsFailExplicitlyEvenWithAnAutomaticBenefit(string reason)
    {
        await using var data = await TestOrders.CreateAsync();
        await SaveRule(data, Rule());
        var rule = Rule("CODIGO") with { MaxUses = 1 };
        if (reason == "inactive") rule = rule with { IsActive = false };
        if (reason == "expired") rule = rule with { StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-2), EndsAtUtc = DateTimeOffset.UtcNow.AddDays(-1) };
        if (reason == "future") rule = rule with { StartsAtUtc = DateTimeOffset.UtcNow.AddDays(1), EndsAtUtc = DateTimeOffset.UtcNow.AddDays(2) };
        if (reason == "minimum") rule = rule with { MinimumOrderAmount = 1001 };
        if (reason != "missing")
        {
            var saved = await SaveRule(data, rule);
            if (reason == "exhausted") await data.Db.Promotions.Where(item => item.Id == saved.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.UsedCount, 1));
        }
        await Fails(CheckoutError.Validation, () => data.Call(service => service.QuoteAsync(data.Selection() with { CouponCode = "CODIGO" }, Ct)));
    }

    [Fact]
    public async Task ExactAllocationAtMaximumAmountIsStableAndPreservesEveryCop()
    {
        await using var data = await TestOrders.CreateAsync();
        var other = await data.AddProduct();
        await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(item => item.Price, OrderService.MaximumAmount / 2));
        var rule = await SaveRule(data, Rule(value: 33));
        var selection = data.Selection() with { Items = [new() { ProductId = data.ProductId, Quantity = 1 }, new() { ProductId = other, Quantity = 1 }] };
        var first = await data.Call(service => service.QuoteAsync(selection, Ct));
        var reverse = await data.Call(service => service.QuoteAsync(selection with { Items = selection.Items.Reverse().ToArray() }, Ct));
        Assert.Equal(2_970_000_000_000, first.Promotion?.DiscountTotal);
        Assert.Equal(first.Promotion?.DiscountTotal, first.Items.Sum(item => item.PromotionDiscountTotal));
        Assert.Equal(first.Fingerprint, reverse.Fingerprint);
        await SaveRule(data, Rule(value: 1) with { DiscountType = "FixedAmount", UpdatedAtUtc = rule.UpdatedAtUtc }, rule.Id);
        var pennies = await data.Call(service => service.QuoteAsync(selection, Ct));
        Assert.Equal(1, pennies.Items.Sum(item => item.PromotionDiscountTotal));
        Assert.Equal(1, pennies.Items.MinBy(item => item.ProductId)!.PromotionDiscountTotal);
    }

    [Fact]
    public async Task CreationConsumesOnceAndOtherRedemptionsDoNotInvalidateRuleOrQuote()
    {
        await using var data = await TestOrders.CreateAsync();
        var rule = await SaveRule(data, Rule("USOS") with { MaxUses = 3 });
        var first = await data.RequestAsync(data.Selection() with { CouponCode = "usos" });
        var second = await data.RequestAsync(data.Selection() with { CouponCode = "USOS" });
        var created = await data.Call(service => service.CreateAsync(first, Ct));
        var retry = await data.Call(service => service.CreateAsync(first with
        { Selection = first.Selection with { CouponCode = " USOS " } }, Ct));
        Assert.Equal(created.OrderNumber, retry.OrderNumber);
        await data.Call(service => service.CreateAsync(second, Ct));
        var current = await data.Db.Promotions.AsNoTracking().SingleAsync();
        Assert.Equal(2, current.UsedCount);
        Assert.Equal(rule.UpdatedAtUtc.UtcDateTime, current.UpdatedAtUtc);
        Assert.Equal(2, await data.Db.PromotionUsages.CountAsync());
        var recovered = await data.Call(service => service.RecoverAsync(new() { IdempotencyKey = first.IdempotencyKey, TrackingToken = first.TrackingToken }, Ct));
        Assert.Equal(created.Promotion, recovered.Promotion);
        Assert.Equal(10, await data.Stock());
        var immutable = await Assert.ThrowsAsync<CatalogException>(() => SaveRule(data,
            Rule("OTRO") with { UpdatedAtUtc = rule.UpdatedAtUtc }, rule.Id));
        Assert.Equal(CatalogError.Conflict, immutable.Error);
        var tooSmall = await Assert.ThrowsAsync<CatalogException>(() => SaveRule(data,
            Rule("USOS") with { MaxUses = 1, UpdatedAtUtc = rule.UpdatedAtUtc }, rule.Id));
        Assert.Equal(CatalogError.Conflict, tooSmall.Error);
        await SaveRule(data, Rule("USOS", 50) with { UpdatedAtUtc = rule.UpdatedAtUtc }, rule.Id);
        Assert.Equal(100, (await data.Find(created.OrderNumber)).Order.Promotion?.DiscountTotal);
    }

    [Fact]
    public async Task RuleEditsInvalidateQuotesWithoutCreatingAnyOrder()
    {
        await using var data = await TestOrders.CreateAsync();
        var rule = await SaveRule(data, Rule());
        var request = await data.RequestAsync();
        await SaveRule(data, Rule(value: 20) with { UpdatedAtUtc = rule.UpdatedAtUtc }, rule.Id);
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CreateAsync(request, Ct)));
        Assert.Empty(await data.Db.Orders.ToListAsync());
        Assert.Empty(await data.Db.Customers.ToListAsync());
        Assert.Empty(await data.Db.PromotionUsages.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedCreationRollsBackOrderCustomerPaymentAndQuota(bool failCommit)
    {
        await using var data = await TestOrders.CreateAsync();
        await SaveRule(data, Rule() with { MaxUses = 1 });
        var request = await data.RequestAsync();
        if (failCommit)
        {
            await using var context = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
                .UseSqlite(data.Db.Database.GetDbConnection()).AddInterceptors(new RejectPromotionCommit()).Options);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new OrderService(context, new CheckoutSettingsService(context),
                true, data.EvidenceStore).CreateAsync(request, Ct));
        }
        else
        {
            await data.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectOrder BEFORE INSERT ON Orders BEGIN SELECT RAISE(ABORT, 'test'); END;");
            await Assert.ThrowsAsync<DbUpdateException>(() => data.Call(service => service.CreateAsync(request, Ct)));
        }
        Assert.Equal(0, await data.Db.Promotions.Select(item => item.UsedCount).SingleAsync());
        Assert.Empty(await data.Db.Orders.ToListAsync());
        Assert.Empty(await data.Db.Customers.ToListAsync());
        Assert.Empty(await data.Db.Payments.ToListAsync());
        Assert.Empty(await data.Db.PromotionUsages.ToListAsync());
    }

    [Fact]
    public async Task IndependentConnectionsCannotOverspendLastPromotionUse()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mercado-promotion-{Guid.NewGuid():N}.db");
        try
        {
            await using var data = await TestOrders.CreateAsync(filePath: path);
            await SaveRule(data, Rule("ULTIMO") with { MaxUses = 1 });
            var first = await data.RequestAsync(data.Selection() with { CouponCode = "ULTIMO" });
            var second = await data.RequestAsync(data.Selection() with { CouponCode = "ULTIMO" });
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool> Create(CreateOrderRequest request) => Task.Run(async () =>
            {
                await start.Task;
                try { await data.CrossCall(service => service.CreateAsync(request, Ct)); return true; }
                catch (CheckoutException error) when (error.Error is CheckoutError.Validation or CheckoutError.Conflict) { return false; }
            });
            var attempts = new[] { Create(first), Create(second) };
            start.SetResult();
            var results = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Single(results, result => result);
            Assert.Single(await data.Db.Orders.ToListAsync());
            Assert.Single(await data.Db.Customers.ToListAsync());
            Assert.Single(await data.Db.Payments.ToListAsync());
            Assert.Single(await data.Db.PromotionUsages.ToListAsync());
            Assert.Equal(1, await data.Db.Promotions.Select(item => item.UsedCount).SingleAsync());
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationReleasesUseOnlyOnceAndPreservesDiscountedHistory(bool paid)
    {
        await using var data = await TestOrders.CreateAsync();
        var rule = await SaveRule(data, Rule("LIBRE", 25) with { MaxUses = 1 });
        var request = await data.RequestAsync(data.Selection() with { CouponCode = "LIBRE" });
        var response = await data.Call(service => service.CreateAsync(request, Ct));
        var order = await data.Find(response.OrderNumber);
        if (paid) order = await data.Pay(await data.Evidence(order));
        order = await data.Call(service => service.CancelAsync(order.Id, new() { UpdatedAtUtc = order.UpdatedAtUtc, Reason = "Cancelar antes de confirmar" }, data.UserId, Ct));
        await data.Call(service => service.CancelAsync(order.Id, new() { UpdatedAtUtc = order.UpdatedAtUtc }, data.UserId, Ct));
        Assert.Equal(0, await data.Db.Promotions.Select(item => item.UsedCount).SingleAsync());
        Assert.NotNull((await data.Db.PromotionUsages.SingleAsync()).ReleasedAtUtc);
        Assert.Equal(250, order.Order.Promotion?.DiscountTotal);
        Assert.Equal(10, await data.Stock());
        if (paid)
        {
            var returned = Assert.Single(order.Operations.Returns);
            Assert.Equal(750, returned.Summary.RefundAmount);
            Assert.Equal(750, Assert.Single(returned.Items).RefundAmount);
            order = await data.Refund(order, returned);
            Assert.Equal(750, order.Order.RefundedTotal);
        }
        await data.Call(async service => await service.CreateAsync(await data.RequestAsync(data.Selection() with { CouponCode = "LIBRE" }), Ct));
        await using var context = data.NewContext();
        var history = await new PromotionService(context).UsagesAsync(rule.Id, 1, Ct);
        Assert.Equal(2, history.Total);
        Assert.Single(history.Items, item => item.ReleasedAtUtc is not null);
    }

    [Fact]
    public async Task PartialRefundsAndRejectedAllocationsNeverExceedNetPaidMerchandise()
    {
        await using var data = await TestOrders.CreateAsync();
        await SaveRule(data, Rule(value: 1) with { DiscountType = "FixedAmount" });
        var (order, _) = await data.Delivered(quantity: 3);
        var pending = await data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(order, 1), data.UserId, Ct));
        var first = Assert.Single(pending.Operations.Returns);
        Assert.Equal(999, first.Summary.RefundAmount);
        pending = await data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(pending, 1), data.UserId, Ct));
        var second = pending.Operations.Returns.Single(item => item.Summary.Id != first.Summary.Id);
        Assert.Equal(1000, second.Summary.RefundAmount);
        pending = await data.Call(service => service.RejectReturnAsync(order.Id, first.Summary.Id,
            new() { UpdatedAtUtc = pending.UpdatedAtUtc, Reason = "No se devolvio la unidad" }, data.UserId, Ct));
        Assert.Equal(1999, Assert.Single(pending.Operations.Lines).RefundableAmount);
        pending = await data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(pending, 2), data.UserId, Ct));
        var last = pending.Operations.Returns.Single(item => item.Summary.Id != first.Summary.Id && item.Summary.Id != second.Summary.Id);
        Assert.Equal(1999, last.Summary.RefundAmount);
        pending = await data.Receive(pending, second, 1);
        pending = await data.Refund(pending, second);
        pending = await data.Receive(pending, last, 2);
        pending = await data.Refund(pending, last);
        Assert.Equal(2999, pending.Order.RefundedTotal);
        Assert.Equal("Refunded", pending.Order.PaymentStatus);
        Assert.Equal(10, await data.Stock());
        Assert.Equal(1, await data.Db.Promotions.Select(item => item.UsedCount).SingleAsync());
        Assert.Null((await data.Db.PromotionUsages.SingleAsync()).ReleasedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullyDiscountedPickupRequiresNoFundsButStillTracksPhysicalInventory(bool cancel)
    {
        await using var data = await TestOrders.CreateAsync();
        await SaveRule(data, Rule(value: 100));
        var request = await data.RequestAsync();
        var response = await data.Call(service => service.CreateAsync(request, Ct));
        Assert.Equal(0, response.GrandTotal);
        Assert.Equal("Paid", response.PaymentStatus);
        Assert.Equal(10, await data.Stock());
        var order = await data.Find(response.OrderNumber);
        if (cancel)
        {
            order = await data.Call(service => service.CancelAsync(order.Id, new() { UpdatedAtUtc = order.UpdatedAtUtc, Reason = "Cancelar sin cobro" }, data.UserId, Ct));
            Assert.Equal(0, Assert.Single(order.Operations.Returns).Summary.RefundAmount);
            Assert.Equal(0, await data.Db.Promotions.Select(item => item.UsedCount).SingleAsync());
        }
        else
        {
            order = await data.Call(service => service.ConfirmAsync(order.Id, new() { UpdatedAtUtc = order.UpdatedAtUtc }, data.UserId, Ct));
            order = await data.Step(await data.Step(await data.Step(order, "prepare"), "ready"), "deliver");
            Assert.Equal(9, await data.Stock());
            order = await data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(order, 1), data.UserId, Ct));
            var returned = Assert.Single(order.Operations.Returns);
            Assert.Equal(0, returned.Summary.RefundAmount);
            await data.Receive(order, returned, 1);
            Assert.Equal(1, await data.Db.Promotions.Select(item => item.UsedCount).SingleAsync());
        }
        Assert.Equal(10, await data.Stock());
        Assert.Empty(await data.Db.PaymentEvidence.ToListAsync());
        Assert.Empty(await data.Db.AuditLogs.Where(item => item.Action == "Order.PaymentApproved").ToListAsync());
    }

    [Fact]
    public async Task PaidCancellationRefundsScopedNetLinesAndAllShipping()
    {
        await using var data = await TestOrders.CreateAsync();
        var other = await data.AddProduct();
        await SaveRule(data, Rule(value: 25) with { ProductId = data.ProductId });
        var selection = data.Selection(method: "LocalDelivery", municipality: "Municipio de prueba") with
        { Items = [new() { ProductId = data.ProductId, Quantity = 1 }, new() { ProductId = other, Quantity = 1 }] };
        var request = await data.RequestAsync(selection);
        var response = await data.Call(service => service.CreateAsync(request, Ct));
        var order = await data.Find(response.OrderNumber);
        order = await data.Call(service => service.ConfirmAsync(order.Id, new() { UpdatedAtUtc = order.UpdatedAtUtc }, data.UserId, Ct));
        order = await data.Pay(await data.Evidence(order));
        order = await data.Call(service => service.CancelAsync(order.Id, new() { UpdatedAtUtc = order.UpdatedAtUtc, Reason = "Cancelar envio pagado" }, data.UserId, Ct));
        var returned = Assert.Single(order.Operations.Returns);
        Assert.Equal(6750, returned.Summary.RefundAmount);
        Assert.Equal(1750, returned.Items.Sum(item => item.RefundAmount));
        Assert.Equal(new long[] { 750, 1000 }, returned.Items.Select(item => item.RefundAmount).Order().ToArray());
        order = await data.Refund(order, returned);
        Assert.Equal(6750, order.Order.RefundedTotal);
        Assert.All(await data.Db.Products.ToListAsync(), item => Assert.Equal(10, item.StockQuantity));
        Assert.Equal(0, await data.Db.Promotions.Select(item => item.UsedCount).SingleAsync());
    }

    [Fact]
    public async Task FailedShipmentDoesNotReleaseUseEvenAfterFullReceiptAndRefund()
    {
        await using var data = await TestOrders.CreateAsync();
        await SaveRule(data, Rule(value: 100));
        var (order, _) = await data.Confirmed(delivery: "LocalDelivery", quantity: 1);
        order = await data.Pay(await data.Evidence(order));
        order = await data.Step(await data.Step(await data.Step(order, "prepare"), "ready"), "ship");
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CancelAsync(order.Id,
            new() { UpdatedAtUtc = order.UpdatedAtUtc, Reason = "No se cancela un despacho" }, data.UserId, Ct)));
        order = await data.Call(service => service.CreateReturnAsync(order.Id,
            data.ReturnRequest(order, 1) with { ShippingRefundAmount = 5000 }, data.UserId, Ct));
        var returned = Assert.Single(order.Operations.Returns);
        Assert.Equal(5000, returned.Summary.RefundAmount);
        order = await data.Receive(order, returned, 1);
        order = await data.Refund(order, returned);
        Assert.Equal("Cancelled", order.Order.Status);
        Assert.Equal("Refunded", order.Order.PaymentStatus);
        Assert.Equal(1, await data.Db.Promotions.Select(item => item.UsedCount).SingleAsync());
        Assert.Null((await data.Db.PromotionUsages.SingleAsync()).ReleasedAtUtc);
        Assert.Equal(10, await data.Stock());
    }

    [Fact]
    public async Task UncollectedCashOrderHasNoRefundableMoneyDespiteDiscountedMerchandise()
    {
        await using var data = await TestOrders.CreateAsync();
        await SaveRule(data, Rule(value: 25));
        var (order, _) = await data.Confirmed(method: "CashOnDelivery", delivery: "LocalDelivery", quantity: 1);
        Assert.Equal(0, Assert.Single(order.Operations.Lines).RefundableAmount);
        order = await data.Step(await data.Step(await data.Step(order, "prepare"), "ready"), "ship");
        order = await data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(order, 1), data.UserId, Ct));
        var returned = Assert.Single(order.Operations.Returns);
        Assert.Equal(0, returned.Summary.RefundAmount);
        order = await data.Receive(order, returned, 1);
        Assert.Equal("Cancelled", order.Order.Status);
        Assert.Equal(0, Assert.Single(order.Operations.Lines).RefundableAmount);
        Assert.Equal(1, await data.Db.Promotions.Select(item => item.UsedCount).SingleAsync());
    }

    [Fact]
    public async Task MigrationPreservesLegacyRulesAndSupportsTheirCaseInsensitiveCodes()
    {
        await using var data = await TestOrders.CreateAsync();
        await data.Db.GetService<IMigrator>().MigrateAsync("20261019165546_OrderOperationsAndPartialReturns");
        var id = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(-1);
        var end = DateTime.UtcNow.AddDays(1);
        await data.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Promotions (Id, Code, DiscountType, DiscountValue, MinimumOrderAmount, MaxUses, UsedCount, StartsAtUtc, EndsAtUtc, IsActive)
            VALUES ({id}, {"legacy"}, {1}, {10}, {0}, {5}, {0}, {start}, {end}, {true})
            """);
        await data.Db.Database.MigrateAsync();
        var rule = await data.Db.Promotions.AsNoTracking().SingleAsync();
        Assert.Equal(id, rule.Id);
        Assert.Equal("Promocion", rule.Name);
        Assert.True(rule.UpdatedAtUtc > DateTime.MinValue);
        Assert.Equal(5, rule.MaxUses);
        var quote = await data.Call(service => service.QuoteAsync(data.Selection() with { CouponCode = " LEGACY " }, Ct));
        Assert.Equal(100, quote.Promotion?.DiscountTotal);
        Assert.Null(quote.CouponNotice);
        var duplicate = await Assert.ThrowsAsync<CatalogException>(() => SaveRule(data, Rule("LEGACY")));
        Assert.Equal(CatalogError.Conflict, duplicate.Error);
        Assert.Equal("ok", Assert.Single(await data.Db.Database.SqlQueryRaw<string>("PRAGMA integrity_check").ToListAsync()));
    }

    private sealed class RejectPromotionCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated commit failure");
    }
}
