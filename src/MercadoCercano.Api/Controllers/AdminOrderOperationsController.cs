using System.Security.Claims;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Controllers;

[ApiController, Authorize(Policy = RolePolicies.OrderOperations)]
[Route("api/v1/admin/orders/{id:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminOrderOperationsController(IOrderOperationsService operations) : ControllerBase
{
    /// <summary>Prepara, alista, despacha o entrega respetando estados y pago.</summary>
    [HttpPost("fulfillment/{actionName:regex(^(prepare|ready|ship|deliver)$)}")]
    [ProducesResponseType(typeof(AdminOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminOrderResponse>> Fulfill(Guid id, string actionName, FulfillmentRequest request, CancellationToken ct) =>
        Ok(await operations.FulfillAsync(id, actionName, request, UserId, ct));

    /// <summary>Registra fondos realmente recibidos; no realiza cobros externos.</summary>
    [HttpPost("payment/approve")]
    [ProducesResponseType(typeof(AdminOrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminOrderResponse>> Approve(Guid id, PaymentReviewRequest request, CancellationToken ct) =>
        Ok(await operations.ReviewPaymentAsync(id, true, request, UserId, ct));

    /// <summary>Rechaza un comprobante con motivo visible para el cliente.</summary>
    [HttpPost("payment/reject")]
    [ProducesResponseType(typeof(AdminOrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminOrderResponse>> Reject(Guid id, PaymentReviewRequest request, CancellationToken ct) =>
        Ok(await operations.ReviewPaymentAsync(id, false, request, UserId, ct));

    /// <summary>Adjunta un comprobante privado PNG/JPEG/WebP de hasta 750 KiB y pone el pago en revision.</summary>
    [HttpPost("payment/submit-evidence"), Consumes("multipart/form-data")]
    [RequestSizeLimit(1_048_576), RequestFormLimits(MultipartBodyLengthLimit = 1_048_576)]
    [ProducesResponseType(typeof(AdminOrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminOrderResponse>> Evidence(Guid id, [FromForm] PaymentEvidenceRequest request, [FromForm] IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await operations.SubmitEvidenceAsync(id, request, stream, file.Length, file.ContentType, UserId, ct));
    }

    /// <summary>Descarga privada de un comprobante asociado exactamente a este pedido.</summary>
    [HttpGet("payment/evidence/{evidenceId:guid}")]
    public async Task<IActionResult> Download(Guid id, Guid evidenceId, CancellationToken ct)
    {
        var file = await operations.OpenEvidenceAsync(id, evidenceId, ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
        var extension = file.ContentType switch { "image/png" => ".png", "image/jpeg" => ".jpg", _ => ".webp" };
        return File(file.Stream, file.ContentType, $"comprobante-{evidenceId:N}{extension}");
    }

    /// <summary>Solicita una devolucion parcial idempotente o el retorno completo de un despacho fallido.</summary>
    [HttpPost("returns")]
    [ProducesResponseType(typeof(AdminOrderResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminOrderResponse>> CreateReturn(Guid id, CreateReturnRequest request, CancellationToken ct) =>
        CreatedAtAction(nameof(ReturnByRequest), new { id, requestId = request.RequestId },
            await operations.CreateReturnAsync(id, request, UserId, ct));

    /// <summary>Recupera una solicitud por su intento, siempre bajo autorizacion administrativa.</summary>
    [HttpGet("returns/by-request/{requestId:guid}")]
    public async Task<ActionResult<AdminReturnResponse>> ReturnByRequest(Guid id, Guid requestId, CancellationToken ct)
    {
        return Ok(await operations.ReturnByRequestAsync(id, requestId, ct));
    }

    /// <summary>Registra recepcion fisica y repone solo unidades en buen estado, una unica vez.</summary>
    [HttpPost("returns/{returnId:guid}/receive")]
    public async Task<ActionResult<AdminOrderResponse>> Receive(Guid id, Guid returnId, ReceiveReturnRequest request, CancellationToken ct) =>
        Ok(await operations.ReceiveReturnAsync(id, returnId, request, UserId, ct));

    /// <summary>Rechaza una solicitud aun no recibida, sin movimientos de inventario.</summary>
    [HttpPost("returns/{returnId:guid}/reject")]
    public async Task<ActionResult<AdminOrderResponse>> RejectReturn(Guid id, Guid returnId, OrderActionRequest request, CancellationToken ct) =>
        Ok(await operations.RejectReturnAsync(id, returnId, request, UserId, ct));

    /// <summary>Solo administrador: registra un reembolso previamente efectuado fuera del sistema.</summary>
    [HttpPost("returns/{returnId:guid}/refund"), Authorize(Policy = RolePolicies.Administrator)]
    public async Task<ActionResult<AdminOrderResponse>> Refund(Guid id, Guid returnId, RefundReturnRequest request, CancellationToken ct) =>
        Ok(await operations.RefundReturnAsync(id, returnId, request, UserId, ct));

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
