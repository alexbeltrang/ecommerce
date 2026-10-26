using MercadoCercano.Application.Catalog;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Middleware;

public sealed class CatalogExceptionHandler(ILogger<CatalogExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not CatalogException error) return false;
        var status = error.Error switch
        {
            CatalogError.Validation => StatusCodes.Status400BadRequest,
            CatalogError.NotFound => StatusCodes.Status404NotFound,
            CatalogError.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        logger.LogWarning("Catalog request rejected: {Status} {Reason}", status, error.Message);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status, Title = "No fue posible completar la operacion",
            Detail = error.Message, Instance = context.Request.Path
        }, cancellationToken);
        return true;
    }
}
