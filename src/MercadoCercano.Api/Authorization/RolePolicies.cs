using MercadoCercano.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace MercadoCercano.Api.Authorization;

public static class RolePolicies
{
    public const string Administrator = "Administrator";
    public const string OrderOperations = "OrderOperations";
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Administrator, policy => policy.RequireAuthenticatedUser()
            .RequireRole(nameof(UserRole.Administrator)).RequireClaim(UserSessionClaims.PasswordChangeRequired, "false"));
        options.AddPolicy(OrderOperations, policy => policy.RequireAuthenticatedUser()
            .RequireRole(nameof(UserRole.Administrator), nameof(UserRole.OrderOperator))
            .RequireClaim(UserSessionClaims.PasswordChangeRequired, "false"));
    }
}
