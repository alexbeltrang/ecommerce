using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Orders;
using MercadoCercano.Domain.Entities;
using MercadoCercano.Domain.Enums;
using MercadoCercano.Infrastructure.Persistence;
using MercadoCercano.Infrastructure.Promotions;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Orders;

public sealed partial class OrderService(
    StoreDbContext db, ICheckoutSettingsService settings, bool allowTestOrders,
    IPrivatePaymentEvidenceStore evidenceStore) : IOrderService, IOrderOperationsService
{
    public const long MaximumAmount = 9_000_000_000_000;

    public async Task<QuoteResponse> QuoteAsync(QuoteRequest request, CancellationToken ct) =>
        (await PriceAsync(request, ct)).Quote;

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct)
    {
        ValidateSelection(request.Selection);
        if (request.IdempotencyKey == Guid.Empty || !ValidToken(request.TrackingToken))
            throw Validation("Falta un identificador de intento o una credencial de seguimiento valida.");
        if (!request.TermsAccepted) throw Validation("Debes aceptar las condiciones antes de crear el pedido.");
        if (string.IsNullOrWhiteSpace(request.CustomerName) ||
            string.IsNullOrWhiteSpace(request.Phone) || request.Phone.Count(char.IsAsciiDigit) is < 7 or > 15 ||
            request.Phone.Any(character => !char.IsAsciiDigit(character) && !" +()-".Contains(character)))
            throw Validation("Indica tu nombre y un telefono valido de 7 a 15 digitos.");
        if (request.Selection.DeliveryMethod != nameof(DeliveryMethod.CoordinatedPickup) &&
            string.IsNullOrWhiteSpace(request.Address))
            throw Validation("La entrega a domicilio o por transportadora requiere una direccion.");

        var key = request.IdempotencyKey.ToString("N");
        var fingerprint = RequestFingerprint(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existing = await Orders().SingleOrDefaultAsync(order => order.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (!TokenMatches(existing, request.TrackingToken)) throw Missing();
            if (existing.RequestFingerprint != fingerprint)
                throw Conflict("Este intento ya creo un pedido con otros datos. Recupera el pedido antes de iniciar otro.");
            return Public(existing);
        }
        var trackingHash = Hash(request.TrackingToken);
        if (await db.Orders.AnyAsync(order => order.TrackingTokenHash == trackingHash, ct))
            throw Conflict("La credencial de seguimiento pertenece a otro intento. Recupera el pedido original antes de crear otro.");

        var priced = await PriceAsync(request.Selection, ct);
        var quote = priced.Quote;
        if (request.QuoteFingerprint != quote.Fingerprint)
            throw Conflict("Los precios, la entrega o las condiciones cambiaron. Recalcula y revisa el resumen.");
        var customer = new Customer
        {
            FullName = request.CustomerName.Trim(), Phone = request.Phone.Trim(), Email = Clean(request.Email)
        };
        if (quote.DeliveryMethod != nameof(DeliveryMethod.CoordinatedPickup))
        {
            customer.Addresses.Add(new Address
            {
                Municipality = quote.Municipality, Neighborhood = Clean(request.Neighborhood),
                AddressLine = request.Address!.Trim(), References = Clean(request.AddressReferences), IsDefault = true
            });
        }
        var order = new Order
        {
            OrderNumber = $"MC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..20].ToUpperInvariant()}",
            Customer = customer, CustomerId = customer.Id,
            CustomerNameSnapshot = customer.FullName, CustomerPhoneSnapshot = customer.Phone,
            CustomerEmailSnapshot = customer.Email, MunicipalitySnapshot = quote.Municipality,
            NeighborhoodSnapshot = quote.DeliveryMethod == nameof(DeliveryMethod.CoordinatedPickup) ? null : Clean(request.Neighborhood),
            AddressSnapshot = quote.DeliveryMethod == nameof(DeliveryMethod.CoordinatedPickup) ? null : request.Address!.Trim(),
            AddressReferencesSnapshot = quote.DeliveryMethod == nameof(DeliveryMethod.CoordinatedPickup) ? null : Clean(request.AddressReferences),
            DeliveryMethod = Enum.Parse<DeliveryMethod>(quote.DeliveryMethod),
            PaymentMethod = Enum.Parse<PaymentMethod>(quote.PaymentMethod),
            Subtotal = quote.Subtotal, DiscountTotal = quote.DiscountTotal, ShippingCost = quote.ShippingCost,
            ShippingCostConfirmed = !quote.IsTestConfiguration, GrandTotal = quote.GrandTotal,
            PromotionNameSnapshot = quote.Promotion?.Name, PromotionCodeSnapshot = quote.Promotion?.Code,
            PromotionDiscountTotal = quote.Promotion?.DiscountTotal ?? 0,
            PaymentStatus = quote.GrandTotal == 0 ? PaymentStatus.Paid : PaymentStatus.Pending,
            CustomerNotes = Clean(request.Notes), TrackingTokenHash = trackingHash,
            IdempotencyKey = key, RequestFingerprint = fingerprint, IsTestOrder = quote.IsTestConfiguration,
            TermsAcceptedAtUtc = DateTime.UtcNow, CheckoutNoticeSnapshot = quote.TermsText,
            PickupInstructionsSnapshot = quote.DeliveryMethod == nameof(DeliveryMethod.CoordinatedPickup) ? quote.PickupInstructions : null,
            PaymentInstructionsSnapshot = quote.PaymentInstructions
        };
        foreach (var line in quote.Items)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = line.ProductId, ProductNameSnapshot = line.Name, SkuSnapshot = line.Sku,
                Quantity = line.Quantity, UnitPrice = line.UnitPrice, LineTotal = line.LineTotal,
                PromotionDiscountTotal = line.PromotionDiscountTotal,
                UnitCostSnapshot = priced.Products[line.ProductId].Cost
            });
        }
        order.Payments.Add(new Payment
        {
            Method = order.PaymentMethod, Amount = order.GrandTotal, Status = order.PaymentStatus,
            ConfirmedAtUtc = order.GrandTotal == 0 ? DateTime.UtcNow : null
        });
        order.StatusHistory.Add(new OrderStatusHistory
        {
            NewStatus = OrderStatus.PendingConfirmation,
            Reason = "Pedido recibido. El inventario se descuenta al confirmar por el negocio."
        });
        db.Orders.Add(order);
        if (priced.Promotion is not null) await ConsumePromotionAsync(priced.Promotion, order, ct);
        Audit(null, "Order.Create", order.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Public(order);
    }

    public async Task<OrderResponse> TrackAsync(TrackOrderRequest request, CancellationToken ct)
    {
        if (!ValidToken(request.TrackingToken)) throw Missing();
        var number = request.OrderNumber.Trim().ToUpperInvariant();
        var order = await Orders().SingleOrDefaultAsync(order => order.OrderNumber == number, ct);
        if (order is null || !TokenMatches(order, request.TrackingToken)) throw Missing();
        return Public(order);
    }

    public async Task<OrderResponse> RecoverAsync(RecoverOrderRequest request, CancellationToken ct)
    {
        if (request.IdempotencyKey == Guid.Empty || !ValidToken(request.TrackingToken)) throw Missing();
        var key = request.IdempotencyKey.ToString("N");
        var order = await Orders().SingleOrDefaultAsync(order => order.IdempotencyKey == key, ct);
        if (order is null || !TokenMatches(order, request.TrackingToken)) throw Missing();
        return Public(order);
    }

    public async Task<PageResponse<AdminOrderResponse>> ListAsync(OrderQuery query, CancellationToken ct)
    {
        var orders = Orders();
        if (!string.IsNullOrEmpty(query.Status))
        {
            if (!Enum.TryParse<OrderStatus>(query.Status, out var status) || !Enum.IsDefined(status) || status.ToString() != query.Status)
                throw Validation("El estado de pedido no es valido.");
            orders = orders.Where(order => order.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + query.Search.Trim().Replace("/", "//").Replace("%", "/%").Replace("_", "/_") + "%";
            orders = orders.Where(order => EF.Functions.Like(order.OrderNumber, pattern, "/") ||
                EF.Functions.Like(order.CustomerNameSnapshot, pattern, "/") ||
                EF.Functions.Like(order.CustomerPhoneSnapshot, pattern, "/"));
        }
        if (!string.IsNullOrEmpty(query.PaymentStatus))
        {
            if (!Enum.TryParse<PaymentStatus>(query.PaymentStatus, out var paymentStatus) || !Enum.IsDefined(paymentStatus) || paymentStatus.ToString() != query.PaymentStatus)
                throw Validation("El estado de pago no es valido.");
            orders = orders.Where(order => order.PaymentStatus == paymentStatus);
        }
        if (!string.IsNullOrEmpty(query.DeliveryMethod))
        {
            if (!Enum.TryParse<DeliveryMethod>(query.DeliveryMethod, out var delivery) || !Enum.IsDefined(delivery) || delivery.ToString() != query.DeliveryMethod)
                throw Validation("La modalidad de entrega no es valida.");
            orders = orders.Where(order => order.DeliveryMethod == delivery);
        }
        if (query.ReturnsPending) orders = orders.Where(order => order.Returns.Any(item => item.Status == OrderReturnStatus.Requested));
        if (query.RefundsPending) orders = orders.Where(order => order.Returns.Any(item => item.Status == OrderReturnStatus.Received && item.RefundAmount > 0));
        var total = await orders.CountAsync(ct);
        var offset = (long)(query.Page - 1) * query.PageSize;
        var sorted = orders.OrderByDescending(order => order.CreatedAtUtc).ThenBy(order => order.Id);
        var page = offset > int.MaxValue ? sorted.Take(0) : sorted.Skip((int)offset).Take(query.PageSize);
        return new((await page.ToListAsync(ct)).Select(Admin).ToArray(), total, query.Page, query.PageSize);
    }

    public async Task<AdminOrderResponse> GetAsync(Guid id, CancellationToken ct) =>
        Admin(await Orders().SingleOrDefaultAsync(order => order.Id == id, ct) ?? throw Missing());

    public async Task<AdminOrderResponse> ConfirmAsync(
        Guid id, OrderActionRequest request, Guid userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await MutableOrder(id, ct);
        if (order.Status == OrderStatus.Confirmed) return Admin(order);
        if (order.Status != OrderStatus.PendingConfirmation) throw Conflict("Solo se confirman pedidos pendientes.");
        Version(order, request);
        if (order.IsTestOrder && !allowTestOrders)
            throw Conflict("Un pedido de prueba no puede confirmarse fuera de Development.");
        var products = await ProductsForOrder(order, ct);
        foreach (var item in order.Items)
        {
            var product = products[item.ProductId!.Value];
            if (!product.IsActive || !product.Category.IsActive || product.StockQuantity < item.Quantity)
                throw Conflict($"No hay disponibilidad suficiente para {item.ProductNameSnapshot}. No se modifico el inventario.");
        }
        foreach (var item in order.Items)
        {
            var product = products[item.ProductId!.Value];
            Move(product, -item.Quantity, InventoryMovementType.Sale, order, userId, "Confirmacion de pedido");
        }
        ChangeStatus(order, OrderStatus.Confirmed, "Pedido confirmado por el negocio.", userId);
        Audit(userId, "Order.Confirm", order.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Admin(order);
    }

    public async Task<AdminOrderResponse> CancelAsync(
        Guid id, OrderActionRequest request, Guid userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await MutableOrder(id, ct);
        if (order.Status == OrderStatus.Cancelled) return Admin(order);
        if (order.Status is not (OrderStatus.PendingConfirmation or OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.ReadyForDelivery))
            throw Conflict("Un pedido despachado requiere recepcion de devolucion, no cancelacion directa.");
        Version(order, request);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw Validation("Indica un motivo de cancelacion visible al cliente.");
        CurrentPayment(order);
        if (order.PaymentStatus == PaymentStatus.UnderReview || order.PaymentStatus == PaymentStatus.Refunded)
            throw Conflict("Valida o rechaza el pago en revision antes de cancelar.");
        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            var refund = new OrderReturn
            {
                Order = order, Kind = OrderReturnKind.Cancellation, Status = OrderReturnStatus.Received,
                Reason = "Reembolso por cancelacion antes del despacho.", RefundAmount = order.GrandTotal,
                ShippingRefundAmount = order.ShippingCost, RequestedByUserId = userId,
                ReceivedByUserId = userId, ReceivedAtUtc = DateTime.UtcNow
            };
            foreach (var item in order.Items)
                refund.Items.Add(new OrderReturnItem { OrderItemId = item.Id, Quantity = item.Quantity, RefundAmount = item.LineTotal - item.PromotionDiscountTotal });
            db.OrderReturns.Add(refund);
        }
        if (order.Status != OrderStatus.PendingConfirmation)
        {
            var products = await ProductsForOrder(order, ct);
            foreach (var item in order.Items)
            {
                var product = products[item.ProductId!.Value];
                if (product.StockQuantity > int.MaxValue - item.Quantity)
                    throw Conflict("La reposicion supera el limite de inventario. Revisa las existencias.");
            }
            foreach (var item in order.Items)
                Move(products[item.ProductId!.Value], item.Quantity, InventoryMovementType.OrderCancellation,
                    order, userId, "Cancelacion de pedido");
        }
        await ReleasePromotionAsync(order, ct);
        order.CancellationReason = request.Reason.Trim();
        order.CancelledByUserId = userId;
        ChangeStatus(order, OrderStatus.Cancelled, order.CancellationReason, userId);
        Audit(userId, "Order.Cancel", order.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Admin(order);
    }

    private async Task<PricedCart> PriceAsync(QuoteRequest request, CancellationToken ct)
    {
        ValidateSelection(request);
        var configuration = await settings.GetAsync(ct);
        var values = configuration.Values;
        if (values.IsTestConfiguration && !allowTestOrders)
            throw new CheckoutException(CheckoutError.Unavailable, "El administrador debe reemplazar las tarifas y condiciones de prueba antes de recibir pedidos reales.");
        if (!values.PaymentMethods.Contains(request.PaymentMethod, StringComparer.Ordinal))
            throw Validation("El medio de pago seleccionado no esta habilitado.");
        var municipality = request.DeliveryMethod == nameof(DeliveryMethod.CoordinatedPickup)
            ? values.PickupMunicipality.Trim() : Clean(request.Municipality);
        long shipping;
        switch (request.DeliveryMethod)
        {
            case nameof(DeliveryMethod.CoordinatedPickup) when values.PickupEnabled:
                shipping = 0;
                break;
            case nameof(DeliveryMethod.LocalDelivery) when values.LocalDeliveryEnabled:
                if (municipality is null || !values.LocalMunicipalities.Contains(municipality, StringComparer.OrdinalIgnoreCase))
                    throw Validation("El municipio no tiene cobertura de domicilio local.");
                shipping = values.LocalDeliveryFee;
                break;
            case nameof(DeliveryMethod.CarrierShipping) when values.CarrierShippingEnabled:
                if (municipality is null || (!values.CarrierNationwide &&
                    !values.CarrierMunicipalities.Contains(municipality, StringComparer.OrdinalIgnoreCase)))
                    throw Validation("El municipio no tiene cobertura de transportadora.");
                shipping = values.CarrierShippingFee;
                break;
            default: throw Validation("La modalidad de entrega no esta habilitada.");
        }
        var ids = request.Items.Select(item => item.ProductId).ToArray();
        var products = await db.Products.AsNoTracking().Include(product => product.Category)
            .Where(product => ids.Contains(product.Id)).ToDictionaryAsync(product => product.Id, ct);
        var lines = new List<QuoteLineResponse>();
        long subtotal = 0, effectiveTotal = 0;
        var now = DateTime.UtcNow;
        foreach (var item in request.Items.OrderBy(item => item.ProductId))
        {
            if (!products.TryGetValue(item.ProductId, out var product) || !product.IsActive || !product.Category.IsActive)
                throw Conflict("Un producto del carrito ya no esta disponible. Revisa el carrito.");
            if (product.StockQuantity < item.Quantity)
                throw Conflict($"La cantidad de {product.Name} supera las existencias actuales. El carrito no reserva unidades.");
            var price = product.EffectivePriceAt(now);
            if (price <= 0 || product.Price <= 0 || item.Quantity > MaximumAmount / product.Price)
                throw Validation("El importe del pedido supera el limite permitido.");
            var regularLine = product.Price * item.Quantity;
            var line = price * item.Quantity;
            if (subtotal > MaximumAmount - regularLine) throw Validation("El importe del pedido supera el limite permitido.");
            subtotal += regularLine;
            effectiveTotal += line;
            lines.Add(new(product.Id, product.Name, product.Sku, item.Quantity, product.Price, price, line, product.StockQuantity));
        }
        var promotion = await SelectPromotionAsync(request, lines, products, now, ct);
        if (promotion is not null) lines = DistributeDiscount(lines, promotion);
        var additional = promotion?.Amount ?? 0;
        if (effectiveTotal - additional > MaximumAmount - shipping) throw Validation("El total con envio supera el limite permitido.");
        var grandTotal = effectiveTotal - additional + shipping;
        var fingerprint = Hash(JsonSerializer.Serialize(new
        {
            Lines = lines.Select(line => new { line.ProductId, line.Quantity, line.RegularUnitPrice, line.UnitPrice, line.Name, line.Sku }),
            Shipping = shipping, Total = grandTotal, request.DeliveryMethod, request.PaymentMethod,
            Municipality = municipality, configuration.UpdatedAtUtc
        }));
        var code = PromotionService.NormalizeCode(request.CouponCode);
        if (promotion is not null || code is not null)
            fingerprint = Hash(fingerprint + JsonSerializer.Serialize(new
            {
                CouponCode = code, promotion?.Rule.Id, promotion?.Rule.UpdatedAtUtc, Discount = additional,
                Shares = lines.Select(line => new { line.ProductId, line.PromotionDiscountTotal })
            }));
        return new(new(lines, subtotal, subtotal - effectiveTotal + additional, shipping, grandTotal,
            request.DeliveryMethod, request.PaymentMethod, municipality!, values.IsTestConfiguration,
            values.TermsText, values.PickupInstructions, values.PaymentInstructions, fingerprint,
            promotion is null ? null : new(promotion.Rule.Name, promotion.Rule.Code, promotion.Amount),
            code is not null && PromotionService.NormalizeCode(promotion?.Rule.Code) != code ? "Se aplico una promocion automatica con mayor beneficio; el cupon no consume usos." : null),
            products, promotion?.Rule);
    }

    private static void ValidateSelection(QuoteRequest request)
    {
        var code = PromotionService.NormalizeCode(request.CouponCode);
        if (code is not null && !PromotionService.ValidCode(code))
            throw Validation("El cupon requiere de 3 a 80 letras ASCII, numeros, guiones o guion bajo.");
        if (request.Items is null || request.Items.Length is < 1 or > 50 ||
            request.Items.Any(item => item is null || item.ProductId == Guid.Empty || item.Quantity <= 0) ||
            request.Items.Select(item => item.ProductId).Distinct().Count() != request.Items.Length)
            throw Validation("El carrito requiere de 1 a 50 productos distintos con cantidades enteras positivas.");
        if (!Enum.TryParse<DeliveryMethod>(request.DeliveryMethod, out var delivery) || !Enum.IsDefined(delivery) ||
            delivery.ToString() != request.DeliveryMethod ||
            !Enum.TryParse<PaymentMethod>(request.PaymentMethod, out var payment) || !Enum.IsDefined(payment) ||
            payment.ToString() != request.PaymentMethod)
            throw Validation("Selecciona entrega y medio de pago validos.");
    }

    private IQueryable<Order> Orders() => db.Orders.AsNoTracking()
        .Include(order => order.Items).Include(order => order.StatusHistory)
        .Include(order => order.Payments).ThenInclude(payment => payment.Evidence)
        .Include(order => order.Returns).ThenInclude(item => item.Items).AsSplitQuery();

    private async Task<Order> MutableOrder(Guid id, CancellationToken ct) =>
        await Orders().AsTracking().SingleOrDefaultAsync(order => order.Id == id, ct) ?? throw Missing();

    private async Task<Dictionary<Guid, Product>> ProductsForOrder(Order order, CancellationToken ct)
    {
        if (order.Items.Count == 0 || order.Items.Any(item => !item.ProductId.HasValue))
            throw Conflict("Un producto del pedido ya no existe. Revisa el pedido antes de operarlo.");
        var ids = order.Items.Select(item => item.ProductId!.Value).Distinct().ToArray();
        var products = await db.Products.Include(product => product.Category)
            .Where(product => ids.Contains(product.Id)).ToDictionaryAsync(product => product.Id, ct);
        if (products.Count != ids.Length) throw Conflict("Un producto del pedido ya no existe.");
        return products;
    }

    private void Move(Product product, int delta, InventoryMovementType type, Order order, Guid userId, string reason, OrderReturn? returned = null)
    {
        var previous = product.StockQuantity;
        product.StockQuantity += delta;
        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id, UserId = userId, MovementType = type,
            PreviousQuantity = previous, NewQuantity = product.StockQuantity, QuantityDelta = delta,
            Reason = reason + " " + order.OrderNumber, ReferenceType = returned is null ? nameof(Order) : nameof(OrderReturn),
            ReferenceId = (returned?.Id ?? order.Id).ToString()
        });
    }

    private void ChangeStatus(Order order, OrderStatus status, string reason, Guid userId)
    {
        db.OrderStatusHistory.Add(new OrderStatusHistory
        {
            Order = order, OrderId = order.Id, PreviousStatus = order.Status,
            NewStatus = status, Reason = reason, UserId = userId
        });
        order.Status = status;
    }

    private void Audit(Guid? userId, string action, Guid id) =>
        db.AuditLogs.Add(new AuditLog { UserId = userId, Action = action, EntityType = nameof(Order), EntityId = id.ToString() });

    private static void Version(Order order, OrderActionRequest request)
    {
        if (!request.UpdatedAtUtc.HasValue || request.UpdatedAtUtc.Value.UtcDateTime != order.UpdatedAtUtc)
            throw Conflict("El pedido cambio. Recargalo antes de continuar.");
    }

    private static string RequestFingerprint(CreateOrderRequest request)
    {
        var original = Hash(JsonSerializer.Serialize(new
        {
        Items = request.Selection.Items.OrderBy(item => item.ProductId).Select(item => new { item.ProductId, item.Quantity }),
        request.Selection.DeliveryMethod, request.Selection.PaymentMethod,
        Municipality = Clean(request.Selection.Municipality), request.QuoteFingerprint,
        Name = request.CustomerName.Trim(), Phone = request.Phone.Trim(), Email = Clean(request.Email),
        Neighborhood = Clean(request.Neighborhood), Address = Clean(request.Address),
        References = Clean(request.AddressReferences), Notes = Clean(request.Notes), request.TermsAccepted
        }));
        var code = PromotionService.NormalizeCode(request.Selection.CouponCode);
        return code is null ? original : Hash(original + "\n" + code);
    }

    private static bool ValidToken(string? token) => token is not null &&
        Regex.IsMatch(token, @"\A[A-Za-z0-9_-]{43}\z", RegexOptions.CultureInvariant);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool TokenMatches(Order order, string token) => order.TrackingTokenHash is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(order.TrackingTokenHash), Encoding.ASCII.GetBytes(Hash(token)));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static OrderResponse Public(Order order) => new(
        order.OrderNumber, order.Status.ToString(), order.PaymentStatus.ToString(), order.DeliveryMethod.ToString(),
        order.PaymentMethod.ToString(), order.Subtotal, order.DiscountTotal, order.ShippingCost, order.GrandTotal,
        order.IsTestOrder, Utc(order.CreatedAtUtc),
        order.Items.OrderBy(item => item.SkuSnapshot).Select(item => new OrderLineResponse(
            item.ProductNameSnapshot, item.SkuSnapshot, item.Quantity, item.UnitPrice, item.LineTotal, item.PromotionDiscountTotal)).ToArray(),
        order.StatusHistory.OrderBy(history => history.CreatedAtUtc).ThenBy(history => history.Id)
            .Select(history => new OrderHistoryResponse(history.NewStatus.ToString(), history.Reason, Utc(history.CreatedAtUtc))).ToArray(),
        order.CheckoutNoticeSnapshot, order.PickupInstructionsSnapshot, order.PaymentInstructionsSnapshot,
        Utc(order.UpdatedAtUtc), new(order.CarrierName, order.TrackingNumber, OptionalUtc(order.ShippedAtUtc), OptionalUtc(order.DeliveredAtUtc)),
        order.Returns.Where(item => item.Status == OrderReturnStatus.Refunded).Sum(item => item.RefundAmount),
        order.Payments.Count == 1 ? order.Payments.Single().RejectionReason : null,
        order.Returns.OrderBy(item => item.CreatedAtUtc).Select(ReturnSummary).ToArray(),
        order.PromotionNameSnapshot is null ? null : new(order.PromotionNameSnapshot, order.PromotionCodeSnapshot, order.PromotionDiscountTotal));
    private static AdminOrderResponse Admin(Order order) => new(
        order.Id, Public(order), order.CustomerNameSnapshot, order.CustomerPhoneSnapshot,
        order.CustomerEmailSnapshot, order.MunicipalitySnapshot, order.NeighborhoodSnapshot,
        order.AddressSnapshot, order.AddressReferencesSnapshot, order.CustomerNotes,
        order.CancellationReason, Utc(order.UpdatedAtUtc), Operations(order));
    private static CheckoutException Validation(string message) => new(CheckoutError.Validation, message);
    private static CheckoutException Conflict(string message) => new(CheckoutError.Conflict, message);
    private static CheckoutException Missing() => new(CheckoutError.NotFound, "No se encontro el pedido o la credencial no es valida.");
    private sealed record PricedCart(QuoteResponse Quote, IReadOnlyDictionary<Guid, Product> Products, Promotion? Promotion);
}
