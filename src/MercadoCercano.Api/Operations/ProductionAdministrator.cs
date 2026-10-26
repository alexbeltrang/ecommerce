using MercadoCercano.Api.Authorization;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Api.Operations;

public static class ProductionAdministrator
{
    public static async Task CreateAsync(StoreDbContext db, string name, string email, string password, CancellationToken ct)
    {
        if (!AdministratorCredentials.IsValid(name, email, password))
            throw new InvalidOperationException("Credenciales invalidas: nombre/correo validos y clave de 8 a 32 con mayuscula, minuscula, numero y especial, sin espacios.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.Users.AnyAsync(ct))
            throw new InvalidOperationException("El aprovisionamiento esta cerrado porque ya existe un usuario. No se modificaron credenciales.");
        var user = new User
        {
            Name = name.Trim(), Email = email.Trim(), NormalizedEmail = email.Trim().ToUpperInvariant(),
            PasswordHash = "", Role = UserRole.Administrator
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        db.Users.Add(user);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id, Action = "Authentication.ProductionAdministratorCreated", EntityType = nameof(User), EntityId = user.Id.ToString()
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
