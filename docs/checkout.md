# Carrito, checkout y pedidos (Fases 5 a 7)

## Pantallas y acceso

| Ruta | Acceso | Funcion |
| --- | --- | --- |
| `/carrito` | Publico | Cantidades, eliminacion, disponibilidad actual y subtotal estimado |
| `/checkout` | Publico | Contacto, entrega, pago, revision de precios y condiciones |
| `/pedido/:numero#codigo` | Codigo privado | Confirmacion, resumen e historial del pedido |
| `/seguimiento` | Publico | Consulta manual con numero y codigo privado |
| `/admin/pedidos` | Administrador u operador | Filtros por contacto, estado, pago, entrega, recepciones/reembolsos pendientes; paginacion |
| `/admin/pedidos/:id` | Administrador u operador | Pago, comprobantes, preparacion, despacho, entrega, devoluciones y mensajes manuales; reembolsos solo administrador |
| `/admin/entregas` | Solo administrador | Tarifas, cobertura, medios de pago y condiciones |
| `/admin/promociones` | Solo administrador | Promociones automaticas, cupones, cupos e historial |

Los guards no sustituyen las politicas de autorizacion de la API. Las
modificaciones y consultas POST obtienen un CSRF nuevo y conservan cookies
del mismo origen.

## Preparacion y valores iniciales

Reinicia la depuracion con F5 despues de actualizar el codigo. Development
aplica las migraciones y crea una configuracion inicial solo cuando no existe:

- Recogida coordinada sin costo; municipio **Municipio de prueba**.
- Domicilio local: **5.000 COP**, solo en ese municipio ficticio.
- Transportadora: **15.000 COP**, cobertura nacional en Colombia.
- Transferencia bancaria, billetera digital y efectivo contra entrega.
- Avisos visibles de prueba: **no realizar pagos ni usar datos personales reales**.

Edita estos valores en **Entregas y checkout**, no en el codigo. Hay tarifas
planas por modalidad, no cotizacion externa por distancia o peso. Los
municipios se escriben uno por linea, hasta 100 distintos de 120 caracteres.
La cobertura limitada se compara sin distinguir mayusculas.

Habilita al menos una modalidad y un medio de pago. Completa las instrucciones
publicas y condiciones reales del negocio antes de desmarcar el modo de prueba.
Las condiciones deben explicar el tratamiento de datos y la confirmacion de
disponibilidad. No publiques secretos bancarios en las instrucciones.

Fuera de Development no se crean defaults y no se cotizan ni crean pedidos con
configuracion de prueba; tampoco se confirman pedidos historicos de prueba.
El administrador puede crear la configuracion inicial desde el formulario si
aun no existe. El primer administrador de produccion se aprovisiona desde
consola offline; consulta [production.md](production.md) y [security.md](security.md).

## Flujo de compra

1. El catalogo y el detalle permiten agregar productos con existencias.
2. El carrito conserva solamente identificadores y cantidades en
   `localStorage`, nunca precios ni datos personales. Al abrirlo consulta cada
   producto por ID, por lo que un cambio de slug no rompe la seleccion.
3. Los productos desactivados, faltantes o sin stock se muestran para corregir
   o eliminar. Los errores de almacenamiento o conexion se informan, no se
   ocultan convirtiendolos en un carrito vacio.
4. El formulario solicita nombre, telefono de 7 a 15 digitos, correo opcional,
   modalidad y medio de pago. Domicilio y transportadora requieren municipio
   y direccion; barrio, referencias y observaciones son opcionales.
5. **Recalcular y revisar resumen** consulta la API. El cliente no envia precios:
   SQLite determina productos activos, precios y promociones vigentes,
   cantidades disponibles y tarifa de entrega.
6. El resumen muestra subtotal regular, descuentos de productos, beneficio adicional elegido, entrega,
   total COP, municipio e instrucciones cotizadas. Cada importe se limita a
   **9.000.000.000.000 COP**, dentro de los enteros seguros de JavaScript.
7. Aceptar las condiciones habilita el envio. Un cambio de carrito, entrega,
   municipio, cupon o medio de pago invalida la cotizacion y exige revisarla otra vez.
   Si cambian precios, reglas o configuracion antes de crear, debe recalcularse.
8. La creacion atomica guarda cliente invitado independiente, direccion cuando
   corresponde, lineas/precios/costos/descuentos e instrucciones historicas, pago pendiente
   (resuelto sin cobro si el total es cero), uso promocional cuando corresponda,
   aceptacion de condiciones, auditoria e historial. Nunca fusiona clientes por
   coincidencia de telefono o correo.
9. El resultado queda **Pendiente de confirmacion**. Guarda el enlace privado
   antes de cerrar la pagina. No se envia automaticamente por correo o mensajeria.

Las ofertas del producto se aplican segun su vigencia UTC. Sobre ese precio
se elige un solo beneficio adicional, el mayor entre automaticas y el cupon
opcional, sin descuento del envio. Cotizar no consume usos; crear si y cancelar
antes de despacho libera una vez. Los pedidos conservan el beneficio historico.
Consulta [promotions.md](promotions.md) para alcance, minimos, cupos, pedidos
sin cobro y devoluciones netas.

## Reintentos y perdida de respuesta

Antes del primer envio, el navegador genera un GUID de idempotencia y un codigo
aleatorio de 32 bytes (43 caracteres base64url). Conserva ambos y la firma del
carrito en `sessionStorage`, exclusivamente para recuperar ese intento; no
guarda nombre, direccion, telefono, correo, notas ni contrasenas.

- Repetir el mismo intento, codigo y contenido devuelve el pedido original,
  incluso si despues cambian precios, condiciones o existencias.
- Reutilizar el identificador con otro contenido produce 409. Usa
  **Recuperar pedido**; no generes automaticamente otra compra.
- Recuperar exige identificador **y** codigo: el GUID por si solo no autoriza.
- Un 404 de recuperacion no descarta el intento: puede no haber resultado o
  estar terminando el envio original. Puedes reintentar con el mismo identificador.
- Descartar un intento es una accion explicita con advertencia de duplicacion.
  No lo descartes tras un timeout sin comprobar el resultado.
- Al completar se elimina el intento y se vacia el carrito solo si coincide.
  Si otro tab cambio el carrito, se conserva y se muestra un aviso.
- Si no se puede guardar el intento en el navegador, no se envia el pedido.
  Si no se puede limpiarlo tras un exito, se conserva visible el numero y el enlace.
- Si falla la actualizacion del carrito tras crear, se muestra el enlace y se
  conserva el intento para recuperar sin duplicar la compra.

No es una sesion de autenticacion administrativa. Aun asi, el codigo es una
credencial sensible: cualquier script del mismo origen podria leerlo.
Protege la aplicacion contra XSS y no compartas el perfil del navegador.
Cerrar la pestaña puede perder la recuperacion automatica; guarda el enlace.

## Seguimiento privado

El codigo viaja en el fragmento `#` del enlace, que no se envia al servidor en
la URL HTTP ni en Referer. La pagina lo envia en el cuerpo de la consulta.
SQLite guarda solo SHA-256 y compara la credencial en tiempo constante.
No pongas el codigo en query string, logs, capturas o tickets publicos.

La consulta publica muestra productos, totales, estados e instrucciones
historicas, pero no nombre del comprador, telefono, correo, direccion,
costos internos, GUID del intento ni hash del codigo. Numero o codigo
incorrectos devuelven el mismo mensaje 404.

Si pierdes el enlace y el intento de sesion, no hay recuperacion publica por
telefono ni correo: contacta al negocio para coordinar el pedido de forma privada.
No hay rotacion automatica del codigo en esta fase.

## Inventario y operacion administrativa

**El stock se descuenta cuando el negocio confirma, no cuando se crea el pedido.**
Varios pedidos pendientes pueden solicitar las mismas unidades. La confirmacion:

- Requiere la version `updatedAtUtc` y un pedido pendiente.
- Comprueba todos los productos, categorias activas y existencias.
- Descuenta todas las lineas, crea movimientos `Sale`, historial y auditoria
  dentro de una transaccion SQLite serializada. Si falta una linea, no modifica ninguna.
- Conserva precios e instrucciones historicos, aunque el catalogo ya haya cambiado.
- Repetir la confirmacion no vuelve a descontar inventario.
- No marca el pago como realizado.

La cancelacion exige version y un motivo **visible para el cliente**.
Permite pendientes, confirmados, en preparacion o listos, pero no despachados
ni entregados. Un pago en revision debe resolverse antes. Un pedido pendiente
no repone unidades; uno confirmado/preparado/listo las repone una sola vez con
movimientos `OrderCancellation`. Se rechazan desbordamientos de inventario.
Si estaba pagado, se crea la obligacion de reembolsar el total, incluido el
envio; cancelar no devuelve dinero automaticamente.

Preparacion, despacho, entrega, comprobantes, validacion manual de pagos y
devoluciones parciales estan implementados. Consulta
[operations.md](operations.md) para formularios, reglas, roles y mensajes manuales.

## API y persistencia

Publica: `/api/v1/checkout/settings`, `/quote`, `/orders`, `/track`, `/recover`.
El POST de creacion devuelve 201 y Location al recurso
`/api/v1/checkout/orders/{numero}`; consultarlo exige `X-Order-Token`, nunca un
token en la URL. Administracion: `/api/v1/admin/orders` y
`/api/v1/admin/checkout-settings`.

Limites por IP y minuto: 30 cotizaciones, 5 creaciones y 15 consultas/recuperaciones.
Un 429 exige esperar; no hay polling ni reintentos automaticos.
Las respuestas no se almacenan en cache. Los ejemplos de
[MercadoCercano.Api.http](../src/MercadoCercano.Api/MercadoCercano.Api.http)
son manuales y no se ejecutan durante la compilacion.

La configuracion versionada se guarda como JSON en `StoreSettings`, clave
`Checkout.Configuration.v1`, y cada cambio administrativo queda auditado.
La migracion `CheckoutOrderSnapshots` agrega instantaneas a `Orders`.
`OrderOperationsAndPartialReturns` incorpora comprobantes privados, solicitudes
y lineas de devolucion, y amplia los movimientos de inventario.
Respalda SQLite de forma consistente y tambien las carpetas privadas/publicas
de `uploads`; consulta
[database.md](database.md).

## Validacion local sin HTTP

```powershell
dotnet test tests\MercadoCercano.Infrastructure.Tests\MercadoCercano.Infrastructure.Tests.csproj --configuration Release --filter 'FullyQualifiedName~OrderServiceTests|FullyQualifiedName~CatalogServiceTests|FullyQualifiedName~StoreDbContextTests' --disable-build-servers
Set-Location src\MercadoCercano.Web
npm run build
npm run lint
```

Las pruebas seleccionadas usan SQLite en memoria, archivos/carpetas temporales y
metadatos de controladores. Incluyen conexiones concurrentes reales sobre una
base temporal. No arrancan un host web ni hacen peticiones HTTP. Las pruebas de
autenticacion con host en memoria no estan incluidas en ese filtro.

El recorrido visual debe realizarse manualmente con F5. Esta implementacion no
agrega sondeos desde PowerShell, apertura automatica adicional de navegadores
por el asistente ni servicios de prueba.
