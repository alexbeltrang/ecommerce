using System.ComponentModel.DataAnnotations;

namespace MercadoCercano.Api.Authorization;

/// <summary>Disponibilidad del alta unica de desarrollo, sin revelar datos de usuarios.</summary>
public sealed record InitialAdministratorStatusResponse(bool Available);

/// <summary>Credenciales elegidas localmente para el primer administrador.</summary>
public sealed record CreateInitialAdministratorRequest
{
    [Required, StringLength(160)] public required string Name { get; init; }
    [Required, EmailAddress, StringLength(254)] public required string Email { get; init; }
    [Required, StringLength(32, MinimumLength = 8)] public required string Password { get; init; }
}

/// <summary>Identificador del administrador creado; no inicia sesion ni devuelve credenciales.</summary>
public sealed record InitialAdministratorResponse(Guid Id);

public interface IInitialAdministratorService
{
    Task<InitialAdministratorStatusResponse> StatusAsync(CancellationToken ct);
    Task<InitialAdministratorResponse> CreateAsync(CreateInitialAdministratorRequest request, CancellationToken ct);
}

public enum InitialAdministratorError { NotFound, Conflict, Validation }

public sealed class InitialAdministratorException(InitialAdministratorError error, string message) : Exception(message)
{
    public InitialAdministratorError Error { get; } = error;
}
