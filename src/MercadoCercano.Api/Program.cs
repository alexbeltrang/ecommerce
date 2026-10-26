using MercadoCercano.Api.Authorization;
using MercadoCercano.Api.Middleware;
using MercadoCercano.Api.Operations;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Orders;
using MercadoCercano.Application.Promotions;
using MercadoCercano.Application.Reports;
using MercadoCercano.Application.Users;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Catalog;
using MercadoCercano.Infrastructure.Orders;
using MercadoCercano.Infrastructure.Promotions;
using MercadoCercano.Infrastructure.Reports;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;

if (args.Length > 0 && args[0] == "--maintenance")
{
    Environment.ExitCode = await MaintenanceCommands.RunAsync(args[1..], AppContext.BaseDirectory);
    return;
}

var builder = WebApplication.CreateBuilder(args);
var storage = builder.Environment.IsDevelopment() ? null : ProductionStorage.Load(builder.Configuration, builder.Environment.ContentRootPath);
storage?.ValidateServer(builder.Configuration, builder.Environment.ContentRootPath);
using var productionLease = storage?.AcquireLease();
if (storage is not null)
{
    builder.Configuration["Database:Path"] = storage.DatabasePath;
    var protection = builder.Services.AddDataProtection().SetApplicationName("MercadoCercano")
        .PersistKeysToFileSystem(new DirectoryInfo(storage.Keys));
    if (OperatingSystem.IsWindows())
    {
        protection.ProtectKeysWithDpapi(protectToLocalMachine: true);
        builder.Logging.AddEventLog(new Microsoft.Extensions.Logging.EventLog.EventLogSettings { SourceName = "MercadoCercano" });
    }
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
}

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AntiforgeryExceptionHandler>();
builder.Services.AddExceptionHandler<CatalogExceptionHandler>();
builder.Services.AddExceptionHandler<CheckoutExceptionHandler>();
builder.Services.AddExceptionHandler<InitialAdministratorExceptionHandler>();
builder.Services.AddExceptionHandler<UserAccountExceptionHandler>();
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddOpenApi();
builder.Services.AddStorePersistence(builder.Configuration, builder.Environment.ContentRootPath, allowCreateDatabase: storage is null);
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IUserAdministrationService, UserAdministrationService>();
builder.Services.AddScoped<ICheckoutSettingsService, CheckoutSettingsService>();
builder.Services.AddScoped<OrderService>(services => new OrderService(
    services.GetRequiredService<StoreDbContext>(),
    services.GetRequiredService<ICheckoutSettingsService>(),
    builder.Environment.IsDevelopment(),
    services.GetRequiredService<IPrivatePaymentEvidenceStore>()));
builder.Services.AddScoped<IOrderService>(services => services.GetRequiredService<OrderService>());
builder.Services.AddScoped<IOrderOperationsService>(services => services.GetRequiredService<OrderService>());
builder.Services.AddSingleton<IPrivatePaymentEvidenceStore>(new FilePaymentEvidenceStore(
    storage?.PaymentEvidence ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "uploads", "payment-evidence"))));
builder.Services.AddSingleton<IProductImageStore>(new FileProductImageStore(
    storage?.Products ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "uploads", "products"))));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IInitialAdministratorService>(services => new InitialAdministratorService(
    services.GetRequiredService<StoreDbContext>(),
    services.GetRequiredService<IPasswordHasher<User>>(),
    builder.Environment.IsDevelopment()));
builder.Services.AddScoped<AuthenticationCookieEvents>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "MercadoCercano.Antiforgery"
        : "__Host-MercadoCercano.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Environment.IsDevelopment()
            ? "MercadoCercano.Auth"
            : "__Host-MercadoCercano.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.EventsType = typeof(AuthenticationCookieEvents);
    });
builder.Services.AddAuthorization(RolePolicies.Configure);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    foreach (var (policy, limit) in new[]
    {
        (RateLimitPolicies.CheckoutQuote, 30),
        (RateLimitPolicies.OrderCreation, 5),
        (RateLimitPolicies.PaymentEvidence, 5),
        (RateLimitPolicies.OrderTracking, 15)
    })
    {
        options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            }));
    }
    options.AddPolicy(RateLimitPolicies.Login, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Demasiados intentos",
                Detail = "Espera un momento antes de volver a intentarlo."
            },
            cancellationToken);
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
    await dbContext.Database.MigrateAsync();
    await DevelopmentDataSeeder.SeedAsync(dbContext);
    await CheckoutSettingsService.SeedDevelopmentAsync(dbContext);
    await DevelopmentAdministratorSeeder.SeedAsync(
        dbContext,
        scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
        builder.Configuration);
}
else
{
    await using var scope = app.Services.CreateAsyncScope();
    await ProductionReadiness.RequireReadyAsync(scope.ServiceProvider.GetRequiredService<StoreDbContext>(), CancellationToken.None);
}

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.Use(async (context, next) =>
    {
        ProductionWeb.SetHeaders(context.Response);
        await next(context);
    });
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl =
            context.File.Name == "index.html" ? "no-store" : "public,max-age=3600"
    });
    app.MapFallback(async context =>
    {
        if (ProductionWeb.IsApiPath(context.Request.Path))
        {
            await Results.Problem(statusCode: 404, title: "Recurso no encontrado").ExecuteAsync(context);
            return;
        }
        context.Response.Headers.CacheControl = "no-store";
        await Results.File(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html"), "text/html")
            .ExecuteAsync(context);
    }).WithMetadata(new Microsoft.AspNetCore.Routing.HttpMethodMetadata(["GET", "HEAD"]));
}
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUi(options =>
    {
        options.Path = "/swagger";
        options.DocumentPath = "/openapi/v1.json";
        options.DocumentTitle = "Mercado Cercano API";
    });
}

app.Run();

public partial class Program { }
