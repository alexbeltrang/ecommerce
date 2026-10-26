import { Component, inject, signal, type OnInit } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { Catalog, slugify, type AdminProduct, type Category, type ProductImage } from '../../services/catalog'
import { Session } from '../../services/session'

const integer = (minimum: number, maximum = 2147483647) => [
  Validators.min(minimum), Validators.max(maximum), Validators.pattern(/^\d+$/),
]

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './product-editor.html',
})
export class ProductEditor implements OnInit {
  private readonly catalog = inject(Catalog)
  private readonly session = inject(Session)
  private readonly router = inject(Router)
  private readonly builder = inject(FormBuilder)
  readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')
  readonly categories = signal<Category[]>([])
  readonly product = signal<AdminProduct | null>(null)
  readonly loading = signal(false)
  readonly ready = signal(false)
  readonly busy = signal(false)
  readonly imageBusy = signal(false)
  readonly error = signal('')
  readonly message = signal('')
  readonly form = this.builder.nonNullable.group({
    categoryId: ['', Validators.required],
    sku: ['', [Validators.required, Validators.maxLength(80)]],
    name: ['', [Validators.required, Validators.maxLength(180)]],
    slug: ['', [Validators.required, Validators.maxLength(200), Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)]],
    description: ['', [Validators.required, Validators.maxLength(10000)]],
    price: [0, [Validators.required, ...integer(1, 9_000_000_000_000)]],
    cost: [0, [Validators.required, ...integer(0, 9_000_000_000_000)]],
    promotionalPrice: this.builder.control<number | null>(null, integer(1, 9_000_000_000_000)),
    promotionStartUtc: [''],
    promotionEndUtc: [''],
    stockQuantity: [0, [Validators.required, ...integer(0)]],
    lowStockThreshold: [2, [Validators.required, ...integer(0)]],
    unit: ['unidad', [Validators.required, Validators.maxLength(40)]],
    weightGrams: this.builder.control<number | null>(null, integer(1)),
    presentation: ['', Validators.maxLength(300)],
    lotNumber: ['', Validators.maxLength(120)],
    expirationDateUtc: [''],
    supplierLabelingJson: ['', Validators.maxLength(10000)],
    isFeatured: [false],
    isActive: [true],
    inventoryReason: ['', Validators.maxLength(1000)],
  })

  ngOnInit(): void { void this.load() }
  suggestSlug(): void {
    if (!this.form.controls.slug.value) this.form.controls.slug.setValue(slugify(this.form.controls.name.value))
  }

  reload(): void {
    if (!this.form.dirty || window.confirm('¿Descartar los cambios sin guardar y recargar el producto?')) void this.load()
  }

  async load(): Promise<void> {
    this.loading.set(true)
    this.error.set('')
    try {
      const [categories, product] = await Promise.all([
        this.catalog.categories(true),
        this.id ? this.catalog.adminProduct(this.id) : Promise.resolve(null),
      ])
      this.categories.set(categories)
      if (product) {
        this.product.set(product)
        this.form.controls.sku.setValue(product.product.sku)
        this.form.reset({
          ...product.product, cost: product.cost, lowStockThreshold: product.lowStockThreshold,
          isActive: product.isActive, promotionalPrice: product.promotionalPrice,
          promotionStartUtc: this.localDate(product.promotionStartUtc),
          promotionEndUtc: this.localDate(product.promotionEndUtc),
          lotNumber: product.lotNumber ?? '', expirationDateUtc: this.localDate(product.expirationDateUtc),
          supplierLabelingJson: product.supplierLabelingJson ?? '',
          presentation: product.product.presentation ?? '', inventoryReason: '',
        })
      }
      this.ready.set(true)
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.loading.set(false) }
  }

  async save(): Promise<void> {
    if (this.busy() || this.imageBusy()) return
    this.form.markAllAsTouched()
    if (this.form.invalid) {
      this.error.set('Revisa los campos. Los precios COP y las cantidades deben ser enteros dentro de los límites indicados.')
      return
    }
    this.busy.set(true)
    this.error.set('')
    this.message.set('')
    try {
      const value = this.form.getRawValue()
      const product = await this.catalog.saveProduct(this.id, {
        ...value,
        promotionStartUtc: this.utcDate(value.promotionStartUtc),
        promotionEndUtc: this.utcDate(value.promotionEndUtc),
        expirationDateUtc: this.utcDate(value.expirationDateUtc),
        updatedAtUtc: this.product()?.updatedAtUtc ?? null,
      })
      this.product.set(product)
      this.form.controls.inventoryReason.reset('')
      this.form.markAsPristine()
      this.message.set('Producto guardado.')
      if (!this.id) await this.router.navigate(['/admin/productos', product.product.id], { replaceUrl: true })
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }

  async upload(input: HTMLInputElement, altText: string): Promise<void> {
    const file = input.files?.[0]
    if (!file) { this.error.set('Selecciona una imagen.'); return }
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || file.size === 0 || file.size > 750 * 1024) {
      this.error.set('Usa PNG, JPEG o WebP de hasta 750 KiB.')
      return
    }
    await this.imageAction(async (id) => {
      const image = await this.catalog.uploadImage(id, file, altText)
      this.updateImages([...this.product()!.product.images, image])
      input.value = ''
    })
  }

  async saveImage(image: ProductImage, altText: string, sortOrder: number): Promise<void> {
    if (!Number.isInteger(sortOrder) || sortOrder < 0 || sortOrder > 2147483647 || altText.length > 300) {
      this.error.set('El orden debe ser un entero no negativo y el texto debe tener hasta 300 caracteres.')
      return
    }
    await this.imageAction(async (id) => {
      const updated = await this.catalog.updateImage(id, image.id, altText, sortOrder)
      this.updateImages(this.product()!.product.images.map((item) => item.id === image.id ? updated : item))
    })
  }

  async deleteImage(image: ProductImage): Promise<void> {
    if (!window.confirm('¿Eliminar esta imagen del producto?')) return
    await this.imageAction(async (id) => {
      await this.catalog.deleteImage(id, image.id)
      this.updateImages(this.product()!.product.images.filter((item) => item.id !== image.id))
    })
  }

  private async imageAction(action: (id: string) => Promise<void>): Promise<void> {
    const product = this.product()
    if (!product || this.imageBusy() || this.busy()) return
    if (this.form.dirty) { this.error.set('Guarda los cambios del producto antes de administrar imágenes.'); return }
    this.imageBusy.set(true)
    this.error.set('')
    this.message.set('')
    try {
      await action(product.product.id)
      this.message.set('Imágenes actualizadas.')
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.imageBusy.set(false) }
  }

  private updateImages(images: ProductImage[]): void {
    this.product.update((current) => current ? {
      ...current, product: { ...current.product, images: images.sort((a, b) => a.sortOrder - b.sortOrder || a.id.localeCompare(b.id)) },
    } : current)
  }
  private localDate(value: string | null): string {
    if (!value) return ''
    const date = new Date(value)
    return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
  }
  private utcDate(value: string): string | null { return value ? new Date(value).toISOString() : null }
}
