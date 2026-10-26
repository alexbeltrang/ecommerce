using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MercadoCercano.Api.Authorization;

public static class DevelopmentAdministratorSeeder
{
    public static async Task SeedAsync(
        StoreDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var name = configuration["BOOTSTRAP_ADMIN_NAME"];
        var email = configuration["BOOTSTRAP_ADMIN_EMAIL"];
        var password = configuration["BOOTSTRAP_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(name) &&
            string.IsNullOrWhiteSpace(email) &&
            string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Para crear el administrador inicial de desarrollo configura BOOTSTRAP_ADMIN_NAME, BOOTSTRAP_ADMIN_EMAIL y BOOTSTRAP_ADMIN_PASSWORD.");
        }

        if (!AdministratorCredentials.IsValid(name, email, password))
        {
            throw new InvalidOperationException(
                "Las credenciales iniciales de desarrollo no cumplen los requisitos de nombre, correo o contraseña.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var existingUser = await dbContext.Users
            .SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            if (existingUser.Role != UserRole.Administrator)
            {
                throw new InvalidOperationException(
                    "El correo configurado para el administrador inicial ya pertenece a una cuenta con otro rol.");
            }

            return;
        }

        var user = new User
        {
            Name = name.Trim(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = string.Empty,
            Role = UserRole.Administrator
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        dbContext.Users.Add(user);
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "Authentication.DevelopmentAdministratorCreated",
            EntityType = nameof(User),
            EntityId = user.Id.ToString()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
