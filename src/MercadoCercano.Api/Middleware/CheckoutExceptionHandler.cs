using MercadoCercano.Application.Orders;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Middleware;

public sealed class CheckoutExceptionHandler(ILogger<CheckoutExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not CheckoutException error) return false;
        var status = error.Error switch
        {
            CheckoutError.Validation => StatusCodes.Status400BadRequest,
            CheckoutError.NotFound => StatusCodes.Status404NotFound,
            CheckoutError.Conflict => StatusCodes.Status409Conflict,
            CheckoutError.Unavailable => StatusCodes.Status503ServiceUnavailable,
            CheckoutError.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };
        logger.LogWarning("Checkout request rejected: {Status} {Reason}", status, error.Message);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status, Title = "No fue posible completar la operacion",
            Detail = error.Message, Instance = context.Request.Path
        }, ct);
        return true;
    }
}
