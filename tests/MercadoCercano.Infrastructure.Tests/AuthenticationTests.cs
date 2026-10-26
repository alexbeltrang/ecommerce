using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MercadoCercano.Infrastructure.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task LoginRequiresCsrfAndIssuesSecureHttpOnlyCookieWithRole()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var anonymousResponse = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        await factory.SeedUserAsync(UserRole.Administrator);

        var missingCsrfResponse = await client.PostAsJsonAsync("/api/v1/auth/login", ValidLoginPayload);
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrfResponse.StatusCode);

        var csrfToken = await GetCsrfTokenAsync(client, verifyCookieAttributes: true);
        using var loginResponse = await PostLoginAsync(client, csrfToken, ValidLoginPayload);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains(
            loginResponse.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("MercadoCercano.Auth=", StringComparison.Ordinal) &&
                      cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
                      cookie.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
                      cookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));

        var currentUser = await client.GetFromJsonAsync<CurrentUserPayload>("/api/v1/auth/me");
        Assert.NotNull(currentUser);
        Assert.Equal("admin@example.test", currentUser.Email);
        Assert.Equal(nameof(UserRole.Administrator), currentUser.Role);

        var authorizationService = factory.Services.GetRequiredService<IAuthorizationService>();
        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, currentUser.Id.ToString()),
            new Claim(ClaimTypes.Role, currentUser.Role),
            new Claim(UserSessionClaims.PasswordChangeRequired, "false")
        ], "test"));
        var operatorPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, currentUser.Id.ToString()),
            new Claim(ClaimTypes.Role, nameof(UserRole.OrderOperator)),
            new Claim(UserSessionClaims.PasswordChangeRequired, "false")
        ], "test"));

        Assert.True((await authorizationService.AuthorizeAsync(adminPrincipal, RolePolicies.Administrator)).Succeeded);
        Assert.True((await authorizationService.AuthorizeAsync(adminPrincipal, RolePolicies.OrderOperations)).Succeeded);
        Assert.False((await authorizationService.AuthorizeAsync(operatorPrincipal, RolePolicies.Administrator)).Succeeded);
        Assert.True((await authorizationService.AuthorizeAsync(operatorPrincipal, RolePolicies.OrderOperations)).Succeeded);

        using var logoutRequest = CreateCsrfRequest(await GetCsrfTokenAsync(client));
        using var logoutResponse = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task LoginDoesNotRevealUnknownUserAndRateLimitsRepeatedAttempts()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        await factory.SeedUserAsync(UserRole.OrderOperator);
        var csrfToken = await GetCsrfTokenAsync(client);

        using var wrongPasswordResponse = await PostLoginAsync(
            client,
            csrfToken,
            new LoginPayload("admin@example.test", "incorrecta"));
        using var unknownUserResponse = await PostLoginAsync(
            client,
            csrfToken,
            new LoginPayload("missing@example.test", "incorrecta"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUserResponse.StatusCode);
        Assert.Equal(
            await wrongPasswordResponse.Content.ReadAsStringAsync(),
            await unknownUserResponse.Content.ReadAsStringAsync());

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var response = await PostLoginAsync(
                client,
                csrfToken,
                new LoginPayload("missing@example.test", "incorrecta"));
            if (attempt == 3)
            {
                Assert.Equal((HttpStatusCode)429, response.StatusCode);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task InactiveUserIsSignedOutWhenExistingCookieIsValidated()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var userId = await factory.SeedUserAsync(UserRole.OrderOperator);
        var csrfToken = await GetCsrfTokenAsync(client);
        using var loginResponse = await PostLoginAsync(client, csrfToken, ValidLoginPayload);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        await factory.DeactivateUserAsync(userId);

        using var meResponse = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
        Assert.Contains(
            meResponse.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("MercadoCercano.Auth=", StringComparison.Ordinal) &&
                      cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DevelopmentBootstrapCreatesAdminOnceAndRequiresAllCredentials()
    {
        await using var factory = new AuthenticationApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var validConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_NAME"] = "Admin de prueba",
                ["BOOTSTRAP_ADMIN_EMAIL"] = "Admin@Example.Test",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "Bootstrap-Password-5482!"
            })
            .Build();

        await DevelopmentAdministratorSeeder.SeedAsync(dbContext, passwordHasher, validConfiguration);
        var admin = await dbContext.Users.SingleAsync();
        var originalHash = admin.PasswordHash;
        await DevelopmentAdministratorSeeder.SeedAsync(
            dbContext,
            passwordHasher,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BOOTSTRAP_ADMIN_NAME"] = "Admin de prueba",
                    ["BOOTSTRAP_ADMIN_EMAIL"] = "Admin@Example.Test",
                    ["BOOTSTRAP_ADMIN_PASSWORD"] = "Changed-Password-5482!"
                })
                .Build());

        Assert.Equal("ADMIN@EXAMPLE.TEST", admin.NormalizedEmail);
        Assert.Equal(UserRole.Administrator, admin.Role);
        Assert.Equal(originalHash, admin.PasswordHash);
        Assert.Equal(1, await dbContext.AuditLogs.CountAsync());
        Assert.Equal(
            PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, "Bootstrap-Password-5482!"));

        var partialConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "admin@example.test"
            })
            .Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentAdministratorSeeder.SeedAsync(dbContext, passwordHasher, partialConfiguration));
    }

    private static readonly LoginPayload ValidLoginPayload =
        new("admin@example.test", "Test-Password-5482!");

    private static async Task<string> GetCsrfTokenAsync(
        HttpClient client,
        bool verifyCookieAttributes = false)
    {
        using var responseMessage = await client.GetAsync("/api/v1/auth/csrf");
        Assert.True(responseMessage.Headers.CacheControl?.NoStore);
        responseMessage.EnsureSuccessStatusCode();
        if (verifyCookieAttributes)
        {
            Assert.Contains(
                responseMessage.Headers.GetValues("Set-Cookie"),
                cookie => cookie.Contains("MercadoCercano.Antiforgery=", StringComparison.Ordinal) &&
                          cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
                          cookie.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
                          cookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        }

        var response = await responseMessage.Content.ReadFromJsonAsync<CsrfTokenPayload>();
        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response.RequestToken));
        return response.RequestToken;
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string csrfToken,
        LoginPayload payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CreateCsrfRequest(string csrfToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return request;
    }

    private sealed record LoginPayload(string Email, string Password);
    private sealed record CsrfTokenPayload(string RequestToken);
    private sealed record CurrentUserPayload(Guid Id, string Name, string Email, string Role);

    private sealed class AuthenticationApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _testConnection = new("Data Source=:memory:");

        public AuthenticationApiFactory()
        {
            ClientOptions.BaseAddress = new Uri("https://localhost");
            _testConnection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BOOTSTRAP_ADMIN_NAME"] = string.Empty,
                    ["BOOTSTRAP_ADMIN_EMAIL"] = string.Empty,
                    ["BOOTSTRAP_ADMIN_PASSWORD"] = string.Empty
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<StoreDbContext>();
                services.RemoveAll<DbContextOptions<StoreDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<IDbContextOptionsConfiguration<StoreDbContext>>();
                services.AddSingleton(_testConnection);
                services.AddDbContext<StoreDbContext>((serviceProvider, options) =>
                    options.UseSqlite(
                        serviceProvider.GetRequiredService<SqliteConnection>(),
                        sqlite => sqlite.MigrationsAssembly(
                            typeof(StoreDbContext).Assembly.GetName().Name)));
                services.RemoveAll<IPasswordHasher<User>>();
                services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
            });
        }

        public async Task<Guid> SeedUserAsync(UserRole role)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

            var user = new User
            {
                Name = "Administrador de prueba",
                Email = "admin@example.test",
                NormalizedEmail = "ADMIN@EXAMPLE.TEST",
                PasswordHash = string.Empty,
                Role = role
            };
            user.PasswordHash = passwordHasher.HashPassword(user, ValidLoginPayload.Password);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            return user.Id;
        }

        public async Task DeactivateUserAsync(Guid userId)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            var user = await dbContext.Users.SingleAsync(candidate => candidate.Id == userId);
            user.IsActive = false;
            await dbContext.SaveChangesAsync();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _testConnection.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _testConnection.DisposeAsync();
        }
    }

}
