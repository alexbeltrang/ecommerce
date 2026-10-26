using System.Reflection;
using System.Text;
using System.Text.Json;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Api.Controllers;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Reports;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Tests;

public sealed partial class OrderServiceTests
{
    private static readonly DateTime ReportDay = new(2026, 10, 10, 5, 0, 0, DateTimeKind.Utc);
    private static ReportPeriodQuery ReportPeriod(string dataSet = "real") => new()
    { From = new(2026, 10, 10), To = new(2026, 10, 10), DataSet = dataSet };
    private static async Task<T> ReportCall<T>(TestOrders data, Func<ReportService, Task<T>> action)
    {
        await using var context = data.NewContext();
        return await action(new(context));
    }
    private static Order ReportOrder(TestOrders data, DateTime? delivered = null, DateTime? paid = null,
        DateTime? created = null, bool test = false, long amount = 1000, int quantity = 1,
        long discount = 0, long shipping = 0)
    {
        var order = new Order
        {
            OrderNumber = "REPORT-" + Guid.NewGuid().ToString("N"), CustomerNameSnapshot = "PRIVATE-NAME",
            CustomerPhoneSnapshot = "PRIVATE-PHONE", MunicipalitySnapshot = "Prueba", CustomerNotes = "PRIVATE-NOTES",
            Status = delivered.HasValue ? OrderStatus.Delivered : OrderStatus.PendingConfirmation,
            DeliveredAtUtc = delivered, CreatedAtUtc = created ?? ReportDay.AddDays(-5), IsTestOrder = test,
            PaymentMethod = PaymentMethod.BankTransfer, DeliveryMethod = DeliveryMethod.CoordinatedPickup,
            Subtotal = amount, DiscountTotal = discount, PromotionDiscountTotal = discount,
            GrandTotal = amount - discount + shipping, ShippingCost = shipping,
            PaymentStatus = paid.HasValue ? PaymentStatus.Paid : PaymentStatus.Pending
        };
        order.Items.Add(new OrderItem
        {
            ProductId = data.ProductId, ProductNameSnapshot = "Producto historico", SkuSnapshot = "HISTORICO",
            Quantity = quantity, UnitPrice = amount / quantity, LineTotal = amount, UnitCostSnapshot = 400,
            PromotionDiscountTotal = discount
        });
        order.Payments.Add(new Payment
        {
            Amount = order.GrandTotal, Method = order.PaymentMethod, Status = order.PaymentStatus, ConfirmedAtUtc = paid,
            Reference = "PRIVATE-PAYMENT"
        });
        data.Db.Orders.Add(order);
        return order;
    }
    private static OrderReturn ReportReturn(TestOrders data, Order order, DateTime received, DateTime? refunded,
        long amount, int quantity = 1, int restocked = 1, OrderReturnKind kind = OrderReturnKind.CustomerReturn)
    {
        var returned = new OrderReturn
        {
            Order = order, Kind = kind, Status = refunded.HasValue ? OrderReturnStatus.Refunded : OrderReturnStatus.Received,
            Reason = "Prueba", ReceivedAtUtc = received, RefundedAtUtc = refunded, RefundAmount = amount,
            CreatedAtUtc = received.AddHours(-1), RefundReference = "PRIVATE-REFUND"
        };
        returned.Items.Add(new OrderReturnItem
        { OrderItem = order.Items.Single(), Quantity = quantity, RestockedQuantity = restocked, RefundAmount = amount });
        data.Db.OrderReturns.Add(returned);
        return returned;
    }

    [Fact]
    public void ReportsAndCsvAreAdministratorOnlyAndNeverCached()
    {
        var type = typeof(AdminReportsController);
        Assert.Equal(RolePolicies.Administrator, type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.True(type.GetCustomAttribute<ResponseCacheAttribute>()?.NoStore);
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(8, methods.Length);
        Assert.All(methods, method =>
        {
            Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
            Assert.Contains(method.GetParameters(), item => item.ParameterType == typeof(CancellationToken));
        });
    }

    [Fact]
    public async Task ReportsSeparateCreationDeliveryCollectionsAndRefundDates()
    {
        await using var data = await TestOrders.CreateAsync();
        var delivered = ReportOrder(data, ReportDay.AddHours(3), ReportDay.AddDays(-1), shipping: 5000);
        ReportOrder(data, paid: ReportDay.AddHours(1), created: ReportDay.AddHours(2));
        var older = ReportOrder(data, ReportDay.AddDays(-2), ReportDay.AddDays(-3));
        ReportReturn(data, older, ReportDay.AddHours(4), ReportDay.AddHours(5), 1000);
        await data.Db.SaveChangesAsync();
        var result = await ReportCall(data, service => service.SalesAsync(ReportPeriod(), Ct));
        Assert.Equal(1, result.Totals.CreatedOrders);
        Assert.Equal(1, result.Totals.DeliveredOrders);
        Assert.Equal("6000", result.Totals.SalesTotal);
        Assert.Equal("1000", result.Totals.Collections);
        Assert.Equal("1000", result.Totals.Refunds);
        Assert.Equal("0", result.Totals.NetCash);
        Assert.Equal("1000", result.Totals.MerchandiseRefunds);
        Assert.Equal("400", result.Totals.DeliveredCost);
        Assert.Equal("400", result.Totals.RestockedCost);
        Assert.Equal("0", result.Totals.EstimatedMargin);
        Assert.Equal(result.Totals, Assert.Single(result.Days).Metrics);
        Assert.Single(result.Statuses, item => item.Orders == 1 && item.Status == "PendingConfirmation");
        Assert.Equal(6000, delivered.GrandTotal);
    }

    [Fact]
    public async Task ReportsRespectInclusiveColombiaDaysAndExclusiveFollowingMidnight()
    {
        await using var data = await TestOrders.CreateAsync();
        ReportOrder(data, ReportDay.AddTicks(-1), ReportDay.AddTicks(-1));
        ReportOrder(data, ReportDay, ReportDay);
        ReportOrder(data, ReportDay.AddDays(1).AddTicks(-1), ReportDay.AddDays(1).AddTicks(-1));
        ReportOrder(data, ReportDay.AddDays(1), ReportDay.AddDays(1));
        await data.Db.SaveChangesAsync();
        var report = await ReportCall(data, service => service.SalesAsync(ReportPeriod(), Ct));
        Assert.Equal(2, report.Totals.DeliveredOrders);
        Assert.Equal("2000", report.Totals.Collections);
        Assert.Equal("2000", report.Totals.SalesTotal);
        Assert.Contains("UTC-05:00", report.Period.TimeZone);
    }

    [Theory]
    [InlineData("real", 1, "1000")]
    [InlineData("test", 1, "2000")]
    [InlineData("all", 2, "3000")]
    public async Task ReportsFilterTestDataExplicitly(string dataSet, int count, string amount)
    {
        await using var data = await TestOrders.CreateAsync();
        ReportOrder(data, ReportDay, ReportDay);
        ReportOrder(data, ReportDay, ReportDay, test: true, amount: 2000);
        await data.Db.SaveChangesAsync();
        var report = await ReportCall(data, service => service.SalesAsync(ReportPeriod(dataSet), Ct));
        Assert.Equal(count, report.Totals.DeliveredOrders);
        Assert.Equal(amount, report.Totals.SalesTotal);
        var products = await ReportCall(data, service => service.ProductsAsync(ReportPeriod(dataSet), null, "units", 1, 12, Ct));
        Assert.Equal(amount, Assert.Single(products.Rows.Items).MerchandiseSales);
    }

    [Fact]
    public async Task ReportsSupportEmptyRangesZeroOrdersAndNegativeCashFlow()
    {
        await using var data = await TestOrders.CreateAsync();
        var empty = await ReportCall(data, service => service.SalesAsync(ReportPeriod() with { To = new(2026, 10, 12) }, Ct));
        Assert.Equal(3, empty.Days.Count);
        Assert.All(empty.Days, day => Assert.Equal("0", day.Metrics.SalesTotal));
        var order = ReportOrder(data, paid: ReportDay.AddDays(-2));
        order.Status = OrderStatus.Cancelled;
        ReportReturn(data, order, ReportDay, ReportDay, 1000, kind: OrderReturnKind.Cancellation);
        var free = ReportOrder(data, ReportDay, ReportDay, discount: 1000);
        free.Items.Single().UnitCostSnapshot = 400;
        await data.Db.SaveChangesAsync();
        var report = await ReportCall(data, service => service.SalesAsync(ReportPeriod(), Ct));
        Assert.Equal(1, report.Totals.FreeDeliveredOrders);
        Assert.Equal("0", report.Totals.Collections);
        Assert.Equal("-1000", report.Totals.NetCash);
        Assert.Equal("0", report.Totals.MerchandiseRefunds);
        Assert.Equal("-400", report.Totals.EstimatedMargin);
    }

    [Fact]
    public async Task ReportsAdjustMarginOnlyForActualPosSaleRefundsAndVendibleReceipts()
    {
        await using var data = await TestOrders.CreateAsync();
        var order = ReportOrder(data, ReportDay, ReportDay.AddDays(-1), amount: 3000, quantity: 3, discount: 1);
        ReportReturn(data, order, ReportDay.AddHours(1), ReportDay.AddHours(2), 999, restocked: 1);
        var damaged = ReportOrder(data, ReportDay.AddDays(-2), ReportDay.AddDays(-2));
        ReportReturn(data, damaged, ReportDay, ReportDay, 1000, restocked: 0);
        await data.Db.SaveChangesAsync();
        await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(item => item.Cost, 9999).SetProperty(item => item.Name, "Nuevo nombre"));
        var report = await ReportCall(data, service => service.SalesAsync(ReportPeriod(), Ct));
        Assert.Equal("2999", report.Totals.MerchandiseSales);
        Assert.Equal("1999", report.Totals.MerchandiseRefunds);
        Assert.Equal("1200", report.Totals.DeliveredCost);
        Assert.Equal("400", report.Totals.RestockedCost);
        Assert.Equal("200", report.Totals.EstimatedMargin);
        var products = await ReportCall(data, service => service.ProductsAsync(ReportPeriod(), null, "margin", 1, 12, Ct));
        var row = Assert.Single(products.Rows.Items);
        Assert.Equal("Producto historico", row.Name);
        Assert.Equal(3, row.DeliveredQuantity);
        Assert.Equal(2, row.ReturnedQuantity);
        Assert.Equal(1, row.RestockedQuantity);
        Assert.Equal("200", row.EstimatedMargin);
        Assert.Equal(report.Totals.EstimatedMargin, row.EstimatedMargin);
    }

    [Fact]
    public async Task ReportsUseReceiptAndRefundEventsIndependentlyAndIgnoreRejectedRequests()
    {
        await using var data = await TestOrders.CreateAsync();
        var older = ReportOrder(data, ReportDay.AddDays(-5), ReportDay.AddDays(-5));
        ReportReturn(data, older, ReportDay, ReportDay.AddDays(1), 1000);
        var rejected = ReportReturn(data, older, ReportDay, null, 1000);
        rejected.Status = OrderReturnStatus.Rejected;
        await data.Db.SaveChangesAsync();
        var report = await ReportCall(data, service => service.SalesAsync(ReportPeriod(), Ct));
        Assert.Equal("0", report.Totals.Refunds);
        Assert.Equal("400", report.Totals.RestockedCost);
        Assert.Equal("400", report.Totals.EstimatedMargin);
        var products = await ReportCall(data, service => service.ProductsAsync(ReportPeriod(), null, "units", 1, 12, Ct));
        Assert.Equal(1, Assert.Single(products.Rows.Items).RestockedQuantity);
    }

    [Fact]
    public async Task ReportsWarnAboutMissingHistoricalEventDatesRatherThanGuessing()
    {
        await using var data = await TestOrders.CreateAsync();
        var order = ReportOrder(data, created: ReportDay);
        order.Status = OrderStatus.Delivered;
        order.PaymentStatus = PaymentStatus.Paid;
        order.Payments.Single().Status = PaymentStatus.Paid;
        var returned = ReportReturn(data, order, ReportDay, null, 1000);
        returned.Status = OrderReturnStatus.Refunded;
        await data.Db.SaveChangesAsync();
        var report = await ReportCall(data, service => service.SalesAsync(ReportPeriod(), Ct));
        Assert.Equal(3, report.Period.Warnings.Count);
        Assert.Equal("0", report.Totals.Collections);
        Assert.Equal("0", report.Totals.SalesTotal);
        Assert.Equal("0", report.Totals.Refunds);
    }

    [Fact]
    public async Task ReportsPreserveAmountsBeyondJavaScriptPrecisionInJsonAndCsv()
    {
        await using var data = await TestOrders.CreateAsync();
        for (var i = 0; i < 1002; i++) ReportOrder(data, ReportDay, ReportDay, amount: 9_000_000_000_000);
        await data.Db.SaveChangesAsync();
        var result = await ReportCall(data, service => service.SalesAsync(ReportPeriod(), Ct));
        Assert.Equal("9018000000000000", result.Totals.SalesTotal);
        Assert.Contains("\"salesTotal\":\"9018000000000000\"", JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var csv = await ReportCall(data, service => service.SalesCsvAsync(ReportPeriod(), Ct));
        Assert.Contains("\"'9018000000000000\"", Encoding.UTF8.GetString(csv.Content));
    }

    [Theory]
    [InlineData("low", 1)]
    [InlineData("out", 1)]
    [InlineData("all", 3)]
    public async Task ReportsInventoryFiltersDistinguishZeroFromPositiveLowStock(string stock, int expected)
    {
        await using var data = await TestOrders.CreateAsync();
        var low = await data.AddProduct();
        var empty = await data.AddProduct();
        await data.Db.Products.Where(item => item.Id == low).ExecuteUpdateAsync(update => update.SetProperty(item => item.StockQuantity, 2).SetProperty(item => item.LowStockThreshold, 2));
        await data.Db.Products.Where(item => item.Id == empty).ExecuteUpdateAsync(update => update.SetProperty(item => item.StockQuantity, 0));
        var report = await ReportCall(data, service => service.InventoryAsync(new() { Stock = stock, PageSize = 1 }, Ct));
        Assert.Equal(expected, report.Products);
        Assert.Single(report.Rows.Items);
        Assert.Equal(expected, report.Rows.Total);
        if (stock == "all") { Assert.Equal(1, report.OutOfStock); Assert.Equal(1, report.LowStock); Assert.Equal("4800", report.StockValue); }
    }

    [Fact]
    public async Task ReportsInventoryUsesEffectivePublicationLiteralSearchAndBigIntegerValuation()
    {
        await using var data = await TestOrders.CreateAsync();
        await data.Db.Products.ExecuteUpdateAsync(update => update.SetProperty(item => item.Name, "100%_literal")
            .SetProperty(item => item.Cost, 9_000_000_000_000).SetProperty(item => item.StockQuantity, int.MaxValue));
        var report = await ReportCall(data, service => service.InventoryAsync(new() { Search = "%_", Visibility = "active" }, Ct));
        Assert.Equal("19327352823000000000000", report.StockValue);
        await data.Db.Categories.ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        var active = await ReportCall(data, service => service.InventoryAsync(new() { Visibility = "active" }, Ct));
        Assert.Empty(active.Rows.Items);
        var inactive = await ReportCall(data, service => service.InventoryAsync(new() { Visibility = "inactive" }, Ct));
        Assert.False(Assert.Single(inactive.Rows.Items).IsPublished);
        var noCategory = await ReportCall(data, service => service.InventoryAsync(new() { CategoryId = Guid.NewGuid() }, Ct));
        Assert.Empty(noCategory.Rows.Items);
    }

    [Fact]
    public async Task ReportsPromotionReleasesCanBelongToUsesFromEarlierPeriods()
    {
        await using var data = await TestOrders.CreateAsync();
        var rule = new Promotion
        {
            Name = "Regla actual", Code = "REPORTE", DiscountType = DiscountType.Percentage, DiscountValue = 10,
            StartsAtUtc = ReportDay.AddDays(-20), EndsAtUtc = ReportDay.AddDays(20), UsedCount = 1
        };
        data.Db.Promotions.Add(rule);
        var first = ReportOrder(data);
        var second = ReportOrder(data);
        var test = ReportOrder(data, test: true);
        data.Db.PromotionUsages.AddRange(
            new PromotionUsage { Promotion = rule, Order = first, UsedAtUtc = ReportDay.AddDays(-1), ReleasedAtUtc = ReportDay, DiscountAmount = 100 },
            new PromotionUsage { Promotion = rule, Order = second, UsedAtUtc = ReportDay, DiscountAmount = 100 },
            new PromotionUsage { Promotion = rule, Order = test, UsedAtUtc = ReportDay, ReleasedAtUtc = ReportDay, DiscountAmount = 200 });
        await data.Db.SaveChangesAsync();
        var report = await ReportCall(data, service => service.PromotionsAsync(ReportPeriod(), "REPORTE", 1, 12, Ct));
        var row = Assert.Single(report.Rows.Items);
        Assert.Equal(1, row.UsesCreated);
        Assert.Equal(1, row.UsesReleased);
        Assert.Equal(1, row.CreatedUsesStillConsumed);
        Assert.Equal("100", row.DiscountsGranted);
        Assert.Equal("100", row.DiscountsReleased);
        var all = await ReportCall(data, service => service.PromotionsAsync(ReportPeriod("all"), null, 1, 12, Ct));
        Assert.Equal(2, Assert.Single(all.Rows.Items).UsesCreated);
        Assert.Equal(1, row.CurrentUsedCount);
    }

    [Fact]
    public async Task ReportsCsvExportsAllFilteredRowsWithBomEscapingAndNoBuyerData()
    {
        await using var data = await TestOrders.CreateAsync();
        await data.AddProduct();
        await data.AddProduct();
        await data.Db.Products.Where(item => item.Id == data.ProductId).ExecuteUpdateAsync(update =>
            update.SetProperty(item => item.Sku, "=1+1").SetProperty(item => item.Name, "Una \"prueba\";\r\notra"));
        ReportOrder(data, ReportDay, ReportDay);
        await data.Db.SaveChangesAsync();
        var paged = await ReportCall(data, service => service.InventoryAsync(new() { PageSize = 1 }, Ct));
        Assert.Single(paged.Rows.Items);
        var csv = await ReportCall(data, service => service.InventoryCsvAsync(new() { PageSize = 1 }, Ct));
        Assert.Equal(new byte[] { 239, 187, 191 }, csv.Content.Take(3).ToArray());
        var text = Encoding.UTF8.GetString(csv.Content);
        Assert.Contains("\"'=1+1\"", text);
        Assert.Contains("\"Una \"\"prueba\"\";\r\notra\"", text);
        Assert.Equal(3, text.Split("\"'400\"").Length - 1);
        var sales = Encoding.UTF8.GetString((await ReportCall(data, service => service.SalesCsvAsync(ReportPeriod(), Ct))).Content);
        var products = Encoding.UTF8.GetString((await ReportCall(data, service => service.ProductsCsvAsync(ReportPeriod(), null, "units", Ct))).Content);
        Assert.DoesNotContain("PRIVATE-", text + sales + products);
        var promo = await ReportCall(data, service => service.PromotionsCsvAsync(ReportPeriod(), null, Ct));
        Assert.EndsWith(".csv", promo.FileName);
    }

    [Fact]
    public async Task ReportsValidateDatesFiltersAndPageBoundaries()
    {
        await using var data = await TestOrders.CreateAsync();
        foreach (var invalid in new[]
        {
            ReportPeriod() with { From = null }, ReportPeriod() with { To = null },
            ReportPeriod() with { To = new(2026, 10, 9) }, ReportPeriod() with { DataSet = "unknown" },
            ReportPeriod() with { To = new(2027, 10, 11) }, ReportPeriod() with { To = DateOnly.MaxValue }
        })
        {
            var error = await Assert.ThrowsAsync<CatalogException>(() => ReportCall(data, service => service.SalesAsync(invalid, Ct)));
            Assert.Equal(CatalogError.Validation, error.Error);
        }
        var full = await ReportCall(data, service => service.SalesAsync(ReportPeriod() with { To = new(2027, 10, 10) }, Ct));
        Assert.Equal(366, full.Days.Count);
        await Assert.ThrowsAsync<CatalogException>(() => ReportCall(data, service => service.ProductsAsync(ReportPeriod(), null, "bad", 1, 12, Ct)));
        await Assert.ThrowsAsync<CatalogException>(() => ReportCall(data, service => service.ProductsAsync(ReportPeriod(), null, "units", 0, 12, Ct)));
        await Assert.ThrowsAsync<CatalogException>(() => ReportCall(data, service => service.InventoryAsync(new() { Stock = "bad" }, Ct)));
        await Assert.ThrowsAsync<CatalogException>(() => ReportCall(data, service => service.PromotionsAsync(ReportPeriod(), new string('x', 101), 1, 12, Ct)));
        var huge = await ReportCall(data, service => service.InventoryAsync(new() { Page = int.MaxValue, PageSize = 50 }, Ct));
        Assert.Empty(huge.Rows.Items);
    }

    [Fact]
    public async Task ReportsDoNotMutateOrdersInventoryPaymentsAuditOrUsageCounters()
    {
        await using var data = await TestOrders.CreateAsync();
        ReportOrder(data, ReportDay, ReportDay);
        await data.Db.SaveChangesAsync();
        var before = (await data.Db.Orders.AsNoTracking().SingleAsync()).UpdatedAtUtc;
        await ReportCall(data, service => service.SalesAsync(ReportPeriod(), Ct));
        await ReportCall(data, service => service.ProductsAsync(ReportPeriod(), null, "revenue", 1, 12, Ct));
        await ReportCall(data, service => service.InventoryAsync(new(), Ct));
        await ReportCall(data, service => service.PromotionsAsync(ReportPeriod(), null, 1, 12, Ct));
        Assert.Equal(before, (await data.Db.Orders.AsNoTracking().SingleAsync()).UpdatedAtUtc);
        Assert.Equal(10, await data.Stock());
        Assert.Single(await data.Db.Payments.ToListAsync());
        Assert.Empty(await data.Db.AuditLogs.ToListAsync());
        Assert.Empty(await data.Db.PromotionUsages.ToListAsync());
    }

    [Fact]
    public async Task ReportsRejectOversizedQueriesInsteadOfReturningTruncatedCsvOrTotals()
    {
        await using var data = await TestOrders.CreateAsync();
        var properties = data.Db.Model.FindEntityType(typeof(Product))!.GetProperties().Select(item => item.GetColumnName()).ToArray();
        var names = string.Join(", ", properties.Select(item => "\"" + item + "\""));
        var values = string.Join(", ", properties.Select(item => item switch
        {
            "Id" => "printf('%08x-0000-0000-0000-%012x', n, n)",
            "Sku" or "Slug" => "'limit-' || n",
            _ => "p.\"" + item + "\""
        }));
        await using var command = data.Db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $$"""
            WITH RECURSIVE counter(n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM counter WHERE n < 50000)
            INSERT INTO Products ({{names}}) SELECT {{values}} FROM Products p CROSS JOIN counter WHERE p.Id = $product
            """;
        var productParameter = command.CreateParameter();
        productParameter.ParameterName = "$product";
        productParameter.Value = data.ProductId;
        command.Parameters.Add(productParameter);
        await command.ExecuteNonQueryAsync();
        Assert.Equal(50001, await data.Db.Products.CountAsync());
        var error = await Assert.ThrowsAsync<CatalogException>(() => ReportCall(data, service => service.InventoryAsync(new(), Ct)));
        Assert.Contains("50.000", error.Message);
        await Assert.ThrowsAsync<CatalogException>(() => ReportCall(data, service => service.InventoryCsvAsync(new(), Ct)));
        Assert.Equal(1, await data.Db.Products.Where(item => item.Id == data.ProductId).ExecuteDeleteAsync());
        var exactLimit = await ReportCall(data, service => service.InventoryAsync(new() { PageSize = 1 }, Ct));
        Assert.Equal(50000, exactLimit.Rows.Total);
        Assert.Single(exactLimit.Rows.Items);
        var fullCsv = await ReportCall(data, service => service.InventoryCsvAsync(new(), Ct));
        Assert.Equal(50000, Encoding.UTF8.GetString(fullCsv.Content).Split("\"'400\"").Length - 1);
    }

    [Fact]
    public async Task ReportsWorkOnQueryOnlySqliteWithoutReservingWriterTransactions()
    {
        await using var data = await TestOrders.CreateAsync();
        ReportOrder(data, ReportDay, ReportDay);
        await data.Db.SaveChangesAsync();
        await data.Db.Database.ExecuteSqlRawAsync("PRAGMA query_only = ON;");
        await ReportCall(data, service => service.SalesAsync(ReportPeriod(), Ct));
        await ReportCall(data, service => service.ProductsAsync(ReportPeriod(), null, "units", 1, 12, Ct));
        await ReportCall(data, service => service.InventoryAsync(new(), Ct));
        await ReportCall(data, service => service.PromotionsAsync(ReportPeriod(), null, 1, 12, Ct));
        await ReportCall(data, service => service.SalesCsvAsync(ReportPeriod(), Ct));
        await ReportCall(data, service => service.ProductsCsvAsync(ReportPeriod(), null, "units", Ct));
        await ReportCall(data, service => service.InventoryCsvAsync(new(), Ct));
        await ReportCall(data, service => service.PromotionsCsvAsync(ReportPeriod(), null, Ct));
    }

    [Fact]
    public async Task ReportsSeparateProductOffersFromAdditionalPromotionsAndCountRefundedPayments()
    {
        await using var data = await TestOrders.CreateAsync();
        var order = ReportOrder(data, ReportDay, ReportDay, amount: 3000, quantity: 3, discount: 200);
        order.Items.Single().UnitPrice = 800;
        order.Items.Single().LineTotal = 2400;
        order.DiscountTotal = 800;
        order.GrandTotal = 2200;
        order.Payments.Single().Amount = 2200;
        order.Payments.Single().Status = PaymentStatus.Refunded;
        order.PaymentStatus = PaymentStatus.Refunded;
        ReportReturn(data, order, ReportDay, ReportDay, 2200, quantity: 3, restocked: 3);
        await data.Db.SaveChangesAsync();
        var report = await ReportCall(data, service => service.SalesAsync(ReportPeriod(), Ct));
        Assert.Equal("600", report.Totals.ProductDiscounts);
        Assert.Equal("200", report.Totals.PromotionDiscounts);
        Assert.Equal("2200", report.Totals.Collections);
        Assert.Equal("2200", report.Totals.Refunds);
        Assert.Equal("0", report.Totals.EstimatedMargin);
    }

    [Fact]
    public async Task ReportsExcludeShippingRefundsFromMerchandiseMargin()
    {
        await using var data = await TestOrders.CreateAsync();
        var order = ReportOrder(data, ReportDay.AddDays(-1), ReportDay.AddDays(-1), discount: 1000, shipping: 5000);
        var returned = ReportReturn(data, order, ReportDay, ReportDay, 5000, restocked: 0);
        returned.ShippingRefundAmount = 5000;
        returned.Items.Single().RefundAmount = 0;
        await data.Db.SaveChangesAsync();
        var report = await ReportCall(data, service => service.SalesAsync(ReportPeriod(), Ct));
        Assert.Equal("5000", report.Totals.Refunds);
        Assert.Equal("-5000", report.Totals.NetCash);
        Assert.Equal("0", report.Totals.MerchandiseRefunds);
        Assert.Equal("0", report.Totals.EstimatedMargin);
    }
}
