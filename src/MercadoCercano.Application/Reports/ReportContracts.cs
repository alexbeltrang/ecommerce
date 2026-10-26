using System.ComponentModel.DataAnnotations;
using MercadoCercano.Application.Catalog;

namespace MercadoCercano.Application.Reports;

/// <summary>Periodo inclusivo en Colombia, de hasta 366 dias, y conjunto comercial.</summary>
public sealed record ReportPeriodQuery
{
    [Required] public DateOnly? From { get; init; }
    [Required] public DateOnly? To { get; init; }
    [Required, RegularExpression("^(real|test|all)$")] public string DataSet { get; init; } = "real";
}

/// <summary>Contexto temporal de la consulta; importes agregados se transmiten como texto entero COP.</summary>
public sealed record ReportPeriodResponse(DateOnly From, DateOnly To, string TimeZone, string DataSet,
    DateTimeOffset GeneratedAtUtc, IReadOnlyList<string> Warnings);

/// <summary>Eventos comerciales y de dinero; ventas no equivalen a cobros.</summary>
public sealed record ReportMetricsResponse(long CreatedOrders, long DeliveredOrders, long FreeDeliveredOrders,
    string MerchandiseSales, string ShippingSales, string SalesTotal, string ProductDiscounts,
    string PromotionDiscounts, string Collections, string Refunds, string NetCash,
    string MerchandiseRefunds, string DeliveredCost, string RestockedCost, string EstimatedMargin);

/// <summary>Eventos agrupados por dia de Colombia, incluidos dias sin operaciones.</summary>
public sealed record ReportDayResponse(DateOnly Date, ReportMetricsResponse Metrics);

/// <summary>Estado actual de pedidos creados en el periodo, no historial de transiciones.</summary>
public sealed record ReportStatusResponse(string Status, long Orders, string OrderAmount);

/// <summary>Resumen y serie diaria de ventas, dinero y margen estimado.</summary>
public sealed record SalesReportResponse(ReportPeriodResponse Period, ReportMetricsResponse Totals,
    IReadOnlyList<ReportDayResponse> Days, IReadOnlyList<ReportStatusResponse> Statuses);

/// <summary>Rendimiento por producto usando nombres, importes y costos historicos.</summary>
public sealed record ProductReportRow(Guid? ProductId, string Sku, string Name, long DeliveredQuantity,
    long ReturnedQuantity, long RestockedQuantity, string MerchandiseSales, string MerchandiseRefunds,
    string NetMerchandise, string DeliveredCost, string RestockedCost, string EstimatedMargin);

/// <summary>Productos por eventos del periodo, paginados despues de aplicar busqueda y orden.</summary>
public sealed record ProductsReportResponse(ReportPeriodResponse Period, PageResponse<ProductReportRow> Rows);

/// <summary>Busqueda, alcance actual y pagina de inventario; no es un corte historico.</summary>
public sealed record InventoryReportQuery
{
    [StringLength(100)] public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    [Required, RegularExpression("^(all|low|out)$")] public string Stock { get; init; } = "all";
    [Required, RegularExpression("^(all|active|inactive)$")] public string Visibility { get; init; } = "all";
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 50)] public int PageSize { get; init; } = 12;
}

/// <summary>Existencias y costo actual, incluso si el producto no esta publicado.</summary>
public sealed record InventoryReportRow(Guid ProductId, string Sku, string Name, string Category,
    bool IsPublished, int Quantity, int LowStockThreshold, string UnitCost, string StockValue, string State);

/// <summary>Inventario actual con totales de todo el conjunto filtrado, no solo de la pagina.</summary>
public sealed record InventoryReportResponse(DateTimeOffset GeneratedAtUtc, long Products, long Units,
    long OutOfStock, long LowStock, string StockValue, PageResponse<InventoryReportRow> Rows);

/// <summary>Usos creados/liberados por fecha del evento y cupo actual de la regla.</summary>
public sealed record PromotionReportRow(Guid PromotionId, string Name, string? Code, bool IsActive,
    long UsesCreated, long UsesReleased, long CreatedUsesStillConsumed, string DiscountsGranted,
    string DiscountsReleased, int CurrentUsedCount, int? MaxUses);

/// <summary>Rendimiento de promociones, sin atribuir ventas que no se han entregado.</summary>
public sealed record PromotionsReportResponse(ReportPeriodResponse Period, PageResponse<PromotionReportRow> Rows);

/// <summary>Archivo CSV completo del conjunto filtrado, nunca solo la pagina visible.</summary>
public sealed record ReportCsvResponse(string FileName, byte[] Content);

public interface IReportService
{
    Task<SalesReportResponse> SalesAsync(ReportPeriodQuery query, CancellationToken ct);
    Task<ProductsReportResponse> ProductsAsync(ReportPeriodQuery query, string? search, string sort, int page, int pageSize, CancellationToken ct);
    Task<InventoryReportResponse> InventoryAsync(InventoryReportQuery query, CancellationToken ct);
    Task<PromotionsReportResponse> PromotionsAsync(ReportPeriodQuery query, string? search, int page, int pageSize, CancellationToken ct);
    Task<ReportCsvResponse> SalesCsvAsync(ReportPeriodQuery query, CancellationToken ct);
    Task<ReportCsvResponse> ProductsCsvAsync(ReportPeriodQuery query, string? search, string sort, CancellationToken ct);
    Task<ReportCsvResponse> InventoryCsvAsync(InventoryReportQuery query, CancellationToken ct);
    Task<ReportCsvResponse> PromotionsCsvAsync(ReportPeriodQuery query, string? search, CancellationToken ct);
}
