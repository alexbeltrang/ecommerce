namespace MercadoCercano.Application.Catalog;

public interface ICatalogService
{
    Task<IReadOnlyList<CategoryResponse>> CategoriesAsync(bool admin, CancellationToken ct);
    Task<CategoryResponse> CategoryAsync(Guid id, CancellationToken ct);
    Task<CategoryResponse> SaveCategoryAsync(Guid? id, CategoryWriteRequest request, Guid userId, CancellationToken ct);
    Task<PageResponse<ProductResponse>> ProductsAsync(CatalogQuery query, CancellationToken ct);
    Task<ProductResponse> ProductAsync(string slug, CancellationToken ct);
    Task<ProductResponse> PublicProductAsync(Guid id, CancellationToken ct);
    Task<PageResponse<AdminProductResponse>> AdminProductsAsync(CatalogQuery query, CancellationToken ct);
    Task<AdminProductResponse> AdminProductAsync(Guid id, CancellationToken ct);
    Task<AdminProductResponse> SaveProductAsync(Guid? id, ProductWriteRequest request, Guid userId, CancellationToken ct);
    Task<ProductImageResponse> UploadAsync(Guid productId, Stream stream, long length, string contentType,
        string? altText, Guid userId, CancellationToken ct);
    Task<ProductImageResponse> UpdateImageAsync(Guid productId, Guid imageId, ImageWriteRequest request, Guid userId, CancellationToken ct);
    Task DeleteImageAsync(Guid productId, Guid imageId, Guid userId, CancellationToken ct);
    Task<StoredImageFile> OpenImageAsync(string fileName, bool admin, CancellationToken ct);
}

public enum CatalogError { Validation, NotFound, Conflict }

public sealed class CatalogException(CatalogError error, string message) : Exception(message)
{
    public CatalogError Error { get; } = error;
}

public sealed record StoredImageFile(Stream Stream, string ContentType);

public interface IProductImageStore
{
    Task<string> SaveAsync(Stream stream, long length, string contentType, CancellationToken ct);
    void Delete(string fileName);
    StoredImageFile Open(string fileName);
}
