using System.ComponentModel.DataAnnotations;
using System.Reflection;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Api.Controllers;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MercadoCercano.Infrastructure.Tests;

public sealed class InitialAdministratorServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly PasswordHasher<User> Hasher = new();
    private static CreateInitialAdministratorRequest Request => new()
    {
        Name = " Administrador ficticio ", Email = " Example@localhost.test ", Password = "TestOnly-Strong-123!"
    };

    [Fact]
    public async Task CreatesFirstAdministratorWithHashedPasswordAndAuditThenClosesSetup()
    {
        await using var db = CreateContext("Data Source=:memory:");
        await db.Database.OpenConnectionAsync(Ct);
        await db.Database.EnsureCreatedAsync(Ct);
        var service = new InitialAdministratorService(db, Hasher, true);
        Assert.True((await service.StatusAsync(Ct)).Available);
        var result = await service.CreateAsync(Request, Ct);
        var user = await db.Users.SingleAsync(Ct);
        Assert.Equal(result.Id, user.Id);
        Assert.Equal("Administrador ficticio", user.Name);
        Assert.Equal("Example@localhost.test", user.Email);
        Assert.Equal("EXAMPLE@LOCALHOST.TEST", user.NormalizedEmail);
        Assert.Equal(UserRole.Administrator, user.Role);
        Assert.True(user.IsActive);
        Assert.NotEqual(Request.Password, user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, Hasher.VerifyHashedPassword(user, user.PasswordHash, Request.Password));
        var audit = await db.AuditLogs.SingleAsync(Ct);
        Assert.Equal(user.Id, audit.UserId);
        Assert.Equal("Authentication.DevelopmentAdministratorCreated", audit.Action);
        Assert.False((await service.StatusAsync(Ct)).Available);
        var error = await Assert.ThrowsAsync<InitialAdministratorException>(() =>
            service.CreateAsync(Request with { Email = "other@localhost.test" }, Ct));
        Assert.Equal(InitialAdministratorError.Conflict, error.Error);
        Assert.Equal(1, await db.Users.CountAsync(Ct));
    }

    [Theory]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Administrator, false)]
    [InlineData(UserRole.OrderOperator, true)]
    [InlineData(UserRole.OrderOperator, false)]
    public async Task AnyExistingUserClosesSetupWithoutResettingOrReactivatingAccounts(UserRole role, bool active)
    {
        await using var db = CreateContext("Data Source=:memory:");
        await db.Database.OpenConnectionAsync(Ct);
        await db.Database.EnsureCreatedAsync(Ct);
        var existing = new User
        {
            Name = "Existente", Email = "existing@localhost.test", NormalizedEmail = "EXISTING@LOCALHOST.TEST",
            Role = role, IsActive = active, PasswordHash = "unchanged-test-fixture"
        };
        db.Users.Add(existing);
        await db.SaveChangesAsync(Ct);
        var service = new InitialAdministratorService(db, Hasher, true);
        Assert.False((await service.StatusAsync(Ct)).Available);
        var error = await Assert.ThrowsAsync<InitialAdministratorException>(() => service.CreateAsync(Request, Ct));
        Assert.Equal(InitialAdministratorError.Conflict, error.Error);
        Assert.Equal("unchanged-test-fixture", (await db.Users.SingleAsync(Ct)).PasswordHash);
        Assert.Equal(active, existing.IsActive);
        Assert.Empty(await db.AuditLogs.ToListAsync(Ct));
    }

    [Fact]
    public async Task NonDevelopmentNeverPermitsSetupEvenWhenDatabaseIsEmpty()
    {
        await using var db = CreateContext("Data Source=:memory:");
        await db.Database.OpenConnectionAsync(Ct);
        await db.Database.EnsureCreatedAsync(Ct);
        var service = new InitialAdministratorService(db, Hasher, false);
        Assert.False((await service.StatusAsync(Ct)).Available);
        var error = await Assert.ThrowsAsync<InitialAdministratorException>(() => service.CreateAsync(Request, Ct));
        Assert.Equal(InitialAdministratorError.NotFound, error.Error);
        Assert.Empty(await db.Users.ToListAsync(Ct));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("testonly-strong-123!")]
    [InlineData("TESTONLY-STRONG-123!")]
    [InlineData("12345678!")]
    [InlineData("TestOnly-Strong-None!")]
    [InlineData("TestOnlyStrong123")]
    [InlineData("TestOnly Strong123!")]
    [InlineData("TestOnly\tStrong123!")]
    [InlineData("TestOnly\u0085Strong123!")]
    public async Task WeakPasswordsDoNotCreateUsersOrAudits(string password)
    {
        await using var db = CreateContext("Data Source=:memory:");
        await db.Database.OpenConnectionAsync(Ct);
        await db.Database.EnsureCreatedAsync(Ct);
        var service = new InitialAdministratorService(db, Hasher, true);
        var error = await Assert.ThrowsAsync<InitialAdministratorException>(() =>
            service.CreateAsync(Request with { Password = password }, Ct));
        Assert.Equal(InitialAdministratorError.Validation, error.Error);
        Assert.Empty(await db.Users.ToListAsync(Ct));
        Assert.Empty(await db.AuditLogs.ToListAsync(Ct));
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(32, true)]
    [InlineData(33, false)]
    [InlineData(256, false)]
    [InlineData(257, false)]
    public async Task PasswordLengthLimitsAreExact(int length, bool expected)
    {
        var request = Request with
        {
            Name = "Prueba", Email = "test@localhost.test", Password = "Aa1!" + new string('x', length - 4)
        };
        Assert.Contains(request.Password, char.IsDigit);
        Assert.Equal(expected, AdministratorCredentials.IsValid(request.Name, request.Email, request.Password));
        Assert.Equal(expected, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
        await using var db = CreateContext("Data Source=:memory:");
        await db.Database.OpenConnectionAsync(Ct);
        await db.Database.EnsureCreatedAsync(Ct);
        var service = new InitialAdministratorService(db, Hasher, true);
        if (expected)
        {
            await service.CreateAsync(request, Ct);
            var user = await db.Users.SingleAsync(Ct);
            Assert.Equal(PasswordVerificationResult.Success, Hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password));
        }
        else
        {
            var error = await Assert.ThrowsAsync<InitialAdministratorException>(() => service.CreateAsync(request, Ct));
            Assert.Equal(InitialAdministratorError.Validation, error.Error);
            Assert.Empty(await db.Users.ToListAsync(Ct));
            Assert.Empty(await db.AuditLogs.ToListAsync(Ct));
        }
    }

    [Fact]
    public async Task InvalidNameAndEmailCannotCreateAccounts()
    {
        await using var db = CreateContext("Data Source=:memory:");
        await db.Database.OpenConnectionAsync(Ct);
        await db.Database.EnsureCreatedAsync(Ct);
        var service = new InitialAdministratorService(db, Hasher, true);
        foreach (var request in new[]
        {
            Request with { Name = " " }, Request with { Name = new string('n', 161) },
            Request with { Email = "not-an-email" }, Request with { Email = "Nombre <test@localhost.test>" },
            Request with { Email = new string('x', 245) + "@localhost.test" }
        })
        {
            var error = await Assert.ThrowsAsync<InitialAdministratorException>(() => service.CreateAsync(request, Ct));
            Assert.Equal(InitialAdministratorError.Validation, error.Error);
        }
        Assert.Empty(await db.Users.ToListAsync(Ct));
    }

    [Fact]
    public async Task FailureToWriteAuditRollsBackNewAdministrator()
    {
        await using var db = CreateContext("Data Source=:memory:");
        await db.Database.OpenConnectionAsync(Ct);
        await db.Database.EnsureCreatedAsync(Ct);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER reject_admin_audit BEFORE INSERT ON AuditLogs BEGIN SELECT RAISE(ABORT, 'test rollback'); END;", Ct);
        var service = new InitialAdministratorService(db, Hasher, true);
        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(Request, Ct));
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Users.ToListAsync(Ct));
        Assert.True((await service.StatusAsync(Ct)).Available);
    }

    [Fact]
    public async Task ConcurrentFirstAccountRequestsCreateOnlyOneAdministrator()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mercado-cercano-initial-admin-{Guid.NewGuid():N}.db");
        var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 10 }.ToString();
        try
        {
            await using var db = CreateContext(connection);
            await db.Database.EnsureCreatedAsync(Ct);
            var start = new TaskCompletionSource();
            async Task<InitialAdministratorError?> CreateOne(string email)
            {
                await start.Task;
                await using var other = CreateContext(connection);
                try
                {
                    await new InitialAdministratorService(other, Hasher, true).CreateAsync(Request with { Email = email }, Ct);
                    return null;
                }
                catch (InitialAdministratorException error) { return error.Error; }
            }
            var requests = new[]
            {
                Task.Run(() => CreateOne("first@localhost.test")),
                Task.Run(() => CreateOne("second@localhost.test"))
            };
            start.SetResult();
            var results = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Single(results, result => result is null);
            Assert.Single(results, result => result == InitialAdministratorError.Conflict);
            Assert.Equal(1, await db.Users.CountAsync(Ct));
            Assert.Equal(1, await db.AuditLogs.CountAsync(Ct));
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }

    [Fact]
    public async Task EnvironmentBootstrapStillWorksAndDoesNotResetExistingPassword()
    {
        await using var db = CreateContext("Data Source=:memory:");
        await db.Database.OpenConnectionAsync(Ct);
        await db.Database.EnsureCreatedAsync(Ct);
        IConfiguration Configuration(string password) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BOOTSTRAP_ADMIN_NAME"] = Request.Name,
            ["BOOTSTRAP_ADMIN_EMAIL"] = Request.Email,
            ["BOOTSTRAP_ADMIN_PASSWORD"] = password
        }).Build();
        await DevelopmentAdministratorSeeder.SeedAsync(db, Hasher, Configuration(Request.Password), Ct);
        var hash = (await db.Users.SingleAsync(Ct)).PasswordHash;
        await DevelopmentAdministratorSeeder.SeedAsync(db, Hasher, Configuration("Different-TestOnly-456!"), Ct);
        Assert.Equal(hash, (await db.Users.SingleAsync(Ct)).PasswordHash);
        Assert.Single(await db.AuditLogs.ToListAsync(Ct));
        Assert.False((await new InitialAdministratorService(db, Hasher, true).StatusAsync(Ct)).Available);
    }

    [Fact]
    public async Task ControllerCreatesLocationForProtectedProfileWithoutStartingAHost()
    {
        await using var db = CreateContext("Data Source=:memory:");
        await db.Database.OpenConnectionAsync(Ct);
        await db.Database.EnsureCreatedAsync(Ct);
        var controller = new InitialAdministratorController(new InitialAdministratorService(db, Hasher, true));
        Assert.NotNull(typeof(InitialAdministratorController).GetCustomAttribute<AllowAnonymousAttribute>());
        var method = typeof(InitialAdministratorController).GetMethod(nameof(InitialAdministratorController.Create))!;
        Assert.Equal(RateLimitPolicies.Login, method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        var result = Assert.IsType<CreatedAtActionResult>((await controller.Create(Request, Ct)).Result);
        Assert.Equal(nameof(AuthController.Me), result.ActionName);
        Assert.Equal("Auth", result.ControllerName);
        Assert.IsType<InitialAdministratorResponse>(result.Value);
    }

    private static StoreDbContext CreateContext(string connection) =>
        new(new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options);
}
