import { Component, inject } from '@angular/core'
import { RouterLink, RouterOutlet } from '@angular/router'
import { Session } from '../services/session'
import { Cart } from '../services/cart'

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.html',
})
export class App {
  readonly session = inject(Session)
  readonly cart = inject(Cart)
}
