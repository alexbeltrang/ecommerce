import { CurrencyPipe, DatePipe } from '@angular/common'
import { Component, inject, signal, type OnInit } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import type { Page } from '../../services/catalog'
import { Orders, deliveryLabels, paymentStatusLabels, statusLabels, type AdminOrder, type DeliveryMethod, type OrderStatus, type PaymentStatus } from '../../services/orders'
import { Session } from '../../services/session'

@Component({
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe],
  template: `
    <h1>Pedidos</h1>
    <form class="filters panel" (ngSubmit)="search()">
      <label>Buscar por número, cliente o teléfono<input name="search" [(ngModel)]="term" maxlength="100" /></label>
      <label>Estado<select name="status" [(ngModel)]="status">
        <option value="">Todos</option>@for (value of statusOptions; track value) { <option [value]="value">{{ statuses[value] }}</option> }
      </select></label>
      <label>Pago<select name="paymentStatus" [(ngModel)]="paymentStatus">
        <option value="">Todos</option>@for (value of paymentOptions; track value) { <option [value]="value">{{ payments[value] }}</option> }
      </select></label>
      <label>Entrega<select name="deliveryMethod" [(ngModel)]="deliveryMethod">
        <option value="">Todas</option>@for (value of deliveryOptions; track value) { <option [value]="value">{{ deliveries[value] }}</option> }
      </select></label>
      <label class="checkbox"><input name="returnsPending" type="checkbox" [(ngModel)]="returnsPending" /> Recepciones pendientes</label>
      <label class="checkbox"><input name="refundsPending" type="checkbox" [(ngModel)]="refundsPending" /> Reembolsos pendientes</label>
      <button [disabled]="busy()">Buscar / actualizar</button>
    </form>
    <p class="muted">Confirma para descontar stock. Cancelar un pedido confirmado repone sus unidades una sola vez. No acredita pagos.</p>
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    @if (busy()) { <p role="status">Cargando pedidos…</p> }
    @if (result(); as page) {
      <p>{{ page.total }} pedidos encontrados</p>
      <section class="panel table-scroll"><table>
        <thead><tr><th>Pedido</th><th>Cliente</th><th>Estado / entrega</th><th>Pago</th><th>Total COP</th><th>Acción</th></tr></thead>
        <tbody>@for (item of page.items; track item.id) {
          <tr><td>{{ item.order.orderNumber }}<small>{{ item.order.createdAtUtc | date:'short' }}</small>
            @if (item.order.isTestOrder) { <span class="badge warning">PRUEBA</span> }</td>
            <td>{{ item.customerName }}<small>{{ item.phone }}</small></td><td>{{ statuses[item.order.status] }}<small>{{ deliveries[item.order.deliveryMethod] }}</small></td>
            <td>{{ item.order.grandTotal === 0 ? 'Sin cobro' : payments[item.order.paymentStatus] }}
              @if (item.order.refundedTotal > 0) { <small>Reembolsado: {{ item.order.refundedTotal | currency:'COP':'symbol-narrow':'1.0-0' }}</small> }</td>
            <td>{{ item.order.grandTotal | currency:'COP':'symbol-narrow':'1.0-0' }}</td>
            <td><a [routerLink]="['/admin/pedidos', item.id]">Abrir pedido</a></td></tr>
        } @empty { <tr><td colspan="6">No hay pedidos con estos filtros.</td></tr> }</tbody>
      </table></section>
      <nav class="pagination" aria-label="Páginas de pedidos">
        <button class="secondary" [disabled]="busy() || page.page <= 1" (click)="changePage(-1)">Anterior</button>
        <span>Página {{ page.page }} de {{ totalPages() }}</span>
        <button class="secondary" [disabled]="busy() || page.page * page.pageSize >= page.total" (click)="changePage(1)">Siguiente</button>
      </nav>
    }
  `,
})
export class OrdersPage implements OnInit {
  private readonly orders = inject(Orders)
  private readonly session = inject(Session)
  readonly statuses = statusLabels
  readonly statusOptions = Object.keys(statusLabels) as OrderStatus[]
  readonly payments = paymentStatusLabels
  readonly paymentOptions = Object.keys(paymentStatusLabels) as PaymentStatus[]
  readonly deliveries = deliveryLabels
  readonly deliveryOptions = Object.keys(deliveryLabels) as DeliveryMethod[]
  readonly result = signal<Page<AdminOrder> | null>(null)
  readonly busy = signal(false)
  readonly error = signal('')
  term = ''
  status = ''
  paymentStatus = ''
  deliveryMethod = ''
  returnsPending = false
  refundsPending = false
  private page = 1
  ngOnInit(): void { void this.load() }
  search(): void { this.page = 1; void this.load() }
  changePage(delta: number): void { this.page += delta; void this.load() }
  totalPages(): number { return Math.max(1, Math.ceil((this.result()?.total ?? 0) / 12)) }
  async load(): Promise<void> {
    if (this.busy()) return
    this.busy.set(true)
    this.error.set('')
    this.result.set(null)
    try { this.result.set(await this.orders.list({
      search: this.term, status: this.status, paymentStatus: this.paymentStatus, deliveryMethod: this.deliveryMethod,
      returnsPending: this.returnsPending, refundsPending: this.refundsPending, page: this.page,
    })) }
    catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
}
