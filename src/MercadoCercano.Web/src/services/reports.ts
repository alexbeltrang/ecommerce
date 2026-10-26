import { Injectable } from '@angular/core'
import { apiRequest, downloadCsvRequest } from './http'
import type { Page } from './catalog'
import type { OrderStatus } from './orders'

export type ReportView = 'sales' | 'products' | 'inventory' | 'promotions'
export type ReportFilters = {
  from: string; to: string; dataSet: 'real' | 'test' | 'all'; search: string
  sort: 'units' | 'revenue' | 'margin'; categoryId: string
  stock: 'all' | 'low' | 'out'; visibility: 'all' | 'active' | 'inactive'
}
export type ReportPeriod = {
  from: string; to: string; timeZone: string; dataSet: string; generatedAtUtc: string; warnings: string[]
}
export type ReportMetrics = {
  createdOrders: number; deliveredOrders: number; freeDeliveredOrders: number
  merchandiseSales: string; shippingSales: string; salesTotal: string
  productDiscounts: string; promotionDiscounts: string; collections: string; refunds: string; netCash: string
  merchandiseRefunds: string; deliveredCost: string; restockedCost: string; estimatedMargin: string
}
export type SalesReport = {
  period: ReportPeriod; totals: ReportMetrics; days: { date: string; metrics: ReportMetrics }[]
  statuses: { status: OrderStatus; orders: number; orderAmount: string }[]
}
export type ProductReportRow = {
  productId: string | null; sku: string; name: string; deliveredQuantity: number
  returnedQuantity: number; restockedQuantity: number; merchandiseSales: string; merchandiseRefunds: string
  netMerchandise: string; deliveredCost: string; restockedCost: string; estimatedMargin: string
}
export type ProductsReport = { period: ReportPeriod; rows: Page<ProductReportRow> }
export type InventoryReportRow = {
  productId: string; sku: string; name: string; category: string; isPublished: boolean; quantity: number
  lowStockThreshold: number; unitCost: string; stockValue: string; state: 'Out' | 'Low' | 'Available'
}
export type InventoryReport = {
  generatedAtUtc: string; products: number; units: number; outOfStock: number; lowStock: number
  stockValue: string; rows: Page<InventoryReportRow>
}
export type PromotionReportRow = {
  promotionId: string; name: string; code: string | null; isActive: boolean
  usesCreated: number; usesReleased: number; createdUsesStillConsumed: number
  discountsGranted: string; discountsReleased: string; currentUsedCount: number; maxUses: number | null
}
export type PromotionsReport = { period: ReportPeriod; rows: Page<PromotionReportRow> }

@Injectable({ providedIn: 'root' })
export class Reports {
  sales(filters: ReportFilters): Promise<SalesReport> {
    return apiRequest(this.path('sales', filters))
  }
  products(filters: ReportFilters, page: number): Promise<ProductsReport> {
    return apiRequest(this.path('products', filters, page))
  }
  inventory(filters: ReportFilters, page: number): Promise<InventoryReport> {
    return apiRequest(this.path('inventory', filters, page))
  }
  promotions(filters: ReportFilters, page: number): Promise<PromotionsReport> {
    return apiRequest(this.path('promotions', filters, page))
  }
  export(view: ReportView, filters: ReportFilters): Promise<void> {
    return downloadCsvRequest(this.path(view, filters, undefined, true), `reporte-${view}.csv`)
  }
  private path(view: ReportView, filters: ReportFilters, page?: number, csv = false): string {
    const query = new URLSearchParams()
    if (view === 'inventory') {
      query.set('search', filters.search)
      query.set('stock', filters.stock)
      query.set('visibility', filters.visibility)
      if (filters.categoryId) query.set('categoryId', filters.categoryId)
    } else {
      query.set('from', filters.from)
      query.set('to', filters.to)
      query.set('dataSet', filters.dataSet)
      if (view !== 'sales') query.set('search', filters.search)
      if (view === 'products') query.set('sort', filters.sort)
    }
    if (page !== undefined && !csv) { query.set('page', String(page)); query.set('pageSize', '12') }
    return `/api/v1/admin/reports/${view}${csv ? '.csv' : ''}?${query}`
  }
}
