import { CurrencyPipe, DatePipe } from '@angular/common'
import { Component, HostListener, inject, signal, type OnInit } from '@angular/core'
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { Catalog, type AdminProduct, type Category, type Page } from '../../services/catalog'
import { Promotions, promotionStates, type Promotion, type PromotionUsage, type PromotionWrite } from '../../services/promotions'
import { Session } from '../../services/session'

const maximum = 9_000_000_000_000
const positive = [Validators.min(1), Validators.max(maximum), Validators.pattern(/^\d+$/)]

@Component({
  imports: [FormsModule, ReactiveFormsModule, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './promotions-page.html',
})
export class PromotionsPage implements OnInit {
  private readonly api = inject(Promotions)
  private readonly catalog = inject(Catalog)
  private readonly session = inject(Session)
  private readonly fb = inject(FormBuilder)
  readonly result = signal<Page<Promotion> | null>(null)
  readonly categories = signal<Category[]>([])
  readonly editing = signal<Promotion | null>(null)
  readonly products = signal<Page<AdminProduct> | null>(null)
  readonly productName = signal('')
  readonly usagePromotion = signal<Promotion | null>(null)
  readonly history = signal<Page<PromotionUsage> | null>(null)
  readonly busy = signal(false)
  readonly loading = signal(false)
  readonly productLoading = signal(false)
  readonly historyLoading = signal(false)
  readonly error = signal('')
  readonly message = signal('')
  readonly states = promotionStates
  term = ''
  kind = ''
  active = ''
  page = 1
  productTerm = ''
  productPage = 1
  usagePage = 1
  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(160), Validators.pattern(/\S/)]],
    mode: ['automatic'],
    code: ['', [Validators.maxLength(80), Validators.pattern(/^[A-Za-z0-9][A-Za-z0-9_-]{2,79}$/)]],
    discountType: ['Percentage' as PromotionWrite['discountType']],
    discountValue: [10, [Validators.required, ...positive]],
    minimumOrderAmount: [0, [Validators.required, Validators.min(0), Validators.max(maximum), Validators.pattern(/^\d+$/)]],
    maximumDiscountAmount: this.fb.control<number | null>(null, positive),
    maxUses: this.fb.control<number | null>(null, [Validators.min(1), Validators.max(2147483647), Validators.pattern(/^\d+$/)]),
    scope: ['all'],
    productId: [''], categoryId: [''],
    startsAt: [this.local(new Date().toISOString()), Validators.required],
    endsAt: [this.local(new Date(Date.now() + 7 * 86400000).toISOString()), Validators.required],
    isActive: [false],
  })
  ngOnInit(): void { void this.load() }
  modeChanged(): void {
    if (this.form.controls.mode.value === 'automatic') this.form.controls.code.reset('')
  }
  canLeave(): boolean {
    return !this.session.user() || (!this.busy() && (!this.form.dirty || window.confirm('¿Descartar la promoción sin guardar?')))
  }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.busy() || this.form.dirty) { event.preventDefault(); event.returnValue = '' }
  }
  private local(iso: string): string {
    const date = new Date(iso)
    return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
  }
  reset(): void {
    if (this.busy() || (this.form.dirty && !window.confirm('¿Descartar los cambios actuales?'))) return
    this.editing.set(null)
    this.productName.set('')
    this.products.set(null)
    this.form.reset({
      name: '', mode: 'automatic', code: '', discountType: 'Percentage', discountValue: 10, minimumOrderAmount: 0,
      maximumDiscountAmount: null, maxUses: null, scope: 'all', productId: '', categoryId: '',
      startsAt: this.local(new Date().toISOString()), endsAt: this.local(new Date(Date.now() + 7 * 86400000).toISOString()), isActive: false,
    })
  }
  async load(): Promise<void> {
    if (this.loading()) return
    this.loading.set(true)
    this.error.set('')
    try {
      const [result, categories] = await Promise.all([this.api.list(this.term, this.kind, this.active, this.page), this.catalog.categories(true)])
      this.result.set(result); this.categories.set(categories)
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.loading.set(false) }
  }
  search(): void { this.page = 1; void this.load() }
  async edit(id: string): Promise<void> {
    if (this.busy() || (this.form.dirty && !window.confirm('¿Descartar los cambios actuales?'))) return
    this.busy.set(true); this.error.set(''); this.message.set('')
    try {
      const item = await this.api.get(id)
      this.editing.set(item)
      this.productName.set(item.productName ?? '')
      this.products.set(null)
      this.form.reset({
        ...item, mode: item.code === null ? 'automatic' : 'coupon', code: item.code ?? '',
        scope: item.productId ? 'product' : item.categoryId ? 'category' : 'all',
        productId: item.productId ?? '', categoryId: item.categoryId ?? '',
        startsAt: this.local(item.startsAtUtc), endsAt: this.local(item.endsAtUtc),
      })
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
  async findProducts(reset = false): Promise<void> {
    if (this.productLoading() || this.busy()) return
    if (reset) this.productPage = 1
    this.productLoading.set(true); this.error.set('')
    try { this.products.set(await this.catalog.adminProducts({ search: this.productTerm, categoryId: '', sort: 'name', page: this.productPage })) }
    catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.productLoading.set(false) }
  }
  selectProduct(item: AdminProduct): void {
    this.form.controls.productId.setValue(item.product.id)
    this.form.controls.productId.markAsDirty()
    this.productName.set(item.product.name)
  }
  async save(): Promise<void> {
    if (this.busy()) return
    this.form.markAllAsTouched()
    const value = this.form.getRawValue()
    const start = new Date(value.startsAt), end = new Date(value.endsAt)
    if (this.form.invalid || !value.name.trim() || !Number.isFinite(start.getTime()) || !Number.isFinite(end.getTime()) || end <= start ||
        (value.discountType === 'Percentage' && value.discountValue > 100) ||
        (value.mode === 'coupon' && !value.code.trim()) || (value.scope === 'product' && !value.productId) ||
        (value.scope === 'category' && !value.categoryId)) {
      this.error.set('Revisa nombre, código de 3–80 caracteres si es cupón, importes enteros positivos, porcentaje hasta 100, alcance y fechas crecientes.')
      return
    }
    this.busy.set(true); this.error.set(''); this.message.set('')
    try {
      const previous = this.editing()
      const saved = await this.api.save(previous?.id ?? null, {
        name: value.name.trim(), code: value.mode === 'coupon' ? value.code.trim().toUpperCase() : null,
        discountType: value.discountType, discountValue: value.discountValue, minimumOrderAmount: value.minimumOrderAmount,
        maximumDiscountAmount: value.maximumDiscountAmount, maxUses: value.maxUses,
        productId: value.scope === 'product' ? value.productId : null, categoryId: value.scope === 'category' ? value.categoryId : null,
        startsAtUtc: previous && value.startsAt === this.local(previous.startsAtUtc) ? previous.startsAtUtc : start.toISOString(),
        endsAtUtc: previous && value.endsAt === this.local(previous.endsAtUtc) ? previous.endsAtUtc : end.toISOString(),
        isActive: value.isActive, updatedAtUtc: previous?.updatedAtUtc ?? null,
      })
      this.editing.set(saved)
      this.form.markAsPristine()
      this.message.set('Promoción guardada. Los pedidos existentes conservan sus descuentos originales.')
      await this.load()
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
  async showHistory(item: Promotion, page = 1): Promise<void> {
    if (this.historyLoading()) return
    this.historyLoading.set(true); this.error.set('')
    try {
      const result = await this.api.usages(item.id, page)
      this.usagePromotion.set(item); this.usagePage = page; this.history.set(result)
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.historyLoading.set(false) }
  }
}
