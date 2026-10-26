import { CurrencyPipe } from '@angular/common'
import { Component, inject, signal, type OnInit } from '@angular/core'
import { ActivatedRoute, RouterLink } from '@angular/router'
import { Catalog, type Product } from '../../services/catalog'
import { errorMessage } from '../../services/http'
import { Cart } from '../../services/cart'

@Component({
  imports: [RouterLink, CurrencyPipe],
  template: `
    <a routerLink="/">← Volver al catálogo</a>
    @if (busy()) { <p role="status">Cargando producto…</p> }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    @if (product(); as item) {
      <article class="panel product-detail">
        <div>
          @if (item.images[selected()]; as image) {
            <img class="detail-image" [src]="image.imageUrl" [alt]="image.altText || item.name" />
          } @else { <div class="image-placeholder">Sin imagen</div> }
          <div class="thumbnails">
            @for (image of item.images; track image.id; let index = $index) {
              <button type="button" class="secondary" (click)="selected.set(index)" [attr.aria-label]="'Ver imagen ' + (index + 1)" [attr.aria-pressed]="selected() === index">
                <img [src]="image.imageUrl" [alt]="image.altText || item.name" />
              </button>
            }
          </div>
        </div>
        <div><p class="eyebrow">{{ item.categoryName }}</p><h1>{{ item.name }}</h1>
          <p class="muted">SKU: {{ item.sku }}</p>
          @if (item.isFeatured) { <span class="badge">Destacado</span> }
          @if (item.effectivePrice < item.price) { <del>{{ item.price | currency:'COP':'symbol-narrow':'1.0-0' }}</del> }
          <p class="price">{{ item.effectivePrice | currency:'COP':'symbol-narrow':'1.0-0' }} <small>COP</small></p>
          <p>{{ item.presentation || item.unit }}</p>
          @if (item.weightGrams) { <p>Peso: {{ item.weightGrams }} g</p> }
          <p class="badge" [class.warning]="item.stockQuantity === 0">{{ item.stockQuantity > 0 ? 'Disponible' : 'Agotado' }}</p>
          <p class="description">{{ item.description }}</p>
          <label>Cantidad<input #quantity type="number" min="1" step="1" [max]="item.stockQuantity" value="1" /></label>
          <div class="actions"><button type="button" (click)="cart.add(item, quantity.valueAsNumber)"
            [disabled]="!cart.ready() || item.stockQuantity === 0">Agregar al carrito</button><a routerLink="/carrito">Ver carrito</a></div>
          <p class="muted">El carrito no reserva inventario; se verificará de nuevo al finalizar el pedido.</p>
        </div>
      </article>
    }
  `,
})
export class ProductDetail implements OnInit {
  readonly cart = inject(Cart)
  private readonly catalog = inject(Catalog)
  private readonly route = inject(ActivatedRoute)
  readonly product = signal<Product | null>(null)
  readonly selected = signal(0)
  readonly error = signal('')
  readonly busy = signal(true)

  async ngOnInit(): Promise<void> {
    try { this.product.set(await this.catalog.product(this.route.snapshot.paramMap.get('slug')!)) }
    catch (error) { this.error.set(errorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
