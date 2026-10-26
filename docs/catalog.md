# Catalogo y portal administrativo

## Pantallas Angular

| Ruta | Acceso | Funcion |
| --- | --- | --- |
| `/` | Publico | Catalogo, busqueda por nombre o SKU, categoria, orden y paginacion |
| `/productos/:slug` | Publico | Detalle, precio vigente, disponibilidad y galeria |
| `/login` | Publico | Inicio de sesion por correo y contrasena |
| `/configuracion-inicial` | Solo desarrollo y sin usuarios | Crear el primer administrador con credenciales elegidas en el formulario |
| `/mi-cuenta` | Autenticado, ambos roles | Cambio de contraseña con verificacion de la actual; obligatorio si es temporal |
| `/admin` | Autenticado | Portal y navegacion segun el rol |
| `/admin/categorias` | Administrador | Crear, editar, ordenar, activar o desactivar |
| `/admin/productos` | Administrador | Listado y filtros, estado y alertas de stock bajo |
| `/admin/productos/nuevo` | Administrador | Alta de producto e inventario inicial |
| `/admin/productos/:id` | Administrador | Edicion, inventario, promociones e imagenes |
| `/admin/reportes` | Administrador | Ventas, dinero, margen estimado, inventario, promociones y CSV |
| `/admin/usuarios` | Administrador | Listar, crear, editar, activar/desactivar y restablecer otras cuentas |

Los operadores pueden entrar al portal, gestionar pedidos y consultar el
catalogo publico, pero no administrar categorias o productos. Consulta
[checkout.md](checkout.md) para carrito, checkout y operacion basica de pedidos.
Los guards mejoran la navegacion; la API vuelve a aplicar la
politica `Administrator` a todas las operaciones privadas del catalogo.
Los reportes financieros y costos tambien son exclusivos del administrador.
Consulta [reports.md](reports.md) para filtros, precision COP y criterios
de reconocimiento por eventos.
Consulta [users.md](users.md) para claves temporales, cambio obligatorio y
revocacion de sesiones. Una cuenta con clave temporal no accede al portal
hasta cambiarla e iniciar sesion nuevamente.

## Primer ingreso

1. Deten las sesiones de depuracion anteriores y pulsa F5 con
   **Mercado Cercano: aplicacion completa**.
2. Abre `/login`. Si la base no tiene usuarios, pulsa **Crear primer
   administrador** y elige nombre, correo y contraseña. Confirma la contraseña
   y guarda. La contraseña requiere de 8 a 32 caracteres, mayusculas,
   minusculas, numeros y un caracter especial, sin espacios.
   El alta solo funciona en Development y se cierra al existir
   cualquier usuario, aunque sea operador o este inactivo.
3. Vuelve al login e ingresa con los datos que elegiste. La pantalla
   redirige al portal; conserva el destino interno solicitado por el administrador.
4. Crea una categoria y un producto con datos reales. Guarda el producto antes
   de subir imagenes.
5. Activa tanto la categoria como el producto para publicarlo en el catalogo.

No hay cuentas o contrasenas predeterminadas ni registro publico de
administradores en produccion. Como alternativa local siguen disponibles las
variables `BOOTSTRAP_ADMIN_NAME`, `BOOTSTRAP_ADMIN_EMAIL` y
`BOOTSTRAP_ADMIN_PASSWORD`; consulta [security.md](security.md).
La sesion utiliza cookies HttpOnly, nunca localStorage.
Cada escritura obtiene un token CSRF nuevo; no hay sondeos periodicos.
Cerrar sesion elimina la cookie del navegador mediante la respuesta de la API.
Las respuestas 401 de las
operaciones administrativas devuelven al usuario al login.

El producto **Producto demostrativo - Snack** se crea inactivo. Aunque tenga
existencias y `sna` coincida con su nombre, no aparece en el catalogo publico
hasta editarlo en **Productos**, marcar **Activo** y **Guardar producto**.
Su categoria tambien debe estar activa. No es necesario reiniciar la API
despues de guardar el producto.

## Reglas del catalogo

- Los slug son unicos por recurso: letras minusculas ASCII, numeros y guiones.
  El formulario propone uno a partir del nombre, que puede ajustarse antes de guardar.
- Los SKU se normalizan a mayusculas y deben ser unicos.
- Los precios y costos COP se reciben como enteros, entre 1 y 9 billones para
  el precio, y entre 0 y 9 billones para el costo. El limite de la API queda
  dentro de los enteros seguros de JavaScript.
- Precio promocional positivo y menor al normal. Puede aplicarse sin fechas o
  dentro de un intervalo completo de inicio y fin. Las fechas se editan en
  hora local y viajan en UTC.
- El catalogo ordena por nombre, novedad o precio efectivo, tambien cuando
  existe una promocion vigente. La busqueda trata `%` y `_` como texto literal.
- Las paginas admiten de 1 a 50 elementos; el frontend usa 12. El total se
  calcula antes de paginar y los empates se ordenan por identificador.
- Solo se publican productos activos de categorias activas. Las respuestas
  publicas excluyen costo, lote, etiquetado interno y umbral de inventario.
- Para retirar productos o categorias se desactivan: no hay borrado fisico que
  destruya las referencias e historiales comerciales.
- Las ediciones requieren el `updatedAtUtc` recibido. Una version antigua
  produce 409 y requiere recargar el registro, sin sobrescribir datos ajenos.
- Un cambio de existencias al editar requiere un motivo. El producto, el
  movimiento de inventario y la auditoria se guardan en la misma transaccion.

## Imagenes

Hasta 8 imagenes por producto y 750 KiB por archivo. Se permiten PNG, JPEG y
WebP, comprobando tipo MIME y firma binaria. No se admite SVG ni nombres o
rutas aportados por el cliente; el servidor genera identificadores aleatorios.
Esta comprobacion no reemplaza un servicio antivirus ni un proceso de
moderacion de contenido.

En Development los archivos se guardan en `uploads/products` en la raiz del repositorio,
fuera del frontend y de SQLite. La base conserva su asociacion, texto
alternativo y orden. `uploads` no se versiona. Una copia de seguridad completa
debe incluir tanto SQLite como esta carpeta.
En produccion viven en `Storage:Root\products`, fuera de wwwroot; la
publicacion y el respaldo conjunto se describen en [production.md](production.md).
IIS sirve Angular y API en el mismo origen y conserva rutas profundas del portal.

La API sirve los archivos en `/api/v1/catalog/images/{archivo}` tras validar
la visibilidad del producto y su categoria, o la sesion de administrador.
No se expone la carpeta mediante un servidor estatico sin autorizacion.
Las respuestas incluyen `nosniff` y `no-store` para no reutilizar imagenes
privadas tras desactivar un producto. Las imagenes existentes pueden
reordenarse, cambiar su texto alternativo o eliminarse desde el editor.

Si falla el guardado de una imagen nueva en SQLite, se elimina su archivo.
Al eliminar una imagen se retira primero su referencia de SQLite y luego el
archivo: si el sistema de archivos falla, la API informa el error y el archivo
residual queda inaccesible mediante la API, pero debe limpiarse operativamente.

## Contrato y validacion

Las rutas publicas viven en `/api/v1/catalog`; las privadas en
`/api/v1/admin/categories` y `/api/v1/admin/products`. Swagger describe los
contratos, y [MercadoCercano.Api.http](../src/MercadoCercano.Api/MercadoCercano.Api.http)
incluye ejemplos manuales que no se ejecutan automaticamente.

La implementacion reutiliza las tablas existentes: no requiere una migracion
adicional ni modifica los datos de demostracion, que siguen inactivos.

Las pruebas `CatalogServiceTests` usan SQLite en memoria y carpetas temporales
aisladas. No inician un host web ni consultan HTTP. Pueden ejecutarse solas:

```powershell
dotnet test tests\MercadoCercano.Infrastructure.Tests\MercadoCercano.Infrastructure.Tests.csproj --configuration Release --filter FullyQualifiedName~CatalogServiceTests
```

Las pruebas de autenticacion existentes usan un host HTTP en memoria; no se
incluyen en ese filtro. El recorrido visual de login, formularios, subida de
imagenes y cierre de sesion debe verificarse manualmente desde F5 si no se
autoriza ejecutar navegadores o servicios desde el asistente.
