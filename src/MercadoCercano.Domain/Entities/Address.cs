namespace MercadoCercano.Domain.Entities;

public sealed class Address
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public required string Municipality { get; set; }
    public string? Neighborhood { get; set; }
    public required string AddressLine { get; set; }
    public string? References { get; set; }
    public bool IsDefault { get; set; }
    public Customer Customer { get; set; } = null!;
}
