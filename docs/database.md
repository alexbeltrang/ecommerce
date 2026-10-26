# Base de datos

## Motor y ubicación

SQLite es el motor de desarrollo y la base principal inicial. La configuración
predeterminada apunta a `BaseDatos/mercado-cercano.db` en la raíz del repositorio,
fuera del directorio público de Angular. La ruta se configura mediante
`Database__Path`; puede ser absoluta o relativa al directorio de la API.

No agregues el archivo `.db`, sus archivos WAL/SHM ni copias de seguridad al
control de versiones. En producción, guarda la base y sus copias en un volumen
privado y persistente, con acceso de sistema limitado al servicio.

## Esquema

La migración inicial incluye usuarios y roles; clientes y direcciones;
categorías, productos e imágenes; pedidos y sus líneas históricas; pagos;
movimientos de inventario; promociones y sus usos; configuración comercial;
auditoría e historial de estados de pedido.

La base contiene 18 tablas de dominio y 25 relaciones de clave foranea:

| Tabla | Relaciones |
| --- | --- |
| `Users` | Rol de administrador u operador almacenado en la columna `Role` |
| `Customers` | Datos del cliente |
| `Addresses` | Cliente |
| `Categories` | Categorias del catalogo |
| `Products` | Categoria |
| `ProductImages` | Producto |
| `Orders` | Cliente y usuario que cancela |
| `OrderItems` | Pedido y producto |
| `Payments` | Pedido y usuario que confirma |
| `PaymentEvidence` | Pago; metadatos del archivo privado, nunca su contenido |
| `OrderReturns` | Pedido y usuarios que solicitan, reciben y reembolsan |
| `OrderReturnItems` | Solicitud de devolucion y linea original del pedido |
| `InventoryMovements` | Producto y usuario |
| `Promotions` | Condiciones de descuento; producto o categoria opcional, nunca ambos |
| `PromotionUsages` | Promocion y pedido |
| `StoreSettings` | Configuracion comercial |
| `AuditLogs` | Usuario |
| `OrderStatusHistory` | Pedido y usuario |

EF Core conserva ademas sus tablas tecnicas de historial y bloqueo de
migraciones. Los roles son valores restringidos del modelo, no una tabla
independiente. No se han agregado tablas sin respaldo en el modelo del dominio.
La ubicacion anterior `App_Data` se conserva sin cambios; sus datos no se
copian automaticamente a la nueva base.

Las cantidades y los precios COP usan columnas enteras. El modelo añade índices
para búsquedas frecuentes, restricciones para importes, existencias y límites
de descuentos, y claves foráneas que impiden borrar en cascada el historial
comercial. La configuración comercial distingue valores públicos de valores
solo administrativos; no debe usarse para guardar secretos.

La migracion `CheckoutOrderSnapshots` agrega a `Orders` la huella del intento,
marca de prueba e instantaneas de condiciones e instrucciones, sin quitar
columnas ni tablas. Los indices unicos existentes protegen numero de pedido,
hash del codigo e identificador de idempotencia. La configuracion del checkout
se guarda versionada en `StoreSettings` bajo `Checkout.Configuration.v1`.
Las tarifas iniciales de desarrollo no se sobrescriben al reiniciar.
Consulta [checkout.md](checkout.md) para creacion, confirmacion y cancelacion.

La migracion `OrderOperationsAndPartialReturns` agrega despacho y fechas a
`Orders`, motivo de rechazo a `Payments` y las tres tablas de comprobantes y
devoluciones. Reconstruye `InventoryMovements` preservando sus filas para
permitir el tipo `CustomerReturn` (6). No crea pagos, devoluciones ni productos
reales. Los intentos de devolucion tienen GUID unico y huella de contenido;
las cantidades reembolsables usan precios historicos del pedido.
Consulta [operations.md](operations.md) para recepcion y reembolso separados.

`PromotionsAndCoupons` agrega nombre, version, alcance y tope a promociones,
descuentos historicos a pedidos/lineas y descuento/fecha de liberacion a usos.
Agrega dos claves foraneas restrictivas, sin nuevas tablas de dominio.
Los pedidos previos conservan sus valores y parten con beneficio adicional
cero. Las reglas previas reciben nombre y version, sin cambiar sus codigos.
Consulta [promotions.md](promotions.md) para cupos y prorrateo exacto en COP.

La Fase 8 consulta las tablas existentes sin modificar esquema ni valores.
Las consultas/exportaciones de reportes abren un snapshot diferido de lectura;
no guardan agregados ni alteran pedidos, inventario, pagos, usos o auditoria.
Consulta [reports.md](reports.md).

`UserAdministrationAndSessionRevocation` agrega `MustChangePassword`,
`SecurityStamp` y `UpdatedAtUtc` a `Users`, sin tablas ni relaciones nuevas.
Las filas existentes reciben sellos aleatorios distintos y fecha UTC de
migracion; conservan nombre, correo, hash, rol, estado y fecha de creacion,
sin cambio obligatorio de contraseña. `UpdatedAtUtc` es token de concurrencia;
los guardados y cambios de clave rotan el sello para revocar cookies antiguas.
Los usuarios nuevos/restablecidos por un administrador requieren cambio.
La auditoria guarda solo rol, estado y marca de cambio obligatorio, no claves,
hashes ni sellos. Consulta [users.md](users.md).

## Migraciones

En produccion Windows/IIS se usa `Storage:Root\database\mercado-cercano.db`;
no la ruta relativa de Development. El proceso web abre ReadWrite, nunca crea
una base ausente, migra ni siembra al arrancar. Usa los comandos offline y
respaldos conjuntos de [production.md](production.md), no el factory de
diseno de EF ni copias sueltas de SQLite. Esta fase no cambia el esquema.

Instala las herramientas locales tras clonar el repositorio:

```powershell
dotnet tool restore
```

Consulta, crea y aplica migraciones desde la raíz:

```powershell
dotnet ef migrations list --project src\MercadoCercano.Infrastructure --startup-project src\MercadoCercano.Api --context StoreDbContext
dotnet ef migrations add NombreDescriptivo --project src\MercadoCercano.Infrastructure --startup-project src\MercadoCercano.Api --context StoreDbContext --output-dir Persistence\Migrations
dotnet ef database update --project src\MercadoCercano.Infrastructure --startup-project src\MercadoCercano.Api --context StoreDbContext
```

La API actualiza el esquema automáticamente solo en Development. En otros
entornos, aplica las migraciones explícitamente antes de iniciar una versión que
necesite el nuevo esquema y revisa el SQL de la migración.

## Copia y restauración

Detén la API antes de copiar el archivo para una copia sencilla y consistente:

```powershell
Copy-Item BaseDatos\mercado-cercano.db BaseDatos\mercado-cercano.backup.db
```

Restaura después de detener la API, conservando una copia de la base actual:

```powershell
Copy-Item BaseDatos\mercado-cercano.db BaseDatos\mercado-cercano.before-restore.db
Copy-Item BaseDatos\mercado-cercano.backup.db BaseDatos\mercado-cercano.db
```

Verifica periódicamente las copias restaurándolas en un entorno separado. Para
copias en línea de bases SQLite en uso, utiliza la API de backup de SQLite en
vez de copiar únicamente el archivo principal mientras hay transacciones.

Una copia completa incluye `uploads/products` y `uploads/payment-evidence`
bajo la raiz del repositorio en la configuracion actual, ademas de SQLite. Los comprobantes no se guardan
dentro de la base. Deten escrituras durante una copia conjunta de archivos y
base para mantener sus referencias consistentes. Conserva los comprobantes en
almacenamiento privado y restaura sus nombres generados junto con la base.
No publiques ni agregues al repositorio archivos o respaldos con datos reales.
