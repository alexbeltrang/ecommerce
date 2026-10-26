using System.Security.Claims;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Controllers;

[ApiController, Authorize(Policy = RolePolicies.OrderOperations)]
[Route("api/v1/admin/orders")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminOrdersController(IOrderService orders) : ControllerBase
{
    /// <summary>Lista pedidos para administradores y operadores autorizados.</summary>
    [HttpGet]
    public async Task<ActionResult<PageResponse<AdminOrderResponse>>> List([FromQuery] OrderQuery query, CancellationToken ct) =>
        Ok(await orders.ListAsync(query, ct));

    /// <summary>Muestra contacto, entrega y version de un pedido privado.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminOrderResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await orders.GetAsync(id, ct));

    /// <summary>Confirma y descuenta inventario de forma atomica, sin marcar el pago como realizado.</summary>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(AdminOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminOrderResponse>> Confirm(Guid id, OrderActionRequest request, CancellationToken ct) =>
        Ok(await orders.ConfirmAsync(id, request, UserId, ct));

    /// <summary>Cancela un pedido pendiente o confirmado; repone stock solo si antes fue confirmado.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(AdminOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminOrderResponse>> Cancel(Guid id, OrderActionRequest request, CancellationToken ct) =>
        Ok(await orders.CancelAsync(id, request, UserId, ct));

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
