using System.ComponentModel.DataAnnotations;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Controllers;

[ApiController, Authorize(Policy = RolePolicies.Administrator), Route("api/v1/admin/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminReportsController(IReportService reports) : ControllerBase
{
    /// <summary>Ventas al entregar y flujo de dinero por fecha del evento en Colombia.</summary>
    [HttpGet("sales")]
    public async Task<ActionResult<SalesReportResponse>> Sales([FromQuery] ReportPeriodQuery query, CancellationToken ct) =>
        Ok(await reports.SalesAsync(query, ct));

    /// <summary>Productos, cantidades, devoluciones y margen estimado historico por eventos.</summary>
    [HttpGet("products")]
    public async Task<ActionResult<ProductsReportResponse>> Products([FromQuery] ReportPeriodQuery query, CancellationToken ct,
        [FromQuery, StringLength(100)] string? search = null, [FromQuery, RegularExpression("^(units|revenue|margin)$")] string sort = "units",
        [FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery, Range(1, 50)] int pageSize = 12) =>
        Ok(await reports.ProductsAsync(query, search, sort, page, pageSize, ct));

    /// <summary>Inventario actual filtrado con valoracion al costo y alertas.</summary>
    [HttpGet("inventory")]
    public async Task<ActionResult<InventoryReportResponse>> Inventory([FromQuery] InventoryReportQuery query, CancellationToken ct) =>
        Ok(await reports.InventoryAsync(query, ct));

    /// <summary>Redenciones y liberaciones de promociones por fecha, con contador global actual.</summary>
    [HttpGet("promotions")]
    public async Task<ActionResult<PromotionsReportResponse>> Promotions([FromQuery] ReportPeriodQuery query, CancellationToken ct,
        [FromQuery, StringLength(100)] string? search = null, [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 50)] int pageSize = 12) =>
        Ok(await reports.PromotionsAsync(query, search, page, pageSize, ct));

    /// <summary>Exporta resumen, todos los dias y estados del periodo a CSV.</summary>
    [HttpGet("sales.csv")]
    public async Task<FileContentResult> SalesCsv([FromQuery] ReportPeriodQuery query, CancellationToken ct) =>
        Download(await reports.SalesCsvAsync(query, ct));

    /// <summary>Exporta todos los productos filtrados, no solo la pagina visible.</summary>
    [HttpGet("products.csv")]
    public async Task<FileContentResult> ProductsCsv([FromQuery] ReportPeriodQuery query, CancellationToken ct,
        [FromQuery, StringLength(100)] string? search = null, [FromQuery, RegularExpression("^(units|revenue|margin)$")] string sort = "units") =>
        Download(await reports.ProductsCsvAsync(query, search, sort, ct));

    /// <summary>Exporta todas las existencias filtradas al momento de consultar.</summary>
    [HttpGet("inventory.csv")]
    public async Task<FileContentResult> InventoryCsv([FromQuery] InventoryReportQuery query, CancellationToken ct) =>
        Download(await reports.InventoryCsvAsync(query, ct));

    /// <summary>Exporta todos los usos promocionales agrupados y filtrados del periodo.</summary>
    [HttpGet("promotions.csv")]
    public async Task<FileContentResult> PromotionsCsv([FromQuery] ReportPeriodQuery query, CancellationToken ct,
        [FromQuery, StringLength(100)] string? search = null) =>
        Download(await reports.PromotionsCsvAsync(query, search, ct));

    private FileContentResult Download(ReportCsvResponse report)
    {
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(report.Content, "text/csv; charset=utf-8", report.FileName);
    }
}
