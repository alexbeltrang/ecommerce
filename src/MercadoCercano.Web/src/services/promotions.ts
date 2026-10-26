import { Injectable } from '@angular/core'
import { apiRequest } from './http'
import { csrfRequest } from './auth'
import type { Page } from './catalog'

export type PromotionWrite = {
  name: string; code: string | null; discountType: 'Percentage' | 'FixedAmount'; discountValue: number
  minimumOrderAmount: number; maximumDiscountAmount: number | null; maxUses: number | null
  productId: string | null; categoryId: string | null; startsAtUtc: string; endsAtUtc: string
  isActive: boolean; updatedAtUtc: string | null
}
export type Promotion = Omit<PromotionWrite, 'updatedAtUtc'> & {
  id: string; usedCount: number; productName: string | null; categoryName: string | null
  state: 'Active' | 'Inactive' | 'Scheduled' | 'Expired' | 'Exhausted'; updatedAtUtc: string
}
export type PromotionUsage = {
  orderId: string; orderNumber: string; discountAmount: number; usedAtUtc: string; releasedAtUtc: string | null
}
export const promotionStates: Record<Promotion['state'], string> = {
  Active: 'Vigente', Inactive: 'Inactiva', Scheduled: 'Programada', Expired: 'Vencida', Exhausted: 'Agotada',
}

@Injectable({ providedIn: 'root' })
export class Promotions {
  list(search: string, kind: string, active: string, page: number): Promise<Page<Promotion>> {
    const query = new URLSearchParams({ search, page: String(page), pageSize: '12' })
    if (kind) query.set('kind', kind)
    if (active) query.set('isActive', active)
    return apiRequest(`/api/v1/admin/promotions?${query}`)
  }
  get(id: string): Promise<Promotion> { return apiRequest(`/api/v1/admin/promotions/${encodeURIComponent(id)}`) }
  save(id: string | null, value: PromotionWrite): Promise<Promotion> {
    return csrfRequest(`/api/v1/admin/promotions${id ? `/${encodeURIComponent(id)}` : ''}`, {
      method: id ? 'PUT' : 'POST', body: JSON.stringify(value),
    })
  }
  usages(id: string, page: number): Promise<Page<PromotionUsage>> {
    return apiRequest(`/api/v1/admin/promotions/${encodeURIComponent(id)}/usages?page=${page}`)
  }
}
