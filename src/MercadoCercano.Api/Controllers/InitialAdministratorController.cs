using MercadoCercano.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MercadoCercano.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/v1/auth/initial-administrator")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InitialAdministratorController(IInitialAdministratorService service) : ControllerBase
{
    /// <summary>Indica si Development permite crear el primer usuario, sin revelar cuentas existentes.</summary>
    [HttpGet]
    public async Task<ActionResult<InitialAdministratorStatusResponse>> Status(CancellationToken ct) =>
        Ok(await service.StatusAsync(ct));

    /// <summary>Crea el unico administrador inicial de desarrollo; requiere CSRF y una base sin usuarios.</summary>
    [HttpPost, EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(typeof(InitialAdministratorResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<InitialAdministratorResponse>> Create(CreateInitialAdministratorRequest request, CancellationToken ct) =>
        CreatedAtAction(nameof(AuthController.Me), "Auth", null, await service.CreateAsync(request, ct));
}
