using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Api.Controllers;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Catalog;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Tests;

public sealed class CatalogServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a8ioAAAAASUVORK5CYII=");

    [Fact]
    public void AdministrativeControllersRequireAdministratorForEveryAction()
    {
        foreach (var controller in new[] { typeof(AdminCategoriesController), typeof(AdminProductsController) })
        {
            Assert.Equal(RolePolicies.Administrator, controller.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
            Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.All(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                action => Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>()));
        }
    }

    [Fact]
    public async Task PublicCatalogFiltersPaginatesAndHidesInternalFields()
    {
        await using var data = await TestCatalog.CreateAsync();
        var category = await data.CategoryAsync();
        for (var index = 0; index < 14; index++)
            await data.Service.SaveProductAsync(null, Request(category.Id, $"item-{index:D2}"), data.UserId, Ct);
        await data.Service.SaveProductAsync(null,
            Request(category.Id, "hidden") with { IsActive = false }, data.UserId, Ct);
        var hiddenCategory = await data.CategoryAsync("oculta", active: false);
        await data.Service.SaveProductAsync(null, Request(hiddenCategory.Id, "hidden-category"), data.UserId, Ct);

        var page = await data.Service.ProductsAsync(new CatalogQuery { Page = 2, PageSize = 5 }, Ct);
        Assert.Equal(14, page.Total);
        Assert.Equal(5, page.Items.Count);
        Assert.Equal("item-05", page.Items[0].Slug);
        var filtered = await data.Service.ProductsAsync(new CatalogQuery { CategoryId = hiddenCategory.Id }, Ct);
        Assert.Empty(filtered.Items);
        Assert.DoesNotContain("cost", JsonSerializer.Serialize(page), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupplierLabelingJson", JsonSerializer.Serialize(page), StringComparison.OrdinalIgnoreCase);
        var hidden = await Assert.ThrowsAsync<CatalogException>(() => data.Service.ProductAsync("hidden", Ct));
        Assert.Equal(CatalogError.NotFound, hidden.Error);
        Assert.Equal(16, (await data.Service.AdminProductsAsync(new CatalogQuery(), Ct)).Total);
    }

    [Fact]
    public async Task SearchEscapesWildcardsAndPaginationCannotOverflow()
    {
        await using var data = await TestCatalog.CreateAsync();
        var category = await data.CategoryAsync();
        await data.Service.SaveProductAsync(null, Request(category.Id, "discount") with { Name = "100% cafe" }, data.UserId, Ct);
        await data.Service.SaveProductAsync(null, Request(category.Id, "regular"), data.UserId, Ct);
        var found = await data.Service.ProductsAsync(new CatalogQuery { Search = "%" }, Ct);
        Assert.Single(found.Items);
        Assert.Equal("discount", found.Items[0].Slug);
        var last = await data.Service.ProductsAsync(new CatalogQuery { Page = int.MaxValue, PageSize = 50 }, Ct);
        Assert.Equal(2, last.Total);
        Assert.Empty(last.Items);
    }

    [Fact]
    public async Task PromotionalPricingAndSortingUseCurrentWindow()
    {
        await using var data = await TestCatalog.CreateAsync();
        var category = await data.CategoryAsync();
        var now = DateTimeOffset.UtcNow;
        await data.Service.SaveProductAsync(null, Request(category.Id, "regular") with { Price = 5000 }, data.UserId, Ct);
        await data.Service.SaveProductAsync(null, Request(category.Id, "sale") with
        {
            Price = 9000, PromotionalPrice = 2000,
            PromotionStartUtc = now.AddDays(-1), PromotionEndUtc = now.AddDays(1)
        }, data.UserId, Ct);
        await data.Service.SaveProductAsync(null, Request(category.Id, "expired") with
        {
            Price = 8000, PromotionalPrice = 1000,
            PromotionStartUtc = now.AddDays(-2), PromotionEndUtc = now.AddDays(-1)
        }, data.UserId, Ct);
        var page = await data.Service.ProductsAsync(new CatalogQuery { Sort = "price-asc" }, Ct);
        Assert.Equal(new[] { "sale", "regular", "expired" }, page.Items.Select(product => product.Slug));
        Assert.Equal(new long[] { 2000, 5000, 8000 }, page.Items.Select(product => product.EffectivePrice));
        var descending = await data.Service.ProductsAsync(new CatalogQuery { Sort = "price-desc" }, Ct);
        Assert.Equal("expired", descending.Items[0].Slug);
    }

    [Fact]
    public async Task StockChangesRequireReasonAndCreateReconciledMovementsAndAudit()
    {
        await using var data = await TestCatalog.CreateAsync();
        var category = await data.CategoryAsync();
        var request = Request(category.Id, "stock") with { StockQuantity = 5 };
        var product = await data.Service.SaveProductAsync(null, request, data.UserId, Ct);
        var initial = await data.Db.InventoryMovements.SingleAsync();
        Assert.Equal(0, initial.PreviousQuantity);
        Assert.Equal(5, initial.NewQuantity);
        Assert.Equal(5, initial.QuantityDelta);

        data.Db.ChangeTracker.Clear();
        var update = request with { StockQuantity = 2, UpdatedAtUtc = product.UpdatedAtUtc };
        var invalid = await Assert.ThrowsAsync<CatalogException>(() =>
            data.Service.SaveProductAsync(product.Product.Id, update, data.UserId, Ct));
        Assert.Equal(CatalogError.Validation, invalid.Error);
        data.Db.ChangeTracker.Clear();
        var saved = await data.Service.SaveProductAsync(product.Product.Id,
            update with { InventoryReason = "Conteo fisico" }, data.UserId, Ct);
        Assert.Equal(2, saved.Product.StockQuantity);
        var movement = await data.Db.InventoryMovements.SingleAsync(item => item.QuantityDelta == -3);
        Assert.Equal(5, movement.PreviousQuantity);
        Assert.Equal(2, movement.NewQuantity);
        Assert.Equal(data.UserId, movement.UserId);
        Assert.Equal(2, await data.Db.AuditLogs.CountAsync(log => log.EntityType == nameof(Product)));

        data.Db.ChangeTracker.Clear();
        var conflict = await Assert.ThrowsAsync<CatalogException>(() =>
            data.Service.SaveProductAsync(product.Product.Id, update with { InventoryReason = "Version antigua" }, data.UserId, Ct));
        Assert.Equal(CatalogError.Conflict, conflict.Error);
        Assert.Equal(2, await data.Db.InventoryMovements.CountAsync());
    }

    [Fact]
    public async Task CategoriesAreOrderedVersionedAndHideTheirProductsWhenDisabled()
    {
        await using var data = await TestCatalog.CreateAsync();
        var category = await data.CategoryAsync();
        await data.CategoryAsync("primera", order: 0);
        await data.Service.SaveProductAsync(null, Request(category.Id, "visible"), data.UserId, Ct);
        var update = new CategoryWriteRequest
        {
            Name = category.Name, Slug = category.Slug, SortOrder = 4,
            IsActive = false, UpdatedAtUtc = category.UpdatedAtUtc
        };
        var saved = await data.Service.SaveCategoryAsync(category.Id, update, data.UserId, Ct);
        Assert.False(saved.IsActive);
        Assert.Empty((await data.Service.ProductsAsync(new CatalogQuery(), Ct)).Items);
        Assert.Single(await data.Service.CategoriesAsync(false, Ct));
        Assert.Equal(2, (await data.Service.CategoriesAsync(true, Ct)).Count);
        data.Db.ChangeTracker.Clear();
        var conflict = await Assert.ThrowsAsync<CatalogException>(() =>
            data.Service.SaveCategoryAsync(category.Id, update, data.UserId, Ct));
        Assert.Equal(CatalogError.Conflict, conflict.Error);
    }

    [Fact]
    public async Task DuplicateIdentifiersAndInvalidPromotionsAreRejected()
    {
        await using var data = await TestCatalog.CreateAsync();
        var category = await data.CategoryAsync();
        var request = Request(category.Id, "unique");
        await data.Service.SaveProductAsync(null, request, data.UserId, Ct);
        var duplicate = await Assert.ThrowsAsync<CatalogException>(() =>
            data.Service.SaveProductAsync(null, request with { Slug = "other" }, data.UserId, Ct));
        Assert.Equal(CatalogError.Conflict, duplicate.Error);
        var duplicateCategory = await Assert.ThrowsAsync<CatalogException>(() =>
            data.Service.SaveCategoryAsync(null, new CategoryWriteRequest { Name = "Otra", Slug = category.Slug }, data.UserId, Ct));
        Assert.Equal(CatalogError.Conflict, duplicateCategory.Error);
        foreach (var invalid in new[]
        {
            Request(category.Id, "invalid") with { PromotionalPrice = 1000 },
            Request(category.Id, "invalid") with { PromotionStartUtc = DateTimeOffset.UtcNow },
            Request(category.Id, "invalid") with { SupplierLabelingJson = "not-json" },
            Request(Guid.NewGuid(), "invalid")
        })
        {
            var error = await Assert.ThrowsAsync<CatalogException>(() =>
                data.Service.SaveProductAsync(null, invalid, data.UserId, Ct));
            Assert.Equal(CatalogError.Validation, error.Error);
        }
        Assert.Equal(1, await data.Db.Products.CountAsync());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(9_000_000_000_000L, true)]
    [InlineData(9_000_000_000_001L, false)]
    public void RequestValidatesExactPriceLimits(long price, bool expected)
    {
        var request = Request(Guid.NewGuid(), "validation") with { Price = price };
        Assert.Equal(expected, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public async Task ImagesEnforceVisibilityOwnershipOrderingLimitAndDeletion()
    {
        await using var data = await TestCatalog.CreateAsync();
        var category = await data.CategoryAsync();
        var product = await data.Service.SaveProductAsync(null,
            Request(category.Id, "image") with { IsActive = false }, data.UserId, Ct);
        ProductImageResponse? first = null;
        for (var index = 0; index < 8; index++)
        {
            using var stream = new MemoryStream(Png);
            var image = await data.Service.UploadAsync(product.Product.Id, stream, Png.Length, "image/png", "Foto", data.UserId, Ct);
            first ??= image;
        }
        using var extra = new MemoryStream(Png);
        var limit = await Assert.ThrowsAsync<CatalogException>(() =>
            data.Service.UploadAsync(product.Product.Id, extra, Png.Length, "image/png", null, data.UserId, Ct));
        Assert.Equal(CatalogError.Validation, limit.Error);
        Assert.Equal(8, Directory.GetFiles(data.ImageDirectory).Length);
        var fileName = Path.GetFileName(first!.ImageUrl);
        var hidden = await Assert.ThrowsAsync<CatalogException>(() => data.Service.OpenImageAsync(fileName, false, Ct));
        Assert.Equal(CatalogError.NotFound, hidden.Error);
        var file = await data.Service.OpenImageAsync(fileName, true, Ct);
        await file.Stream.DisposeAsync();
        var owner = await Assert.ThrowsAsync<CatalogException>(() =>
            data.Service.UpdateImageAsync(Guid.NewGuid(), first.Id, new ImageWriteRequest(), data.UserId, Ct));
        Assert.Equal(CatalogError.NotFound, owner.Error);
        var changed = await data.Service.UpdateImageAsync(product.Product.Id, first.Id,
            new ImageWriteRequest { AltText = "Nuevo texto", SortOrder = 20 }, data.UserId, Ct);
        Assert.Equal(20, changed.SortOrder);
        Assert.Equal("Nuevo texto", changed.AltText);
        await data.Service.DeleteImageAsync(product.Product.Id, first.Id, data.UserId, Ct);
        Assert.Equal(7, await data.Db.ProductImages.CountAsync());
        Assert.Equal(7, Directory.GetFiles(data.ImageDirectory).Length);
    }

    [Fact]
    public async Task FailedImageDatabaseWriteDoesNotLeaveAnOrphanFile()
    {
        await using var data = await TestCatalog.CreateAsync();
        var category = await data.CategoryAsync();
        var product = await data.Service.SaveProductAsync(null, Request(category.Id, "image"), data.UserId, Ct);
        using var stream = new MemoryStream(Png);
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            data.Service.UploadAsync(product.Product.Id, stream, Png.Length, "image/png", null, Guid.NewGuid(), Ct));
        Assert.Empty(Directory.GetFiles(data.ImageDirectory));
        data.Db.ChangeTracker.Clear();
        Assert.Empty(await data.Db.ProductImages.ToListAsync());
    }

    [Fact]
    public async Task PublicImageAccessRequiresBothActiveProductAndActiveCategory()
    {
        await using var data = await TestCatalog.CreateAsync();
        var category = await data.CategoryAsync();
        var product = await data.Service.SaveProductAsync(null, Request(category.Id, "public-image"), data.UserId, Ct);
        using var stream = new MemoryStream(Png);
        var image = await data.Service.UploadAsync(product.Product.Id, stream, Png.Length, "image/png", null, data.UserId, Ct);
        var fileName = Path.GetFileName(image.ImageUrl);
        var file = await data.Service.OpenImageAsync(fileName, false, Ct);
        Assert.Equal("image/png", file.ContentType);
        await file.Stream.DisposeAsync();
        await data.Service.SaveCategoryAsync(category.Id, new CategoryWriteRequest
        {
            Name = category.Name, Slug = category.Slug, IsActive = false, UpdatedAtUtc = category.UpdatedAtUtc
        }, data.UserId, Ct);
        var hidden = await Assert.ThrowsAsync<CatalogException>(() => data.Service.OpenImageAsync(fileName, false, Ct));
        Assert.Equal(CatalogError.NotFound, hidden.Error);
    }

    [Fact]
    public async Task ImageLimitChecksActualStreamAndAcceptsExactSizeBoundary()
    {
        await using var data = await TestCatalog.CreateAsync();
        var exact = new byte[FileProductImageStore.MaximumBytes];
        Png.CopyTo(exact, 0);
        using var stream = new MemoryStream(exact);
        var name = await data.Store.SaveAsync(stream, exact.Length, "image/png", Ct);
        Assert.Equal(FileProductImageStore.MaximumBytes, new FileInfo(Path.Combine(data.ImageDirectory, name)).Length);
        data.Store.Delete(name);
        using var larger = new MemoryStream(new byte[FileProductImageStore.MaximumBytes + 1]);
        await Assert.ThrowsAsync<CatalogException>(() => data.Store.SaveAsync(larger, 1, "image/png", Ct));
        Assert.Empty(Directory.GetFiles(data.ImageDirectory));
    }

    [Fact]
    public async Task ImageStorageRejectsOversizeActiveContentAndTraversal()
    {
        await using var data = await TestCatalog.CreateAsync();
        using var svg = new MemoryStream("<svg onload='alert(1)'/>"u8.ToArray());
        await Assert.ThrowsAsync<CatalogException>(() =>
            data.Store.SaveAsync(svg, svg.Length, "image/svg+xml", Ct));
        using var fake = new MemoryStream("not a png"u8.ToArray());
        await Assert.ThrowsAsync<CatalogException>(() =>
            data.Store.SaveAsync(fake, fake.Length, "image/png", Ct));
        using var png = new MemoryStream(Png);
        await Assert.ThrowsAsync<CatalogException>(() =>
            data.Store.SaveAsync(png, FileProductImageStore.MaximumBytes + 1, "image/png", Ct));
        Assert.Throws<CatalogException>(() => data.Store.Open("..\\secret.png"));
        Assert.Throws<CatalogException>(() => data.Store.Delete("../secret.png"));
        var stored = await data.Store.SaveAsync(png, png.Length, "image/png", Ct);
        Assert.Matches("^[a-f0-9]{32}\\.png$", stored);
        data.Store.Delete(stored);
    }

    private static ProductWriteRequest Request(Guid categoryId, string slug) => new()
    {
        CategoryId = categoryId, Name = slug, Slug = slug, Sku = slug,
        Description = "Descripcion real del producto de prueba.", Price = 1000, Unit = "unidad"
    };

    [Fact]
    public async Task PublicLookupByIdSurvivesSlugChangesAndStillHonorsVisibility()
    {
        await using var data = await TestCatalog.CreateAsync();
        var category = await data.CategoryAsync();
        var request = Request(category.Id, "original");
        var created = await data.Service.SaveProductAsync(null, request, data.UserId, Ct);
        data.Db.ChangeTracker.Clear();
        var renamed = await data.Service.SaveProductAsync(created.Product.Id,
            request with { Slug = "nuevo", UpdatedAtUtc = created.UpdatedAtUtc }, data.UserId, Ct);
        var found = await data.Service.PublicProductAsync(created.Product.Id, Ct);
        Assert.Equal("nuevo", found.Slug);
        data.Db.ChangeTracker.Clear();
        await data.Service.SaveProductAsync(created.Product.Id,
            request with { Slug = "nuevo", IsActive = false, UpdatedAtUtc = renamed.UpdatedAtUtc }, data.UserId, Ct);
        var hidden = await Assert.ThrowsAsync<CatalogException>(() => data.Service.PublicProductAsync(created.Product.Id, Ct));
        Assert.Equal(CatalogError.NotFound, hidden.Error);
        var missing = await Assert.ThrowsAsync<CatalogException>(() => data.Service.PublicProductAsync(Guid.NewGuid(), Ct));
        Assert.Equal(CatalogError.NotFound, missing.Error);
    }

    private sealed class TestCatalog : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
        public string ImageDirectory { get; } = Path.Combine(Path.GetTempPath(), "mercado-cercano-catalog-" + Guid.NewGuid().ToString("N"));
        public Guid UserId { get; } = Guid.NewGuid();
        public StoreDbContext Db { get; private set; } = null!;
        public CatalogService Service { get; private set; } = null!;
        public FileProductImageStore Store { get; private set; } = null!;

        public static async Task<TestCatalog> CreateAsync()
        {
            var data = new TestCatalog();
            await data.connection.OpenAsync();
            data.Db = new(new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(data.connection).Options);
            await data.Db.Database.MigrateAsync();
            data.Db.Users.Add(new User
            {
                Id = data.UserId, Name = "Prueba", Email = "catalog@example.test",
                NormalizedEmail = "CATALOG@EXAMPLE.TEST", PasswordHash = "not-a-real-password",
                Role = UserRole.Administrator
            });
            await data.Db.SaveChangesAsync();
            data.Store = new(data.ImageDirectory);
            data.Service = new(data.Db, data.Store);
            return data;
        }

        public Task<CategoryResponse> CategoryAsync(string slug = "categoria", bool active = true, int order = 1) =>
            Service.SaveCategoryAsync(null, new CategoryWriteRequest
            {
                Name = slug, Slug = slug, IsActive = active, SortOrder = order
            }, UserId, Ct);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
            if (Directory.Exists(ImageDirectory)) Directory.Delete(ImageDirectory, recursive: true);
        }
    }
}
