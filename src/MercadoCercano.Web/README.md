# Mercado Cercano Web

Frontend Angular 21 con componentes standalone, signals, TypeScript estricto
y Angular CLI. No usa React.

## Desarrollo

Con Node.js compatible con Angular 21 y npm:

```powershell
npm ci
npm run dev
```

La interfaz se sirve en `http://localhost:5173`. Para abrir el navegador:

```powershell
npm run dev -- --open
```

El archivo [proxy.conf.json](proxy.conf.json) redirige `/api/**` a la API
en `http://127.0.0.1:5136`. No se usan variables de Vite ni `.env.local`.
La API debe estar iniciada antes de abrir la interfaz.

Desde la raiz del repositorio tambien puedes seleccionar
**Mercado Cercano: aplicacion completa** y pulsar **F5** para iniciar la API,
Angular y Swagger. Consulta el [README principal](../../README.md).

## Validacion

```powershell
npm run build
npm run lint
```

La compilacion de produccion queda en `dist/mercado-cercano-web/browser`.
No publiques el servidor de desarrollo de Angular CLI en produccion.
Para Windows/IIS, MSBuild incluye esa compilacion en wwwroot de la API al
publicar. Angular y API usan el mismo origen HTTPS; no hay proxy de desarrollo.
Consulta [production.md](../../docs/production.md). La optimizacion evita
manejadores JavaScript inline para respetar la CSP de produccion.

## Estructura

- `src/app/catalog`: listado, filtros, paginacion y detalle publicos.
- `src/app/auth`: login, alta inicial local y cambio personal de contraseña.
- `src/app/admin`: portal segun rol, categorias, productos, imagenes, entregas, promociones, pedidos, reportes y usuarios.
- `src/app/checkout`: carrito, formulario de compra y seguimiento privado.
- `src/app/app.routes.ts`: rutas lazy y guards de sesion, rol y cambios sin guardar.
- `src/services`: cliente HTTP sobre fetch, autenticacion CSRF y servicios
  inyectables de sesion, catalogo, carrito y pedidos. Conservan cookies del
  mismo origen, sin guardar credenciales administrativas en almacenamiento
  del navegador. El carrito guarda solo IDs/cantidades en localStorage y el
  intento de compra guarda su identificador/codigo privado en sessionStorage.
- `src/styles.css`: estilos globales.

La portada muestra el catalogo y `/login` permite entrar en `/admin`.
Si Development no tiene usuarios, el login ofrece **Crear primer administrador**
en `/configuracion-inicial`. Elige tus propias credenciales; una vez creado
cualquier usuario, la API cierra ese alta. No es registro publico en produccion.
Las consultas se realizan al navegar o usar los formularios; no hay sondeos
desde terminales ni reintentos periodicos.

Consulta [catalog.md](../../docs/catalog.md) para el alcance, las reglas y el
procedimiento del primer ingreso.
Consulta [checkout.md](../../docs/checkout.md) para formularios de compra,
reintentos sin duplicacion, administracion de entregas y descuento de stock al confirmar.
Consulta [operations.md](../../docs/operations.md) para los paneles de pago,
comprobantes privados, preparacion/despacho/entrega, devoluciones parciales,
recepcion, reembolso administrativo y mensajes de envio manual.
Consulta [promotions.md](../../docs/promotions.md) para `/admin/promociones`,
seleccion de productos/categorias, cupos, cupones en checkout y saldos netos
en devoluciones. Los pedidos de total cero se muestran sin cobro.
Consulta [reports.md](../../docs/reports.md) para `/admin/reportes`: filtros,
indicadores por eventos, evolucion diaria, productos/margen, inventario,
promociones y CSV completo. Los importes agregados se reciben como texto COP
y se formatean con BigInt; el operador no tiene acceso a estos datos.
Consulta [users.md](../../docs/users.md) para `/admin/usuarios` y `/mi-cuenta`:
claves temporales, cambio obligatorio, edicion de perfiles/roles/estado,
restablecimiento de otra cuenta y cierre de todas las sesiones. Las claves
solo permanecen en memoria durante el formulario y no se devuelven ni envian.
