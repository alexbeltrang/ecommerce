using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Api.Authorization;

public sealed class InitialAdministratorService(
    StoreDbContext db, IPasswordHasher<User> passwordHasher, bool isDevelopment) : IInitialAdministratorService
{
    public async Task<InitialAdministratorStatusResponse> StatusAsync(CancellationToken ct) =>
        new(isDevelopment && !await db.Users.AnyAsync(ct));

    public async Task<InitialAdministratorResponse> CreateAsync(CreateInitialAdministratorRequest request, CancellationToken ct)
    {
        if (!isDevelopment)
            throw new InitialAdministratorException(InitialAdministratorError.NotFound, "El alta inicial no esta disponible.");
        if (!AdministratorCredentials.IsValid(request.Name, request.Email, request.Password))
            throw new InitialAdministratorException(InitialAdministratorError.Validation,
                "Indica nombre y correo validos. La contraseña requiere entre 8 y 32 caracteres, mayusculas, minusculas, numeros y un caracter especial, sin espacios.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.Users.AnyAsync(ct))
            throw new InitialAdministratorException(InitialAdministratorError.Conflict,
                "El alta inicial esta cerrada porque ya existe un usuario. Inicia sesion con una cuenta existente.");

        var user = new User
        {
            Name = request.Name.Trim(), Email = request.Email.Trim(),
            NormalizedEmail = request.Email.Trim().ToUpperInvariant(),
            PasswordHash = string.Empty, Role = UserRole.Administrator
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id, Action = "Authentication.DevelopmentAdministratorCreated",
            EntityType = nameof(User), EntityId = user.Id.ToString()
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(user.Id);
    }
}
