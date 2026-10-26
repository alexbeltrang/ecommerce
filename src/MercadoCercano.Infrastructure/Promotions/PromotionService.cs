using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Promotions;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Promotions;

public sealed class PromotionService(StoreDbContext db) : IPromotionService
{
    public static string? NormalizeCode(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    public static bool ValidCode(string code) => Regex.IsMatch(code, @"\A[A-Z0-9][A-Z0-9_-]{2,79}\z", RegexOptions.CultureInvariant);
    private IQueryable<Promotion> Query() => db.Promotions.AsNoTracking().Include(item => item.Product).Include(item => item.Category);

    public async Task<PageResponse<PromotionResponse>> ListAsync(PromotionQuery query, CancellationToken ct)
    {
        Validate(query);
        var filtered = Query();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + query.Search.Trim().Replace("/", "//").Replace("%", "/%").Replace("_", "/_") + "%";
            filtered = filtered.Where(item => EF.Functions.Like(item.Name, pattern, "/") || (item.Code != null && EF.Functions.Like(item.Code, pattern, "/")));
        }
        if (query.Kind == "coupon") filtered = filtered.Where(item => item.Code != null);
        if (query.Kind == "automatic") filtered = filtered.Where(item => item.Code == null);
        if (query.IsActive.HasValue) filtered = filtered.Where(item => item.IsActive == query.IsActive.Value);
        var total = await filtered.CountAsync(ct);
        var offset = (long)(query.Page - 1) * query.PageSize;
        var rows = offset > int.MaxValue ? [] : await filtered.OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToArray(), total, query.Page, query.PageSize);
    }

    public async Task<PromotionResponse> GetAsync(Guid id, CancellationToken ct) => Map(await Query().SingleOrDefaultAsync(item => item.Id == id, ct) ?? throw Missing());

    public async Task<PromotionResponse> SaveAsync(Guid? id, PromotionWriteRequest request, Guid userId, CancellationToken ct)
    {
        Validate(request);
        var code = NormalizeCode(request.Code);
        if (code is not null && !ValidCode(code)) throw Invalid("El codigo requiere de 3 a 80 letras ASCII, numeros, guiones o guion bajo.");
        if (!Enum.TryParse<DiscountType>(request.DiscountType, out var type) || !Enum.IsDefined(type) ||
            (type == DiscountType.Percentage && request.DiscountValue > 100))
            throw Invalid("Selecciona un descuento fijo positivo o un porcentaje entero entre 1 y 100.");
        if (request.EndsAtUtc <= request.StartsAtUtc || (request.ProductId.HasValue && request.CategoryId.HasValue))
            throw Invalid("Indica una vigencia creciente y un solo alcance: todos, categoria o producto.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = id.HasValue ? await db.Promotions.SingleOrDefaultAsync(item => item.Id == id.Value, ct) ?? throw Missing() : new Promotion();
        if (id.HasValue && request.UpdatedAtUtc?.UtcDateTime != item.UpdatedAtUtc)
            throw Conflict("La promocion cambio. Recarga antes de editar.");
        if (request.MaxUses.HasValue && request.MaxUses < item.UsedCount) throw Conflict("El cupo no puede ser menor que los usos consumidos.");
        if (id.HasValue && NormalizeCode(item.Code) != code && await db.PromotionUsages.AnyAsync(usage => usage.PromotionId == item.Id, ct))
            throw Conflict("No se puede cambiar el codigo de una promocion que ya tiene historial; crea otra.");
        if (code is not null && await db.Promotions.AnyAsync(other => other.Id != item.Id && EF.Functions.Collate(other.Code, "NOCASE") == code, ct))
            throw Conflict("Ya existe una promocion con ese codigo.");
        if (request.ProductId.HasValue && !await db.Products.AnyAsync(product => product.Id == request.ProductId, ct))
            throw Invalid("Selecciona un producto existente.");
        if (request.CategoryId.HasValue && !await db.Categories.AnyAsync(category => category.Id == request.CategoryId, ct))
            throw Invalid("Selecciona una categoria existente.");
        item.Name = request.Name.Trim(); item.Code = code; item.DiscountType = type;
        item.DiscountValue = request.DiscountValue; item.MinimumOrderAmount = request.MinimumOrderAmount;
        item.MaximumDiscountAmount = request.MaximumDiscountAmount; item.MaxUses = request.MaxUses;
        item.ProductId = request.ProductId; item.CategoryId = request.CategoryId;
        item.StartsAtUtc = request.StartsAtUtc!.Value.UtcDateTime; item.EndsAtUtc = request.EndsAtUtc!.Value.UtcDateTime;
        item.IsActive = request.IsActive;
        if (!id.HasValue) db.Promotions.Add(item);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = id.HasValue ? "Promotion.Update" : "Promotion.Create",
            EntityType = nameof(Promotion), EntityId = item.Id.ToString(), DetailsJson = JsonSerializer.Serialize(request)
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(item.Id, ct);
    }

    public async Task<PageResponse<PromotionUsageResponse>> UsagesAsync(Guid id, int page, CancellationToken ct)
    {
        if (page < 1) throw Invalid("La pagina debe ser positiva.");
        if (!await db.Promotions.AnyAsync(item => item.Id == id, ct)) throw Missing();
        var query = db.PromotionUsages.AsNoTracking().Where(item => item.PromotionId == id);
        var total = await query.CountAsync(ct);
        var offset = (long)(page - 1) * 12;
        var rows = offset > int.MaxValue ? [] : await query.Include(item => item.Order).OrderByDescending(item => item.UsedAtUtc)
            .ThenBy(item => item.Id).Skip((int)offset).Take(12).ToListAsync(ct);
        return new(rows.Select(item => new PromotionUsageResponse(item.OrderId, item.Order.OrderNumber, item.DiscountAmount,
            Utc(item.UsedAtUtc), item.ReleasedAtUtc.HasValue ? Utc(item.ReleasedAtUtc.Value) : null)).ToArray(), total, page, 12);
    }

    private static void Validate(object value)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), errors, true))
            throw Invalid(string.Join(" ", errors.Select(error => error.ErrorMessage)));
    }
    private static PromotionResponse Map(Promotion item)
    {
        var now = DateTime.UtcNow;
        var state = !item.IsActive ? "Inactive" : now < item.StartsAtUtc ? "Scheduled" : now >= item.EndsAtUtc ? "Expired"
            : item.MaxUses.HasValue && item.UsedCount >= item.MaxUses ? "Exhausted" : "Active";
        return new(item.Id, item.Name, item.Code, item.DiscountType.ToString(), item.DiscountValue,
            item.MinimumOrderAmount, item.MaximumDiscountAmount, item.MaxUses, item.UsedCount,
            item.ProductId, item.Product?.Name, item.CategoryId, item.Category?.Name,
            Utc(item.StartsAtUtc), Utc(item.EndsAtUtc), item.IsActive, state, Utc(item.UpdatedAtUtc));
    }
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static CatalogException Invalid(string message) => new(CatalogError.Validation, message);
    private static CatalogException Conflict(string message) => new(CatalogError.Conflict, message);
    private static CatalogException Missing() => new(CatalogError.NotFound, "La promocion no existe.");
}
