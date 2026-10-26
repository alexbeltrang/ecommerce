import { Injectable } from '@angular/core'
import { csrfRequest } from './auth'
import { apiRequest } from './http'

export type Category = {
  id: string; name: string; slug: string; description: string | null
  sortOrder: number; isActive: boolean; updatedAtUtc: string
}
export type ProductImage = { id: string; imageUrl: string; altText: string | null; sortOrder: number }
export type Product = {
  id: string; categoryId: string; categoryName: string; sku: string; name: string
  slug: string; description: string; price: number; effectivePrice: number
  stockQuantity: number; unit: string; weightGrams: number | null
  presentation: string | null; isFeatured: boolean; images: ProductImage[]
}
export type AdminProduct = {
  product: Product; cost: number; lowStockThreshold: number; isActive: boolean
  promotionalPrice: number | null; promotionStartUtc: string | null; promotionEndUtc: string | null
  lotNumber: string | null; expirationDateUtc: string | null; supplierLabelingJson: string | null
  updatedAtUtc: string
}
export type Page<T> = { items: T[]; total: number; page: number; pageSize: number }
export type CategoryWrite = Omit<Category, 'id' | 'updatedAtUtc'> & { updatedAtUtc: string | null }
export type ProductWrite = {
  categoryId: string; sku: string; name: string; slug: string; description: string
  price: number; cost: number; promotionalPrice: number | null
  promotionStartUtc: string | null; promotionEndUtc: string | null
  stockQuantity: number; lowStockThreshold: number; unit: string; weightGrams: number | null
  presentation: string | null; lotNumber: string | null; expirationDateUtc: string | null
  supplierLabelingJson: string | null; isFeatured: boolean; isActive: boolean
  inventoryReason: string | null; updatedAtUtc: string | null
}
export type ProductFilters = { search: string; categoryId: string; sort: string; page: number }

@Injectable({ providedIn: 'root' })
export class Catalog {
  categories(admin = false): Promise<Category[]> {
    return apiRequest(admin ? '/api/v1/admin/categories' : '/api/v1/catalog/categories')
  }

  products(filters: ProductFilters): Promise<Page<Product>> {
    return apiRequest(`/api/v1/catalog/products?${this.query(filters)}`)
  }

  adminProducts(filters: ProductFilters): Promise<Page<AdminProduct>> {
    return apiRequest(`/api/v1/admin/products?${this.query(filters)}`)
  }

  product(slug: string): Promise<Product> {
    return apiRequest(`/api/v1/catalog/products/${encodeURIComponent(slug)}`)
  }

  productById(id: string): Promise<Product> {
    return apiRequest(`/api/v1/catalog/products/by-id/${encodeURIComponent(id)}`)
  }

  adminProduct(id: string): Promise<AdminProduct> {
    return apiRequest(`/api/v1/admin/products/${encodeURIComponent(id)}`)
  }

  saveCategory(id: string | null, value: CategoryWrite): Promise<Category> {
    return csrfRequest(`/api/v1/admin/categories${id ? `/${encodeURIComponent(id)}` : ''}`, {
      method: id ? 'PUT' : 'POST', body: JSON.stringify(value),
    })
  }

  saveProduct(id: string | null, value: ProductWrite): Promise<AdminProduct> {
    return csrfRequest(`/api/v1/admin/products${id ? `/${encodeURIComponent(id)}` : ''}`, {
      method: id ? 'PUT' : 'POST', body: JSON.stringify(value),
    })
  }

  uploadImage(id: string, file: File, altText: string): Promise<ProductImage> {
    const data = new FormData()
    data.append('file', file)
    data.append('altText', altText)
    return csrfRequest(`/api/v1/admin/products/${encodeURIComponent(id)}/images`, {
      method: 'POST', body: data,
    })
  }

  updateImage(id: string, imageId: string, altText: string, sortOrder: number): Promise<ProductImage> {
    return csrfRequest(`/api/v1/admin/products/${encodeURIComponent(id)}/images/${encodeURIComponent(imageId)}`, {
      method: 'PUT', body: JSON.stringify({ altText, sortOrder }),
    })
  }

  deleteImage(id: string, imageId: string): Promise<void> {
    return csrfRequest(`/api/v1/admin/products/${encodeURIComponent(id)}/images/${encodeURIComponent(imageId)}`, {
      method: 'DELETE',
    })
  }

  private query(filters: ProductFilters): string {
    const query = new URLSearchParams({
      search: filters.search, sort: filters.sort, page: String(filters.page), pageSize: '12',
    })
    if (filters.categoryId) query.set('categoryId', filters.categoryId)
    return query.toString()
  }
}

export function slugify(value: string): string {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')
}
