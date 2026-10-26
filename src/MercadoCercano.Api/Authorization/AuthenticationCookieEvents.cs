using System.Security.Claims;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Api.Authorization;

public sealed class AuthenticationCookieEvents(StoreDbContext dbContext) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await RejectPrincipalAsync(context);
            return;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId && candidate.IsActive)
            .Select(candidate => new { candidate.Role, candidate.SecurityStamp, candidate.MustChangePassword })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (user is null ||
            context.Principal?.FindFirstValue(ClaimTypes.Role) != user.Role.ToString() ||
            context.Principal?.FindFirstValue(UserSessionClaims.SecurityStamp) != user.SecurityStamp.ToString("N") ||
            context.Principal?.FindFirstValue(UserSessionClaims.PasswordChangeRequired) != (user.MustChangePassword ? "true" : "false"))
        {
            await RejectPrincipalAsync(context);
        }
    }

    private static async Task RejectPrincipalAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
