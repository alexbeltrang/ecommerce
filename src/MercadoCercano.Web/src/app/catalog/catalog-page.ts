import { CurrencyPipe } from '@angular/common'
import { Component, inject, signal, type OnInit } from '@angular/core'
import { FormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { Catalog, type Category, type Page, type Product } from '../../services/catalog'
import { errorMessage } from '../../services/http'
import { Cart } from '../../services/cart'

@Component({
  imports: [FormsModule, RouterLink, CurrencyPipe],
  template: `
    <section class="hero"><p class="eyebrow">PRODUCTOS CERCA DE TI</p><h1>Tu tienda local, en un solo lugar</h1>
      <p>Consulta nuestros productos y encuentra lo que necesitas.</p></section>
    <form class="filters panel" (ngSubmit)="search()">
      <label>Buscar producto<input name="search" [(ngModel)]="term" maxlength="100" placeholder="Nombre o SKU" /></label>
      <label>Categoría<select name="category" [(ngModel)]="categoryId">
        <option value="">Todas</option>
        @for (category of categories(); track category.id) { <option [value]="category.id">{{ category.name }}</option> }
      </select></label>
      <label>Orden<select name="sort" [(ngModel)]="sort">
        <option value="name">Nombre</option><option value="newest">Más recientes</option>
        <option value="price-asc">Menor precio</option><option value="price-desc">Mayor precio</option>
      </select></label>
      <button [disabled]="busy()">Buscar</button>
    </form>
    @if (error()) { <div class="error" role="alert">{{ error() }} <button class="secondary" (click)="load()">Reintentar</button></div> }
    @if (busy()) { <p role="status">Cargando catálogo…</p> }
    @if (!busy() && result(); as page) {
      <p class="muted">{{ page.total }} productos encontrados</p>
      <div class="product-grid">
        @for (product of page.items; track product.id) {
          <article class="product-card">
            <a [routerLink]="['/productos', product.slug]">
              @if (product.images[0]; as image) {
                <img [src]="image.imageUrl" [alt]="image.altText || product.name" loading="lazy" />
              } @else { <div class="image-placeholder" role="img" [attr.aria-label]="product.name + ': sin imagen'">Sin imagen</div> }
              <div class="product-card-body">
                <p class="eyebrow">{{ product.categoryName }}</p>
                <h2>{{ product.name }}</h2>
                @if (product.isFeatured) { <span class="badge">Destacado</span> }
                @if (product.effectivePrice < product.price) { <del>{{ product.price | currency:'COP':'symbol-narrow':'1.0-0' }}</del> }
                <p class="price">{{ product.effectivePrice | currency:'COP':'symbol-narrow':'1.0-0' }} <small>COP</small></p>
                <p>{{ product.presentation || product.unit }}</p>
                <span class="badge" [class.warning]="product.stockQuantity === 0">{{ product.stockQuantity > 0 ? 'Disponible' : 'Agotado' }}</span>
              </div>
            </a>
            <div class="product-card-body"><button type="button" (click)="cart.add(product)" [attr.aria-label]="'Agregar ' + product.name + ' al carrito'"
              [disabled]="!cart.ready() || product.stockQuantity === 0">Agregar al carrito</button></div>
          </article>
        } @empty { <p class="panel">No hay productos disponibles con estos filtros.</p> }
      </div>
      <nav class="pagination" aria-label="Páginas del catálogo">
        <button class="secondary" [disabled]="page.page <= 1" (click)="changePage(-1)">Anterior</button>
        <span>Página {{ page.page }} de {{ totalPages() }}</span>
        <button class="secondary" [disabled]="page.page * page.pageSize >= page.total" (click)="changePage(1)">Siguiente</button>
      </nav>
    }
  `,
})
export class CatalogPage implements OnInit {
  readonly cart = inject(Cart)
  private readonly catalog = inject(Catalog)
  readonly categories = signal<Category[]>([])
  readonly result = signal<Page<Product> | null>(null)
  readonly busy = signal(false)
  readonly error = signal('')
  term = ''
  categoryId = ''
  sort = 'name'
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
    try {
      const [categories, products] = await Promise.all([
        this.catalog.categories(),
        this.catalog.products({ search: this.term, categoryId: this.categoryId, sort: this.sort, page: this.page }),
      ])
      this.categories.set(categories)
      this.result.set(products)
    } catch (error) { this.error.set(errorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
