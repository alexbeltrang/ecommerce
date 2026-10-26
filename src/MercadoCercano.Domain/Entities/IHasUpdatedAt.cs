namespace MercadoCercano.Domain.Entities;

public interface IHasUpdatedAt
{
    DateTime UpdatedAtUtc { get; set; }
}
