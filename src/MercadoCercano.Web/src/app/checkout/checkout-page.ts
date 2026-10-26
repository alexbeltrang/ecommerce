import { CurrencyPipe } from '@angular/common'
import { Component, effect, inject, signal, type OnInit } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { Router, RouterLink } from '@angular/router'
import { Cart } from '../../services/cart'
import { CheckoutIntentStore, type CheckoutIntent } from '../../services/checkout-intent'
import { ApiError, errorMessage } from '../../services/http'
import { Orders, paymentLabels, type CheckoutSettings, type DeliveryMethod, type Order, type PaymentMethod, type Quote, type QuoteSelection } from '../../services/orders'

@Component({
  imports: [ReactiveFormsModule, RouterLink, CurrencyPipe],
  templateUrl: './checkout-page.html',
})
export class CheckoutPage implements OnInit {
  readonly cart = inject(Cart)
  readonly intents = inject(CheckoutIntentStore)
  private readonly orders = inject(Orders)
  private readonly router = inject(Router)
  readonly settings = signal<CheckoutSettings | null>(null)
  readonly quote = signal<Quote | null>(null)
  readonly busy = signal(false)
  readonly loading = signal(true)
  readonly error = signal('')
  readonly notice = signal('')
  readonly created = signal<Order | null>(null)
  readonly receiptUrl = signal('')
  readonly payments = paymentLabels
  private quotedCart = ''
  private completed = false
  readonly form = inject(FormBuilder).nonNullable.group({
    customerName: ['', [Validators.required, Validators.maxLength(160)]],
    phone: ['', [Validators.required, Validators.maxLength(32), Validators.pattern(/^[+0-9 ()-]+$/)]],
    email: ['', [Validators.email, Validators.maxLength(254)]],
    deliveryMethod: ['CoordinatedPickup' as DeliveryMethod, Validators.required],
    paymentMethod: ['BankTransfer' as PaymentMethod, Validators.required],
    municipality: ['', Validators.maxLength(120)],
    neighborhood: ['', Validators.maxLength(120)],
    address: ['', Validators.maxLength(300)],
    addressReferences: ['', Validators.maxLength(500)],
    notes: ['', Validators.maxLength(2000)],
    couponCode: ['', [Validators.maxLength(80), Validators.pattern(/^\s*[A-Za-z0-9][A-Za-z0-9_-]{2,79}\s*$/)]],
    termsAccepted: [false, Validators.requiredTrue],
  })

  constructor() {
    effect(() => {
      const signature = this.cart.signature()
      if (!this.completed && this.quote() && signature !== this.quotedCart) {
        this.selectionChanged()
        this.notice.set('El carrito cambió. Revisa de nuevo los totales antes de enviar.')
      }
    })
  }

  ngOnInit(): void { void this.load() }
  canLeave(): boolean {
    return !this.busy() && (this.completed || !this.form.dirty ||
      window.confirm('¿Salir del checkout? Los datos del formulario no se guardan. El carrito y el intento pendiente se conservan.'))
  }
  selectionChanged(): void {
    this.quote.set(null)
    this.form.controls.termsAccepted.setValue(false)
    const requiresAddress = this.form.controls.deliveryMethod.value !== 'CoordinatedPickup'
    this.form.controls.address.setValidators([Validators.maxLength(300), ...(requiresAddress ? [Validators.required] : [])])
    this.form.controls.municipality.setValidators([Validators.maxLength(120), ...(requiresAddress ? [Validators.required] : [])])
    this.form.controls.address.updateValueAndValidity()
    this.form.controls.municipality.updateValueAndValidity()
  }
  deliveryChanged(): void {
    this.form.controls.municipality.setValue('')
    this.selectionChanged()
  }
  async load(): Promise<void> {
    this.loading.set(true)
    this.error.set('')
    try {
      const { values } = await this.orders.settings()
      this.settings.set(values)
      this.form.controls.deliveryMethod.setValue(values.pickupEnabled ? 'CoordinatedPickup' : values.localDeliveryEnabled ? 'LocalDelivery' : 'CarrierShipping')
      const payment = values.paymentMethods[0]
      if (!payment) throw new Error('La tienda no tiene métodos de pago habilitados.')
      this.form.controls.paymentMethod.setValue(payment)
      this.selectionChanged()
    } catch (error) { this.error.set(errorMessage(error)) }
    finally { this.loading.set(false) }
  }
  private validForm(includeTerms: boolean): boolean {
    for (const [name, control] of Object.entries(this.form.controls)) {
      if (includeTerms || name !== 'termsAccepted') control.markAsTouched()
    }
    const phoneDigits = this.form.controls.phone.value.replace(/\D/g, '').length
    const invalid = Object.entries(this.form.controls).some(([name, control]) => (includeTerms || name !== 'termsAccepted') && control.invalid)
    if (invalid || !this.form.controls.customerName.value.trim() || phoneDigits < 7 || phoneDigits > 15 ||
        (this.form.controls.deliveryMethod.value !== 'CoordinatedPickup' &&
          (!this.form.controls.address.value.trim() || !this.form.controls.municipality.value.trim()))) {
      this.error.set('Revisa los campos obligatorios, teléfono (7–15 dígitos) y cupón opcional (3–80 letras ASCII, números, guiones o guion bajo). Acepta las condiciones para enviar.')
      return false
    }
    return true
  }
  private selection(): QuoteSelection {
    const values = this.form.getRawValue()
    return {
      items: this.cart.items().map((item) => ({ ...item })),
      deliveryMethod: values.deliveryMethod, paymentMethod: values.paymentMethod,
      municipality: values.deliveryMethod === 'CoordinatedPickup' ? null : values.municipality.trim(),
      couponCode: values.couponCode.trim().toUpperCase() || null,
    }
  }
  async review(): Promise<void> {
    if (this.completed || this.busy() || !this.cart.ready() || !this.cart.items().length || !this.settings() || !this.validForm(false)) return
    this.busy.set(true)
    this.error.set('')
    this.quote.set(null)
    this.form.controls.termsAccepted.setValue(false)
    const signature = this.cart.signature()
    try {
      const quote = await this.orders.quote(this.selection())
      if (signature !== this.cart.signature()) throw new Error('El carrito cambió durante la consulta. Recalcula el resumen.')
      this.quotedCart = signature
      this.quote.set(quote)
    } catch (error) { this.error.set(errorMessage(error)) }
    finally { this.busy.set(false) }
  }
  async submit(): Promise<void> {
    const quote = this.quote()
    if (this.completed || this.busy() || !quote || !this.validForm(true)) return
    this.error.set('')
    if (this.cart.signature() !== this.quotedCart) { this.selectionChanged(); this.error.set('El carrito cambió. Recalcula el resumen.'); return }
    this.busy.set(true)
    try {
      const intent = this.intents.ensure(this.quotedCart)
      const values = this.form.getRawValue()
      const pickup = values.deliveryMethod === 'CoordinatedPickup'
      const order = await this.orders.create({
        selection: this.selection(), quoteFingerprint: quote.fingerprint, idempotencyKey: intent.idempotencyKey, trackingToken: intent.trackingToken,
        customerName: values.customerName.trim(), phone: values.phone.trim(), email: values.email.trim() || null,
        neighborhood: pickup ? null : values.neighborhood.trim() || null, address: pickup ? null : values.address.trim(),
        addressReferences: pickup ? null : values.addressReferences.trim() || null, notes: values.notes.trim() || null,
        termsAccepted: values.termsAccepted,
      })
      this.busy.set(false)
      await this.finish(order, intent)
    } catch (error) {
      this.error.set(`${errorMessage(error)} No inicies otro intento si la respuesta se perdió: usa «Recuperar pedido» o reintenta con el mismo identificador.`)
    } finally { this.busy.set(false) }
  }
  async recover(): Promise<void> {
    const intent = this.intents.pending()
    if (this.completed || !intent || this.busy()) return
    this.busy.set(true)
    this.error.set('')
    try {
      const order = await this.orders.recover(intent.idempotencyKey, intent.trackingToken)
      this.busy.set(false)
      await this.finish(order, intent)
    } catch (error) {
      this.error.set(error instanceof ApiError && error.status === 404
        ? 'No se encontró un pedido con este intento. Se conserva el mismo identificador para reintentar; no lo descartes si todavía se está enviando.'
        : errorMessage(error))
    } finally { this.busy.set(false) }
  }
  discardIntent(): void {
    if (!window.confirm('Solo descarta el intento si comprobaste que no creó un pedido. Perderás la recuperación automática y podrías duplicar la compra. ¿Continuar?')) return
    try { this.intents.clear(); this.notice.set('Intento descartado explícitamente. El próximo envío tendrá un identificador nuevo.') }
    catch (error) { this.error.set(errorMessage(error)) }
  }
  private async finish(order: Order, intent: CheckoutIntent): Promise<void> {
    this.completed = true
    this.created.set(order)
    const path = `/pedido/${encodeURIComponent(order.orderNumber)}`
    this.receiptUrl.set(`${location.origin}${path}#${intent.trackingToken}`)
    if (!this.cart.clearIfUnchanged(intent.cartSignature)) {
      this.notice.set('El pedido se registró, pero no se pudo actualizar el carrito. Guarda el enlace privado; se conserva el intento para recuperarlo sin duplicar la compra.')
      return
    }
    try { this.intents.clear() }
    catch (error) { this.notice.set(errorMessage(error)); return }
    await this.router.navigate([path], { fragment: intent.trackingToken })
  }
}
