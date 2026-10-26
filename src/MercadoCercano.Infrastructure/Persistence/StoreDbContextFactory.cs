using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MercadoCercano.Infrastructure.Persistence;

public sealed class StoreDbContextFactory : IDesignTimeDbContextFactory<StoreDbContext>
{
    public StoreDbContext CreateDbContext(string[] args)
    {
        var configuredPath = Environment.GetEnvironmentVariable("Database__Path");
        var apiContentRoot = GetApiContentRoot();
        var databasePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(
                apiContentRoot,
                configuredPath ?? Path.Combine("..", "..", "BaseDatos", "mercado-cercano.db")));

        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (databaseDirectory is null)
        {
            throw new InvalidOperationException("La ruta configurada para SQLite no contiene un directorio.");
        }

        Directory.CreateDirectory(databaseDirectory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            DefaultTimeout = 30
        }.ToString();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connectionString, sqlite =>
                sqlite.MigrationsAssembly(typeof(StoreDbContext).Assembly.GetName().Name))
            .Options;

        return new StoreDbContext(options);
    }

    private static string GetApiContentRoot()
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var directory = currentDirectory; directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MercadoCercano.sln")))
            {
                return Path.Combine(directory.FullName, "src", "MercadoCercano.Api");
            }
        }

        if (File.Exists(Path.Combine(currentDirectory.FullName, "MercadoCercano.Api.csproj")))
        {
            return currentDirectory.FullName;
        }

        throw new InvalidOperationException(
            "No se encontró la raíz de la solución. Ejecuta EF Core desde el repositorio Mercado Cercano.");
    }
}
