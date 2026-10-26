import { Component, inject, signal, type OnInit } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { Session } from '../../services/session'
import { errorMessage } from '../../services/http'
import { getInitialAdministratorStatus } from '../../services/auth'

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="panel login-panel">
      <p class="eyebrow">ACCESO PRIVADO</p><h1>Iniciar sesión</h1>
      <p>Ingresa con la cuenta asignada por el administrador.</p>
      @if (checkingSetup()) { <p role="status">Comprobando si falta crear el administrador…</p> }
      @if (setupError()) {
        <p role="alert" class="error">{{ setupError() }}</p>
        <button type="button" class="secondary" [disabled]="checkingSetup()" (click)="checkSetup()">Reintentar comprobación inicial</button>
      }
      @if (setupAvailable()) {
        <div class="warning"><p>Todavía no existe ningún usuario. Crea tu primera cuenta de administrador para poder ingresar.</p>
          <a routerLink="/configuracion-inicial">Crear primer administrador</a></div>
      }
      @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
      @if (notice) { <p role="status" class="success">{{ notice }}</p> }
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>Correo electrónico
          <input type="email" formControlName="email" autocomplete="username" maxlength="254" required />
        </label>
        <label>Contraseña
          <input type="password" formControlName="password" autocomplete="current-password" maxlength="256" required />
        </label>
        @if (form.invalid && form.touched) { <p class="error">Escribe un correo válido y tu contraseña.</p> }
        <button type="submit" [disabled]="busy()">{{ busy() ? 'Ingresando…' : 'Ingresar' }}</button>
      </form>
      <p class="muted">No hay credenciales predeterminadas. En desarrollo, el alta inicial solo está disponible mientras no exista ningún usuario.</p>
    </section>
  `,
})
export class LoginPage implements OnInit {
  private readonly session = inject(Session)
  private readonly router = inject(Router)
  private readonly route = inject(ActivatedRoute)
  readonly busy = signal(false)
  readonly checkingSetup = signal(false)
  readonly setupAvailable = signal(false)
  readonly setupError = signal('')
  readonly error = signal(this.route.snapshot.queryParamMap.get('error') ?? '')
  readonly notice = this.route.snapshot.queryParamMap.get('notice') === 'password-changed'
    ? 'Contraseña cambiada. Todas tus sesiones se cerraron; ingresa con la nueva contraseña.'
    : this.route.snapshot.queryParamMap.get('notice') === 'account-updated'
      ? 'Cuenta actualizada. Por seguridad, vuelve a iniciar sesión.' : ''
  readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: ['', [Validators.required, Validators.maxLength(256)]],
  })

  ngOnInit(): void { void this.checkSetup() }

  async checkSetup(): Promise<void> {
    if (this.checkingSetup()) return
    this.checkingSetup.set(true)
    this.setupError.set('')
    try { this.setupAvailable.set((await getInitialAdministratorStatus()).available) }
    catch (error) { this.setupError.set(errorMessage(error)) }
    finally { this.checkingSetup.set(false) }
  }

  async submit(): Promise<void> {
    if (this.busy()) return
    this.form.markAllAsTouched()
    if (this.form.invalid) return
    this.busy.set(true)
    this.error.set('')
    try {
      const value = this.form.getRawValue()
      const user = await this.session.signIn(value.email, value.password)
      this.form.controls.password.reset()
      const requested = this.route.snapshot.queryParamMap.get('returnUrl') ?? ''
      const target = user.mustChangePassword || /^\/mi-cuenta(?:\?|$)/.test(requested) ? '/mi-cuenta'
        : user.role === 'Administrator' && /^\/admin(?:\/|\?|$)/.test(requested) ? requested : '/admin'
      await this.router.navigateByUrl(target)
    } catch (error) {
      this.error.set(errorMessage(error))
    } finally {
      this.busy.set(false)
    }
  }
}
