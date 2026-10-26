using Microsoft.Data.Sqlite;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Api.Operations;

public sealed record ProductionStorage(string Root, string BackupRoot, int RetentionCount)
{
    public string DatabasePath => Path.Combine(Root, "database", "mercado-cercano.db");
    public string Products => Path.Combine(Root, "products");
    public string PaymentEvidence => Path.Combine(Root, "payment-evidence");
    public string Keys => Path.Combine(Root, "keys");
    public static readonly string[] DataDirectories = ["database", "products", "payment-evidence", "keys"];

    public static ProductionStorage Load(IConfiguration configuration, string contentRoot)
    {
        var root = AbsoluteDirectory(configuration["Storage:Root"], "Storage:Root");
        var backups = AbsoluteDirectory(configuration["Operations:BackupRoot"], "Operations:BackupRoot");
        if (Overlaps(root, contentRoot) || Overlaps(backups, contentRoot) || Overlaps(root, backups))
            throw new InvalidOperationException("Datos, respaldos y publicacion deben estar en directorios separados, sin anidarse.");
        var retention = configuration.GetValue<int?>("Operations:RetentionCount") ?? 14;
        if (retention is < 1 or > 365)
            throw new InvalidOperationException("Operations:RetentionCount debe estar entre 1 y 365.");
        foreach (var child in DataDirectories) RejectLinks(Path.Combine(root, child));
        var database = Path.Combine(root, "database", "mercado-cercano.db");
        if (File.Exists(database) && (File.GetAttributes(database) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("SQLite de produccion no admite enlaces.");
        return new(root, backups, retention);
    }

    private static string AbsoluteDirectory(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new InvalidOperationException($"{key} debe ser una ruta absoluta privada.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        if (OperatingSystem.IsWindows() && Path.GetPathRoot(full)!.StartsWith(@"\\", StringComparison.Ordinal))
            throw new InvalidOperationException($"{key} debe estar en disco local, no en un recurso UNC.");
        if (full == Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full)!))
            throw new InvalidOperationException($"{key} no puede ser la raiz de un volumen.");
        RejectLinks(full);
        return full;
    }

    public static bool Overlaps(string left, string right)
    {
        left = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
        right = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
        return left.Equals(right, StringComparison.OrdinalIgnoreCase) ||
            left.StartsWith(right + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            right.StartsWith(left + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static void RejectLinks(string path)
    {
        for (var item = new DirectoryInfo(path); item is not null; item = item.Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("El almacenamiento de operaciones no admite enlaces ni junctions.");
    }

    public FileStream AcquireLease(TimeSpan wait = default)
    {
        RejectLinks(Root);
        Directory.CreateDirectory(Root);
        var path = Path.Combine(Root, ".application.lock");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("El bloqueo de operaciones no admite enlaces.");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (timer.Elapsed < wait) { Thread.Sleep(250); }
        }
    }

    public StoreDbContext OpenDatabase(bool create = false)
    {
        RejectLinks(Path.GetDirectoryName(DatabasePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath, ForeignKeys = true, DefaultTimeout = 30,
            Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite, Pooling = false
        };
        return new(new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection.ToString()).Options);
    }

    public void ValidateServer(IConfiguration configuration, string contentRoot)
    {
        var hosts = configuration["AllowedHosts"]?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts is null || hosts.Length == 0 || hosts.Any(host => host.Contains('*') || Uri.CheckHostName(host) == UriHostNameType.Unknown))
            throw new InvalidOperationException("Produccion requiere AllowedHosts explicitos, sin comodines ni esquemas.");
        if (!File.Exists(DatabasePath))
            throw new InvalidOperationException("Falta SQLite de produccion. Ejecuta el mantenimiento migrate y create-admin antes de iniciar IIS.");
        if (!File.Exists(Path.Combine(contentRoot, "wwwroot", "index.html")))
            throw new InvalidOperationException("Falta Angular publicado en wwwroot. Usa Publish-Iis.ps1.");
        if (new[] { "BOOTSTRAP_ADMIN_NAME", "BOOTSTRAP_ADMIN_EMAIL", "BOOTSTRAP_ADMIN_PASSWORD" }
            .Any(key => !string.IsNullOrEmpty(configuration[key])))
            throw new InvalidOperationException("Retira las variables de bootstrap de desarrollo en produccion.");
    }
}
