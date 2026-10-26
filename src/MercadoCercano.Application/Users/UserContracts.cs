using System.ComponentModel.DataAnnotations;
using MercadoCercano.Application.Catalog;

namespace MercadoCercano.Application.Users;

/// <summary>Listado paginado de cuentas internas, nunca hashes ni sellos de sesion.</summary>
public sealed record UserQuery
{
    [StringLength(100)] public string? Search { get; init; }
    [RegularExpression("^(Administrator|OrderOperator)$")] public string? Role { get; init; }
    public bool? IsActive { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 50)] public int PageSize { get; init; } = 12;
}

/// <summary>Cuenta administrativa versionada; las claves nunca se devuelven.</summary>
public sealed record UserAccountResponse(Guid Id, string Name, string Email, string Role, bool IsActive,
    bool MustChangePassword, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

/// <summary>Alta interna con contraseña temporal y cambio obligatorio.</summary>
public sealed record CreateUserRequest
{
    [Required, StringLength(160)] public required string Name { get; init; }
    [Required, EmailAddress, StringLength(254)] public required string Email { get; init; }
    [Required, RegularExpression("^(Administrator|OrderOperator)$")] public required string Role { get; init; }
    public bool IsActive { get; init; } = true;
    [Required, StringLength(32, MinimumLength = 8)] public required string TemporaryPassword { get; init; }
}

/// <summary>Edicion completa sin modificar la contraseña; revoca sesiones del destinatario.</summary>
public sealed record UpdateUserRequest
{
    [Required, StringLength(160)] public required string Name { get; init; }
    [Required, EmailAddress, StringLength(254)] public required string Email { get; init; }
    [Required, RegularExpression("^(Administrator|OrderOperator)$")] public required string Role { get; init; }
    public bool IsActive { get; init; }
    [Required] public DateTimeOffset? UpdatedAtUtc { get; init; }
}

/// <summary>Restablece otra cuenta con clave temporal sin devolverla ni enviarla por correo.</summary>
public sealed record ResetUserPasswordRequest
{
    [Required, StringLength(32, MinimumLength = 8)] public required string TemporaryPassword { get; init; }
    [Required] public DateTimeOffset? UpdatedAtUtc { get; init; }
}

/// <summary>Cambio personal con verificacion de la contraseña actual y cierre de todas las sesiones.</summary>
public sealed record ChangeOwnPasswordRequest
{
    [Required, StringLength(256, MinimumLength = 1)] public required string CurrentPassword { get; init; }
    [Required, StringLength(32, MinimumLength = 8)] public required string NewPassword { get; init; }
}

public interface IUserAdministrationService
{
    Task<PageResponse<UserAccountResponse>> ListAsync(UserQuery query, Guid actorId, CancellationToken ct);
    Task<UserAccountResponse> GetAsync(Guid id, Guid actorId, CancellationToken ct);
    Task<UserAccountResponse> CreateAsync(CreateUserRequest request, Guid actorId, CancellationToken ct);
    Task<UserAccountResponse> UpdateAsync(Guid id, UpdateUserRequest request, Guid actorId, CancellationToken ct);
    Task<UserAccountResponse> ResetPasswordAsync(Guid id, ResetUserPasswordRequest request, Guid actorId, CancellationToken ct);
    Task ChangeOwnPasswordAsync(ChangeOwnPasswordRequest request, Guid actorId, CancellationToken ct);
}

public enum UserAccountError { Validation, NotFound, Conflict, Forbidden }

public sealed class UserAccountException(UserAccountError error, string message) : Exception(message)
{
    public UserAccountError Error { get; } = error;
}
