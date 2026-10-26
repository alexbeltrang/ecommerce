import { Component, inject, signal, viewChild, type OnInit } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { ActivatedRoute, RouterLink } from '@angular/router'
import { Orders, type AdminOrder } from '../../services/orders'
import { Session } from '../../services/session'
import { OrderSummary } from '../checkout/order-summary'
import { OrderPaymentPanel } from './order-payment-panel'
import { OrderFulfillmentPanel } from './order-fulfillment-panel'
import { OrderReturnsPanel } from './order-returns-panel'
import { OrderContactPanel } from './order-contact-panel'

@Component({
  imports: [RouterLink, ReactiveFormsModule, OrderSummary, OrderPaymentPanel, OrderFulfillmentPanel, OrderReturnsPanel, OrderContactPanel],
  template: `
    <div class="section-title"><h1>Operación del pedido</h1><a routerLink="/admin/pedidos">Volver a pedidos</a></div>
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    @if (message()) { <p class="success" role="status">{{ message() }}</p> }
    <button class="secondary" (click)="load()" [disabled]="anyBusy() || loading()">Recargar pedido</button>
    @if (loading()) { <p role="status">Cargando pedido…</p> }
    @if (value(); as item) {
      <section class="panel">
        <h2>Datos privados de contacto y entrega</h2>
        <div class="form-grid">
          <div><strong>{{ item.customerName }}</strong><p>Teléfono: {{ item.phone }}</p><p>Correo: {{ item.email || 'No indicado' }}</p></div>
          <div><p>Municipio: {{ item.municipality }}</p>
            @if (item.address) { <p class="description">Dirección: {{ item.address }}</p> }
            @if (item.neighborhood) { <p>Barrio: {{ item.neighborhood }}</p> }
            @if (item.addressReferences) { <p class="description">Referencias: {{ item.addressReferences }}</p> }
          </div>
        </div>
        @if (item.notes) { <h3>Observaciones del cliente</h3><p class="description">{{ item.notes }}</p> }
        <p class="muted">Utiliza esta información solo para gestionar el pedido. No compartas capturas ni datos de contacto.</p>
      </section>
      <app-order-summary [order]="item.order" />
      @if (item.operations) {
        <app-order-payment-panel [value]="item" [blocked]="anyBusy()" (updated)="accept($event)" />
        <app-order-fulfillment-panel [value]="item" [blocked]="anyBusy()" (updated)="accept($event)" />
        <app-order-returns-panel [value]="item" [blocked]="anyBusy()" (updated)="accept($event)" />
        <app-order-contact-panel [value]="item" />
      } @else { <p class="error">La API no contiene el módulo de operación actualizado. Reinicia la depuración con F5.</p> }
      @if (item.order.status === 'PendingConfirmation' || item.order.status === 'Confirmed' || item.order.status === 'Preparing' || item.order.status === 'ReadyForDelivery') {
        <section class="panel"><h2>Acciones</h2>
          @if (item.order.status === 'PendingConfirmation') {
            <p>Confirma solo después de verificar la disponibilidad. Se descuentan todas las líneas de forma atómica; un faltante no descuenta ninguna.</p>
            <button (click)="confirm()" [disabled]="anyBusy() || loading()">Confirmar y descontar inventario</button>
          }
          @if (item.order.paymentStatus === 'Pending' || item.order.paymentStatus === 'Rejected' || item.order.paymentStatus === 'Paid') {
            <form [formGroup]="form" (ngSubmit)="cancel()">
              <label>Motivo de cancelación *<textarea formControlName="reason" maxlength="1000" rows="3" required></textarea>
                <small>Este motivo será visible para el cliente. No incluyas datos privados.</small></label>
              <button class="danger" [disabled]="anyBusy() || loading()">Cancelar pedido{{ item.order.status !== 'PendingConfirmation' ? ' y liberar inventario' : '' }}</button>
              @if (item.order.paymentStatus === 'Paid') { <p class="warning">Se creará un reembolso pendiente por el total, incluido el envío. Solo el administrador puede registrar que ya devolvió el dinero.</p> }
            </form>
          } @else { <p class="warning">Revisa y valida o rechaza el comprobante antes de cancelar.</p> }
        </section>
      }
    }
  `,
})
export class OrderDetail implements OnInit {
  private readonly orders = inject(Orders)
  private readonly session = inject(Session)
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!
  readonly value = signal<AdminOrder | null>(null)
  readonly busy = signal(false)
  readonly loading = signal(false)
  readonly error = signal('')
  readonly message = signal('')
  readonly payments = viewChild(OrderPaymentPanel)
  readonly fulfillment = viewChild(OrderFulfillmentPanel)
  readonly returns = viewChild(OrderReturnsPanel)
  readonly contact = viewChild(OrderContactPanel)
  readonly form = inject(FormBuilder).nonNullable.group({ reason: ['', [Validators.required, Validators.maxLength(1000)]] })
  ngOnInit(): void { void this.load() }
  anyBusy(): boolean { return this.busy() || !!this.payments()?.isBusy() || !!this.fulfillment()?.busy() || !!this.returns()?.busy() }
  private dirty(): boolean { return this.form.dirty || !!this.payments()?.hasChanges() || !!this.fulfillment()?.hasChanges() || !!this.returns()?.hasChanges() || !!this.contact()?.hasChanges() }
  canLeave(): boolean { return !this.session.user() || (!this.anyBusy() && (!this.dirty() || window.confirm('¿Salir y descartar los formularios sin guardar? Los intentos de devolución pendientes se conservan.'))) }
  accept(value: AdminOrder): void { this.value.set(value); this.message.set('Pedido actualizado. Revisa el estado y los saldos vigentes.') }
  async load(): Promise<void> {
    if (this.anyBusy() || this.loading()) return
    if (this.dirty() && !window.confirm('¿Recargar y descartar los borradores sin guardar?')) return
    this.loading.set(true)
    this.error.set('')
    this.value.set(null)
    try { this.value.set(await this.orders.adminOrder(this.id)) }
    catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.loading.set(false) }
  }
  confirm(): void {
    if (window.confirm('¿Confirmar este pedido y descontar sus unidades? Esto no marca el pago como realizado.')) void this.act('confirm')
  }
  cancel(): void {
    this.form.markAllAsTouched()
    if (this.form.invalid || !this.form.controls.reason.value.trim()) { this.error.set('Indica un motivo público de cancelación, de hasta 1000 caracteres.'); return }
    if (window.confirm('¿Cancelar el pedido? Si estaba confirmado, se repondrá su inventario.')) void this.act('cancel')
  }
  private async act(action: 'confirm' | 'cancel'): Promise<void> {
    const value = this.value()
    if (!value || this.anyBusy()) return
    this.busy.set(true)
    this.error.set('')
    this.message.set('')
    try {
      this.value.set(await this.orders.act(value.id, action, value.updatedAtUtc, this.form.controls.reason.value.trim()))
      this.form.reset()
      this.message.set(action === 'confirm' ? 'Pedido confirmado e inventario descontado. La confirmación no modifica el pago.' : 'Pedido cancelado. Inventario conciliado según el estado anterior.')
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
}
