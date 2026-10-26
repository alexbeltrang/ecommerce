using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using MercadoCercano.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Api.Operations;

public sealed partial class BackupManager(ProductionStorage storage)
{
    private const string ManifestName = "manifest.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<string> CreateAsync(CancellationToken ct)
    {
        using var lease = storage.AcquireLease(TimeSpan.FromSeconds(60));
        ProductionStorage.RejectLinks(storage.BackupRoot);
        Directory.CreateDirectory(storage.BackupRoot);
        var id = "backup-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + Guid.NewGuid().ToString("N")[..8];
        var staging = Path.Combine(storage.BackupRoot, ".partial-" + id);
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(Path.Combine(staging, "database"));
        await using (var source = Connection(storage.DatabasePath, SqliteOpenMode.ReadOnly))
        await using (var destination = Connection(Path.Combine(staging, "database", "mercado-cercano.db"), SqliteOpenMode.ReadWriteCreate))
        {
            await source.OpenAsync(ct);
            await destination.OpenAsync(ct);
            source.BackupDatabase(destination);
            await using var journal = destination.CreateCommand();
            journal.CommandText = "PRAGMA journal_mode=DELETE";
            if (!string.Equals(Convert.ToString(await journal.ExecuteScalarAsync(ct)), "delete", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("No se pudo consolidar SQLite en un archivo de respaldo independiente.");
        }
        foreach (var directory in ProductionStorage.DataDirectories.Where(item => item != "database"))
        {
            var original = Path.Combine(storage.Root, directory);
            Directory.CreateDirectory(Path.Combine(staging, directory));
            if (Directory.Exists(original)) CopyTree(original, Path.Combine(staging, directory), ct);
        }
        var entries = new List<BackupEntry>();
        foreach (var file in Files(staging))
        {
            ct.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(staging, file).Replace(Path.DirectorySeparatorChar, '/');
            await using var input = File.OpenRead(file);
            entries.Add(new(relative, input.Length, Convert.ToHexString(await SHA256.HashDataAsync(input, ct))));
        }
        var manifest = new BackupManifest(1, DateTimeOffset.UtcNow, entries);
        await File.WriteAllTextAsync(Path.Combine(staging, ManifestName), JsonSerializer.Serialize(manifest, JsonOptions), ct);
        await VerifyDirectoryAsync(staging, ct);
        var completed = Path.Combine(storage.BackupRoot, id);
        Directory.Move(staging, completed);
        await PruneAsync(id, ct);
        return completed;
    }

    public Task VerifyAsync(string id, CancellationToken ct) => VerifyDirectoryAsync(Resolve(id), ct);

    public async Task<string> RestoreAsync(string id, bool newMachine, CancellationToken ct)
    {
        using var lease = storage.AcquireLease(TimeSpan.FromSeconds(60));
        var backup = Resolve(id);
        await VerifyDirectoryAsync(backup, ct);
        var operationId = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(storage.Root, ".restore-" + operationId);
        var recovery = Path.Combine(storage.Root, "recovery-" + operationId);
        Directory.CreateDirectory(staging);
        foreach (var directory in ProductionStorage.DataDirectories)
        {
            var target = Path.Combine(staging, directory);
            Directory.CreateDirectory(target);
            if (!newMachine || directory != "keys") CopyTree(Path.Combine(backup, directory), target, ct);
        }
        var stagedStorage = storage with { Root = staging };
        await using (var db = stagedStorage.OpenDatabase())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Users\" SET \"SecurityStamp\" = lower(hex(randomblob(16))), \"UpdatedAtUtc\" = CURRENT_TIMESTAMP;", ct);
            db.AuditLogs.Add(new AuditLog
            {
                Action = "Operations.BackupRestored", EntityType = "Backup", EntityId = id,
                DetailsJson = JsonSerializer.Serialize(new { NewMachine = newMachine })
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        Directory.CreateDirectory(recovery);
        var installed = new List<string>();
        var preserved = new List<string>();
        try
        {
            foreach (var directory in ProductionStorage.DataDirectories)
            {
                ct.ThrowIfCancellationRequested();
                var current = Path.Combine(storage.Root, directory);
                ProductionStorage.RejectLinks(current);
                if (Directory.Exists(current))
                {
                    Directory.Move(current, Path.Combine(recovery, directory));
                    preserved.Add(directory);
                }
                Directory.Move(Path.Combine(staging, directory), current);
                installed.Add(directory);
            }
        }
        catch (Exception restoreError)
        {
            try
            {
                foreach (var directory in installed.AsEnumerable().Reverse())
                    Directory.Move(Path.Combine(storage.Root, directory), Path.Combine(staging, directory));
                foreach (var directory in preserved.AsEnumerable().Reverse())
                    Directory.Move(Path.Combine(recovery, directory), Path.Combine(storage.Root, directory));
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException("Fallo la restauracion y su reversion. Conserva IIS offline y revisa los directorios de recuperacion.", restoreError, rollbackError);
            }
            throw;
        }
        Directory.Delete(staging);
        return recovery;
    }

    private string Resolve(string id)
    {
        if (!BackupName().IsMatch(id))
            throw new InvalidOperationException("Selecciona el nombre exacto de un respaldo completo dentro de Operations:BackupRoot.");
        ProductionStorage.RejectLinks(storage.BackupRoot);
        var path = Path.Combine(storage.BackupRoot, id);
        ProductionStorage.RejectLinks(path);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("El respaldo seleccionado no existe.");
        return path;
    }

    private async Task PruneAsync(string created, CancellationToken ct)
    {
        var candidates = Directory.EnumerateDirectories(storage.BackupRoot)
            .Where(path => BackupName().IsMatch(Path.GetFileName(path))).OrderByDescending(Path.GetFileName, StringComparer.Ordinal).ToArray();
        var keep = new[] { created }.Concat(candidates.Select(Path.GetFileName).OfType<string>().Where(name => name != created))
            .Take(storage.RetentionCount).ToHashSet(StringComparer.Ordinal);
        foreach (var path in candidates.Where(path => !keep.Contains(Path.GetFileName(path))))
        {
            await VerifyDirectoryAsync(path, ct);
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task VerifyDirectoryAsync(string directory, CancellationToken ct)
    {
        ProductionStorage.RejectLinks(directory);
        var manifestPath = Path.Combine(directory, ManifestName);
        RejectFileLink(manifestPath);
        if (new FileInfo(manifestPath).Length > 100_000_000)
            throw new InvalidOperationException("El manifiesto del respaldo supera el limite permitido.");
        var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath, ct))
            ?? throw new InvalidOperationException("Falta el manifiesto del respaldo.");
        if (manifest.FormatVersion != 1 || manifest.Files is null || manifest.Files.Count == 0)
            throw new InvalidOperationException("Formato de respaldo no compatible.");
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            if (entry is null || !ValidRelativePath(entry.RelativePath) || !expected.Add(entry.RelativePath))
                throw new InvalidOperationException("El respaldo contiene rutas invalidas o duplicadas.");
            var path = Path.Combine(directory, Path.Combine(entry.RelativePath.Split('/')));
            ProductionStorage.RejectLinks(Path.GetDirectoryName(path)!);
            RejectFileLink(path);
            await using var input = File.OpenRead(path);
            if (input.Length != entry.Length || Convert.ToHexString(await SHA256.HashDataAsync(input, ct)) != entry.Sha256)
                throw new InvalidOperationException("Un archivo del respaldo no coincide con su longitud o SHA-256.");
        }
        var actual = Files(directory).Where(path => path != manifestPath)
            .Select(path => Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!actual.SetEquals(expected) || !expected.Contains("database/mercado-cercano.db"))
            throw new InvalidOperationException("El respaldo esta incompleto o contiene archivos no declarados.");
        foreach (var child in Directory.EnumerateDirectories(directory))
            if (!ProductionStorage.DataDirectories.Contains(Path.GetFileName(child), StringComparer.Ordinal))
                throw new InvalidOperationException("El respaldo contiene un directorio no reconocido.");
        foreach (var child in ProductionStorage.DataDirectories)
            if (!Directory.Exists(Path.Combine(directory, child)))
                throw new InvalidOperationException("Falta un directorio del respaldo.");
        await using var db = Connection(Path.Combine(directory, "database", "mercado-cercano.db"), SqliteOpenMode.ReadOnly);
        await db.OpenAsync(ct);
        await using var check = db.CreateCommand();
        check.CommandText = "PRAGMA integrity_check";
        await using (var results = await check.ExecuteReaderAsync(ct))
        {
            if (!await results.ReadAsync(ct) || results.GetString(0) != "ok" || await results.ReadAsync(ct))
                throw new InvalidOperationException("SQLite no supera integrity_check.");
        }
        check.CommandText = "PRAGMA foreign_key_check";
        await using (var results = await check.ExecuteReaderAsync(ct))
            if (await results.ReadAsync(ct)) throw new InvalidOperationException("SQLite contiene relaciones invalidas.");
        await RequireFilesAsync(db, directory, "SELECT ImageUrl FROM ProductImages", "products", true, ct);
        await RequireFilesAsync(db, directory, "SELECT StoredFileName FROM PaymentEvidence", "payment-evidence", false, ct);
    }

    private static async Task RequireFilesAsync(SqliteConnection db, string root, string sql, string directory, bool url, CancellationToken ct)
    {
        await using var command = db.CreateCommand();
        command.CommandText = sql;
        await using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
        {
            var value = rows.GetString(0);
            var name = url ? value[(value.LastIndexOf('/') + 1)..] : value;
            if (!ValidRelativePath(directory + "/" + name) || name.Contains('/') || !File.Exists(Path.Combine(root, directory, name)))
                throw new InvalidOperationException("Falta un archivo referenciado por SQLite.");
        }
    }

    private static bool ValidRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\\') || value.Contains(':') || Path.IsPathRooted(value))
            return false;
        var parts = value.Split('/');
        return parts.Length >= 2 && ProductionStorage.DataDirectories.Contains(parts[0], StringComparer.Ordinal) &&
            (parts[0] != "database" || value == "database/mercado-cercano.db") &&
            parts.All(part => part.Length != 0 && part is not "." and not ".." && part.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
    }

    private static void CopyTree(string source, string destination, CancellationToken ct)
    {
        ProductionStorage.RejectLinks(source);
        foreach (var path in Files(source))
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, Path.GetRelativePath(source, path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(path, target, overwrite: false);
        }
    }

    private static IEnumerable<string> Files(string directory)
    {
        ProductionStorage.RejectLinks(directory);
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            RejectFileLink(file);
            yield return file;
        }
        foreach (var child in Directory.EnumerateDirectories(directory))
            foreach (var file in Files(child)) yield return file;
    }

    private static void RejectFileLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("El respaldo no admite archivos enlazados.");
    }

    private static SqliteConnection Connection(string path, SqliteOpenMode mode) => new(new SqliteConnectionStringBuilder
    {
        DataSource = path, Mode = mode, ForeignKeys = true, Pooling = false, DefaultTimeout = 30
    }.ToString());

    [GeneratedRegex(@"^backup-[0-9]{8}T[0-9]{13}Z-[a-f0-9]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex BackupName();
}

public sealed record BackupEntry(string RelativePath, long Length, string Sha256);
public sealed record BackupManifest(int FormatVersion, DateTimeOffset CreatedAtUtc, IReadOnlyList<BackupEntry> Files);
