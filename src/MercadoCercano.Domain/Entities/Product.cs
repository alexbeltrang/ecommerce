namespace MercadoCercano.Domain.Entities;

public sealed class Product : IHasUpdatedAt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Description { get; set; }
    public long Price { get; set; }
    public long Cost { get; set; }
    public long? PromotionalPrice { get; set; }
    public DateTime? PromotionStartUtc { get; set; }
    public DateTime? PromotionEndUtc { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; }
    public required string Unit { get; set; }
    public int? WeightGrams { get; set; }
    public string? Presentation { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDateUtc { get; set; }
    public string? SupplierLabelingJson { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Category Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = [];

    public long EffectivePriceAt(DateTime utcNow) =>
        PromotionalPrice.HasValue &&
        (!PromotionStartUtc.HasValue || PromotionStartUtc <= utcNow) &&
        (!PromotionEndUtc.HasValue || PromotionEndUtc > utcNow)
            ? PromotionalPrice.Value : Price;
}
