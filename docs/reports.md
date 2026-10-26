# Reportes e indicadores (Fase 8)

## Acceso y formularios

Entra como **Administrator** y abre **Reportes e indicadores**
(`/admin/reportes`). El operador no puede consultar ni exportar estos datos:
incluyen costos, margenes e informacion financiera. La API aplica la misma
politica que el guard y la navegacion Angular; ocultar un enlace no autoriza.

El panel ofrece cuatro vistas:

1. **Ventas y dinero**: indicadores, evolucion diaria y estado actual de pedidos
   creados en el periodo.
2. **Productos y margen**: unidades entregadas, devueltas y repuestas; importes
   historicos, reembolsos de mercancia, costos y margen estimado. Busqueda por
   nombre/SKU y orden por unidades, mercancia neta o margen.
3. **Inventario actual**: categoria, busqueda literal, agotados, stock bajo y
   visibilidad; valoracion al costo actual. Los totales abarcan todo el filtro.
4. **Promociones y cupones**: usos creados/liberados, descuentos historicos,
   usos creados aun consumidos y contador global actual. Busqueda por
   nombre/codigo y paginacion.

Las vistas historicas tienen fechas inclusivas de Colombia, hasta 366 dias,
y selector **Solo reales / Solo pruebas / Reales y pruebas**. El valor inicial
es mes actual, solo reales. Los reportes con pruebas tienen advertencia visible.
El inventario es actual, no tiene filtro de pedidos de prueba: incluye
productos de demostracion si cumplen los filtros del catalogo.

**Consultar / actualizar** aplica el formulario. Cambiar un campo no altera
los resultados hasta consultar; el periodo y la busqueda aplicados se muestran
con el reporte. Paginar y exportar usan los filtros aplicados, no cambios aun
sin consultar. No hay polling ni consultas periodicas.

## Fechas y reconocimiento por eventos

Los limites de fecha se convierten de **UTC-05:00** a UTC. Se incluye desde
las 00:00:00 del primer dia hasta, sin incluir, las 00:00:00 del dia posterior
al ultimo. Los eventos se agrupan por dia de Colombia, no por UTC ni por la
zona del equipo que abre el navegador.

| Indicador | Fecha y regla |
| --- | --- |
| Pedidos creados | `Order.CreatedAtUtc`, incluidos pendientes y cancelados |
| Estado de pedidos | Estado actual de pedidos creados en el periodo, no transiciones historicas |
| Ventas entregadas | `Order.DeliveredAtUtc`; mercancia neta original mas envio |
| Ofertas de productos | Descuento total menos beneficio adicional en pedidos entregados |
| Beneficios adicionales | Reparto/snapshot promocional de pedidos entregados |
| Cobros | `Payment.ConfirmedAtUtc`, importe positivo y estado Paid o Refunded |
| Reembolsos | `OrderReturn.RefundedAtUtc` y estado Refunded, incluidas cancelaciones y retornos |
| Reposicion vendible posventa | `OrderReturn.ReceivedAtUtc`, solo devoluciones de pedidos entregados |
| Usos promocionales creados | `PromotionUsage.UsedAtUtc` |
| Usos promocionales liberados | `PromotionUsage.ReleasedAtUtc`, aunque el uso se crease antes del periodo |

Una venta no es un cobro. Un pedido creado o pagado pero no entregado no
incrementa ventas. Un cobro totalmente reembolsado sigue contando en la fecha
de su confirmacion; su salida se cuenta en la fecha del reembolso. El
**flujo neto** es cobros menos reembolsos y puede ser negativo.
Los pedidos de total cero cuentan como entregas **sin cobro**, no como fondos
recibidos.

Si existen registros del conjunto seleccionado sin fecha necesaria de entrega,
confirmacion de cobro o reembolso, se muestran advertencias de calidad de datos.
No se inventa una fecha usando la creacion del pedido. Esas advertencias son
del conjunto real/prueba seleccionado, no una atribucion de los registros sin
fecha al periodo mostrado.

## Margen estimado y devoluciones

La formula por eventos es:

```text
mercancia entregada despues de ofertas y beneficios adicionales
- reembolsos de mercancia de devoluciones posventa
- costo historico de las unidades entregadas
+ costo historico de unidades vendibles recibidas y repuestas
= margen estimado del periodo
```

Cada termino utiliza su propia fecha de evento. Una recepcion de devolucion y
su reembolso pueden caer en periodos distintos. No se recupera costo por
unidades danadas que no se repusieron. Las cancelaciones y retornos de
despachos nunca entregados afectan dinero e inventario, pero no revierten ventas
que no se reconocieron. El envio devuelto no es reembolso de mercancia.

Se usan importes/descuentos por linea y `UnitCostSnapshot` historicos:
editar precios, costos o promociones no cambia el pasado. Los productos se
agrupan por su ID; si falta la referencia historica, por SKU. Se muestra el
nombre/SKU del evento de entrega mas reciente de ese producto entre los
registros relevantes, no el nombre actual del catalogo.

Este margen **no es utilidad contable**: no incluye transporte, impuestos,
comisiones, gastos fijos ni un sistema de costeo de inventario por lotes.
Las ventas mostradas son las entregas originales del periodo; los reembolsos
se muestran aparte, no se restan retroactivamente de la fecha de venta.

## Inventario y promociones

La valoracion de inventario multiplica stock actual por costo actual. **Agotado**
es stock cero; **bajo** es stock positivo menor o igual al umbral. No publicado
incluye producto o categoria inactiva. No se reconstruye inventario historico
ni se cambia stock desde estos reportes.

En promociones, un uso se consume al crear, no al entregar. Por eso los usos
y descuentos otorgados **no equivalen a ventas atribuidas** ni a utilidad de
una campana. El contador global actual incluye todos los periodos y pedidos,
reales y de prueba, incluso al filtrar un solo conjunto. El nombre/codigo y
cupo son los actuales; el descuento de cada uso es el historico. Solo se
listan reglas con eventos creados o liberados en el periodo.

## Precision, exportacion y limites

Las sumas y multiplicaciones intermedias usan `BigInteger`; todos los importes
de reportes viajan como **cadenas de enteros COP**, incluso negativos. Angular
los formatea con `BigInt` e `Intl.NumberFormat`, sin convertirlos a `Number`.
Esto evita perder pesos al sumar muchos pedidos o valorar mucho inventario.
Los contratos de checkout y las otras fases no cambian.

**Exportar todo a CSV** genera el conjunto filtrado completo, no solo la pagina.
Se vuelve a consultar al exportar: operaciones nuevas pueden cambiar el
resultado frente a la pantalla anterior. Cada consulta/exportacion mantiene
un snapshot de lectura consistente; no reserva el bloqueo de escritor de
SQLite ni modifica datos.

El CSV usa UTF-8 con BOM, separador punto y coma y campos entre comillas.
Escapa comillas/saltos de linea y protege texto que podria interpretarse como
formula. Los importes llevan prefijo apostrofe para conservarse como texto
exacto en hojas de calculo; no elimines esa proteccion en valores largos.
No se exportan nombre/contacto del comprador, direcciones, referencias privadas
de pago/reembolso ni credenciales de seguimiento.

Se admiten de 1 a 366 dias, paginas de 1 a 50 filas y busquedas de hasta 100
caracteres. Cada consulta de eventos/catalogo tiene un limite de **50.000
registros fuente**. El agregado puede tener menos filas que sus fuentes. Si
se excede el limite, se informa error para reducir fechas/filtros: no se
entregan totales ni archivos truncados. El mismo limite aplica a exportaciones.

## API y validacion

GET bajo `/api/v1/admin/reports`:

- `/sales` y `/sales.csv`
- `/products` y `/products.csv`
- `/inventory` y `/inventory.csv`
- `/promotions` y `/promotions.csv`

Ventas/productos/promociones reciben `from=AAAA-MM-DD`, `to=AAAA-MM-DD`,
`dataSet=real|test|all`. Productos agrega `search`, `sort=units|revenue|margin`,
`page`, `pageSize`; promociones agrega `search`, `page`, `pageSize`.
Inventario recibe `search`, `categoryId`, `stock=all|low|out`,
`visibility=all|active|inactive`, `page`, `pageSize`.
Las exportaciones ignoran la paginacion para exportar todos los resultados
filtrados. Los errores de filtros/limites son Problem Details 400; sin sesion
401, sin rol 403. Respuestas privadas y descargas usan no-store; CSV incluye
attachment y nosniff.

No se requieren tablas, migraciones ni dependencias nuevas. Se consulta el
SQLite existente de [database.md](database.md). Los ejemplos de
[la API](../src/MercadoCercano.Api/MercadoCercano.Api.http) son manuales.

Las pruebas aisladas usan SQLite temporal/en memoria y no inician host HTTP,
servidores ni navegadores:

```powershell
dotnet test tests\MercadoCercano.Infrastructure.Tests\MercadoCercano.Infrastructure.Tests.csproj --configuration Release --filter FullyQualifiedName~OrderServiceTests.Reports --disable-build-servers
```

Cubren fechas limite, datos de prueba, entregas sin cobro, flujo negativo,
descuentos, pagos reembolsados, devoluciones/repuestos, costos historicos,
precision por encima de los limites de JavaScript/long, filtros, permisos,
exportacion, limite de 50.000 y ejecucion con `PRAGMA query_only = ON`.
