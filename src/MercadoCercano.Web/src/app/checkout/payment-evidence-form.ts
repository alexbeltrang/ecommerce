import { Component, inject, input, output, signal } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { Orders, type AdminOrder, type Order } from '../../services/orders'
import { errorMessage } from '../../services/http'
import { Session } from '../../services/session'

@Component({
  selector: 'app-payment-evidence',
  imports: [ReactiveFormsModule],
  template: `
    <form class="panel" [formGroup]="form" (ngSubmit)="submit()">
      <fieldset [disabled]="busy() || blocked()">
        <h3>Adjuntar comprobante privado</h3>
        <p>Una imagen PNG, JPEG o WebP, hasta 750 KiB. No se admiten PDF ni SVG. Solo el personal autorizado puede descargarla.</p>
        @if (order().isTestOrder) { <p class="warning">Usa una imagen ficticia sin información bancaria ni datos personales reales.</p> }
        <label>Referencia del pago *<input formControlName="reference" maxlength="160" autocomplete="off" required /></label>
        <label>Comprobante *<input #upload type="file" accept="image/png,image/jpeg,image/webp" (change)="select(upload)" /></label>
        <p class="muted">Adjuntar no acredita el pago. El negocio debe verificar los fondos; no pagues dos veces mientras se revisa.</p>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <button [disabled]="busy() || blocked()">{{ busy() ? 'Guardando…' : 'Enviar a revisión' }}</button>
      </fieldset>
    </form>
  `,
})
export class PaymentEvidenceForm {
  readonly order = input.required<Order>()
  readonly token = input('')
  readonly adminId = input<string | null>(null)
  readonly blocked = input(false)
  readonly updated = output<Order>()
  readonly adminUpdated = output<AdminOrder>()
  private readonly orders = inject(Orders)
  private readonly session = inject(Session)
  readonly busy = signal(false)
  readonly error = signal('')
  readonly form = inject(FormBuilder).nonNullable.group({ reference: ['', [Validators.required, Validators.maxLength(160)]] })
  private file: File | null = null
  private upload: HTMLInputElement | null = null
  select(input: HTMLInputElement): void {
    this.error.set('')
    this.upload = input
    const file = input.files?.[0]
    this.file = null
    this.form.markAsDirty()
    if (!file) return
    if (file.size === 0 || file.size > 750 * 1024 || !['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) {
      this.error.set('Selecciona una imagen PNG/JPEG/WebP de 1 byte a 750 KiB.')
      input.value = ''
      return
    }
    this.file = file
  }
  async submit(): Promise<void> {
    if (this.busy() || this.blocked()) return
    this.form.markAllAsTouched()
    if (this.form.invalid || !this.form.controls.reference.value.trim() || !this.file) { this.error.set('Completa la referencia y selecciona un archivo válido.'); return }
    this.busy.set(true)
    this.error.set('')
    const data = new FormData()
    data.append('reference', this.form.controls.reference.value.trim())
    data.append('updatedAtUtc', this.order().updatedAtUtc)
    data.append('file', this.file)
    try {
      const id = this.adminId()
      if (id) this.adminUpdated.emit(await this.orders.submitEvidence(id, data))
      else {
        data.append('orderNumber', this.order().orderNumber)
        data.append('trackingToken', this.token())
        this.updated.emit(await this.orders.submitGuestEvidence(data))
      }
      this.form.reset()
      this.file = null
      if (this.upload) this.upload.value = ''
    } catch (error) { this.error.set(this.adminId() ? this.session.adminError(error) : errorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
