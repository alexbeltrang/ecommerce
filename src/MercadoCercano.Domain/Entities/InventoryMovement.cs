using MercadoCercano.Domain.Enums;

namespace MercadoCercano.Domain.Entities;

public sealed class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public InventoryMovementType MovementType { get; set; }
    public int QuantityDelta { get; set; }
    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }
    public required string Reason { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Product Product { get; set; } = null!;
    public User? User { get; set; }
}
