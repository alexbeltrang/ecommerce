# Mercado Cercano

Base de desarrollo para una tienda ecommerce colombiana con portal de
administración. La aplicación se está construyendo por fases; incluye una API
ASP.NET Core, una interfaz Angular y persistencia SQLite controlada por
migraciones de Entity Framework Core.

Incluye login y portal administrativo en Angular, gestion de categorias,
productos e imagenes, y catalogo publico con busqueda, filtros, detalle y
paginacion. Incluye carrito persistente, checkout de invitado, confirmacion y
seguimiento privado y configuracion de entregas. La operacion administrativa
incluye comprobantes privados, validacion manual de pagos, preparacion,
despacho, entrega y devoluciones/reembolsos parciales. La Fase 7 agrega
promociones automaticas y cupones, formularios administrativos, cupos e
integracion con checkout y devoluciones netas.
La Fase 8 incorpora reportes por eventos de ventas/cobros/reembolsos,
productos y margen estimado, inventario, promociones y exportacion CSV.
La Fase 9 incorpora administracion de usuarios, claves temporales con cambio
obligatorio, cuenta personal y revocacion de todas las sesiones.
La Fase 10 prepara Windows/IIS, publicacion conjunta de Angular/API,
aprovisionamiento offline y respaldos diarios verificados con restauracion.
Consulta [docs/catalog.md](docs/catalog.md), [docs/checkout.md](docs/checkout.md)
y [docs/operations.md](docs/operations.md), [docs/promotions.md](docs/promotions.md)
y [docs/reports.md](docs/reports.md), [docs/users.md](docs/users.md).

## Fases

1. Base de la solucion y entorno.
2. Dominio y SQLite con migraciones.
3. Autenticacion, roles y portal administrativo.
4. Catalogo y administracion de productos.
5. Carrito, checkout y pedidos.
6. Pago manual, cumplimiento y devoluciones.
7. Promociones y cupones.
8. Reportes e indicadores.
9. Administracion de usuarios.
10. Preparacion para produccion, respaldos y despliegue (Windows/IIS; sin desplegar).

## Requisitos

- .NET SDK 10 LTS
- Node.js compatible con Angular 21 (20.19+, 22.12+ o 24+) y npm
- Git

## Ejecutar en desarrollo

### Iniciar todo con F5 en VS Code

Abre la carpeta raiz del proyecto en VS Code e instala la extension recomendada
**C#** de Microsoft. Los depuradores de JavaScript y Chrome vienen incluidos en
VS Code. Necesitas Google Chrome instalado para la apertura automatica.

La primera vez, instala las dependencias del frontend desde su directorio:

```powershell
Set-Location src\MercadoCercano.Web
npm ci
```

En **Ejecutar y depurar**, selecciona **Mercado Cercano: aplicacion completa**
y pulsa **F5**. VS Code:

- Compila la API y restaura sus paquetes si hace falta.
- Inicia la API en `http://127.0.0.1:5136`, en Development.
- Aplica las migraciones SQLite y el sembrado de desarrollo al iniciar la API.
  SQLite no necesita un servidor separado.
- Espera el mensaje de la API indicando que ya esta escuchando, sin realizar
  consultas HTTP desde una terminal.
- Solo entonces lanza el grupo **Frontend y Swagger**: abre una sesion de
  Chrome dedicada a `http://127.0.0.1:5136/swagger` e inicia Angular CLI en
  `http://localhost:5173`. Angular abre el frontend en el navegador
  predeterminado cuando termina de compilar.
- La apertura de Swagger depende directamente del inicio de la API, no de
  detectar mensajes de Angular en una terminal. Swagger es la URL principal
  de su sesion, no un argumento adicional de Chrome.

El puerto del frontend es fijo: si esta ocupado, Angular informa el error en lugar
de cambiarlo silenciosamente. Para detener todo, detiene una de las sesiones
**Frontend** o **Swagger API** con **Shift+F5** (el grupo detiene ambas) y
despues detiene **Mercado Cercano: aplicacion completa** para cerrar la API.
La pestaña del frontend, abierta por Angular en el navegador predeterminado,
puede cerrarse manualmente.
Tambien puedes seleccionar **Swagger API** directamente en el selector de
depuracion para reabrir su navegador si la API ya esta ejecutandose.
Si aun no tienes usuarios, abre `/login` y usa **Crear primer administrador**.
Como alternativa, las variables de bootstrap deben estar disponibles en el
entorno de VS Code antes de iniciarlo; no guardes contrasenas en la
configuracion de depuracion.

Esta configuracion no agrega sondeos HTTP ni pruebas automaticas. El frontend
consume los endpoints de catalogo, autenticacion y pedidos a traves del proxy de
Angular CLI al navegar y usar los formularios. Es trafico normal del navegador,
no consultas desde PowerShell del asistente. Las politicas de monitoreo corporativo pueden registrar tambien la
ejecucion normal de los servidores y del navegador.

Si antes aparecio un error 502, deten la sesion anterior antes de volver a
pulsar F5. La pagina anterior no reintenta automaticamente la conexion.
Si la API falla al iniciar (por ejemplo, por un puerto ocupado o un error de
SQLite), el frontend no se abrira: revisa el error en **Consola de depuracion**.
Los errores de compilacion aparecen en la tarea **Compilar API**, y los de Angular
en la terminal **Frontend**.

### Inicio manual

Restaura la herramienta local de EF Core una vez:

```powershell
dotnet tool restore
```

En una terminal, desde la raíz del repositorio:

```powershell
dotnet run --project src\MercadoCercano.Api --launch-profile http
```

En otra terminal:

```powershell
Set-Location src\MercadoCercano.Web
npm ci
npm run dev
```

Abre `http://localhost:5173` para el catalogo y `/login` para el ingreso
administrativo. Las peticiones `/api` pasan por el proxy de Angular CLI.
El destino es `http://127.0.0.1:5136`; para cambiarlo,
ajusta `target` en `src/MercadoCercano.Web/proxy.conf.json`. Angular no consume
las antiguas variables `VITE_*` ni archivos `.env.local`.

## Verificación

```powershell
dotnet build MercadoCercano.sln
dotnet test MercadoCercano.sln
Set-Location src\MercadoCercano.Web
npm run build
npm run lint
```

El documento OpenAPI está disponible en desarrollo en
`/openapi/v1.json` en el puerto de la API. Swagger UI esta disponible en
`/swagger`, usando ese mismo documento y recursos servidos por la API (sin CDN).
Ambas rutas estan habilitadas unicamente en Development. La interfaz usa NSwag
solo para presentar Swagger UI; la generacion OpenAPI sigue siendo la nativa de
ASP.NET Core.

Los endpoints de autenticacion conservan la proteccion CSRF tambien desde
Swagger: solicita primero `/api/v1/auth/csrf` y envia el token en
`X-CSRF-TOKEN` para las operaciones POST. Abrir Swagger no omite la seguridad
de la API.

Las pruebas de servicios de catalogo y pedidos pueden ejecutarse sin HTTP ni
servidores usando el filtro documentado en [docs/checkout.md](docs/checkout.md).

## Estructura

- `src/MercadoCercano.Api`: ASP.NET Core Web API y configuración del host.
- `src/MercadoCercano.Application`: casos de uso y contratos de aplicación.
- `src/MercadoCercano.Domain`: modelo y reglas de negocio.
- `src/MercadoCercano.Infrastructure`: persistencia e integraciones.
- `src/MercadoCercano.Web`: Angular 21, TypeScript y Angular CLI.
- `BaseDatos`: SQLite local con las tablas y relaciones del proyecto.
- `docs/architecture.md`: límites y dirección de dependencias iniciales.

En desarrollo, la API aplica las migraciones pendientes y crea productos
ficticios desactivados, con SKU `DEMO-`, únicamente para demostración. No
representan productos ni precios reales.

## SQLite y migraciones

La base predeterminada se crea en `BaseDatos/mercado-cercano.db`, fuera del
directorio público del frontend. Configura otra ruta con la variable
`Database__Path`; se admiten rutas absolutas o relativas al directorio de la API.
El archivo y los directorios de SQLite están excluidos de Git.

La base anterior en `App_Data` no se elimina ni se modifica: la base de
`BaseDatos` se crea con el esquema completo mediante migraciones, sin copiar
datos de la ubicacion anterior. Para conservar datos de una base anterior,
realiza una copia consistente con SQLite antes de sustituir la nueva base;
no copies solo el archivo `.db` si existen transacciones en WAL.

Para revisar las migraciones o actualizar el esquema manualmente:

```powershell
dotnet ef migrations list --project src\MercadoCercano.Infrastructure --startup-project src\MercadoCercano.Api --context StoreDbContext
dotnet ef database update --project src\MercadoCercano.Infrastructure --startup-project src\MercadoCercano.Api --context StoreDbContext
```

La API no aplica migraciones automáticamente fuera de Development. Antes de
publicar, revisa y aplica las migraciones como parte del despliegue, con una
copia de seguridad previa. Consulta [docs/database.md](docs/database.md).

## Autenticación local

La API ofrece `POST /api/v1/auth/login`, `POST /api/v1/auth/logout` y
`GET /api/v1/auth/me`. Antes de iniciar o cerrar sesión, solicita
`GET /api/v1/auth/csrf` y envía el token recibido en `X-CSRF-TOKEN`. Obtén un
token nuevo después de iniciar sesión. Las cookies son HttpOnly; no guardes
credenciales ni cookies en `localStorage`.

Para crear el primer administrador local con F5, abre `/login` y pulsa
**Crear primer administrador**. Completa nombre, correo, contraseña y
confirmacion en `/configuracion-inicial`; despues inicia sesion normalmente.
El formulario solo permite el alta en Development mientras no exista ningun
usuario, y no guarda la contraseña en almacenamiento del navegador.
Requiere entre 8 y 32 caracteres, mayusculas, minusculas, numeros y al menos un
caracter especial, sin espacios. No crea una cuenta ni
contraseña predeterminadas.

Como alternativa, configura
`BOOTSTRAP_ADMIN_NAME`, `BOOTSTRAP_ADMIN_EMAIL` y `BOOTSTRAP_ADMIN_PASSWORD`
como variables de entorno antes de iniciar la API. La contraseña requiere entre
8 y 32 caracteres, mayúsculas, minúsculas, números y un carácter especial, sin espacios.
El bootstrap
solo corre en Development, se ejecuta una única vez por cuenta y no hay
credenciales predeterminadas. Consulta [docs/security.md](docs/security.md)
para el procedimiento en PowerShell, cookies y roles. En produccion se usa
aprovisionamiento offline interactivo; consulta [docs/production.md](docs/production.md).

El formulario de Angular en `/login` obtiene el token CSRF automaticamente y
redirige a `/admin`. Solo `Administrator` puede gestionar el catalogo;
`OrderOperator` accede al portal sin permisos de edicion de productos.
Ambos roles pueden operar pedidos, validar pagos y recibir devoluciones; solo
`Administrator` registra reembolsos y administra tarifas y condiciones en
`/admin/entregas`.

## Carrito y checkout

1. Reinicia la sesion de depuracion con F5 para cargar la API actualizada.
   Se aplica la migracion de pedidos y, solo en Development, se crea la
   configuracion inicial si no existe.
2. En `/admin/entregas`, revisa los valores ficticios: recogida 0 COP,
   domicilio 5.000 COP y transportadora 15.000 COP. El municipio inicial es
   **Municipio de prueba**. Puedes sustituirlos desde ese formulario.
3. Publica productos con existencias desde el catalogo administrativo.
4. Agrega productos al carrito y completa `/checkout`. Recalcula el resumen,
   revisa condiciones e importes y acepta antes de enviar.
5. Guarda el enlace privado de seguimiento. El pedido nace pendiente y
   **no reserva ni descuenta inventario**.
6. Desde `/admin/pedidos`, confirma para descontar stock atomicamente o cancela.
   Cancelar repone stock solo cuando se habia confirmado, una unica vez.

La configuracion de prueba no acepta pedidos fuera de Development. No hay
cobros automaticos ni notificaciones por correo/WhatsApp. La confirmacion
del negocio no marca el pago como realizado.

## Operacion de pedidos (Fase 6)

- En `/admin/pedidos`, filtra por pago, entrega, estado, recepciones o
  reembolsos pendientes; abre un pedido para usar los formularios operativos.
- Transferencia y billetera: adjunta un comprobante desde seguimiento o
  administracion, revisa los fondos reales y aprueba el importe completo
  antes de preparar. Subir un archivo no acredita dinero.
- Confirma para descontar stock; prepara, alista y despacha segun modalidad.
  La transportadora exige empresa y guia. Registra el efectivo contra entrega
  antes de marcar recibido por el cliente.
- Solicita devoluciones por producto/cantidad y registra su recepcion fisica;
  repone solo las unidades en buen estado. Solo el administrador registra un
  reembolso que ya realizo fuera del aplicativo.
- Prepara y edita mensajes para copiar o abrir WhatsApp/correo de forma
  explicita; no se envian automaticamente.

Los comprobantes son imagenes PNG/JPEG/WebP de hasta 750 KiB, no PDF, y
requieren acceso privado. Consulta [docs/operations.md](docs/operations.md)
para estados, cancelaciones pagadas, envios fallidos y limites.
