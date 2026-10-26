import { Injectable } from '@angular/core'
import { apiRequest } from './http'
import { csrfRequest, type UserRole } from './auth'
import type { Page } from './catalog'

export type UserAccount = {
  id: string; name: string; email: string; role: UserRole; isActive: boolean; mustChangePassword: boolean
  createdAtUtc: string; updatedAtUtc: string
}
export type UserProfile = { name: string; email: string; role: UserRole; isActive: boolean }

@Injectable({ providedIn: 'root' })
export class Users {
  list(search: string, role: string, active: string, page: number): Promise<Page<UserAccount>> {
    const query = new URLSearchParams({ search, page: String(page), pageSize: '12' })
    if (role) query.set('role', role)
    if (active) query.set('isActive', active)
    return apiRequest(`/api/v1/admin/users?${query}`)
  }
  get(id: string): Promise<UserAccount> { return apiRequest(`/api/v1/admin/users/${encodeURIComponent(id)}`) }
  create(profile: UserProfile, temporaryPassword: string): Promise<UserAccount> {
    return csrfRequest('/api/v1/admin/users', { method: 'POST', body: JSON.stringify({ ...profile, temporaryPassword }) })
  }
  update(user: UserAccount, profile: UserProfile): Promise<UserAccount> {
    return csrfRequest(`/api/v1/admin/users/${encodeURIComponent(user.id)}`, {
      method: 'PUT', body: JSON.stringify({ ...profile, updatedAtUtc: user.updatedAtUtc }),
    })
  }
  reset(user: UserAccount, temporaryPassword: string): Promise<UserAccount> {
    return csrfRequest(`/api/v1/admin/users/${encodeURIComponent(user.id)}/reset-password`, {
      method: 'POST', body: JSON.stringify({ temporaryPassword, updatedAtUtc: user.updatedAtUtc }),
    })
  }
}
