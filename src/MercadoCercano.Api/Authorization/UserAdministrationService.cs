using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Users;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Api.Authorization;

public sealed class UserAdministrationService(StoreDbContext db, IPasswordHasher<User> hasher) : IUserAdministrationService
{
    public async Task<PageResponse<UserAccountResponse>> ListAsync(UserQuery query, Guid actorId, CancellationToken ct)
    {
        Validate(query);
        await Administrator(actorId, ct);
        var source = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + query.Search.Trim().Replace("/", "//").Replace("%", "/%").Replace("_", "/_") + "%";
            source = source.Where(item => EF.Functions.Like(item.Name, pattern, "/") || EF.Functions.Like(item.Email, pattern, "/"));
        }
        if (query.Role is not null)
        {
            var role = Role(query.Role);
            source = source.Where(item => item.Role == role);
        }
        if (query.IsActive.HasValue) source = source.Where(item => item.IsActive == query.IsActive);
        var count = await source.CountAsync(ct);
        var offset = (long)(query.Page - 1) * query.PageSize;
        var rows = offset > int.MaxValue ? [] : await source.OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToArray(), count, query.Page, query.PageSize);
    }

    public async Task<UserAccountResponse> GetAsync(Guid id, Guid actorId, CancellationToken ct)
    {
        await Administrator(actorId, ct);
        return Map(await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct) ?? throw Missing());
    }

    public async Task<UserAccountResponse> CreateAsync(CreateUserRequest request, Guid actorId, CancellationToken ct)
    {
        Validate(request); Profile(request.Name, request.Email); Password(request.TemporaryPassword);
        var role = Role(request.Role);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Administrator(actorId, ct);
        var email = request.Email.Trim();
        await UniqueEmail(email, null, ct);
        var item = new User
        {
            Name = request.Name.Trim(), Email = email, NormalizedEmail = email.ToUpperInvariant(), PasswordHash = "",
            Role = role, IsActive = request.IsActive, MustChangePassword = true
        };
        item.PasswordHash = hasher.HashPassword(item, request.TemporaryPassword);
        db.Users.Add(item);
        Audit(actorId, "User.Create", item);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(item);
    }

    public async Task<UserAccountResponse> UpdateAsync(Guid id, UpdateUserRequest request, Guid actorId, CancellationToken ct)
    {
        Validate(request); Profile(request.Name, request.Email);
        var role = Role(request.Role);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Administrator(actorId, ct);
        var item = await Mutable(id, request.UpdatedAtUtc, ct);
        if (id == actorId && (!request.IsActive || role != UserRole.Administrator))
            throw Conflict("No puedes desactivar tu propia cuenta ni cambiar tu propio rol.");
        if (item.IsActive && item.Role == UserRole.Administrator && (!request.IsActive || role != UserRole.Administrator) &&
            !await db.Users.AnyAsync(other => other.Id != id && other.IsActive && other.Role == UserRole.Administrator, ct))
            throw Conflict("Debe conservarse al menos un administrador activo.");
        var email = request.Email.Trim();
        await UniqueEmail(email, id, ct);
        item.Name = request.Name.Trim(); item.Email = email; item.NormalizedEmail = email.ToUpperInvariant();
        item.Role = role; item.IsActive = request.IsActive; item.SecurityStamp = Guid.NewGuid();
        Audit(actorId, "User.Update", item);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(item);
    }

    public async Task<UserAccountResponse> ResetPasswordAsync(Guid id, ResetUserPasswordRequest request, Guid actorId, CancellationToken ct)
    {
        Validate(request); Password(request.TemporaryPassword);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Administrator(actorId, ct);
        if (id == actorId) throw Conflict("Para tu propia cuenta usa Cambiar mi contraseña y confirma la actual.");
        var item = await Mutable(id, request.UpdatedAtUtc, ct);
        if (hasher.VerifyHashedPassword(item, item.PasswordHash, request.TemporaryPassword) != PasswordVerificationResult.Failed)
            throw Invalid("La nueva contraseña temporal debe ser diferente de la anterior.");
        item.PasswordHash = hasher.HashPassword(item, request.TemporaryPassword);
        item.MustChangePassword = true; item.SecurityStamp = Guid.NewGuid();
        Audit(actorId, "User.PasswordReset", item);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(item);
    }

    public async Task ChangeOwnPasswordAsync(ChangeOwnPasswordRequest request, Guid actorId, CancellationToken ct)
    {
        Validate(request); Password(request.NewPassword);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.Users.SingleOrDefaultAsync(item => item.Id == actorId && item.IsActive, ct)
            ?? throw new UserAccountException(UserAccountError.Forbidden, "La cuenta ya no esta disponible.");
        if (hasher.VerifyHashedPassword(item, item.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            throw Invalid("La contraseña actual no es correcta.");
        if (hasher.VerifyHashedPassword(item, item.PasswordHash, request.NewPassword) != PasswordVerificationResult.Failed)
            throw Invalid("La nueva contraseña debe ser diferente de la actual.");
        item.PasswordHash = hasher.HashPassword(item, request.NewPassword);
        item.MustChangePassword = false; item.SecurityStamp = Guid.NewGuid();
        Audit(actorId, "User.PasswordChanged", item);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task Administrator(Guid actorId, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(item => item.Id == actorId && item.IsActive &&
            item.Role == UserRole.Administrator && !item.MustChangePassword, ct))
            throw new UserAccountException(UserAccountError.Forbidden, "Se requiere una cuenta de administrador activa con contraseña definitiva.");
    }
    private async Task<User> Mutable(Guid id, DateTimeOffset? version, CancellationToken ct)
    {
        var item = await db.Users.SingleOrDefaultAsync(item => item.Id == id, ct) ?? throw Missing();
        if (version?.UtcDateTime != item.UpdatedAtUtc) throw Conflict("La cuenta cambio. Recarga antes de volver a operar.");
        return item;
    }
    private async Task UniqueEmail(string email, Guid? except, CancellationToken ct)
    {
        var normalized = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(item => item.Id != except && item.NormalizedEmail == normalized, ct))
            throw Conflict("Ya existe una cuenta con ese correo, incluso si esta desactivada.");
    }
    private void Audit(Guid actorId, string action, User item) => db.AuditLogs.Add(new AuditLog
    {
        UserId = actorId, Action = action, EntityType = nameof(User), EntityId = item.Id.ToString(),
        DetailsJson = JsonSerializer.Serialize(new { Role = item.Role.ToString(), item.IsActive, item.MustChangePassword })
    });
    private static void Profile(string name, string email)
    {
        if (!AdministratorCredentials.IsValidProfile(name, email)) throw Invalid("Indica nombre y correo validos.");
    }
    private static void Password(string value)
    {
        if (!AdministratorCredentials.IsValidPassword(value))
            throw Invalid("La contraseña requiere de 8 a 32 caracteres, mayusculas, minusculas, numeros y un caracter especial, sin espacios.");
    }
    private static UserRole Role(string value) => value switch
    {
        nameof(UserRole.Administrator) => UserRole.Administrator,
        nameof(UserRole.OrderOperator) => UserRole.OrderOperator,
        _ => throw Invalid("Selecciona Administrador u Operador de pedidos.")
    };
    private static void Validate(object request)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new(request), results, true))
            throw Invalid(string.Join(" ", results.Select(item => item.ErrorMessage)));
    }
    private static UserAccountResponse Map(User item) => new(item.Id, item.Name, item.Email, item.Role.ToString(), item.IsActive,
        item.MustChangePassword, new(DateTime.SpecifyKind(item.CreatedAtUtc, DateTimeKind.Utc)),
        new(DateTime.SpecifyKind(item.UpdatedAtUtc, DateTimeKind.Utc)));
    private static UserAccountException Invalid(string message) => new(UserAccountError.Validation, message);
    private static UserAccountException Conflict(string message) => new(UserAccountError.Conflict, message);
    private static UserAccountException Missing() => new(UserAccountError.NotFound, "La cuenta no existe.");
}
