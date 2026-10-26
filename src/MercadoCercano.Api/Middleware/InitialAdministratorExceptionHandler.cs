using MercadoCercano.Api.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Middleware;

public sealed class InitialAdministratorExceptionHandler(ILogger<InitialAdministratorExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not InitialAdministratorException error) return false;
        var status = error.Error switch
        {
            InitialAdministratorError.NotFound => StatusCodes.Status404NotFound,
            InitialAdministratorError.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        logger.LogWarning("Initial administrator request rejected: {Status} {Reason}", status, error.Message);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status, Title = "No fue posible crear el administrador",
            Detail = error.Message, Instance = context.Request.Path
        }, ct);
        return true;
    }
}
