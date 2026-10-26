import { CurrencyPipe, DatePipe } from '@angular/common'
import { Component, input } from '@angular/core'
import { deliveryLabels, paymentLabels, paymentStatusLabels, returnStatusLabels, statusLabels, type Order } from '../../services/orders'

@Component({
  selector: 'app-order-summary',
  imports: [CurrencyPipe, DatePipe],
  template: `
    @let value = order();
    @if (value.isTestOrder) { <p class="warning">PEDIDO DE PRUEBA. Las tarifas e instrucciones son ficticias; no realices pagos.</p> }
    <section class="panel">
      <h2>Pedido {{ value.orderNumber }}</h2>
      <p>{{ value.createdAtUtc | date:'medium' }} · <strong>{{ statuses[value.status] }}</strong></p>
      <p>{{ deliveries[value.deliveryMethod] }} · {{ payments[value.paymentMethod] }}</p>
      <p>Estado del pago: <strong>{{ value.grandTotal === 0 ? 'Sin cobro' : paymentStatuses[value.paymentStatus] || value.paymentStatus }}</strong>. Registrar o confirmar el pedido no acredita un pago.</p>
      @if (value.grandTotal === 0) { <p class="success">El descuento cubre todo el importe. No realices pagos para este pedido.</p> }
      @if (value.paymentStatus === 'UnderReview') { <p class="warning">Comprobante en revisión. No realices otro pago mientras el negocio verifica los fondos.</p> }
      @if (value.paymentRejectionReason) { <p class="error">Motivo del rechazo del comprobante: {{ value.paymentRejectionReason }}</p> }
      @if (value.fulfillment; as shipment) {
        @if (shipment.carrierName || shipment.trackingNumber) { <p>Transportadora / responsable: {{ shipment.carrierName || 'Coordinado por el negocio' }} · Guía: {{ shipment.trackingNumber || 'No aplica' }}</p> }
        @if (shipment.shippedAtUtc) { <p>Despacho: {{ shipment.shippedAtUtc | date:'medium' }}</p> }
        @if (shipment.deliveredAtUtc) { <p>Entrega confirmada: {{ shipment.deliveredAtUtc | date:'medium' }}</p> }
      }
      <div class="table-scroll"><table>
        <thead><tr><th>Producto</th><th>Cantidad</th><th>Precio COP antes del beneficio adicional</th><th>Beneficio adicional COP</th><th>Total neto COP</th></tr></thead>
        <tbody>@for (item of value.items; track $index) {
          <tr><td>{{ item.name }}<small>{{ item.sku }}</small></td><td>{{ item.quantity }}</td>
            <td>{{ item.unitPrice | currency:'COP':'symbol-narrow':'1.0-0' }}</td>
            <td>− {{ item.promotionDiscountTotal | currency:'COP':'symbol-narrow':'1.0-0' }}</td>
            <td>{{ item.lineTotal - item.promotionDiscountTotal | currency:'COP':'symbol-narrow':'1.0-0' }}</td></tr>
        }</tbody>
      </table></div>
      <dl class="totals">
        <dt>Subtotal antes de promociones</dt><dd>{{ value.subtotal | currency:'COP':'symbol-narrow':'1.0-0' }}</dd>
        <dt>Ofertas de productos</dt><dd>− {{ value.discountTotal - (value.promotion?.discountTotal ?? 0) | currency:'COP':'symbol-narrow':'1.0-0' }}</dd>
        @if (value.promotion; as benefit) {
          <dt>{{ benefit.name }} {{ benefit.code ? '(' + benefit.code + ')' : '(automática)' }}</dt>
          <dd>− {{ benefit.discountTotal | currency:'COP':'symbol-narrow':'1.0-0' }}</dd>
        }
        <dt>Entrega</dt><dd>{{ value.shippingCost | currency:'COP':'symbol-narrow':'1.0-0' }}</dd>
        <dt><strong>Total COP</strong></dt><dd><strong>{{ value.grandTotal | currency:'COP':'symbol-narrow':'1.0-0' }}</strong></dd>
        @if (value.refundedTotal > 0) { <dt>Reembolsado</dt><dd>{{ value.refundedTotal | currency:'COP':'symbol-narrow':'1.0-0' }}</dd> }
      </dl>
      @if (value.status === 'PendingConfirmation') { <p class="warning">Recibido, pendiente de confirmación del negocio. Todavía no se ha reservado ni descontado inventario.</p> }
      @if (value.status === 'Cancelled') { <p class="warning">Pedido cancelado. No realices pagos para este pedido.</p> }
      @if (value.pickupInstructions) { <h3>Recogida</h3><p class="description">{{ value.pickupInstructions }}</p> }
      @if (value.paymentInstructions && value.status !== 'Cancelled' && (value.paymentStatus === 'Pending' || value.paymentStatus === 'Rejected')) { <h3>Instrucciones de pago</h3><p class="description">{{ value.paymentInstructions }}</p> }
      @if (value.termsText) { <details><summary>Condiciones aceptadas al comprar</summary><p class="description">{{ value.termsText }}</p></details> }
    </section>
    @if (value.returns && value.returns.length) {
      <section class="panel"><h2>Devoluciones y reembolsos</h2>
        @for (item of value.returns; track item.id) {
          <article><h3>{{ item.kind === 'Cancellation' ? 'Reembolso por cancelación' : item.kind === 'FailedDelivery' ? 'Despacho no entregado' : 'Devolución de productos' }}</h3>
            <p><strong>{{ returns[item.status] }}</strong> · {{ item.createdAtUtc | date:'medium' }}</p>
            <p class="description">{{ item.reason }}</p>
            @if (item.rejectionReason) { <p class="error">{{ item.rejectionReason }}</p> }
            <p>Importe a reembolsar: {{ item.refundAmount | currency:'COP':'symbol-narrow':'1.0-0' }}</p>
            @if (item.status === 'Received' && item.refundAmount > 0) { <p class="warning">Recepción registrada; reembolso pendiente de ejecución por el negocio.</p> }
            @if (item.status === 'Received' && item.refundAmount === 0) { <p>Esta solicitud no tiene importe pendiente por reembolsar.</p> }
            @if (item.refundedAtUtc) { <p>Reembolso registrado: {{ item.refundedAtUtc | date:'medium' }}</p> }
          </article>
        }
      </section>
    }
    <section class="panel"><h2>Historial del pedido</h2>
      <ol class="order-history">@for (entry of value.history; track $index) {
        <li><strong>{{ statuses[entry.status] }}</strong> · {{ entry.createdAtUtc | date:'medium' }}
          <p class="description">{{ entry.reason }}</p></li>
      }</ol>
    </section>
  `,
})
export class OrderSummary {
  readonly order = input.required<Order>()
  readonly deliveries = deliveryLabels
  readonly payments = paymentLabels
  readonly statuses = statusLabels
  readonly paymentStatuses = paymentStatusLabels
  readonly returns = returnStatusLabels
}
