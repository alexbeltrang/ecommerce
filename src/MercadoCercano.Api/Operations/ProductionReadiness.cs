using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Api.Operations;

public static class ProductionReadiness
{
    public static async Task RequireReadyAsync(StoreDbContext db, CancellationToken ct)
    {
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
            throw new InvalidOperationException("Hay migraciones pendientes. Aplicalas durante mantenimiento antes de iniciar IIS.");
        if (!await db.Users.AsNoTracking().AnyAsync(user => user.IsActive && user.Role == UserRole.Administrator && !user.MustChangePassword, ct))
            throw new InvalidOperationException("Se requiere un administrador activo con contraseña definitiva. Aprovisiona la base durante mantenimiento.");
    }
}
