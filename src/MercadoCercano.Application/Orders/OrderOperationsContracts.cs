using System.ComponentModel.DataAnnotations;
using MercadoCercano.Application.Catalog;

namespace MercadoCercano.Application.Orders;

/// <summary>Transicion operativa ligada a una version del pedido.</summary>
public sealed record FulfillmentRequest
{
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    [StringLength(120)] public string? CarrierName { get; init; }
    [StringLength(120)] public string? TrackingNumber { get; init; }
    public bool DeliveryConfirmed { get; init; }
}

/// <summary>Referencia de un comprobante privado; no confirma la recepcion de dinero.</summary>
public sealed record PaymentEvidenceRequest
{
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    [Required, StringLength(160)] public required string Reference { get; init; }
}

/// <summary>Validacion manual del importe recibido o motivo publico de rechazo.</summary>
public sealed record PaymentReviewRequest
{
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    [Range(0, 9_000_000_000_000)] public long Amount { get; init; }
    [StringLength(160)] public string? Reference { get; init; }
    [StringLength(1000)] public string? Reason { get; init; }
    public bool FundsVerified { get; init; }
}

/// <summary>Linea parcial solicitada por cantidad, sin precios suministrados por el cliente.</summary>
public sealed record ReturnLineRequest
{
    public Guid OrderItemId { get; init; }
    [Range(1, int.MaxValue)] public int Quantity { get; init; }
}

/// <summary>Solicitud idempotente de devolucion; el servidor calcula el importe.</summary>
public sealed record CreateReturnRequest
{
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public Guid RequestId { get; init; }
    [Required, MinLength(1), MaxLength(50)] public required ReturnLineRequest[] Items { get; init; }
    [Required, StringLength(1000)] public required string Reason { get; init; }
    [Range(0, 9_000_000_000_000)] public long ShippingRefundAmount { get; init; }
}

/// <summary>Unidades en buen estado que realmente pueden volver a venderse.</summary>
public sealed record ReceiveReturnLineRequest
{
    public Guid ReturnItemId { get; init; }
    [Range(0, int.MaxValue)] public int RestockQuantity { get; init; }
}

/// <summary>Recepcion fisica completa de la solicitud; mercancia danada no se repone.</summary>
public sealed record ReceiveReturnRequest
{
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public bool ReceiptConfirmed { get; init; }
    [Required, MinLength(1), MaxLength(50)] public required ReceiveReturnLineRequest[] Items { get; init; }
}

/// <summary>Registro administrativo de dinero ya reembolsado fuera del sistema.</summary>
public sealed record RefundReturnRequest
{
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    [Range(1, 9_000_000_000_000)] public long Amount { get; init; }
    [Required, StringLength(160)] public required string Reference { get; init; }
    public bool RefundCompleted { get; init; }
}

/// <summary>Datos publicos del despacho, sin direccion del comprador.</summary>
public sealed record FulfillmentResponse(string? CarrierName, string? TrackingNumber, DateTimeOffset? ShippedAtUtc, DateTimeOffset? DeliveredAtUtc);
/// <summary>Resumen publico de devolucion; nunca incluye referencia bancaria.</summary>
public sealed record ReturnSummaryResponse(Guid Id, string Kind, string Status, string Reason, string? RejectionReason,
    long RefundAmount, DateTimeOffset CreatedAtUtc, DateTimeOffset? ReceivedAtUtc, DateTimeOffset? RefundedAtUtc);
/// <summary>Comprobante privado descargable solo por personal autorizado.</summary>
public sealed record PaymentEvidenceResponse(Guid Id, long ByteLength, DateTimeOffset CreatedAtUtc);
/// <summary>Linea identificable y saldo de unidades aun no solicitadas en devolucion.</summary>
public sealed record ReturnableLineResponse(Guid Id, string Name, int Quantity, long UnitPrice, int ReturnableQuantity, long RefundableAmount);
/// <summary>Detalle privado para registrar recepcion y reembolso.</summary>
public sealed record ReturnItemResponse(Guid Id, Guid OrderItemId, string Name, int Quantity, int RestockedQuantity, long RefundAmount);
/// <summary>Solicitud operativa con referencia financiera privada.</summary>
public sealed record AdminReturnResponse(ReturnSummaryResponse Summary, long ShippingRefundAmount, string? RefundReference, IReadOnlyList<ReturnItemResponse> Items);
/// <summary>Herramientas operativas privadas; no se proyectan en seguimiento anonimo.</summary>
public sealed record OrderOperationsResponse(string? PaymentReference, string? PaymentRejectionReason,
    IReadOnlyList<PaymentEvidenceResponse> Evidence, IReadOnlyList<ReturnableLineResponse> Lines, IReadOnlyList<AdminReturnResponse> Returns);

public interface IPrivatePaymentEvidenceStore
{
    Task<string> SaveAsync(Stream stream, long length, string contentType, CancellationToken ct);
    StoredImageFile Open(string fileName);
    void Delete(string fileName);
}

public interface IOrderOperationsService
{
    Task<AdminReturnResponse> ReturnByRequestAsync(Guid id, Guid requestId, CancellationToken ct);
    Task<AdminOrderResponse> FulfillAsync(Guid id, string action, FulfillmentRequest request, Guid userId, CancellationToken ct);
    Task<AdminOrderResponse> ReviewPaymentAsync(Guid id, bool approve, PaymentReviewRequest request, Guid userId, CancellationToken ct);
    Task<AdminOrderResponse> SubmitEvidenceAsync(Guid id, PaymentEvidenceRequest request, Stream file, long length, string contentType, Guid userId, CancellationToken ct);
    Task<OrderResponse> SubmitGuestEvidenceAsync(TrackOrderRequest access, PaymentEvidenceRequest request, Stream file, long length, string contentType, CancellationToken ct);
    Task<StoredImageFile> OpenEvidenceAsync(Guid id, Guid evidenceId, CancellationToken ct);
    Task<AdminOrderResponse> CreateReturnAsync(Guid id, CreateReturnRequest request, Guid userId, CancellationToken ct);
    Task<AdminOrderResponse> ReceiveReturnAsync(Guid id, Guid returnId, ReceiveReturnRequest request, Guid userId, CancellationToken ct);
    Task<AdminOrderResponse> RejectReturnAsync(Guid id, Guid returnId, OrderActionRequest request, Guid userId, CancellationToken ct);
    Task<AdminOrderResponse> RefundReturnAsync(Guid id, Guid returnId, RefundReturnRequest request, Guid userId, CancellationToken ct);
}
