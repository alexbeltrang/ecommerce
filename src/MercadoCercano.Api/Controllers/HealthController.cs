using MercadoCercano.Api.Operations;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/v1/health")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HealthController(StoreDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    /// <summary>Indica que el proceso responde, sin revelar configuracion.</summary>
    [HttpGet]
    public IActionResult Live() => Ok(new HealthResponse("ok", "MercadoCercano.Api"));

    /// <summary>Disponibilidad de SQLite, esquema actualizado y administrador operativo.</summary>
    [HttpGet("ready")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        try
        {
            if (!System.IO.File.Exists(db.Database.GetDbConnection().DataSource))
                throw new InvalidOperationException("SQLite no esta disponible.");
            await ProductionReadiness.RequireReadyAsync(db, ct);
            return Ok(new HealthResponse("ready", "MercadoCercano.Api"));
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.LogError(error, "La comprobacion de disponibilidad fallo.");
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Servicio no disponible",
                detail: "El servicio requiere revision operativa.");
        }
    }
}

/// <summary>Estado operativo publico, sin rutas, versiones ni datos privados.</summary>
public sealed record HealthResponse(string Status, string Service);
