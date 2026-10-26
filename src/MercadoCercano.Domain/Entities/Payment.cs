using MercadoCercano.Domain.Enums;

namespace MercadoCercano.Domain.Entities;

public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public long Amount { get; set; }
    public string? Reference { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Order Order { get; set; } = null!;
    public User? ConfirmedByUser { get; set; }
    public ICollection<PaymentEvidence> Evidence { get; set; } = [];
}
