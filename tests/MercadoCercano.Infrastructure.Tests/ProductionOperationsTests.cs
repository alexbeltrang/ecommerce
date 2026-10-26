using System.Text.Json;
using MercadoCercano.Api.Controllers;
using MercadoCercano.Api.Operations;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MercadoCercano.Infrastructure.Tests;

public sealed class ProductionOperationsTests
{
    private const string TestPassword = "Pruebas1!administrador";

    [Fact]
    public async Task ConfigurationKeepsProductionFilesOutsidePublishedSite()
    {
        await using var fixture = await Fixture.CreateAsync();
        var storage = ProductionStorage.Load(fixture.Configuration, fixture.Site);
        Assert.Equal(14, storage.RetentionCount);
        Assert.False(ProductionStorage.Overlaps(storage.Root, fixture.Site));
        storage.ValidateServer(fixture.Configuration, fixture.Site);
        foreach (var pair in new[]
        {
            ("Storage:Root", ""), ("Storage:Root", "relative"),
            ("Storage:Root", fixture.Site), ("Storage:Root", Path.Combine(fixture.Site, "private")),
            ("Storage:Root", Path.GetPathRoot(fixture.Site)!),
            ("Operations:BackupRoot", storage.Root), ("Operations:BackupRoot", Path.Combine(storage.Root, "backups")),
            ("Operations:RetentionCount", "0"), ("Operations:RetentionCount", "366")
        })
        {
            fixture.Configuration[pair.Item1] = pair.Item2;
            Assert.Throws<InvalidOperationException>(() => ProductionStorage.Load(fixture.Configuration, fixture.Site));
            fixture.ResetConfiguration();
        }
    }

    [Theory]
    [InlineData("*")]
    [InlineData("*.example.test")]
    [InlineData("https://tienda.example.test")]
    [InlineData("")]
    public async Task ServerRefusesInvalidAllowedHosts(string hosts)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Configuration["AllowedHosts"] = hosts;
        Assert.Throws<InvalidOperationException>(() => fixture.Storage.ValidateServer(fixture.Configuration, fixture.Site));
    }

    [Fact]
    public async Task ServerRequiresPublishedAngularDatabaseAndNoDevelopmentBootstrap()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Configuration["BOOTSTRAP_ADMIN_PASSWORD"] = "not-used";
        Assert.Throws<InvalidOperationException>(() => fixture.Storage.ValidateServer(fixture.Configuration, fixture.Site));
        fixture.Configuration["BOOTSTRAP_ADMIN_PASSWORD"] = "";
        File.Delete(Path.Combine(fixture.Site, "wwwroot", "index.html"));
        Assert.Throws<InvalidOperationException>(() => fixture.Storage.ValidateServer(fixture.Configuration, fixture.Site));
        File.WriteAllText(Path.Combine(fixture.Site, "wwwroot", "index.html"), "<html></html>");
        File.Delete(fixture.Storage.DatabasePath);
        Assert.Throws<InvalidOperationException>(() => fixture.Storage.ValidateServer(fixture.Configuration, fixture.Site));
    }

    [Fact]
    public async Task ProcessLeaseRejectsASecondWriterAndCanBeReacquiredAfterDisposal()
    {
        await using var fixture = await Fixture.CreateAsync();
        using (fixture.Storage.AcquireLease()) Assert.Throws<IOException>(() => fixture.Storage.AcquireLease());
        using var next = fixture.Storage.AcquireLease();
        Assert.True(next.CanWrite);
    }

    [Fact]
    public async Task ReadinessRequiresCurrentSchemaAndDefinitiveActiveAdministrator()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Storage.OpenDatabase();
        await ProductionReadiness.RequireReadyAsync(db, default);
        await db.Users.ExecuteUpdateAsync(update => update.SetProperty(user => user.MustChangePassword, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProductionReadiness.RequireReadyAsync(db, default));
        await db.Users.ExecuteUpdateAsync(update => update.SetProperty(user => user.MustChangePassword, false).SetProperty(user => user.IsActive, false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProductionReadiness.RequireReadyAsync(db, default));
        await db.GetService<IMigrator>().MigrateAsync("20261020141508_PromotionsAndCoupons");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProductionReadiness.RequireReadyAsync(db, default));
    }

    [Fact]
    public async Task HealthControllerReturnsGenericUnavailableWithoutStartingHost()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Storage.OpenDatabase();
        var controller = new HealthController(db, NullLogger<HealthController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<OkObjectResult>(controller.Live());
        Assert.IsType<OkObjectResult>(await controller.Ready(default));
        await db.Users.ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        var failure = Assert.IsType<ObjectResult>(await controller.Ready(default));
        Assert.Equal(503, failure.StatusCode);
        Assert.DoesNotContain(fixture.Root, JsonSerializer.Serialize(failure.Value));
    }

    [Fact]
    public void ProductionHeadersSupportAngularWithoutInlineScriptsAndApiPathsNeverBecomeSpa()
    {
        var context = new DefaultHttpContext();
        ProductionWeb.SetHeaders(context.Response);
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions);
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
        Assert.Contains("script-src 'self';", context.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", context.Response.Headers.ContentSecurityPolicy.ToString());
        foreach (var path in new[] { "/api", "/api/v1/missing", "/API/v1/private" }) Assert.True(ProductionWeb.IsApiPath(path));
        foreach (var path in new[] { "/", "/login", "/admin/usuarios", "/mi-cuenta", "/api-not-a-prefix" }) Assert.False(ProductionWeb.IsApiPath(path));
    }

    [Fact]
    public async Task OfflineProvisioningCreatesOnlyOneHashedAdministratorAndAudit()
    {
        await using var fixture = await Fixture.CreateAsync(administrator: false);
        await using var db = fixture.Storage.OpenDatabase();
        await ProductionAdministrator.CreateAsync(db, "  Admin prueba  ", " admin@example.test ", TestPassword, default);
        var user = Assert.Single(await db.Users.AsNoTracking().ToListAsync());
        Assert.Equal("Admin prueba", user.Name);
        Assert.Equal("ADMIN@EXAMPLE.TEST", user.NormalizedEmail);
        Assert.Equal(UserRole.Administrator, user.Role);
        Assert.False(user.MustChangePassword);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, TestPassword));
        var audit = Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Equal("Authentication.ProductionAdministratorCreated", audit.Action);
        Assert.DoesNotContain(TestPassword, JsonSerializer.Serialize(audit));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProductionAdministrator.CreateAsync(db, "Otro", "other@example.test", TestPassword, default));
        Assert.Single(await db.Users.ToListAsync());
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.StoreSettings.ToListAsync());
    }

    [Fact]
    public async Task ProvisioningRollsBackWhenAuditCannotBePersisted()
    {
        await using var fixture = await Fixture.CreateAsync(administrator: false);
        await using var db = fixture.Storage.OpenDatabase();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectProductionAudit BEFORE INSERT ON AuditLogs BEGIN SELECT RAISE(ABORT, 'test'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => ProductionAdministrator.CreateAsync(db, "Admin", "admin@example.test", TestPassword, default));
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task BackupPreservesDatabaseFilesAndKeysAndVerifiesChecksums()
    {
        await using var fixture = await Fixture.CreateAsync();
        var manager = new BackupManager(fixture.Storage);
        var backup = await manager.CreateAsync(default);
        await manager.VerifyAsync(Path.GetFileName(backup), default);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(Path.Combine(backup, "manifest.json")))!;
        Assert.Contains(manifest.Files, item => item.RelativePath == "database/mercado-cercano.db");
        Assert.Contains(manifest.Files, item => item.RelativePath == "products/test-image.png");
        Assert.Contains(manifest.Files, item => item.RelativePath == "payment-evidence/test-evidence.png");
        Assert.Contains(manifest.Files, item => item.RelativePath == "keys/test-key.xml");
        File.AppendAllText(Path.Combine(backup, "products", "test-image.png"), "tampered");
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.VerifyAsync(Path.GetFileName(backup), default));
    }

    [Fact]
    public async Task BackupRejectsMissingDatabaseReferencedEvidenceAndDoesNotPublishPartialCopy()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using (var db = fixture.Storage.OpenDatabase())
        {
            var category = new Category { Name = "Pruebas", Slug = "pruebas" };
            var product = new Product
            {
                CategoryId = category.Id, Name = "Producto", Slug = "producto", Sku = "TEST-PROD",
                Description = "", Price = 1000, Cost = 500, Unit = "Unidad"
            };
            db.Categories.Add(category); db.Products.Add(product);
            db.ProductImages.Add(new ProductImage { ProductId = product.Id, ImageUrl = "/api/v1/catalog/images/missing.png" });
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => new BackupManager(fixture.Storage).CreateAsync(default));
        Assert.Empty(Directory.GetDirectories(fixture.Storage.BackupRoot, "backup-*"));
        Assert.Single(Directory.GetDirectories(fixture.Storage.BackupRoot, ".partial-*"));
    }

    [Fact]
    public async Task RetentionKeepsExactlyFourteenVerifiedCopiesAndIgnoresUnrelatedDirectories()
    {
        await using var fixture = await Fixture.CreateAsync();
        var unrelated = Path.Combine(fixture.Storage.BackupRoot, "manual-keep");
        Directory.CreateDirectory(unrelated);
        File.WriteAllText(Path.Combine(unrelated, "keep.txt"), "not managed");
        var manager = new BackupManager(fixture.Storage);
        var copies = new List<string>();
        for (var index = 0; index < 15; index++) copies.Add(await manager.CreateAsync(default));
        Assert.Equal(14, Directory.GetDirectories(fixture.Storage.BackupRoot, "backup-*").Length);
        Assert.False(Directory.Exists(copies[0]));
        Assert.True(Directory.Exists(copies[^1]));
        Assert.True(File.Exists(Path.Combine(unrelated, "keep.txt")));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("C:\\outside")]
    [InlineData(".partial-backup-anything")]
    public async Task RestoreRejectsNamesOutsideCompletedManagedBackups(string name)
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new BackupManager(fixture.Storage).RestoreAsync(name, false, default));
    }

    [Fact]
    public async Task ManifestCannotTraverseDirectoriesOrOmitFiles()
    {
        await using var fixture = await Fixture.CreateAsync();
        var manager = new BackupManager(fixture.Storage);
        var copy = await manager.CreateAsync(default);
        var path = Path.Combine(copy, "manifest.json");
        var original = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(path))!;
        foreach (var invalid in new[]
        {
            original with { Files = [new("../outside", 0, "")] },
            original with { Files = original.Files.Skip(1).ToArray() },
            original with { Files = original.Files.Concat([original.Files[0]]).ToArray() }
        })
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(invalid));
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.VerifyAsync(Path.GetFileName(copy), default));
        }
    }

    [Fact]
    public async Task RestoreRecoversExactFilesAndCredentialsButRevokesEverySession()
    {
        await using var fixture = await Fixture.CreateAsync();
        var manager = new BackupManager(fixture.Storage);
        User before;
        await using (var db = fixture.Storage.OpenDatabase()) before = await db.Users.AsNoTracking().SingleAsync();
        var copy = await manager.CreateAsync(default);
        await using (var db = fixture.Storage.OpenDatabase())
            await db.Users.ExecuteUpdateAsync(update => update.SetProperty(user => user.Name, "Changed after backup"));
        File.WriteAllText(Path.Combine(fixture.Storage.Products, "test-image.png"), "changed");
        File.WriteAllText(Path.Combine(fixture.Storage.PaymentEvidence, "new-after-backup.png"), "new");
        var recovery = await manager.RestoreAsync(Path.GetFileName(copy), false, default);
        Assert.True(Directory.Exists(recovery));
        Assert.Equal("public image", File.ReadAllText(Path.Combine(fixture.Storage.Products, "test-image.png")));
        Assert.False(File.Exists(Path.Combine(fixture.Storage.PaymentEvidence, "new-after-backup.png")));
        Assert.True(File.Exists(Path.Combine(recovery, "payment-evidence", "new-after-backup.png")));
        await using var restored = fixture.Storage.OpenDatabase();
        var after = await restored.Users.AsNoTracking().SingleAsync();
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.Single(await restored.AuditLogs.Where(item => item.Action == "Operations.BackupRestored").ToListAsync());
        Assert.Equal("<key>opaque-test-key</key>", File.ReadAllText(Path.Combine(fixture.Storage.Keys, "test-key.xml")));
        await manager.VerifyAsync(Path.GetFileName(copy), default);
    }

    [Fact]
    public async Task CorruptBackupCannotReplaceLiveDataAndNewMachineRestoreDiscardsOldDpapiKeys()
    {
        await using var fixture = await Fixture.CreateAsync();
        var manager = new BackupManager(fixture.Storage);
        var bad = await manager.CreateAsync(default);
        File.AppendAllText(Path.Combine(bad, "database", "mercado-cercano.db"), "invalid-checksum");
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RestoreAsync(Path.GetFileName(bad), false, default));
        Assert.Equal("public image", File.ReadAllText(Path.Combine(fixture.Storage.Products, "test-image.png")));
        var good = await manager.CreateAsync(default);
        await manager.RestoreAsync(Path.GetFileName(good), true, default);
        Assert.Empty(Directory.GetFiles(fixture.Storage.Keys));
    }

    [Fact]
    public async Task OfflineCliAppliesMigrationsWithoutSeedingOrStartingAnHttpHost()
    {
        await using var fixture = await Fixture.CreateAsync(administrator: false);
        Assert.Equal(0, await MaintenanceCommands.RunAsync(["migrate"], fixture.Site));
        await using var db = fixture.Storage.OpenDatabase();
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Equal(1, await MaintenanceCommands.RunAsync(["restore", "../outside"], fixture.Site));
        Assert.Equal(1, await MaintenanceCommands.RunAsync(["unknown-command"], fixture.Site));
    }

    [Fact]
    public async Task PersistedWindowsDataProtectionKeysSurviveProviderRecreation()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var fixture = await Fixture.CreateAsync();
        File.Delete(Path.Combine(fixture.Storage.Keys, "test-key.xml"));
        IDataProtector Create() => DataProtectionProvider.Create(new DirectoryInfo(fixture.Storage.Keys), builder =>
        {
            builder.SetApplicationName("MercadoCercano");
            if (OperatingSystem.IsWindows()) builder.ProtectKeysWithDpapi(protectToLocalMachine: true);
        }).CreateProtector("isolated-production-test");
        var protectedValue = Create().Protect("session-test");
        Assert.Equal("session-test", Create().Unprotect(protectedValue));
        Assert.Contains(Directory.GetFiles(fixture.Storage.Keys), path => File.ReadAllText(path).Contains("encryptedSecret"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "mercado-production-test-" + Guid.NewGuid().ToString("N"));
        public string Site => Path.Combine(Root, "site");
        public ProductionStorage Storage => new(Path.Combine(Root, "data"), Path.Combine(Root, "backups"), 14);
        public IConfigurationRoot Configuration { get; private set; } = null!;
        public void ResetConfiguration() => Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Root"] = Storage.Root, ["Operations:BackupRoot"] = Storage.BackupRoot,
            ["Operations:RetentionCount"] = "14", ["AllowedHosts"] = "tienda.example.test"
        }).Build();
        public static async Task<Fixture> CreateAsync(bool administrator = true)
        {
            var value = new Fixture();
            value.ResetConfiguration();
            Directory.CreateDirectory(Path.Combine(value.Site, "wwwroot"));
            Directory.CreateDirectory(value.Storage.BackupRoot);
            foreach (var child in ProductionStorage.DataDirectories) Directory.CreateDirectory(Path.Combine(value.Storage.Root, child));
            File.WriteAllText(Path.Combine(value.Site, "wwwroot", "index.html"), "<html></html>");
            File.WriteAllText(Path.Combine(value.Site, "appsettings.json"), "{}");
            File.WriteAllText(Path.Combine(value.Site, "appsettings.Production.json"), JsonSerializer.Serialize(new
            {
                Storage = new { value.Storage.Root },
                Operations = new { value.Storage.BackupRoot, value.Storage.RetentionCount },
                AllowedHosts = "tienda.example.test"
            }));
            File.WriteAllText(Path.Combine(value.Storage.Products, "test-image.png"), "public image");
            File.WriteAllText(Path.Combine(value.Storage.PaymentEvidence, "test-evidence.png"), "private evidence");
            File.WriteAllText(Path.Combine(value.Storage.Keys, "test-key.xml"), "<key>opaque-test-key</key>");
            await using var db = value.Storage.OpenDatabase(create: true);
            await db.Database.MigrateAsync();
            if (administrator) await ProductionAdministrator.CreateAsync(db, "Admin prueba", "admin@example.test", TestPassword, default);
            return value;
        }
        public ValueTask DisposeAsync()
        {
            Directory.Delete(Root, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
