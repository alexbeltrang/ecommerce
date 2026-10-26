import { apiRequest } from './http'

export type UserRole = 'Administrator' | 'OrderOperator'
export type CurrentUser = { id: string; name: string; email: string; role: UserRole; mustChangePassword: boolean }
export type InitialAdministratorStatus = { available: boolean }

export function getInitialAdministratorStatus(): Promise<InitialAdministratorStatus> {
  return apiRequest('/api/v1/auth/initial-administrator')
}

export function createInitialAdministrator(value: { name: string; email: string; password: string }): Promise<{ id: string }> {
  return csrfRequest('/api/v1/auth/initial-administrator', { method: 'POST', body: JSON.stringify(value) })
}

async function getCsrfToken(): Promise<string> {
  const response = await apiRequest<{ requestToken: string }>('/api/v1/auth/csrf')
  if (typeof response.requestToken !== 'string' || response.requestToken.length === 0) {
    throw new Error('La API no devolvió un token antifalsificación válido.')
  }
  return response.requestToken
}

export async function csrfRequest<T>(path: string, options: RequestInit): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('X-CSRF-TOKEN', await getCsrfToken())
  return apiRequest<T>(path, { ...options, headers })
}

export function login(email: string, password: string): Promise<CurrentUser> {
  return csrfRequest<CurrentUser>('/api/v1/auth/login', {
    method: 'POST', body: JSON.stringify({ email, password }),
  })
}

export function getCurrentUser(): Promise<CurrentUser> {
  return apiRequest<CurrentUser>('/api/v1/auth/me')
}

export function logout(): Promise<void> {
  return csrfRequest<void>('/api/v1/auth/logout', { method: 'POST' })
}

export function changeOwnPassword(currentPassword: string, newPassword: string): Promise<void> {
  return csrfRequest('/api/v1/account/password', { method: 'POST', body: JSON.stringify({ currentPassword, newPassword }) })
}
