import { DatePipe } from '@angular/common'
import { Component, computed, inject, signal, type OnInit } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { Catalog, type Category } from '../../services/catalog'
import { statusLabels } from '../../services/orders'
import { Reports, type ReportFilters, type ReportView, type SalesReport, type ProductsReport, type InventoryReport, type PromotionsReport } from '../../services/reports'
import { Session } from '../../services/session'
import { ReportCop } from './report-cop'

@Component({
  imports: [ReactiveFormsModule, DatePipe, RouterLink, ReportCop],
  templateUrl: './reports-page.html',
})
export class ReportsPage implements OnInit {
  private readonly api = inject(Reports)
  private readonly catalog = inject(Catalog)
  private readonly session = inject(Session)
  private readonly fb = inject(FormBuilder)
  private readonly today = new Date(Date.now() - 5 * 3600000).toISOString().slice(0, 10)
  readonly view = signal<ReportView>('sales')
  readonly views: { id: ReportView; name: string }[] = [
    { id: 'sales', name: 'Ventas y dinero' }, { id: 'products', name: 'Productos y margen' },
    { id: 'inventory', name: 'Inventario actual' }, { id: 'promotions', name: 'Promociones y cupones' },
  ]
  readonly form = this.fb.nonNullable.group({
    from: [this.today.slice(0, 8) + '01', Validators.required], to: [this.today, Validators.required],
    dataSet: ['real' as ReportFilters['dataSet']], search: ['', Validators.maxLength(100)],
    sort: ['units' as ReportFilters['sort']], categoryId: [''],
    stock: ['all' as ReportFilters['stock']], visibility: ['all' as ReportFilters['visibility']],
  })
  readonly sales = signal<SalesReport | null>(null)
  readonly products = signal<ProductsReport | null>(null)
  readonly inventory = signal<InventoryReport | null>(null)
  readonly promotions = signal<PromotionsReport | null>(null)
  readonly categories = signal<Category[]>([])
  readonly applied = signal<ReportFilters | null>(null)
  readonly busy = signal(false)
  readonly downloading = signal(false)
  readonly error = signal('')
  readonly message = signal('')
  readonly statuses = statusLabels
  readonly period = computed(() => this.sales()?.period ?? this.products()?.period ?? this.promotions()?.period ?? null)
  readonly pagination = computed(() => this.products()?.rows ?? this.inventory()?.rows ?? this.promotions()?.rows ?? null)
  readonly maximumSales = computed(() => this.sales()?.days.reduce((maximum, day) => {
    const amount = BigInt(day.metrics.salesTotal)
    return amount > maximum ? amount : maximum
  }, 0n) ?? 0n)

  ngOnInit(): void { void this.load() }
  select(view: ReportView): void {
    if (this.busy() || this.downloading()) return
    this.clearResults()
    this.view.set(view)
    void this.load()
  }
  async load(filters = this.form.getRawValue(), page = 1): Promise<void> {
    if (this.busy() || this.downloading()) return
    const days = (Date.parse(filters.to + 'T00:00:00Z') - Date.parse(filters.from + 'T00:00:00Z')) / 86400000
    if ((this.view() !== 'inventory' && (!Number.isFinite(days) || days < 0 || days >= 366 ||
        filters.from < '1900-01-01' || filters.to > '9998-12-31')) || filters.search.length > 100) {
      this.error.set('Revisa las fechas: de 1 a 366 días inclusivos, entre 1900 y 9998; búsqueda de hasta 100 caracteres.')
      return
    }
    this.busy.set(true); this.error.set(''); this.message.set(''); this.clearResults()
    try {
      switch (this.view()) {
        case 'sales': this.sales.set(await this.api.sales(filters)); break
        case 'products': this.products.set(await this.api.products(filters, page)); break
        case 'promotions': this.promotions.set(await this.api.promotions(filters, page)); break
        case 'inventory': {
          const [result, categories] = await Promise.all([this.api.inventory(filters, page), this.catalog.categories(true)])
          this.inventory.set(result); this.categories.set(categories)
          break
        }
      }
      this.applied.set({ ...filters })
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
  changePage(delta: number): void {
    const applied = this.applied(), page = this.pagination()
    if (applied && page) void this.load(applied, page.page + delta)
  }
  async export(): Promise<void> {
    const applied = this.applied()
    if (!applied || this.busy() || this.downloading()) return
    this.downloading.set(true); this.error.set(''); this.message.set('')
    try {
      await this.api.export(this.view(), applied)
      this.message.set('Descarga CSV preparada con todos los resultados de los filtros aplicados, no solo esta página. Refleja los datos al exportar.')
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.downloading.set(false) }
  }
  bar(amount: string): number {
    const maximum = this.maximumSales()
    return maximum === 0n ? 0 : Number(BigInt(amount) * 10000n / maximum) / 100
  }
  private clearResults(): void {
    this.applied.set(null)
    this.sales.set(null); this.products.set(null); this.inventory.set(null); this.promotions.set(null)
  }
}
