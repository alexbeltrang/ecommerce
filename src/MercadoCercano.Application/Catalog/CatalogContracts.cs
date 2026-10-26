using System.ComponentModel.DataAnnotations;

namespace MercadoCercano.Application.Catalog;

/// <summary>Filtros y paginacion del catalogo.</summary>
public sealed record CatalogQuery
{
    [StringLength(100)] public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 50)] public int PageSize { get; init; } = 12;
    [Required, RegularExpression("^(name|newest|price-asc|price-desc)$")]
    public string Sort { get; init; } = "name";
}

/// <summary>Pagina de resultados con total independiente del limite.</summary>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Categoria del catalogo.</summary>
public sealed record CategoryResponse(
    Guid Id, string Name, string Slug, string? Description, int SortOrder,
    bool IsActive, DateTimeOffset UpdatedAtUtc);

/// <summary>Datos para crear o actualizar una categoria.</summary>
public sealed record CategoryWriteRequest
{
    [Required, StringLength(120)] public required string Name { get; init; }
    [Required, StringLength(140), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public required string Slug { get; init; }
    [StringLength(2000)] public string? Description { get; init; }
    [Range(0, int.MaxValue)] public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTimeOffset? UpdatedAtUtc { get; init; }
}

/// <summary>Imagen validada y asociada a un producto.</summary>
public sealed record ProductImageResponse(Guid Id, string ImageUrl, string? AltText, int SortOrder);

/// <summary>Informacion publica del producto; excluye costos y datos internos.</summary>
public sealed record ProductResponse(
    Guid Id, Guid CategoryId, string CategoryName, string Sku, string Name, string Slug,
    string Description, long Price, long EffectivePrice, int StockQuantity,
    string Unit, int? WeightGrams, string? Presentation, bool IsFeatured,
    IReadOnlyList<ProductImageResponse> Images);

/// <summary>Informacion del producto exclusiva para administradores.</summary>
public sealed record AdminProductResponse(
    ProductResponse Product, long Cost, int LowStockThreshold, bool IsActive,
    long? PromotionalPrice, DateTimeOffset? PromotionStartUtc, DateTimeOffset? PromotionEndUtc,
    string? LotNumber, DateTimeOffset? ExpirationDateUtc, string? SupplierLabelingJson,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Datos administrativos de un producto e inventario en COP enteros.</summary>
public sealed record ProductWriteRequest
{
    public Guid CategoryId { get; init; }
    [Required, StringLength(80)] public required string Sku { get; init; }
    [Required, StringLength(180)] public required string Name { get; init; }
    [Required, StringLength(200), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public required string Slug { get; init; }
    [Required, StringLength(10000)] public required string Description { get; init; }
    [Range(1, 9_000_000_000_000)] public long Price { get; init; }
    [Range(0, 9_000_000_000_000)] public long Cost { get; init; }
    [Range(1, 9_000_000_000_000)] public long? PromotionalPrice { get; init; }
    public DateTimeOffset? PromotionStartUtc { get; init; }
    public DateTimeOffset? PromotionEndUtc { get; init; }
    [Range(0, int.MaxValue)] public int StockQuantity { get; init; }
    [Range(0, int.MaxValue)] public int LowStockThreshold { get; init; }
    [Required, StringLength(40)] public required string Unit { get; init; }
    [Range(1, int.MaxValue)] public int? WeightGrams { get; init; }
    [StringLength(300)] public string? Presentation { get; init; }
    [StringLength(120)] public string? LotNumber { get; init; }
    public DateTimeOffset? ExpirationDateUtc { get; init; }
    [StringLength(10000)] public string? SupplierLabelingJson { get; init; }
    public bool IsFeatured { get; init; }
    public bool IsActive { get; init; } = true;
    [StringLength(1000)] public string? InventoryReason { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
}

/// <summary>Texto alternativo y orden de una imagen.</summary>
public sealed record ImageWriteRequest
{
    [StringLength(300)] public string? AltText { get; init; }
    [Range(0, int.MaxValue)] public int SortOrder { get; init; }
}
