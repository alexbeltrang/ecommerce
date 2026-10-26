import type { CanDeactivateFn, Routes } from '@angular/router'
import { inject } from '@angular/core'
import { Session, administratorGuard, authGuard } from '../services/session'

const unsavedChanges: CanDeactivateFn<{ form: { dirty: boolean } }> = (component) =>
  !inject(Session).user() || !component.form.dirty || window.confirm('¿Salir y descartar los cambios sin guardar?')
const checkoutChanges: CanDeactivateFn<{ canLeave: () => boolean }> = (component) => component.canLeave()

export const routes: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./catalog/catalog-page').then((module) => module.CatalogPage) },
  { path: 'productos/:slug', loadComponent: () => import('./catalog/product-detail').then((module) => module.ProductDetail) },
  { path: 'carrito', loadComponent: () => import('./checkout/cart-page').then((module) => module.CartPage) },
  { path: 'checkout', canDeactivate: [checkoutChanges], loadComponent: () => import('./checkout/checkout-page').then((module) => module.CheckoutPage) },
  { path: 'seguimiento', canDeactivate: [checkoutChanges], loadComponent: () => import('./checkout/tracking-page').then((module) => module.TrackingPage) },
  { path: 'pedido/:number', canDeactivate: [checkoutChanges], loadComponent: () => import('./checkout/tracking-page').then((module) => module.TrackingPage) },
  { path: 'login', loadComponent: () => import('./auth/login-page').then((module) => module.LoginPage) },
  { path: 'mi-cuenta', canActivate: [authGuard], canDeactivate: [checkoutChanges], loadComponent: () => import('./auth/account-page').then((module) => module.AccountPage) },
  { path: 'configuracion-inicial', canDeactivate: [checkoutChanges], loadComponent: () => import('./auth/initial-administrator-page').then((module) => module.InitialAdministratorPage) },
  {
    path: 'admin',
    canActivate: [authGuard],
    loadComponent: () => import('./admin/admin-layout').then((module) => module.AdminLayout),
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./admin/dashboard').then((module) => module.Dashboard) },
      { path: 'entregas', canActivate: [administratorGuard], canDeactivate: [unsavedChanges], loadComponent: () => import('./admin/delivery-settings-page').then((module) => module.DeliverySettingsPage) },
      { path: 'promociones', canActivate: [administratorGuard], canDeactivate: [checkoutChanges], loadComponent: () => import('./admin/promotions-page').then((module) => module.PromotionsPage) },
      { path: 'reportes', canActivate: [administratorGuard], loadComponent: () => import('./admin/reports-page').then((module) => module.ReportsPage) },
      { path: 'usuarios', canActivate: [administratorGuard], canDeactivate: [checkoutChanges], loadComponent: () => import('./admin/users-page').then((module) => module.UsersPage) },
      { path: 'pedidos', loadComponent: () => import('./admin/orders-page').then((module) => module.OrdersPage) },
      { path: 'pedidos/:id', canDeactivate: [checkoutChanges], loadComponent: () => import('./admin/order-detail').then((module) => module.OrderDetail) },
      { path: 'categorias', canActivate: [administratorGuard], canDeactivate: [unsavedChanges], loadComponent: () => import('./admin/categories-page').then((module) => module.CategoriesPage) },
      { path: 'productos', canActivate: [administratorGuard], loadComponent: () => import('./admin/products-page').then((module) => module.ProductsPage) },
      { path: 'productos/nuevo', canActivate: [administratorGuard], canDeactivate: [unsavedChanges], loadComponent: () => import('./admin/product-editor').then((module) => module.ProductEditor) },
      { path: 'productos/:id', canActivate: [administratorGuard], canDeactivate: [unsavedChanges], loadComponent: () => import('./admin/product-editor').then((module) => module.ProductEditor) },
    ],
  },
  { path: '**', loadComponent: () => import('./not-found').then((module) => module.NotFound) },
]
