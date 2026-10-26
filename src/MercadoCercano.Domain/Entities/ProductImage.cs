namespace MercadoCercano.Domain.Entities;

public sealed class ProductImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public required string ImageUrl { get; set; }
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
    public Product Product { get; set; } = null!;
}
