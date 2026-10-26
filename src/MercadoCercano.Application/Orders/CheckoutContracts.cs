using System.ComponentModel.DataAnnotations;
using MercadoCercano.Application.Catalog;

namespace MercadoCercano.Application.Orders;

/// <summary>Reglas publicas de entrega, pago y condiciones del checkout.</summary>
public sealed record CheckoutSettings
{
    public bool IsTestConfiguration { get; init; } = true;
    public bool PickupEnabled { get; init; } = true;
    [Required(AllowEmptyStrings = true), StringLength(120)] public required string PickupMunicipality { get; init; }
    [Required(AllowEmptyStrings = true), StringLength(1000)] public required string PickupInstructions { get; init; }
    public bool LocalDeliveryEnabled { get; init; }
    [Range(0, 9_000_000_000_000)] public long LocalDeliveryFee { get; init; }
    [Required, MaxLength(100)] public string[] LocalMunicipalities { get; init; } = [];
    public bool CarrierShippingEnabled { get; init; }
    [Range(0, 9_000_000_000_000)] public long CarrierShippingFee { get; init; }
    public bool CarrierNationwide { get; init; }
    [Required, MaxLength(100)] public string[] CarrierMunicipalities { get; init; } = [];
    [Required, MinLength(1), MaxLength(3)] public string[] PaymentMethods { get; init; } = [];
    [Required, StringLength(1000)] public required string PaymentInstructions { get; init; }
    [Required, StringLength(5000)] public required string TermsText { get; init; }
}

/// <summary>Configuracion con version para impedir sobrescrituras administrativas.</summary>
public sealed record CheckoutSettingsResponse(CheckoutSettings Values, DateTimeOffset UpdatedAtUtc);

/// <summary>Actualizacion de tarifas y cobertura.</summary>
public sealed record CheckoutSettingsRequest
{
    [Required] public required CheckoutSettings Values { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
}

/// <summary>Producto y cantidad solicitados; no acepta precios del navegador.</summary>
public sealed record CartLineRequest
{
    public Guid ProductId { get; init; }
    [Range(1, int.MaxValue)] public int Quantity { get; init; }
}

/// <summary>Seleccion que la API debe cotizar y revalidar.</summary>
public sealed record QuoteRequest
{
    [StringLength(80)] public string? CouponCode { get; init; }
    [Required, MinLength(1), MaxLength(50)] public required CartLineRequest[] Items { get; init; }
    [Required, RegularExpression("^(CoordinatedPickup|LocalDelivery|CarrierShipping)$")]
    public required string DeliveryMethod { get; init; }
    [StringLength(120)] public string? Municipality { get; init; }
    [Required, RegularExpression("^(BankTransfer|DigitalWallet|CashOnDelivery)$")]
    public required string PaymentMethod { get; init; }
}

/// <summary>Linea cotizada con informacion y precios obtenidos de SQLite.</summary>
public sealed record QuoteLineResponse(
    Guid ProductId, string Name, string Sku, int Quantity, long RegularUnitPrice,
    long UnitPrice, long LineTotal, int AvailableStock, long PromotionDiscountTotal = 0);

/// <summary>Beneficio adicional aplicado, conservado como instantanea publica.</summary>
public sealed record AppliedPromotionResponse(string Name, string? Code, long DiscountTotal);

/// <summary>Totales COP e identificador del contenido cotizado, no una reserva de stock.</summary>
public sealed record QuoteResponse(
    IReadOnlyList<QuoteLineResponse> Items, long Subtotal, long DiscountTotal,
    long ShippingCost, long GrandTotal, string DeliveryMethod, string PaymentMethod,
    string Municipality, bool IsTestConfiguration, string TermsText,
    string PickupInstructions, string PaymentInstructions, string Fingerprint,
    AppliedPromotionResponse? Promotion = null, string? CouponNotice = null);

/// <summary>Pedido de invitado ligado a una cotizacion y a un intento idempotente.</summary>
public sealed record CreateOrderRequest
{
    [Required] public required QuoteRequest Selection { get; init; }
    [Required, RegularExpression("^[A-F0-9]{64}$")] public required string QuoteFingerprint { get; init; }
    public Guid IdempotencyKey { get; init; }
    [Required, RegularExpression("^[A-Za-z0-9_-]{43}$")] public required string TrackingToken { get; init; }
    [Required, StringLength(160)] public required string CustomerName { get; init; }
    [Required, StringLength(32)] public required string Phone { get; init; }
    [EmailAddress, StringLength(254)] public string? Email { get; init; }
    [StringLength(120)] public string? Neighborhood { get; init; }
    [StringLength(300)] public string? Address { get; init; }
    [StringLength(500)] public string? AddressReferences { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
    public bool TermsAccepted { get; init; }
}

/// <summary>Credencial de seguimiento enviada en el cuerpo, nunca en query string.</summary>
public sealed record TrackOrderRequest
{
    [Required, StringLength(40)] public required string OrderNumber { get; init; }
    [Required, RegularExpression("^[A-Za-z0-9_-]{43}$")] public required string TrackingToken { get; init; }
}

/// <summary>Recupera el resultado de un intento cuya respuesta pudo perderse.</summary>
public sealed record RecoverOrderRequest
{
    public Guid IdempotencyKey { get; init; }
    [Required, RegularExpression("^[A-Za-z0-9_-]{43}$")] public required string TrackingToken { get; init; }
}

/// <summary>Linea historica publica, sin costos privados.</summary>
public sealed record OrderLineResponse(string Name, string Sku, int Quantity, long UnitPrice, long LineTotal, long PromotionDiscountTotal = 0);

/// <summary>Historial publico sin identificadores internos de operadores.</summary>
public sealed record OrderHistoryResponse(string Status, string Reason, DateTimeOffset CreatedAtUtc);

/// <summary>Confirmacion y seguimiento seguro; no incluye datos personales del comprador.</summary>
public sealed record OrderResponse(
    string OrderNumber, string Status, string PaymentStatus, string DeliveryMethod,
    string PaymentMethod, long Subtotal, long DiscountTotal, long ShippingCost, long GrandTotal,
    bool IsTestOrder, DateTimeOffset CreatedAtUtc, IReadOnlyList<OrderLineResponse> Items,
    IReadOnlyList<OrderHistoryResponse> History, string? TermsText,
    string? PickupInstructions, string? PaymentInstructions,
    DateTimeOffset UpdatedAtUtc, FulfillmentResponse Fulfillment, long RefundedTotal,
    string? PaymentRejectionReason, IReadOnlyList<ReturnSummaryResponse> Returns, AppliedPromotionResponse? Promotion = null);

/// <summary>Pedido para operadores autorizados, con los datos de contacto y entrega.</summary>
public sealed record AdminOrderResponse(
    Guid Id, OrderResponse Order, string CustomerName, string Phone, string? Email,
    string Municipality, string? Neighborhood, string? Address, string? AddressReferences,
    string? Notes, string? CancellationReason, DateTimeOffset UpdatedAtUtc, OrderOperationsResponse Operations);

/// <summary>Filtros y pagina del listado de pedidos privado.</summary>
public sealed record OrderQuery
{
    [StringLength(100)] public string? Search { get; init; }
    [RegularExpression("^(PendingConfirmation|Confirmed|Preparing|ReadyForDelivery|Shipped|Delivered|Cancelled)$")]
    public string? Status { get; init; }
    [RegularExpression("^(Pending|UnderReview|Paid|Rejected|Refunded)$")]
    public string? PaymentStatus { get; init; }
    [RegularExpression("^(CoordinatedPickup|LocalDelivery|CarrierShipping)$")]
    public string? DeliveryMethod { get; init; }
    public bool ReturnsPending { get; init; }
    public bool RefundsPending { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 50)] public int PageSize { get; init; } = 12;
}

/// <summary>Accion sobre una version conocida; el motivo de cancelacion sera visible al cliente.</summary>
public sealed record OrderActionRequest
{
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    [StringLength(1000)] public string? Reason { get; init; }
}

public interface ICheckoutSettingsService
{
    Task<CheckoutSettingsResponse> GetAsync(CancellationToken ct);
    Task<CheckoutSettingsResponse> SaveAsync(CheckoutSettingsRequest request, Guid userId, CancellationToken ct);
}

public interface IOrderService
{
    Task<QuoteResponse> QuoteAsync(QuoteRequest request, CancellationToken ct);
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct);
    Task<OrderResponse> TrackAsync(TrackOrderRequest request, CancellationToken ct);
    Task<OrderResponse> RecoverAsync(RecoverOrderRequest request, CancellationToken ct);
    Task<PageResponse<AdminOrderResponse>> ListAsync(OrderQuery query, CancellationToken ct);
    Task<AdminOrderResponse> GetAsync(Guid id, CancellationToken ct);
    Task<AdminOrderResponse> ConfirmAsync(Guid id, OrderActionRequest request, Guid userId, CancellationToken ct);
    Task<AdminOrderResponse> CancelAsync(Guid id, OrderActionRequest request, Guid userId, CancellationToken ct);
}

public enum CheckoutError { Validation, NotFound, Conflict, Unavailable, Forbidden }

public sealed class CheckoutException(CheckoutError error, string message) : Exception(message)
{
    public CheckoutError Error { get; } = error;
}
