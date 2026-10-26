import { Injectable, signal } from '@angular/core'
import { uuidPattern } from './cart'

export type CheckoutIntent = { idempotencyKey: string; trackingToken: string; cartSignature: string }
const key = 'MercadoCercano.PendingCheckout.v1'

@Injectable({ providedIn: 'root' })
export class CheckoutIntentStore {
  readonly pending = signal<CheckoutIntent | null>(null)
  readonly error = signal('')
  readonly ready = signal(false)

  constructor() {
    try {
      const stored = sessionStorage.getItem(key)
      if (stored !== null) {
        const value: unknown = JSON.parse(stored)
        if (typeof value !== 'object' || value === null ||
            !('idempotencyKey' in value) || typeof value.idempotencyKey !== 'string' || !uuidPattern.test(value.idempotencyKey) ||
            !('trackingToken' in value) || typeof value.trackingToken !== 'string' || !/^[A-Za-z0-9_-]{43}$/.test(value.trackingToken) ||
            !('cartSignature' in value) || typeof value.cartSignature !== 'string' || value.cartSignature.length > 8192) {
          throw new Error('Identificador de checkout inválido.')
        }
        this.pending.set({ idempotencyKey: value.idempotencyKey, trackingToken: value.trackingToken, cartSignature: value.cartSignature })
      }
      this.ready.set(true)
    } catch {
      this.error.set('No pudimos leer el intento pendiente. No envíes otro pedido sin comprobar antes si ya fue registrado.')
    }
  }

  ensure(signature: string): CheckoutIntent {
    if (!this.ready()) throw new Error(this.error())
    const pending = this.pending()
    if (pending) return pending
    const bytes = crypto.getRandomValues(new Uint8Array(32))
    const intent = {
      idempotencyKey: crypto.randomUUID(),
      trackingToken: btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, ''),
      cartSignature: signature,
    }
    try { sessionStorage.setItem(key, JSON.stringify(intent)) }
    catch { throw new Error('No se pudo conservar el identificador del pedido. Habilita el almacenamiento de sesión antes de enviarlo.') }
    this.pending.set(intent)
    return intent
  }

  clear(): void {
    try { sessionStorage.removeItem(key) }
    catch { throw new Error('El pedido se registró, pero no pudimos limpiar el intento pendiente del navegador. Guarda el enlace de seguimiento.') }
    this.pending.set(null)
    this.error.set('')
    this.ready.set(true)
  }
}
