using System.Security.Claims;
using MercadoCercano.Domain.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MercadoCercano.Api.Authorization;

public static class UserSessionClaims
{
    public const string SecurityStamp = "mc:session-version";
    public const string PasswordChangeRequired = "mc:password-change-required";
    public static ClaimsPrincipal Principal(User user) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Name),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim(SecurityStamp, user.SecurityStamp.ToString("N")),
        new Claim(PasswordChangeRequired, user.MustChangePassword ? "true" : "false")
    ], CookieAuthenticationDefaults.AuthenticationScheme));
}
