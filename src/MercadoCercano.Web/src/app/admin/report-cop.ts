import { Pipe, type PipeTransform } from '@angular/core'

@Pipe({ name: 'reportCop' })
export class ReportCop implements PipeTransform {
  private readonly formatter = new Intl.NumberFormat('es-CO', {
    style: 'currency', currency: 'COP', currencyDisplay: 'code', maximumFractionDigits: 0, minimumFractionDigits: 0,
  })
  transform(value: string): string { return this.formatter.format(BigInt(value)) }
}
