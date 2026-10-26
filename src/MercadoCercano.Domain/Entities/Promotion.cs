using MercadoCercano.Domain.Enums;

namespace MercadoCercano.Domain.Entities;

public sealed class Promotion : IHasUpdatedAt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Promocion";
    public string? Code { get; set; }
    public DiscountType DiscountType { get; set; }
    public long DiscountValue { get; set; }
    public long MinimumOrderAmount { get; set; }
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public long? MaximumDiscountAmount { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public Product? Product { get; set; }
    public Category? Category { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<PromotionUsage> Usages { get; set; } = [];
}
