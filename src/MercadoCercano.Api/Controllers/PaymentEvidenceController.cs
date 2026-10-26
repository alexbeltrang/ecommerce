using System.ComponentModel.DataAnnotations;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MercadoCercano.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/v1/checkout/payment/submit-evidence")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PaymentEvidenceController(IOrderOperationsService operations) : ControllerBase
{
    /// <summary>El titular del codigo privado adjunta un comprobante; no acredita el pago ni expone el archivo.</summary>
    [HttpPost, Consumes("multipart/form-data"), EnableRateLimiting(RateLimitPolicies.PaymentEvidence)]
    [RequestSizeLimit(1_048_576), RequestFormLimits(MultipartBodyLengthLimit = 1_048_576)]
    public async Task<ActionResult<OrderResponse>> Submit(
        [FromForm, Required, StringLength(40)] string orderNumber,
        [FromForm, Required, RegularExpression("^[A-Za-z0-9_-]{43}$")] string trackingToken,
        [FromForm] PaymentEvidenceRequest request, [FromForm] IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await operations.SubmitGuestEvidenceAsync(new() { OrderNumber = orderNumber, TrackingToken = trackingToken },
            request, stream, file.Length, file.ContentType, ct));
    }
}
