import { Injectable, inject, signal } from '@angular/core'
import { Router, type CanActivateFn } from '@angular/router'
import { getCurrentUser, login, logout, type CurrentUser } from './auth'
import { ApiError, errorMessage } from './http'

@Injectable({ providedIn: 'root' })
export class Session {
  private readonly router = inject(Router)
  private pending: Promise<CurrentUser | null> | undefined
  readonly user = signal<CurrentUser | null>(null)

  refresh(): Promise<CurrentUser | null> {
    if (this.pending) return this.pending
    this.pending = getCurrentUser()
      .then((user) => { this.user.set(user); return user })
      .catch((error: unknown) => {
        if (error instanceof ApiError && error.status === 401) {
          this.user.set(null)
          return null
        }
        throw error
      })
      .finally(() => { this.pending = undefined })
    return this.pending
  }

  async signIn(email: string, password: string): Promise<CurrentUser> {
    const user = await login(email, password)
    this.user.set(user)
    return user
  }

  async signOut(): Promise<void> {
    try {
      await logout()
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401) throw error
    }
    this.user.set(null)
    await this.router.navigateByUrl('/login')
  }

  async endSession(notice: 'password-changed' | 'account-updated'): Promise<void> {
    this.user.set(null)
    await this.router.navigate(['/login'], { queryParams: { notice } })
  }

  adminError(error: unknown): string {
    if (error instanceof ApiError && error.status === 401) {
      this.user.set(null)
      void this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } })
    }
    return errorMessage(error)
  }
}

export const authGuard: CanActivateFn = async (_route, state) => {
  const session = inject(Session)
  const router = inject(Router)
  try {
    const user = await session.refresh()
    if (!user) return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } })
    return user.mustChangePassword && !/^\/mi-cuenta(?:[?#]|$)/.test(state.url)
      ? router.createUrlTree(['/mi-cuenta']) : true
  } catch (error) {
    return router.createUrlTree(['/login'], { queryParams: { error: errorMessage(error) } })
  }
}

export const administratorGuard: CanActivateFn = async () => {
  const session = inject(Session)
  const router = inject(Router)
  try {
    const user = await session.refresh()
    if (!user) return router.createUrlTree(['/login'])
    if (user.mustChangePassword) return router.createUrlTree(['/mi-cuenta'])
    return user.role === 'Administrator'
      ? true
      : router.createUrlTree(['/admin'], { queryParams: { denied: 'true' } })
  } catch (error) {
    return router.createUrlTree(['/login'], { queryParams: { error: errorMessage(error) } })
  }
}
