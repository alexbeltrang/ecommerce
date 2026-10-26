namespace MercadoCercano.Domain.Entities;

public sealed class PromotionUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PromotionId { get; set; }
    public Guid OrderId { get; set; }
    public DateTime UsedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAtUtc { get; set; }
    public long DiscountAmount { get; set; }
    public Promotion Promotion { get; set; } = null!;
    public Order Order { get; set; } = null!;
}
