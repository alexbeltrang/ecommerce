import { Component, inject } from '@angular/core'
import { ActivatedRoute, RouterLink } from '@angular/router'
import { Session } from '../../services/session'

@Component({
  imports: [RouterLink],
  template: `
    <section class="panel">
      <h1>Bienvenido, {{ session.user()?.name }}</h1>
      @if (denied) { <p role="alert" class="error">Tu rol no permite acceder a esa sección administrativa.</p> }
      @if (session.user()?.role === 'Administrator') {
        <p>Administra la información real de la tienda. Los cambios se guardan en SQLite.</p>
        <div class="actions">
          <a class="button" routerLink="/admin/categorias">Gestionar categorías</a>
          <a class="button" routerLink="/admin/productos">Gestionar productos</a>
          <a class="button" routerLink="/admin/promociones">Promociones y cupones</a>
          <a class="button" routerLink="/admin/reportes">Reportes e indicadores</a>
          <a class="button" routerLink="/admin/usuarios">Administrar usuarios</a>
          <a class="button" routerLink="/admin/entregas">Configurar entregas y pagos</a>
        </div>
        <p class="muted">Desactiva productos o categorías para retirarlos del catálogo sin eliminar su historial.</p>
      } @else {
        <p>Tu acceso como operador permite gestionar pedidos, validar pagos, preparar, despachar, entregar y recibir devoluciones. Solo el administrador registra reembolsos.</p>
        <a routerLink="/">Consultar catálogo público</a>
      }
      <div class="actions"><a class="button" routerLink="/admin/pedidos">Gestionar pedidos</a></div>
    </section>
  `,
})
export class Dashboard {
  readonly session = inject(Session)
  readonly denied = inject(ActivatedRoute).snapshot.queryParamMap.has('denied')
}
