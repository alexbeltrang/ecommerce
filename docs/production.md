# Produccion en Windows con IIS

La Fase 10 prepara un paquete, configuracion y herramientas. **No despliega
ni crea infraestructura automaticamente.** Desarrollo/F5 conserva SQLite,
uploads y bootstrap anteriores. No se copian datos de desarrollo a produccion.
Los ejemplos siguientes son instrucciones para el operador, no comandos
ejecutados automaticamente al abrir el proyecto.

## Diseno y requisitos

- Windows Server soportado, IIS 10 y **.NET 10 Hosting Bundle** actualizado.
  Instala el bundle despues de IIS o reparalo tras instalar IIS. El servidor
  necesita el runtime ASP.NET Core y ANCM v2; no requiere Node ni el SDK.
- Publicacion framework-dependent Release: API y Angular bajo **el mismo
  origen HTTPS**, en la raiz `/`, no como subaplicacion. No requiere URL Rewrite
  ni Angular CLI en el servidor.
- Un App Pool dedicado, x64, **No Managed Code**, identidad ApplicationPoolIdentity,
  un solo worker (`maximumWorkerProcesses=1`). Deshabilita el reciclado solapado
  (`disallowOverlappingRotation=true`): un bloqueo exclusivo impide dos procesos
  sobre el mismo almacenamiento. No se soportan replicas ni SQLite en red.
- Dominio real, certificado TLS valido y vinculacion HTTPS 443 en IIS. Define
  el hostname tambien en AllowedHosts. HTTP redirige a HTTPS; HSTS se aplica
  fuera de Development. No uses el dominio ficticio del ejemplo.
- Esta receta termina TLS directamente en IIS. No habilites confianza global
  en X-Forwarded-For/Proto ni pongas un CDN/proxy adicional sin configurar
  explicitamente sus IPs y verificar la IP usada por los limitadores.

Rutas sugeridas, en disco local NTFS:

| Ruta | Contenido y permisos |
| --- | --- |
| `D:\MercadoCercano\site` | Binarios, configuracion y wwwroot Angular; IIS solo lectura/ejecucion |
| `D:\MercadoCercano\data\database` | SQLite y auxiliares; App Pool con Modify |
| `D:\MercadoCercano\data\products` | Imagenes, servidas exclusivamente por la API |
| `D:\MercadoCercano\data\payment-evidence` | Comprobantes privados |
| `D:\MercadoCercano\data\keys` | Data Protection persistente y protegida con DPAPI de la maquina |
| `D:\MercadoCercano\backups` | Copias verificadas; solo SYSTEM y Administradores |

Datos, copias y publicacion deben ser rutas absolutas distintas, no anidadas;
no se admiten raices de volumen, UNC, enlaces o junctions. No crees directorios
virtuales IIS para datos/copias. El proceso web no necesita acceso a respaldos.
El paquete no incluye SQLite, uploads, claves, secretos ni credenciales.

## 1. Preparar el paquete en la maquina de construccion

Con las dependencias ya restauradas, copia
[la plantilla](../deployment/iis/appsettings.Production.example.json) a un
archivo **privado fuera de Git**, y ajusta dominio, rutas y retencion:

```powershell
.\deployment\iis\Publish-Iis.ps1 `
  -ProductionConfigPath 'C:\ConfiguracionPrivada\appsettings.Production.json' `
  -Destination 'D:\Paquetes\mercado-cercano-release'
```

Destination debe ser nueva: el script no sobrescribe sitios existentes.
Compila Angular y publica ASP.NET Core; copia los recursos a wwwroot y los
scripts a operations. No inicia procesos web, migra bases ni configura IIS.
La configuracion real queda excluida de Git. Conserva las rutas coherentes si
usas variables de entorno: el mantenimiento y el worker deben recibir los
mismos valores; no definas Storage/Operations solo en el entorno del App Pool.
Los scripts respetan la politica PowerShell de la organizacion. Si exige
AllSigned, deben firmarse con un certificado de firma de codigo aprobado y
confiable antes de usarlos, incluidas las pruebas y los scripts programados.
No uses Bypass, no cambies la politica para ejecutarlos y vuelve a firmar
despues de modificarlos. El job no sobrescribe esa politica.

La publicacion nativa `dotnet publish` tambien incluye Angular ya compilado,
licencias y scripts, pero no instala la configuracion privada. La validacion
de scripts sin firma puede limitarse a su analisis estatico hasta contar con
la firma corporativa; eso no equivale a una prueba real de IIS.

Los archivos de Angular se agregan a `wwwroot` solo al publicar; no forman
parte del manifiesto de recursos estaticos del arranque local. Development/F5
usa Angular CLI por separado y no requiere una carpeta `wwwroot` en el
proyecto de la API. Si una compilacion anterior dejo un manifiesto que apunta
a esa carpeta inexistente, deten la depuracion con Shift+F5, ejecuta
`dotnet clean src\MercadoCercano.Api\MercadoCercano.Api.csproj --configuration Debug`
desde la raiz del repositorio y vuelve a iniciar con F5 para regenerar la salida.

## 2. Instalacion inicial, con el sitio detenido

1. Crea el App Pool y sitio con la configuracion indicada, sin abrirlo a
   usuarios. Copia el paquete a la carpeta site y asigna el binding/certificado.
2. Desde **Windows PowerShell como Administrador**, con el App Pool ya creado:

```powershell
$site = 'D:\MercadoCercano\site'
& "$site\operations\Initialize-IisStorage.ps1" -SitePath $site -AppPoolName MercadoCercano
& "$site\operations\Invoke-IisMaintenance.ps1" -SitePath $site -Command migrate
& "$site\operations\Invoke-IisMaintenance.ps1" -SitePath $site -Command create-admin
```

Initialize exige carpetas data/backups **nuevas**, aplica ACLs y registra la
fuente MercadoCercano en el Event Log Application. No se usa para reaplicar ACLs
sobre datos existentes: revisalos expresamente al actualizar o restaurar.
El sitio permite lectura al App Pool, no escritura. No guardes claves en
appsettings, argumentos de consola, tareas, perfiles, logs o archivos.

Migrate solo aplica esquema; no crea usuarios, productos ni condiciones de
prueba. Si ya existe SQLite, el wrapper exige un respaldo verificado antes
de migrar. No existe migracion automatica al iniciar produccion.
Create-admin pide nombre, correo y contraseña **sin mostrar la clave**,
confirma y crea hash/auditoria atomicamente; se cierra al existir cualquier
usuario, incluso inactivo. No inicia sesion, no restablece cuentas y no expone
un endpoint de aprovisionamiento de produccion. La clave exige 8-32,
mayuscula, minuscula, numero, especial y ningun espacio.

3. Inicia IIS manualmente. El arranque rechaza wildcard en AllowedHosts,
   SQLite inexistente, migraciones pendientes, Angular ausente, variables de
   bootstrap de desarrollo o falta de administrador definitivo activo.
4. Ingresa y configura `/admin/entregas` con municipios, tarifas, instrucciones
   bancarias y condiciones reales; desmarca configuracion de prueba. Hasta
   entonces el checkout no recibe pedidos reales. Crea categorias/productos
   reales y conserva desactivados los ficticios.
5. Revisa manualmente HTTPS/cookies, login/logout, ambos roles, cambio temporal,
   recarga de rutas Angular, catalogo, checkout, subida privada, reportes y CSV.
   Swagger/OpenAPI y alta inicial web siguen deshabilitados en produccion.

## 3. Respaldo diario, 02:00 y 14 copias

```powershell
& "$site\operations\Register-IisBackup.ps1" -SitePath $site
```

Registra, **sin ejecutarla**, MercadoCercano-Backup bajo SYSTEM a las **02:00,
hora local del servidor**. No guarda contraseñas de tareas. La tarea no se
solapa consigo misma, tiene limite de dos horas y recupera una ejecucion
perdida al estar disponible el servidor; esto puede causar mantenimiento
fuera de las 02:00 despues de un reinicio. Ajusta la zona horaria del servidor
y verifica la hora mostrada en el Programador de tareas.

El wrapper crea app_offline.htm sin sobrescribir otro mantenimiento, espera
hasta 60 segundos a que IIS libere el bloqueo exclusivo y ejecuta la consola
**sin arrancar host ni hacer HTTP**. Mientras tanto IIS muestra mantenimiento.
Se usa la API de backup SQLite, no una copia aislada del .db en WAL. El destino
se consolida en journal DELETE para ser autocontenido; se incluyen imagenes,
comprobantes y claves. Se comprueban SHA-256/longitud de cada archivo,
integrity_check, foreign_key_check y archivos referenciados por la base.

Solo se publica el directorio backup-* cuando todo pasa. Se conservan las
14 copias completas mas recientes (incluyendo siempre la recien creada);
la eliminacion solo alcanza copias anteriores reconocidas y verificadas.
No se borran carpetas ajenas, copias corruptas ni .partial-* fallidas.
Un error de retencion se informa aunque la copia nueva ya exista: investiga,
no lo interpretes como exito de la tarea.

Tras respaldo exitoso o fallido de solo lectura se retira **solo** el offline
propio. Un fallo de migracion/restauracion deja el sitio offline. Si cambia
el archivo de mantenimiento, no se retira el de otro operador. Una terminacion
forzada puede dejar offline y .partial-*: revisa antes de limpiar.

Respaldo manual:

```powershell
& "$site\operations\Invoke-IisMaintenance.ps1" -SitePath $site -Command backup
```

Los respaldos contienen datos personales y hashes: SHA-256 detecta corrupcion,
**no autentica ni cifra** el contenido. Usa BitLocker, ACLs privadas, volumen
con espacio para 14 copias completas mas temporales, y una copia adicional
cifrada/fuera del servidor administrada por tu operacion. Esta fase no configura
almacenamiento remoto ni garantiza recuperacion ante perdida de todo el disco.
El objetivo nominal entre respaldos es 24 horas; el RTO debe medirse con tus
datos reales, no se deduce de las pruebas pequenas.

## 4. Restauracion y simulacro

Prueba primero una copia en almacenamiento aislado con configuracion privada
que apunte a otras carpetas data/backups y un paquete separado. No uses
produccion para el simulacro. Selecciona un nombre real de respaldo:

```powershell
$copy = 'backup-20261020T0200000000000Z-1234abcd' # sustituir
& "$site\operations\Invoke-IisMaintenance.ps1" -SitePath $site -Command verify-backup -BackupName $copy
& "$site\operations\Invoke-IisMaintenance.ps1" -SitePath $site -Command restore -BackupName $copy -ConfirmRestore
```

Restore requiere confirmacion explicita, verifica antes de reemplazar y prepara
los datos en un staging privado. Conserva el estado previo en data\recovery-*
y revierte los movimientos si falla el reemplazo. Si tambien falla esa
reversion, informa ambos errores y exige recuperacion manual con IIS offline.
No se borran automaticamente recovery-*; revisalos y retiralos expresamente
cuando el simulacro y el nuevo respaldo esten verificados.

Las credenciales y registros regresan a la fecha del respaldo, pero **todos los
sellos de sesion se regeneran**, evitando revivir cookies antiguas. Se registra
auditoria de restauracion. Revisa pedidos/pagos ocurridos despues del respaldo
contra tus registros externos antes de reabrir; no repitas cobros o reembolsos.

DPAPI liga las claves al Windows original. Para recuperar en **otra maquina**,
usa -NewMachine junto con -ConfirmRestore: no importa las claves DPAPI antiguas;
IIS genera claves nuevas en el directorio privado y exige nuevo login/CSRF.
No altera hashes de contraseña. Aprovisiona ACLs y certificado en esa maquina,
verifica esquema y condiciones comerciales antes de abrirla.

## 5. Actualizaciones y rollback

Construye en una carpeta nueva; conserva el paquete anterior y la configuracion
privada. Deten el sitio, toma y verifica respaldo, copia/apunta el nuevo paquete,
revisa permisos de lectura y ejecuta migrate con el wrapper. No publiques sobre
un sitio activo ni mezcles binarios de versiones distintas. Inicia y verifica
manualmente. No reviertas solo binarios si hubo cambios de esquema: vuelve al
paquete anterior **junto con su respaldo compatible**, conciliando operaciones
externas. Nunca ejecutes migraciones descendentes improvisadas en produccion.

## 6. Observabilidad y limites pendientes

- `/api/v1/health` es liveness. `/api/v1/health/ready` devuelve 200 solo con base,
  esquema y administrador disponibles; ante fallo, 503 generico y log privado.
  No revela rutas, correos ni excepciones. Readiness no certifica tarifas,
  fondos bancarios, certificados, backups recientes ni condiciones legales.
- Revisa Event Viewer > Application > MercadoCercano: eventos 1000/1001 para
  mantenimiento, errores de arranque/operacion y fallos de readiness.
  Revisa LastTaskResult, ultima ejecucion y existencia de copia verificada
  reciente en el Programador de tareas. Configura alertas externas para fallos,
  ausencia de respaldo por mas de 24 horas, espacio libre, 5xx y expiracion TLS;
  no hay un servicio externo de alertas instalado por el proyecto.
- IIS conserva sus access logs segun tu politica de retencion. stdout de ANCM
  esta deshabilitado; para diagnostico temporal usa una carpeta privada con
  permisos y retiralo despues. No habilites logs de cuerpos/contraseñas,
  evidencias o datos bancarios. Protege los logs porque pueden contener URLs.
- Limites locales por IP/instancia, no distribuidos; sin MFA, proveedor externo,
  correo automatico, pagos automaticos ni analisis antimalware integrado.
  Antes de abrir al publico, revisa politica de tratamiento de datos,
  comprobantes/retencion, recuperacion de administradores y respuesta a incidentes.

## Validacion sin servidores

```powershell
dotnet build MercadoCercano.sln --configuration Release --no-restore --disable-build-servers
dotnet test tests\MercadoCercano.Infrastructure.Tests\MercadoCercano.Infrastructure.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~ProductionOperationsTests --disable-build-servers
powershell -NoProfile -File tests\scripts\IisMaintenance.Tests.ps1
```

Las pruebas usan bases/rutas temporales, provisiones ficticias, backups,
restauracion real aislada y comandos IIS simulados; no inician host, navegador,
IIS ni tareas. El binding, TLS y el recorrido visual requieren validacion manual
en la infraestructura elegida.
