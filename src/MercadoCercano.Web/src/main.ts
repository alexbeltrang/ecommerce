import { bootstrapApplication } from '@angular/platform-browser'
import { registerLocaleData } from '@angular/common'
import localeEsCo from '@angular/common/locales/es-CO'
import { App } from './app/app'
import { appConfig } from './app/app.config'

registerLocaleData(localeEsCo)

bootstrapApplication(App, appConfig).catch((error: unknown) => {
  console.error('No fue posible iniciar Mercado Cercano.', error)
})
