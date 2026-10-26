# Arquitectura inicial

La solución se divide en una API ASP.NET Core y una interfaz Angular, con
proyectos separados para el dominio, la aplicación y la infraestructura. La
separación establece límites para crecer sin introducir microservicios ni
dependencias prematuras.

## Dirección de dependencias

- `Domain` no depende de otros proyectos de la solución.
- `Application` depende de `Domain`.
- `Infrastructure` depende de `Application` y `Domain`.
- `Api` compone la aplicación y la infraestructura.
- `Web` usa componentes standalone, signals y servicios tipados sobre `fetch`.
  Consume la API REST; en desarrollo usa el proxy de Angular CLI.

## Catalogo

`Application.Catalog` define DTOs inmutables y el contrato `ICatalogService`.
`Infrastructure.Catalog` implementa consultas, validacion comercial,
transacciones de inventario, auditoria y almacenamiento local de imagenes.
Los controladores publicos y administrativos componen estos servicios sin
exponer entidades de EF Core. Las operaciones privadas requieren la politica
`Administrator` y CSRF. Los errores de negocio se traducen a Problem Details.

Angular separa las rutas publicas, login y portal. La sesion se mantiene en
memoria y se restaura desde la cookie del servidor al entrar en rutas
protegidas. No hay contrasenas o tokens de sesion en almacenamiento del
navegador. Consulta [catalog.md](catalog.md) para rutas y reglas.

## Usuarios y sesiones

`Application.Users` define contratos inmutables e `IUserAdministrationService`.
La implementacion vive en `Api.Authorization`, junto al alta inicial, para
reutilizar `IPasswordHasher<User>` de ASP.NET Core sin agregar una dependencia
del framework web a Infrastructure. Validacion de credenciales compartida,
transacciones SQLite, versiones y auditoria protegen todas las mutaciones.
No se serializan solicitudes que contienen contraseñas.

La cookie incluye sello y marca de cambio obligatorio; se comparan con SQLite
en cada peticion. Las politicas de rol exigen contraseña definitiva, mientras
que el endpoint personal solo exige autenticacion y verifica la clave actual.
Angular dirige claves temporales a Mi cuenta y, tras el cambio, vuelve al login
sin renovar la sesion. Consulta [users.md](users.md).

## Checkout y pedidos

`Application.Orders` define cotizaciones, configuracion versionada, creacion
idempotente, seguimiento y acciones administrativas. `Infrastructure.Orders`
calcula importes desde SQLite y guarda las instantaneas comerciales. Crear no
reserva stock; confirmar y cancelar concilian inventario dentro de transacciones
SQLite. Los controladores usan `OrderOperations` para pedidos privados y
`Administrator` para reglas de entrega. No hay integraciones de pago.

Angular mantiene el carrito sin precios ni PII en localStorage; conserva un
intento de creacion en sessionStorage con un codigo privado recuperable.
La API guarda solo el hash del codigo. Esto no cambia la sesion administrativa
por cookie HttpOnly. Consulta [checkout.md](checkout.md) para reintentos,
privacidad y creacion de pedidos.

`IOrderOperationsService` comparte la instancia scoped de `OrderService` con
`IOrderService`; sus archivos parciales mantienen una sola transaccion y
proyeccion por operacion. Controla pagos, estados operativos, comprobantes,
devoluciones por linea, recepcion y registro de reembolsos. No llama bancos,
transportadoras ni proveedores de mensajeria. Las escrituras SQLite comienzan
antes de leer, validan version y registran auditoria. Los intentos y acciones
repetidos no duplican inventario ni dinero.

`IPrivatePaymentEvidenceStore` reutiliza validacion de archivos del catalogo,
pero usa un directorio privado sin endpoint estatico. Si falla guardar o
confirmar la transaccion, elimina el nuevo archivo.

Angular compone paneles de pago, cumplimiento, devoluciones y contacto en el
detalle administrativo; seguimiento reutiliza el formulario de comprobantes.
Los guards protegen formularios modificados y cargas en curso. El servidor
calcula saldos e importes de devolucion; el cliente muestra solo estimaciones.
Consulta [operations.md](operations.md) para el flujo completo y sus limites.

## Persistencia

## Publicacion y mantenimiento

En Windows/IIS, ASP.NET Core sirve Angular compilado desde wwwroot y conserva
las rutas SPA, pero no convierte un API inexistente en HTML exitoso. MSBuild
incluye recursos Angular, licencias y scripts al publicar; no arranca servidores.
Development conserva Angular CLI, proxy y almacenamiento anteriores.

`Api.Operations` valida las rutas privadas, mantiene una lease exclusiva por
almacenamiento y usa Data Protection persistente con DPAPI. El arranque verifica
esquema y administrador sin migrar ni sembrar produccion. Un modo de consola
`--maintenance` se ejecuta antes de crear el host, usando la misma configuracion
y validacion de credenciales. Proporciona migracion, aprovisionamiento,
respaldos autocontenidos/verificados, retencion y restauracion con revocacion
de sesiones. IIS se pone offline mediante scripts que no se ejecutan al
compilar. Consulta [production.md](production.md).

## Persistencia y consultas

`Application.Reports` expone DTOs de solo lectura e `IReportService`.
`Infrastructure.Reports` combina eventos de entrega, cobro, recepcion y
reembolso, sin confundir la creacion con una venta. Cada consulta usa una
transaccion diferida de lectura SQLite, sin reservar el bloqueo de escritor.
Los agregados usan `BigInteger` y se transmiten como texto COP; Angular los
formatea con `BigInt`. Los reportes y CSV son exclusivos de Administrator.
No se agregan tablas, paquetes ni trabajos en segundo plano.
Consulta [reports.md](reports.md) para reglas financieras y limites.

`Application.Promotions` define administracion versionada e historial;
`Infrastructure.Promotions` valida reglas y audita cambios. La seleccion y el
reparto monetario viven junto al checkout en `OrderPromotions`, dentro del
mismo `OrderService`. Se usa `BigInteger` para multiplicaciones/prorrateos
intermedios y se persisten enteros COP. El contador cambia atomicamente sin
alterar la version de las condiciones. Pedido, uso y contador comparten
transaccion; cancelaciones previas al despacho liberan una sola vez.
Angular administra reglas y consulta usos en `/admin/promociones`; checkout
compara el cupon con automaticas y muestra el beneficio historico. Consulta
[promotions.md](promotions.md).

`Infrastructure.Persistence.StoreDbContext` es el único contexto de EF Core.
Las migraciones viven en Infrastructure y usan SQLite. Importes en COP se
persisten como enteros `long`, no como `float`, `double` ni `decimal`.
Restricciones de SQLite protegen importes, cantidades, descuentos y relaciones
de inventario; los pedidos, pagos, movimientos e historiales no se eliminan en
cascada. Las dependencias opcionales de clientes y usuarios se conservan como
instantáneas cuando se elimina un registro relacionado.

La base predeterminada se guarda en `BaseDatos/mercado-cercano.db`, fuera del
frontend. En Development, la API aplica migraciones y crea datos de demostración
desactivados. En otros entornos la migración debe ejecutarse expresamente como
parte del despliegue.

`GET /api/v1/health` comprueba que la API responde. OpenAPI se expone únicamente
en desarrollo, con Swagger UI en `/swagger` y el documento en
`/openapi/v1.json`. La API autentica con cookies, protección CSRF y políticas de rol;
consulta [security.md](security.md) para el contrato y los límites actuales.
