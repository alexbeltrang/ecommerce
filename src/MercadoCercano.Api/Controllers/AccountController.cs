using System.Security.Claims;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Application.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MercadoCercano.Api.Controllers;

[ApiController, Authorize, Route("api/v1/account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(IUserAdministrationService service) : ControllerBase
{
    /// <summary>Cambia la contraseña propia, temporal o definitiva, y cierra todas las sesiones.</summary>
    [HttpPost("password"), EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangeOwnPasswordRequest request, CancellationToken ct)
    {
        await service.ChangeOwnPasswordAsync(request, Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
