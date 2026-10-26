using MercadoCercano.Domain.Enums;

namespace MercadoCercano.Domain.Entities;

public sealed class OrderStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public OrderStatus? PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public required string Reason { get; set; }
    public Guid? UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Order Order { get; set; } = null!;
    public User? User { get; set; }
}
