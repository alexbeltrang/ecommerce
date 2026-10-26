using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Authorization;

public sealed class AntiforgeryExceptionHandler(
    ILogger<AntiforgeryExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not AntiforgeryValidationException)
        {
            return false;
        }

        logger.LogWarning("Se rechazó una solicitud por token antifalsificación ausente o no válido.");
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Solicitud no válida",
                Detail = "La solicitud requiere un token de seguridad válido."
            },
            cancellationToken);
        return true;
    }
}
