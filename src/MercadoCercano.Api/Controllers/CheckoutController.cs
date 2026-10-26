using MercadoCercano.Api.Authorization;
using MercadoCercano.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MercadoCercano.Api.Controllers;

[ApiController, AllowAnonymous]
[Route("api/v1/checkout")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CheckoutController(IOrderService orders, ICheckoutSettingsService settings) : ControllerBase
{
    /// <summary>Entrega tarifas, cobertura, medios de pago y condiciones publicadas.</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<CheckoutSettingsResponse>> Settings(CancellationToken ct) =>
        Ok(await settings.GetAsync(ct));

    /// <summary>Recalcula importes e inventario disponible sin reservar unidades.</summary>
    [HttpPost("quote"), EnableRateLimiting(RateLimitPolicies.CheckoutQuote)]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteResponse>> Quote(QuoteRequest request, CancellationToken ct) =>
        Ok(await orders.QuoteAsync(request, ct));

    /// <summary>Crea o recupera idempotentemente un pedido pendiente, sin descontar stock ni cobrar.</summary>
    [HttpPost("orders"), EnableRateLimiting(RateLimitPolicies.OrderCreation)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var result = await orders.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { orderNumber = result.OrderNumber }, result);
    }

    /// <summary>Recurso de pedido protegido por X-Order-Token; el token nunca forma parte de la URL.</summary>
    [HttpGet("orders/{orderNumber}"), EnableRateLimiting(RateLimitPolicies.OrderTracking)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Get(
        string orderNumber, [FromHeader(Name = "X-Order-Token")] string? token, CancellationToken ct) =>
        Ok(await orders.TrackAsync(new TrackOrderRequest { OrderNumber = orderNumber, TrackingToken = token ?? "" }, ct));

    /// <summary>Consulta un pedido mediante numero y token secreto en el cuerpo, sin revelar datos personales.</summary>
    [HttpPost("track"), EnableRateLimiting(RateLimitPolicies.OrderTracking)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Track(TrackOrderRequest request, CancellationToken ct) =>
        Ok(await orders.TrackAsync(request, ct));

    /// <summary>Recupera una creacion cuyo resultado se perdio usando intento y token.</summary>
    [HttpPost("recover"), EnableRateLimiting(RateLimitPolicies.OrderTracking)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Recover(RecoverOrderRequest request, CancellationToken ct) =>
        Ok(await orders.RecoverAsync(request, ct));
}
