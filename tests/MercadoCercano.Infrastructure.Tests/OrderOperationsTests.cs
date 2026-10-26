using System.Reflection;
using System.Text.Json;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Api.Controllers;
using MercadoCercano.Application.Catalog;
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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MercadoCercano.Infrastructure.Tests;

public sealed partial class OrderServiceTests
{
    private static readonly byte[] Receipt = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a8ioAAAAASUVORK5CYII=");

    [Fact]
    public void OperationsAndEvidenceHaveExplicitAuthorizationAndRefundIsAdministratorOnly()
    {
        var type = typeof(AdminOrderOperationsController);
        Assert.Equal(RolePolicies.OrderOperations, type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(RolePolicies.Administrator, type.GetMethod(nameof(AdminOrderOperationsController.Refund))!
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.All(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            action => Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>()));
        Assert.True(type.GetCustomAttribute<ResponseCacheAttribute>()?.NoStore);
        Assert.NotNull(typeof(PaymentEvidenceController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(RateLimitPolicies.PaymentEvidence, typeof(PaymentEvidenceController).GetMethod(nameof(PaymentEvidenceController.Submit))!
            .GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Theory]
    [InlineData("BankTransfer")]
    [InlineData("DigitalWallet")]
    public async Task PreparationRequiresValidatedFundsNotJustAProof(string method)
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, request) = await data.Confirmed(method);
        await Fails(CheckoutError.Conflict, () => data.Step(order, "prepare"));
        var proof = await data.Evidence(order);
        Assert.Equal("UnderReview", proof.Order.PaymentStatus);
        await Fails(CheckoutError.Conflict, () => data.Step(proof, "prepare"));
        var paid = await data.Pay(proof);
        var prepared = await data.Step(paid, "prepare");
        Assert.Equal("Preparing", prepared.Order.Status);
        Assert.Equal(7, await data.Stock());
        Assert.Equal(1, await data.Db.InventoryMovements.CountAsync());
        Assert.Equal("Paid", (await data.Call(service => service.TrackAsync(new() { OrderNumber = order.Order.OrderNumber, TrackingToken = request.TrackingToken }, Ct))).PaymentStatus);
    }

    [Theory]
    [InlineData("CoordinatedPickup")]
    [InlineData("LocalDelivery")]
    [InlineData("CarrierShipping")]
    public async Task FulfillmentHonorsDeliveryRoutesAndCannotSkipStages(string delivery)
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Confirmed("BankTransfer", delivery);
        await Fails(CheckoutError.Conflict, () => data.Step(order, "deliver"));
        order = await data.Pay(await data.Evidence(order));
        order = await data.Step(order, "prepare");
        await Fails(CheckoutError.Conflict, () => data.Step(order, "ship"));
        order = await data.Step(order, "ready");
        if (delivery == "CoordinatedPickup")
            await Fails(CheckoutError.Conflict, () => data.Step(order, "ship"));
        else
        {
            await Fails(CheckoutError.Conflict, () => data.Step(order, "deliver"));
            if (delivery == "CarrierShipping")
                await Fails(CheckoutError.Validation, () => data.Call(service => service.FulfillAsync(order.Id, "ship", new() { UpdatedAtUtc = order.UpdatedAtUtc }, data.UserId, Ct)));
            order = await data.Step(order, "ship");
            Assert.NotNull(order.Order.Fulfillment.ShippedAtUtc);
            if (delivery == "CarrierShipping") Assert.Equal("GUIA-PRUEBA", order.Order.Fulfillment.TrackingNumber);
        }
        await Fails(CheckoutError.Validation, () => data.Call(service => service.FulfillAsync(order.Id, "deliver", new() { UpdatedAtUtc = order.UpdatedAtUtc }, data.UserId, Ct)));
        var delivered = await data.Step(order, "deliver");
        Assert.Equal("Delivered", delivered.Order.Status);
        Assert.NotNull(delivered.Order.Fulfillment.DeliveredAtUtc);
        Assert.Equal(7, await data.Stock());
        var repeat = await data.Step(delivered, "deliver");
        Assert.Equal(delivered.Order.Fulfillment.DeliveredAtUtc, repeat.Order.Fulfillment.DeliveredAtUtc);
        await Fails(CheckoutError.Conflict, () => data.Step(delivered, "prepare"));
    }

    [Fact]
    public async Task CashOnDeliveryCannotBeCreditedEarlyOrDeliveredBeforeCollection()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Confirmed("CashOnDelivery");
        await Fails(CheckoutError.Conflict, () => data.Pay(order));
        order = await data.Step(await data.Step(order, "prepare"), "ready");
        await Fails(CheckoutError.Conflict, () => data.Step(order, "deliver"));
        var paid = await data.Pay(order);
        Assert.Equal("Paid", paid.Order.PaymentStatus);
        var delivered = await data.Step(paid, "deliver");
        Assert.Equal("Delivered", delivered.Order.Status);
        Assert.Empty(delivered.Operations.Evidence);
    }

    [Fact]
    public async Task PaymentApprovalRequiresExactAmountAttestationAndConsistentPaymentRecord()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Confirmed();
        order = await data.Evidence(order);
        await Fails(CheckoutError.Validation, () => data.Call(service => service.ReviewPaymentAsync(order.Id, true, new()
        { UpdatedAtUtc = order.UpdatedAtUtc, Amount = order.Order.GrandTotal - 1, Reference = "REF", FundsVerified = true }, data.UserId, Ct)));
        await Fails(CheckoutError.Validation, () => data.Call(service => service.ReviewPaymentAsync(order.Id, true, new()
        { UpdatedAtUtc = order.UpdatedAtUtc, Amount = order.Order.GrandTotal, Reference = "REF" }, data.UserId, Ct)));
        var paid = await data.Pay(order);
        var repeated = await data.Pay(paid);
        Assert.Equal(paid.UpdatedAtUtc, repeated.UpdatedAtUtc);
        Assert.Single(await data.Db.AuditLogs.Where(log => log.Action == "Payment.Approve").ToListAsync());
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.ReviewPaymentAsync(paid.Id, true, new()
        { UpdatedAtUtc = paid.UpdatedAtUtc, Amount = paid.Order.GrandTotal, Reference = "CHANGED", FundsVerified = true }, data.UserId, Ct)));
        Assert.Equal(PaymentStatus.Paid, (await data.Db.Payments.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task OperatorCanValidatePaymentsButCannotRegisterRefunds()
    {
        await using var data = await TestOrders.CreateAsync();
        var operatorId = await data.Operator();
        var (order, _) = await data.Confirmed();
        order = await data.Pay(await data.Evidence(order), operatorId);
        Assert.Equal(operatorId, (await data.Db.Payments.AsNoTracking().SingleAsync()).ConfirmedByUserId);
        order = await data.Call(service => service.CancelAsync(order.Id, new() { UpdatedAtUtc = order.UpdatedAtUtc, Reason = "Cancelacion coordinada" }, operatorId, Ct));
        var returned = Assert.Single(order.Operations.Returns);
        await Fails(CheckoutError.Forbidden, () => data.Refund(order, returned, operatorId));
        Assert.Equal(0, (await data.Find(order.Order.OrderNumber)).Order.RefundedTotal);
        order = await data.Refund(order, returned);
        Assert.Equal("Refunded", order.Order.PaymentStatus);
    }

    [Fact]
    public async Task RejectedProofCanBeResubmittedWithoutMarkingFundsReceived()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Confirmed();
        order = await data.Evidence(order);
        var rejection = new PaymentReviewRequest { UpdatedAtUtc = order.UpdatedAtUtc, Reason = "No coincide el importe." };
        var rejected = await data.Call(service => service.ReviewPaymentAsync(order.Id, false, rejection, data.UserId, Ct));
        Assert.Equal("Rejected", rejected.Order.PaymentStatus);
        Assert.Equal(rejection.Reason, rejected.Order.PaymentRejectionReason);
        await Fails(CheckoutError.Conflict, () => data.Step(rejected, "prepare"));
        var submitted = await data.Evidence(rejected);
        Assert.Equal("UnderReview", submitted.Order.PaymentStatus);
        Assert.Null(submitted.Order.PaymentRejectionReason);
        Assert.Equal(2, submitted.Operations.Evidence.Count);
        Assert.NotEqual(rejected.UpdatedAtUtc, submitted.UpdatedAtUtc);
    }

    [Fact]
    public async Task GuestEvidenceRequiresSecretAndDoesNotExposePrivateReferencesOrFiles()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, request) = await data.Confirmed();
        using var wrong = new MemoryStream(Receipt);
        await Fails(CheckoutError.NotFound, () => data.Call(service => service.SubmitGuestEvidenceAsync(new()
        { OrderNumber = order.Order.OrderNumber, TrackingToken = Token() }, new() { Reference = "SECRET-BANK-REFERENCE", UpdatedAtUtc = order.UpdatedAtUtc },
            wrong, wrong.Length, "image/png", Ct)));
        Assert.Empty(Directory.Exists(data.EvidenceDirectory) ? Directory.GetFiles(data.EvidenceDirectory) : []);
        using var image = new MemoryStream(Receipt);
        var result = await data.Call(service => service.SubmitGuestEvidenceAsync(new()
        { OrderNumber = order.Order.OrderNumber, TrackingToken = request.TrackingToken }, new() { Reference = "SECRET-BANK-REFERENCE", UpdatedAtUtc = order.UpdatedAtUtc },
            image, image.Length, "image/png", Ct));
        Assert.Equal("UnderReview", result.PaymentStatus);
        var json = JsonSerializer.Serialize(result);
        foreach (var secret in new[] { "SECRET-BANK-REFERENCE", request.Phone, request.CustomerName, "StoredFileName", "Evidence" })
            Assert.DoesNotContain(secret, json);
        var privateOrder = await data.Find(order.Order.OrderNumber);
        var evidence = Assert.Single(privateOrder.Operations.Evidence);
        var file = await data.Call(service => service.OpenEvidenceAsync(order.Id, evidence.Id, Ct));
        await using var stream = file.Stream;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        Assert.Equal(Receipt, copy.ToArray());
        Assert.Equal("image/png", file.ContentType);
        await Fails(CheckoutError.NotFound, () => data.Call(service => service.OpenEvidenceAsync(Guid.NewGuid(), evidence.Id, Ct)));
    }

    [Fact]
    public async Task InvalidOrRepeatedEvidenceAndStaleVersionsDoNotCreateFiles()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Confirmed();
        using var fake = new MemoryStream("not a PNG"u8.ToArray());
        await Fails(CheckoutError.Validation, () => data.Call(service => service.SubmitEvidenceAsync(order.Id, new()
        { Reference = "REF", UpdatedAtUtc = order.UpdatedAtUtc }, fake, fake.Length, "image/png", data.UserId, Ct)));
        using var large = new MemoryStream(Receipt);
        await Fails(CheckoutError.Validation, () => data.Call(service => service.SubmitEvidenceAsync(order.Id, new()
        { Reference = "REF", UpdatedAtUtc = order.UpdatedAtUtc }, large, 768001, "image/png", data.UserId, Ct)));
        var proof = await data.Evidence(order);
        await Fails(CheckoutError.Conflict, () => data.Evidence(order));
        await Fails(CheckoutError.Conflict, () => data.Evidence(proof));
        Assert.Single(Directory.GetFiles(data.EvidenceDirectory));
        Assert.Single(await data.Db.PaymentEvidence.ToListAsync());
    }

    [Fact]
    public async Task EvidenceFileIsRemovedWhenDatabaseSaveOrCommitFails()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Confirmed();
        await data.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectEvidence BEFORE INSERT ON PaymentEvidence BEGIN SELECT RAISE(ABORT, 'test'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => data.Evidence(order));
        Assert.Empty(Directory.GetFiles(data.EvidenceDirectory));
        await data.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectEvidence;");
        await Assert.ThrowsAsync<SqliteException>(async () =>
        {
            await using var context = data.NewContext();
            var store = new DeferredEvidenceStore(data.EvidenceStore, context);
            var service = new OrderService(context, new CheckoutSettingsService(context), true, store);
            using var file = new MemoryStream(Receipt);
            await service.SubmitEvidenceAsync(order.Id, new() { UpdatedAtUtc = order.UpdatedAtUtc, Reference = "REF" },
                file, file.Length, "image/png", Guid.NewGuid(), Ct);
        });
        Assert.Empty(Directory.GetFiles(data.EvidenceDirectory));
        Assert.Empty(await data.Db.PaymentEvidence.ToListAsync());
        Assert.Equal("Pending", (await data.Find(order.Order.OrderNumber)).Order.PaymentStatus);
    }

    [Fact]
    public async Task PartialReturnsCalculateHistoricalAmountsAndSeparateStockFromRefund()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, request) = await data.Delivered(quantity: 5);
        Assert.Equal(5, await data.Stock());
        var attempt = data.ReturnRequest(order, 2);
        var requested = await data.Call(service => service.CreateReturnAsync(order.Id, attempt, data.UserId, Ct));
        var returned = Assert.Single(requested.Operations.Returns);
        Assert.Equal(2000, returned.Summary.RefundAmount);
        Assert.Equal(5, await data.Stock());
        Assert.Equal(3, Assert.Single(requested.Operations.Lines).ReturnableQuantity);
        var repeat = await data.Call(service => service.CreateReturnAsync(order.Id, attempt, data.UserId, Ct));
        Assert.Single(repeat.Operations.Returns);
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CreateReturnAsync(order.Id, attempt with { Reason = "Cambio" }, data.UserId, Ct)));
        await Fails(CheckoutError.Conflict, () => data.Refund(requested, returned));
        var received = await data.Receive(requested, returned, good: 1);
        Assert.Equal(6, await data.Stock());
        Assert.Equal(0, received.Order.RefundedTotal);
        var refunded = await data.Refund(received, Assert.Single(received.Operations.Returns));
        Assert.Equal(2000, refunded.Order.RefundedTotal);
        Assert.Equal("Paid", refunded.Order.PaymentStatus);
        await data.Refund(refunded, Assert.Single(refunded.Operations.Returns));
        await data.Receive(refunded, Assert.Single(refunded.Operations.Returns), good: 1);
        Assert.Equal(6, await data.Stock());
        var secondRequest = data.ReturnRequest(refunded, 3);
        var second = await data.Call(service => service.CreateReturnAsync(order.Id, secondRequest, data.UserId, Ct));
        var remaining = second.Operations.Returns.Single(item => item.Summary.Id != returned.Summary.Id);
        second = await data.Receive(second, remaining, good: 2);
        second = await data.Refund(second, second.Operations.Returns.Single(item => item.Summary.Id == remaining.Summary.Id));
        Assert.Equal(5000, second.Order.RefundedTotal);
        Assert.Equal("Refunded", second.Order.PaymentStatus);
        Assert.Equal(8, await data.Stock());
        Assert.Equal(3, await data.Db.InventoryMovements.CountAsync());
        Assert.Equal(0, Assert.Single(second.Operations.Lines).ReturnableQuantity);
        var publicOrder = await data.Call(service => service.TrackAsync(new() { OrderNumber = order.Order.OrderNumber, TrackingToken = request.TrackingToken }, Ct));
        Assert.Equal(2, publicOrder.Returns.Count);
        Assert.DoesNotContain("REFUND-PRIVATE", JsonSerializer.Serialize(publicOrder));
        Assert.Equal("Delivered", publicOrder.Status);
    }

    [Fact]
    public async Task ReturnCannotReuseQuantitiesOrShippingAlreadyAllocatedAndRejectionReleasesThem()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Delivered("LocalDelivery", 5);
        var first = data.ReturnRequest(order, 2) with { ShippingRefundAmount = 2000 };
        order = await data.Call(service => service.CreateReturnAsync(order.Id, first, data.UserId, Ct));
        Assert.Equal(4000, Assert.Single(order.Operations.Returns).Summary.RefundAmount);
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(order, 4), data.UserId, Ct)));
        await Fails(CheckoutError.Validation, () => data.Call(service => service.CreateReturnAsync(order.Id,
            data.ReturnRequest(order, 1) with { ShippingRefundAmount = 3001 }, data.UserId, Ct)));
        var returned = Assert.Single(order.Operations.Returns);
        order = await data.Call(service => service.RejectReturnAsync(order.Id, returned.Summary.Id,
            new() { UpdatedAtUtc = order.UpdatedAtUtc, Reason = "Solicitud retirada antes de recibir." }, data.UserId, Ct));
        Assert.Equal(5, Assert.Single(order.Operations.Lines).ReturnableQuantity);
        Assert.Equal(5, await data.Stock());
        var next = data.ReturnRequest(order, 5) with { ShippingRefundAmount = 5000 };
        var created = await data.Call(service => service.CreateReturnAsync(order.Id, next, data.UserId, Ct));
        Assert.Equal(10000, created.Operations.Returns.Single(item => item.Summary.Status == "Requested").Summary.RefundAmount);
    }

    [Fact]
    public async Task ReturnReceiptRequiresPhysicalAttestationAndValidGoodQuantities()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Delivered();
        order = await data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(order, 2), data.UserId, Ct));
        var returned = Assert.Single(order.Operations.Returns);
        var line = Assert.Single(returned.Items);
        await Fails(CheckoutError.Validation, () => data.Call(service => service.ReceiveReturnAsync(order.Id, returned.Summary.Id, new()
        { UpdatedAtUtc = order.UpdatedAtUtc, Items = [new() { ReturnItemId = line.Id, RestockQuantity = 1 }] }, data.UserId, Ct)));
        await Fails(CheckoutError.Validation, () => data.Receive(order, returned, 3));
        var received = await data.Receive(order, returned, 1);
        await Fails(CheckoutError.Conflict, () => data.Receive(received, Assert.Single(received.Operations.Returns), 2));
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.RejectReturnAsync(order.Id, returned.Summary.Id,
            new() { UpdatedAtUtc = received.UpdatedAtUtc, Reason = "Tarde" }, data.UserId, Ct)));
        Assert.Equal(8, await data.Stock());
    }

    [Fact]
    public async Task ReturnReceiptOverflowAndStaleVersionsDoNotPartiallyRestock()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Delivered();
        order = await data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(order, 1), data.UserId, Ct));
        var returned = Assert.Single(order.Operations.Returns);
        await Fails(CheckoutError.Conflict, () => data.Receive(order with { UpdatedAtUtc = order.UpdatedAtUtc.AddSeconds(-1) }, returned, 1));
        await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity, int.MaxValue));
        await Fails(CheckoutError.Conflict, () => data.Receive(order, returned, 1));
        Assert.Equal(int.MaxValue, await data.Stock());
        Assert.Equal("Requested", Assert.Single((await data.Find(order.Order.OrderNumber)).Operations.Returns).Summary.Status);
        Assert.Single(await data.Db.InventoryMovements.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaidCancellationReleasesStockOnceAndCreatesASeparateRefundObligation(bool prepare)
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Confirmed("BankTransfer", "LocalDelivery");
        order = await data.Pay(await data.Evidence(order));
        if (prepare) order = await data.Step(await data.Step(order, "prepare"), "ready");
        order = await data.Call(service => service.CancelAsync(order.Id,
            new() { UpdatedAtUtc = order.UpdatedAtUtc, Reason = "Cancelacion antes de despachar" }, data.UserId, Ct));
        Assert.Equal(10, await data.Stock());
        Assert.Equal("Paid", order.Order.PaymentStatus);
        var returned = Assert.Single(order.Operations.Returns);
        Assert.Equal("Cancellation", returned.Summary.Kind);
        Assert.Equal("Received", returned.Summary.Status);
        Assert.Equal(8000, returned.Summary.RefundAmount);
        await Fails(CheckoutError.Conflict, () => data.Receive(order, returned, 1));
        var refunded = await data.Refund(order, returned);
        Assert.Equal("Refunded", refunded.Order.PaymentStatus);
        await data.Call(service => service.CancelAsync(order.Id, new(), data.UserId, Ct));
        Assert.Equal(10, await data.Stock());
        Assert.Equal(2, await data.Db.InventoryMovements.CountAsync());
        Assert.Single(await data.Db.OrderReturns.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedDeliveryMustReturnAllGoodsBeforeClosingAndNeverRefundsUncollectedCash(bool collected)
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Confirmed("CashOnDelivery", "CarrierShipping");
        order = await data.Step(await data.Step(await data.Step(order, "prepare"), "ready"), "ship");
        if (collected) order = await data.Pay(order);
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.CancelAsync(order.Id,
            new() { UpdatedAtUtc = order.UpdatedAtUtc, Reason = "En ruta" }, data.UserId, Ct)));
        await Fails(CheckoutError.Validation, () => data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(order, 1), data.UserId, Ct)));
        order = await data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(order, 3), data.UserId, Ct));
        var returned = Assert.Single(order.Operations.Returns);
        Assert.Equal(collected ? 3000 : 0, returned.Summary.RefundAmount);
        await Fails(CheckoutError.Conflict, () => data.Step(order, "deliver"));
        order = await data.Receive(order, returned, 2);
        Assert.Equal("Cancelled", order.Order.Status);
        Assert.Equal(9, await data.Stock());
        if (collected)
        {
            order = await data.Refund(order, Assert.Single(order.Operations.Returns));
            Assert.Equal(3000, order.Order.RefundedTotal);
        }
        else await Fails(CheckoutError.Validation, () => data.Refund(order, Assert.Single(order.Operations.Returns)));
    }

    [Fact]
    public async Task RefundRequiresExactAmountConfirmedExecutionAndActiveAdministrator()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Delivered();
        order = await data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(order, 1), data.UserId, Ct));
        order = await data.Receive(order, Assert.Single(order.Operations.Returns), 0);
        var returned = Assert.Single(order.Operations.Returns);
        await Fails(CheckoutError.Validation, () => data.Call(service => service.RefundReturnAsync(order.Id, returned.Summary.Id,
            new() { UpdatedAtUtc = order.UpdatedAtUtc, Amount = 1000, Reference = "REF" }, data.UserId, Ct)));
        await Fails(CheckoutError.Validation, () => data.Call(service => service.RefundReturnAsync(order.Id, returned.Summary.Id,
            new() { UpdatedAtUtc = order.UpdatedAtUtc, Amount = 999, Reference = "REF", RefundCompleted = true }, data.UserId, Ct)));
        await data.Db.Users.Where(user => user.Id == data.UserId).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        await Fails(CheckoutError.Forbidden, () => data.Refund(order, returned));
        Assert.Equal(0, (await data.Find(order.Order.OrderNumber)).Order.RefundedTotal);
    }

    [Fact]
    public async Task OperationalChangesRejectStaleVersionsAndTestDataOutsideDevelopment()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Confirmed();
        await Fails(CheckoutError.Conflict, () => data.Call(service => service.FulfillAsync(order.Id, "prepare",
            new() { UpdatedAtUtc = order.UpdatedAtUtc }, data.UserId, Ct), allowTests: false));
        order = await data.Pay(await data.Evidence(order));
        await Fails(CheckoutError.Conflict, () => data.Step(order with { UpdatedAtUtc = order.UpdatedAtUtc.AddSeconds(-1) }, "prepare"));
        Assert.Equal("Confirmed", (await data.Find(order.Order.OrderNumber)).Order.Status);
    }

    [Fact]
    public async Task MigrationUpgradePreservesExistingInventoryMovementsAndForeignKeys()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261019155446_CheckoutOrderSnapshots");
        var category = new Category { Name = "Anterior", Slug = "anterior" };
        var product = new Product { Category = category, Sku = "ANTERIOR", Slug = "anterior", Name = "Anterior", Description = "Anterior", Unit = "unidad", Price = 1000, StockQuantity = 7 };
        db.Products.Add(product);
        db.InventoryMovements.Add(new InventoryMovement { Product = product, MovementType = InventoryMovementType.PurchaseEntry, PreviousQuantity = 0, QuantityDelta = 7, NewQuantity = 7, Reason = "Movimiento anterior" });
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(7, (await db.Products.SingleAsync()).StockQuantity);
        Assert.Equal("Movimiento anterior", (await db.InventoryMovements.SingleAsync()).Reason);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check";
        using var reader = await command.ExecuteReaderAsync();
        Assert.False(await reader.ReadAsync());
    }

    private sealed class DeferredEvidenceStore(IPrivatePaymentEvidenceStore store, StoreDbContext context) : IPrivatePaymentEvidenceStore
    {
        public async Task<string> SaveAsync(Stream stream, long length, string contentType, CancellationToken ct)
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA defer_foreign_keys=ON", ct);
            return await store.SaveAsync(stream, length, contentType, ct);
        }
        public StoredImageFile Open(string name) => store.Open(name);
        public void Delete(string name) => store.Delete(name);
    }

    [Fact]
    public async Task PaymentDeliveryAndPendingReturnFiltersUseActualOperationalState()
    {
        await using var data = await TestOrders.CreateAsync();
        var (order, _) = await data.Delivered("CarrierShipping");
        Assert.Single((await data.Call(service => service.ListAsync(new() { PaymentStatus = "Paid", DeliveryMethod = "CarrierShipping" }, Ct))).Items);
        Assert.Empty((await data.Call(service => service.ListAsync(new() { PaymentStatus = "Pending" }, Ct))).Items);
        order = await data.Call(service => service.CreateReturnAsync(order.Id, data.ReturnRequest(order, 1), data.UserId, Ct));
        Assert.Single((await data.Call(service => service.ListAsync(new() { ReturnsPending = true }, Ct))).Items);
        Assert.Empty((await data.Call(service => service.ListAsync(new() { RefundsPending = true }, Ct))).Items);
        order = await data.Receive(order, Assert.Single(order.Operations.Returns), 1);
        Assert.Empty((await data.Call(service => service.ListAsync(new() { ReturnsPending = true }, Ct))).Items);
        Assert.Single((await data.Call(service => service.ListAsync(new() { RefundsPending = true }, Ct))).Items);
        await data.Refund(order, Assert.Single(order.Operations.Returns));
        Assert.Empty((await data.Call(service => service.ListAsync(new() { RefundsPending = true }, Ct))).Items);
    }

    [Fact]
    public async Task ConcurrentReturnReceiptAndRefundCannotRepeatStockOrMoney()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mercado-cercano-return-{Guid.NewGuid():N}.db");
        try
        {
            await using var data = await TestOrders.CreateAsync(filePath: path);
            var (order, _) = await data.Delivered();
            var request = data.ReturnRequest(order, 2);
            await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
                data.CrossCall(service => service.CreateReturnAsync(order.Id, request, data.UserId, Ct)))));
            order = await data.Find(order.Order.OrderNumber);
            var returned = Assert.Single(order.Operations.Returns);
            var receive = new ReceiveReturnRequest
            {
                UpdatedAtUtc = order.UpdatedAtUtc, ReceiptConfirmed = true,
                Items = [new() { ReturnItemId = returned.Items[0].Id, RestockQuantity = 1 }]
            };
            await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
                data.CrossCall(service => service.ReceiveReturnAsync(order.Id, returned.Summary.Id, receive, data.UserId, Ct)))));
            order = await data.Find(order.Order.OrderNumber);
            var refund = new RefundReturnRequest { UpdatedAtUtc = order.UpdatedAtUtc, Amount = 2000, Reference = "RACE-REFUND", RefundCompleted = true };
            await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
                data.CrossCall(service => service.RefundReturnAsync(order.Id, returned.Summary.Id, refund, data.UserId, Ct)))));
            Assert.Equal(8, await data.Stock());
            Assert.Single(await data.Db.OrderReturns.ToListAsync());
            Assert.Single(await data.Db.InventoryMovements.Where(item => item.MovementType == InventoryMovementType.CustomerReturn).ToListAsync());
            Assert.Single(await data.Db.AuditLogs.Where(item => item.Action == "Order.RefundRecorded").ToListAsync());
            Assert.Equal(2000, (await data.Find(order.Order.OrderNumber)).Order.RefundedTotal);
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }

    private sealed partial class TestOrders
    {
        public string EvidenceDirectory => evidenceDirectory;
        public async Task<T> CrossCall<T>(Func<OrderService, Task<T>> action)
        {
            await using var other = new SqliteConnection(connection.ConnectionString);
            await using var context = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(other).Options);
            return await action(new(context, new CheckoutSettingsService(context), true, EvidenceStore));
        }
        public async Task<Guid> Operator()
        {
            var id = Guid.NewGuid();
            Db.Users.Add(new User { Id = id, Name = "Operador", Email = $"{id:N}@example.test", NormalizedEmail = $"{id:N}@EXAMPLE.TEST", PasswordHash = "not-a-real-password", Role = UserRole.OrderOperator });
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return id;
        }
        public async Task<(AdminOrderResponse Order, CreateOrderRequest Request)> Confirmed(string method = "BankTransfer", string delivery = "CoordinatedPickup", int quantity = 3)
        {
            var request = await RequestAsync(Selection(quantity, delivery, delivery == "CoordinatedPickup" ? null : "Municipio de prueba") with { PaymentMethod = method });
            var response = await Call(service => service.CreateAsync(request, Ct));
            var admin = await Find(response.OrderNumber);
            return (await Call(service => service.ConfirmAsync(admin.Id, new() { UpdatedAtUtc = admin.UpdatedAtUtc }, UserId, Ct)), request);
        }
        public async Task<AdminOrderResponse> Evidence(AdminOrderResponse order)
        {
            using var file = new MemoryStream(Receipt);
            return await Call(service => service.SubmitEvidenceAsync(order.Id, new() { UpdatedAtUtc = order.UpdatedAtUtc, Reference = "PRIVATE-REFERENCE" },
                file, file.Length, "image/png", UserId, Ct));
        }
        public Task<AdminOrderResponse> Pay(AdminOrderResponse order, Guid? actor = null) => Call(service => service.ReviewPaymentAsync(order.Id, true, new()
        { UpdatedAtUtc = order.UpdatedAtUtc, Amount = order.Order.GrandTotal, Reference = "PRIVATE-REFERENCE", FundsVerified = true }, actor ?? UserId, Ct));
        public Task<AdminOrderResponse> Step(AdminOrderResponse order, string step) => Call(service => service.FulfillAsync(order.Id, step, new()
        { UpdatedAtUtc = order.UpdatedAtUtc, CarrierName = "Transportadora prueba", TrackingNumber = "GUIA-PRUEBA", DeliveryConfirmed = true }, UserId, Ct));
        public async Task<(AdminOrderResponse Order, CreateOrderRequest Request)> Delivered(string delivery = "CoordinatedPickup", int quantity = 3)
        {
            var (order, request) = await Confirmed(delivery: delivery, quantity: quantity);
            order = await Pay(await Evidence(order));
            order = await Step(await Step(order, "prepare"), "ready");
            if (delivery != "CoordinatedPickup") order = await Step(order, "ship");
            return (await Step(order, "deliver"), request);
        }
        public CreateReturnRequest ReturnRequest(AdminOrderResponse order, int quantity) => new()
        {
            UpdatedAtUtc = order.UpdatedAtUtc, RequestId = Guid.NewGuid(), Reason = "Devolucion coordinada con el cliente.",
            Items = [new() { OrderItemId = order.Operations.Lines[0].Id, Quantity = quantity }]
        };
        public Task<AdminOrderResponse> Receive(AdminOrderResponse order, AdminReturnResponse returned, int good) => Call(service =>
            service.ReceiveReturnAsync(order.Id, returned.Summary.Id, new()
            {
                UpdatedAtUtc = order.UpdatedAtUtc, ReceiptConfirmed = true,
                Items = returned.Items.Select(item => new ReceiveReturnLineRequest { ReturnItemId = item.Id, RestockQuantity = good }).ToArray()
            }, UserId, Ct));
        public Task<AdminOrderResponse> Refund(AdminOrderResponse order, AdminReturnResponse returned, Guid? actor = null) => Call(service =>
            service.RefundReturnAsync(order.Id, returned.Summary.Id, new()
            {
                UpdatedAtUtc = order.UpdatedAtUtc, RefundCompleted = true, Amount = returned.Summary.RefundAmount, Reference = "REFUND-PRIVATE"
            }, actor ?? UserId, Ct));
    }
}
