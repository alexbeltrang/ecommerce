import { Component, inject, signal, viewChild, type OnInit } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { ActivatedRoute, RouterLink } from '@angular/router'
import { errorMessage } from '../../services/http'
import { Orders, type Order } from '../../services/orders'
import { OrderSummary } from './order-summary'
import { PaymentEvidenceForm } from './payment-evidence-form'

@Component({
  imports: [ReactiveFormsModule, RouterLink, OrderSummary, PaymentEvidenceForm],
  template: `
    <div class="section-title"><h1>Seguimiento del pedido</h1><a routerLink="/">Volver al catálogo</a></div>
    <section class="panel">
      <p>Consulta con el número del pedido y su código privado. No uses tu teléfono ni tu contraseña administrativa.</p>
      <p class="warning">Guarda el enlace o el código antes de cerrar esta página. Quien tenga el enlace podrá consultar el resumen del pedido. No contiene datos de contacto.</p>
      <form class="form-grid" [formGroup]="form" (ngSubmit)="lookup()">
        <label>Número de pedido<input formControlName="orderNumber" maxlength="40" autocomplete="off" required /></label>
        <label>Código privado<input formControlName="trackingToken" type="password" maxlength="43" autocomplete="off" required /></label>
        <div class="actions"><button [disabled]="busy() || evidence()?.busy()">{{ busy() ? 'Consultando…' : 'Consultar / actualizar' }}</button></div>
      </form>
      @if (shareUrl()) {
        <details><summary>Mostrar enlace privado para guardar</summary><a class="secret-link" [href]="shareUrl()">{{ shareUrl() }}</a></details>
      }
    </section>
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    @if (result(); as order) {
      <app-order-summary [order]="order" />
      @if (order.updatedAtUtc && order.status !== 'Cancelled' && order.paymentMethod !== 'CashOnDelivery' &&
        (order.paymentStatus === 'Pending' || order.paymentStatus === 'Rejected')) {
        <app-payment-evidence [order]="order" [token]="form.controls.trackingToken.value" (updated)="result.set($event)" />
      }
    }
  `,
})
export class TrackingPage implements OnInit {
  private readonly orders = inject(Orders)
  private readonly route = inject(ActivatedRoute)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly result = signal<Order | null>(null)
  readonly shareUrl = signal('')
  readonly evidence = viewChild(PaymentEvidenceForm)
  canLeave(): boolean { return !this.evidence()?.busy() && (!this.evidence()?.form.dirty || window.confirm('¿Salir y descartar el comprobante sin enviar?')) }
  readonly form = inject(FormBuilder).nonNullable.group({
    orderNumber: ['', [Validators.required, Validators.maxLength(40)]],
    trackingToken: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9_-]{43}$/)]],
  })
  ngOnInit(): void {
    const number = this.route.snapshot.paramMap.get('number')
    const token = this.route.snapshot.fragment
    if (number) this.form.controls.orderNumber.setValue(number)
    if (token) this.form.controls.trackingToken.setValue(token)
    if (number && token) void this.lookup()
  }
  async lookup(): Promise<void> {
    if (this.busy()) return
    if (!this.canLeave()) return
    this.form.markAllAsTouched()
    if (this.form.invalid) { this.error.set('Indica el número de pedido y el código privado de 43 caracteres.'); return }
    this.busy.set(true)
    this.error.set('')
    this.result.set(null)
    this.shareUrl.set('')
    try {
      const { orderNumber, trackingToken } = this.form.getRawValue()
      const order = await this.orders.track(orderNumber.trim(), trackingToken)
      this.result.set(order)
      this.shareUrl.set(`${location.origin}/pedido/${encodeURIComponent(order.orderNumber)}#${trackingToken}`)
    } catch (error) { this.error.set(errorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
