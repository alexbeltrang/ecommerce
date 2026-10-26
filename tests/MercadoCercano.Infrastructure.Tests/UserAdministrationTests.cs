using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Api.Controllers;
using MercadoCercano.Application.Users;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace MercadoCercano.Infrastructure.Tests;

public sealed partial class OrderServiceTests
{
    private const string AccountTestPassword = "Temporal1!prueba";
    private const string ChangedTestPassword = "Definitiva2!prueba";
    private static readonly PasswordHasher<User> AccountHasher = new();
    private static CreateUserRequest NewAccount(string? email = null, string role = "OrderOperator") => new()
    {
        Name = "Cuenta de prueba", Email = email ?? $"{Guid.NewGuid():N}@example.test", Role = role,
        TemporaryPassword = AccountTestPassword
    };
    private static async Task<T> UserCall<T>(TestOrders data, Func<UserAdministrationService, Task<T>> action)
    {
        await using var context = data.NewContext();
        return await action(new(context, AccountHasher));
    }
    private static UpdateUserRequest AccountEdit(UserAccountResponse user) => new()
    { Name = user.Name, Email = user.Email, Role = user.Role, IsActive = user.IsActive, UpdatedAtUtc = user.UpdatedAtUtc };
    private static async Task<User> StoredUser(TestOrders data, Guid id) => await data.Db.Users.AsNoTracking().SingleAsync(item => item.Id == id);
    private static async Task CompleteTemporary(TestOrders data, Guid id) => await UserCall(data, async service =>
    {
        await service.ChangeOwnPasswordAsync(new() { CurrentPassword = AccountTestPassword, NewPassword = ChangedTestPassword }, id, Ct);
        return true;
    });

    [Fact]
    public void UserAccountsControllersArePrivateAndPasswordOperationsAreRateLimited()
    {
        Assert.Equal(RolePolicies.Administrator, typeof(AdminUsersController).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.NotNull(typeof(AccountController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(typeof(AccountController).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        foreach (var type in new[] { typeof(AdminUsersController), typeof(AccountController) })
        {
            Assert.True(type.GetCustomAttribute<ResponseCacheAttribute>()?.NoStore);
            Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.All(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                method => Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>()));
        }
        foreach (var method in new[]
        {
            typeof(AdminUsersController).GetMethod(nameof(AdminUsersController.Create))!,
            typeof(AdminUsersController).GetMethod(nameof(AdminUsersController.Reset))!,
            typeof(AccountController).GetMethod(nameof(AccountController.ChangePassword))!
        }) Assert.Equal(RateLimitPolicies.Login, method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        Assert.DoesNotContain(typeof(AdminUsersController).GetMethods(), method => method.GetCustomAttribute<HttpDeleteAttribute>() != null);
    }

    [Fact]
    public async Task UserAccountsTemporarySessionsCannotAccessEitherBusinessPolicy()
    {
        using var services = new ServiceCollection().AddLogging().AddAuthorization(RolePolicies.Configure).BuildServiceProvider();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var user = new User { Name = "Prueba", Email = "test@example.test", NormalizedEmail = "TEST@EXAMPLE.TEST",
            PasswordHash = "", Role = UserRole.Administrator, MustChangePassword = true };
        foreach (var policy in new[] { RolePolicies.Administrator, RolePolicies.OrderOperations })
            Assert.False((await authorization.AuthorizeAsync(UserSessionClaims.Principal(user), policy)).Succeeded);
        user.MustChangePassword = false;
        Assert.True((await authorization.AuthorizeAsync(UserSessionClaims.Principal(user), RolePolicies.Administrator)).Succeeded);
        user.Role = UserRole.OrderOperator;
        Assert.False((await authorization.AuthorizeAsync(UserSessionClaims.Principal(user), RolePolicies.Administrator)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(UserSessionClaims.Principal(user), RolePolicies.OrderOperations)).Succeeded);
    }

    [Fact]
    public async Task UserAccountsCreateHashedTemporaryAccountsWithoutSecretResponsesOrAudit()
    {
        await using var data = await TestOrders.CreateAsync();
        var response = await UserCall(data, service => service.CreateAsync(NewAccount("  nuevo@example.test  "), data.UserId, Ct));
        Assert.Equal("nuevo@example.test", response.Email);
        Assert.True(response.MustChangePassword);
        var stored = await StoredUser(data, response.Id);
        Assert.Equal("NUEVO@EXAMPLE.TEST", stored.NormalizedEmail);
        Assert.Equal(PasswordVerificationResult.Success, AccountHasher.VerifyHashedPassword(stored, stored.PasswordHash, AccountTestPassword));
        Assert.NotEqual(Guid.Empty, stored.SecurityStamp);
        var audit = Assert.Single(await data.Db.AuditLogs.ToListAsync());
        Assert.Equal("User.Create", audit.Action);
        var json = JsonSerializer.Serialize(response) + audit.DetailsJson;
        Assert.DoesNotContain(AccountTestPassword, json);
        Assert.DoesNotContain(stored.PasswordHash, json);
        Assert.DoesNotContain(stored.SecurityStamp.ToString(), json);
        Assert.DoesNotContain("PasswordHash", json);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(32, true)]
    [InlineData(33, false)]
    public async Task UserAccountsPasswordBoundsAreExact(int length, bool valid)
    {
        await using var data = await TestOrders.CreateAsync();
        var request = NewAccount() with { TemporaryPassword = "Aa1!" + new string('x', length - 4) };
        if (valid) await UserCall(data, service => service.CreateAsync(request, data.UserId, Ct));
        else Assert.Equal(UserAccountError.Validation,
            (await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service => service.CreateAsync(request, data.UserId, Ct)))).Error);
    }

    [Theory]
    [InlineData("minuscula1!")]
    [InlineData("MAYUSCULA1!")]
    [InlineData("SinNumeros!")]
    [InlineData("SinEspecial1")]
    [InlineData("Con espacio1!")]
    public async Task UserAccountsPasswordClassesAreRequired(string password)
    {
        await using var data = await TestOrders.CreateAsync();
        await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service =>
            service.CreateAsync(NewAccount() with { TemporaryPassword = password }, data.UserId, Ct)));
        Assert.Single(await data.Db.Users.ToListAsync());
    }

    [Fact]
    public async Task UserAccountsRejectDuplicatesInvalidProfilesRolesAndStaleVersions()
    {
        await using var data = await TestOrders.CreateAsync();
        var created = await UserCall(data, service => service.CreateAsync(NewAccount("same@example.test"), data.UserId, Ct));
        var duplicate = await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service =>
            service.CreateAsync(NewAccount("SAME@EXAMPLE.TEST"), data.UserId, Ct)));
        Assert.Equal(UserAccountError.Conflict, duplicate.Error);
        foreach (var invalid in new[] { NewAccount() with { Name = " " }, NewAccount() with { Email = "Nombre <x@example.test>" },
            NewAccount() with { Role = "1" }, NewAccount() with { Role = "Other" } })
            await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service => service.CreateAsync(invalid, data.UserId, Ct)));
        var updated = await UserCall(data, service => service.UpdateAsync(created.Id, AccountEdit(created) with { IsActive = false }, data.UserId, Ct));
        Assert.False(updated.IsActive);
        Assert.Equal(UserAccountError.Conflict, (await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service =>
            service.UpdateAsync(created.Id, AccountEdit(created), data.UserId, Ct)))).Error);
        await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service =>
            service.CreateAsync(NewAccount("same@example.test"), data.UserId, Ct)));
        var result = await UserCall(data, service => service.ListAsync(new() { Search = "same", Role = "OrderOperator", IsActive = false }, data.UserId, Ct));
        Assert.Single(result.Items);
        var huge = await UserCall(data, service => service.ListAsync(new() { Page = int.MaxValue, PageSize = 50 }, data.UserId, Ct));
        Assert.Empty(huge.Items);
    }

    [Fact]
    public async Task UserAccountsOnlyActiveDefinitiveAdministratorsCanManageAccounts()
    {
        await using var data = await TestOrders.CreateAsync();
        foreach (var role in new[] { "OrderOperator", "Administrator" })
        {
            var created = await UserCall(data, service => service.CreateAsync(NewAccount(role: role), data.UserId, Ct));
            Assert.Equal(UserAccountError.Forbidden, (await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service =>
                service.ListAsync(new(), created.Id, Ct)))).Error);
            Assert.Equal(UserAccountError.Forbidden, (await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service =>
                service.CreateAsync(NewAccount(), created.Id, Ct)))).Error);
        }
        await data.Db.Users.Where(item => item.Id == data.UserId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service => service.GetAsync(data.UserId, data.UserId, Ct)));
    }

    [Fact]
    public async Task UserAccountsPreventSelfDeactivationDemotionAndSelfReset()
    {
        await using var data = await TestOrders.CreateAsync();
        var self = await UserCall(data, service => service.GetAsync(data.UserId, data.UserId, Ct));
        foreach (var invalid in new[] { AccountEdit(self) with { IsActive = false }, AccountEdit(self) with { Role = "OrderOperator" } })
            Assert.Equal(UserAccountError.Conflict, (await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service =>
                service.UpdateAsync(self.Id, invalid, data.UserId, Ct)))).Error);
        await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service => service.ResetPasswordAsync(self.Id,
            new() { TemporaryPassword = AccountTestPassword, UpdatedAtUtc = self.UpdatedAtUtc }, data.UserId, Ct)));
        Assert.True((await StoredUser(data, data.UserId)).IsActive);
        Assert.Empty(await data.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task UserAccountsOwnPasswordRequiresCurrentDiffersFromOldAndEndsTemporaryState()
    {
        await using var data = await TestOrders.CreateAsync();
        var user = await UserCall(data, service => service.CreateAsync(NewAccount(), data.UserId, Ct));
        var old = await StoredUser(data, user.Id);
        foreach (var invalid in new[]
        {
            new ChangeOwnPasswordRequest { CurrentPassword = "Wrong1!password", NewPassword = ChangedTestPassword },
            new ChangeOwnPasswordRequest { CurrentPassword = AccountTestPassword, NewPassword = AccountTestPassword }
        }) await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, async service =>
        { await service.ChangeOwnPasswordAsync(invalid, user.Id, Ct); return true; }));
        Assert.Equal(old.SecurityStamp, (await StoredUser(data, user.Id)).SecurityStamp);
        await CompleteTemporary(data, user.Id);
        var changed = await StoredUser(data, user.Id);
        Assert.False(changed.MustChangePassword);
        Assert.NotEqual(old.SecurityStamp, changed.SecurityStamp);
        Assert.Equal(PasswordVerificationResult.Success, AccountHasher.VerifyHashedPassword(changed, changed.PasswordHash, ChangedTestPassword));
        Assert.Equal(PasswordVerificationResult.Failed, AccountHasher.VerifyHashedPassword(changed, changed.PasswordHash, AccountTestPassword));
        Assert.Single(await data.Db.AuditLogs.Where(item => item.Action == "User.PasswordChanged").ToListAsync());
    }

    [Fact]
    public async Task UserAccountsResetRevokesSessionsRequiresChangeAndDoesNotReactivate()
    {
        await using var data = await TestOrders.CreateAsync();
        var created = await UserCall(data, service => service.CreateAsync(NewAccount(), data.UserId, Ct));
        await CompleteTemporary(data, created.Id);
        created = await UserCall(data, service => service.GetAsync(created.Id, data.UserId, Ct));
        created = await UserCall(data, service => service.UpdateAsync(created.Id, AccountEdit(created) with { IsActive = false }, data.UserId, Ct));
        var old = await StoredUser(data, created.Id);
        var reset = await UserCall(data, service => service.ResetPasswordAsync(created.Id,
            new() { UpdatedAtUtc = created.UpdatedAtUtc, TemporaryPassword = AccountTestPassword }, data.UserId, Ct));
        Assert.True(reset.MustChangePassword);
        Assert.False(reset.IsActive);
        Assert.NotEqual(old.SecurityStamp, (await StoredUser(data, created.Id)).SecurityStamp);
        await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service => service.ResetPasswordAsync(created.Id,
            new() { UpdatedAtUtc = created.UpdatedAtUtc, TemporaryPassword = ChangedTestPassword }, data.UserId, Ct)));
    }

    [Fact]
    public async Task UserAccountsEveryEditRevokesCookiesAndOldSessionsDoNotReturnAfterReactivation()
    {
        await using var data = await TestOrders.CreateAsync();
        var user = await UserCall(data, service => service.CreateAsync(NewAccount(), data.UserId, Ct));
        await CompleteTemporary(data, user.Id);
        var original = await StoredUser(data, user.Id);
        var principal = UserSessionClaims.Principal(original);
        Assert.True(await CookieAccepted(data, principal));
        user = await UserCall(data, service => service.GetAsync(user.Id, data.UserId, Ct));
        user = await UserCall(data, service => service.UpdateAsync(user.Id, AccountEdit(user) with { IsActive = false }, data.UserId, Ct));
        Assert.False(await CookieAccepted(data, principal));
        user = await UserCall(data, service => service.UpdateAsync(user.Id, AccountEdit(user) with { IsActive = true }, data.UserId, Ct));
        Assert.False(await CookieAccepted(data, principal));
        var fresh = UserSessionClaims.Principal(await StoredUser(data, user.Id));
        Assert.True(await CookieAccepted(data, fresh));
        await UserCall(data, service => service.UpdateAsync(user.Id, AccountEdit(user) with { Name = "Editado" }, data.UserId, Ct));
        Assert.False(await CookieAccepted(data, fresh));
        var legacy = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, "OrderOperator") }, CookieAuthenticationDefaults.AuthenticationScheme));
        Assert.False(await CookieAccepted(data, legacy));
    }

    [Fact]
    public async Task UserAccountsChangingAndResettingPasswordsInvalidateOriginalCookie()
    {
        await using var data = await TestOrders.CreateAsync();
        var user = await UserCall(data, service => service.CreateAsync(NewAccount(), data.UserId, Ct));
        var temporaryCookie = UserSessionClaims.Principal(await StoredUser(data, user.Id));
        Assert.True(await CookieAccepted(data, temporaryCookie));
        await CompleteTemporary(data, user.Id);
        Assert.False(await CookieAccepted(data, temporaryCookie));
        user = await UserCall(data, service => service.GetAsync(user.Id, data.UserId, Ct));
        var definitiveCookie = UserSessionClaims.Principal(await StoredUser(data, user.Id));
        await UserCall(data, service => service.ResetPasswordAsync(user.Id,
            new() { UpdatedAtUtc = user.UpdatedAtUtc, TemporaryPassword = AccountTestPassword }, data.UserId, Ct));
        Assert.False(await CookieAccepted(data, definitiveCookie));
        Assert.True(await CookieAccepted(data, UserSessionClaims.Principal(await StoredUser(data, user.Id))));
    }

    [Fact]
    public async Task UserAccountsFailedAuditRollsBackPasswordStampAndAccountCreation()
    {
        await using var data = await TestOrders.CreateAsync();
        var user = await UserCall(data, service => service.CreateAsync(NewAccount(), data.UserId, Ct));
        var before = await StoredUser(data, user.Id);
        await data.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectAccountAudit BEFORE INSERT ON AuditLogs BEGIN SELECT RAISE(ABORT, 'test'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => CompleteTemporary(data, user.Id));
        var after = await StoredUser(data, user.Id);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.True(after.MustChangePassword);
        await Assert.ThrowsAsync<DbUpdateException>(() => UserCall(data, service => service.CreateAsync(NewAccount(), data.UserId, Ct)));
        Assert.Equal(2, await data.Db.Users.CountAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => UserCall(data, service => service.UpdateAsync(user.Id,
            AccountEdit(user) with { Name = "No debe guardarse" }, data.UserId, Ct)));
        await Assert.ThrowsAsync<DbUpdateException>(() => UserCall(data, service => service.ResetPasswordAsync(user.Id,
            new() { UpdatedAtUtc = user.UpdatedAtUtc, TemporaryPassword = ChangedTestPassword }, data.UserId, Ct)));
        after = await StoredUser(data, user.Id);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
    }

    [Fact]
    public async Task UserAccountsSearchEscapesSqlWildcardsAndValidatesPagination()
    {
        await using var data = await TestOrders.CreateAsync();
        await UserCall(data, service => service.CreateAsync(NewAccount() with { Name = "Nombre 100%_real" }, data.UserId, Ct));
        Assert.Single((await UserCall(data, service => service.ListAsync(new() { Search = "%_" }, data.UserId, Ct))).Items);
        Assert.Empty((await UserCall(data, service => service.ListAsync(new() { Search = "%ausente" }, data.UserId, Ct))).Items);
        foreach (var invalid in new[] { new UserQuery { Page = 0 }, new UserQuery { PageSize = 51 },
            new UserQuery { Role = "1" }, new UserQuery { Search = new string('x', 101) } })
            Assert.Equal(UserAccountError.Validation, (await Assert.ThrowsAsync<UserAccountException>(() =>
                UserCall(data, service => service.ListAsync(invalid, data.UserId, Ct)))).Error);
        Assert.Equal(UserAccountError.NotFound, (await Assert.ThrowsAsync<UserAccountException>(() =>
            UserCall(data, service => service.GetAsync(Guid.NewGuid(), data.UserId, Ct)))).Error);
    }

    [Fact]
    public async Task UserAccountsDefinitiveOperatorsCannotReadEditResetOrCreateAccounts()
    {
        await using var data = await TestOrders.CreateAsync();
        var user = await UserCall(data, service => service.CreateAsync(NewAccount(), data.UserId, Ct));
        await CompleteTemporary(data, user.Id);
        user = await UserCall(data, service => service.GetAsync(user.Id, data.UserId, Ct));
        var attempts = new Func<UserAdministrationService, Task<UserAccountResponse>>[]
        {
            service => service.GetAsync(data.UserId, user.Id, Ct),
            service => service.CreateAsync(NewAccount(), user.Id, Ct),
            service => service.UpdateAsync(user.Id, AccountEdit(user), user.Id, Ct),
            service => service.ResetPasswordAsync(user.Id, new() { UpdatedAtUtc = user.UpdatedAtUtc, TemporaryPassword = AccountTestPassword }, user.Id, Ct)
        };
        foreach (var attempt in attempts)
            Assert.Equal(UserAccountError.Forbidden, (await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, attempt))).Error);
    }

    [Fact]
    public async Task UserAccountsLegacyLongCurrentPasswordsCanBeReplacedButNotReusedOnReset()
    {
        await using var data = await TestOrders.CreateAsync();
        var response = await UserCall(data, service => service.CreateAsync(NewAccount(), data.UserId, Ct));
        var user = await StoredUser(data, response.Id);
        var legacy = new string('x', 100);
        var hash = AccountHasher.HashPassword(user, legacy);
        await data.Db.Users.Where(item => item.Id == user.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.PasswordHash, hash));
        await UserCall(data, async service =>
        { await service.ChangeOwnPasswordAsync(new() { CurrentPassword = legacy, NewPassword = ChangedTestPassword }, user.Id, Ct); return true; });
        response = await UserCall(data, service => service.GetAsync(user.Id, data.UserId, Ct));
        Assert.False(response.MustChangePassword);
        Assert.Equal(UserAccountError.Validation, (await Assert.ThrowsAsync<UserAccountException>(() => UserCall(data, service =>
            service.ResetPasswordAsync(user.Id, new() { UpdatedAtUtc = response.UpdatedAtUtc, TemporaryPassword = ChangedTestPassword }, data.UserId, Ct)))).Error);
    }

    [Fact]
    public async Task UserAccountsRoleAndForcedChangeClaimsAreIndependentlyRevalidated()
    {
        await using var data = await TestOrders.CreateAsync();
        var user = await StoredUser(data, data.UserId);
        var principal = UserSessionClaims.Principal(user);
        await data.Db.Users.Where(item => item.Id == user.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Role, UserRole.OrderOperator));
        Assert.False(await CookieAccepted(data, principal));
        await data.Db.Users.Where(item => item.Id == user.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Role, UserRole.Administrator)
            .SetProperty(item => item.MustChangePassword, true));
        Assert.False(await CookieAccepted(data, principal));
        await data.Db.Users.Where(item => item.Id == user.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.MustChangePassword, false));
        var missingClaim = UserSessionClaims.Principal(user);
        var identity = Assert.IsType<ClaimsIdentity>(missingClaim.Identity);
        identity.RemoveClaim(identity.FindFirst(UserSessionClaims.PasswordChangeRequired)!);
        Assert.False(await CookieAccepted(data, missingClaim));
    }

    [Fact]
    public async Task UserAccountsPersonalPasswordControllerClosesTheCurrentSession()
    {
        await using var data = await TestOrders.CreateAsync();
        var user = await UserCall(data, service => service.CreateAsync(NewAccount(), data.UserId, Ct));
        var authentication = new UnitAuthentication();
        using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services, User = UserSessionClaims.Principal(await StoredUser(data, user.Id)) };
        await using var db = data.NewContext();
        var controller = new AccountController(new UserAdministrationService(db, AccountHasher))
        { ControllerContext = new ControllerContext { HttpContext = http } };
        Assert.IsType<NoContentResult>(await controller.ChangePassword(
            new() { CurrentPassword = AccountTestPassword, NewPassword = ChangedTestPassword }, Ct));
        Assert.Equal(1, authentication.SignOuts);
        Assert.False((await StoredUser(data, user.Id)).MustChangePassword);
    }

    [Fact]
    public async Task UserAccountsEditingMyProfileClosesTheCurrentSession()
    {
        await using var data = await TestOrders.CreateAsync();
        var user = await UserCall(data, service => service.GetAsync(data.UserId, data.UserId, Ct));
        var authentication = new UnitAuthentication();
        using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
        var principal = UserSessionClaims.Principal(await StoredUser(data, user.Id));
        var http = new DefaultHttpContext { RequestServices = services, User = principal };
        await using var db = data.NewContext();
        var controller = new AdminUsersController(new UserAdministrationService(db, AccountHasher))
        { ControllerContext = new ControllerContext { HttpContext = http } };
        Assert.IsType<OkObjectResult>((await controller.Update(user.Id, AccountEdit(user) with { Name = "Mi perfil editado" }, Ct)).Result);
        Assert.Equal(1, authentication.SignOuts);
        Assert.False(await CookieAccepted(data, principal));
    }

    [Fact]
    public async Task UserAccountsConcurrentAdministratorsCannotRemoveEachOtherAndLeaveNoAdmin()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mercado-users-{Guid.NewGuid():N}.db");
        try
        {
            await using var data = await TestOrders.CreateAsync(filePath: path);
            var second = await UserCall(data, service => service.CreateAsync(NewAccount(role: "Administrator"), data.UserId, Ct));
            await CompleteTemporary(data, second.Id);
            second = await UserCall(data, service => service.GetAsync(second.Id, data.UserId, Ct));
            var first = await UserCall(data, service => service.GetAsync(data.UserId, data.UserId, Ct));
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool> Disable(UserAccountResponse target, Guid actor) => Task.Run(async () =>
            {
                await start.Task;
                await using var connection = new SqliteConnection(data.Db.Database.GetConnectionString());
                await using var context = new MercadoCercano.Infrastructure.Persistence.StoreDbContext(
                    new DbContextOptionsBuilder<MercadoCercano.Infrastructure.Persistence.StoreDbContext>().UseSqlite(connection).Options);
                try { await new UserAdministrationService(context, AccountHasher).UpdateAsync(target.Id, AccountEdit(target) with { IsActive = false }, actor, Ct); return true; }
                catch (UserAccountException error) when (error.Error is UserAccountError.Forbidden or UserAccountError.Conflict) { return false; }
            });
            var attempts = new[] { Disable(second, first.Id), Disable(first, second.Id) };
            start.SetResult();
            var results = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Single(results, value => value);
            Assert.Equal(1, await data.Db.Users.CountAsync(item => item.IsActive && item.Role == UserRole.Administrator));
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }

    [Fact]
    public async Task UserAccountsMigrationPreservesExistingCredentialsAndCreatesDistinctStamps()
    {
        await using var data = await TestOrders.CreateAsync();
        await data.Db.GetService<IMigrator>().MigrateAsync("20261020141508_PromotionsAndCoupons");
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        foreach (var id in new[] { first, second })
            await data.Db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Users (Id, Name, Email, NormalizedEmail, PasswordHash, Role, IsActive, CreatedAtUtc)
                VALUES ({id}, {"Legado"}, {id.ToString() + "@example.test"}, {id.ToString().ToUpperInvariant() + "@EXAMPLE.TEST"}, {"legacy-hash"}, {2}, {true}, {DateTime.UtcNow})
                """);
        await data.Db.Database.MigrateAsync();
        var rows = await data.Db.Users.AsNoTracking().ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal(3, rows.Select(item => item.SecurityStamp).Distinct().Count());
        Assert.All(rows, item => { Assert.NotEqual(Guid.Empty, item.SecurityStamp); Assert.False(item.MustChangePassword); Assert.True(item.UpdatedAtUtc > DateTime.MinValue); });
        Assert.Equal("legacy-hash", rows.Single(item => item.Id == first).PasswordHash);
        Assert.Equal("not-a-real-password", rows.Single(item => item.Id == data.UserId).PasswordHash);
    }

    private static async Task<bool> CookieAccepted(TestOrders data, ClaimsPrincipal principal)
    {
        var authentication = new UnitAuthentication();
        using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), scheme.Name);
        var context = new CookieValidatePrincipalContext(http, scheme, new CookieAuthenticationOptions(), ticket);
        await using var db = data.NewContext();
        await new AuthenticationCookieEvents(db).ValidatePrincipal(context);
        Assert.Equal(context.Principal is null ? 1 : 0, authentication.SignOuts);
        return context.Principal is not null;
    }

    private sealed class UnitAuthentication : IAuthenticationService
    {
        public int SignOuts { get; private set; }
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => throw new NotSupportedException();
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) { SignOuts++; return Task.CompletedTask; }
    }
}
