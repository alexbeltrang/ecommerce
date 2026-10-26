import { Component, HostListener, inject, signal, type OnInit } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { createInitialAdministrator, getInitialAdministratorStatus, type InitialAdministratorStatus } from '../../services/auth'
import { ApiError, errorMessage } from '../../services/http'
import { matchingPasswords, passwordValidators } from '../../services/password-rules'

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="panel login-panel">
      <p class="eyebrow">CONFIGURACIÓN LOCAL</p><h1>Crear primer administrador</h1>
      @if (created()) {
        <p role="status" class="success">Administrador creado. Ya puedes iniciar sesión con el correo y la contraseña que elegiste.</p>
        <a routerLink="/login">Continuar al inicio de sesión</a>
        <p>Después entra en Productos, edita el producto de prueba, marca Activo y guarda para publicarlo.</p>
      } @else {
        <p>Disponible únicamente en desarrollo y mientras no exista ningún usuario. No se crean contraseñas predeterminadas.</p>
        @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
        @if (checking()) { <p role="status">Comprobando disponibilidad…</p> }
        @if (!status() && !checking()) {
          <button type="button" (click)="load()">Reintentar comprobación</button>
        }
        @if (status()?.available) {
          <form [formGroup]="form" (ngSubmit)="submit()">
            <label>Nombre<input formControlName="name" autocomplete="name" maxlength="160" required /></label>
            <label>Correo electrónico<input type="email" formControlName="email" autocomplete="username" maxlength="254" required /></label>
            <label>Contraseña<input type="password" formControlName="password" autocomplete="new-password" minlength="8" maxlength="32" required /></label>
            <p class="muted">Entre 8 y 32 caracteres, con al menos una mayúscula, una minúscula, un número y un carácter especial, sin espacios.</p>
            <label>Confirmar contraseña<input type="password" formControlName="confirmation" autocomplete="new-password" minlength="8" maxlength="32" required /></label>
            @if (form.touched && form.invalid) {
              <p class="error">Revisa nombre, correo, requisitos de contraseña y que ambas contraseñas coincidan.</p>
            }
            <button type="submit" [disabled]="busy()">{{ busy() ? 'Creando…' : 'Crear administrador' }}</button>
          </form>
          @if (error()) {
            <p>Si se perdió la respuesta, comprueba el estado e intenta iniciar sesión con los datos elegidos antes de volver a crear.</p>
            <button type="button" class="secondary" [disabled]="busy() || checking()" (click)="load()">Comprobar estado</button>
          }
        } @else if (status()) {
          <p>El alta inicial está cerrada: ya existe un usuario o este entorno no es de desarrollo.
            Si perdiste la respuesta al crear tu cuenta, intenta iniciar sesión con los datos que elegiste.</p>
        }
        <p><a routerLink="/login">Volver al inicio de sesión</a></p>
      }
    </section>
  `,
})
export class InitialAdministratorPage implements OnInit {
  readonly status = signal<InitialAdministratorStatus | null>(null)
  readonly checking = signal(false)
  readonly busy = signal(false)
  readonly created = signal(false)
  readonly error = signal('')
  readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(160), Validators.pattern(/\S/)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: ['', passwordValidators],
    confirmation: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(32)]],
  }, { validators: matchingPasswords })

  ngOnInit(): void { void this.load() }

  canLeave(): boolean {
    return this.created() || (!this.busy() && (!this.form.dirty || window.confirm('¿Salir y descartar los datos del administrador sin guardar?')))
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (!this.created() && (this.busy() || this.form.dirty)) {
      event.preventDefault()
      event.returnValue = ''
    }
  }

  async load(): Promise<void> {
    if (this.checking() || this.busy()) return
    this.checking.set(true)
    this.error.set('')
    try { this.status.set(await getInitialAdministratorStatus()) }
    catch (error) { this.error.set(errorMessage(error)) }
    finally { this.checking.set(false) }
  }

  async submit(): Promise<void> {
    if (this.busy() || this.checking() || !this.status()?.available) return
    this.form.markAllAsTouched()
    if (this.form.invalid) return
    this.busy.set(true)
    this.error.set('')
    try {
      const { name, email, password } = this.form.getRawValue()
      await createInitialAdministrator({ name, email, password })
      this.form.reset()
      this.created.set(true)
      this.status.set({ available: false })
    } catch (error) {
      this.error.set(errorMessage(error))
      if (error instanceof ApiError && (error.status === 404 || error.status === 409)) {
        this.status.set({ available: false })
        this.form.reset()
      }
    } finally { this.busy.set(false) }
  }
}
