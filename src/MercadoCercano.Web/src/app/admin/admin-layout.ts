import { Component, inject, signal } from '@angular/core'
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router'
import { Session } from '../../services/session'
import { errorMessage } from '../../services/http'

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <section class="admin-header panel">
      <div><p class="eyebrow">PORTAL ADMINISTRATIVO</p><strong>{{ session.user()?.name }}</strong>
        <span class="badge">{{ session.user()?.role === 'Administrator' ? 'Administrador' : 'Operador de pedidos' }}</span></div>
      <button type="button" class="secondary" (click)="signOut()" [disabled]="busy()">Cerrar sesión</button>
    </section>
    @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
    <nav class="tabs" aria-label="Administración">
      <a routerLink="/admin" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}">Inicio</a>
      <a routerLink="/admin/pedidos" routerLinkActive="active">Pedidos</a>
      <a routerLink="/mi-cuenta">Mi cuenta</a>
      @if (session.user()?.role === 'Administrator') {
        <a routerLink="/admin/categorias" routerLinkActive="active">Categorías</a>
        <a routerLink="/admin/productos" routerLinkActive="active">Productos</a>
        <a routerLink="/admin/promociones" routerLinkActive="active">Promociones y cupones</a>
        <a routerLink="/admin/reportes" routerLinkActive="active">Reportes e indicadores</a>
        <a routerLink="/admin/usuarios" routerLinkActive="active">Usuarios</a>
        <a routerLink="/admin/entregas" routerLinkActive="active">Entregas y checkout</a>
      }
    </nav>
    <router-outlet />
  `,
})
export class AdminLayout {
  readonly session = inject(Session)
  readonly busy = signal(false)
  readonly error = signal('')
  async signOut(): Promise<void> {
    if (this.busy()) return
    this.busy.set(true)
    this.error.set('')
    try { await this.session.signOut() }
    catch (error) { this.error.set(errorMessage(error)) }
    finally { this.busy.set(false) }
  }
}
