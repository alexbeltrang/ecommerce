using MercadoCercano.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Persistence;

public static class DevelopmentDataSeeder
{
    private static readonly Guid DemonstrationCategoryId =
        Guid.Parse("3f95812f-31ab-4b98-93d8-d8f5e3e42001");

    private static readonly (Guid Id, string Sku, string Name, string Slug)[] DemonstrationProducts =
    [
        (
            Guid.Parse("3f95812f-31ab-4b98-93d8-d8f5e3e42002"),
            "DEMO-CAFE-001",
            "Producto demostrativo - Café molido",
            "producto-demostrativo-cafe-molido"),
        (
            Guid.Parse("3f95812f-31ab-4b98-93d8-d8f5e3e42003"),
            "DEMO-SNACK-001",
            "Producto demostrativo - Snack",
            "producto-demostrativo-snack")
    ];

    public static async Task SeedAsync(StoreDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Categories.AnyAsync(category => category.Id == DemonstrationCategoryId, cancellationToken))
        {
            dbContext.Categories.Add(new Category
            {
                Id = DemonstrationCategoryId,
                Name = "Categoría demostrativa",
                Slug = "categoria-demostrativa",
                Description = "Datos ficticios para desarrollo local. No representan productos ni precios reales.",
                SortOrder = 0
            });
        }

        foreach (var (id, sku, name, slug) in DemonstrationProducts)
        {
            if (await dbContext.Products.AnyAsync(product => product.Id == id, cancellationToken))
            {
                continue;
            }

            dbContext.Products.Add(new Product
            {
                Id = id,
                CategoryId = DemonstrationCategoryId,
                Sku = sku,
                Name = name,
                Slug = slug,
                Description = "Producto ficticio de demostración. Sustituir por información verificada antes de vender.",
                Price = 1000,
                Cost = 0,
                StockQuantity = 10,
                LowStockThreshold = 2,
                Unit = "unidad",
                IsFeatured = false,
                IsActive = false
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
