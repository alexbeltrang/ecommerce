using MercadoCercano.Application.Users;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Middleware;

public sealed class UserAccountExceptionHandler(ILogger<UserAccountExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not UserAccountException error) return false;
        var status = error.Error switch
        {
            UserAccountError.Validation => StatusCodes.Status400BadRequest,
            UserAccountError.NotFound => StatusCodes.Status404NotFound,
            UserAccountError.Conflict => StatusCodes.Status409Conflict,
            UserAccountError.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };
        logger.LogWarning("User operation rejected: {Status} {Reason}", status, error.Message);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        { Status = status, Title = "No fue posible completar la operacion de cuenta", Detail = error.Message, Instance = context.Request.Path }, ct);
        return true;
    }
}
