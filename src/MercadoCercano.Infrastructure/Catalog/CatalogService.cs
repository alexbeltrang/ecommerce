using System.Text.Json;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Catalog;

public sealed class CatalogService(StoreDbContext db, IProductImageStore images) : ICatalogService
{
    private const string ImagePrefix = "/api/v1/catalog/images/";

    public async Task<IReadOnlyList<CategoryResponse>> CategoriesAsync(bool admin, CancellationToken ct) =>
        (await db.Categories.AsNoTracking().Where(category => admin || category.IsActive)
            .OrderBy(category => category.SortOrder).ThenBy(category => category.Name)
            .ThenBy(category => category.Id).ToListAsync(ct)).Select(ToCategory).ToArray();

    public async Task<CategoryResponse> CategoryAsync(Guid id, CancellationToken ct) =>
        ToCategory(await db.Categories.AsNoTracking().SingleOrDefaultAsync(category => category.Id == id, ct)
            ?? throw NotFound("La categoria no existe."));

    public async Task<CategoryResponse> SaveCategoryAsync(
        Guid? id, CategoryWriteRequest request, Guid userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var category = id.HasValue
            ? await db.Categories.SingleOrDefaultAsync(category => category.Id == id, ct)
                ?? throw NotFound("La categoria no existe.")
            : new Category { Name = request.Name.Trim(), Slug = request.Slug };
        if (id.HasValue) CheckVersion(category.UpdatedAtUtc, request.UpdatedAtUtc);
        if (await db.Categories.AnyAsync(other => other.Id != category.Id && other.Slug == request.Slug, ct))
            throw Conflict("Ya existe una categoria con esa URL.");

        category.Name = request.Name.Trim();
        category.Slug = request.Slug;
        category.Description = request.Description?.Trim();
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        if (!id.HasValue) db.Categories.Add(category);
        Audit(userId, id.HasValue ? "Category.Update" : "Category.Create", nameof(Category), category.Id);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return ToCategory(category);
    }

    public async Task<PageResponse<ProductResponse>> ProductsAsync(CatalogQuery query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var filtered = Filter(query, publicOnly: true);
        var total = await filtered.CountAsync(ct);
        var products = await Page(filtered, query, now).ToListAsync(ct);
        return new(products.Select(product => ToProduct(product, now)).ToArray(), total, query.Page, query.PageSize);
    }

    public async Task<ProductResponse> ProductAsync(string slug, CancellationToken ct) =>
        ToProduct(await Products().SingleOrDefaultAsync(product =>
            product.Slug == slug && product.IsActive && product.Category.IsActive, ct)
            ?? throw NotFound("El producto no esta disponible."));

    public async Task<PageResponse<AdminProductResponse>> AdminProductsAsync(CatalogQuery query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var filtered = Filter(query, publicOnly: false);
        var total = await filtered.CountAsync(ct);
        var products = await Page(filtered, query, now).ToListAsync(ct);
        return new(products.Select(product => ToAdmin(product, now)).ToArray(), total, query.Page, query.PageSize);
    }

    public async Task<AdminProductResponse> AdminProductAsync(Guid id, CancellationToken ct) =>
        ToAdmin(await Products().SingleOrDefaultAsync(product => product.Id == id, ct)
            ?? throw NotFound("El producto no existe."));

    public async Task<ProductResponse> PublicProductAsync(Guid id, CancellationToken ct) =>
        ToProduct(await Products().SingleOrDefaultAsync(product =>
            product.Id == id && product.IsActive && product.Category.IsActive, ct)
            ?? throw NotFound("El producto no esta disponible."));

    public async Task<AdminProductResponse> SaveProductAsync(
        Guid? id, ProductWriteRequest request, Guid userId, CancellationToken ct)
    {
        ValidatePromotion(request);
        if (!string.IsNullOrWhiteSpace(request.SupplierLabelingJson))
        {
            try { using var document = JsonDocument.Parse(request.SupplierLabelingJson); }
            catch (JsonException) { throw Validation("El etiquetado del proveedor debe ser JSON valido."); }
        }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var category = await db.Categories.SingleOrDefaultAsync(category => category.Id == request.CategoryId, ct)
            ?? throw Validation("Selecciona una categoria existente.");
        var sku = request.Sku.Trim().ToUpperInvariant();
        var product = id.HasValue
            ? await db.Products.Include(product => product.Images).SingleOrDefaultAsync(product => product.Id == id, ct)
                ?? throw NotFound("El producto no existe.")
            : new Product { Name = request.Name.Trim(), Slug = request.Slug, Sku = sku,
                Description = request.Description.Trim(), Unit = request.Unit.Trim() };
        if (id.HasValue) CheckVersion(product.UpdatedAtUtc, request.UpdatedAtUtc);
        if (await db.Products.AnyAsync(other => other.Id != product.Id &&
            (other.Sku == sku || other.Slug == request.Slug), ct))
            throw Conflict("Ya existe un producto con ese SKU o URL.");

        var previousStock = product.StockQuantity;
        if (id.HasValue && previousStock != request.StockQuantity && string.IsNullOrWhiteSpace(request.InventoryReason))
            throw Validation("Indica el motivo del ajuste de inventario.");
        product.Category = category;
        product.CategoryId = category.Id;
        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.Slug = request.Slug;
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        product.Cost = request.Cost;
        product.PromotionalPrice = request.PromotionalPrice;
        product.PromotionStartUtc = request.PromotionStartUtc?.UtcDateTime;
        product.PromotionEndUtc = request.PromotionEndUtc?.UtcDateTime;
        product.StockQuantity = request.StockQuantity;
        product.LowStockThreshold = request.LowStockThreshold;
        product.Unit = request.Unit.Trim();
        product.WeightGrams = request.WeightGrams;
        product.Presentation = request.Presentation?.Trim();
        product.LotNumber = request.LotNumber?.Trim();
        product.ExpirationDateUtc = request.ExpirationDateUtc?.UtcDateTime;
        product.SupplierLabelingJson = request.SupplierLabelingJson;
        product.IsFeatured = request.IsFeatured;
        product.IsActive = request.IsActive;
        if (!id.HasValue) db.Products.Add(product);
        if (previousStock != product.StockQuantity)
        {
            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id, UserId = userId,
                MovementType = product.StockQuantity > previousStock
                    ? InventoryMovementType.ManualAddition : InventoryMovementType.ManualRemoval,
                PreviousQuantity = previousStock, NewQuantity = product.StockQuantity,
                QuantityDelta = product.StockQuantity - previousStock,
                Reason = string.IsNullOrWhiteSpace(request.InventoryReason) ? "Inventario inicial" : request.InventoryReason.Trim(),
                ReferenceType = nameof(Product), ReferenceId = product.Id.ToString()
            });
        }
        Audit(userId, id.HasValue ? "Product.Update" : "Product.Create", nameof(Product), product.Id);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return ToAdmin(product);
    }

    public async Task<ProductImageResponse> UploadAsync(Guid productId, Stream stream, long length,
        string contentType, string? altText, Guid userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var product = await db.Products.Include(product => product.Images)
            .SingleOrDefaultAsync(product => product.Id == productId, ct)
            ?? throw NotFound("El producto no existe.");
        if (product.Images.Count >= 8) throw Validation("Cada producto admite hasta 8 imagenes.");
        var fileName = await images.SaveAsync(stream, length, contentType, ct);
        var committed = false;
        try
        {
            var lastOrder = product.Images.Count == 0 ? -1 : product.Images.Max(image => image.SortOrder);
            var image = new ProductImage
            {
                ProductId = productId, ImageUrl = ImagePrefix + fileName,
                AltText = altText?.Trim(), SortOrder = lastOrder == int.MaxValue ? int.MaxValue : lastOrder + 1
            };
            db.ProductImages.Add(image);
            Audit(userId, "ProductImage.Create", nameof(ProductImage), image.Id);
            await SaveAsync(ct);
            await transaction.CommitAsync(ct);
            committed = true;
            return ToImage(image);
        }
        finally
        {
            if (!committed) images.Delete(fileName);
        }
    }

    public async Task<ProductImageResponse> UpdateImageAsync(
        Guid productId, Guid imageId, ImageWriteRequest request, Guid userId, CancellationToken ct)
    {
        var image = await db.ProductImages.SingleOrDefaultAsync(image =>
            image.Id == imageId && image.ProductId == productId, ct) ?? throw NotFound("La imagen no existe.");
        image.AltText = request.AltText?.Trim();
        image.SortOrder = request.SortOrder;
        Audit(userId, "ProductImage.Update", nameof(ProductImage), image.Id);
        await SaveAsync(ct);
        return ToImage(image);
    }

    public async Task DeleteImageAsync(Guid productId, Guid imageId, Guid userId, CancellationToken ct)
    {
        var image = await db.ProductImages.SingleOrDefaultAsync(image =>
            image.Id == imageId && image.ProductId == productId, ct) ?? throw NotFound("La imagen no existe.");
        db.ProductImages.Remove(image);
        Audit(userId, "ProductImage.Delete", nameof(ProductImage), image.Id);
        await SaveAsync(ct);
        images.Delete(Path.GetFileName(image.ImageUrl));
    }

    public async Task<StoredImageFile> OpenImageAsync(string fileName, bool admin, CancellationToken ct)
    {
        var url = ImagePrefix + fileName;
        if (!await db.ProductImages.AnyAsync(image => image.ImageUrl == url &&
            (admin || (image.Product.IsActive && image.Product.Category.IsActive)), ct))
            throw NotFound("La imagen no esta disponible.");
        return images.Open(fileName);
    }

    private IQueryable<Product> Products() => db.Products.AsNoTracking()
        .Include(product => product.Category).Include(product => product.Images);

    private IQueryable<Product> Filter(CatalogQuery query, bool publicOnly)
    {
        var products = Products();
        if (publicOnly) products = products.Where(product => product.IsActive && product.Category.IsActive);
        if (query.CategoryId.HasValue) products = products.Where(product => product.CategoryId == query.CategoryId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().Replace("/", "//").Replace("%", "/%").Replace("_", "/_");
            products = products.Where(product => EF.Functions.Like(product.Name, "%" + term + "%", "/") ||
                EF.Functions.Like(product.Sku, "%" + term + "%", "/"));
        }
        return products;
    }

    private static IQueryable<Product> Page(IQueryable<Product> products, CatalogQuery query, DateTime now)
    {
        var sorted = query.Sort switch
        {
            "newest" => products.OrderByDescending(product => product.CreatedAtUtc),
            "price-asc" => products.OrderBy(product => product.PromotionalPrice.HasValue &&
                (product.PromotionStartUtc == null || product.PromotionStartUtc <= now) &&
                (product.PromotionEndUtc == null || product.PromotionEndUtc > now)
                    ? product.PromotionalPrice.Value : product.Price),
            "price-desc" => products.OrderByDescending(product => product.PromotionalPrice.HasValue &&
                (product.PromotionStartUtc == null || product.PromotionStartUtc <= now) &&
                (product.PromotionEndUtc == null || product.PromotionEndUtc > now)
                    ? product.PromotionalPrice.Value : product.Price),
            _ => products.OrderBy(product => product.Name)
        };
        var offset = (long)(query.Page - 1) * query.PageSize;
        if (offset > int.MaxValue) return sorted.Take(0);
        return sorted.ThenBy(product => product.Id).Skip((int)offset).Take(query.PageSize);
    }

    private void Audit(Guid userId, string action, string type, Guid id) =>
        db.AuditLogs.Add(new AuditLog { UserId = userId, Action = action, EntityType = type, EntityId = id.ToString() });

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        { throw Conflict("El SKU o la URL ya esta en uso. Actualiza la pagina antes de reintentar."); }
    }

    private static void CheckVersion(DateTime updated, DateTimeOffset? expected)
    {
        if (!expected.HasValue) throw Validation("La actualizacion requiere la version del registro.");
        if (expected.Value.UtcDateTime != updated)
            throw Conflict("Otro administrador modifico este registro. Recargalo antes de guardar.");
    }

    private static void ValidatePromotion(ProductWriteRequest request)
    {
        if (request.PromotionalPrice >= request.Price) throw Validation("El precio promocional debe ser menor al precio normal.");
        if (request.PromotionStartUtc.HasValue != request.PromotionEndUtc.HasValue ||
            (request.PromotionStartUtc.HasValue && request.PromotionEndUtc <= request.PromotionStartUtc))
            throw Validation("La promocion requiere inicio y fin, con fin posterior al inicio.");
        if (!request.PromotionalPrice.HasValue && request.PromotionStartUtc.HasValue)
            throw Validation("Indica el precio promocional para programar una promocion.");
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static CategoryResponse ToCategory(Category category) =>
        new(category.Id, category.Name, category.Slug, category.Description, category.SortOrder, category.IsActive, Utc(category.UpdatedAtUtc));
    private static ProductImageResponse ToImage(ProductImage image) => new(image.Id, image.ImageUrl, image.AltText, image.SortOrder);
    private static ProductResponse ToProduct(Product product, DateTime? asOf = null)
    {
        var now = asOf ?? DateTime.UtcNow;
        var effective = product.EffectivePriceAt(now);
        return new(product.Id, product.CategoryId, product.Category.Name, product.Sku, product.Name,
            product.Slug, product.Description, product.Price, effective, product.StockQuantity,
            product.Unit, product.WeightGrams, product.Presentation, product.IsFeatured,
            product.Images.OrderBy(image => image.SortOrder).ThenBy(image => image.Id).Select(ToImage).ToArray());
    }
    private static AdminProductResponse ToAdmin(Product product, DateTime? asOf = null) =>
        new(ToProduct(product, asOf), product.Cost, product.LowStockThreshold, product.IsActive, product.PromotionalPrice,
            product.PromotionStartUtc.HasValue ? Utc(product.PromotionStartUtc.Value) : null,
            product.PromotionEndUtc.HasValue ? Utc(product.PromotionEndUtc.Value) : null,
            product.LotNumber, product.ExpirationDateUtc.HasValue ? Utc(product.ExpirationDateUtc.Value) : null,
            product.SupplierLabelingJson, Utc(product.UpdatedAtUtc));
    private static CatalogException NotFound(string message) => new(CatalogError.NotFound, message);
    private static CatalogException Validation(string message) => new(CatalogError.Validation, message);
    private static CatalogException Conflict(string message) => new(CatalogError.Conflict, message);
}
