# Usuarios y cuenta personal

La Fase 9 agrega `/admin/usuarios` para administradores y `/mi-cuenta` para
ambos roles. No hay registro publico, eliminacion fisica de usuarios ni
recuperacion automatica por correo. El primer administrador de desarrollo
conserva el procedimiento de [security.md](security.md).

## Administrar cuentas

- Busca por nombre o correo, filtra por rol/estado y pagina los resultados.
  Las cuentas inactivas siguen visibles y conservan su historial.
- Crea un Administrador u Operador de pedidos con nombre, correo, estado y
  contraseña temporal confirmada. El rol inicial del formulario es Operador.
  El correo es unico incluso para cuentas inactivas, sin distinguir mayusculas
  ni espacios exteriores.
- Entrega la contraseña temporal por un canal privado; la aplicacion no la
  devuelve, almacena en el navegador ni envia por correo. Cada alta exige
  cambiarla antes de entrar al portal, tambien para nuevos administradores.
- Edita perfil, rol o estado con la version consultada. Un conflicto 409 exige
  recargar y revisar antes de guardar. Todo guardado de perfil revoca las
  sesiones del destinatario, incluso si solo cambia el nombre o correo.
- Restablece la contraseña de otra cuenta con una temporal nueva y diferente
  de la actual. Primero guarda o recarga los cambios de perfil pendientes.
  El restablecimiento no reactiva una cuenta desactivada.
- No puedes desactivar ni cambiar el rol de tu propia cuenta. Tampoco puedes
  restablecer tu propia contraseña desde Usuarios: usa Mi cuenta.
  Siempre debe conservarse al menos un administrador activo.

Los cambios y su auditoria se confirman juntos en una transaccion SQLite.
La comprobacion del administrador y la proteccion frente a desactivaciones
concurrentes ocurren dentro de esa transaccion. Las contraseñas, hashes y
sellos de sesion nunca se incluyen en respuestas ni auditoria.

## Contraseña temporal y cambio personal

1. Ingresa en `/login` con el correo y la contraseña asignada.
2. Si es temporal, se abre `/mi-cuenta`: introduce la temporal como contraseña
   actual y elige/confirma una nueva. La API tambien bloquea las politicas de
   administracion y operacion hasta completar el cambio; no depende del guard.
3. Se cierran todas tus sesiones, incluida la actual, sin renovacion automatica.
   Ingresa de nuevo con la nueva contraseña para acceder al portal.

Mi cuenta tambien permite cambios voluntarios para ambos roles, verificando
la contraseña actual. La nueva debe ser diferente y cumplir 8 a 32 caracteres,
mayuscula, minuscula, numero y caracter especial, sin espacios. Las claves
anteriores largas siguen admitidas como contraseña actual (hasta 256), pero
la nueva debe cumplir la politica vigente. Las cuentas existentes no reciben
contraseñas nuevas ni cambio obligatorio al migrar.

Si se pierde la respuesta, intenta ingresar con la nueva contraseña antes de
repetir un cambio. Para recuperar otra cuenta, un administrador puede asignar
una temporal; no existe recuperacion publica. Conserva acceso administrativo
seguro. El primer administrador de produccion se aprovisiona offline segun
[production.md](production.md); no se usa el formulario de Development.

## Revocacion y protecciones

Cada peticion autenticada comprueba cuenta activa, rol, sello de sesion y
marca de cambio obligatorio. Cambiar/restablecer contraseña o guardar perfil,
rol o estado rota el sello: todas las cookies anteriores son rechazadas en
su siguiente peticion. Reactivar o devolver el rol no revive cookies antiguas.
Esto no interrumpe operaciones que ya estaban en curso.

Guardar tu propio perfil o cambiar tu contraseña cierra ademas la cookie
actual y vuelve al login. Las cookies previas a esta migracion carecen del
sello requerido y exigen iniciar sesion nuevamente.

Las escrituras requieren CSRF. Crear cuentas, restablecer y cambiar contraseña
comparten con login/alta inicial el limite de cinco intentos por minuto/IP;
un 429 exige esperar. Listados y respuestas privadas usan no-store.
Las contraseñas solo permanecen en memoria mientras se usa el formulario;
no se guardan en localStorage, sessionStorage, URLs ni archivos.

## API y validacion local

| Operacion | Ruta | Resultado |
| --- | --- | --- |
| Listar/filtrar | `GET /api/v1/admin/users` | 200, pagina sin secretos |
| Consultar/version | `GET /api/v1/admin/users/{id}` | 200 o 404 |
| Crear | `POST /api/v1/admin/users` | 201 con Location |
| Guardar perfil/estado/rol | `PUT /api/v1/admin/users/{id}` | 200 o 409 por version/protecciones |
| Restablecer otra cuenta | `POST /api/v1/admin/users/{id}/reset-password` | 200, cambio obligatorio |
| Cambiar mi contraseña | `POST /api/v1/account/password` | 204 y cierre de sesion |

Los errores usan Problem Details (400, 403, 404, 409); sin sesion valida, 401.
Los ejemplos de la API son manuales y no se ejecutan automaticamente.
Las pruebas aisladas no inician servidor ni realizan solicitudes HTTP:

```powershell
dotnet test tests\MercadoCercano.Infrastructure.Tests\MercadoCercano.Infrastructure.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~UserAccounts --disable-build-servers
```

Incluyen claves, roles, permisos, conflictos, auditoria atomica, migracion
compatible, cookies con autenticacion simulada y administradores concurrentes.
El recorrido visual se realiza manualmente con F5.
