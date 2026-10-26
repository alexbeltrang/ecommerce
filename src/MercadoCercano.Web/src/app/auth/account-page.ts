import { Component, HostListener, inject, signal } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { changeOwnPassword } from '../../services/auth'
import { matchingPasswords, passwordValidators } from '../../services/password-rules'
import { Session } from '../../services/session'

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="panel login-panel">
      <h1>Mi cuenta</h1>
      <p>{{ session.user()?.name }} · {{ session.user()?.email }}</p>
      <p>Rol: {{ session.user()?.role === 'Administrator' ? 'Administrador' : 'Operador de pedidos' }}.</p>
      @if (session.user()?.mustChangePassword) {
        <p class="warning">Debes reemplazar tu contraseña temporal antes de acceder al portal. Introduce la temporal como contraseña actual.</p>
      } @else { <p><a routerLink="/admin">Volver al portal</a></p> }
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <form [formGroup]="form" (ngSubmit)="submit()">
        <fieldset [disabled]="busy()">
          <legend>Cambiar mi contraseña</legend>
          <label>Contraseña actual<input type="password" formControlName="currentPassword" autocomplete="current-password" maxlength="256" required /></label>
          <label>Nueva contraseña<input type="password" formControlName="password" autocomplete="new-password" minlength="8" maxlength="32" required /></label>
          <label>Confirmar nueva contraseña<input type="password" formControlName="confirmation" autocomplete="new-password" minlength="8" maxlength="32" required /></label>
          <p>De 8 a 32 caracteres, mayúscula, minúscula, número y carácter especial, sin espacios. Debe ser distinta de la actual.</p>
          @if (form.touched && form.invalid) { <p class="error">Revisa la contraseña actual, los requisitos y que la nueva coincida en ambos campos.</p> }
          <p class="warning">Se cerrarán todas tus sesiones, incluida esta. Deberás ingresar con la nueva contraseña.</p>
          <button>{{ busy() ? 'Guardando…' : 'Cambiar contraseña y cerrar sesiones' }}</button>
        </fieldset>
      </form>
      <button type="button" class="secondary" [disabled]="busy()" (click)="signOut()">Cerrar sesión</button>
      <p class="muted">Si se pierde la respuesta al cambiar la clave, prueba iniciar sesión con la nueva antes de repetir el cambio. No hay recuperación automática por correo.</p>
    </section>
  `,
})
export class AccountPage {
  readonly session = inject(Session)
  readonly busy = signal(false)
  readonly error = signal('')
  private completed = false
  readonly form = inject(FormBuilder).nonNullable.group({
    currentPassword: ['', [Validators.required, Validators.maxLength(256)]],
    password: ['', passwordValidators], confirmation: ['', [Validators.required, Validators.maxLength(32)]],
  }, { validators: matchingPasswords })
  canLeave(): boolean {
    return this.completed || !this.session.user() || (!this.busy() && (!this.form.dirty || window.confirm('¿Descartar el cambio de contraseña sin guardar?')))
  }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (!this.completed && (this.busy() || this.form.dirty)) { event.preventDefault(); event.returnValue = '' }
  }
  async submit(): Promise<void> {
    if (this.busy()) return
    this.form.markAllAsTouched()
    if (this.form.invalid) return
    this.busy.set(true); this.error.set('')
    try {
      const value = this.form.getRawValue()
      await changeOwnPassword(value.currentPassword, value.password)
      this.form.reset(); this.completed = true
      await this.session.endSession('password-changed')
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
  async signOut(): Promise<void> {
    if (this.busy()) return
    this.busy.set(true); this.error.set('')
    try { await this.session.signOut() }
    catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
}
