# Seguridad de autenticación

## Sesiones y credenciales

La API usa contraseñas procesadas con `PasswordHasher<User>` de ASP.NET Core
Identity. Nunca guarda ni registra contraseñas en texto plano. Las credenciales
no se incluyen en la base de datos de configuración, el repositorio ni los
logs.

En desarrollo, si no existe ningun usuario, `/login` muestra **Crear primer
administrador**, con formulario en `/configuracion-inicial`. El usuario elige
nombre, correo y contraseña; no hay credenciales predeterminadas. La contraseña
solo se conserva en memoria mientras se completa el formulario; no se guarda
en localStorage, sessionStorage ni URLs. El servidor guarda un hash, no la clave.

El alta inicial exige CSRF y comparte el limite de cinco intentos/minuto/IP
con login. Comprueba dentro de una transaccion SQLite que no exista ningun
usuario, incluso inactivo u operador; dos solicitudes concurrentes no crean
dos cuentas. Se guarda auditoria junto con el usuario y, si falla cualquiera,
se revierte todo. El alta no inicia sesion: despues ingresa normalmente.
No permite restablecer contrasenas, reactivar cuentas ni elegir un rol.

`GET /api/v1/auth/initial-administrator` devuelve solo `available`.
`POST` en esa ruta devuelve 201 con Location al perfil protegido; fuera de
Development no permite crear usuarios y, si ya hay alguno, responde 409.
No expongas Development a redes publicas; conserva la API local en loopback.
Produccion usa aprovisionamiento offline interactivo, no este endpoint;
consulta [production.md](production.md).

Como alternativa de desarrollo sigue disponible el bootstrap con las tres
variables `BOOTSTRAP_ADMIN_NAME`, `BOOTSTRAP_ADMIN_EMAIL` y
`BOOTSTRAP_ADMIN_PASSWORD`. La contraseña debe tener entre 8 y 32 caracteres,
mayúsculas, minúsculas, dígitos y al menos un carácter especial, sin espacios.
Esta regla se comparte con el formulario inicial; no
cambia ni invalida las contraseñas de cuentas existentes al iniciar sesion. La
creación es idempotente: no restablece la contraseña de una cuenta existente.
No hay usuario ni contraseña predeterminados.

Ejemplo de PowerShell para proporcionar las credenciales solo al proceso local:

```powershell
$env:BOOTSTRAP_ADMIN_NAME = 'Nombre del administrador'
$env:BOOTSTRAP_ADMIN_EMAIL = 'admin@localhost.test'
$securePassword = Read-Host 'Contraseña inicial (8 a 32 caracteres)' -AsSecureString
$env:BOOTSTRAP_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new('', $securePassword).Password
dotnet run --project src\MercadoCercano.Api --launch-profile http
Remove-Item Env:BOOTSTRAP_ADMIN_NAME, Env:BOOTSTRAP_ADMIN_EMAIL, Env:BOOTSTRAP_ADMIN_PASSWORD
```

Al volver a iniciar la API, quita o rota las variables: el bootstrap solo crea
la cuenta cuando aún no existe. Guarda la base local en un lugar privado. El
bootstrap está deliberadamente limitado a `Development`. El comando offline
de produccion exige consola privada, base sin usuarios y confirmacion de clave.

Las pruebas aisladas del alta inicial no arrancan un host ni hacen HTTP:

```powershell
dotnet test tests\MercadoCercano.Infrastructure.Tests\MercadoCercano.Infrastructure.Tests.csproj --configuration Release --filter FullyQualifiedName~InitialAdministratorServiceTests --disable-build-servers
```

Comprueban hash, auditoria atomica, cierre ante cualquier usuario, bloqueo
fuera de Development, limites exactos de contraseña, concurrencia y
compatibilidad del bootstrap por variables. La creacion con datos reales y
el recorrido visual del formulario se hacen manualmente desde el navegador.

## Cookie, CSRF y autorización

- `POST /api/v1/auth/login`, `POST /api/v1/auth/logout` y todos los endpoints
  futuros que cambien estado validan antifalsificación globalmente.
- Obtén un token en `GET /api/v1/auth/csrf` y envíalo en la cabecera
  `X-CSRF-TOKEN`. La respuesta del token no se puede almacenar en caché.
- Solicita un token nuevo después de iniciar sesión: ASP.NET Core vincula el
  token de solicitud a la identidad autenticada.
- La cookie de autenticación es `HttpOnly`, `SameSite=Lax`, de sesión y expira
  tras ocho horas de inactividad. La cookie antifalsificación es `HttpOnly` y
  `SameSite=Strict`. En producción ambas requieren HTTPS y usan prefijo
  `__Host-`; en desarrollo HTTP local se permite para el proxy de Angular CLI.
- `GET /api/v1/auth/me` devuelve identificador, nombre, correo, rol y
  `mustChangePassword`, nunca hashes ni sellos de sesion.
  Cada petición autenticada vuelve a comprobar que el usuario siga activo y
  que coincidan rol, sello de sesion y marca de cambio obligatorio.
- Las políticas `Administrator` y `OrderOperations` se aplican en el servidor.
  Protege los endpoints administrativos futuros con `[Authorize(Policy = ...)]`;
  ocultar controles del frontend no sustituye esa autorización.
- Login se limita a cinco intentos por minuto y dirección IP. La respuesta a
  correo inexistente, contraseña incorrecta o cuenta desactivada es la misma.
  Crear usuarios, restablecer y cambiar contraseña comparten ese mismo limite
  con login y alta inicial; requieren CSRF y no guardan claves en auditoria.
- Una clave temporal permite iniciar sesion, consultar la cuenta y cambiar
  contraseña, pero no satisface `Administrator` ni `OrderOperations`.
  El cambio personal exige la contraseña actual y cierra todas las sesiones,
  incluida la actual, sin renovarla. Guardar perfil/rol/estado o restablecer
  otra cuenta tambien revoca sus cookies. Se rechazan en su siguiente peticion;
  reactivar una cuenta no revive sesiones anteriores.
- Solo Administrator administra usuarios; no puede desactivarse ni cambiar su
  propio rol. No hay borrado fisico y debe quedar un administrador activo,
  tambien ante cambios concurrentes. Consulta [users.md](users.md).
- Checkout limita por IP y minuto las cotizaciones (30), creaciones (5) y
  seguimiento/recuperacion (15). El codigo de seguimiento se envia en el cuerpo
  o `X-Order-Token`, nunca en query string, y se guarda solo como SHA-256.
- Los pedidos publicos no exponen datos de contacto, direccion ni costos
  internos. `OrderOperations` permite consultar datos privados, confirmar y
  cancelar, validar pagos y operar entregas/devoluciones; solo `Administrator`
  puede registrar reembolsos y cambiar tarifas, condiciones y promociones/cupones. El servicio
  comprueba tambien el rol activo del administrador dentro de la transaccion
  del reembolso.
- El API no configura CORS permisivo. La tienda debe consumirlo en el mismo
  origen mediante el proxy local o el reverse proxy de publicación.

En producción, configura TLS y el reverse proxy con una lista explícita de
proxies confiables antes de habilitar encabezados reenviados. No confíes
`X-Forwarded-For` directamente desde clientes públicos: el limitador de login
usa la IP que presenta ASP.NET Core. El limitador actual es local a cada
instancia; una publicación con varias réplicas debe aplicar límites compartidos
en el gateway o almacenamiento distribuido.

## Límites de esta fase

El portal Angular incluye login, cierre de sesion y guards de rol. Los
endpoints de categorias, productos e imagenes requieren `Administrator`.
Los endpoints administrativos de promociones e historial de usos tambien
requieren `Administrator`, CSRF en escrituras y version en ediciones.
El checkout no acepta importes promocionales del cliente: valida condiciones,
cupo y beneficio en el servidor. El historial de usos no incluye datos personales.
Los reportes y exportaciones CSV requieren Administrator y no-store;
incluyen costos y margenes, por lo que no estan disponibles al operador.
No exportan contactos de compradores, direcciones ni referencias privadas
de pago/reembolso. El CSV protege celdas interpretables como formulas y
conserva importes grandes como texto. Consulta [reports.md](reports.md).
Las respuestas de sesion y de datos administrativos no se almacenan en cache.
La subida de imagenes comprueba limites de tamano, tipo MIME y firma binaria;
consulta [catalog.md](catalog.md) para almacenamiento y limites.

Los comprobantes de pago se guardan en `uploads/payment-evidence`, nunca en
una ruta estatica. Solo el personal autorizado descarga un archivo asociado al
pedido consultado: attachment, no-store, nosniff y CSP sandbox. Se aceptan
PNG/JPEG/WebP de hasta 750 KiB, con firma binaria compatible, hasta cinco
archivos historicos por pago. No PDF ni SVG. Esta comprobacion no sustituye un
analisis antimalware o una politica de retencion de produccion; no se purgan
automaticamente comprobantes historicos.

La carga publica exige numero y codigo privado, CSRF, version del pedido y
limite de cinco solicitudes/minuto/IP. No permite descargar comprobantes.
El seguimiento muestra motivos publicos de rechazo/devolucion, guia y totales
reembolsados; nunca referencias bancarias ni rutas de archivos privados.
No incluyas datos sensibles en motivos publicos. Los pagos y reembolsos son
registros manuales, no instrucciones a un banco o pasarela.

El carrito conserva solo IDs y cantidades en `localStorage`. Un intento de
checkout guarda temporalmente su GUID, codigo privado y firma del carrito en
`sessionStorage`, para evitar duplicaciones al perder una respuesta. No
contiene datos personales ni credenciales administrativas. Se elimina al
completar; si el navegador impide limpiarlo, se informa el error. El codigo
es sensible y accesible a scripts del mismo origen, a diferencia de la cookie
HttpOnly. Consulta [checkout.md](checkout.md) para los limites y precauciones.

El formulario administrativo de devolucion conserva solo un GUID por pedido en
`sessionStorage` para recuperar una respuesta perdida: no guarda motivos,
referencias ni datos personales. Los mensajes a clientes son borradores
editables; copiar o abrir WhatsApp/correo exige una accion del usuario y no
registra un envio como completado. Consulta [operations.md](operations.md).

Los enlaces de seguimiento incluyen el codigo en el fragmento, no transmitido
en la URL HTTP o Referer. Siguen visibles en el historial del navegador y para
quien obtenga el enlace; no los compartas publicamente. Los motivos de
cancelacion son publicos para quien tenga ese codigo y no deben incluir PII.

Se implementan cambio personal con clave actual y restablecimiento por otro
administrador con clave temporal. Las cuentas anteriores conservan sus
credenciales; las cookies anteriores a la migracion de usuarios requieren
nuevo login. No se implementan recuperacion publica/automatica, MFA, bloqueo
persistente, integracion de identidad
externa. Produccion aprovisiona el primer administrador exclusivamente desde
la consola offline, sin claves en argumentos/configuracion. No publiques sin
verificar TLS, permisos, condiciones reales y restauracion de respaldos.
La protección CSRF no sustituye la
prevención de XSS ni la autorización de cada operación.

## Preparacion de produccion

IIS sirve Angular y API bajo el mismo origen HTTPS; AllowedHosts no admite
comodines. SQLite, imagenes, comprobantes, claves y respaldos estan separados
de la publicacion. Las claves Data Protection se persisten con nombre de
aplicacion estable y DPAPI de maquina; no se deben copiar a wwwroot.
El arranque no migra ni siembra produccion y rechaza esquema incompleto o
ausencia de administrador operativo. El servidor tampoco crea SQLite si falta.
Una lease de archivo impide workers concurrentes sobre el mismo almacenamiento.

Las cabeceras de produccion incluyen nosniff, no framing, referrer policy,
permissions policy y CSP sin JavaScript inline. Angular deshabilita la
insercion de CSS critico que produciria manejadores inline incompatibles.
Los endpoints privados conservan CSRF y no-store; un API inexistente nunca
se devuelve como index Angular con estado 200.

Los respaldos verificados incluyen datos privados: protegelos con ACLs y
cifrado del volumen/copia externa. Su manifiesto SHA-256 no sustituye
autenticacion ni cifrado. Restaurar regenera sellos de sesion, preserva el
estado anterior y exige confirmar; una restauracion entre maquinas descarta
las claves DPAPI antiguas. Consulta [production.md](production.md).
