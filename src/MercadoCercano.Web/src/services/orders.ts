import { Injectable } from '@angular/core'
import { apiRequest } from './http'
import { csrfRequest } from './auth'
import type { Page } from './catalog'

export type DeliveryMethod = 'CoordinatedPickup' | 'LocalDelivery' | 'CarrierShipping'
export type PaymentMethod = 'BankTransfer' | 'DigitalWallet' | 'CashOnDelivery'
export type PaymentStatus = 'Pending' | 'UnderReview' | 'Paid' | 'Rejected' | 'Refunded'
export type OrderStatus = 'PendingConfirmation' | 'Confirmed' | 'Preparing' | 'ReadyForDelivery' | 'Shipped' | 'Delivered' | 'Cancelled'
export const deliveryLabels: Record<DeliveryMethod, string> = {
  CoordinatedPickup: 'Recogida coordinada', LocalDelivery: 'Domicilio local', CarrierShipping: 'Transportadora',
}
export const paymentLabels: Record<PaymentMethod, string> = {
  BankTransfer: 'Transferencia bancaria', DigitalWallet: 'Billetera digital', CashOnDelivery: 'Efectivo contra entrega',
}
export const statusLabels: Record<OrderStatus, string> = {
  PendingConfirmation: 'Pendiente de confirmación', Confirmed: 'Confirmado', Preparing: 'En preparación',
  ReadyForDelivery: 'Listo para entrega', Shipped: 'Enviado', Delivered: 'Entregado', Cancelled: 'Cancelado',
}
export const paymentStatusLabels: Record<PaymentStatus, string> = {
  Pending: 'Pendiente', UnderReview: 'En revisión', Paid: 'Pagado', Rejected: 'Rechazado', Refunded: 'Reembolsado',
}
export type CheckoutSettings = {
  isTestConfiguration: boolean; pickupEnabled: boolean; pickupMunicipality: string; pickupInstructions: string
  localDeliveryEnabled: boolean; localDeliveryFee: number; localMunicipalities: string[]
  carrierShippingEnabled: boolean; carrierShippingFee: number; carrierNationwide: boolean
  carrierMunicipalities: string[]; paymentMethods: PaymentMethod[]; paymentInstructions: string; termsText: string
}
export type SettingsResponse = { values: CheckoutSettings; updatedAtUtc: string }
export type CartItem = { productId: string; quantity: number }
export type QuoteSelection = {
  items: CartItem[]; deliveryMethod: DeliveryMethod; paymentMethod: PaymentMethod; municipality: string | null
  couponCode?: string | null
}
export type AppliedPromotion = { name: string; code: string | null; discountTotal: number }
export type Quote = {
  items: { productId: string; name: string; sku: string; quantity: number; regularUnitPrice: number; unitPrice: number; lineTotal: number; availableStock: number; promotionDiscountTotal: number }[]
  subtotal: number; discountTotal: number; shippingCost: number; grandTotal: number
  deliveryMethod: DeliveryMethod; paymentMethod: PaymentMethod; municipality: string; isTestConfiguration: boolean
  termsText: string; pickupInstructions: string; paymentInstructions: string; fingerprint: string
  promotion: AppliedPromotion | null; couponNotice: string | null
}
export type CreateOrder = {
  selection: QuoteSelection; quoteFingerprint: string; idempotencyKey: string; trackingToken: string
  customerName: string; phone: string; email: string | null; neighborhood: string | null
  address: string | null; addressReferences: string | null; notes: string | null; termsAccepted: boolean
}
export type Order = {
  orderNumber: string; status: OrderStatus; paymentStatus: PaymentStatus; deliveryMethod: DeliveryMethod; paymentMethod: PaymentMethod
  subtotal: number; discountTotal: number; shippingCost: number; grandTotal: number; isTestOrder: boolean; createdAtUtc: string
  items: { name: string; sku: string; quantity: number; unitPrice: number; lineTotal: number; promotionDiscountTotal: number }[]
  history: { status: OrderStatus; reason: string; createdAtUtc: string }[]
  termsText: string | null; pickupInstructions: string | null; paymentInstructions: string | null
  updatedAtUtc: string; fulfillment: { carrierName: string | null; trackingNumber: string | null; shippedAtUtc: string | null; deliveredAtUtc: string | null }
  refundedTotal: number; paymentRejectionReason: string | null; returns: ReturnSummary[]
  promotion: AppliedPromotion | null
}
export type ReturnStatus = 'Requested' | 'Received' | 'Refunded' | 'Rejected'
export const returnStatusLabels: Record<ReturnStatus, string> = {
  Requested: 'Pendiente de recepción', Received: 'Recepción registrada', Refunded: 'Reembolso registrado', Rejected: 'Solicitud rechazada',
}
export type ReturnSummary = {
  id: string; kind: 'CustomerReturn' | 'Cancellation' | 'FailedDelivery'; status: ReturnStatus
  reason: string; rejectionReason: string | null; refundAmount: number; createdAtUtc: string
  receivedAtUtc: string | null; refundedAtUtc: string | null
}
export type AdminReturn = {
  summary: ReturnSummary; shippingRefundAmount: number; refundReference: string | null
  items: { id: string; orderItemId: string; name: string; quantity: number; restockedQuantity: number; refundAmount: number }[]
}
export type AdminOrder = {
  id: string; order: Order; customerName: string; phone: string; email: string | null
  municipality: string; neighborhood: string | null; address: string | null; addressReferences: string | null
  notes: string | null; cancellationReason: string | null; updatedAtUtc: string
  operations: {
    paymentReference: string | null; paymentRejectionReason: string | null
    evidence: { id: string; byteLength: number; createdAtUtc: string }[]
    lines: { id: string; name: string; quantity: number; unitPrice: number; returnableQuantity: number; refundableAmount: number }[]
    returns: AdminReturn[]
  }
}

@Injectable({ providedIn: 'root' })
export class Orders {
  settings(admin = false): Promise<SettingsResponse> {
    return apiRequest(admin ? '/api/v1/admin/checkout-settings' : '/api/v1/checkout/settings')
  }
  saveSettings(values: CheckoutSettings, updatedAtUtc: string | null): Promise<SettingsResponse> {
    return csrfRequest('/api/v1/admin/checkout-settings', { method: 'PUT', body: JSON.stringify({ values, updatedAtUtc }) })
  }
  quote(selection: QuoteSelection): Promise<Quote> {
    return csrfRequest('/api/v1/checkout/quote', { method: 'POST', body: JSON.stringify(selection) })
  }
  create(request: CreateOrder): Promise<Order> {
    return csrfRequest('/api/v1/checkout/orders', { method: 'POST', body: JSON.stringify(request) })
  }
  track(orderNumber: string, trackingToken: string): Promise<Order> {
    return csrfRequest('/api/v1/checkout/track', { method: 'POST', body: JSON.stringify({ orderNumber, trackingToken }) })
  }
  recover(idempotencyKey: string, trackingToken: string): Promise<Order> {
    return csrfRequest('/api/v1/checkout/recover', { method: 'POST', body: JSON.stringify({ idempotencyKey, trackingToken }) })
  }
  list(filters: { search: string; status: string; paymentStatus: string; deliveryMethod: string; returnsPending: boolean; refundsPending: boolean; page: number }): Promise<Page<AdminOrder>> {
    const query = new URLSearchParams({ search: filters.search, page: String(filters.page), pageSize: '12' })
    if (filters.status) query.set('status', filters.status)
    if (filters.paymentStatus) query.set('paymentStatus', filters.paymentStatus)
    if (filters.deliveryMethod) query.set('deliveryMethod', filters.deliveryMethod)
    if (filters.returnsPending) query.set('returnsPending', 'true')
    if (filters.refundsPending) query.set('refundsPending', 'true')
    return apiRequest(`/api/v1/admin/orders?${query}`)
  }
  adminOrder(id: string): Promise<AdminOrder> {
    return apiRequest(`/api/v1/admin/orders/${encodeURIComponent(id)}`)
  }
  act(id: string, action: 'confirm' | 'cancel', updatedAtUtc: string, reason?: string): Promise<AdminOrder> {
    return csrfRequest(`/api/v1/admin/orders/${encodeURIComponent(id)}/${action}`, {
      method: 'POST', body: JSON.stringify({ updatedAtUtc, reason }),
    })
  }
  fulfill(id: string, action: 'prepare' | 'ready' | 'ship' | 'deliver', body: {
    updatedAtUtc: string; carrierName?: string; trackingNumber?: string; deliveryConfirmed?: boolean
  }): Promise<AdminOrder> {
    return csrfRequest(`/api/v1/admin/orders/${encodeURIComponent(id)}/fulfillment/${action}`, { method: 'POST', body: JSON.stringify(body) })
  }
  reviewPayment(id: string, approve: boolean, body: {
    updatedAtUtc: string; amount?: number; reference?: string; reason?: string; fundsVerified?: boolean
  }): Promise<AdminOrder> {
    return csrfRequest(`/api/v1/admin/orders/${encodeURIComponent(id)}/payment/${approve ? 'approve' : 'reject'}`, { method: 'POST', body: JSON.stringify(body) })
  }
  submitEvidence(id: string, form: FormData): Promise<AdminOrder> {
    return csrfRequest(`/api/v1/admin/orders/${encodeURIComponent(id)}/payment/submit-evidence`, { method: 'POST', body: form })
  }
  submitGuestEvidence(form: FormData): Promise<Order> {
    return csrfRequest('/api/v1/checkout/payment/submit-evidence', { method: 'POST', body: form })
  }
  requestReturn(id: string, body: {
    updatedAtUtc: string; requestId: string; reason: string; shippingRefundAmount: number
    items: { orderItemId: string; quantity: number }[]
  }): Promise<AdminOrder> {
    return csrfRequest(`/api/v1/admin/orders/${encodeURIComponent(id)}/returns`, { method: 'POST', body: JSON.stringify(body) })
  }
  returnByRequest(id: string, requestId: string): Promise<AdminReturn> {
    return apiRequest(`/api/v1/admin/orders/${encodeURIComponent(id)}/returns/by-request/${encodeURIComponent(requestId)}`)
  }
  returnAction(id: string, returnId: string, action: 'receive' | 'reject' | 'refund', body: {
    updatedAtUtc: string; reason?: string; amount?: number; reference?: string; refundCompleted?: boolean; receiptConfirmed?: boolean
    items?: { returnItemId: string; restockQuantity: number }[]
  }): Promise<AdminOrder> {
    return csrfRequest(`/api/v1/admin/orders/${encodeURIComponent(id)}/returns/${encodeURIComponent(returnId)}/${action}`, { method: 'POST', body: JSON.stringify(body) })
  }
}
