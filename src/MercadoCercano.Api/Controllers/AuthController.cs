using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Security.Claims;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(
    StoreDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IAntiforgery antiforgery) : ControllerBase
{
    private const string InvalidCredentialsMessage = "Correo o contraseña incorrectos.";
    private static readonly User DummyUser = new()
    {
        Name = "Dummy",
        Email = "dummy@example.invalid",
        NormalizedEmail = "DUMMY@EXAMPLE.INVALID",
        PasswordHash = string.Empty
    };
    private static readonly string DummyPasswordHash = new PasswordHasher<User>()
        .HashPassword(DummyUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    [AllowAnonymous]
    [HttpGet("csrf")]
    [ProducesResponseType(typeof(CsrfTokenResponse), StatusCodes.Status200OK)]
    public IActionResult GetCsrfToken()
    {
        Response.Headers.CacheControl = "no-store";
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse(tokens.RequestToken!));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [HttpPost("login")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);

        var verificationResult = user is null
            ? passwordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash, request.Password)
            : passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (user is null || !user.IsActive ||
            verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "No fue posible iniciar sesión",
                Detail = InvalidCredentialsMessage
            });
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "Authentication.Login",
            EntityType = nameof(User),
            EntityId = user.Id.ToString()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            UserSessionClaims.Principal(user),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true
            });

        return Ok(ToResponse(user));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId && candidate.IsActive, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(ToResponse(user));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userId, out var parsedUserId))
        {
            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = parsedUserId,
                Action = "Authentication.Logout",
                EntityType = nameof(User),
                EntityId = parsedUserId.ToString()
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private static CurrentUserResponse ToResponse(User user) =>
        new(user.Id, user.Name, user.Email, user.Role.ToString(), user.MustChangePassword);

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}

public sealed record LoginRequest(
    [param: Required]
    [param: EmailAddress]
    [param: StringLength(254)]
    string Email,
    [param: Required]
    [param: StringLength(256, MinimumLength = 1)]
    string Password);

public sealed record CsrfTokenResponse(string RequestToken);

public sealed record CurrentUserResponse(Guid Id, string Name, string Email, string Role, bool MustChangePassword = false);
