import { CurrencyPipe } from '@angular/common'
import { Component, inject, signal, type OnInit } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { Catalog, type Category, type AdminProduct, type Page } from '../../services/catalog'
import { Session } from '../../services/session'

@Component({
  imports: [FormsModule, RouterLink, CurrencyPipe],
  template: `
    <div class="section-title"><h1>Productos</h1><a class="button" routerLink="/admin/productos/nuevo">Nuevo producto</a></div>
    <form class="filters panel" (ngSubmit)="search()">
      <label>Buscar<input name="term" [(ngModel)]="term" maxlength="100" placeholder="Nombre o SKU" /></label>
      <label>Categoría<select name="category" [(ngModel)]="categoryId"><option value="">Todas</option>
        @for (category of categories(); track category.id) { <option [value]="category.id">{{ category.name }}{{ category.isActive ? '' : ' (inactiva)' }}</option> }
      </select></label><button [disabled]="busy()">Buscar</button>
    </form>
    @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
    @if (busy()) { <p role="status">Cargando productos…</p> }
    @if (result(); as page) {
      <div class="panel table-scroll"><table><thead><tr><th>Producto</th><th>Precio COP</th><th>Stock</th><th>Estado</th><th>Acción</th></tr></thead>
        <tbody>@for (item of page.items; track item.product.id) {
          <tr><td>{{ item.product.name }}<small>{{ item.product.sku }} · {{ item.product.categoryName }}</small></td>
            <td>{{ item.product.effectivePrice | currency:'COP':'symbol-narrow':'1.0-0' }}</td>
            <td><span class="badge" [class.warning]="item.product.stockQuantity <= item.lowStockThreshold">{{ item.product.stockQuantity }}</span></td>
            <td>{{ item.isActive ? 'Activo' : 'Inactivo' }}</td>
            <td><a [routerLink]="['/admin/productos', item.product.id]">Editar</a></td></tr>
        } @empty { <tr><td colspan="5">No hay productos con estos filtros.</td></tr> }</tbody></table></div>
      <nav class="pagination" aria-label="Páginas de productos">
        <button class="secondary" [disabled]="page.page <= 1 || busy()" (click)="changePage(-1)">Anterior</button>
        <span>{{ page.total }} productos · Página {{ page.page }}</span>
        <button class="secondary" [disabled]="page.page * page.pageSize >= page.total || busy()" (click)="changePage(1)">Siguiente</button>
      </nav>
    }
  `,
})
export class ProductsPage implements OnInit {
  private readonly catalog = inject(Catalog)
  private readonly session = inject(Session)
  readonly categories = signal<Category[]>([])
  readonly result = signal<Page<AdminProduct> | null>(null)
  readonly busy = signal(false)
  readonly error = signal('')
  term = ''
  categoryId = ''
  private page = 1
  ngOnInit(): void { void this.load() }
  search(): void { this.page = 1; void this.load() }
  changePage(delta: number): void { this.page += delta; void this.load() }
  async load(): Promise<void> {
    if (this.busy()) return
    this.busy.set(true)
    this.error.set('')
    this.result.set(null)
    try {
      const [categories, products] = await Promise.all([
        this.catalog.categories(true),
        this.catalog.adminProducts({ search: this.term, categoryId: this.categoryId, page: this.page, sort: 'name' }),
      ])
      this.categories.set(categories)
      this.result.set(products)
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
}
