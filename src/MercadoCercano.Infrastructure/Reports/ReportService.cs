using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Numerics;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Reports;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MercadoCercano.Infrastructure.Reports;

public sealed partial class ReportService(StoreDbContext db) : IReportService
{
    private const int MaximumRows = 50_000;
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);
    private sealed class ReadSnapshot(StoreDbContext context) : IAsyncDisposable
    {
        private SqliteTransaction? transaction;
        private IDbContextTransaction? enlisted;
        public async Task Open(CancellationToken ct)
        {
            await context.Database.OpenConnectionAsync(ct);
            // A deferred snapshot does not reserve SQLite's writer lock.
            transaction = ((SqliteConnection)context.Database.GetDbConnection()).BeginTransaction(deferred: true);
            enlisted = await context.Database.UseTransactionAsync(transaction, ct);
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (enlisted is not null) await enlisted.DisposeAsync();
            }
            finally
            {
                try
                {
                    if (transaction is not null) await transaction.DisposeAsync();
                }
                finally { await context.Database.CloseConnectionAsync(); }
            }
        }
    }
    private sealed record Window(DateOnly From, DateOnly To, DateTime Start, DateTime End)
    {
        public bool Contains(DateTime? value) => value >= Start && value < End;
    }
    private sealed record Sale(Guid Id, DateTime Created, DateTime? Delivered, OrderStatus Status,
        long Subtotal, long Discount, long PromotionDiscount, long Shipping, long Total);
    private sealed record Line(Guid Id, Guid OrderId, Guid? ProductId, string Sku, string Name, int Quantity, long Cost, long Net);
    private sealed record Cash(DateTime? Date, long Amount);
    private sealed record Returned(Guid Id, Guid OrderId, OrderReturnKind Kind, OrderReturnStatus Status,
        DateTime? Received, DateTime? Refunded, long Amount, long Shipping);
    private sealed record ReturnedLine(Guid ReturnId, Guid LineId, int Quantity, int Restocked, long Amount);
    private sealed record Snapshot(Window Window, ReportPeriodResponse Period, Sale[] Orders, Line[] Lines,
        Cash[] Payments, Returned[] Returns, ReturnedLine[] ReturnLines);

    private IQueryable<Order> Orders(string dataSet) => dataSet switch
    {
        "test" => db.Orders.AsNoTracking().Where(item => item.IsTestOrder),
        "real" => db.Orders.AsNoTracking().Where(item => !item.IsTestOrder),
        "all" => db.Orders.AsNoTracking(),
        _ => throw Invalid("Selecciona datos reales, de prueba o todos.")
    };

    private static Window Period(ReportPeriodQuery query)
    {
        Validate(query);
        if (!query.From.HasValue || !query.To.HasValue || query.To < query.From ||
            query.To.Value.DayNumber - query.From.Value.DayNumber >= 366 || query.To == DateOnly.MaxValue ||
            query.From.Value.Year < 1900 || query.To.Value.Year > 9998)
            throw Invalid("Selecciona un periodo inclusivo de 1 a 366 dias, desde el ano 1900 hasta 9998.");
        var start = query.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var end = query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new(query.From.Value, query.To.Value,
            new DateTimeOffset(start, ColombiaOffset).UtcDateTime, new DateTimeOffset(end, ColombiaOffset).UtcDateTime);
    }

    private async Task<ReportPeriodResponse> Metadata(ReportPeriodQuery query, Window window, CancellationToken ct)
    {
        var orders = Orders(query.DataSet);
        var warnings = new List<string>();
        if (await orders.AnyAsync(order => order.Status == OrderStatus.Delivered && order.DeliveredAtUtc == null, ct))
            warnings.Add("Hay pedidos entregados sin fecha de entrega. No se pueden asignar sus ventas a un periodo.");
        if (await orders.AnyAsync(order => order.Payments.Any(payment => payment.Amount > 0 &&
            (payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.Refunded) && payment.ConfirmedAtUtc == null), ct))
            warnings.Add("Hay pagos resueltos sin fecha de confirmacion. Esos cobros no se incluyen en los eventos del periodo.");
        if (await orders.AnyAsync(order => order.Returns.Any(item => item.Status == OrderReturnStatus.Refunded && item.RefundedAtUtc == null), ct))
            warnings.Add("Hay reembolsos sin fecha de registro. Esos importes no se incluyen en el periodo.");
        return new(window.From, window.To, "America/Bogota (UTC-05:00)", query.DataSet, DateTimeOffset.UtcNow, warnings);
    }

    private async Task<Snapshot> Load(ReportPeriodQuery query, CancellationToken ct)
    {
        var window = Period(query);
        var source = Orders(query.DataSet).Where(order =>
            (order.CreatedAtUtc >= window.Start && order.CreatedAtUtc < window.End) ||
            (order.DeliveredAtUtc >= window.Start && order.DeliveredAtUtc < window.End) ||
            order.Payments.Any(payment => payment.ConfirmedAtUtc >= window.Start && payment.ConfirmedAtUtc < window.End) ||
            order.Returns.Any(item => (item.ReceivedAtUtc >= window.Start && item.ReceivedAtUtc < window.End) ||
                (item.RefundedAtUtc >= window.Start && item.RefundedAtUtc < window.End)));
        var orders = await Bounded(source.OrderBy(item => item.Id).Select(item => new Sale(item.Id, item.CreatedAtUtc,
            item.DeliveredAtUtc, item.Status, item.Subtotal, item.DiscountTotal, item.PromotionDiscountTotal, item.ShippingCost, item.GrandTotal)), ct);
        var ids = source.Select(item => item.Id);
        var lines = await Bounded(db.OrderItems.AsNoTracking().Where(item => ids.Contains(item.OrderId)).OrderBy(item => item.Id)
            .Select(item => new Line(item.Id, item.OrderId, item.ProductId, item.SkuSnapshot, item.ProductNameSnapshot,
                item.Quantity, item.UnitCostSnapshot, item.LineTotal - item.PromotionDiscountTotal)), ct);
        var payments = await Bounded(db.Payments.AsNoTracking().Where(item => ids.Contains(item.OrderId) &&
            item.Amount > 0 && (item.Status == PaymentStatus.Paid || item.Status == PaymentStatus.Refunded) &&
            item.ConfirmedAtUtc >= window.Start && item.ConfirmedAtUtc < window.End).OrderBy(item => item.Id)
            .Select(item => new Cash(item.ConfirmedAtUtc, item.Amount)), ct);
        var returnQuery = db.OrderReturns.AsNoTracking().Where(item => ids.Contains(item.OrderId) &&
            item.Status != OrderReturnStatus.Rejected &&
            ((item.ReceivedAtUtc >= window.Start && item.ReceivedAtUtc < window.End) ||
             (item.RefundedAtUtc >= window.Start && item.RefundedAtUtc < window.End)));
        var returns = await Bounded(returnQuery.OrderBy(item => item.Id).Select(item => new Returned(item.Id, item.OrderId,
            item.Kind, item.Status, item.ReceivedAtUtc, item.RefundedAtUtc, item.RefundAmount, item.ShippingRefundAmount)), ct);
        var returnIds = returnQuery.Select(item => item.Id);
        var returnLines = await Bounded(db.OrderReturnItems.AsNoTracking().Where(item => returnIds.Contains(item.OrderReturnId))
            .OrderBy(item => item.Id).Select(item => new ReturnedLine(item.OrderReturnId, item.OrderItemId, item.Quantity, item.RestockedQuantity, item.RefundAmount)), ct);
        return new(window, await Metadata(query, window, ct), orders, lines, payments, returns, returnLines);
    }

    public async Task<SalesReportResponse> SalesAsync(ReportPeriodQuery query, CancellationToken ct)
    {
        await using var transaction = new ReadSnapshot(db);
        await transaction.Open(ct);
        var data = await Load(query, ct);
        var days = Enumerable.Range(0, data.Window.To.DayNumber - data.Window.From.DayNumber + 1)
            .Select(offset => data.Window.From.AddDays(offset)).Select(day =>
                new ReportDayResponse(day, Metrics(data, new(day, day,
                    new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), ColombiaOffset).UtcDateTime,
                    new DateTimeOffset(day.AddDays(1).ToDateTime(TimeOnly.MinValue), ColombiaOffset).UtcDateTime)))).ToArray();
        var statuses = Enum.GetValues<OrderStatus>().Select(status =>
        {
            var selected = data.Orders.Where(item => data.Window.Contains(item.Created) && item.Status == status).ToArray();
            return new ReportStatusResponse(status.ToString(), selected.LongLength, Money(Sum(selected, item => item.Total)));
        }).ToArray();
        return new(data.Period, Metrics(data, data.Window), days, statuses);
    }

    private static ReportMetricsResponse Metrics(Snapshot data, Window window)
    {
        var delivered = data.Orders.Where(item => window.Contains(item.Delivered)).ToArray();
        var deliveredIds = delivered.Select(item => item.Id).ToHashSet();
        var deliveredLines = data.Lines.Where(item => deliveredIds.Contains(item.OrderId)).ToArray();
        var customerOrderIds = data.Orders.Where(item => item.Delivered.HasValue).Select(item => item.Id).ToHashSet();
        var refunded = data.Returns.Where(item => item.Status == OrderReturnStatus.Refunded && window.Contains(item.Refunded)).ToArray();
        var customerRefundIds = refunded.Where(item => item.Kind == OrderReturnKind.CustomerReturn && customerOrderIds.Contains(item.OrderId))
            .Select(item => item.Id).ToHashSet();
        var receivedIds = data.Returns.Where(item => item.Kind == OrderReturnKind.CustomerReturn &&
            customerOrderIds.Contains(item.OrderId) && window.Contains(item.Received)).Select(item => item.Id).ToHashSet();
        var lines = data.Lines.ToDictionary(item => item.Id);
        var merchandise = Sum(deliveredLines, item => item.Net);
        var salesCost = Sum(deliveredLines, item => (BigInteger)item.Cost * item.Quantity);
        var goodsRefunds = Sum(data.ReturnLines.Where(item => customerRefundIds.Contains(item.ReturnId)), item => item.Amount);
        var recoveredCost = Sum(data.ReturnLines.Where(item => receivedIds.Contains(item.ReturnId)),
            item => (BigInteger)lines[item.LineId].Cost * item.Restocked);
        var collections = Sum(data.Payments.Where(item => window.Contains(item.Date)), item => item.Amount);
        var refunds = Sum(refunded, item => item.Amount);
        var shipping = Sum(delivered, item => item.Shipping);
        return new(data.Orders.LongCount(item => window.Contains(item.Created)), delivered.LongLength,
            delivered.LongCount(item => item.Total == 0), Money(merchandise), Money(shipping), Money(merchandise + shipping),
            Money(Sum(delivered, item => (BigInteger)item.Discount - item.PromotionDiscount)),
            Money(Sum(delivered, item => item.PromotionDiscount)), Money(collections), Money(refunds), Money(collections - refunds),
            Money(goodsRefunds), Money(salesCost), Money(recoveredCost), Money(merchandise - goodsRefunds - salesCost + recoveredCost));
    }

    public async Task<ProductsReportResponse> ProductsAsync(ReportPeriodQuery query, string? search, string sort, int page, int pageSize, CancellationToken ct)
    {
        Pagination(page, pageSize); Search(search);
        await using var transaction = new ReadSnapshot(db);
        await transaction.Open(ct);
        var data = await Load(query, ct);
        var rows = ProductRows(data, search, sort);
        return new(data.Period, Page(rows, page, pageSize));
    }

    private static ProductReportRow[] ProductRows(Snapshot data, string? search, string sort)
    {
        if (sort is not ("units" or "revenue" or "margin")) throw Invalid("El orden de productos no es valido.");
        var delivered = data.Orders.Where(item => data.Window.Contains(item.Delivered)).Select(item => item.Id).ToHashSet();
        var customerOrders = data.Orders.Where(item => item.Delivered.HasValue).Select(item => item.Id).ToHashSet();
        var refunds = data.Returns.Where(item => item.Kind == OrderReturnKind.CustomerReturn && customerOrders.Contains(item.OrderId) &&
            item.Status == OrderReturnStatus.Refunded && data.Window.Contains(item.Refunded)).Select(item => item.Id).ToHashSet();
        var receipts = data.Returns.Where(item => item.Kind == OrderReturnKind.CustomerReturn && customerOrders.Contains(item.OrderId) &&
            data.Window.Contains(item.Received)).Select(item => item.Id).ToHashSet();
        var byLine = data.ReturnLines.ToLookup(item => item.LineId);
        var dates = data.Orders.ToDictionary(item => item.Id, item => item.Delivered ?? item.Created);
        var relevant = data.Lines.Where(item => delivered.Contains(item.OrderId) ||
            byLine[item.Id].Any(line => refunds.Contains(line.ReturnId) || receipts.Contains(line.ReturnId)));
        var rows = relevant.GroupBy(item => item.ProductId.HasValue ? item.ProductId.Value.ToString("N") : "sku:" + item.Sku)
            .Select(group =>
            {
                var latest = group.OrderByDescending(item => dates[item.OrderId]).ThenBy(item => item.Id).First();
                var shipped = group.Where(item => delivered.Contains(item.OrderId)).ToArray();
                var returned = group.SelectMany(item => byLine[item.Id]).ToArray();
                var amount = Sum(shipped, item => item.Net);
                var refund = Sum(returned.Where(item => refunds.Contains(item.ReturnId)), item => item.Amount);
                var cost = Sum(shipped, item => (BigInteger)item.Cost * item.Quantity);
                var recovered = Sum(group, item => Sum(byLine[item.Id].Where(line => receipts.Contains(line.ReturnId)),
                    line => (BigInteger)item.Cost * line.Restocked));
                return new ProductReportRow(latest.ProductId, latest.Sku, latest.Name, shipped.Sum(item => (long)item.Quantity),
                    returned.Where(item => receipts.Contains(item.ReturnId)).Sum(item => (long)item.Quantity),
                    returned.Where(item => receipts.Contains(item.ReturnId)).Sum(item => (long)item.Restocked),
                    Money(amount), Money(refund), Money(amount - refund), Money(cost), Money(recovered), Money(amount - refund - cost + recovered));
            }).Where(item => Matches(search, item.Name, item.Sku));
        return (sort switch
        {
            "revenue" => rows.OrderByDescending(item => Parse(item.NetMerchandise)),
            "margin" => rows.OrderByDescending(item => Parse(item.EstimatedMargin)),
            _ => rows.OrderByDescending(item => item.DeliveredQuantity)
        }).ThenBy(item => item.Sku, StringComparer.Ordinal).ThenBy(item => item.ProductId).ToArray();
    }

    public async Task<InventoryReportResponse> InventoryAsync(InventoryReportQuery query, CancellationToken ct)
    {
        Validate(query);
        await using var transaction = new ReadSnapshot(db);
        await transaction.Open(ct);
        var rows = await InventoryRows(query, ct);
        return new(DateTimeOffset.UtcNow, rows.LongLength, rows.Sum(item => (long)item.Quantity),
            rows.LongCount(item => item.Quantity == 0), rows.LongCount(item => item.State == "Low"),
            Money(Sum(rows, item => Parse(item.StockValue))), Page(rows, query.Page, query.PageSize));
    }

    private async Task<InventoryReportRow[]> InventoryRows(InventoryReportQuery query, CancellationToken ct)
    {
        Validate(query);
        var source = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + query.Search.Trim().Replace("/", "//").Replace("%", "/%").Replace("_", "/_") + "%";
            source = source.Where(item => EF.Functions.Like(item.Name, pattern, "/") || EF.Functions.Like(item.Sku, pattern, "/"));
        }
        if (query.CategoryId.HasValue) source = source.Where(item => item.CategoryId == query.CategoryId);
        if (query.Visibility == "active") source = source.Where(item => item.IsActive && item.Category.IsActive);
        if (query.Visibility == "inactive") source = source.Where(item => !item.IsActive || !item.Category.IsActive);
        if (query.Stock == "out") source = source.Where(item => item.StockQuantity == 0);
        if (query.Stock == "low") source = source.Where(item => item.StockQuantity > 0 && item.StockQuantity <= item.LowStockThreshold);
        var rows = await Bounded(source.OrderBy(item => item.Name).ThenBy(item => item.Id).Select(item => new
        {
            item.Id, item.Sku, item.Name, Category = item.Category.Name, Published = item.IsActive && item.Category.IsActive,
            Quantity = item.StockQuantity, item.LowStockThreshold, item.Cost
        }), ct);
        return rows.Select(item => new InventoryReportRow(item.Id, item.Sku, item.Name, item.Category, item.Published,
            item.Quantity, item.LowStockThreshold, Money(item.Cost), Money((BigInteger)item.Cost * item.Quantity),
            item.Quantity == 0 ? "Out" : item.Quantity <= item.LowStockThreshold ? "Low" : "Available")).ToArray();
    }

    public async Task<PromotionsReportResponse> PromotionsAsync(ReportPeriodQuery query, string? search, int page, int pageSize, CancellationToken ct)
    {
        Pagination(page, pageSize); Search(search);
        var window = Period(query);
        await using var transaction = new ReadSnapshot(db);
        await transaction.Open(ct);
        var rows = await PromotionRows(query, window, search, ct);
        return new(await Metadata(query, window, ct), Page(rows, page, pageSize));
    }

    private async Task<PromotionReportRow[]> PromotionRows(ReportPeriodQuery query, Window window, string? search, CancellationToken ct)
    {
        var source = db.PromotionUsages.AsNoTracking().Where(item =>
            (item.UsedAtUtc >= window.Start && item.UsedAtUtc < window.End) ||
            (item.ReleasedAtUtc >= window.Start && item.ReleasedAtUtc < window.End));
        if (query.DataSet == "real") source = source.Where(item => !item.Order.IsTestOrder);
        if (query.DataSet == "test") source = source.Where(item => item.Order.IsTestOrder);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = "%" + search.Trim().Replace("/", "//").Replace("%", "/%").Replace("_", "/_") + "%";
            source = source.Where(item => EF.Functions.Like(item.Promotion.Name, pattern, "/") ||
                (item.Promotion.Code != null && EF.Functions.Like(item.Promotion.Code, pattern, "/")));
        }
        var rows = await Bounded(source.OrderBy(item => item.Id).Select(item => new
        {
            item.PromotionId, item.Promotion.Name, item.Promotion.Code, item.Promotion.IsActive,
            item.Promotion.UsedCount, item.Promotion.MaxUses, item.UsedAtUtc, item.ReleasedAtUtc, item.DiscountAmount
        }), ct);
        return rows.GroupBy(item => item.PromotionId).Select(group =>
        {
            var rule = group.First();
            var created = group.Where(item => window.Contains(item.UsedAtUtc)).ToArray();
            var released = group.Where(item => window.Contains(item.ReleasedAtUtc)).ToArray();
            return new PromotionReportRow(rule.PromotionId, rule.Name, rule.Code, rule.IsActive, created.LongLength,
                released.LongLength, created.LongCount(item => !item.ReleasedAtUtc.HasValue),
                Money(Sum(created, item => item.DiscountAmount)), Money(Sum(released, item => item.DiscountAmount)), rule.UsedCount, rule.MaxUses);
        }).OrderByDescending(item => item.UsesCreated).ThenBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.PromotionId).ToArray();
    }

    private static async Task<T[]> Bounded<T>(IQueryable<T> source, CancellationToken ct)
    {
        var rows = await source.Take(MaximumRows + 1).ToArrayAsync(ct);
        if (rows.Length > MaximumRows) throw Invalid("La consulta supera 50.000 registros. Reduce el periodo o los filtros; no se entrega un reporte incompleto.");
        return rows;
    }
    private static PageResponse<T> Page<T>(T[] rows, int page, int size)
    {
        var offset = (long)(page - 1) * size;
        return new(offset >= rows.LongLength ? [] : rows.Skip((int)offset).Take(size).ToArray(), rows.Length, page, size);
    }
    private static void Pagination(int page, int size)
    {
        if (page < 1 || size is < 1 or > 50) throw Invalid("Selecciona pagina positiva y entre 1 y 50 filas por pagina.");
    }
    private static void Search(string? search)
    {
        if (search?.Length > 100) throw Invalid("La busqueda permite hasta 100 caracteres.");
    }
    private static bool Matches(string? search, params string[] values) => string.IsNullOrWhiteSpace(search) ||
        values.Any(value => value.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
    private static BigInteger Sum<T>(IEnumerable<T> items, Func<T, BigInteger> value) =>
        items.Aggregate(BigInteger.Zero, (total, item) => total + value(item));
    private static string Money(BigInteger value) => value.ToString(CultureInfo.InvariantCulture);
    private static BigInteger Parse(string value) => BigInteger.Parse(value, CultureInfo.InvariantCulture);
    private static void Validate(object query)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(query, new(query), errors, true))
            throw Invalid(string.Join(" ", errors.Select(item => item.ErrorMessage)));
    }
    private static CatalogException Invalid(string message) => new(CatalogError.Validation, message);
}
