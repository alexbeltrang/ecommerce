using MercadoCercano.Domain.Enums;

namespace MercadoCercano.Domain.Entities;

public sealed class OrderReturn : IHasUpdatedAt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid RequestId { get; set; } = Guid.NewGuid();
    public string? RequestFingerprint { get; set; }
    public OrderReturnKind Kind { get; set; }
    public OrderReturnStatus Status { get; set; } = OrderReturnStatus.Requested;
    public required string Reason { get; set; }
    public string? RejectionReason { get; set; }
    public long RefundAmount { get; set; }
    public long ShippingRefundAmount { get; set; }
    public string? RefundReference { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public Guid? RefundedByUserId { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }
    public DateTime? RefundedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Order Order { get; set; } = null!;
    public ICollection<OrderReturnItem> Items { get; set; } = [];
}

public sealed class OrderReturnItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderReturnId { get; set; }
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
    public int RestockedQuantity { get; set; }
    public long RefundAmount { get; set; }
    public OrderReturn Return { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}
