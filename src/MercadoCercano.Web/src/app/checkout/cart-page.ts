import { CurrencyPipe } from '@angular/common'
import { Component, computed, inject, signal, type OnInit } from '@angular/core'
import { RouterLink } from '@angular/router'
import { Cart } from '../../services/cart'
import { Catalog, type Product } from '../../services/catalog'
import { ApiError, errorMessage } from '../../services/http'

@Component({
  imports: [CurrencyPipe, RouterLink],
  template: `
    <div class="section-title"><h1>Tu carrito</h1><a routerLink="/">Seguir comprando</a></div>
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    @if (!cart.ready()) { <button class="danger" (click)="reset()">Vaciar carrito guardado</button> }
    @if (busy()) { <p role="status">Actualizando precios y disponibilidad…</p> }
    @if (cart.items().length === 0 && cart.ready()) { <p class="panel">Tu carrito está vacío.</p> }
    @if (cart.items().length > 0) {
      <button class="secondary" (click)="load()" [disabled]="busy()">Actualizar disponibilidad</button>
      <section class="panel table-scroll">
        <table><thead><tr><th>Producto</th><th>Precio COP</th><th>Cantidad</th><th>Acción</th></tr></thead><tbody>
          @for (item of cart.items(); track item.productId) {
            <tr>
              @if (products().get(item.productId); as product) {
                <td><a [routerLink]="['/productos', product.slug]">{{ product.name }}</a><small>{{ product.sku }}</small></td>
                <td>{{ product.effectivePrice | currency:'COP':'symbol-narrow':'1.0-0' }}</td>
                <td><label>Cantidad<input #quantity type="number" min="1" [max]="product.stockQuantity" step="1"
                  [value]="item.quantity" (change)="update(item.productId, quantity, product.stockQuantity)" /></label>
                  @if (item.quantity > product.stockQuantity) { <small class="error">Quedan {{ product.stockQuantity }} unidades. Ajusta la cantidad o elimina el producto.</small> }
                </td>
              } @else { <td colspan="3">{{ busy() ? 'Cargando producto…' : 'Producto no disponible o pendiente de actualizar' }}</td> }
              <td><button type="button" class="danger" (click)="cart.remove(item.productId)">Eliminar</button></td>
            </tr>
          }
        </tbody></table>
      </section>
      <section class="panel">
        @if (estimated() !== null) { <p class="price">Subtotal estimado: {{ estimated() | currency:'COP':'symbol-narrow':'1.0-0' }}</p> }
        @else { <p class="error">Revisa los productos o las cantidades: el pedido no puede superar 9 billones COP.</p> }
        <p>El total definitivo, los descuentos vigentes y el envío se recalculan en el checkout.</p>
        <p class="warning">El carrito y el pedido pendiente no reservan inventario. El negocio confirma la disponibilidad antes de descontarlo.</p>
        <div class="actions">
          @if (canCheckout()) { <a class="button" routerLink="/checkout">Continuar al checkout</a> }
          <button class="secondary" (click)="reset()">Vaciar carrito</button>
        </div>
      </section>
    }
  `,
})
export class CartPage implements OnInit {
  readonly cart = inject(Cart)
  private readonly catalog = inject(Catalog)
  readonly products = signal(new Map<string, Product | null>())
  readonly busy = signal(false)
  readonly error = signal('')
  readonly estimated = computed(() => {
    let total = 0n
    for (const item of this.cart.items()) {
      const product = this.products().get(item.productId)
      if (!product) return null
      total += BigInt(product.effectivePrice) * BigInt(item.quantity)
      if (total > 9_000_000_000_000n) return null
    }
    return Number(total)
  })
  readonly canCheckout = computed(() => this.cart.ready() && !this.busy() && !this.error() &&
    this.cart.items().length > 0 && this.estimated() !== null &&
    this.cart.items().every((item) => (this.products().get(item.productId)?.stockQuantity ?? 0) >= item.quantity))

  ngOnInit(): void { void this.load() }
  reset(): void { if (window.confirm('¿Vaciar el carrito guardado?')) this.cart.clear() }
  update(id: string, input: HTMLInputElement, stock: number): void {
    this.cart.quantity(id, input.valueAsNumber, stock)
    input.value = String(this.cart.items().find((item) => item.productId === id)?.quantity ?? 1)
  }
  async load(): Promise<void> {
    if (this.busy() || !this.cart.ready()) return
    this.busy.set(true)
    this.error.set('')
    try {
      const products = await Promise.all(this.cart.items().map(async (item): Promise<[string, Product | null]> => {
        try { return [item.productId, await this.catalog.productById(item.productId)] }
        catch (error) {
          if (error instanceof ApiError && error.status === 404) return [item.productId, null]
          throw error
        }
      }))
      this.products.set(new Map(products))
    } catch (error) { this.error.set(errorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
