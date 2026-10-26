using System.Security.Claims;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MercadoCercano.Api.Controllers;

[ApiController, Authorize(Policy = RolePolicies.Administrator), Route("api/v1/admin/users")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminUsersController(IUserAdministrationService service) : ControllerBase
{
    /// <summary>Lista cuentas internas, incluidas desactivadas, sin secretos.</summary>
    [HttpGet]
    public async Task<ActionResult<PageResponse<UserAccountResponse>>> List([FromQuery] UserQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ActorId, ct));
    /// <summary>Obtiene una cuenta y su version de edicion.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserAccountResponse>> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ActorId, ct));
    /// <summary>Crea una cuenta con clave temporal y cambio obligatorio.</summary>
    [HttpPost, EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(typeof(UserAccountResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<UserAccountResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ActorId, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    /// <summary>Edita o activa/desactiva; revoca sesiones y preserva historial.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserAccountResponse>> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, request, ActorId, ct);
        if (id == ActorId) await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(result);
    }
    /// <summary>Restablece otra cuenta con contraseña temporal y revoca sus sesiones.</summary>
    [HttpPost("{id:guid}/reset-password"), EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<UserAccountResponse>> Reset(Guid id, ResetUserPasswordRequest request, CancellationToken ct) =>
        Ok(await service.ResetPasswordAsync(id, request, ActorId, ct));
    private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
