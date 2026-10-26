using System.Security.Claims;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Controllers;

[ApiController, Authorize(Policy = RolePolicies.Administrator)]
[Route("api/v1/admin/checkout-settings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminCheckoutSettingsController(ICheckoutSettingsService settings) : ControllerBase
{
    /// <summary>Obtiene configuracion y version para administracion de entregas.</summary>
    [HttpGet]
    public async Task<ActionResult<CheckoutSettingsResponse>> Get(CancellationToken ct) =>
        Ok(await settings.GetAsync(ct));

    /// <summary>Configura tarifas COP, cobertura, instrucciones y condiciones reales o de prueba.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(CheckoutSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CheckoutSettingsResponse>> Update(CheckoutSettingsRequest request, CancellationToken ct) =>
        Ok(await settings.SaveAsync(request, Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct));
}
