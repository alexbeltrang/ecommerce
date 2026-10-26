# Promociones y cupones (Fase 7)

## Portal administrativo

Entra como **Administrator** y abre **Promociones y cupones**
(`/admin/promociones`). El operador puede consultar el beneficio historico
de un pedido, pero no administrar reglas promocionales.

El listado tiene busqueda por nombre/codigo, filtros automatico/cupon,
activas/inactivas y paginacion. El formulario permite:

- Nombre publico, tipo automatico o cupon con codigo.
- Porcentaje entero entre 1 y 100 o importe fijo entero COP.
- Compra minima de mercancia elegible y tope opcional del descuento.
- Alcance: toda la mercancia, una categoria o un producto. La seleccion de
  producto incluye buscador y paginacion; no exige copiar identificadores.
- Inicio y fin de vigencia, cupo global opcional y activacion/desactivacion.

Las fechas se editan en hora local del navegador y se transmiten en UTC.
Las promociones nuevas empiezan desactivadas: revisa y marca **Activa**
cuando corresponda. Las ediciones requieren la version actual; un conflicto
no sobrescribe cambios ajenos. No se borran reglas con historial.

Los codigos nuevos se recortan y convierten a mayusculas: 3 a 80 caracteres,
primero letra ASCII o numero, despues letras ASCII, numeros, guion o guion
bajo. La comprobacion de unicidad y la aplicacion no distinguen mayusculas,
incluidos codigos antiguos. No se puede cambiar el codigo o convertir entre
automatico/cupon despues de tener usos, incluso liberados. Crea otra regla.
No puede reducirse el cupo por debajo de los usos aun consumidos.

**Ver usos** muestra pedido, fecha, descuento y si el uso sigue consumido o
fue liberado; permite abrir el detalle administrativo. No expone datos del
comprador en ese listado.

## Reglas de calculo

1. Se conserva el precio de oferta vigente de cada producto.
2. Sobre esa mercancia se compara cada promocion automatica disponible y,
   si se digito uno, el cupon disponible.
3. Se aplica **un solo beneficio adicional**, el de mayor descuento real
   despues de alcance, minimo y tope. En empate se prefiere el cupon
   introducido; entre reglas equivalentes se desempata por identificador.
4. El envio nunca recibe descuento. Un minimo por categoria/producto cuenta
   solo la mercancia correspondiente, despues de sus ofertas, no todo el
   carrito ni el envio.
5. Los porcentajes se redondean hacia abajo al peso COP. El importe fijo o
   porcentaje nunca supera la mercancia elegible ni el tope configurado.
6. El beneficio se reparte proporcionalmente entre las lineas elegibles:
   cocientes enteros y mayores residuos, con desempate estable por producto.
   La suma de las lineas coincide exactamente con el descuento del pedido.

Ejemplo: producto regular de 1.000 COP, oferta de 800 COP y cupon del 20 %:
descuento de producto 200 COP, beneficio adicional 160 COP, mercancia neta
640 COP. Si el envio vale 5.000 COP, se cobran 5.640 COP.

En `/checkout`, digita el cupon opcional y pulsa **Recalcular y revisar resumen**.
Se muestran ofertas de productos, beneficio elegido, descuento por linea y
total neto. Cambiar cupon, carrito, entrega o pago invalida la revision y la
aceptacion de condiciones. Un cupon inexistente, inactivo, vencido, futuro,
agotado o sin alcance/minimo suficiente produce un error explicito; corrigelo
o quitalo. No se descarta silenciosamente. Si gana una automatica, se informa
y el cupon no consume cupo.

## Cupos, reintentos e historial

- Cotizar no consume ni reserva cupo.
- Crear consume un uso global del beneficio elegido dentro de la misma
  transaccion que cliente, pedido, pago, historial y auditoria.
- Conexiones concurrentes no pueden consumir dos veces el ultimo uso.
  Un fallo de guardado o commit revierte tambien el contador.
- Reintentar/recuperar el mismo pedido no consume otro uso, aunque despues se
  agote o desactive la regla. No descartes un intento perdido sin recuperarlo.
- Otras compras no cambian la version administrativa de la regla. Si aun hay
  cupo y las condiciones no cambiaron, no invalidan por si solas la cotizacion.
- Cancelar antes del despacho libera el uso una sola vez, conserva la fila
  historica y permite reutilizar el cupo.
- Un retorno de envio despachado o una devolucion posterior a la entrega
  **no libera usos**, incluso despues del reembolso completo.
- El pedido conserva nombre, codigo, descuento total y reparto originales.
  Editar o desactivar la promocion no cambia pedidos ni reembolsos historicos.

Los cupos no tienen vencimiento automatico para pedidos pendientes: si un
pedido no se completara, el personal debe cancelarlo para liberar el uso.
El stock sigue descontandose solo cuando el negocio confirma.

## Pedidos sin cobro y devoluciones

Una recogida con descuento del 100 % puede totalizar cero. Se registra sin
cobro, con pago resuelto, sin acreditar una transferencia ni exigir comprobante.
El negocio todavia debe confirmar existencias, preparar y entregar. El envio,
si lo hay, sigue siendo cobrable.

Cada linea conserva su importe antes del beneficio adicional y el descuento
asignado. Su saldo reembolsable es el importe **neto**, no el precio sin cupon.
Para cada nueva devolucion se reparte el dinero restante entre las unidades
aun disponibles, redondeando hacia abajo; las ultimas unidades reciben los
pesos restantes. Las solicitudes no rechazadas reservan unidades y dinero.
Rechazar una solicitud libera ambos sin recalcular obligaciones posteriores.
Nunca se devuelve mas de lo efectivamente pagado.

Cancelar un pedido pagado antes del despacho crea una obligacion por su
importe neto completo, incluido el envio, aunque aun no se hubiera confirmado.
Las lineas gratuitas pueden recibirse y reponerse fisicamente sin reembolso
monetario. El reembolso sigue siendo manual y exclusivo del administrador.

## API y persistencia

Todos los endpoints administrativos requieren sesion Administrator; las
escrituras requieren CSRF:

| Metodo | Ruta relativa a `/api/v1/admin/promotions` | Funcion |
| --- | --- | --- |
| GET | `/` | Lista y filtros |
| GET | `/{id}` | Regla y version |
| POST | `/` | Crear, 201 con Location |
| PUT | `/{id}` | Editar o desactivar |
| GET | `/{id}/usages?page=1` | Historial paginado |

La seleccion de cotizacion/creacion acepta `couponCode` opcional. Los importes
son calculados por el servidor: el cliente no envia descuentos autoritativos.
Los errores usan Problem Details (400 validacion, 404 inexistente,
409 conflicto de version/condiciones).

`PromotionsAndCoupons` agrega alcance y version a promociones, snapshots a
pedidos y descuentos por linea/uso. Conserva las tablas y datos existentes;
las reglas antiguas reciben nombre y version, los descuentos nuevos de pedidos
anteriores parten de cero. No se crean promociones activas de demostracion.

Consulta [checkout.md](checkout.md), [operations.md](operations.md) y
[database.md](database.md). Los ejemplos de
[la API](../src/MercadoCercano.Api/MercadoCercano.Api.http) son manuales:
no se ejecutan servidores, navegadores ni peticiones como parte de las pruebas
aisladas de esta fase.
