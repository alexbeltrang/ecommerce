import { CurrencyPipe } from '@angular/common'
import { Component, inject, input, output, signal, type OnInit } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { uuidPattern } from '../../services/cart'
import { ApiError, errorMessage } from '../../services/http'
import { Orders, returnStatusLabels, type AdminOrder, type AdminReturn } from '../../services/orders'
import { Session } from '../../services/session'

const amountValidators = [Validators.required, Validators.min(0), Validators.max(9_000_000_000_000), Validators.pattern(/^\d+$/)]
const quantityValidators = [Validators.required, Validators.min(0), Validators.max(2147483647), Validators.pattern(/^\d+$/)]

@Component({
  selector: 'app-order-returns-panel',
  imports: [ReactiveFormsModule, CurrencyPipe],
  templateUrl: './order-returns-panel.html',
})
export class OrderReturnsPanel implements OnInit {
  readonly value = input.required<AdminOrder>()
  readonly blocked = input(false)
  readonly updated = output<AdminOrder>()
  readonly busy = signal(false)
  readonly error = signal('')
  readonly notice = signal('')
  readonly pending = signal(false)
  readonly storageReady = signal(false)
  readonly editing = signal<{ value: AdminReturn; action: 'receive' | 'refund' | 'reject' } | null>(null)
  readonly statuses = returnStatusLabels
  readonly session = inject(Session)
  private readonly orders = inject(Orders)
  private readonly fb = inject(FormBuilder)
  private requestId: string = crypto.randomUUID()
  private get storageKey(): string { return `MercadoCercano.ReturnIntent.${this.value().id}` }
  readonly createForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(1000)]],
    shippingRefundAmount: [0, amountValidators],
    items: this.fb.array<ReturnType<OrderReturnsPanel['lineForm']>>([]),
  })
  readonly receiveForm = this.fb.nonNullable.group({
    receiptConfirmed: [false, Validators.requiredTrue],
    items: this.fb.array<ReturnType<OrderReturnsPanel['receiveLine']>>([]),
  })
  readonly refundForm = this.fb.nonNullable.group({
    amount: [0, [Validators.required, Validators.min(1), Validators.max(9_000_000_000_000), Validators.pattern(/^\d+$/)]],
    reference: ['', [Validators.required, Validators.maxLength(160)]], refundCompleted: [false, Validators.requiredTrue],
  })
  readonly rejectForm = this.fb.nonNullable.group({ reason: ['', [Validators.required, Validators.maxLength(1000)]] })
  private lineForm(id: string) { return this.fb.nonNullable.group({ orderItemId: [id], quantity: [0, quantityValidators] }) }
  private receiveLine(id: string, max: number) {
    return this.fb.nonNullable.group({ returnItemId: [id], restockQuantity: [0, [...quantityValidators, Validators.max(max)]] })
  }
  ngOnInit(): void {
    for (const item of this.value().operations.lines) this.createForm.controls.items.push(this.lineForm(item.id))
    try {
      const stored = sessionStorage.getItem(this.storageKey)
      if (stored !== null) {
        if (!uuidPattern.test(stored)) throw new Error('El identificador guardado no es válido. Descártalo solo después de comprobar las solicitudes existentes.')
        this.requestId = stored
        this.pending.set(true)
      }
      this.storageReady.set(true)
    } catch (error) { this.error.set(errorMessage(error)) }
  }
  hasChanges(): boolean { return this.createForm.dirty || this.receiveForm.dirty || this.refundForm.dirty || this.rejectForm.dirty }
  remainingShipping(): number {
    return this.value().order.shippingCost - this.value().operations.returns.filter((item) => item.summary.status !== 'Rejected').reduce((sum, item) => sum + item.shippingRefundAmount, 0)
  }
  estimate(): number | null {
    const values = this.createForm.getRawValue()
    if (!Number.isSafeInteger(values.shippingRefundAmount) || values.shippingRefundAmount < 0) return null
    let total = BigInt(values.shippingRefundAmount)
    for (const line of values.items) {
      const item = this.value().operations.lines.find((item) => item.id === line.orderItemId)
      if (!item || !Number.isInteger(line.quantity) || line.quantity < 0 || line.quantity > item.returnableQuantity) return null
      if (this.value().order.paymentStatus === 'Paid' && line.quantity > 0)
        total += BigInt(item.refundableAmount) * BigInt(line.quantity) / BigInt(item.returnableQuantity)
    }
    return total <= 9_000_000_000_000n ? Number(total) : null
  }
  async create(): Promise<void> {
    if (this.busy() || this.blocked() || !this.storageReady()) return
    this.createForm.markAllAsTouched()
    const values = this.createForm.getRawValue()
    const items = values.items.filter((item) => item.quantity > 0)
    if (this.createForm.invalid || !values.reason.trim() || !items.length || this.estimate() === null || values.shippingRefundAmount > this.remainingShipping()) {
      this.error.set('Selecciona cantidades disponibles, motivo y un reembolso de envío dentro del saldo. El servidor determina el importe definitivo.')
      return
    }
    if (!window.confirm('¿Registrar esta solicitud? No repone inventario ni realiza un reembolso todavía.')) return
    this.busy.set(true)
    this.error.set('')
    try {
      sessionStorage.setItem(this.storageKey, this.requestId)
      this.pending.set(true)
      const value = this.value()
      const result = await this.orders.requestReturn(value.id, {
        updatedAtUtc: value.updatedAtUtc, requestId: this.requestId, reason: values.reason.trim(),
        shippingRefundAmount: values.shippingRefundAmount, items,
      })
      this.updated.emit(result)
      this.clearIntent()
      this.resetCreation()
      this.notice.set('Solicitud registrada. Recibe físicamente los productos antes de reponer o reembolsar.')
    } catch (error) { this.error.set(`${this.session.adminError(error)} Si la respuesta se perdió, recupera este mismo intento antes de crear otro.`) }
    finally { this.busy.set(false) }
  }
  async recover(): Promise<void> {
    if (this.busy() || this.blocked() || !this.pending()) return
    this.busy.set(true)
    this.error.set('')
    try {
      await this.orders.returnByRequest(this.value().id, this.requestId)
      const result = await this.orders.adminOrder(this.value().id)
      this.updated.emit(result)
      this.clearIntent()
      this.resetCreation()
      this.notice.set('Solicitud recuperada sin duplicarla.')
    } catch (error) {
      this.error.set(error instanceof ApiError && error.status === 404
        ? 'Todavía no se encontró la solicitud. Se conserva el identificador para reintentar; no lo descartes si el envío sigue en curso.'
        : this.session.adminError(error))
    } finally { this.busy.set(false) }
  }
  discard(): void {
    if (this.busy() || this.blocked() || !window.confirm('¿Descartar el intento? Comprueba antes las devoluciones registradas; perder la recuperación puede duplicar una solicitud.')) return
    try { this.clearIntent(); this.storageReady.set(true); this.notice.set('Intento descartado explícitamente.') }
    catch (error) { this.error.set(errorMessage(error)) }
  }
  private clearIntent(): void {
    sessionStorage.removeItem(this.storageKey)
    this.pending.set(false)
    this.requestId = crypto.randomUUID()
  }
  private resetCreation(): void {
    this.createForm.controls.reason.reset()
    this.createForm.controls.shippingRefundAmount.reset()
    for (const item of this.createForm.controls.items.controls) item.controls.quantity.reset()
    this.createForm.markAsPristine()
  }
  open(value: AdminReturn, action: 'receive' | 'refund' | 'reject'): void {
    if (this.busy() || this.blocked()) return
    if ((this.receiveForm.dirty || this.refundForm.dirty || this.rejectForm.dirty) && !window.confirm('¿Descartar el formulario de operación anterior?')) return
    this.receiveForm.reset()
    this.receiveForm.controls.items.clear()
    this.refundForm.reset({ amount: value.summary.refundAmount, reference: '', refundCompleted: false })
    this.rejectForm.reset()
    for (const item of value.items) this.receiveForm.controls.items.push(this.receiveLine(item.id, item.quantity))
    this.editing.set({ value, action })
    this.error.set('')
  }
  async apply(): Promise<void> {
    const editing = this.editing()
    if (!editing || this.busy() || this.blocked()) return
    const form = editing.action === 'receive' ? this.receiveForm : editing.action === 'refund' ? this.refundForm : this.rejectForm
    form.markAllAsTouched()
    if (form.invalid) { this.error.set('Completa el formulario y confirma la operación. Los importes y cantidades deben ser enteros.'); return }
    if (editing.action === 'reject' && !this.rejectForm.controls.reason.value.trim()) { this.error.set('Escribe el motivo público del rechazo.'); return }
    if (!window.confirm(editing.action === 'refund'
      ? '¿Registrar el dinero ya devuelto fuera del sistema? No se enviará dinero automáticamente.'
      : '¿Registrar esta operación? La recepción repone únicamente las unidades en buen estado indicadas.')) return
    this.busy.set(true)
    this.error.set('')
    try {
      const fields = editing.action === 'receive' ? this.receiveForm.getRawValue()
        : editing.action === 'refund' ? this.refundForm.getRawValue() : this.rejectForm.getRawValue()
      const result = await this.orders.returnAction(this.value().id, editing.value.summary.id, editing.action, { ...fields, updatedAtUtc: this.value().updatedAtUtc })
      this.updated.emit(result)
      this.receiveForm.markAsPristine()
      this.refundForm.markAsPristine()
      this.rejectForm.markAsPristine()
      this.editing.set(null)
      this.notice.set('Operación registrada y saldos actualizados. Los cambios de inventario no se repiten al reintentar.')
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
}
