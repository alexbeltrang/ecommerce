using MercadoCercano.Domain.Enums;

namespace MercadoCercano.Domain.Entities;

public sealed class Order : IHasUpdatedAt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string OrderNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public required string CustomerNameSnapshot { get; set; }
    public required string CustomerPhoneSnapshot { get; set; }
    public string? CustomerEmailSnapshot { get; set; }
    public required string MunicipalitySnapshot { get; set; }
    public string? NeighborhoodSnapshot { get; set; }
    public string? AddressSnapshot { get; set; }
    public string? AddressReferencesSnapshot { get; set; }
    public DeliveryMethod DeliveryMethod { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.PendingConfirmation;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public long Subtotal { get; set; }
    public long DiscountTotal { get; set; }
    public long ShippingCost { get; set; }
    public bool ShippingCostConfirmed { get; set; } = true;
    public long GrandTotal { get; set; }
    public string? PromotionCodeSnapshot { get; set; }
    public string? PromotionNameSnapshot { get; set; }
    public long PromotionDiscountTotal { get; set; }
    public string? CustomerNotes { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? TrackingTokenHash { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? RequestFingerprint { get; set; }
    public bool IsTestOrder { get; set; }
    public string? CheckoutNoticeSnapshot { get; set; }
    public string? PickupInstructionsSnapshot { get; set; }
    public string? PaymentInstructionsSnapshot { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? ShippedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? TermsAcceptedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Customer? Customer { get; set; }
    public User? CancelledByUser { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<OrderReturn> Returns { get; set; } = [];
}
