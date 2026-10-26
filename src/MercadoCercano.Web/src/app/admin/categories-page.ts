import { Component, inject, signal, type OnInit } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { Catalog, slugify, type Category } from '../../services/catalog'
import { Session } from '../../services/session'

@Component({
  imports: [ReactiveFormsModule],
  template: `
    <div class="section-title"><h1>Categorías</h1><button type="button" class="secondary" (click)="reset()">Nueva categoría</button></div>
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    @if (message()) { <p class="success" role="status">{{ message() }}</p> }
    <div class="admin-columns">
      <section class="panel">
        <h2>{{ editing() ? 'Editar categoría' : 'Crear categoría' }}</h2>
        <form [formGroup]="form" (ngSubmit)="save()">
          <label>Nombre<input formControlName="name" required maxlength="120" (blur)="suggestSlug()" /></label>
          <label>URL<input formControlName="slug" required maxlength="140" placeholder="cafe-y-bebidas" /></label>
          <label>Descripción<textarea formControlName="description" maxlength="2000"></textarea></label>
          <label>Orden<input type="number" formControlName="sortOrder" min="0" step="1" required /></label>
          <label class="checkbox"><input type="checkbox" formControlName="isActive" /> Activa en el catálogo</label>
          @if (form.invalid && form.touched) { <p class="error">Revisa los campos: nombre, URL sin espacios y orden entero no negativo.</p> }
          <div class="actions"><button [disabled]="busy()">{{ busy() ? 'Guardando…' : 'Guardar categoría' }}</button>
            <button type="button" class="secondary" (click)="reset()" [disabled]="busy()">Cancelar</button></div>
        </form>
      </section>
      <section class="panel">
        <div class="section-title"><h2>Listado</h2><button class="secondary" (click)="load()" [disabled]="busy()">Recargar</button></div>
        @if (loading()) { <p role="status">Cargando…</p> }
        <div class="table-scroll"><table><thead><tr><th>Orden</th><th>Categoría</th><th>Estado</th><th>Acción</th></tr></thead>
          <tbody>@for (category of categories(); track category.id) {
            <tr><td>{{ category.sortOrder }}</td><td>{{ category.name }}<small>{{ category.slug }}</small></td>
              <td>{{ category.isActive ? 'Activa' : 'Inactiva' }}</td>
              <td><button class="secondary" (click)="edit(category)" [disabled]="busy()">Editar</button></td></tr>
          } @empty { <tr><td colspan="4">No hay categorías.</td></tr> }</tbody></table></div>
        <p class="muted">Al desactivar una categoría, todos sus productos dejan de aparecer públicamente.</p>
      </section>
    </div>
  `,
})
export class CategoriesPage implements OnInit {
  private readonly catalog = inject(Catalog)
  private readonly session = inject(Session)
  readonly categories = signal<Category[]>([])
  readonly editing = signal<Category | null>(null)
  readonly busy = signal(false)
  readonly loading = signal(false)
  readonly error = signal('')
  readonly message = signal('')
  readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    slug: ['', [Validators.required, Validators.maxLength(140), Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)]],
    description: ['', Validators.maxLength(2000)],
    sortOrder: [0, [Validators.required, Validators.min(0), Validators.max(2147483647), Validators.pattern(/^\d+$/)]],
    isActive: [true],
  })

  ngOnInit(): void { void this.load() }
  suggestSlug(): void {
    if (!this.form.controls.slug.value) this.form.controls.slug.setValue(slugify(this.form.controls.name.value))
  }
  reset(): void {
    if (this.busy()) return
    this.editing.set(null)
    this.form.reset({ name: '', slug: '', description: '', sortOrder: 0, isActive: true })
  }
  edit(category: Category): void {
    this.editing.set(category)
    this.message.set('')
    this.form.reset({ ...category, description: category.description ?? '' })
  }
  async load(): Promise<void> {
    this.loading.set(true)
    this.error.set('')
    try { this.categories.set(await this.catalog.categories(true)) }
    catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.loading.set(false) }
  }
  async save(): Promise<void> {
    if (this.busy()) return
    this.form.markAllAsTouched()
    if (this.form.invalid) return
    this.busy.set(true)
    this.error.set('')
    this.message.set('')
    try {
      const editing = this.editing()
      const saved = await this.catalog.saveCategory(editing?.id ?? null, {
        ...this.form.getRawValue(), updatedAtUtc: editing?.updatedAtUtc ?? null,
      })
      this.categories.update((items) => [...items.filter((item) => item.id !== saved.id), saved]
        .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name)))
      this.editing.set(null)
      this.form.reset({ name: '', slug: '', description: '', sortOrder: 0, isActive: true })
      this.message.set('Categoría guardada.')
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
}
