import { DatePipe } from '@angular/common'
import { Component, HostListener, inject, signal, type OnInit } from '@angular/core'
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms'
import { RouterLink } from '@angular/router'
import type { Page } from '../../services/catalog'
import type { UserRole } from '../../services/auth'
import { matchingPasswords, passwordValidators } from '../../services/password-rules'
import { Users, type UserAccount } from '../../services/users'
import { Session } from '../../services/session'

@Component({
  imports: [DatePipe, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './users-page.html',
})
export class UsersPage implements OnInit {
  private readonly api = inject(Users)
  readonly session = inject(Session)
  private readonly fb = inject(FormBuilder)
  readonly result = signal<Page<UserAccount> | null>(null)
  readonly editing = signal<UserAccount | null>(null)
  readonly busy = signal(false)
  readonly loading = signal(false)
  readonly error = signal('')
  readonly message = signal('')
  term = ''
  role = ''
  active = ''
  page = 1
  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(160), Validators.pattern(/\S/)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    role: ['OrderOperator' as UserRole], isActive: [true],
  })
  readonly credentials = this.fb.nonNullable.group({
    password: ['', passwordValidators], confirmation: ['', [Validators.required, Validators.maxLength(32)]],
  }, { validators: matchingPasswords })
  readonly resetForm = this.fb.nonNullable.group({
    password: ['', passwordValidators], confirmation: ['', [Validators.required, Validators.maxLength(32)]],
  }, { validators: matchingPasswords })
  ngOnInit(): void { void this.load() }
  canLeave(): boolean {
    return !this.session.user() || (!this.busy() && (!this.dirty() || window.confirm('¿Descartar los cambios de usuarios sin guardar?')))
  }
  private dirty(): boolean { return this.form.dirty || this.credentials.dirty || this.resetForm.dirty }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.busy() || this.dirty()) { event.preventDefault(); event.returnValue = '' }
  }
  private fill(item: UserAccount | null): void {
    this.editing.set(item)
    this.form.enable()
    this.form.reset(item ?? { name: '', email: '', role: 'OrderOperator', isActive: true })
    this.credentials.reset(); this.resetForm.reset()
    if (item?.id === this.session.user()?.id) { this.form.controls.role.disable(); this.form.controls.isActive.disable() }
  }
  reset(): void {
    if (this.busy() || (this.dirty() && !window.confirm('¿Descartar los cambios actuales?'))) return
    this.fill(null); this.error.set(''); this.message.set('')
  }
  async load(): Promise<void> {
    if (this.loading()) return
    this.loading.set(true); this.error.set('')
    try { this.result.set(await this.api.list(this.term, this.role, this.active, this.page)) }
    catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.loading.set(false) }
  }
  search(): void { this.page = 1; void this.load() }
  changePage(delta: number): void { this.page += delta; void this.load() }
  async edit(id: string): Promise<void> {
    if (this.busy() || (this.dirty() && !window.confirm('¿Descartar los cambios actuales?'))) return
    this.busy.set(true); this.error.set(''); this.message.set('')
    try { this.fill(await this.api.get(id)) }
    catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
  async save(): Promise<void> {
    if (this.busy()) return
    this.form.markAllAsTouched()
    const previous = this.editing()
    if (!previous) this.credentials.markAllAsTouched()
    if (this.form.invalid || (!previous && this.credentials.invalid)) return
    if (previous && !window.confirm('Se cerrarán todas las sesiones de esta cuenta. ¿Guardar los cambios?')) return
    this.busy.set(true); this.error.set(''); this.message.set('')
    try {
      const value = this.form.getRawValue()
      const profile = { ...value, name: value.name.trim(), email: value.email.trim() }
      const saved = previous ? await this.api.update(previous, profile) : await this.api.create(profile, this.credentials.controls.password.value)
      this.fill(saved)
      if (previous?.id === this.session.user()?.id) { await this.session.endSession('account-updated'); return }
      this.message.set(previous ? 'Cuenta actualizada; sus sesiones anteriores quedaron revocadas.' : 'Cuenta creada con clave temporal. Comunícala por un canal privado; deberá cambiarla al ingresar.')
      await this.load()
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
  async resetPassword(): Promise<void> {
    const user = this.editing()
    if (!user || this.busy()) return
    if (this.form.dirty) { this.error.set('Guarda o recarga los datos del perfil antes de restablecer su contraseña.'); return }
    this.resetForm.markAllAsTouched()
    if (this.resetForm.invalid || !window.confirm('¿Asignar la clave temporal y cerrar todas las sesiones de esta cuenta?')) return
    this.busy.set(true); this.error.set(''); this.message.set('')
    try {
      this.fill(await this.api.reset(user, this.resetForm.controls.password.value))
      this.message.set('Contraseña temporal asignada y sesiones revocadas. Entrega la clave por un canal privado; no se envió por correo.')
      await this.load()
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
}
