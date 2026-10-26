import { Component, inject, input, signal } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { errorMessage } from '../../services/http'
import { statusLabels, type AdminOrder } from '../../services/orders'

@Component({
  selector: 'app-order-contact-panel',
  imports: [ReactiveFormsModule],
  template: `
    <section class="panel"><h2>Mensaje para envío manual</h2>
      <p>No se envía automáticamente ni se marca como entregado. Revisa el destinatario y el contenido antes de abrir una aplicación externa.</p>
      <form [formGroup]="form">
        <label>Texto del mensaje<textarea formControlName="message" maxlength="2000" rows="6"></textarea></label>
      </form>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="success" role="status">{{ notice() }}</p> }
      <div class="actions">
        <button class="secondary" (click)="prepare()">Preparar con el estado actual</button>
        <button class="secondary" (click)="copy()" [disabled]="form.invalid">Copiar texto</button>
        @if (whatsappUrl()) { <a class="button" [href]="whatsappUrl()" target="_blank" rel="noopener noreferrer">Abrir WhatsApp (externo)</a> }
        @if (value().email && form.valid) { <a class="button" [href]="emailUrl()">Abrir correo</a> }
      </div>
      <p class="muted">WhatsApp usa indicativo +57 cuando el número colombiano tiene 10 dígitos. No incluye el código privado de seguimiento.</p>
    </section>
  `,
})
export class OrderContactPanel {
  readonly value = input.required<AdminOrder>()
  readonly error = signal('')
  readonly notice = signal('')
  readonly form = inject(FormBuilder).nonNullable.group({ message: ['', [Validators.required, Validators.maxLength(2000)]] })
  hasChanges(): boolean { return this.form.dirty }
  prepare(): void {
    if (this.form.dirty && !window.confirm('¿Reemplazar el borrador del mensaje?')) return
    const value = this.value()
    const guide = value.order.fulfillment.trackingNumber
    this.form.reset({ message: `${value.order.isTestOrder ? '[PRUEBA: NO REALIZAR PAGOS] ' : ''}Hola ${value.customerName}. Tu pedido ${value.order.orderNumber} está ${statusLabels[value.order.status].toLowerCase()}.${guide ? ` Guía: ${guide}.` : ''} Conserva tu enlace privado de seguimiento para consultar el resumen. Gracias por comprar en Mercado Cercano.` })
    this.notice.set('Texto preparado. Aún no se envió ningún mensaje.')
  }
  whatsappUrl(): string | null {
    if (this.form.invalid) return null
    let phone = this.value().phone.replace(/\D/g, '')
    if (phone.length === 10) phone = '57' + phone
    if (phone.length < 11 || phone.length > 15) return null
    return `https://wa.me/${phone}?text=${encodeURIComponent(this.form.controls.message.value)}`
  }
  emailUrl(): string {
    return `mailto:${encodeURIComponent(this.value().email ?? '')}?subject=${encodeURIComponent(`Pedido ${this.value().order.orderNumber}`)}&body=${encodeURIComponent(this.form.controls.message.value)}`
  }
  async copy(): Promise<void> {
    if (this.form.invalid) return
    this.error.set('')
    try { await navigator.clipboard.writeText(this.form.controls.message.value); this.notice.set('Texto copiado; falta enviarlo manualmente.') }
    catch (error) { this.error.set(`${errorMessage(error)} Selecciona y copia el texto manualmente si el navegador bloquea el portapapeles.`) }
  }
}
