import { Component } from '@angular/core'
import { RouterLink } from '@angular/router'

@Component({
  imports: [RouterLink],
  template: `<section class="panel"><h1>Página no encontrada</h1><a routerLink="/">Volver al catálogo</a></section>`,
})
export class NotFound {}
