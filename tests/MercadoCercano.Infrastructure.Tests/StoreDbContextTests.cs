using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Tests;

public sealed class StoreDbContextTests
{
    [Fact]
    public async Task MigrationsCreateSchemaAndDevelopmentSeedIsIdempotent()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);

        await dbContext.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(dbContext);
        await DevelopmentDataSeeder.SeedAsync(dbContext);

        Assert.Equal(1, await dbContext.Categories.CountAsync());
        Assert.Equal(2, await dbContext.Products.CountAsync());
        Assert.All(
            await dbContext.Products.ToListAsync(),
            product =>
            {
                Assert.StartsWith("DEMO-", product.Sku);
                Assert.False(product.IsActive);
            });

        var tableNames = await GetTableNamesAsync(connection);
        Assert.Contains("Orders", tableNames);
        Assert.Contains("InventoryMovements", tableNames);
        Assert.Contains("PromotionUsages", tableNames);
        Assert.Contains("OrderStatusHistory", tableNames);
        Assert.Contains("PaymentEvidence", tableNames);
        Assert.Contains("OrderReturns", tableNames);
        Assert.Contains("OrderReturnItems", tableNames);
        Assert.Contains("__EFMigrationsHistory", tableNames);
    }

    [Fact]
    public async Task DatabaseRejectsNonPositiveProductPrices()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var category = new Category
        {
            Name = "Prueba",
            Slug = "prueba"
        };
        dbContext.Categories.Add(category);
        dbContext.Products.Add(CreateProduct(category.Id, "INVALID-PRICE", price: 0));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task SqliteStoresCopAsExactIntegersAndProtectsInventoryHistory()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var category = new Category
        {
            Name = "Prueba",
            Slug = "prueba"
        };
        dbContext.Categories.Add(category);

        var priceAboveJavaScriptSafeInteger = 9_007_199_254_740_993L;
        var product = CreateProduct(category.Id, "EXACT-COP", priceAboveJavaScriptSafeInteger);
        product.StockQuantity = 4;
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var storedProduct = await dbContext.Products.SingleAsync(item => item.Id == product.Id);
        Assert.Equal(priceAboveJavaScriptSafeInteger, storedProduct.Price);

        dbContext.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            MovementType = InventoryMovementType.PurchaseEntry,
            QuantityDelta = 4,
            PreviousQuantity = 0,
            NewQuantity = 4,
            Reason = "Ingreso de inventario para prueba"
        });
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();
        dbContext.Products.Remove(await dbContext.Products.SingleAsync(item => item.Id == product.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        dbContext.ChangeTracker.Clear();
        Assert.True(await dbContext.Products.AnyAsync(item => item.Id == product.Id));
        Assert.True(await dbContext.InventoryMovements.AnyAsync(item => item.ProductId == product.Id));
    }

    private static async Task<SqliteConnection> CreateOpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static StoreDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection, sqlite =>
                sqlite.MigrationsAssembly(typeof(StoreDbContext).Assembly.GetName().Name))
            .Options;

        return new StoreDbContext(options);
    }

    private static Product CreateProduct(Guid categoryId, string sku, long price)
    {
        return new Product
        {
            CategoryId = categoryId,
            Sku = sku,
            Name = sku,
            Slug = sku.ToLowerInvariant(),
            Description = "Producto de prueba",
            Price = price,
            Cost = 0,
            StockQuantity = 0,
            LowStockThreshold = 0,
            Unit = "unidad"
        };
    }

    private static async Task<HashSet<string>> GetTableNamesAsync(SqliteConnection connection)
    {
        var tableNames = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }
}
