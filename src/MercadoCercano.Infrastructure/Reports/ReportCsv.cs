using System.Text;
using MercadoCercano.Application.Reports;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Reports;

public sealed partial class ReportService
{
    public async Task<ReportCsvResponse> SalesCsvAsync(ReportPeriodQuery query, CancellationToken ct)
    {
        var report = await SalesAsync(query, ct);
        var rows = Context(report.Period);
        rows.Add(["Dia Colombia", "Pedidos creados", "Entregados", "Sin cobro", "Mercancia COP", "Envio COP",
            "Venta entregada COP", "Ofertas productos COP", "Promociones COP", "Cobros COP", "Reembolsos COP",
            "Flujo neto COP", "Reembolsos mercancia posventa COP", "Costo entregado COP", "Costo recuperado COP", "Margen estimado COP"]);
        rows.Add(MetricsCsv("TOTAL", report.Totals));
        rows.AddRange(report.Days.Select(item => MetricsCsv(item.Date.ToString("yyyy-MM-dd"), item.Metrics)));
        rows.Add([]);
        rows.Add(["Estado actual de pedidos creados en el periodo", "Pedidos", "Importe solicitado COP (no son ventas)"]);
        rows.AddRange(report.Statuses.Select(item => new[] { item.Status, item.Orders.ToString(), Exact(item.OrderAmount) }));
        return Csv($"ventas-{report.Period.From:yyyyMMdd}-{report.Period.To:yyyyMMdd}.csv", rows);
    }

    public async Task<ReportCsvResponse> ProductsCsvAsync(ReportPeriodQuery query, string? search, string sort, CancellationToken ct)
    {
        Search(search);
        await using var transaction = new ReadSnapshot(db);
        await transaction.Open(ct);
        var data = await Load(query, ct);
        var products = ProductRows(data, search, sort);
        var rows = Context(data.Period);
        rows.Add(["Busqueda", search ?? "", "Orden", sort]);
        rows.Add(["SKU", "Producto historico", "Unidades entregadas", "Unidades recibidas en devolucion",
            "Unidades repuestas", "Mercancia vendida COP", "Reembolsos mercancia COP", "Mercancia neta COP",
            "Costo entregado COP", "Costo recuperado COP", "Margen estimado COP"]);
        rows.AddRange(products.Select(item => new[] { item.Sku, item.Name, item.DeliveredQuantity.ToString(),
            item.ReturnedQuantity.ToString(), item.RestockedQuantity.ToString(), Exact(item.MerchandiseSales),
            Exact(item.MerchandiseRefunds), Exact(item.NetMerchandise), Exact(item.DeliveredCost),
            Exact(item.RestockedCost), Exact(item.EstimatedMargin) }));
        return Csv($"productos-{data.Period.From:yyyyMMdd}-{data.Period.To:yyyyMMdd}.csv", rows);
    }

    public async Task<ReportCsvResponse> InventoryCsvAsync(InventoryReportQuery query, CancellationToken ct)
    {
        await using var transaction = new ReadSnapshot(db);
        await transaction.Open(ct);
        var inventory = await InventoryRows(query, ct);
        List<string[]> rows =
        [
            ["Inventario actual; no historico", DateTimeOffset.UtcNow.ToString("O")],
            ["Busqueda", query.Search ?? "", "Categoria", query.CategoryId?.ToString() ?? "Todas",
                "Stock", query.Stock, "Visibilidad", query.Visibility],
            ["Importes COP exactos como texto; prefijo apostrofe para hojas de calculo"],
            ["SKU", "Producto", "Categoria actual", "Publicado", "Unidades", "Umbral stock bajo", "Costo unitario COP", "Valor stock COP", "Estado"]
        ];
        rows.AddRange(inventory.Select(item => new[] { item.Sku, item.Name, item.Category, item.IsPublished ? "Si" : "No",
            item.Quantity.ToString(), item.LowStockThreshold.ToString(), Exact(item.UnitCost), Exact(item.StockValue), item.State }));
        return Csv("inventario-actual.csv", rows);
    }

    public async Task<ReportCsvResponse> PromotionsCsvAsync(ReportPeriodQuery query, string? search, CancellationToken ct)
    {
        Search(search);
        var window = Period(query);
        await using var transaction = new ReadSnapshot(db);
        await transaction.Open(ct);
        var promotions = await PromotionRows(query, window, search, ct);
        var rows = Context(await Metadata(query, window, ct));
        rows.Add(["Busqueda", search ?? ""]);
        rows.Add(["Nombre actual", "Codigo", "Activa", "Usos creados", "Usos liberados", "Creados aun consumidos",
            "Descuentos otorgados COP", "Descuentos liberados COP", "Contador global actual", "Cupo global"]);
        rows.AddRange(promotions.Select(item => new[] { item.Name, item.Code ?? "Automatica", item.IsActive ? "Si" : "No",
            item.UsesCreated.ToString(), item.UsesReleased.ToString(), item.CreatedUsesStillConsumed.ToString(),
            Exact(item.DiscountsGranted), Exact(item.DiscountsReleased), item.CurrentUsedCount.ToString(), item.MaxUses?.ToString() ?? "Sin limite" }));
        return Csv($"promociones-{window.From:yyyyMMdd}-{window.To:yyyyMMdd}.csv", rows);
    }

    private static List<string[]> Context(ReportPeriodResponse period)
    {
        List<string[]> rows =
        [
            ["Periodo inclusivo Colombia", period.From.ToString("yyyy-MM-dd"), period.To.ToString("yyyy-MM-dd")],
            ["Zona", period.TimeZone, "Datos", period.DataSet, "Generado UTC", period.GeneratedAtUtc.ToString("O")],
            ["Importes COP exactos como texto; prefijo apostrofe para hojas de calculo"],
            ["Margen estimado = mercancia entregada - reembolsos posventa - costo entregado + costo repuesto vendible; no utilidad contable"]
        ];
        rows.AddRange(period.Warnings.Select(item => new[] { "ADVERTENCIA", item }));
        return rows;
    }
    private static string[] MetricsCsv(string label, ReportMetricsResponse item) =>
        [label, item.CreatedOrders.ToString(), item.DeliveredOrders.ToString(), item.FreeDeliveredOrders.ToString(),
            Exact(item.MerchandiseSales), Exact(item.ShippingSales), Exact(item.SalesTotal), Exact(item.ProductDiscounts),
            Exact(item.PromotionDiscounts), Exact(item.Collections), Exact(item.Refunds), Exact(item.NetCash),
            Exact(item.MerchandiseRefunds), Exact(item.DeliveredCost), Exact(item.RestockedCost), Exact(item.EstimatedMargin)];
    private static string Exact(string value) => "'" + value;
    private static ReportCsvResponse Csv(string fileName, IEnumerable<string[]> rows)
    {
        var text = string.Join("\r\n", rows.Select(row => string.Join(";", row.Select(Cell)))) + "\r\n";
        return new(fileName, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray());
    }
    private static string Cell(string value)
    {
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
