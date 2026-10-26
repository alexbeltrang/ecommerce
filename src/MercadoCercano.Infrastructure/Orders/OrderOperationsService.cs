using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Orders;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Orders;

public sealed partial class OrderService
{
    public async Task<AdminReturnResponse> ReturnByRequestAsync(Guid id, Guid requestId, CancellationToken ct)
    {
        var order = await Orders().SingleOrDefaultAsync(item => item.Id == id, ct) ?? throw Missing();
        var returned = order.Returns.SingleOrDefault(item => item.RequestId == requestId) ?? throw Missing();
        return Operations(order).Returns.Single(item => item.Summary.Id == returned.Id);
    }

    public async Task<AdminOrderResponse> FulfillAsync(Guid id, string action, FulfillmentRequest request, Guid userId, CancellationToken ct)
    {
        ValidateInput(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await MutableOrder(id, ct);
        Operational(order);
        var target = action switch
        {
            "prepare" => OrderStatus.Preparing, "ready" => OrderStatus.ReadyForDelivery,
            "ship" => OrderStatus.Shipped, "deliver" => OrderStatus.Delivered,
            _ => throw Validation("La transicion solicitada no existe.")
        };
        if (order.Status == target)
        {
            if (target == OrderStatus.Shipped && (order.CarrierName != Clean(request.CarrierName) || order.TrackingNumber != Clean(request.TrackingNumber)))
                throw Conflict("El despacho ya fue registrado con otros datos.");
            return Admin(order);
        }
        Version(order, new() { UpdatedAtUtc = request.UpdatedAtUtc });
        var validSource = target switch
        {
            OrderStatus.Preparing => order.Status == OrderStatus.Confirmed,
            OrderStatus.ReadyForDelivery => order.Status == OrderStatus.Preparing,
            OrderStatus.Shipped => order.Status == OrderStatus.ReadyForDelivery && order.DeliveryMethod != DeliveryMethod.CoordinatedPickup,
            OrderStatus.Delivered => order.DeliveryMethod == DeliveryMethod.CoordinatedPickup
                ? order.Status == OrderStatus.ReadyForDelivery : order.Status == OrderStatus.Shipped,
            _ => false
        };
        if (!validSource) throw Conflict("El estado actual no permite esa transicion. No se omitieron etapas.");
        CurrentPayment(order);
        if ((order.PaymentMethod != PaymentMethod.CashOnDelivery || target == OrderStatus.Delivered) && order.PaymentStatus != PaymentStatus.Paid)
            throw Conflict("Valida el pago antes de preparar o confirma el cobro contraentrega antes de entregar.");
        if (order.Returns.Any(item => item.Status != OrderReturnStatus.Rejected))
            throw Conflict("Resuelve la devolucion abierta antes de continuar el despacho.");
        if (target == OrderStatus.Shipped)
        {
            if (order.DeliveryMethod == DeliveryMethod.CarrierShipping &&
                (Clean(request.CarrierName) is null || Clean(request.TrackingNumber) is null))
                throw Validation("Indica la transportadora y el numero de guia.");
            order.CarrierName = Clean(request.CarrierName);
            order.TrackingNumber = Clean(request.TrackingNumber);
            order.ShippedAtUtc = DateTime.UtcNow;
        }
        if (target == OrderStatus.Delivered)
        {
            if (!request.DeliveryConfirmed) throw Validation("Confirma expresamente que el cliente recibio el pedido.");
            order.DeliveredAtUtc = DateTime.UtcNow;
        }
        ChangeStatus(order, target, target switch
        {
            OrderStatus.Preparing => "El negocio inicio la preparacion del pedido.",
            OrderStatus.ReadyForDelivery => "Pedido listo para recogida o despacho.",
            OrderStatus.Shipped => "Pedido despachado por el negocio.",
            _ => "Entrega al cliente confirmada por el negocio."
        }, userId);
        Audit(userId, "Order." + action, order.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Admin(order);
    }

    public async Task<AdminOrderResponse> ReviewPaymentAsync(Guid id, bool approve, PaymentReviewRequest request, Guid userId, CancellationToken ct)
    {
        ValidateInput(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await MutableOrder(id, ct);
        Operational(order);
        var payment = CurrentPayment(order);
        if (approve && payment.Status == PaymentStatus.Paid)
        {
            if (request.Amount != payment.Amount || Clean(request.Reference) != payment.Reference || !request.FundsVerified)
                throw Conflict("El pago ya fue validado con otros datos.");
            return Admin(order);
        }
        if (!approve && payment.Status == PaymentStatus.Rejected && Clean(request.Reason) == payment.RejectionReason)
            return Admin(order);
        Version(order, new() { UpdatedAtUtc = request.UpdatedAtUtc });
        if (order.Status == OrderStatus.Cancelled || payment.Status is PaymentStatus.Paid or PaymentStatus.Refunded)
            throw Conflict("El estado actual no permite revisar el pago.");
        if (approve)
        {
            if (!request.FundsVerified || request.Amount != order.GrandTotal)
                throw Validation("Verifica el dinero realmente recibido y el importe total exacto; no se admiten pagos parciales.");
            if (order.PaymentMethod == PaymentMethod.CashOnDelivery)
            {
                if (order.Status is not (OrderStatus.ReadyForDelivery or OrderStatus.Shipped))
                    throw Conflict("Registra el efectivo contraentrega cuando el pedido este listo o en ruta.");
            }
            else if (payment.Status != PaymentStatus.UnderReview || payment.Evidence.Count == 0 || Clean(request.Reference) is null)
                throw Conflict("Se requiere un comprobante en revision y una referencia para validar transferencia o billetera.");
            payment.Status = order.PaymentStatus = PaymentStatus.Paid;
            payment.Reference = Clean(request.Reference);
            payment.ConfirmedByUserId = userId;
            payment.ConfirmedAtUtc = DateTime.UtcNow;
            payment.RejectionReason = null;
        }
        else
        {
            if (payment.Status != PaymentStatus.UnderReview) throw Conflict("Solo se rechazan comprobantes en revision.");
            if (Clean(request.Reason) is null) throw Validation("Indica un motivo visible para el cliente, sin datos privados.");
            payment.Status = order.PaymentStatus = PaymentStatus.Rejected;
            payment.RejectionReason = request.Reason!.Trim();
        }
        Note(order, approve ? "Pago validado manualmente por el negocio." : "Comprobante rechazado. Consulta el motivo en el estado del pago.", userId);
        Audit(userId, approve ? "Payment.Approve" : "Payment.Reject", order.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Admin(order);
    }

    public async Task<AdminOrderResponse> SubmitEvidenceAsync(Guid id, PaymentEvidenceRequest request, Stream file, long length, string contentType, Guid userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await MutableOrder(id, ct);
        var name = await SaveEvidence(order, request, file, length, contentType, userId, ct);
        var committed = false;
        try { await transaction.CommitAsync(ct); committed = true; }
        finally { if (!committed) evidenceStore.Delete(name); }
        return Admin(order);
    }

    public async Task<OrderResponse> SubmitGuestEvidenceAsync(TrackOrderRequest access, PaymentEvidenceRequest request, Stream file, long length, string contentType, CancellationToken ct)
    {
        if (!ValidToken(access.TrackingToken)) throw Missing();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var number = access.OrderNumber.Trim().ToUpperInvariant();
        var order = await Orders().AsTracking().SingleOrDefaultAsync(item => item.OrderNumber == number, ct);
        if (order is null || !TokenMatches(order, access.TrackingToken)) throw Missing();
        var name = await SaveEvidence(order, request, file, length, contentType, null, ct);
        var committed = false;
        try { await transaction.CommitAsync(ct); committed = true; }
        finally { if (!committed) evidenceStore.Delete(name); }
        return Public(order);
    }

    private async Task<string> SaveEvidence(Order order, PaymentEvidenceRequest request, Stream file, long length, string contentType, Guid? userId, CancellationToken ct)
    {
        ValidateInput(request);
        Operational(order);
        Version(order, new() { UpdatedAtUtc = request.UpdatedAtUtc });
        var payment = CurrentPayment(order);
        if (order.Status == OrderStatus.Cancelled || order.PaymentMethod == PaymentMethod.CashOnDelivery ||
            payment.Status is not (PaymentStatus.Pending or PaymentStatus.Rejected))
            throw Conflict("No se puede adjuntar un comprobante en el estado actual.");
        if (payment.Evidence.Count >= 5) throw Conflict("Se alcanzo el limite de cinco comprobantes. Coordina la revision con el negocio.");
        var fileName = await evidenceStore.SaveAsync(file, length, contentType, ct);
        var saved = false;
        try
        {
            db.PaymentEvidence.Add(new PaymentEvidence { Payment = payment, StoredFileName = fileName, ByteLength = length });
            payment.Reference = request.Reference.Trim();
            payment.Status = order.PaymentStatus = PaymentStatus.UnderReview;
            payment.RejectionReason = null;
            Note(order, "Comprobante recibido para revision. Todavia no acredita el pago.", userId);
            Audit(userId, "Payment.Evidence", order.Id);
            await db.SaveChangesAsync(ct);
            saved = true;
            return fileName;
        }
        finally { if (!saved) evidenceStore.Delete(fileName); }
    }

    public async Task<StoredImageFile> OpenEvidenceAsync(Guid id, Guid evidenceId, CancellationToken ct)
    {
        var evidence = await db.PaymentEvidence.AsNoTracking().Include(item => item.Payment)
            .SingleOrDefaultAsync(item => item.Id == evidenceId && item.Payment.OrderId == id, ct)
            ?? throw Missing();
        return evidenceStore.Open(evidence.StoredFileName);
    }

    public async Task<AdminOrderResponse> CreateReturnAsync(Guid id, CreateReturnRequest request, Guid userId, CancellationToken ct)
    {
        ValidateInput(request);
        if (request.RequestId == Guid.Empty || request.Items.Any(item => item is null || item.OrderItemId == Guid.Empty || item.Quantity <= 0) ||
            request.Items.Select(item => item.OrderItemId).Distinct().Count() != request.Items.Length)
            throw Validation("Usa un identificador de intento y lineas distintas con cantidades positivas.");
        var fingerprint = Hash(JsonSerializer.Serialize(new
        {
            Items = request.Items.OrderBy(item => item.OrderItemId).Select(item => new { item.OrderItemId, item.Quantity }),
            Reason = request.Reason.Trim(), request.ShippingRefundAmount
        }));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await MutableOrder(id, ct);
        Operational(order);
        var existing = await db.OrderReturns.AsNoTracking().SingleOrDefaultAsync(item => item.RequestId == request.RequestId, ct);
        if (existing is not null)
        {
            if (existing.OrderId != id || existing.RequestFingerprint != fingerprint) throw Conflict("El intento de devolucion ya se utilizo con otros datos.");
            return Admin(order);
        }
        Version(order, new() { UpdatedAtUtc = request.UpdatedAtUtc });
        if (order.Status is not (OrderStatus.Delivered or OrderStatus.Shipped))
            throw Conflict("La devolucion fisica requiere entrega o un despacho fallido; antes de despachar utiliza cancelar.");
        var payment = CurrentPayment(order);
        var paid = payment.Status == PaymentStatus.Paid;
        if (!paid && !(order.Status == OrderStatus.Shipped && order.PaymentMethod == PaymentMethod.CashOnDelivery && payment.Status == PaymentStatus.Pending))
            throw Conflict("Concilia el pago antes de solicitar la devolucion.");
        var active = order.Returns.Where(item => item.Status != OrderReturnStatus.Rejected).ToArray();
        if (request.ShippingRefundAmount > order.ShippingCost - active.Sum(item => item.ShippingRefundAmount) ||
            (!paid && request.ShippingRefundAmount != 0))
            throw Validation("El reembolso de envio supera el saldo cobrado disponible.");
        var returned = new OrderReturn
        {
            Order = order, RequestId = request.RequestId, RequestFingerprint = fingerprint, RequestedByUserId = userId,
            Kind = order.Status == OrderStatus.Shipped ? OrderReturnKind.FailedDelivery : OrderReturnKind.CustomerReturn,
            Reason = request.Reason.Trim(), ShippingRefundAmount = request.ShippingRefundAmount
        };
        foreach (var line in request.Items)
        {
            var item = order.Items.SingleOrDefault(item => item.Id == line.OrderItemId) ?? throw Validation("La linea no pertenece a este pedido.");
            var allocated = active.SelectMany(item => item.Items).Where(item => item.OrderItemId == line.OrderItemId).Sum(item => (long)item.Quantity);
            if (line.Quantity > item.Quantity - allocated) throw Conflict("La cantidad supera las unidades aun disponibles para devolver.");
            var assigned = active.SelectMany(item => item.Items).Where(item => item.OrderItemId == line.OrderItemId).Sum(item => item.RefundAmount);
            var remainingAmount = item.LineTotal - item.PromotionDiscountTotal - assigned;
            returned.Items.Add(new OrderReturnItem
            {
                OrderItemId = item.Id, Quantity = line.Quantity,
                RefundAmount = paid ? Proportion(remainingAmount, line.Quantity, item.Quantity - allocated) : 0
            });
        }
        if (returned.Kind == OrderReturnKind.FailedDelivery &&
            (returned.Items.Count != order.Items.Count || returned.Items.Any(line => line.Quantity != order.Items.Single(item => item.Id == line.OrderItemId).Quantity)))
            throw Validation("Un despacho fallido requiere recibir el pedido completo; las devoluciones parciales se gestionan despues de entregar.");
        returned.RefundAmount = returned.Items.Sum(item => item.RefundAmount) + returned.ShippingRefundAmount;
        db.OrderReturns.Add(returned);
        Note(order, "Devolucion solicitada. Pendiente de recepcion fisica por el negocio.", userId);
        Audit(userId, "Order.ReturnRequested", id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Admin(order);
    }

    public async Task<AdminOrderResponse> ReceiveReturnAsync(Guid id, Guid returnId, ReceiveReturnRequest request, Guid userId, CancellationToken ct)
    {
        ValidateInput(request);
        if (!request.ReceiptConfirmed) throw Validation("Confirma la recepcion fisica de todas las unidades de esta solicitud.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await MutableOrder(id, ct);
        Operational(order);
        var returned = FindReturn(order, returnId);
        if (returned.Kind == OrderReturnKind.Cancellation) throw Conflict("La cancelacion no requiere recepcion ni otra reposicion de inventario.");
        if (request.Items.Any(line => line is null)) throw Validation("Las lineas de recepcion no pueden ser nulas.");
        if (request.Items.Select(item => item.ReturnItemId).Distinct().Count() != request.Items.Length ||
            request.Items.Length != returned.Items.Count ||
            request.Items.Any(line => !returned.Items.Any(item => item.Id == line.ReturnItemId) || line.RestockQuantity < 0 ||
                line.RestockQuantity > returned.Items.Single(item => item.Id == line.ReturnItemId).Quantity))
            throw Validation("Indica una vez cada linea y las unidades en buen estado, sin exceder las recibidas.");
        if (returned.Status is OrderReturnStatus.Received or OrderReturnStatus.Refunded)
        {
            if (request.Items.Any(line => returned.Items.Single(item => item.Id == line.ReturnItemId).RestockedQuantity != line.RestockQuantity))
                throw Conflict("Esta recepcion ya se registro con otras cantidades.");
            return Admin(order);
        }
        Version(order, new() { UpdatedAtUtc = request.UpdatedAtUtc });
        if (returned.Status != OrderReturnStatus.Requested) throw Conflict("La solicitud ya no permite recepcion.");
        var changes = new List<(OrderReturnItem Line, Product Product, int Quantity)>();
        foreach (var line in request.Items.Where(line => line.RestockQuantity > 0))
        {
            var item = returned.Items.Single(item => item.Id == line.ReturnItemId);
            var original = order.Items.Single(original => original.Id == item.OrderItemId);
            var product = await db.Products.SingleOrDefaultAsync(product => product.Id == original.ProductId, ct)
                ?? throw Conflict("El producto ya no existe. No se puede reponer su inventario.");
            if (product.StockQuantity > int.MaxValue - line.RestockQuantity) throw Conflict("La reposicion supera el limite de inventario.");
            changes.Add((item, product, line.RestockQuantity));
        }
        if (changes.GroupBy(item => item.Product.Id).Any(group => group.Sum(item => (long)item.Quantity) > int.MaxValue - (long)group.First().Product.StockQuantity))
            throw Conflict("La reposicion acumulada supera el limite de inventario.");
        foreach (var (line, product, quantity) in changes)
        {
            Move(product, quantity, InventoryMovementType.CustomerReturn, order, userId, "Recepcion de devolucion", returned);
            line.RestockedQuantity = quantity;
        }
        returned.Status = OrderReturnStatus.Received;
        returned.ReceivedAtUtc = DateTime.UtcNow;
        returned.ReceivedByUserId = userId;
        if (returned.Kind == OrderReturnKind.FailedDelivery)
        {
            order.CancellationReason = "Despacho no entregado; mercancia recibida por el negocio.";
            order.CancelledByUserId = userId;
            ChangeStatus(order, OrderStatus.Cancelled, order.CancellationReason, userId);
        }
        Note(order, "Devolucion recibida. Solo se repusieron las unidades en buen estado.", userId);
        Audit(userId, "Order.ReturnReceived", id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Admin(order);
    }

    public async Task<AdminOrderResponse> RejectReturnAsync(Guid id, Guid returnId, OrderActionRequest request, Guid userId, CancellationToken ct)
    {
        ValidateInput(request);
        if (Clean(request.Reason) is null) throw Validation("Indica un motivo publico para rechazar la solicitud.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await MutableOrder(id, ct);
        Operational(order);
        var returned = FindReturn(order, returnId);
        if (returned.Status == OrderReturnStatus.Rejected && returned.RejectionReason == request.Reason!.Trim()) return Admin(order);
        Version(order, request);
        if (returned.Status != OrderReturnStatus.Requested) throw Conflict("No se rechaza una devolucion ya recibida o reembolsada.");
        returned.Status = OrderReturnStatus.Rejected;
        returned.RejectionReason = request.Reason!.Trim();
        Note(order, "Solicitud de devolucion rechazada. Consulta el motivo en su resumen.", userId);
        Audit(userId, "Order.ReturnRejected", id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Admin(order);
    }

    public async Task<AdminOrderResponse> RefundReturnAsync(Guid id, Guid returnId, RefundReturnRequest request, Guid userId, CancellationToken ct)
    {
        ValidateInput(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Users.AnyAsync(user => user.Id == userId && user.IsActive && user.Role == UserRole.Administrator, ct))
            throw new CheckoutException(CheckoutError.Forbidden, "Solo el administrador puede registrar reembolsos.");
        if (!request.RefundCompleted) throw Validation("Confirma que el reembolso ya fue realizado fuera del sistema.");
        var order = await MutableOrder(id, ct);
        Operational(order);
        var returned = FindReturn(order, returnId);
        if (request.Amount != returned.RefundAmount || Clean(request.Reference) is null)
            throw Validation("El importe debe coincidir exactamente con el reembolso calculado y tener referencia.");
        if (returned.Status == OrderReturnStatus.Refunded)
        {
            if (returned.RefundReference != request.Reference.Trim()) throw Conflict("El reembolso ya fue registrado con otra referencia.");
            return Admin(order);
        }
        Version(order, new() { UpdatedAtUtc = request.UpdatedAtUtc });
        var payment = CurrentPayment(order);
        if (returned.Status != OrderReturnStatus.Received || returned.RefundAmount <= 0 || payment.Status != PaymentStatus.Paid)
            throw Conflict("Se requiere recepcion previa (o cancelacion sin despacho) y saldo de pago validado.");
        var previousRefunds = order.Returns.Where(item => item.Status == OrderReturnStatus.Refunded).Sum(item => item.RefundAmount);
        if (returned.RefundAmount > payment.Amount - previousRefunds) throw Conflict("El reembolso supera el dinero cobrado pendiente de devolver.");
        returned.Status = OrderReturnStatus.Refunded;
        returned.RefundedAtUtc = DateTime.UtcNow;
        returned.RefundedByUserId = userId;
        returned.RefundReference = request.Reference.Trim();
        if (previousRefunds + returned.RefundAmount == payment.Amount)
            payment.Status = order.PaymentStatus = PaymentStatus.Refunded;
        Note(order, "Reembolso registrado manualmente por el administrador.", userId);
        Audit(userId, "Order.RefundRecorded", id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Admin(order);
    }

    private static Payment CurrentPayment(Order order)
    {
        if (order.Payments.Count != 1) throw Conflict("El pedido requiere una conciliacion especial: debe tener un unico registro de pago.");
        var payment = order.Payments.Single();
        if (payment.Status != order.PaymentStatus || payment.Amount != order.GrandTotal || payment.Method != order.PaymentMethod)
            throw Conflict("El registro de pago no coincide con el pedido; revisa la conciliacion.");
        return payment;
    }
    private void Operational(Order order)
    {
        if (order.IsTestOrder && !allowTestOrders) throw Conflict("Un pedido de prueba no puede operarse fuera de Development.");
    }
    private void Note(Order order, string reason, Guid? userId)
    {
        db.OrderStatusHistory.Add(new OrderStatusHistory { Order = order, NewStatus = order.Status, PreviousStatus = order.Status, Reason = reason, UserId = userId });
        db.Entry(order).Property(item => item.UpdatedAtUtc).IsModified = true;
    }
    private static void ValidateInput(object value)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new(value), results, true))
            throw Validation(string.Join(" ", results.Select(result => result.ErrorMessage)));
    }
    private static OrderReturn FindReturn(Order order, Guid id) => order.Returns.SingleOrDefault(item => item.Id == id) ?? throw Missing();
    private static DateTimeOffset? OptionalUtc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
    private static ReturnSummaryResponse ReturnSummary(OrderReturn item) => new(item.Id, item.Kind.ToString(), item.Status.ToString(),
        item.Reason, item.RejectionReason, item.RefundAmount, Utc(item.CreatedAtUtc), OptionalUtc(item.ReceivedAtUtc), OptionalUtc(item.RefundedAtUtc));
    private static OrderOperationsResponse Operations(Order order)
    {
        var active = order.Returns.Where(item => item.Status != OrderReturnStatus.Rejected).SelectMany(item => item.Items).ToArray();
        var payment = order.Payments.Count == 1 ? order.Payments.Single() : null;
        return new(payment?.Reference, payment?.RejectionReason,
            order.Payments.SelectMany(item => item.Evidence).OrderBy(item => item.CreatedAtUtc).Select(item => new PaymentEvidenceResponse(item.Id, item.ByteLength, Utc(item.CreatedAtUtc))).ToArray(),
            order.Items.OrderBy(item => item.SkuSnapshot).Select(item => new ReturnableLineResponse(item.Id, item.ProductNameSnapshot, item.Quantity, item.UnitPrice,
                (int)(item.Quantity - active.Where(line => line.OrderItemId == item.Id).Sum(line => (long)line.Quantity)),
                payment?.Status == PaymentStatus.Paid
                    ? item.LineTotal - item.PromotionDiscountTotal - active.Where(line => line.OrderItemId == item.Id).Sum(line => line.RefundAmount)
                    : 0)).ToArray(),
            order.Returns.OrderBy(item => item.CreatedAtUtc).Select(item => new AdminReturnResponse(ReturnSummary(item), item.ShippingRefundAmount,
                item.RefundReference, item.Items.Select(line => new ReturnItemResponse(line.Id, line.OrderItemId,
                    order.Items.Single(original => original.Id == line.OrderItemId).ProductNameSnapshot, line.Quantity, line.RestockedQuantity, line.RefundAmount)).ToArray())).ToArray());
    }
}
