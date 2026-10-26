namespace MercadoCercano.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FullName { get; set; }
    public string? Email { get; set; }
    public required string Phone { get; set; }
    public string? BusinessNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Address> Addresses { get; set; } = [];
}
