namespace MercadoCercano.Domain.Entities;

public sealed class PaymentEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public required string StoredFileName { get; set; }
    public long ByteLength { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Payment Payment { get; set; } = null!;
}
