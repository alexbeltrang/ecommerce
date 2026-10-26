import { computed, Injectable, signal } from '@angular/core'
import type { Product } from './catalog'
import type { CartItem } from './orders'

const storageKey = 'MercadoCercano.Cart.v1'
export const uuidPattern = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i

function validItem(value: unknown): value is CartItem {
  return typeof value === 'object' && value !== null &&
    'productId' in value && typeof value.productId === 'string' && uuidPattern.test(value.productId) &&
    'quantity' in value && typeof value.quantity === 'number' && Number.isInteger(value.quantity) &&
    value.quantity > 0 && value.quantity <= 2147483647
}

@Injectable({ providedIn: 'root' })
export class Cart {
  private readonly state = signal<CartItem[]>([])
  readonly items = this.state.asReadonly()
  readonly count = computed(() => this.items().reduce((sum, item) => sum + item.quantity, 0))
  readonly ready = signal(false)
  readonly error = signal('')
  readonly notice = signal('')

  constructor() {
    this.read()
    window.addEventListener('storage', (event) => { if (event.key === storageKey || event.key === null) this.read() })
  }

  signature(): string {
    return JSON.stringify([...this.items()].sort((a, b) => a.productId.localeCompare(b.productId)))
  }

  add(product: Product, quantity = 1): void {
    if (!this.ready()) return
    const previous = this.items().find((item) => item.productId === product.id)
    const total = (previous?.quantity ?? 0) + quantity
    if (!Number.isInteger(quantity) || quantity <= 0 || total > product.stockQuantity || total > 2147483647) {
      this.error.set('La cantidad supera las existencias disponibles o no es un entero positivo.')
      return
    }
    if (!previous && this.items().length >= 50) {
      this.error.set('El carrito admite hasta 50 productos distintos.')
      return
    }
    const next = previous
      ? this.items().map((item) => item.productId === product.id ? { ...item, quantity: total } : item)
      : [...this.items(), { productId: product.id, quantity }]
    if (this.persist(next)) this.notice.set(`${product.name}: agregado al carrito.`)
  }

  quantity(id: string, quantity: number, stock: number): void {
    if (!this.ready()) return
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > stock || quantity > 2147483647) {
      this.error.set('Usa una cantidad entera positiva que no supere las existencias actuales.')
      return
    }
    this.persist(this.items().map((item) => item.productId === id ? { ...item, quantity } : item))
  }
  remove(id: string): void { if (this.ready()) this.persist(this.items().filter((item) => item.productId !== id)) }
  clear(): boolean { return this.persist([]) }
  clearIfUnchanged(signature: string): boolean {
    if (!this.ready()) return false
    if (this.signature() === signature) return this.clear()
    else this.notice.set('El pedido se registró. Conservamos el carrito porque cambió durante el envío; revísalo antes de pedir otra vez.')
    return true
  }

  private persist(items: CartItem[]): boolean {
    try {
      localStorage.setItem(storageKey, JSON.stringify({ version: 1, items }))
      this.state.set(items)
      this.ready.set(true)
      this.error.set('')
      return true
    } catch {
      this.error.set('El navegador no permite guardar el carrito. No se modificó la selección; habilita el almacenamiento para continuar.')
      return false
    }
  }

  private read(): void {
    try {
      const stored = localStorage.getItem(storageKey)
      if (stored === null) {
        this.state.set([])
      } else {
        const value: unknown = JSON.parse(stored)
        if (typeof value !== 'object' || value === null || !('version' in value) || value.version !== 1 ||
            !('items' in value) || !Array.isArray(value.items) || value.items.length > 50 || !value.items.every(validItem)) {
          throw new Error('Formato de carrito inválido.')
        }
        const items = value.items.map((item) => ({ productId: item.productId.toLowerCase(), quantity: item.quantity }))
        if (new Set(items.map((item) => item.productId)).size !== items.length) throw new Error('Productos duplicados.')
        this.state.set(items)
      }
      this.ready.set(true)
      this.error.set('')
    } catch {
      this.ready.set(false)
      this.error.set('No pudimos leer el carrito guardado. Vacíalo explícitamente para empezar de nuevo o revisa el almacenamiento del navegador.')
    }
  }
}
