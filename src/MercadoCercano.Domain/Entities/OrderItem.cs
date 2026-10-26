namespace MercadoCercano.Domain.Entities;

public sealed class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid? ProductId { get; set; }
    public required string ProductNameSnapshot { get; set; }
    public required string SkuSnapshot { get; set; }
    public int Quantity { get; set; }
    public long UnitPrice { get; set; }
    public long UnitCostSnapshot { get; set; }
    public long LineTotal { get; set; }
    public long PromotionDiscountTotal { get; set; }
    public Order Order { get; set; } = null!;
    public Product? Product { get; set; }
}
