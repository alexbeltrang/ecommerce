using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Orders;
using MercadoCercano.Infrastructure.Catalog;

namespace MercadoCercano.Infrastructure.Orders;

public sealed class FilePaymentEvidenceStore(string directory) : IPrivatePaymentEvidenceStore
{
    private readonly FileProductImageStore files = new(directory);
    public async Task<string> SaveAsync(Stream stream, long length, string contentType, CancellationToken ct)
    {
        try { return await files.SaveAsync(stream, length, contentType, ct); }
        catch (CatalogException error) when (error.Error == CatalogError.Validation)
        { throw new CheckoutException(CheckoutError.Validation, error.Message); }
    }
    public StoredImageFile Open(string fileName)
    {
        try { return files.Open(fileName); }
        catch (CatalogException error) when (error.Error == CatalogError.NotFound)
        { throw new CheckoutException(CheckoutError.NotFound, "El comprobante no esta disponible."); }
    }
    public void Delete(string fileName) => files.Delete(fileName);
}
