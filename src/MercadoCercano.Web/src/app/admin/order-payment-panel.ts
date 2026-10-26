import { CurrencyPipe, DatePipe } from '@angular/common'
import { Component, effect, inject, input, output, signal, viewChild } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { downloadRequest } from '../../services/http'
import { Orders, type AdminOrder } from '../../services/orders'
import { Session } from '../../services/session'
import { PaymentEvidenceForm } from '../checkout/payment-evidence-form'

@Component({
  selector: 'app-order-payment-panel',
  imports: [ReactiveFormsModule, CurrencyPipe, DatePipe, PaymentEvidenceForm],
  template: `
    <section class="panel"><h2>Conciliación manual del pago</h2>
      @if (value().order.grandTotal === 0) {
        <p class="success">Sin cobro: no se requieren fondos ni comprobante. El estado de pago está resuelto por el descuento, no por una transferencia recibida.</p>
      }
      <p>Importe original: <strong>{{ value().order.grandTotal | currency:'COP':'symbol-narrow':'1.0-0' }} COP</strong>.
        El sistema registra la verificación; no consulta bancos ni mueve dinero.</p>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <p>Referencia privada: {{ value().operations.paymentReference || 'No registrada' }}</p>
      @if (value().operations.evidence.length) {
        <h3>Comprobantes privados</h3><ul>
          @for (item of value().operations.evidence; track item.id) {
            <li>{{ item.createdAtUtc | date:'medium' }} · {{ item.byteLength }} bytes
              <button class="secondary" type="button" (click)="download(item.id)" [disabled]="busy() || blocked()">Descargar comprobante</button></li>
          }
        </ul><p class="muted">Comprueba la recepción de los fondos por un canal confiable. Una imagen no demuestra por sí sola un pago.</p>
      }
      @if (value().order.status !== 'Cancelled' && (value().order.paymentStatus === 'UnderReview' ||
          (value().order.paymentMethod === 'CashOnDelivery' && value().order.paymentStatus === 'Pending' &&
          (value().order.status === 'ReadyForDelivery' || value().order.status === 'Shipped')))) {
        <form [formGroup]="approveForm" (ngSubmit)="approve()">
          <fieldset [disabled]="busy() || blocked()">
            <label>Importe efectivamente recibido COP *<input type="number" formControlName="amount" min="1" max="9000000000000" step="1" required /></label>
            <label>Referencia bancaria / recibo<input formControlName="reference" maxlength="160" autocomplete="off" /></label>
            <label class="checkbox"><input type="checkbox" formControlName="fundsVerified" /> Verifiqué los fondos y el importe total. No estoy validando solo una imagen. *</label>
            <button>Registrar pago recibido</button>
          </fieldset>
        </form>
        @if (value().order.paymentStatus === 'UnderReview') {
          <form [formGroup]="rejectForm" (ngSubmit)="reject()">
            <fieldset [disabled]="busy() || blocked()">
              <label>Motivo de rechazo visible para el cliente *<textarea formControlName="reason" maxlength="1000" required></textarea></label>
              <button class="danger">Rechazar comprobante</button>
            </fieldset>
          </form>
        }
      }
      @if (value().order.paymentMethod === 'CashOnDelivery' && value().order.paymentStatus === 'Pending') {
        <p class="warning">Registra el cobro cuando el pedido esté listo o en ruta. No se puede marcar entregado sin confirmar el dinero recibido.</p>
      }
    </section>
    @if (value().order.status !== 'Cancelled' && value().order.paymentMethod !== 'CashOnDelivery' &&
        (value().order.paymentStatus === 'Pending' || value().order.paymentStatus === 'Rejected')) {
      <app-payment-evidence [order]="value().order" [adminId]="value().id" [blocked]="blocked() || busy()" (adminUpdated)="updated.emit($event)" />
    }
  `,
})
export class OrderPaymentPanel {
  readonly value = input.required<AdminOrder>()
  readonly blocked = input(false)
  readonly updated = output<AdminOrder>()
  readonly busy = signal(false)
  readonly error = signal('')
  readonly evidence = viewChild(PaymentEvidenceForm)
  private readonly orders = inject(Orders)
  private readonly session = inject(Session)
  private readonly fb = inject(FormBuilder)
  readonly approveForm = this.fb.nonNullable.group({
    amount: [0, [Validators.required, Validators.min(1), Validators.max(9_000_000_000_000), Validators.pattern(/^\d+$/)]],
    reference: ['', Validators.maxLength(160)], fundsVerified: [false, Validators.requiredTrue],
  })
  readonly rejectForm = this.fb.nonNullable.group({ reason: ['', [Validators.required, Validators.maxLength(1000)]] })
  constructor() {
    effect(() => {
      const value = this.value()
      if (this.approveForm.controls.amount.pristine) this.approveForm.controls.amount.setValue(value.order.grandTotal)
      if (this.approveForm.controls.reference.pristine) this.approveForm.controls.reference.setValue(value.operations.paymentReference ?? '')
    })
  }
  isBusy(): boolean { return this.busy() || !!this.evidence()?.busy() }
  hasChanges(): boolean { return this.approveForm.dirty || this.rejectForm.dirty || !!this.evidence()?.form.dirty }
  async approve(): Promise<void> {
    this.approveForm.markAllAsTouched()
    const values = this.approveForm.getRawValue()
    if (this.approveForm.invalid || values.amount !== this.value().order.grandTotal ||
        (this.value().order.paymentMethod !== 'CashOnDelivery' && !values.reference.trim())) {
      this.error.set('Verifica el total exacto, la referencia de transferencia/billetera y la confirmación de fondos.')
      return
    }
    if (!window.confirm('¿Registrar este dinero como recibido? No se realizará ningún cobro automático.')) return
    await this.review(true, { ...values, reference: values.reference.trim() })
  }
  async reject(): Promise<void> {
    this.rejectForm.markAllAsTouched()
    if (this.rejectForm.invalid || !this.rejectForm.controls.reason.value.trim()) { this.error.set('Indica un motivo público de hasta 1000 caracteres.'); return }
    await this.review(false, { reason: this.rejectForm.controls.reason.value.trim() })
  }
  private async review(approve: boolean, values: { amount?: number; reference?: string; fundsVerified?: boolean; reason?: string }): Promise<void> {
    if (this.isBusy() || this.blocked()) return
    this.busy.set(true)
    this.error.set('')
    try {
      const value = this.value()
      const result = await this.orders.reviewPayment(value.id, approve, { ...values, updatedAtUtc: value.updatedAtUtc })
      this.approveForm.markAsPristine()
      this.rejectForm.reset()
      this.updated.emit(result)
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
  async download(id: string): Promise<void> {
    if (this.isBusy() || this.blocked()) return
    this.busy.set(true)
    this.error.set('')
    try { await downloadRequest(`/api/v1/admin/orders/${this.value().id}/payment/evidence/${id}`, `comprobante-${id}`) }
    catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
}
