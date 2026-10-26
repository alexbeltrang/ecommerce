namespace MercadoCercano.Domain.Entities;

public sealed class StoreSetting : IHasUpdatedAt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Key { get; set; }
    public required string Value { get; set; }
    public bool IsPublic { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
