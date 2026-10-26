using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MercadoCercano.Application.Orders;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Orders;

public sealed class CheckoutSettingsService(StoreDbContext db) : ICheckoutSettingsService
{
    public const string SettingKey = "Checkout.Configuration.v1";

    public async Task<CheckoutSettingsResponse> GetAsync(CancellationToken ct)
    {
        var setting = await db.StoreSettings.AsNoTracking().SingleOrDefaultAsync(item => item.Key == SettingKey, ct)
            ?? throw new CheckoutException(CheckoutError.Unavailable, "El negocio debe configurar las entregas antes de recibir pedidos.");
        CheckoutSettings? values;
        try { values = JsonSerializer.Deserialize<CheckoutSettings>(setting.Value); }
        catch (JsonException)
        { throw new CheckoutException(CheckoutError.Unavailable, "La configuracion de checkout almacenada no es valida. Contacta al administrador."); }
        if (values is null) throw new CheckoutException(CheckoutError.Unavailable, "Falta la configuracion de checkout.");
        try { Validate(values); }
        catch (CheckoutException error) when (error.Error == CheckoutError.Validation)
        { throw new CheckoutException(CheckoutError.Unavailable, "La configuracion de checkout almacenada requiere correccion por el administrador."); }
        return new(values, Utc(setting.UpdatedAtUtc));
    }

    public async Task<CheckoutSettingsResponse> SaveAsync(CheckoutSettingsRequest request, Guid userId, CancellationToken ct)
    {
        Validate(request.Values);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var setting = await db.StoreSettings.SingleOrDefaultAsync(item => item.Key == SettingKey, ct);
        if (setting is not null && (!request.UpdatedAtUtc.HasValue ||
            request.UpdatedAtUtc.Value.UtcDateTime != setting.UpdatedAtUtc))
            throw new CheckoutException(CheckoutError.Conflict, "La configuracion cambio. Recarga antes de guardar.");
        if (setting is null)
        {
            setting = new StoreSetting { Key = SettingKey, Value = "", IsPublic = true };
            db.StoreSettings.Add(setting);
        }
        setting.Value = JsonSerializer.Serialize(request.Values);
        setting.IsPublic = true;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "CheckoutSettings.Update", EntityType = nameof(StoreSetting), EntityId = setting.Id.ToString()
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(request.Values, Utc(setting.UpdatedAtUtc));
    }

    public static async Task SeedDevelopmentAsync(StoreDbContext db, CancellationToken ct = default)
    {
        if (await db.StoreSettings.AnyAsync(setting => setting.Key == SettingKey, ct)) return;
        db.StoreSettings.Add(new StoreSetting
        {
            Key = SettingKey, IsPublic = true, Value = JsonSerializer.Serialize(TestDefaults())
        });
        await db.SaveChangesAsync(ct);
    }

    public static CheckoutSettings TestDefaults() => new()
    {
        IsTestConfiguration = true, PickupEnabled = true,
        PickupMunicipality = "Municipio de prueba",
        PickupInstructions = "PRUEBA: coordina la recogida con el negocio. Falta configurar la ubicacion real.",
        LocalDeliveryEnabled = true, LocalDeliveryFee = 5000, LocalMunicipalities = ["Municipio de prueba"],
        CarrierShippingEnabled = true, CarrierShippingFee = 15000, CarrierNationwide = true,
        PaymentMethods = ["BankTransfer", "DigitalWallet", "CashOnDelivery"],
        PaymentInstructions = "PRUEBA: no realices pagos. El negocio debe reemplazar estas instrucciones.",
        TermsText = "CONDICIONES DE PRUEBA: el pedido queda pendiente de confirmacion y no reserva inventario. Los datos de contacto se utilizaran para coordinar este pedido. El negocio debe publicar sus condiciones y politica de tratamiento de datos reales."
    };

    private static void Validate(CheckoutSettings values)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(values, new ValidationContext(values), results, true))
            throw new CheckoutException(CheckoutError.Validation, string.Join(" ", results.Select(result => result.ErrorMessage)));
        if (!values.PickupEnabled && !values.LocalDeliveryEnabled && !values.CarrierShippingEnabled)
            throw new CheckoutException(CheckoutError.Validation, "Habilita al menos una modalidad de entrega.");
        if (values.PickupEnabled && (string.IsNullOrWhiteSpace(values.PickupMunicipality) || string.IsNullOrWhiteSpace(values.PickupInstructions)))
            throw new CheckoutException(CheckoutError.Validation, "Indica el municipio y las instrucciones de recogida.");
        if (values.PaymentMethods.Any(method => !Enum.TryParse<PaymentMethod>(method, out var parsed) ||
            !Enum.IsDefined(parsed) || parsed.ToString() != method) || values.PaymentMethods.Distinct().Count() != values.PaymentMethods.Length)
            throw new CheckoutException(CheckoutError.Validation, "Selecciona medios de pago validos y sin duplicados.");
        if (values.LocalMunicipalities.Concat(values.CarrierMunicipalities)
            .Any(city => string.IsNullOrWhiteSpace(city) || city.Length > 120 || city != city.Trim()) ||
            values.LocalMunicipalities.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.LocalMunicipalities.Length ||
            values.CarrierMunicipalities.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.CarrierMunicipalities.Length)
            throw new CheckoutException(CheckoutError.Validation, "Usa municipios no vacios, unicos y de hasta 120 caracteres.");
        if (values.LocalDeliveryEnabled && values.LocalMunicipalities.Length == 0)
            throw new CheckoutException(CheckoutError.Validation, "Indica los municipios con domicilio local.");
        if (values.CarrierShippingEnabled && !values.CarrierNationwide && values.CarrierMunicipalities.Length == 0)
            throw new CheckoutException(CheckoutError.Validation, "Indica la cobertura de transportadora o habilita cobertura nacional.");
    }

    private static DateTimeOffset Utc(DateTime date) => new(DateTime.SpecifyKind(date, DateTimeKind.Utc));
}
