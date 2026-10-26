using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MercadoCercano.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddStorePersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath,
        bool allowCreateDatabase = true)
    {
        var configuredPath = configuration["Database:Path"];
        var databasePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath ?? Path.Combine("..", "..", "BaseDatos", "mercado-cercano.db")));

        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (databaseDirectory is null)
        {
            throw new InvalidOperationException("La ruta configurada para SQLite no contiene un directorio.");
        }

        Directory.CreateDirectory(databaseDirectory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = allowCreateDatabase ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            ForeignKeys = true,
            DefaultTimeout = 30
        }.ToString();

        services.AddDbContext<StoreDbContext>(options =>
            options.UseSqlite(connectionString, sqlite =>
                sqlite.MigrationsAssembly(typeof(StoreDbContext).Assembly.GetName().Name)));

        return services;
    }
}
