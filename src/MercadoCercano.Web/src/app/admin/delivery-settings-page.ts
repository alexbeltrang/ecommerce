import { Component, inject, signal, type OnInit } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { ApiError } from '../../services/http'
import { Orders, type CheckoutSettings, type PaymentMethod, type SettingsResponse } from '../../services/orders'
import { Session } from '../../services/session'

const feeValidators = [Validators.required, Validators.min(0), Validators.max(9_000_000_000_000), Validators.pattern(/^\d+$/)]

@Component({
  imports: [ReactiveFormsModule],
  templateUrl: './delivery-settings-page.html',
})
export class DeliverySettingsPage implements OnInit {
  private readonly orders = inject(Orders)
  private readonly session = inject(Session)
  readonly loading = signal(false)
  readonly busy = signal(false)
  readonly available = signal(false)
  readonly missing = signal(false)
  readonly error = signal('')
  readonly message = signal('')
  private version: string | null = null
  readonly form = inject(FormBuilder).nonNullable.group({
    isTestConfiguration: [true], pickupEnabled: [true], pickupMunicipality: ['', Validators.maxLength(120)],
    pickupInstructions: ['', Validators.maxLength(1000)],
    localDeliveryEnabled: [false], localDeliveryFee: [0, feeValidators], localMunicipalities: [''],
    carrierShippingEnabled: [false], carrierShippingFee: [0, feeValidators], carrierNationwide: [false],
    carrierMunicipalities: [''], bankTransfer: [false], digitalWallet: [false], cashOnDelivery: [false],
    paymentInstructions: ['', [Validators.required, Validators.maxLength(1000)]],
    termsText: ['', [Validators.required, Validators.maxLength(5000)]],
  })
  ngOnInit(): void { void this.load() }
  async load(): Promise<void> {
    if (this.busy() || this.loading()) return
    if (this.form.dirty && !window.confirm('¿Recargar y descartar los cambios de configuración?')) return
    this.loading.set(true)
    this.available.set(false)
    this.error.set('')
    this.message.set('')
    this.missing.set(false)
    try { this.populate(await this.orders.settings(true)) }
    catch (error) {
      this.error.set(this.session.adminError(error))
      this.missing.set(error instanceof ApiError && error.status === 503)
    } finally { this.loading.set(false) }
  }
  startNew(): void {
    this.available.set(true)
    this.version = null
    this.message.set('Completa todos los datos para crear la configuración inicial. No se inventaron tarifas ni datos del negocio.')
  }
  async save(): Promise<void> {
    if (this.busy() || !this.available()) return
    this.form.markAllAsTouched()
    if (this.form.invalid) { this.error.set('Revisa las longitudes y las tarifas: usa enteros no negativos hasta 9 billones COP.'); return }
    const values = this.form.getRawValue()
    const paymentMethods: PaymentMethod[] = []
    if (values.bankTransfer) paymentMethods.push('BankTransfer')
    if (values.digitalWallet) paymentMethods.push('DigitalWallet')
    if (values.cashOnDelivery) paymentMethods.push('CashOnDelivery')
    const settings: CheckoutSettings = {
      isTestConfiguration: values.isTestConfiguration, pickupEnabled: values.pickupEnabled,
      pickupMunicipality: values.pickupMunicipality.trim(), pickupInstructions: values.pickupInstructions.trim(),
      localDeliveryEnabled: values.localDeliveryEnabled, localDeliveryFee: values.localDeliveryFee,
      localMunicipalities: this.municipalities(values.localMunicipalities),
      carrierShippingEnabled: values.carrierShippingEnabled, carrierShippingFee: values.carrierShippingFee,
      carrierNationwide: values.carrierNationwide, carrierMunicipalities: this.municipalities(values.carrierMunicipalities),
      paymentMethods, paymentInstructions: values.paymentInstructions.trim(), termsText: values.termsText.trim(),
    }
    if (!settings.isTestConfiguration && !window.confirm('Vas a publicar tarifas, cobertura, instrucciones y condiciones para pedidos reales. ¿Verificaste todos los datos?')) return
    this.busy.set(true)
    this.error.set('')
    this.message.set('')
    try {
      this.populate(await this.orders.saveSettings(settings, this.version))
      this.message.set('Configuración guardada. Las nuevas cotizaciones usarán estas reglas; los pedidos existentes conservan sus importes e instrucciones.')
    } catch (error) { this.error.set(this.session.adminError(error)) }
    finally { this.busy.set(false) }
  }
  private municipalities(text: string): string[] { return text.split(/\r?\n/).map((line) => line.trim()).filter(Boolean) }
  private populate(response: SettingsResponse): void {
    const values = response.values
    this.version = response.updatedAtUtc
    this.form.reset({
      ...values, localMunicipalities: values.localMunicipalities.join('\n'), carrierMunicipalities: values.carrierMunicipalities.join('\n'),
      bankTransfer: values.paymentMethods.includes('BankTransfer'), digitalWallet: values.paymentMethods.includes('DigitalWallet'),
      cashOnDelivery: values.paymentMethods.includes('CashOnDelivery'),
    })
    this.available.set(true)
    this.missing.set(false)
  }
}
