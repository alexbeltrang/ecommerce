import { Component, inject, input, output, signal } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { Orders, type AdminOrder } from '../../services/orders'
import { Session } from '../../services/session'

@Component({
  selector: 'app-order-fulfillment-panel',
  imports: [ReactiveFormsModule],
  template: `
    <section class="panel"><h2>Preparación y entrega</h2>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (value().order.status === 'Confirmed') {
        @if (value().order.paymentMethod === 'CashOnDelivery' || value().order.paymentStatus === 'Paid') {
          <button (click)="run('prepare')" [disabled]="busy() || blocked()">Iniciar preparación</button>
        } @else { <p class="warning">Primero valida la transferencia o el pago por billetera.</p> }
      }
      @if (value().order.status === 'Preparing') { <button (click)="run('ready')" [disabled]="busy() || blocked()">Marcar listo para entrega</button> }
      @if (value().order.status === 'ReadyForDelivery' && value().order.deliveryMethod !== 'CoordinatedPickup') {
        <form [formGroup]="dispatchForm" (ngSubmit)="ship()">
          <fieldset [disabled]="busy() || blocked()">
            <label>Transportadora / responsable<input formControlName="carrierName" maxlength="120" /></label>
            <label>Número de guía / referencia de ruta<input formControlName="trackingNumber" maxlength="120" /></label>
            <p class="muted">Ambos son obligatorios para transportadora. Son visibles en el seguimiento; no escribas datos personales.</p>
            <button>Registrar despacho / salida a ruta</button>
          </fieldset>
        </form>
      }
      @if ((value().order.deliveryMethod === 'CoordinatedPickup' && value().order.status === 'ReadyForDelivery') || value().order.status === 'Shipped') {
        <form [formGroup]="deliveryForm" (ngSubmit)="deliver()">
          <fieldset [disabled]="busy() || blocked()">
            <label class="checkbox"><input type="checkbox" formControlName="deliveryConfirmed" /> Confirmo que el cliente recibió el pedido. *</label>
            <button [disabled]="value().order.paymentStatus !== 'Paid'">Confirmar entrega</button>
            @if (value().order.paymentStatus !== 'Paid') { <p class="warning">Registra primero el cobro contraentrega en la sección de pago.</p> }
          </fieldset>
        </form>
      }
      @if (value().order.status === 'PendingConfirmation') { <p>Confirma primero el pedido y sus existencias.</p> }
      @if (value().order.status === 'Delivered' || value().order.status === 'Cancelled') { <p>Este pedido ya terminó su recorrido operativo.</p> }
    </section>
  `,
})
export class OrderFulfillmentPanel {
  readonly value = input.required<AdminOrder>()
  readonly blocked = input(false)
  readonly updated = output<AdminOrder>()
  readonly busy = signal(false)
  readonly error = signal('')
  private readonly orders = inject(Orders)
  private readonly session = inject(Session)
  private readonly fb = inject(FormBuilder)
  readonly dispatchForm = this.fb.nonNullable.group({
    carrierName: ['', Validators.maxLength(120)], trackingNumber: ['', Validators.maxLength(120)],
  })
  readonly deliveryForm = this.fb.nonNullable.group({ deliveryConfirmed: [false, Validators.requiredTrue] })
  hasChanges(): boolean { return this.dispatchForm.dirty || this.deliveryForm.dirty }
  async ship(): Promise<void> {
    this.dispatchForm.markAllAsTouched()
    const values = this.dispatchForm.getRawValue()
    if (this.dispatchForm.invalid || (this.value().order.deliveryMethod === 'CarrierShipping' && (!values.carrierName.trim() || !values.trackingNumber.trim()))) {
      this.error.set('Indica transportadora y guía, de hasta 120 caracteres cada una.')
      return
    }
    await this.run('ship')
  }
  async deliver(): Promise<void> {
    this.deliveryForm.markAllAsTouched()
    if (this.deliveryForm.invalid) { this.error.set('Confirma la recepción del cliente antes de marcar entregado.'); return }
    await this.run('deliver')
  }
  async run(action: 'prepare' | 'ready' | 'ship' | 'deliver'): Promise<void> {
    if (this.busy() || this.blocked()) return
    if (!window.confirm('¿Registrar esta etapa del pedido? El historial quedará visible para el cliente.')) return
    this.busy.set(true)
    this.error.set('')
    try {
      const value = this.value()
      const fields = this.dispatchForm.getRawValue()
      const result = await this.orders.fulfill(value.id, action, {
        updatedAtUtc: value.updatedAtUtc, carrierName: fields.carrierName.trim(), trackingNumber: fields.trackingNumber.trim(),
        deliveryConfirmed: this.deliveryForm.controls.deliveryConfirmed.value,
      })
      this.dispatchForm.reset()
      this.deliveryForm.reset()
      this.updated.emit(result)
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
}
