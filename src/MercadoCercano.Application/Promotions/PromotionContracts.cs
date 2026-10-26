using System.ComponentModel.DataAnnotations;
using MercadoCercano.Application.Catalog;

namespace MercadoCercano.Application.Promotions;

/// <summary>Regla promocional administrativa, automatica si no tiene codigo.</summary>
public sealed record PromotionWriteRequest
{
    [Required, StringLength(160)] public required string Name { get; init; }
    [StringLength(80)] public string? Code { get; init; }
    [Required, RegularExpression("^(Percentage|FixedAmount)$")] public required string DiscountType { get; init; }
    [Range(1, 9_000_000_000_000)] public long DiscountValue { get; init; }
    [Range(0, 9_000_000_000_000)] public long MinimumOrderAmount { get; init; }
    [Range(1, 9_000_000_000_000)] public long? MaximumDiscountAmount { get; init; }
    [Range(1, int.MaxValue)] public int? MaxUses { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? CategoryId { get; init; }
    [Required] public DateTimeOffset? StartsAtUtc { get; init; }
    [Required] public DateTimeOffset? EndsAtUtc { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
}

/// <summary>Promocion versionada con cupo y alcance, sin datos personales.</summary>
public sealed record PromotionResponse(Guid Id, string Name, string? Code, string DiscountType, long DiscountValue,
    long MinimumOrderAmount, long? MaximumDiscountAmount, int? MaxUses, int UsedCount,
    Guid? ProductId, string? ProductName, Guid? CategoryId, string? CategoryName,
    DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, bool IsActive, string State, DateTimeOffset UpdatedAtUtc);

/// <summary>Filtros y pagina del listado administrativo de promociones.</summary>
public sealed record PromotionQuery
{
    [StringLength(100)] public string? Search { get; init; }
    [RegularExpression("^(automatic|coupon)$")] public string? Kind { get; init; }
    public bool? IsActive { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 50)] public int PageSize { get; init; } = 12;
}

/// <summary>Uso historico vinculado al pedido, conservado incluso si se libera.</summary>
public sealed record PromotionUsageResponse(Guid OrderId, string OrderNumber, long DiscountAmount, DateTimeOffset UsedAtUtc, DateTimeOffset? ReleasedAtUtc);

public interface IPromotionService
{
    Task<PageResponse<PromotionResponse>> ListAsync(PromotionQuery query, CancellationToken ct);
    Task<PromotionResponse> GetAsync(Guid id, CancellationToken ct);
    Task<PromotionResponse> SaveAsync(Guid? id, PromotionWriteRequest request, Guid userId, CancellationToken ct);
    Task<PageResponse<PromotionUsageResponse>> UsagesAsync(Guid id, int page, CancellationToken ct);
}
