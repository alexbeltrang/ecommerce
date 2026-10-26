# Operacion de pedidos (Fase 6)

## Acceso y formularios

En `/admin/pedidos`, busca por numero/contacto y filtra por estado, pago,
modalidad de entrega, recepciones pendientes o reembolsos pendientes.
Estos dos ultimos filtros pueden combinarse: exigen que el pedido tenga
solicitudes aun por recibir y obligaciones recibidas aun por reembolsar.
Abre un pedido para sus formularios de pago, entrega, devoluciones y contacto.

| Accion | Administrator | OrderOperator | Cliente con codigo privado |
| --- | --- | --- | --- |
| Adjuntar comprobante | Si | Si | Si, desde seguimiento |
| Descargar comprobantes privados | Si | Si | No |
| Aprobar/rechazar pago | Si | Si | No |
| Confirmar, preparar, alistar, despachar, entregar | Si | Si | No |
| Cancelar antes del despacho | Si | Si | No |
| Solicitar, recibir o rechazar devolucion | Si | Si | No |
| Registrar reembolso ya realizado | Si | No | No |
| Preparar mensajes manuales | Si | Si | No |

La API aplica estos permisos; ocultar botones no es la autorizacion.
Las mutaciones exigen CSRF y la version exacta del pedido. Ante conflicto,
actualiza el detalle y revisa el estado antes de operar de nuevo.
Los pedidos de prueba no admiten operaciones comerciales fuera de Development.

## Pago manual y comprobantes

Para transferencia o billetera:

1. El cliente abre su enlace privado de seguimiento, o el personal el detalle.
2. Adjunta referencia e imagen PNG/JPEG/WebP de hasta **750 KiB
   (768.000 bytes)**. No PDF ni SVG. Hay hasta cinco comprobantes historicos
   por pago; un rechazo no elimina los anteriores.
3. El pago pasa a **En revision**; el archivo por si solo no acredita dinero.
4. El operador revisa el comprobante y verifica los fondos fuera del sistema.
5. Aprueba con el importe completo original y la casilla de fondos recibidos,
   o rechaza con motivo visible al cliente. Tras rechazo puede cargarse otro
   comprobante. No se admite sustituir un pago ya aprobado.

No hay pagos parciales ni cuotas. Las referencias son privadas. El cliente no
puede descargar comprobantes, ni siquiera con su codigo de seguimiento.
El almacenamiento no conserva el nombre original del archivo; la base guarda
metadatos y el archivo vive en `uploads/payment-evidence` bajo la raiz del repositorio.
No existe borrado automatico por antiguedad: define retencion y control de
acceso del almacenamiento antes de usar datos reales.

Para efectivo contra entrega no se carga comprobante: se registra el importe
completo realmente cobrado con referencia y casilla de verificacion, solo
cuando el pedido esta **Listo** o **Despachado**, antes de marcar entrega.
Los formularios de prueba no deben usarse para realizar movimientos reales.

Si una promocion cubre todo el importe, el pedido se presenta **Sin cobro**:
no requiere comprobante ni confirmacion de fondos. Su pago resuelto no
significa que se haya recibido una transferencia; el flujo fisico sigue vigente.

## Estados y entrega

Confirmar descuenta inventario una unica vez; no cambia el estado del pago.

| Paso | Origen | Destino | Condicion |
| --- | --- | --- | --- |
| Confirmar | Pendiente | Confirmado | Existencias de todas las lineas |
| Preparar | Confirmado | En preparacion | Transferencia/billetera pagada; efectivo puede estar pendiente |
| Alistar | En preparacion | Listo | Flujo previo completo |
| Despachar | Listo | Despachado | Domicilio o transportadora, nunca recogida |
| Entregar recogida | Listo | Entregado | Pago completo y recepcion confirmada |
| Entregar envio | Despachado | Entregado | Pago completo y recepcion confirmada |

Transportadora requiere empresa y numero de guia al despachar. Se conservan
fecha UTC de despacho/entrega y datos publicos de seguimiento. No hay consulta
automatica al transportador ni entrega marcada por una notificacion externa.

Cancelar permite pendientes, confirmados, en preparacion y listos; requiere
motivo publico. Los pagos en revision deben aprobarse o rechazarse antes.
Se restaura solo inventario previamente descontado, una vez. Si estaba pagado,
se crea una obligacion por **todo el pedido, incluido el envio**, lista para
reembolso sin recibir mercancia otra vez. Cancelar no transfiere fondos.
No se cancela directamente un pedido ya despachado o entregado.

## Devoluciones parciales y envios fallidos

Despues de entregar, selecciona productos y cantidades, motivo publico y,
si procede, importe de envio que el negocio acuerda devolver. El servidor
calcula los productos usando sus importes historicos netos despues de ofertas
y del beneficio adicional, no precios actuales ni costos internos. Reparte el
dinero restante entre las unidades pendientes: las ultimas absorben los pesos
de redondeo. Rechazar libera tanto unidades como importe reservado.
Consulta [promotions.md](promotions.md).

- Una solicitud puede incluir varias lineas; cantidades cero no se envian.
- Solicitudes solicitadas, recibidas y reembolsadas reservan cantidades y
  devolucion de envio. No puede solicitarse mas de lo comprado ni devolver
  mas envio del cobrado. Rechazar antes de recibir libera esos saldos.
- El formulario muestra una estimacion; el resultado del servidor es definitivo.
- El GUID del intento se guarda sin datos personales en `sessionStorage`.
  Si se pierde la respuesta, **Recuperar solicitud** consulta ese intento.
  Repetirlo con igual contenido devuelve la misma solicitud; cambiarlo produce
  conflicto. No descartes un intento tras timeout sin verificar primero.

En un pedido **Despachado**, el flujo es retorno de envio fallido: exige todas
las lineas y cantidades, no una devolucion parcial anticipada. Si estaba
pagado se calcula su reembolso; en efectivo aun no cobrado el importe es cero.
La recepcion del retorno cierra el pedido como cancelado.

### Recepcion fisica

Confirma que recibiste toda la mercancia de esa solicitud. En cada linea,
indica cuantas unidades llegaron **en buen estado y vendibles**; el valor
inicial es cero. Las danadas no vuelven al stock.

La recepcion crea movimientos `CustomerReturn` solo por unidades vendibles.
No registra dinero ni lo devuelve. Es atomica, controla desbordamientos y no
se repite por un reintento. No permite corregir cantidades despues de recibir;
revisa el formulario antes de confirmar.

### Reembolso

Solo un administrador puede registrar un reembolso despues de recibir la
solicitud, o de cancelar un pedido pagado antes del despacho.
Primero realiza la devolucion de fondos por el medio externo acordado; luego
registra **exactamente** el importe calculado, su referencia privada y la
casilla de reembolso ya completado.

Cada solicitud tiene un reembolso completo, no abonos parciales sobre esa
solicitud. Varias solicitudes pueden devolver parcialmente un pedido.
El total acumulado nunca excede lo pagado. Un reembolso parcial conserva el
pago como **Pagado** y muestra por separado el total reembolsado; cuando se
devuelve todo, el pago pasa a **Reembolsado**; el estado logistico del pedido
se conserva (entregado o cancelado segun su flujo).
Reembolsar no mueve inventario: eso corresponde a recepcion/cancelacion.

## Mensajes manuales

El panel de contacto prepara un borrador editable segun el estado. Revisa
destinatario y texto antes de copiar o abrir WhatsApp/correo. Para numeros
colombianos de diez digitos se agrega el indicativo 57. Los pedidos de prueba
incluyen un aviso PRUEBA. El borrador no contiene el codigo privado de
seguimiento, que el servidor no conserva en claro.

Preparar, copiar o abrir una aplicacion no acredita envio: no existe estado
falso de mensaje enviado, envio automatico, proveedor conectado ni polling.

## API, base y validacion

Las operaciones cuelgan de `/api/v1/admin/orders/{id}`:

- `POST /fulfillment/{prepare|ready|ship|deliver}`
- `POST /payment/approve`, `/payment/reject`, `/payment/submit-evidence`
- `GET /payment/evidence/{evidenceId}` (archivo privado)
- `POST /returns`, `GET /returns/by-request/{requestId}`
- `POST /returns/{returnId}/receive`, `/reject`, `/refund`

La carga del titular es `POST /api/v1/checkout/payment/submit-evidence`,
multipart con numero, codigo privado en el cuerpo, version, referencia y archivo.
Su limite es cinco solicitudes/minuto/IP; no reintenta automaticamente.
Crear devolucion devuelve 201 y Location al recurso de recuperacion.
Los ejemplos de [la API](../src/MercadoCercano.Api/MercadoCercano.Api.http)
son exclusivamente manuales.

`OrderOperationsAndPartialReturns` agrega tres tablas, columnas operativas y
el movimiento de retorno. Consulta [database.md](database.md) para respaldar
SQLite y ambos directorios de archivos.

Las pruebas filtradas en [checkout.md](checkout.md) incluyen permisos, fondos,
comprobantes y su limpieza ante fallo de guardado/commit, cantidades e importes,
envios fallidos, filtros, idempotencia/concurrencia de recepcion y reembolso,
y preservacion de movimientos durante la migracion. No usan servidor ni HTTP.
Angular se valida con build estricto y lint; el recorrido visual se realiza
manualmente con F5, no mediante sondas del asistente.
