# Manual de usuario de Mercado Cercano

Guia de operacion para clientes, operadores de pedidos y administradores.
Corresponde a las funciones implementadas en el sistema al 27 de octubre de 2026.
Edicion ampliada: explica el significado de los campos, sus efectos, ejemplos
de uso y comprobaciones, ademas de las secuencias de botones.

Este manual explica la operacion desde el navegador. La instalacion, la
publicacion, la creacion offline del primer administrador de produccion y la
restauracion de respaldos corresponden al responsable tecnico; no se realizan
desde el portal. Consulta las referencias de la seccion 20 para esos procedimientos.

## Indice

1. [Antes de comenzar](#1-antes-de-comenzar)
2. [Perfiles y permisos](#2-perfiles-y-permisos)
3. [Ingreso, cuenta y cierre de sesion](#3-ingreso-cuenta-y-cierre-de-sesion)
4. [Preparar la tienda para vender](#4-preparar-la-tienda-para-vender)
5. [Administrar categorias](#5-administrar-categorias)
6. [Administrar productos e inventario](#6-administrar-productos-e-inventario)
7. [Administrar imagenes](#7-administrar-imagenes)
8. [Configurar entregas, pagos y condiciones](#8-configurar-entregas-pagos-y-condiciones)
9. [Comprar: catalogo, carrito y checkout](#9-comprar-catalogo-carrito-y-checkout)
10. [Consultar un pedido y adjuntar comprobantes](#10-consultar-un-pedido-y-adjuntar-comprobantes)
11. [Buscar, confirmar y cancelar pedidos](#11-buscar-confirmar-y-cancelar-pedidos)
12. [Verificar y registrar pagos](#12-verificar-y-registrar-pagos)
13. [Preparar, despachar y entregar](#13-preparar-despachar-y-entregar)
14. [Devoluciones, retornos y reembolsos](#14-devoluciones-retornos-y-reembolsos)
15. [Enviar comunicaciones manuales](#15-enviar-comunicaciones-manuales)
16. [Administrar promociones y cupones](#16-administrar-promociones-y-cupones)
17. [Consultar y exportar reportes](#17-consultar-y-exportar-reportes)
18. [Administrar usuarios](#18-administrar-usuarios)
19. [Rutinas, ejemplos y solucion de problemas](#19-rutinas-ejemplos-y-solucion-de-problemas)
20. [Limites, glosario y referencias](#20-limites-glosario-y-referencias)

## 1. Antes de comenzar

### 1.1 Acceso y elementos necesarios

Solicita al responsable del negocio:

- La direccion web de la tienda. En este manual se llama **direccion de la tienda**.
- Tu correo de acceso y clave temporal, si trabajaras como operador o administrador.
- Las politicas de precios, entregas, cancelaciones y devoluciones del negocio.
- Los canales autorizados para verificar pagos y ejecutar reembolsos.
- El contacto del administrador y del soporte tecnico.

Abre la direccion en un navegador actualizado, con conexion y almacenamiento
del sitio habilitado. Un cliente puede comprar sin crear cuenta. El personal
debe usar una cuenta individual; no debe compartirla.

Cuando el manual indica una ruta como `/login`, agregala a la direccion de la
tienda, o utiliza el enlace correspondiente de la aplicacion. No uses la
direccion de Swagger para trabajar: es una herramienta tecnica, no la tienda.

Para un entorno local ya instalado, la direccion habitual es
`http://localhost:5173`. El responsable tecnico lo inicia segun el
[README](../README.md). Esa direccion no es la direccion de produccion.

### 1.2 Reglas que todo usuario debe conocer

| Regla | Consecuencia practica |
| --- | --- |
| Los importes son pesos colombianos, COP, enteros | Escribe `15000` para quince mil pesos, sin decimales ni simbolos en campos numericos. |
| Crear un pedido no reserva ni descuenta existencias | La disponibilidad definitiva se verifica al confirmar el negocio. |
| Confirmar un pedido no confirma su pago | El personal debe conciliar el pago por separado. |
| Adjuntar una imagen no acredita un pago | Hay que verificar los fondos en el banco, billetera o caja. |
| Registrar un pago o reembolso no mueve dinero | Los movimientos de dinero se hacen fuera de la aplicacion. |
| Los pedidos conservan sus datos comerciales originales | Editar precios, promociones o instrucciones no cambia pedidos anteriores. |
| Hay informacion visible para el cliente | No escribas secretos ni datos sensibles en motivos publicos, instrucciones o guias. |
| Puede haber cambios simultaneos de otros usuarios | Ante conflicto, recarga y revisa antes de repetir una accion. |

Si una pantalla muestra **PRUEBA** o configuracion de prueba, no realices pagos
reales ni ingreses datos personales reales. El modo de prueba no es una
garantia de aislamiento de todos los datos: el inventario es el del entorno
en el que estas trabajando.

### 1.3 Guardar, recargar y salir

1. Completa los campos obligatorios y pulsa el boton de guardado de esa seccion.
2. Espera el resultado; no supongas que salir de un campo guarda el formulario.
3. Comprueba el mensaje de exito y los valores o el estado resultantes.
4. Si necesitas descartar ediciones, utiliza la recarga disponible o vuelve a
   abrir el registro. Recargar puede perder lo que aun no guardaste.
5. Si aparece una advertencia al salir, permanece para guardar o acepta salir
   solo si quieres descartar esas ediciones.

Las consultas no se actualizan de forma periodica. Para ver cambios recientes,
usa el boton de consulta o recarga de la pantalla.

### 1.4 Mapa de navegacion

| Pantalla | Ruta | Para que entrar |
| --- | --- | --- |
| Catalogo | `/` | Buscar y elegir productos. |
| Detalle de producto | `/productos/URL-del-producto` | Consultar informacion, fotos y precio. |
| Carrito | `/carrito` | Ajustar cantidades y continuar la compra. |
| Finalizar pedido | `/checkout` | Diligenciar datos, revisar y enviar. |
| Seguimiento | `/seguimiento` | Consultar con numero y codigo privado. |
| Pedido privado | `/pedido/NUMERO#CODIGO` | Abrir el enlace guardado de una compra. |
| Ingreso | `/login` | Acceder como personal del negocio. |
| Mi cuenta | `/mi-cuenta` | Cambiar tu propia contrasena. |
| Inicio del portal | `/admin` | Ver la navegacion permitida para tu rol. |
| Pedidos | `/admin/pedidos` | Buscar y abrir la operacion de pedidos. |
| Categorias | `/admin/categorias` | Crear y editar categorias. |
| Productos | `/admin/productos` | Buscar y abrir productos. |
| Nuevo producto | `/admin/productos/nuevo` | Registrar una referencia nueva. |
| Promociones y cupones | `/admin/promociones` | Administrar beneficios y consultar usos. |
| Reportes e indicadores | `/admin/reportes` | Consultar resultados y exportar CSV. |
| Usuarios | `/admin/usuarios` | Gestionar las cuentas internas. |
| Entregas y checkout | `/admin/entregas` | Definir cobertura, tarifas, pagos y condiciones. |

Los segmentos escritos en mayusculas son ejemplos, no valores que debas
escribir literalmente. Abre productos y pedidos existentes con sus enlaces
de listado; no necesitas conocer sus identificadores internos.

### 1.5 Como interpretar las instrucciones y los campos

En cada procedimiento, identifica primero **quien puede hacerlo**, **sobre que
registro trabaja** y **que resultado necesita obtener**. No basta con encontrar
un boton cuyo nombre parezca apropiado: confirmar un pedido, confirmar dinero
y confirmar una entrega son acciones diferentes.

| Elemento | Que significa al operar |
| --- | --- |
| Campo obligatorio | Debes completarlo con un valor valido antes de guardar. Un espacio no sustituye un dato. |
| Campo opcional | Puede quedar vacio cuando no aplica. Vacio no siempre equivale a cero: por ejemplo, un cupo vacio significa sin limite comercial. |
| Casilla marcada | La opcion esta seleccionada en el formulario. Solo cambia el registro cuando ejecutas el guardado correspondiente. |
| Selector | Elige una de las opciones disponibles. No escribas otra en un campo diferente para intentar crearla. |
| Buscar / consultar | Lee informacion y aplica filtros; no crea, paga ni confirma pedidos. |
| Guardar / registrar | Envia cambios. Espera el resultado y revisa que se hayan aplicado al registro correcto. |
| Deshabilitado | La accion no esta disponible en ese momento: puede faltar un requisito, permiso o estado, o haber una operacion en curso. |
| Confirmacion del navegador | Te pide autorizar una accion o un descarte. Lee el texto: aceptar no siempre significa guardar. |
| Historial | Relacion de eventos registrados del pedido; sirve para comprobar que una accion ya ocurrio antes de repetirla. |

Los ejemplos comerciales del manual son ilustrativos, no instrucciones para
cambiar precios o politicas reales. Sustituye sus valores por los aprobados
por el negocio. Cuando se advierte que algo se hace **fuera del sistema**, se
requiere una comprobacion fisica o una operacion en banco, caja u otra herramienta.

### 1.6 Direccion web, ruta, enlace e informacion privada

La direccion completa identifica el sitio y la pantalla. Por ejemplo, en
`https://tienda.ejemplo/productos/cafe-molido-250g`, el dominio identifica la
tienda y `/productos/cafe-molido-250g` es la ruta de un producto. El dominio
del ejemplo es ilustrativo; utiliza siempre el autorizado por tu negocio.

No confundas el campo **URL** de un formulario con una orden de navegar:
escribir un slug y guardarlo registra un dato. Para visitar la ficha debes
abrir el producto desde el catalogo o su enlace publico. La seccion 5.3
explica por que la URL de categoria no crea una pagina de categoria.

**Publico** significa que un visitante puede verlo sin cuenta: no coloques
secretos en descripcion comercial, instrucciones, motivos publicos o guia.
**Privado** significa restringido al personal autorizado, no permiso para
compartirlo por cualquier canal. **Codigo privado de seguimiento** es una
credencial de una compra, no una cuenta del portal ni una contrasena del empleado.

## 2. Perfiles y permisos

| Funcion | Cliente sin cuenta | Operador de pedidos | Administrador |
| --- | --- | --- | --- |
| Ver catalogo y crear compras | Si | Si | Si |
| Consultar seguimiento privado | Con numero y codigo | Con numero y codigo | Con numero y codigo |
| Adjuntar comprobante | Con seguimiento privado | Si | Si |
| Consultar pedidos y datos de contacto administrativos | No | Si | Si |
| Descargar comprobantes privados | No | Si | Si |
| Verificar pagos y operar entregas | No | Si | Si |
| Cancelar antes del despacho | No | Si | Si |
| Registrar, recibir o rechazar devoluciones | No | Si | Si |
| Registrar reembolsos | No | No | Si |
| Administrar categorias, productos e imagenes | No | No | Si |
| Configurar entregas, pagos y condiciones | No | No | Si |
| Administrar promociones y cupones | No | No | Si |
| Consultar reportes y exportar CSV | No | No | Si |
| Administrar cuentas del personal | No | No | Si |
| Cambiar la propia contrasena | No aplica | Si | Si |

El portal muestra la navegacion correspondiente al rol. Escribir directamente
una ruta restringida no concede permiso. Para una devolucion de dinero, el
operador debe coordinar con un administrador.

### 2.1 Elegir el acceso correcto para cada tarea

Un **cliente** compra y consulta su pedido. No necesita entrar a `/login` ni
solicitar una clave administrativa para pagar o seguir su compra.
Un **operador** atiende el recorrido de los pedidos y verifica pagos, pero
no decide desde el sistema nuevos precios, tarifas o permisos de empleados.
Un **administrador** configura esas reglas y tambien puede operar pedidos.

Ejemplo: si faltan unidades de un producto, el operador no debe simular un
pago o una entrega para continuar. Debe pedir al administrador que revise
el inventario o coordinar la solucion comercial con el cliente. Si ya hay
mercancia devuelta y corresponde devolver dinero, el operador registra la
recepcion y solicita al administrador el registro del reembolso.

Comprueba nombre y rol en el encabezado del portal al empezar la jornada.
Si una funcion no aparece, primero revisa el rol; no uses la cuenta de otro
empleado para eludir la restriccion. Las acciones deben ser atribuibles a la
persona que realmente las ejecuto.

## 3. Ingreso, cuenta y cierre de sesion

### 3.1 Iniciar sesion

1. Abre `/login`.
2. Escribe el correo y la contrasena asignados. Respeta las mayusculas de la clave.
3. Envia el formulario y espera la respuesta.
4. Si la clave es temporal, completa el cambio obligatorio descrito abajo.
5. Si no es temporal, se abre el portal o el destino interno autorizado que
   intentabas consultar.

No existen cuentas ni contrasenas predeterminadas. Si no tienes cuenta, pide
su creacion al administrador. No hay registro publico de empleados ni
recuperacion automatica de claves por correo.

### 3.2 Cambiar una clave temporal o la propia contrasena

1. Abre **Mi cuenta**, en `/mi-cuenta`. Con clave temporal se abre obligatoriamente.
2. Escribe la contrasena actual; si es tu primer ingreso, es la temporal recibida.
3. Escribe y confirma una nueva contrasena diferente de la actual.
4. Usa entre **8 y 32 caracteres**, con mayuscula, minuscula, numero y caracter
   especial, sin espacios. No utilices datos personales faciles de adivinar.
5. Guarda. El cambio cierra todas tus sesiones, incluida la actual.
6. Inicia sesion nuevamente con la nueva clave.

No podras operar el portal mientras quede pendiente un cambio obligatorio.
Si se pierde la respuesta del cambio, prueba iniciar sesion con la clave nueva
antes de repetirlo. Si olvidaste tu clave, pide a otro administrador una
temporal. Si no queda ningun administrador con acceso, escala al responsable
tecnico; no existe un mecanismo publico para recuperar la cuenta.

### 3.3 Cerrar sesion y proteger el acceso

Utiliza **Cerrar sesion** en la navegacion del portal y comprueba que ya no tienes acceso.
No basta con cerrar una pestaña en un equipo compartido. No guardes claves en
notas publicas, capturas, mensajes de grupo ni instrucciones de pago.

Un cambio de perfil, rol, estado o contrasena puede invalidar tu sesion. Si el
sistema vuelve al ingreso, autentica de nuevo; si persiste, consulta al
administrador. No repitas una operacion financiera sin revisar si ya quedo registrada.

### 3.4 Primer administrador: solo para el responsable autorizado

- **Desarrollo sin ningun usuario:** en el ingreso aparece **Crear primer
  administrador**. Completa nombre, correo, clave y confirmacion; guarda y
  despues inicia sesion. La opcion se cierra al existir cualquier usuario,
  incluso uno inactivo o un operador.
- **Produccion:** el formulario anterior no esta disponible. El responsable
  tecnico debe aprovisionar el primer administrador de forma offline segun
  [produccion](production.md). No intentes habilitar desarrollo en una tienda real.

### 3.5 Que significa cada dato de acceso y como comprobar el cambio

| Dato o accion | Significado y cuidado |
| --- | --- |
| Correo de ingreso | Identifica tu cuenta interna. No es el correo de un cliente ni un numero de pedido. |
| Contrasena actual | La que funciona antes del cambio; permite comprobar que tu eres quien solicita modificarla. |
| Contrasena temporal | Clave asignada por el administrador para ingresar y reemplazarla; no es la definitiva para trabajar. |
| Nueva contrasena | Clave que usaras despues. Debe cumplir la politica y ser diferente de la actual. |
| Confirmacion | Repeticion exacta de la nueva clave para detectar errores al escribir; no es una segunda contrasena. |
| Cerrar sesion | Retira tu acceso en ese navegador. No cancela pedidos ni elimina el carrito publico. |

Para un cambio obligatorio, utiliza la temporal en **actual**, y tu clave
elegida en **nueva** y **confirmacion**. No pongas la misma temporal en los
tres campos. Si el formulario no acepta la clave, revisa longitud y los
cuatro tipos de caracteres; no agregues espacios para cumplir la longitud.

**Comprobacion correcta:** el sistema te devuelve al ingreso; la nueva clave
permite entrar y ya no exige reemplazarla antes de operar. El cierre de
sesion despues del cambio es esperado, no prueba de que el guardado fallo.
No registres contrasenas en la bitacora de trabajo para documentar esa comprobacion.

Si otra persona guarda tu perfil o restablece tu clave mientras trabajas,
tu siguiente operacion puede exigir ingreso nuevamente. Antes de repetir
una accion, revisa el estado del pedido: perder acceso no demuestra que la
operacion anterior no se haya completado.

## 4. Preparar la tienda para vender

Esta secuencia corresponde al administrador. Antes de recibir compras reales:

1. Confirma con soporte que trabajas en la direccion y el entorno correctos.
2. Inicia sesion con una cuenta administrativa propia.
3. Crea y activa las categorias del negocio.
4. Registra productos con SKU, precio, costo, unidad y existencias reales.
5. Guarda los productos y despues agrega sus imagenes.
6. Verifica que tanto las categorias como los productos que publicaras esten activos.
7. Configura modalidades de entrega, cobertura, tarifas y medios de pago.
8. Publica instrucciones y condiciones reales, incluidas las de datos personales.
9. Revisa y sustituye todos los valores ficticios antes de desmarcar la configuracion de prueba.
10. Crea las cuentas del personal y confirma que puedan cambiar sus claves temporales.
11. Si usaras ofertas o cupones, configura alcance, vigencia y cupos.
12. Recorre el catalogo publico y comprueba nombres, fotos, precios y disponibilidad.
13. Coordina una verificacion integral en un entorno de desarrollo/pruebas,
    sin pagos reales. Antes de abrir produccion, confirma tambien el servicio
    de respaldos con el responsable tecnico.

No publiques los productos ficticios como si fueran mercancia real. Por
ejemplo, **Producto demostrativo - Snack** se crea inactivo y no aparece en
el catalogo hasta activarlo junto con su categoria.

### 4.1 Por que importa el orden de preparacion

La categoria organiza el producto; el producto determina que se compra y
a que precio; el inventario limita lo que puede confirmarse; el checkout
determina donde se entrega, cuanto cuesta el envio y que condiciones se
aceptan. Una cuenta administrativa valida, por si sola, no prepara esos datos.

Ejemplo: activar un cafe sin activar su categoria no lo publica. Publicarlo
con cero existencias no permite comprar unidades. Darle stock sin configurar
una entrega y un medio de pago utilizables tampoco completa el proceso de compra.

### 4.2 Lista de aceptacion antes de abrir ventas

| Revision | Evidencia que debe comprobar el responsable |
| --- | --- |
| Catalogo | El cliente encuentra el producto con categoria, unidad, foto y precio esperados. |
| Stock | La cantidad registrada corresponde a unidades realmente disponibles para vender. |
| Entregas | El municipio y la modalidad previstos aparecen y calculan la tarifa aprobada. |
| Pago | Las instrucciones corresponden al negocio y no contienen secretos de acceso. |
| Condiciones | El cliente puede leer el texto aprobado antes de aceptar. |
| Personal | Cada empleado entra con su propia cuenta, cambia su temporal y ve solo su navegacion autorizada. |
| Operacion | En un entorno autorizado se verifico confirmacion, pago, entrega y tratamiento de errores. |
| Continuidad | El responsable tecnico confirma respaldo, acceso a archivos y procedimiento de recuperacion. |

No envies un pedido real solo para comprobar una tarifa: puedes completar
datos de prueba autorizados, recalcular y revisar sin pulsar **Enviar pedido**.
Para ensayos que creen pedidos o muevan stock, utiliza un entorno de desarrollo
aprobado y no interpretes la etiqueta PRUEBA como inventario separado automaticamente.

## 5. Administrar categorias

**Acceso:** administrador, **Categorias**, `/admin/categorias`.

### 5.1 Crear una categoria

Una categoria agrupa productos para facilitar su seleccion en el catalogo y
su administracion. Por ejemplo, `Bebidas` puede contener agua, jugos y cafe.
Crear la categoria no crea esos productos: despues debes registrar cada uno
y seleccionarla en su campo **Categoria**.

**Antes de empezar:** revisa el listado para evitar grupos duplicados, como
`Bebidas` y `Bebida`, que separarian mercancia equivalente. Define el nombre
comercial, su lugar en la lista y si ya debe estar disponible publicamente.

1. Entra al portal como administrador y abre **Categorias**.
2. Si estabas editando otro registro, termina o descarta conscientemente esos
   cambios. **Nueva categoria** limpia el formulario, no guarda el anterior.
3. Pulsa **Nueva categoria**. Comprueba que el titulo diga **Crear categoria**,
   no **Editar categoria**; asi evitas modificar un grupo existente.
4. En **Nombre**, escribe el grupo que reconocera el cliente, por ejemplo
   `Cafe y bebidas`. Admite hasta 120 caracteres y puede llevar espacios y
   acentos. No escribas aqui una direccion web ni una referencia de producto.
5. Sal del campo Nombre, por ejemplo haciendo clic en **URL**. Si URL estaba
   vacia, el formulario propone un texto derivado del nombre. Revisalo con la
   explicacion de la seccion 5.3 antes de guardar.
6. En **Descripcion**, escribe opcionalmente el criterio del grupo, por
   ejemplo `Cafe, agua y bebidas listas para consumir`, hasta 2.000 caracteres.
   No reemplaza las descripciones de los productos ni crea un aviso comercial
   destacado en la pantalla publica actual.
7. En **Orden**, escribe un entero no negativo. Un valor menor coloca la
   categoria antes que otra con valor mayor. Por ejemplo: Alimentos 10,
   Bebidas 20 y Aseo 30. Puedes insertar Desayunos con 15 sin renumerar todo.
   No es un limite de productos, una prioridad de pedidos ni una cantidad de stock.
8. Revisa **Activa en el catalogo**. Marcada permite ofrecer los productos
   activos de este grupo; desmarcada los oculta del catalogo publico.
   Para preparar la categoria sin publicarla, dejala desmarcada.
9. Pulsa **Guardar categoria** una sola vez y espera **Categoria guardada**.
   El formulario se limpia al completar el guardado; eso no significa que
   se perdio el registro. Comprueba que aparece en el listado.
10. Para comprobar la asignacion, abre un producto nuevo o existente y
    verifica que la categoria esta disponible en el selector. No necesitas
    guardar un producto ficticio para hacer esa comprobacion.

**Resultado esperado:** hay una categoria con nombre, identificador legible,
orden y estado propios. Si esta activa pero aun no tiene productos publicables,
elegirla en el catalogo puede devolver cero resultados; eso no indica que el
alta haya fallado.

### 5.2 Editar, ordenar o retirar una categoria

1. Pulsa **Editar** en la categoria del listado.
2. Comprueba el nombre y la URL cargados antes de modificar. El formulario
   trabaja sobre esa categoria existente, no sobre una copia nueva.
3. Modifica sus datos u orden y pulsa **Guardar categoria**.
4. Para retirarla de la tienda, desmarca **Activa en el catalogo** y guarda.
5. Verifica el resultado en el listado y en el catalogo publico, consultando
   nuevamente para no interpretar como actual una pantalla abierta antes del cambio.

**Cancelar** limpia el formulario de edicion; **Recargar** actualiza el listado.
Para resolver un conflicto, recarga el listado y vuelve a pulsar **Editar**
sobre la categoria actual antes de aplicar tus cambios.

Desactivar una categoria oculta tambien sus productos, aunque cada producto
siga marcado como activo. No cambia esos productos a inactivos ni elimina
existencias, pedidos o historiales. Tampoco equivale a cancelar pedidos.
Un pedido pendiente puede dejar de poder confirmarse si su categoria ya no
esta activa; revisa los pedidos afectados antes de retirar una linea del negocio.
No hay borrado fisico de categorias en esta operacion.

Si vuelves a activar la categoria, sus productos individualmente activos
pueden volver a publicarse. Los que estaban inactivos por su propia casilla
siguen inactivos. Revisa el grupo completo antes de reactivarlo.

### 5.3 Que significa URL en una categoria

Aunque el formulario se llama **URL**, aqui no debes escribir una direccion
completa como `https://mitienda.com`, ni `/admin/categorias`, ni el enlace de
una fotografia. Es un **identificador legible**, tambien llamado *slug*:
un texto corto que representa la categoria, por ejemplo `cafe-y-bebidas`.

Hay tres datos distintos que no deben confundirse:

| Dato | Ejemplo | Funcion real |
| --- | --- | --- |
| Nombre | Cafe y bebidas | Nombre que la persona reconoce y selecciona. Puede contener espacios y acentos. |
| URL / slug | `cafe-y-bebidas` | Identificador legible unico que se guarda con la categoria y se muestra en su listado administrativo. |
| Identificador interno | Valor asignado automaticamente | Relaciona los productos y los filtros con esa categoria. No debes escribirlo ni inventarlo. |

**Como funciona en esta version:** el cliente abre el catalogo, elige el
nombre de la categoria y pulsa **Buscar**. El sistema filtra utilizando el
identificador interno, no construyendo una pagina con su slug. No existe
una pantalla publica independiente `/categorias/cafe-y-bebidas` en las rutas
actuales. Guardar este slug no crea esa pagina, un dominio, un codigo QR ni
una redireccion.

**Como se propone:** si URL esta vacia al salir de Nombre, se eliminan los
acentos, se pasa a minusculas y los separadores se convierten en guiones.
Por ejemplo, un nombre escrito como `Cafe y Bebidas` propone `cafe-y-bebidas`.
Si URL ya contiene texto, cambiar Nombre no lo reemplaza automaticamente;
revisa ambos campos. Puedes conservar un slug estable aunque ajustes el nombre.

**Reglas para escribirla:** de hasta 140 caracteres, letras de la `a` a la
`z`, numeros de `0` a `9` y guiones que separen grupos. Sin espacios, tildes,
ene con tilde, barras, puntos ni signos. No uses guion inicial, final ni
dos guiones consecutivos. Debe ser unica entre las categorias existentes.

| Valor | Evaluacion | Motivo |
| --- | --- | --- |
| `cafe-y-bebidas` | Valido si esta disponible | Es legible y respeta el formato. |
| `bebidas-2026` | Valido si esta disponible | Los numeros estan permitidos. |
| `Cafe y bebidas` | No valido | Contiene mayuscula y espacios. |
| `/bebidas` | No valido | No se escribe una ruta completa ni barras. |
| `bebidas--frias` | No valido | Tiene guiones consecutivos. |
| `https://mitienda.com/bebidas` | No valido | Es una direccion completa, no un slug. |

**Si la cambias despues:** sigues editando la misma categoria; no se crea una
nueva ni se reasignan o pierden productos, porque esas relaciones usan el
identificador interno. No cambia por eso la URL de cada producto. Aun asi,
mantener nombres legibles estables facilita el trabajo del personal y evita
confusiones con referencias compartidas. Si un sistema externo llegara a
usar ese slug, coordina el cambio; el portal no garantiza compatibilidad con
integraciones externas.

### 5.4 Ejercicio completo y comprobacion de categoria

Supongamos que vas a publicar cafe y jugos:

1. Crea `Cafe y bebidas`, URL `cafe-y-bebidas`, orden `20`, inicialmente inactiva.
2. Guarda y verifica la fila del listado. Aun no esperes productos publicos.
3. Crea un producto de cafe y asignalo a esa categoria. Guarda el producto
   con precio, unidad y stock correctos; agrega sus imagenes despues.
4. Cuando la mercancia y sus datos esten revisados, activa producto y categoria.
5. En el catalogo publico, selecciona `Cafe y bebidas` y pulsa **Buscar**.
6. Comprueba que el cafe aparece con su precio. Abre su ficha desde el
   resultado: ese enlace usa la URL del **producto**, no la de la categoria.
7. Si no aparece, revisa por separado categoria activa, producto activo y
   filtros de busqueda. Stock cero indica agotado; no sustituye la activacion.

Este ejercicio describe una verificacion; no requiere crear productos de
prueba en produccion. Usa mercancia real autorizada o un entorno de desarrollo.

## 6. Administrar productos e inventario

**Acceso:** administrador, **Productos**, `/admin/productos`.

### 6.1 Buscar y revisar productos

Escribe nombre o SKU, selecciona una categoria si lo deseas y pulsa **Buscar**.
Usa **Anterior** y **Siguiente** para recorrer paginas y **Editar** para abrir
el registro. Revisa el estado de publicacion, las existencias y
las alertas de stock bajo. Un producto agotado no es lo mismo que uno inactivo:
el primero tiene cero unidades; el segundo esta retirado de la oferta publica.

### 6.2 Crear un producto: campos del formulario

Abre `/admin/productos/nuevo` y completa:

| Campo | Como diligenciarlo |
| --- | --- |
| Nombre | Nombre comercial claro, hasta 180 caracteres. |
| SKU | Referencia interna unica, hasta 80 caracteres. Se normaliza a mayusculas; ejemplo `CAFE-250`. |
| URL | Identificador unico del producto, hasta 200 caracteres; minusculas ASCII, numeros y guiones. Ejemplo `cafe-molido-250g`. |
| Categoria | Selecciona la categoria correspondiente. Una categoria inactiva impide publicar el producto. |
| Descripcion | Informacion del producto para el comprador; obligatoria, hasta 10.000 caracteres. |
| Precio COP | Precio normal de venta, entero entre 1 y 9.000.000.000.000. |
| Costo COP (privado) | Costo unitario para gestion interna, entero entre 0 y 9.000.000.000.000; no es visible en el catalogo publico. |
| Unidad | Unidad de venta, hasta 40 caracteres; por ejemplo `unidad`, `bolsa` o `caja`. |
| Presentacion | Detalle opcional, hasta 300 caracteres; por ejemplo `Bolsa de 250 g`. |
| Peso en gramos | Opcional; entero positivo cuando se diligencia. No genera cotizacion automatica de transporte. |
| Existencias | Cantidad total disponible registrada, entero no negativo. |
| Umbral de stock bajo | Nivel de alerta, entero no negativo. No es una reserva de unidades. |
| Activo | Permite publicarlo si su categoria tambien esta activa. |
| Destacado | Marca comercial del producto; no sustituye la activacion ni crea stock. |

Pulsa **Guardar producto** y espera la confirmacion. Solo despues de ese primer
guardado puedes subir imagenes. Revisa el producto desde la tienda publica.

### 6.3 Editar y ajustar existencias

1. Abre el producto desde el listado.
2. Si necesitas datos actuales, pulsa **Recargar datos** antes de editar.
3. Cambia los campos necesarios.
4. Si ajustas inventario, escribe en **Existencias** el **saldo final**, no la
   cantidad que quieres sumar o restar.
5. Completa **Motivo del ajuste de inventario** si cambias existencias al editar.
6. Pulsa **Guardar producto** y verifica el nuevo saldo.

Ejemplo: hay 12 unidades y recibes 8. Registra `20`, no `8`, con un motivo como
`Ingreso de 8 unidades por compra al proveedor, documento interno 125`.
Si salen 2 por dano, cambia el saldo de 20 a 18 y explica la merma.

El ajuste genera movimiento de inventario y auditoria. No uses ajustes manuales
para repetir una salida por venta ya confirmada ni una reposicion ya realizada
por cancelacion o devolucion: duplicarias el efecto.

**Agotado:** stock cero. **Stock bajo:** stock positivo menor o igual al umbral.
El umbral no impide vender ni repone mercancia automaticamente.

### 6.4 Oferta individual y datos adicionales

Despliega **Promocion y datos adicionales**:

- **Precio promocional COP:** positivo y menor que el precio normal.
- **Inicio / Fin de promocion:** para programar, completa ambas fechas y
  coloca el fin despues del inicio. Se editan en hora local del navegador.
- Sin fechas, la oferta aplica mientras conserve un precio promocional valido.
- **Lote (privado)** y **Vencimiento:** permiten registrar datos del producto.
  No asumas bloqueo automatico de venta por vencimiento; revisa fisicamente la
  mercancia segun el procedimiento del negocio.
- **Etiquetado del proveedor (JSON privado):** dato estructurado opcional.
  Si no tienes contenido validado para ese campo, dejalo vacio; no escribas
  texto libre como si fuera una descripcion. Un ejemplo de JSON es
  `{"origen":"Colombia","registro":"referencia-interna"}`.

Guarda y verifica el precio vigente en el detalle publico. La oferta individual
puede combinarse con **un** beneficio adicional, segun la seccion 16.

### 6.5 Retirar un producto y resolver conflictos

Para retirarlo, desmarca **Activo** y guarda. No se elimina su historial.
Si aparece un conflicto de version, otro proceso pudo modificar el registro:
anota tus cambios, recarga, compara y vuelve a editar sobre la version actual.
No intentes sobrescribir a ciegas.

### 6.6 Nombre, SKU y URL del producto: tres funciones diferentes

**Nombre** presenta el producto al cliente. **SKU** identifica su referencia
comercial para busquedas y operacion interna. **URL** identifica su ficha
publica dentro de la tienda. No los uses como campos intercambiables.

Ejemplo: nombre `Cafe molido 250 g`, SKU `CAFE-250` y URL `cafe-molido-250g`.
El SKU no debe representar al proveedor completo si ese proveedor vende
varias referencias; cada producto registrado necesita su propio SKU unico.

La URL no lleva dominio ni `/productos/`: escribe solo `cafe-molido-250g`.
El sistema la usa para abrir `/productos/cafe-molido-250g`. A diferencia de
la categoria, **el producto si tiene una ficha publica por slug**. Admite
hasta 200 caracteres con el mismo formato de minusculas, numeros y guiones
separadores explicado en 5.3.

Si el campo esta vacio al salir del nombre, se propone un slug. Si ya tiene
valor, renombrar no lo reemplaza automaticamente. Esto permite corregir el
nombre comercial sin cambiar necesariamente el enlace que ya compartiste.

Cambiar la URL de un producto cambia el enlace con que se encuentra su
ficha. Los enlaces antiguos pueden dejar de resolver: no hay redireccion
automatica de slugs anteriores en esta version. Actualiza las referencias
que hayas compartido. El carrito ya guardado identifica el producto por su
identificador interno, por lo que un cambio de slug por si solo no crea otro
producto ni rompe su seleccion. Tampoco cambia las lineas de pedidos anteriores.

### 6.7 Unidad, presentacion, peso, precio y costo

Define primero **que recibe el comprador por una unidad del carrito**.
Si vendes una bolsa de cafe de 250 g, una configuracion comprensible es:
Unidad `bolsa`, Presentacion `Bolsa de 250 g`, Peso `250`, Precio `18000`.
Con cantidad 2, el cliente compra dos bolsas, no dos gramos ni 500 bolsas.

Unidad y Presentacion son textos descriptivos: no convierten kilos a gramos,
no fraccionan stock ni ajustan automaticamente el precio. Las cantidades
de compra y las existencias se manejan como enteros. Si vendes una caja de
12 unidades como una referencia propia, una existencia de 5 representa
5 cajas; no registres 60 salvo que esa referencia se venda por unidad individual.
No asumas un inventario compartido automaticamente entre caja y unidad.

**Precio** es lo que se ofrece por una unidad de venta antes de descuentos.
**Costo** es el valor unitario interno usado en estimaciones de margen.
Con precio 18.000 y costo 11.000, la diferencia simple es 7.000, pero no es
la utilidad final: faltan descuentos, devoluciones y gastos no modelados.
Escribir costo cero no significa costo desconocido: produce calculos con cero.
Usa el costo aprobado y no lo sustituyas por el precio de venta.

El peso describe esa misma unidad de venta y no cambia por si solo el envio.
Una bolsa mas pesada no genera otra tarifa si el checkout usa una tarifa
fija. Al cambiar una presentacion ya comercializada, revisa si corresponde
crear una referencia diferente para no confundir al cliente ni al personal.

### 6.8 Como decidir un ajuste de inventario

Compara el saldo del sistema con las unidades fisicas y con los pedidos ya
confirmados. El valor que escribes reemplaza el saldo actual: no es un campo
de entradas o salidas. Las ventas confirmadas ya estan descontadas y no
deben contarse otra vez como mercancia libre.

| Situacion | Stock antes | Operacion correcta | Resultado |
| --- | --- | --- | --- |
| Ingresan 8 unidades libres | 12 | Editar Existencias a 20 y explicar ingreso | Saldo 20. |
| Se detectan 2 unidades danadas | 20 | Editar a 18 y documentar merma | Saldo 18. |
| Se confirma venta de 3 | 18 | Confirmar el pedido, no editar stock manualmente | Saldo 15. |
| Se cancela esa venta confirmada | 15 | Cancelar el pedido si su estado lo permite | Se reponen 3; saldo 18. |
| Se reciben 3 devueltas, solo 2 vendibles | 15 | Recepcion de devolucion con reposicion 2 | Saldo 17. |

El motivo debe explicar la causa, no solo decir `ajuste`. Usa una referencia
interna autorizada si existe; no incluyas informacion personal innecesaria.
El formulario admite hasta 1.000 caracteres para ese motivo. Stock y umbral
admiten enteros entre 0 y 2.147.483.647; el limite tecnico no es una recomendacion comercial.

Si el umbral es 5, stock 6 es disponible sin alerta baja, de 1 a 5 es bajo
y 0 es agotado. El listado puede resaltar tambien el cero; en reportes,
**Stock bajo** se distingue de **Agotados**. Cambiar umbral no cambia existencias
ni compra mercancia. Antes de guardar un saldo, evita que otro empleado este
ajustando el mismo producto; ante conflicto, cuenta y revisa otra vez.

### 6.9 Alcance real de los datos adicionales y de las ofertas

La descripcion publica debe explicar contenido, uso y caracteristicas de la
referencia. Lote, costo y etiquetado privado no sustituyen esa informacion.
El campo de lote registra un dato de este producto; no ofrece por si solo
inventario desglosado por varios lotes ni seleccion automatica del lote mas antiguo.

**Vencimiento** permite registrar una fecha en hora local. No asumas alarma,
retiro automatico o bloqueo comercial por esa fecha: inspecciona el producto
y retira lo que no se pueda vender segun las reglas del negocio.
**JSON privado** es informacion estructurada; no calcula descuentos ni agrega
campos nuevos al formulario publico. Si no corresponde a tu proceso, dejalo vacio.

**Destacado** muestra una marca comercial en el catalogo; no significa primer
lugar garantizado en todos los ordenamientos ni aplica un descuento.
**Activo** controla publicacion junto con la categoria; no es lo mismo que destacado.

Para una oferta de 18.000 a 15.000 COP, escribe `15000` como precio promocional,
no `3000`: el campo pide **precio final de oferta**, no el ahorro. Si no
programas fechas, conservar ese valor mantiene la oferta aplicable. Poner
solo inicio o solo fin no define un intervalo completo valido.
Para retirarla, elimina el precio promocional y sus fechas, guarda y verifica
que el detalle muestre de nuevo el precio normal. No basta con borrar las
fechas si permanece un precio promocional sin periodo.

**Comprobacion del producto:** reabre el editor, revisa los valores guardados
y consulta su ficha publica. Comprueba nombre, categoria, unidad, precio
efectivo, disponibilidad y fotografia. Si solo cambiaste precio o costo,
los pedidos creados anteriormente conservan sus importes historicos.

## 7. Administrar imagenes

**Acceso:** administrador, dentro de un producto ya guardado.

### 7.1 Subir una imagen

1. Guarda primero los cambios pendientes del producto. La galeria se bloquea
   mientras el formulario tiene cambios sin guardar.
2. En **Imagenes del producto**, selecciona un archivo PNG, JPEG o WebP.
3. Comprueba que pesa como maximo **750 KiB: 768.000 bytes**.
4. Escribe un **Texto alternativo** descriptivo, hasta 300 caracteres;
   por ejemplo `Bolsa de cafe molido de 250 gramos, vista frontal`.
5. Pulsa **Subir imagen** y espera que aparezca en la galeria.
6. Repite si necesitas mas fotos, hasta **8 por producto**.

No se aceptan PDF ni SVG. Cambiar la extension de un archivo no lo convierte
a un formato admitido. Comprimelo o exportalo correctamente antes de subirlo.

### 7.2 Modificar, ordenar y eliminar

En cada imagen puedes editar el texto alternativo y el valor de **Orden**;
pulsa **Guardar imagen** para aplicar esos datos. Usa valores de orden
distintos para controlar la secuencia sin ambiguedad y compruebala en el detalle.

**Eliminar** retira esa imagen, no el producto. Si la necesitas de nuevo,
deberas subirla otra vez. Una imagen de producto inactivo o categoria inactiva
no debe quedar accesible publicamente por medio de la aplicacion.

### 7.3 Que hace cada campo de la galeria

**Archivo** es el contenido visual que se sube; **Texto alternativo** describe
ese contenido para accesibilidad y cuando no puede mostrarse la imagen;
**Orden** organiza las fotos. Escribir texto alternativo no imprime palabras
sobre la imagen ni cambia el nombre comercial del producto.

Ejemplo: foto frontal con orden 0, etiqueta posterior con 10 y empaque abierto
con 20. El catalogo utiliza la primera imagen disponible como presentacion.
Para cambiarla, modifica los ordenes de forma que la foto deseada quede primero,
guarda cada imagen modificada y consulta el catalogo. El valor de orden es
un entero no negativo, hasta 2.147.483.647; no necesitas usar valores tan grandes.

**750 KiB** es el peso del archivo, no el ancho de la fotografia en pixeles.
Una foto de alta resolucion puede necesitar compresion. Exportala a un formato
admitido y comprueba su tamano en las propiedades del archivo. No subas
capturas que incluyan cuentas bancarias, contactos o informacion de clientes.

Las acciones de imagen se guardan individualmente. Subir una foto no guarda
una descripcion pendiente del producto. **Eliminar** no tiene una papelera
en esta pantalla: conserva por fuera una copia autorizada del original si
el negocio necesita reutilizarla. Si una eliminacion informa error, recarga
y revisa si la imagen sigue asociada antes de volver a actuar.

## 8. Configurar entregas, pagos y condiciones

**Acceso:** administrador, **Entregas y checkout**, `/admin/entregas`.

### 8.1 Crear o modificar la configuracion

1. Abre la pantalla. Si no existe configuracion, pulsa **Crear configuracion inicial**.
2. Revisa el **Modo de funcionamiento**.
3. Habilita al menos una modalidad de entrega y un medio de pago.
4. Completa las secciones siguientes.
5. Pulsa **Guardar configuracion** y espera el mensaje de exito.
6. Revisa las opciones y condiciones desde un checkout sin enviarlo.

**Recargar configuracion** consulta lo guardado; no conserva necesariamente tus
ediciones pendientes. Si hay conflicto, coordina con el otro administrador y recarga.

### 8.2 Modalidades de entrega

| Modalidad | Configuracion |
| --- | --- |
| Recogida coordinada | Habilitala, indica municipio y ubicacion/instrucciones publicas. No tiene costo de envio. |
| Domicilio local | Habilitalo, escribe la tarifa fija COP y los municipios cubiertos, uno por linea. |
| Transportadora | Habilitala y configura la tarifa fija. Marca cobertura nacional en Colombia, o indica los municipios cubiertos si es limitada. |

Las listas admiten hasta 100 municipios distintos, de hasta 120 caracteres
cada uno. No repitas municipios. La comparacion de cobertura no distingue
mayusculas, pero evita variantes innecesarias de escritura.

El envio usa tarifa plana por modalidad: no calcula distancia, peso, plazo
ni cotizacion con una transportadora externa. El campo de municipios limitados
se utiliza cuando no esta marcada la cobertura nacional.

### 8.3 Medios de pago y condiciones

1. Selecciona los medios que el negocio recibira: **Transferencia bancaria**,
   **Billetera digital** y/o **Efectivo contra entrega**.
2. En **Instrucciones publicas de pago**, explica como pagar y que debe
   verificar el cliente, hasta 1.000 caracteres.
3. No incluyas contrasenas, PIN, tokens, claves bancarias ni enlaces privados.
4. En **Condiciones de compra y datos personales**, publica el texto que el
   cliente debe aceptar, hasta 5.000 caracteres.
5. Incluye la politica real de tratamiento de datos, la necesidad de confirmar
   disponibilidad y las condiciones comerciales aprobadas por el negocio.

Los pedidos guardan las instrucciones y condiciones que aceptaron al crearse.
Cambiar esta configuracion no actualiza retroactivamente esos textos.

### 8.4 Pasar de prueba a operacion real

En desarrollo pueden aparecer recogida gratuita en **Municipio de prueba**,
domicilio de 5.000 COP y transportadora de 15.000 COP. Son valores ficticios.
Antes de desmarcar **Configuracion de prueba: no aceptar pagos reales**:

- Sustituye municipio, tarifas e instrucciones por datos aprobados.
- Revisa los medios de pago y la cobertura real.
- Sustituye las condiciones y avisos ficticios.
- Comprueba el catalogo y coordina la puesta en servicio con soporte.

Fuera de desarrollo, una configuracion de prueba no permite cotizar ni crear
pedidos; tampoco se operan comercialmente pedidos historicos de prueba.

### 8.5 Cobertura, tarifa e instrucciones: que controla cada dato

**Cobertura** responde donde puede comprarse esa modalidad; **tarifa** responde
cuanto se suma al pedido; **instrucciones** explican la coordinacion. Ninguno
de esos datos asigna automaticamente un repartidor ni compra una guia externa.

En **Recogida**, el municipio y la ubicacion indican al cliente donde retirar;
no son la direccion de su casa. Usa una ubicacion comercial autorizada y un
texto claro de coordinacion, sin prometer horarios que el negocio no atendera.

En **Domicilio**, cada linea de municipios es una opcion habilitada para el
cliente. Escribe solo el nombre correspondiente por linea, no `Municipio:
5000`, ni varios separados por comas. La tarifa se diligencia aparte y es
una sola para esa modalidad: el formulario no configura un precio distinto
por cada municipio. Si necesitas tarifas por zona, no simules que existen;
la capacidad actual es una tarifa fija por modalidad.

En **Transportadora**, cobertura nacional permite escribir un municipio de
Colombia. Si la desmarcas, la lista limitada determina los municipios elegibles.
Marcar nacional no contrata servicios ni comprueba en tiempo real que una
empresa llegue a cada direccion: valida la cobertura comercial antes de habilitarla.

Si la tarifa aprobada es 5.000, escribe `5000`. Un valor `0` significa envio
sin costo para el cliente, no precio pendiente por definir. Habilitar efectivo
contra entrega lo ofrece en el checkout; revisa que el negocio pueda cobrarlo
en las modalidades de entrega que ofrece.

### 8.6 Ejemplo de configuracion y efectos de cambiarla

Un ejemplo ilustrativo es recogida sin costo, domicilio de 5.000 COP en los
municipios autorizados y transportadora de 15.000 COP. No copies esos valores
si no corresponden a tus costos y acuerdos. Configura primero las opciones,
despues instrucciones y condiciones, y finalmente guarda el conjunto.

En el checkout, prueba las modalidades habilitadas y verifica la suma del
envio antes de enviar una compra. Un municipio excluido no debe utilizarse
con otra escritura para intentar eludir la cobertura.

Un cambio guardado afecta nuevas cotizaciones y compras. Un cliente con
resumen anterior puede necesitar recalcular y aceptar las condiciones nuevas.
Un pedido ya creado conserva tarifa, metodo e instrucciones originales;
no cambia por editar este formulario. Tampoco se reasigna automaticamente
su entrega. Si el negocio no puede cumplir un pedido previo, debe coordinar
el caso y aplicar el flujo permitido, no asumir que la nueva configuracion lo corrigio.

Las condiciones son un texto aprobado por el negocio: el sistema registra
su aceptacion, pero no certifica que sean suficientes legalmente. Revisa
privacidad, disponibilidad, entrega y devoluciones con el responsable competente.

## 9. Comprar: catalogo, carrito y checkout

**Acceso:** publico. No se requiere cuenta de cliente.

### 9.1 Encontrar productos

1. Abre la pagina principal `/`.
2. Escribe nombre o SKU, selecciona una categoria si lo deseas y elige **Orden**:
   **Nombre**, **Mas recientes**, **Menor precio** o **Mayor precio**. Pulsa **Buscar**.
3. Recorre los resultados con **Anterior** y **Siguiente**. La interfaz muestra 12 productos por pagina.
4. Abre un producto para consultar descripcion, presentacion, precio,
   disponibilidad e imagenes.
5. Pulsa **Agregar al carrito**. Revisa y ajusta la cantidad deseada en el carrito,
   dentro de las existencias disponibles.

Un producto activo puede estar agotado. Si un producto no aparece, puede estar
desactivado, tener categoria inactiva o no coincidir con los filtros.
La existencia visible puede cambiar mientras completas la compra.

### 9.2 Revisar el carrito

1. Abre **Carrito**, `/carrito`.
2. Pulsa **Actualizar disponibilidad** para comprobar precios y stock recientes.
3. Edita **Cantidad** o pulsa **Eliminar** en las lineas que no compraras.
4. Corrige o elimina los productos no disponibles, desactivados o sin stock.
5. Revisa el subtotal estimado y pulsa **Continuar al checkout** cuando el carrito sea valido.

**Seguir comprando** vuelve al catalogo. **Vaciar carrito** elimina toda la
seleccion tras confirmarlo; no cancela pedidos que ya hayas creado. Si hay
un problema al leer el carrito y aparece **Vaciar carrito guardado**, esa
accion descarta la seleccion almacenada: usala solo despues de leer el aviso.

El carrito se guarda en ese navegador, no en una cuenta. Otro navegador o
dispositivo no recibe automaticamente la seleccion. Borrar los datos del
sitio puede eliminarla. Los precios se consultan de nuevo: el subtotal del
carrito no garantiza el total final ni reserva inventario.

### 9.3 Completar los datos de compra

En `/checkout`, completa:

| Dato | Requisito |
| --- | --- |
| Nombre completo | Identificacion de la persona de contacto, hasta 160 caracteres. |
| Telefono | Entre 7 y 15 digitos; admite +, espacios, parentesis y guiones. Revisa que permita contactar al destinatario. |
| Correo electronico | Opcional, hasta 254 caracteres; si se diligencia, debe ser valido. No implica envio automatico. |
| Modalidad de entrega | Una de las habilitadas por el negocio. |
| Municipio y direccion | Obligatorios para domicilio y transportadora; dentro de cobertura. Municipio hasta 120 caracteres y direccion hasta 300. |
| Barrio y referencias | Opcionales para facilitar la entrega, hasta 120 y 500 caracteres respectivamente. |
| Observaciones | Opcionales, hasta 2.000 caracteres; no incluyas claves ni informacion innecesariamente sensible. |
| Medio de pago | Uno de los habilitados. |
| Cupon | Opcional, hasta 80 caracteres, si tienes un codigo valido. |

Para domicilio o transportadora de cobertura limitada, elige el municipio
de la lista. Para transportadora nacional, escribe el municipio real. En
recogida se muestran el municipio y las instrucciones configuradas; no se
solicita una direccion de envio.

### 9.4 Recalcular, aceptar y crear el pedido

1. Pulsa **Recalcular y revisar resumen**.
2. Revisa productos, cantidades, subtotal regular, ofertas de producto,
   beneficio adicional, tarifa de entrega y total COP.
3. Lee el municipio y las instrucciones de entrega y pago cotizadas.
4. Si el cupon produce error, corrigelo o retiralo y recalcula. No se ignora
   silenciosamente un cupon invalido.
5. Lee las condiciones y marca su aceptacion.
6. Pulsa **Enviar pedido**, o **Enviar pedido de prueba** en un entorno de
   desarrollo configurado para pruebas, una sola vez y espera el resultado.
7. Comprueba el numero y guarda de inmediato el enlace privado completo de seguimiento.

Cambiar carrito, modalidad, municipio, cupon o medio de pago invalida la
revision y exige recalcular y aceptar otra vez. Si el negocio cambia precios
o condiciones antes de crear, tambien puede ser necesario revisar nuevamente.

El resultado inicial es **Pendiente de confirmacion**. No significa que la
mercancia este reservada ni que el dinero este recibido. El enlace privado no
se envia automaticamente por correo o WhatsApp.

### 9.5 Si el envio falla o no se ve la respuesta

1. Conserva abierta la pestaña; no crees inmediatamente otra compra.
2. Utiliza **Recuperar pedido** si hay un intento pendiente.
3. Si se recupera, guarda el numero y el enlace del pedido existente.
4. Si aun no se encuentra, no supongas que fallo definitivamente: el envio
   original puede seguir terminando. Revisa la conexion y vuelve a recuperar
   o reintentar el mismo intento cuando la pantalla lo permita.
5. **Descartar** es una decision explicita que puede producir una compra
   duplicada si el primer intento si llego. Verifica primero con el negocio.

El navegador conserva datos tecnicos de recuperacion de ese intento en la
sesion de la pestaña. Cerrarla puede perder la recuperacion automatica. Si
el almacenamiento falla, la aplicacion puede impedir el envio; no borres datos
del sitio ni cambies de navegador sin comprobar antes si ya hay un pedido.

Si otra pestaña cambio el carrito, la compra completada no necesariamente lo
vacia: lee el aviso y revisa su contenido antes de otra compra.

### 9.6 Como leer el total con un ejemplo

Supongamos dos bolsas con precio normal de 10.000 COP, oferta de 8.000 cada
una, un beneficio adicional valido del 10 % y envio de 5.000 COP:

| Concepto del resumen | Calculo | Importe COP |
| --- | --- | --- |
| Subtotal antes de promociones | 2 por 10.000 | 20.000 |
| Ofertas de productos | 2 por 2.000 de ahorro | Menos 4.000 |
| Mercancia despues de oferta | 20.000 menos 4.000 | 16.000 |
| Beneficio adicional | 10 % de 16.000 | Menos 1.600 |
| Mercancia neta | 16.000 menos 1.600 | 14.400 |
| Entrega | Tarifa de la modalidad elegida | Mas 5.000 |
| Total a pagar | 14.400 mas 5.000 | 19.400 |

El precio unitario mostrado antes del beneficio adicional no siempre es el
neto final de la linea. Revisa la columna de beneficio y el total neto para
evitar descontar dos veces. El cupon no reduce los 5.000 del envio.

**Recalcular** pregunta al servidor los valores vigentes; no bloquea precios,
stock ni cupo. **Aceptar condiciones** declara que leiste el texto cotizado;
no acredita un pago. **Enviar pedido** crea la compra con esos datos si aun
cumple las reglas. Guarda el numero y enlace cuando recibas la confirmacion.

### 9.7 Como diligenciar contacto, direccion y observaciones

El nombre y telefono permiten al negocio coordinar el pedido; no crean una
cuenta de cliente. Dos compras con el mismo telefono siguen siendo pedidos
separados. El correo opcional no habilita notificaciones automaticas.

Para envio, utiliza la direccion real completa en **Direccion**, el sector
en **Barrio** y una orientacion util en **Referencias**, por ejemplo una
identificacion de acceso autorizada. No uses Referencias para reemplazar el
municipio ni para pedir que se cambie el precio. **Observaciones** puede
expresar una necesidad, pero no configura una modalidad nueva, una cita
garantizada ni un acuerdo automatico; el negocio debe revisar lo solicitado.

Antes de enviar, comprueba cantidades y presentaciones: `2` bolsas de 250 g
no es una bolsa de 500 g. No hay un editor publico para cambiar libremente
las lineas de un pedido ya creado. Si te equivocaste despues, contacta al
negocio y no crees otro pedido sin aclarar que debe ocurrir con el primero.

### 9.8 Carrito guardado frente a intento de compra

El **carrito** conserva la seleccion para seguir comprando en ese navegador.
El **intento** permite reconocer un envio cuyo resultado se desconoce en la
sesion de la pestaña. Vaciar el carrito no es cancelar un pedido y borrar los
datos del navegador no deshace una compra registrada.

Si aparece `Procesando`, espera. Si la respuesta se pierde, recupera el
intento y revisa el numero obtenido. Si la recuperacion devuelve el pedido,
continua con ese numero; no vuelvas a comprarlo para obtener otra pantalla
de exito. Este mecanismo evita duplicados del mismo intento, pero no fusiona
dos compras nuevas que tu crees intencionalmente en diferentes intentos.

## 10. Consultar un pedido y adjuntar comprobantes

### 10.1 Consultar seguimiento privado

1. Abre el enlace completo guardado al crear la compra. Tiene una forma como
   `/pedido/NUMERO#CODIGO-PRIVADO`.
2. Tambien puedes entrar a `/seguimiento`, introducir **Numero de pedido** y
   **Codigo privado**, y pulsar **Consultar / actualizar**. El codigo tiene
   43 caracteres y no es tu telefono ni tu contrasena del portal.
3. Consulta productos, totales, estado del pedido, estado del pago,
   instrucciones e historial disponibles.
4. Actualiza la consulta cuando quieras ver cambios recientes; no hay
   seguimiento automatico de transportadora ni sondeo periodico.

El numero por si solo no permite consultar. El codigo es una credencial:
compartir el enlace permite consultar el pedido y, cuando corresponda,
adjuntar comprobantes. No lo publiques en redes, tickets o capturas.
Si copias el enlace, incluye la parte posterior al simbolo `#`.

La consulta publica no muestra direccion, nombre o contacto del comprador,
costos internos ni referencias privadas. Si pierdes el codigo y el intento de
la pestaña, no hay recuperacion publica por correo o telefono. Contacta al
negocio por un canal privado para coordinar la atencion; no se promete regenerar el enlace.

Despues de una consulta correcta, despliega **Mostrar enlace privado para guardar**
si necesitas volver a consultar el enlace completo y conservarlo de forma privada.

### 10.2 Adjuntar comprobante de transferencia o billetera

1. Realiza el pago por el medio externo indicado, solo si no estas en modo de prueba.
2. Abre el seguimiento privado y verifica que estas en el pedido correcto.
3. Si el pago esta pendiente o rechazado, utiliza el formulario de comprobante.
4. Escribe la referencia y elige una imagen PNG, JPEG o WebP de hasta 750 KiB
   (768.000 bytes). No se aceptan PDF ni SVG.
5. Envia y comprueba que el pago quede **En revision**.
6. Espera la verificacion del negocio. Si se rechaza, lee el motivo antes de
   adjuntar un comprobante corregido.

Hay un maximo de **5 comprobantes historicos por pago**, incluidos los
rechazados. El cliente no puede descargarlos desde seguimiento. Si agotas el
limite o hay un error que no puedes corregir, comunicate con el negocio.
Para efectivo contra entrega o pedidos **Sin cobro**, no adjuntes comprobantes.

### 10.3 Numero, codigo, enlace y guia de transporte

| Dato | Para que sirve | Que no debes hacer |
| --- | --- | --- |
| Numero de pedido | Identifica la compra ante el negocio y en la consulta. | No usarlo como si fuera el codigo secreto. |
| Codigo privado | Autoriza a consultar esa compra y a adjuntar comprobante cuando procede. | No reemplazarlo por telefono, clave del empleado o referencia bancaria. |
| Enlace privado | Reune numero y codigo para abrir el seguimiento. | No quitar la parte posterior a `#` al guardarlo. |
| Guia de transportadora | Referencia publica del envio fisico, si fue registrada. | No confundirla con confirmacion de entrega ni con credencial del pedido. |

Los ejemplos `NUMERO` y `CODIGO` no son credenciales utilizables. Utiliza los
datos exactos generados por tu compra, sin agregar espacios ni omitir caracteres.
Si hay un error de consulta, revisa ambos. Para proteger el acceso, el sistema
no distingue publicamente entre numero incorrecto y codigo incorrecto.

### 10.4 Referencia y archivo del comprobante

**Referencia** es el identificador de la operacion de pago segun el medio
utilizado; no es el numero del pedido salvo que el canal realmente use esa
referencia. Debe permitir al personal buscar y conciliar el movimiento.
No escribas el PIN, la clave de la billetera, el codigo de un solo uso ni
credenciales del banco. **Archivo** es la imagen del soporte, no la autorizacion
para debitar tu cuenta: el pago se realiza por fuera.

Antes de subir, comprueba que la imagen corresponde a ese pago, es legible
y tiene solo la informacion necesaria para verificarlo. Una foto aceptada
por formato puede ser rechazada comercialmente si no demuestra fondos
correspondientes. Cada nuevo envio cuenta dentro del limite historico;
no uses cargas repetidas como metodo de refrescar la pantalla.

**Resultado esperado:** tras subir, En revision; tras validacion del negocio,
Pagado o Rechazado con su motivo. Subir de nuevo no permite sustituir un pago
ya aprobado. Que el cliente no pueda descargar los comprobantes desde
seguimiento no impide que el personal autorizado los consulte para conciliar.

## 11. Buscar, confirmar y cancelar pedidos

**Acceso:** operador o administrador, **Pedidos**, `/admin/pedidos`.

### 11.1 Encontrar un pedido y entender su detalle

1. Completa **Buscar por numero, cliente o telefono** y los filtros necesarios.
2. Puedes filtrar por estado del pedido, pago, modalidad de entrega,
   recepciones pendientes y reembolsos pendientes.
3. Pulsa **Buscar / actualizar**, recorre la paginacion y pulsa **Abrir pedido**.
4. Revisa numero, marca de prueba, cliente, direccion cuando corresponda,
   lineas, cantidades, total, pago e historial antes de actuar.

Los filtros de recepciones y reembolsos pendientes pueden combinarse. Una
consulta vacia no demuestra que no exista el pedido: limpia los filtros.
El detalle separa pago, preparacion/entrega, devoluciones y mensajes manuales.
**Recargar pedido** vuelve a consultar su estado; si hay borradores pendientes,
te pide confirmar su descarte. No recargues durante una operacion en curso.

### 11.2 Confirmar disponibilidad

1. Abre un pedido **Pendiente de confirmacion**.
2. Verifica la mercancia y la capacidad del negocio para atenderlo.
3. En **Acciones**, pulsa **Confirmar y descontar inventario** y acepta la confirmacion.
4. Comprueba que quede **Confirmado** y revisa su historial.

En ese momento se descuentan todas las lineas del inventario. Si falta stock
o algun producto/categoria ya no esta activo, no se descuenta parcialmente:
la confirmacion completa debe poder realizarse. Varios pedidos pendientes
pueden pedir las mismas unidades. Resuelve faltantes con el administrador y
el cliente; no marques un pago como recibido para intentar resolver stock.

Confirmar no cambia el precio original ni aprueba pagos. Repetir una
confirmacion ya realizada no debe descontar existencias nuevamente.

### 11.3 Cancelar antes del despacho

Puedes cancelar desde **Pendiente**, **Confirmado**, **En preparacion** o
**Listo**, pero no directamente desde **Despachado** o **Entregado**.

1. Revisa si el pago esta **En revision**. Primero apruebalo o rechazalo
   de acuerdo con los fondos realmente recibidos.
2. Comprueba que el estado aun permite cancelar.
3. Escribe un motivo claro y respetuoso, hasta 1.000 caracteres: **lo vera el cliente**.
4. Pulsa **Cancelar pedido** o **Cancelar pedido y liberar inventario**,
   segun el estado mostrado, confirma y verifica estado e historial.
5. Si el pedido estaba pagado, revisa la obligacion de reembolso y coordina
   su atencion con un administrador.

Un pendiente no repone stock porque no lo habia descontado. Un pedido ya
confirmado repone una sola vez las unidades descontadas. Si estaba pagado,
se debe reembolsar el total neto, incluido el envio; cancelar **no devuelve dinero**.
La obligacion queda lista para registrar el reembolso, sin recibir mercancia otra vez.

### 11.4 Como funcionan los filtros de pedidos

Los filtros acotan simultaneamente el listado. Buscar un telefono con estado
**Confirmado** no devuelve todos los pedidos de esa persona: solo los que
tambien cumplen ese estado. Un resultado cero puede deberse al filtro, no a
que el pedido se haya perdido. Para una busqueda amplia, deja estados en
**Todos**, entrega en **Todas** y desmarca pendientes especiales.

| Filtro | Pregunta que responde | Ejemplo de uso |
| --- | --- | --- |
| Numero, cliente o telefono | Que compra o persona quieres localizar | Encontrar el numero que te comunica un cliente. |
| Estado | En que etapa fisica/comercial esta el pedido | Revisar los pendientes antes de prometer disponibilidad. |
| Pago | Que situacion tiene el dinero registrado | Revisar comprobantes En revision. |
| Entrega | Como debe recibir la mercancia | Organizar recogidas frente a domicilios. |
| Recepciones pendientes | Que pedidos tienen solicitudes aun por recibir | Organizar el retorno fisico de productos. |
| Reembolsos pendientes | Que pedidos tienen obligaciones recibidas aun por reembolsar | Preparar la gestion del administrador. |

Si marcas ambas casillas especiales, buscas pedidos que cumplen **ambas**
condiciones, no la union de dos listados. Puede haber una solicitud por
recibir y otra ya recibida por reembolsar en el mismo pedido. Pulsa **Buscar /
actualizar** despues de cambiar filtros y usa las paginas para revisar todo
el resultado. No supongas que la primera pagina contiene todas las tareas.

### 11.5 Que conservar y revisar antes de confirmar

La confirmacion compromete el stock registrado para atender las cantidades
originales. Antes de ejecutarla, revisa referencia, presentacion, cantidad,
municipio y capacidad de entrega. No es un editor para sustituir productos,
cambiar cantidades, actualizar precios o asignar otro metodo de pago.

Ejemplo: quedan 4 unidades y hay dos pedidos pendientes de 3 cada uno.
Ambos pudieron crearse porque crear no reserva. Al confirmar el primero
quedara 1; el segundo no puede confirmarse completo. El sistema no separa
automaticamente 1 unidad ni cambia la compra a una cantidad menor. Coordina
la solucion y utiliza solo los flujos que el sistema permite.

Un cambio de precio posterior no obliga a modificar el pedido: este conserva
sus importes originales. Un cambio de activacion o stock si puede impedir
la confirmacion. No crees existencias ficticias para forzarla.

### 11.6 Que efecto tiene cancelar y que comprobar despues

| Situacion antes de cancelar | Inventario | Dinero | Verificacion posterior |
| --- | --- | --- | --- |
| Pendiente, sin pago | No repone; no habia salida | No crea dinero a devolver | Estado Cancelado e historial con motivo. |
| Confirmado, sin pago | Repone unidades descontadas una vez | No registra cobro ni reembolso ficticio | Stock conciliado y pedido cancelado. |
| Pendiente o confirmado, ya pagado | Repone solo si habia salida | Genera obligacion del total neto con envio | Obligacion pendiente para el administrador. |
| En revision | Cancelacion bloqueada hasta resolver la revision | Primero comprobar fondos | No rechazar falsamente un pago real solo para habilitar cancelar. |
| Despachado o entregado | No admite esta cancelacion directa | Seguir retorno o devolucion | No modificar stock manualmente para imitar una cancelacion. |

El motivo es una explicacion al cliente, por ejemplo `No podemos atender el
pedido en las condiciones acordadas; coordinamos la cancelacion`. No pegues
conversaciones privadas ni datos financieros en el motivo publico.

Despues de cancelar, no vuelvas a confirmar el mismo pedido para reabrirlo:
el flujo no ofrece reapertura libre. Si se acuerda una nueva compra, identifica
claramente que es otra operacion y resuelve primero obligaciones de la anterior.

## 12. Verificar y registrar pagos

**Acceso:** operador o administrador, detalle del pedido, **Conciliacion manual del pago**.

### 12.1 Entender el estado del pago

| Estado o aviso | Significado y siguiente accion |
| --- | --- |
| Pendiente | Aun no hay pago acreditado; esperar comprobante o cobro contra entrega. |
| En revision | Hay comprobante por verificar; validar fondos fuera del sistema. |
| Rechazado | Se registro un motivo; puede adjuntarse otro comprobante dentro del limite. |
| Pagado | Se registro el importe completo como recibido. Puede tener reembolsos parciales. |
| Reembolsado | Se devolvio y registro todo el importe pagado. No implica una nueva reposicion fisica. |
| Sin cobro | Total cero: pago resuelto sin recibir fondos, pero el pedido debe recorrer el flujo fisico. |

### 12.2 Transferencia o billetera

1. Abre el pedido y comprueba total, medio de pago y referencia privada.
2. Usa **Descargar comprobante** si necesitas revisar la imagen. Guardala
   solo en equipos y ubicaciones autorizadas.
3. Verifica por el canal bancario o de billetera que los fondos realmente
   llegaron y corresponden a ese pedido. Una captura no es prueba suficiente.
4. Si corresponde aprobar, comprueba el **Importe efectivamente recibido COP**:
   debe ser exactamente el total original, no un abono parcial.
5. Completa la referencia bancaria y marca la verificacion de fondos e importe.
6. Pulsa **Registrar pago recibido** y confirma la accion.
7. Verifica el estado **Pagado**.

Para rechazar, escribe el **Motivo de rechazo visible para el cliente** y pulsa
**Rechazar comprobante**. No incluyas datos bancarios sensibles en ese motivo.
Si el personal adjunta el comprobante por el cliente, usa el formulario del
detalle y sigue las mismas restricciones de formato, referencia y cantidad.

No se admiten pagos parciales ni cuotas. Un pago ya aprobado no se sustituye
por otro comprobante. Si recibes un monto incorrecto, resuelvelo por el
procedimiento del negocio antes de registrar un total que no has recibido.

### 12.3 Efectivo contra entrega

1. Confirma y prepara el pedido hasta **Listo**, o despachalo si corresponde.
2. Cobra realmente el total por el canal de caja autorizado.
3. En el panel de pago, escribe el importe completo y, si corresponde, la referencia del recibo.
4. Marca que verificaste los fondos y el total.
5. Pulsa **Registrar pago recibido** y comprueba el resultado.
6. Solo despues registra la entrega fisica.

El formulario de cobro se habilita en **Listo** o **Despachado**, no al crear el
pedido. No cargues una imagen como comprobante de efectivo. No registres
cobros anticipadamente solo para habilitar el boton de entrega.

### 12.4 Total cero

Un descuento puede dejar una recogida en cero. **Sin cobro** no significa
transferencia recibida: no cargues comprobantes ni registres dinero ficticio.
Confirma existencias, prepara y entrega de forma normal. El envio no recibe
descuento promocional y, si tiene costo, sigue siendo cobrable.

### 12.5 Que significa conciliar y como elegir una referencia

Conciliar es comparar el pedido con un movimiento real de dinero. El sistema
no accede al saldo bancario y no verifica automaticamente fecha, titular,
valor ni autenticidad de una captura. La casilla **Verifique los fondos y el
importe total** declara una comprobacion hecha por ti: no ejecuta esa comprobacion.

Para un pedido de 35.000 COP, busca el movimiento correspondiente en el canal
autorizado y comprueba el importe completo. Una transferencia anunciada,
pendiente en el banco o perteneciente a otra compra no equivale a fondos
recibidos para este pedido. Evita acreditar el mismo movimiento externo a
dos pedidos diferentes por tener importes iguales.

La **Referencia bancaria / recibo** permite rastrear esa verificacion. Para
transferencia o billetera debe completarse; en efectivo el formulario admite
una referencia cuando corresponda al recibo del negocio. Admite hasta
160 caracteres. Es privada: no la copies como motivo publico o guia de envio.

### 12.6 Diferencias entre aprobar, rechazar y devolver dinero

**Aprobar** registra que ya recibiste el total. **Rechazar comprobante** registra
que ese soporte no permite acreditar el pago y comunica el motivo. Ninguna
accion ejecuta una transferencia. **Reembolsar** es un flujo posterior para
dinero realmente recibido y devuelto, descrito en el capitulo 14.

Si recibiste 30.000 y el pedido exige 35.000, no escribas 35.000 para avanzar.
El sistema no administra un saldo de abonos parciales; coordina la diferencia
con el responsable del negocio y completa el registro solo cuando cumpla
las reglas. Si el problema es una imagen ilegible, explica ese motivo para
que el cliente pueda corregirlo, sin afirmar hechos bancarios no comprobados.

**Comprobacion de un cobro:** el estado pasa a Pagado, el importe y la
referencia quedan asociados al pedido y el historial refleja la accion.
Si una perdida de conexion ocurre despues del clic, vuelve a consultar antes
de repetir. Nunca vuelvas a cobrar al cliente solo porque no viste el mensaje
de exito. Tampoco efectues un reembolso externo dos veces por no ver aun el registro.

## 13. Preparar, despachar y entregar

**Acceso:** operador o administrador, **Preparacion y entrega** del pedido.

### 13.1 Secuencia y requisitos

| Estado actual | Accion de la pantalla | Resultado / condicion |
| --- | --- | --- |
| Pendiente de confirmacion | Confirmar pedido | Confirmado; descuenta stock. |
| Confirmado | Iniciar preparacion | En preparacion; transferencia/billetera deben estar pagadas. Efectivo puede seguir pendiente. |
| En preparacion | Marcar listo para entrega | Listo. |
| Listo, domicilio o transportadora | Registrar despacho / salida a ruta | Despachado. |
| Listo, recogida coordinada | Confirmar entrega | Entregado, con pago resuelto y recepcion real. |
| Despachado | Confirmar entrega | Entregado, con pago resuelto y recepcion real. |

Los botones dependen del estado y el medio de entrega. No hay un selector
libre para saltar etapas ni para deshacer una entrega ya registrada.

### 13.2 Preparar y alistar

1. Abre el pedido confirmado.
2. Para transferencia o billetera, resuelve primero su pago. Para efectivo
   puedes preparar antes del cobro.
3. Pulsa **Iniciar preparacion** y confirma.
4. Reune productos y cantidades de las lineas originales, revisa su estado y empaca.
5. Cuando este realmente terminado, pulsa **Marcar listo para entrega** y confirma.

### 13.3 Despachar domicilio o transportadora

1. Comprueba que el pedido esta **Listo** y revisa la direccion administrativa.
2. Completa **Transportadora / responsable** y **Numero de guia / referencia de ruta**.
3. Para transportadora, ambos son obligatorios; hasta 120 caracteres cada uno.
4. No escribas datos personales ni secretos: esos campos se ven en seguimiento.
5. Cuando salga fisicamente, pulsa **Registrar despacho / salida a ruta** y confirma.
6. Comprueba que quede **Despachado** y que los datos sean correctos.

La recogida coordinada no se despacha: pasa de listo a entregado. La
aplicacion no consulta al transportador ni marca entregas automaticamente.

### 13.4 Confirmar entrega

1. Verifica que el pedido este listo para recogida o despachado para envio.
2. Comprueba que el pago este resuelto. Si es efectivo, registra primero el cobro real.
3. Confirma por el procedimiento del negocio que el cliente recibio la mercancia.
4. Marca **Confirmo que el cliente recibio el pedido**.
5. Pulsa **Confirmar entrega** y acepta la confirmacion.
6. Comprueba el estado **Entregado** y el historial.

No marques entrega al despachar ni solo por tener una guia. Si un envio falla
antes de llegar al cliente, utiliza retorno de despacho fallido, no una entrega ficticia.

### 13.5 Diferencia entre el estado del pedido y el estado del pago

El pedido tiene una etapa de operacion y el pago tiene su propia situacion.
No esperes que ambos digan lo mismo ni uses uno para corregir el otro.

| Combinacion | Como interpretarla | Siguiente comprobacion |
| --- | --- | --- |
| Pendiente de confirmacion + Pagado | Hay dinero registrado, pero aun no stock confirmado | Verificar disponibilidad; si se cancela, atender reembolso. |
| Confirmado + En revision | Stock descontado, soporte aun por conciliar | Verificar fondos antes de preparar transferencia/billetera. |
| En preparacion + Pendiente, en efectivo | Se prepara antes de cobrar | Registrar cobro real en listo o despachado antes de entregar. |
| Despachado + Pagado | El envio salio y el dinero esta resuelto | Comprobar recepcion real antes de entregar. |
| Entregado + Pagado con reembolso parcial | Se entrego y despues se devolvio parte del dinero | Revisar solicitudes y acumulado, sin reabrir la entrega. |
| Cancelado + Pagado | Puede quedar una obligacion de reembolso | Administrador debe revisar la obligacion, no cobrar nuevamente. |

**Confirmado** no significa empacado. **En preparacion** significa que el
personal inicio el armado. **Listo** significa terminado y disponible para
retirar o despachar. **Despachado** significa que salio, no que llego.
**Entregado** afirma recepcion del cliente. Cambia cada etapa cuando ocurra
el hecho, porque su historial es visible y los reportes usan los eventos.

### 13.6 Que escribir en responsable y guia, y como cerrar el recorrido

Para transportadora, registra el nombre comercial de la empresa y la guia
que permita identificar ese envio en el canal externo autorizado. Para
domicilio, puedes registrar una referencia de ruta o responsable segun el
procedimiento del negocio; evita nombres y telefonos personales si no deben
publicarse. Son campos publicos de seguimiento, no notas internas.

El sistema guarda esos datos y el evento; no genera una guia, no agenda una
recogida con la empresa y no abre automaticamente un rastreo externo.
Para datos incorrectos ya registrados, coordina con el responsable y soporte:
no se ofrece un editor libre de todas las etapas para borrar lo ocurrido.

Antes de confirmar entrega, verifica numero, cantidades y evidencia operativa
de recepcion segun la politica del negocio. La casilla de confirmacion no
captura una firma ni reemplaza ese control. Si el cliente aun no recibio,
no marques la casilla para limpiar el listado de pendientes.

**Comprobacion final:** Entregado, historial coherente y pago resuelto, o
flujo de retorno/cancelacion debidamente cerrado. Si llega una devolucion
posterior a la entrega, usa el capitulo 14 sin deshacer la entrega historica.

## 14. Devoluciones, retornos y reembolsos

**Acceso:** operador o administrador para solicitudes y recepciones;
**solo administrador** para registrar reembolsos.

Son tres acciones distintas: **solicitar** reserva cantidades y saldos;
**recibir** registra la mercancia y repone lo vendible; **reembolsar** registra
dinero ya devuelto fuera del sistema. Ninguna sustituye a las otras.

### 14.1 Solicitar devolucion de un pedido entregado

1. Abre el pedido **Entregado** y la seccion **Devoluciones y reembolsos**.
2. Despliega **Solicitar devolucion parcial / retorno de despacho fallido**.
3. Revisa las unidades disponibles para devolver y el saldo neto de cada linea.
4. Escribe las cantidades que se devolveran; deja cero en las demas.
5. Si se acordo devolver envio, registra el importe, sin superar el saldo
   cobrado aun disponible. Si no se devolvera envio, usa cero.
6. Escribe el motivo visible para el cliente, hasta 1.000 caracteres.
7. Revisa la estimacion y pulsa **Registrar solicitud (sin reponer ni devolver dinero)**.
8. Comprueba la solicitud creada y su importe definitivo.

El servidor usa importes historicos netos, despues de ofertas y cupones. No
usa el precio actual del catalogo. Las solicitudes no rechazadas reservan
unidades y dinero, por lo que reducen lo disponible para nuevas solicitudes.
Nunca debe devolverse mas de lo comprado ni mas dinero del efectivamente pagado.

### 14.2 Recuperar una solicitud con respuesta perdida

Si aparece un intento pendiente, pulsa **Recuperar solicitud** antes de crear
otra. Conserva la pestaña y no cambies el contenido del intento para forzar el
envio. **Descartar intento explicitamente** puede producir duplicados si el
primer registro si se completo; verifica antes el listado de solicitudes.

### 14.3 Retorno de un despacho fallido

Si el pedido esta **Despachado** y no fue recibido por el cliente:

1. Abre el mismo formulario de solicitud.
2. Incluye **todas las lineas y cantidades del pedido**, no una devolucion parcial.
3. Registra el motivo del retorno y el envio reembolsable segun el acuerdo
   permitido; si no hubo cobro, usa cero.
4. Crea la solicitud y espera el retorno fisico.
5. Recibe toda la mercancia con el procedimiento siguiente.

La recepcion del retorno cierra el pedido como cancelado. Si habia un pago,
revisa el importe de reembolso calculado. En efectivo aun no cobrado no hay
dinero que reembolsar. No uses la cancelacion directa de un pedido despachado.

### 14.4 Recibir mercancia devuelta

1. Abre la solicitud pendiente y pulsa **Registrar recepcion fisica**.
2. Comprueba que recibiste **todas las unidades de esa solicitud**.
3. Inspecciona la mercancia.
4. En cada linea, escribe las unidades que llegaron **en buen estado y vendibles**.
   El valor inicial es cero; no lo confundas con la cantidad solicitada.
5. No incluyas unidades danadas en la cantidad a reponer.
6. Marca que recibiste todas las unidades y comprobaste su estado.
7. Revisa cuidadosamente y pulsa **Guardar recepcion y reponer unidades indicadas**.
8. Verifica el estado recibido, las cantidades repuestas y, si corresponde,
   la obligacion pendiente de reembolso.

Ejemplo: recibes 3 unidades, de las cuales 2 estan vendibles y 1 danada.
Registra `2` para reponer, aunque hayas recibido las 3.

La recepcion no devuelve dinero y no permite corregir despues las cantidades
recibidas mediante ese mismo formulario. Si solo llegaron algunas unidades,
no declares que recibiste toda la solicitud. Coordina primero la recepcion
completa o la resolucion del caso con el responsable del negocio.

### 14.5 Rechazar una solicitud antes de recibir

1. En una solicitud pendiente, pulsa **Rechazar solicitud**.
2. Escribe un motivo publico, claro y respetuoso.
3. Pulsa **Rechazar solicitud sin movimiento de inventario**.
4. Comprueba el estado y el motivo registrado.

El rechazo libera las unidades y los saldos reservados por esa solicitud.
No mueve inventario ni devuelve dinero. No se utiliza para revertir una
recepcion o un reembolso ya registrados.

### 14.6 Registrar un reembolso: administrador

1. Abre una solicitud **Recibida** con importe pendiente, o la obligacion
   generada por cancelar un pedido pagado antes del despacho.
2. Revisa el importe exacto calculado; no lo sustituyas por el precio de lista.
3. Realiza primero la devolucion de fondos por el medio externo acordado.
4. Verifica que la operacion externa fue exitosa y conserva su referencia.
5. Pulsa **Registrar reembolso ya realizado**.
6. Escribe exactamente el importe de esa solicitud y la referencia privada,
   hasta 160 caracteres.
7. Marca **Ya devolvi este importe fuera del sistema y verifique el resultado**.
8. Pulsa **Registrar reembolso** y verifica el estado e historial.

Cada solicitud se reembolsa completa; no admite abonos parciales dentro de
esa solicitud. Varias solicitudes pueden devolver parcialmente un pedido.
Un reembolso parcial mantiene el pago **Pagado**, con su acumulado de
reembolsos; al devolver todo pasa a **Reembolsado**.

Reembolsar no repone inventario. La cancelacion o la recepcion ya realizan el
movimiento que corresponda. Si el importe a reembolsar es cero, no registres
dinero ficticio: puede haber devolucion fisica sin devolucion monetaria.

### 14.7 Campos, saldos y estados de una solicitud

| Campo o dato | Significado operativo |
| --- | --- |
| Disponibles para devolver | Unidades de la compra no comprometidas por otras solicitudes no rechazadas; no es stock del catalogo. |
| Cantidad | Unidades que incluira esta solicitud. Usa cero para no incluir una linea. |
| Saldo neto por devolver | Dinero historico aun reembolsable de la linea despues de descuentos y reservas previas. |
| Reembolso de envio | Parte del transporte que se acordo devolver, limitada por lo cobrado y aun disponible. |
| Motivo | Explicacion publica de la solicitud, no referencia privada de un banco. |
| Unidades en buen estado | Parte de lo recibido que puede volver a venderse; no necesariamente todo lo devuelto. |
| Importe reembolsado | Total exacto de la obligacion que se registra como ya devuelto; no un valor libre negociado en este campo. |
| Referencia privada | Identificador del movimiento externo de devolucion de dinero para conciliacion. |

**Solicitada** reserva unidades e importes, pero todavia no registra recepcion
ni reposicion. **Recibida** significa que se registro toda la recepcion y lo
vendible se repuso; puede seguir pendiente el dinero. **Rechazada** libera la
reserva sin reponer. **Reembolsada** registra el pago de esa obligacion.
Una recibida de importe cero no necesita un reembolso ficticio para considerarla
sin dinero pendiente.

No confundas disponibilidad para devolver con disponibilidad para vender.
Que una unidad este reservada en una solicitud no agrega stock al catalogo;
solo se agrega la cantidad vendible al recibirla. Si la cancelacion previa
al despacho ya repuso, su obligacion no debe recibirse nuevamente.

### 14.8 Ejemplo de devolucion con descuentos y unidades danadas

Un pedido entregado tenia dos unidades de 10.000 COP, ambas en oferta de
8.000, y un beneficio adicional de 10 %. La mercancia neta pagada es 14.400,
equivalente a 7.200 por unidad en este ejemplo sin diferencias de redondeo.

Si se acuerda devolver una unidad y ningun envio:

1. Solicita 1 unidad con envio `0`; la estimacion es 7.200, no 10.000 ni 8.000.
2. Recibe toda esa solicitud cuando llegue la unidad.
3. Si esta vendible, registra reposicion `1`; si esta danada, registra `0`.
4. El importe de reembolso de la solicitud no se cambia escribiendo otra
   cantidad vendible: reposicion fisica y obligacion monetaria son diferentes.
5. El administrador devuelve externamente 7.200 y registra ese importe exacto.
6. La compra conserva su entrega historica y su pago muestra reembolso parcial.

En otros casos, el reparto de descuentos puede producir diferencias de un
peso entre unidades. El sistema calcula el saldo definitivo: no multipliques
un precio redondeado y fuerces el resultado si no coincide con la solicitud.
Las ultimas unidades pueden absorber pesos restantes del reparto.

### 14.9 Como decidir entre cancelar, retornar o devolver

| Hecho real | Procedimiento | No hacer |
| --- | --- | --- |
| El pedido no salio y no se atendera | Cancelar en estado permitido, resolviendo primero pago en revision | Crear devolucion ficticia de algo que no se entrego. |
| El pedido salio pero no llego al cliente | Retorno de despacho fallido por todo el pedido y recepcion fisica | Marcar Entregado para habilitar devolucion parcial. |
| El cliente recibio y devuelve parte o todo | Solicitud de devolucion posventa con cantidades disponibles | Cancelar directamente una entrega historica. |
| Solo se perdio la respuesta de la solicitud | Recuperar solicitud y verificar historial | Descartar y repetir como nuevo sin comprobar. |
| Se recibio una solicitud y falta dinero | Administrador gestiona y registra reembolso externo | Reponer otra vez el inventario al devolver el dinero. |

Las condiciones comerciales que autorizan una devolucion deben venir del
negocio. El formulario controla cantidades, saldos y etapas; no decide por
si solo si una reclamacion es legalmente procedente. Si hay controversia o
recepcion incompleta, coordina antes de declarar hechos que no ocurrieron.

### 14.10 Comprobacion final de una devolucion

Revisa el estado de cada solicitud, las cantidades pedidas y repuestas,
el envio incluido, el importe y la referencia privada si hubo reembolso.
Comprueba los acumulados de dinero del pedido y el saldo fisico una sola vez.
Varias solicitudes pueden estar en etapas distintas; terminar una no cierra
automaticamente las demas. Usa filtros de recepciones y reembolsos pendientes
para comprobar que no queda trabajo sin atender.

## 15. Enviar comunicaciones manuales

**Acceso:** operador o administrador, **Mensaje para envio manual** del pedido.

1. Pulsa **Preparar con el estado actual** para generar un borrador.
2. Revisa destinatario, numero de pedido, estado y guia. Si reemplazas un
   borrador editado, confirma solo si quieres perder ese texto.
3. Edita el contenido si hace falta; maximo 2.000 caracteres.
4. Usa **Copiar texto**, **Abrir WhatsApp (externo)** o **Abrir correo**.
5. En la aplicacion externa, verifica de nuevo destinatario y texto y realiza
   el envio manualmente.

Para telefonos colombianos de 10 digitos se agrega el indicativo 57. Un numero
no valido puede impedir el enlace de WhatsApp. La opcion de correo requiere
que el pedido tenga correo y que el equipo pueda abrir una aplicacion de correo.

Preparar, copiar o abrir WhatsApp **no significa que se envio el mensaje**.
No hay notificaciones automaticas ni un registro que certifique el envio.
El borrador no incluye el codigo privado de seguimiento: el servidor no lo
conserva en texto recuperable. Los pedidos de prueba incluyen advertencia de prueba.

### 15.1 Que prepara el sistema y que debe hacer la persona

El borrador toma el estado actual y la guia cuando existe. No conoce por si
solo todos los acuerdos externos: revisa si el mensaje comunica lo que
realmente corresponde. Si cambias de etapa, vuelve a preparar el texto
cuando necesites actualizarlo; un borrador anterior puede seguir diciendo
que el pedido esta en preparacion aunque ya este listo.

**Copiar texto** lo lleva al portapapeles de tu equipo. **Abrir WhatsApp**
prepara una conversacion externa con texto, pero debes revisar y enviarlo.
**Abrir correo** prepara destinatario, asunto y cuerpo en la aplicacion
asociada. Si el equipo no tiene una aplicacion configurada, esa accion puede
no completar nada. Ninguna de las tres confirma lectura ni entrega del mensaje.

Ejemplo: despues de alistar una recogida, prepara el borrador, comprueba el
numero y agrega una indicacion de coordinacion aprobada. Abre el canal y
revisa el contacto antes de enviar. No pegues claves, comprobantes privados
o el enlace de otro cliente. No prometas que se envio el seguimiento privado
automaticamente: el sistema no reconstruye su codigo secreto.

Si el negocio requiere evidencia de comunicaciones, sigue su registro
autorizado externo. No cambies el pedido a Entregado para marcar que se envio
un aviso. Al cerrar el formulario, el borrador no enviado no debe confundirse
con una notificacion registrada permanentemente.

## 16. Administrar promociones y cupones

**Acceso:** administrador, **Promociones y cupones**, `/admin/promociones`.

### 16.1 Crear una regla

1. Pulsa **Nueva promocion** para abrir un formulario limpio.
2. Escribe el nombre publico de la campana, hasta 160 caracteres.
3. En **Aplicacion**, elige **Automatica**, o **Cupon** que el cliente debe introducir.
4. Si es cupon, escribe un codigo unico. Se convierte a mayusculas; admite
   de 3 a 80 caracteres, comienza con letra ASCII o numero y continua con
   letras ASCII, numeros, guion o guion bajo. Ejemplo `BIENVENIDA10`.
5. En **Tipo**, elige **Porcentaje entero** o **Monto fijo COP**, y completa
   **Valor**: de 1 a 100 para porcentaje, o entero COP positivo para monto fijo.
6. Define **Compra minima elegible COP**, que puede ser cero, y un
   **Tope del descuento COP** positivo si lo necesitas. Tope vacio significa
   sin tope adicional; importes COP admiten hasta 9.000.000.000.000.
7. Elige alcance: toda la mercancia, una categoria o un producto. Para un
   producto utiliza el buscador y sus paginas, sin copiar identificadores.
8. Completa **Inicio** y **Fin exclusivo** en hora local del navegador, con
   fin posterior al inicio. Al alcanzar la hora exacta de fin ya no aplica.
9. Configura **Cupo global** como entero positivo si quieres limitar usos;
   vacio significa sin limite comercial. El maximo es 2.147.483.647.
10. Revisa y marca **Habilitada** cuando corresponda. Una regla nueva comienza deshabilitada.
11. Pulsa **Guardar promocion** y comprueba sus datos en el listado.

### 16.2 Buscar, editar, desactivar y revisar usos

Busca por nombre/codigo, filtra por automatica/cupon y habilitada/deshabilitada,
y pulsa **Buscar / actualizar**. Recorre la paginacion y pulsa **Editar**
en la regla. Para detenerla, desmarca **Habilitada** y pulsa **Guardar promocion**;
no se borran reglas con historial. **Cancelar edicion** limpia el formulario.

Habilitada no significa necesariamente vigente: revisa tambien fechas y cupo.
Para alcance de producto, pulsa **Buscar productos**, recorre las paginas y
pulsa **Seleccionar** junto a la referencia correcta. Comprueba el nombre
mostrado en **Seleccionado** antes de guardar.

Si ya tuvo usos, incluso liberados, no puedes cambiar el codigo ni convertir
entre automatica y cupon: crea otra regla. Tampoco puedes reducir el cupo por
debajo de los usos que siguen consumidos.

En **Ver usos**, consulta pedido, fecha, descuento y si el uso sigue consumido
o fue liberado. Puedes abrir el pedido administrativo para investigar el caso.

### 16.3 Como se elige el beneficio

- Primero se aplica la oferta individual vigente de cada producto.
- Despues se compara cada automatica disponible con el cupon introducido.
- Gana **un solo beneficio adicional**, el de mayor descuento efectivo, no
  necesariamente el porcentaje anunciado mas alto.
- En empate se prefiere el cupon introducido. El envio nunca recibe descuento.
- Un minimo por categoria o producto cuenta solo esa mercancia elegible
  despues de ofertas, no todo el carrito ni el envio.
- El porcentaje se redondea hacia abajo al peso; el descuento no supera la
  mercancia elegible ni el tope configurado.
- Un cupon invalido genera error. Si es valido pero gana una automatica, el
  resumen lo informa y el cupon no consume uso.

Ejemplo: precio regular 1.000 COP, oferta individual 800 COP y beneficio
adicional de 20 %. Se descuentan otros 160 COP; la mercancia queda en 640 COP.
Con envio de 5.000 COP, el total es **5.640 COP**.

### 16.4 Cupos e historial

Cotizar no reserva ni consume usos. Crear un pedido consume un uso del
beneficio elegido; recuperarlo no consume otro. Cancelar antes del despacho
libera ese uso una vez. Los retornos de envios ya despachados y las
devoluciones despues de entrega no liberan cupo, aunque se reembolse todo.

Los pendientes no liberan cupo por vencimiento automatico. Si no se atenderan,
el personal debe cancelarlos. Editar o desactivar una regla no cambia los
descuentos ni las obligaciones de devolucion de pedidos anteriores.

### 16.5 Significado de los campos y errores que conviene evitar

| Campo | Que decide | Ejemplo o precaucion |
| --- | --- | --- |
| Nombre | Etiqueta publica del beneficio que puede conservar el pedido | `Bienvenida de octubre`; no incluir instrucciones internas. |
| Aplicacion | Si se evalua automaticamente o exige codigo | Un cupon habilitado no se aplica si el cliente no lo introduce. |
| Codigo | Texto que se introduce en checkout, cuando es cupon | `BIENVENIDA10`; no es un enlace ni una clave del personal. |
| Tipo y Valor | Si el numero representa porcentaje o pesos | Porcentaje 10 significa 10 %; Monto fijo 10 significa 10 COP, no 10.000. |
| Compra minima elegible | Umbral que debe alcanzar solo la mercancia incluida en el alcance, despues de ofertas | Cero no exige minimo; el envio no ayuda a alcanzarlo. |
| Tope del descuento | Maximo COP que puede conceder la regla | Porcentaje 20 con tope 5.000 no puede descontar 8.000. |
| Alcance | Que lineas participan en minimo y descuento | Una categoria no descuenta productos de otra categoria. |
| Inicio y Fin exclusivo | Ventana temporal en que puede elegirse | Habilitada fuera de ese intervalo no equivale a vigente. |
| Cupo global | Numero total de pedidos que pueden consumir la regla | 100 no significa cien usos por cliente ni cien por dia. |
| Habilitada | Permiso administrativo para evaluar la regla | No sustituye vigencia, alcance, minimo ni cupo. |

Un campo de tope o cupo **vacio** omite ese limite comercial; poner cero no
es la forma de representar vacio, porque esos campos, cuando se diligencian,
requieren valor positivo. El nombre no reemplaza el codigo: una regla llamada
`Diez por ciento` debe configurarse como porcentaje con valor 10 para hacerlo.

El cupo es global, no un limite por persona. El sistema no convierte por
si solo una campana de bienvenida en una restriccion de primera compra.
No anuncies `solo una vez por cliente` si ese control no existe en el flujo.

### 16.6 Ejemplos de alcance, minimo, tope y comparacion

**Minimo por categoria:** el carrito tiene 30.000 COP de bebidas y 25.000 de
aseo, despues de sus ofertas. Una regla solo para bebidas con minimo de
40.000 no aplica, aunque todo el carrito sume 55.000 y tenga envio adicional.
Solo los 30.000 elegibles cuentan para su minimo.

**Tope:** 20 % sobre 40.000 produciria 8.000. Si el tope es 5.000, el beneficio
real queda en 5.000. Si ademas hay un cupon fijo valido de 6.000 sobre la
misma mercancia, puede ganar el cupon por tener mayor descuento real.

**No acumulacion adicional:** una automatica de 5.000 y un cupon de 6.000 no
dan 11.000; se elige uno. Las ofertas individuales ya aplicadas a productos
se conservan antes de esa comparacion. Comprueba el nombre del beneficio
ganador y su importe en el resumen, no solo el codigo que escribiste.

**Vigencia:** con fin el dia 30 a las 18:00 en la zona del navegador usado
para configurarlo, a esa hora exacta el intervalo ya termino. Si dos
administradores estan en zonas diferentes, pueden ver otra hora local que
representa el mismo instante. Coordina la zona antes de anunciar una campana;
los reportes usan fechas de Colombia, que es una regla distinta.

### 16.7 Verificar una promocion antes de anunciarla

1. Revisa tipo, valor, alcance, minimo, tope, fechas, cupo y habilitacion.
2. Si el alcance es un producto, confirma nombre y SKU seleccionados; buscar
   y ver resultados no equivale a pulsar **Seleccionar**.
3. En un checkout autorizado, agrega mercancia elegible y recalcula con el
   codigo si es cupon. No necesitas enviar para comprobar el resumen.
4. Verifica descuento real y que el envio no cambia por la promocion.
5. Comprueba tambien un caso fuera de alcance o sin minimo para entender
   que mensaje recibira el cliente. No lo anuncies como valido para todo.
6. Guarda los acuerdos de la campana por el canal del negocio y publicala
   solo cuando los datos revisados correspondan a lo anunciado.

Consultar no consume usos. Un pedido pendiente si consume cuando se crea;
por eso un listado de usos puede tener compras aun no entregadas. No reduzcas
contadores manualmente: las cancelaciones permitidas liberan el uso y
conservan su historial.

## 17. Consultar y exportar reportes

**Acceso:** administrador, **Reportes e indicadores**, `/admin/reportes`.

### 17.1 Procedimiento general

1. Elige la vista del reporte.
2. En vistas historicas, selecciona fechas y conjunto: **Solo reales**,
   **Solo pruebas** o **Reales y pruebas**.
3. Completa busqueda y demas filtros disponibles.
4. Pulsa **Consultar / actualizar**.
5. Revisa el periodo y los filtros aplicados que muestra el resultado.
6. Recorre las paginas si corresponde.
7. Para descargar el conjunto completo, pulsa **Exportar todo a CSV**.

El valor inicial historico es el mes actual y solo pedidos reales. Cambiar
un campo no actualiza por si solo los resultados: debes consultar. Paginar y
exportar usan los filtros **aplicados**, no cambios aun sin consultar.

### 17.2 Ventas y dinero

Consulta indicadores, evolucion diaria y estado actual de los pedidos creados
en el periodo. Interpreta cada concepto por separado:

| Indicador | Que reconoce |
| --- | --- |
| Pedidos creados | Pedidos que nacieron en el periodo, incluidos pendientes y cancelados. |
| Ventas entregadas | Mercancia neta original mas envio de pedidos entregados en el periodo. |
| Cobros | Dinero positivo confirmado como recibido en el periodo. |
| Reembolsos | Dinero registrado como devuelto en el periodo. |
| Flujo neto | Cobros menos reembolsos; puede ser negativo. |
| Sin cobro | Entregas de total cero, no fondos recibidos. |

Un pedido pagado pero no entregado no es una venta entregada. Un cobro
reembolsado sigue contando en su fecha original y su salida en la fecha del
reembolso. El estado del pedido es el **actual**, no una reconstruccion de
como estaba al final del periodo.

### 17.3 Productos y margen

Busca por nombre/SKU y ordena por unidades, mercancia neta o margen. Revisa
unidades entregadas, devueltas y repuestas, importes historicos y costos.

El margen estimado equivale a mercancia entregada neta de descuentos,
menos reembolsos posventa de mercancia, menos costo historico entregado,
mas costo historico de unidades vendibles recibidas y repuestas. Cada evento
cuenta en su propia fecha.

No es utilidad contable: no incluye impuestos, comisiones, transporte,
gastos fijos ni costeo por lotes. Una unidad danada no recupera costo por
reposicion vendible. Cambiar el costo del producto no altera los costos
historicos de los pedidos.

### 17.4 Inventario actual

Filtra por categoria, busqueda, agotados, stock bajo y visibilidad. Revisa
cantidades y valoracion al costo actual. Los totales cubren todo el filtro,
no solo la pagina visible.

Este reporte no reconstruye stock historico ni permite ajustarlo: haz los
ajustes en el producto. No tiene filtro de pedidos reales/prueba; puede
incluir productos de demostracion. No publicado incluye producto o categoria inactiva.

### 17.5 Promociones y cupones

Busca por nombre/codigo y revisa usos creados, liberados, descuentos historicos
y contador global actual. Solo aparecen reglas con eventos en el periodo.
Los usos se consumen al crear, no al entregar: no equivalen a ventas ni a
rentabilidad de una campana.

El contador global actual abarca todos los periodos y pedidos reales/de
prueba, incluso si filtras un solo conjunto. El nombre, codigo y cupo son
actuales; los descuentos de usos son historicos.

### 17.6 Fechas, limites y CSV

- Las fechas historicas son inclusivas y corresponden a Colombia, UTC-05:00,
  aunque tu equipo tenga otra zona horaria. El rango maximo es de 366 dias.
- Las fechas promocionales, en cambio, se editan en hora local del navegador.
- Si hay advertencias de calidad por fechas faltantes, no supongas que los
  totales incluyen esos eventos. Escala la inconsistencia para su revision.
- Cada consulta/exportacion admite hasta 50.000 registros fuente. Si se
  supera, reduce fechas o filtros; no se entrega un reporte truncado.
- El CSV contiene todos los resultados filtrados, no solo la pagina. La
  exportacion consulta nuevamente y puede reflejar operaciones posteriores
  a la consulta de pantalla.
- El archivo usa UTF-8 y separador punto y coma. Si la hoja de calculo no
  separa columnas, importalo con esa codificacion y separador.
- Los importes se exportan como texto protegido para preservar pesos exactos.
  No retires la proteccion de valores largos ni los conviertas a numeros de
  precision limitada. Si necesitas calcular, utiliza herramientas que
  preserven enteros de esa longitud.
- El CSV no exporta contactos o direcciones del comprador, referencias
  privadas de dinero ni credenciales de seguimiento.

### 17.7 Elegir el reporte segun la pregunta del negocio

| Necesidad | Vista y filtros recomendados | Que no concluye por si sola |
| --- | --- | --- |
| Cuanto dinero se registro recibido hoy | Ventas y dinero, desde/hasta hoy en Colombia, Solo reales; revisar Cobros | No dice que toda esa mercancia ya fue entregada. |
| Cuanto se entrego este mes | Ventas y dinero, fechas del mes, Solo reales; revisar ventas entregadas | No es necesariamente el dinero recibido ese mes. |
| Que referencias dejan mayor margen estimado | Productos y margen, periodo y orden por margen | No es utilidad final despues de todos los gastos. |
| Que debo reponer | Inventario actual, stock bajo o agotados, categoria si corresponde | No genera una orden de compra al proveedor. |
| Como se consumio un cupon | Promociones y cupones, periodo y busqueda de codigo | No atribuye automaticamente ventas o utilidad a la campana. |

En inventario, **Publicados** depende de producto y categoria activos; no
significa necesariamente stock positivo. Combina visibilidad con agotados
si quieres encontrar productos que el cliente ve pero no puede comprar.

### 17.8 Ejemplo por fechas: pedido, cobro, entrega y reembolso

Supongamos una compra de mercancia neta de 20.000 y envio de 5.000 COP:

| Dia de Colombia | Hecho registrado | Como aparece al consultar solo ese dia |
| --- | --- | --- |
| 1 de octubre | Se crea la compra | Un pedido creado; no hay venta entregada por ese hecho. |
| 2 de octubre | Se confirma el cobro de 25.000 | Cobros 25.000, aunque todavia no se entregue. |
| 3 de octubre | Se confirma la entrega | Venta entregada 25.000, sin inventar otro cobro. |
| 5 de octubre | Se registra un reembolso posventa de mercancia de 10.000 | Reembolsos 10.000; no borra el cobro del dia 2 ni la entrega del dia 3. |

Consultar del 1 al 5 incluye los eventos de cada fecha. Consultar solo el
dia 5 puede mostrar flujo neto negativo si no hubo cobros ese dia; no es
un error aritmetico. Si el costo historico total entregado era 12.000 y
se repuso una unidad vendible con costo 6.000 dentro de ese mismo rango,
el margen por eventos del rango es 20.000 menos 10.000 menos 12.000 mas
6.000: **4.000 COP**. El envio no se suma al margen de mercancia.

Si la recepcion vendible fue el dia 4 y el reembolso el 5, cada componente
cae en su propio dia. Consultar dias separados puede dar margenes distintos
sin inconsistencia. No corrijas fechas o precios del catalogo para hacer que
un reporte de eventos se parezca a caja.

### 17.9 Como interpretar datos historicos y actuales

Los productos vendidos conservan sus nombres, precios, descuentos y costos
de la operacion. Si hoy renombras un producto o cambias su costo, el reporte
historico no debe tratarlo como si siempre hubiera tenido esos datos nuevos.
El inventario actual, en cambio, usa cantidad y costo actuales: no es una
valoracion de existencias al cierre de un mes pasado.

En promociones, **Usos creados** cuenta eventos de consumo; **Liberados**
cuenta liberaciones del periodo, aunque el consumo se originara antes.
**Creados aun consumidos** pregunta cuantos usos creados en el periodo
siguen consumidos. **Contador global actual** mira el estado completo de la
regla, no solo el periodo ni solo reales. No restes esas columnas como si
todas tuvieran necesariamente el mismo universo temporal.

Si incluyes pruebas, el reporte financiero puede mezclar ejercicios y ventas
reales. Para conciliacion comercial empieza con **Solo reales** y revisa
las advertencias. El filtro no borra ni transforma pedidos de prueba.

### 17.10 Exportar e importar el CSV paso a paso

1. Selecciona la vista, escribe fechas y filtros y pulsa **Consultar / actualizar**.
2. Comprueba el periodo aplicado y la cantidad de resultados.
3. Pulsa **Exportar todo a CSV** y espera la descarga del navegador. Se exporta
   el filtro completo, no solo las filas visibles.
4. Abre la hoja de calculo. Si al abrir directamente todo queda en una columna,
   utiliza su opcion de importar texto/CSV, selecciona UTF-8 y separador `;`.
5. Mantiene los importes largos como texto; una conversion automatica puede
   perder digitos. El apostrofe protector no representa un peso adicional.
6. Verifica encabezados, periodo y valores contra una consulta cercana en tiempo.
   Si hubo operaciones entre consulta y exportacion, vuelve a consultar
   antes de concluir que hay diferencias injustificadas.
7. Guarda el archivo en la ubicacion privada autorizada. Contiene informacion
   financiera aunque no incluya contactos del comprador.

Si excede el limite de fuentes, divide el analisis en periodos no solapados
o acota la busqueda. No sumes indiscriminadamente contadores actuales y
estados de varios archivos: son fotografias de situacion, no todos son flujos
acumulables. Un CSV tampoco sustituye un respaldo de base y archivos.

## 18. Administrar usuarios

**Acceso:** administrador, **Usuarios**, `/admin/usuarios`.

### 18.1 Buscar cuentas

Busca por nombre o correo y filtra por rol y estado. Recorre la paginacion y
abre la cuenta correcta. Las inactivas permanecen visibles y conservan historial.

### 18.2 Crear una cuenta

1. Pulsa **Nueva / limpiar** y escribe nombre, hasta 160 caracteres, y correo,
   hasta 254 caracteres.
2. Elige **Operador de pedidos** o **Administrador**; el valor inicial es operador.
3. Revisa el estado activo/inactivo que corresponda.
4. Escribe y confirma una clave temporal conforme a la politica de la seccion 3.
5. Pulsa **Crear con cambio de clave obligatorio** y comprueba el resultado.
6. Entrega la clave por un canal privado al destinatario, nunca en un grupo.
7. Indicale que debe ingresar, cambiarla y volver a iniciar sesion.

El correo debe ser unico incluso entre cuentas inactivas. No se distinguen
mayusculas ni espacios exteriores. El sistema no envia la clave por correo
ni permite consultarla despues. Toda cuenta nueva, tambien de administrador,
debe cambiar su clave antes de operar.

### 18.3 Editar perfil, rol o estado

1. Pulsa **Editar / restablecer** en la cuenta y verifica su correo y nombre.
2. Modifica los datos autorizados y pulsa **Guardar cuenta y revocar sesiones**.
3. Avisa al destinatario: **todo guardado de perfil revoca sus sesiones**,
   incluso si solo cambia nombre o correo.
4. Si cambiaste el rol, comprueba con el usuario su nuevo acceso tras volver a ingresar.

No puedes desactivar ni cambiar el rol de tu propia cuenta. Siempre debe
quedar al menos un administrador activo. No hay borrado fisico de usuarios.
Reactivar una cuenta no revive sus sesiones antiguas.

### 18.4 Restablecer una clave ajena

1. Confirma la identidad del solicitante por el procedimiento del negocio.
2. Abre su cuenta. Guarda o recarga primero cualquier cambio de perfil pendiente.
3. Usa el formulario de restablecimiento con una clave temporal nueva,
   diferente de la actual, y su confirmacion.
4. Pulsa **Restablecer y cerrar sesiones** y entrega la clave por canal privado.
5. El usuario debe cambiarla y autenticar de nuevo. Sus sesiones previas dejan de ser validas.

Restablecer no reactiva una cuenta desactivada. No restablezcas tu propia
clave desde Usuarios: utiliza **Mi cuenta**. Si guardas tu propio perfil,
tambien se cierra tu sesion y debes ingresar nuevamente.

### 18.5 Significado de perfil, rol, estado y tipo de contrasena

| Dato | Funcion | Ejemplo de decision |
| --- | --- | --- |
| Nombre | Identifica al empleado dentro del portal | Usar un nombre reconocible, no una contrasena o un nombre de cliente. |
| Correo | Identificador de ingreso unico | Corregir un correo no conserva sesiones previas; avisar al empleado del cambio. |
| Rol | Conjunto de acciones autorizadas | Operador para atencion diaria; administrador solo si necesita funciones de gestion. |
| Cuenta activa | Permite autenticar y usar permisos vigentes | Desactivar al terminar la autorizacion laboral; no borra historial. |
| Cambio obligatorio | El usuario debe reemplazar la temporal antes del portal | Esperado en cuentas nuevas y restablecimientos. |
| Definitiva | Ya no exige el cambio temporal inicial | No significa que el administrador pueda conocer o recuperar esa clave. |

Crear una cuenta inactiva registra el perfil, pero no permite que el empleado
trabaje hasta activarla. Restablecer su clave no modifica por si solo ese
estado. Guardar un perfil invalida sus sesiones aunque el rol y el estado
no cambien; no lo hagas repetidamente solo para refrescar el listado.

### 18.6 Alta y retiro de un empleado: ejemplo completo

Para un nuevo operador, confirma su identidad y correo autorizado, utiliza
**Nueva / limpiar**, selecciona Operador, revisa Cuenta activa y crea con
temporal. Comunica direccion de la tienda, correo, temporal y necesidad de
cambiarla por el canal privado del negocio. El sistema no envia esa invitacion.

Comprueba con la persona que cambia la clave, vuelve a entrar y ve **Pedidos**
y **Mi cuenta**, sin gestion administrativa restringida. No le pidas que te
comunique su nueva clave para verificarlo. La confirmacion es el acceso y
el estado de cambio, no conocer el secreto.

Para retirar autorizacion, abre la cuenta correcta, desmarca Cuenta activa y
guarda. Las sesiones anteriores se rechazan en su siguiente peticion; una
operacion que ya estaba en curso no se deshace por esa medida. Revisa las
tareas pendientes de esa persona y reasigna su atencion por el procedimiento
del negocio. No se eliminan sus eventos historicos.

### 18.7 Como evitar bloquear la administracion

Conserva al menos un administrador activo con acceso seguro y definido por
el negocio. El sistema impide ciertas desactivaciones y cambios de rol,
pero eso no recupera una clave olvidada ni crea un administrador de reemplazo.
No conviertas una cuenta compartida en el unico mecanismo de continuidad.

Para recuperar otra cuenta, usa el restablecimiento; para cambiar tu propia
clave, usa Mi cuenta. Si solo necesitas actualizar la lista, pulsa Buscar,
no Guardar cuenta. Ante conflicto, recarga la cuenta y revisa sus datos antes
de aplicar cambios: otra persona pudo desactivarla o cambiarle el rol.

## 19. Rutinas, ejemplos y solucion de problemas

### 19.1 Inicio de jornada

1. Ingresa con tu cuenta y comprueba el entorno, rol y ausencia de avisos de prueba.
2. Consulta pedidos pendientes y confirma los que realmente puedan atenderse.
3. Revisa pagos en revision verificando los fondos fuera del sistema.
4. Atiende pedidos confirmados, en preparacion, listos y despachados segun su etapa real.
5. Revisa recepciones pendientes y, con el administrador, reembolsos pendientes.
6. Como administrador, revisa agotados, stock bajo, cobertura y promociones vigentes.

### 19.2 Cierre de jornada

1. Actualiza pedidos con lo que fisicamente ocurrio; no adelantes estados.
2. Compara cobros y reembolsos registrados con banco, billetera y caja.
3. Revisa pedidos sin atender; cancela solo los que corresponda, con motivo publico.
4. Verifica obligaciones pendientes, incluidos reembolsos por cancelacion.
5. Como administrador, consulta el reporte real del periodo correcto y exporta si se necesita.
6. Avisa al responsable tecnico si no hay confirmacion del respaldo programado.
7. Cierra sesion, especialmente en equipos compartidos.

### 19.3 Ejemplo completo: transferencia y transportadora

1. El cliente compra 2 unidades de 10.000 COP sin descuento; envio 15.000 COP.
2. El resumen totaliza 35.000 COP. El cliente acepta condiciones, crea el
   pedido y conserva su enlace. No se descuenta stock todavia.
3. El operador confirma; salen 2 unidades del inventario.
4. El cliente paga externamente 35.000 COP y adjunta referencia e imagen.
5. El operador verifica el abono real y registra los 35.000 COP recibidos.
6. Inicia preparacion y marca listo al terminar el empaque.
7. Registra transportadora y guia y despacha cuando realmente salga.
8. Confirma entrega solo tras comprobar que el cliente recibio el pedido.
9. Si se devuelve 1 unidad vendible despues, registra solicitud, recepcion de
   1 unidad y reposicion de 1. Sin devolucion de envio, el reembolso de
   mercancia de este ejemplo es 10.000 COP.
10. El administrador devuelve esos fondos por fuera y despues registra el
    reembolso exacto. El pedido sigue entregado y el pago tiene reembolso parcial.

### 19.4 Ejemplo completo: recogida y efectivo

1. El cliente crea una recogida de 20.000 COP, sin envio, con efectivo contra entrega.
2. El operador confirma, inicia preparacion y marca listo.
3. Al retirar, cobra realmente 20.000 COP y registra el total recibido.
4. Marca la recepcion del cliente y confirma entrega. No registra despacho.

### 19.5 Ejemplo: cancelar un pedido pagado antes de despachar

1. Confirma el estado permitido y resuelve cualquier pago en revision.
2. Cancela con motivo publico.
3. Revisa la reposicion automatica del stock si ya se habia confirmado.
4. Abre la obligacion generada por el total neto, incluido el envio.
5. El administrador devuelve el dinero externamente y registra el reembolso.
6. No recibas ni repongas mercancia otra vez por esa misma cancelacion.

### 19.6 Problemas frecuentes y accion recomendada

| Situacion | Que hacer |
| --- | --- |
| No puedo ingresar | Revisa correo y clave; confirma que la cuenta esta activa. Si olvidaste la clave, solicita una temporal a otro administrador. |
| Vuelvo siempre a Mi cuenta | Completa el cambio obligatorio y vuelve a ingresar con la clave nueva. |
| No veo una opcion del portal | Revisa tu rol. Operadores no administran catalogo, configuracion, promociones, reportes ni usuarios, y no registran reembolsos. |
| Un producto guardado no aparece | Comprueba producto activo, categoria activa y filtros de busqueda. Guardar no activa automaticamente ambos. |
| Error de SKU, URL o correo duplicado | Busca el registro existente, tambien inactivo, o usa un valor realmente unico. |
| No puedo guardar existencias | Usa saldo final entero no negativo y completa el motivo si estas editando. |
| No puedo administrar imagenes | Guarda primero el producto y sus cambios pendientes; revisa formato, peso y limite de 8. |
| No puedo cargar comprobante | Revisa pago pendiente/rechazado, formato, peso y limite historico de 5; efectivo y total cero no lo requieren. |
| El municipio no tiene cobertura | Selecciona una modalidad habilitada con cobertura o coordina con el negocio; no inventes un municipio para eludirla. |
| No puedo crear pedidos en produccion | Pide al administrador revisar configuracion real, modalidades, medios, condiciones y disponibilidad. La configuracion de prueba no funciona alli. |
| El cupon da error | Revisa codigo, activacion, fechas, cupo, alcance y minimo; corrigelo o retiralo y recalcula. |
| El cupon no fue el beneficio elegido | Revisa si una automatica otorga un descuento mayor; solo se aplica un beneficio adicional. |
| Cambie datos y ya no puedo enviar | Recalcula, revisa y vuelve a aceptar condiciones. |
| Error tras enviar una compra o devolucion | Recupera el intento; revisa si ya existe antes de descartar o repetir como nuevo. |
| No se puede confirmar un pedido | Verifica stock y activacion de todas sus lineas; pendientes simultaneos pueden haber agotado existencias. |
| No puedo iniciar preparacion | Confirma primero el pedido; transferencia y billetera requieren pago validado. |
| No puedo registrar efectivo | El pedido debe estar listo o despachado; registra el total exacto realmente cobrado. |
| No puedo confirmar entrega | Revisa estado logistico, pago resuelto y casilla de recepcion. Recogida parte de listo; envio parte de despachado. |
| No puedo cancelar | Resuelve pago en revision; si ya esta despachado usa retorno completo y si esta entregado usa devolucion. |
| No puedo devolver cierta cantidad | Revisa unidades y saldos reservados por solicitudes previas no rechazadas. |
| No aparece registrar reembolso | Requiere administrador, obligacion recibida y un importe positivo pendiente. |
| Reporte sin resultados | Revisa fechas, conjunto real/prueba y filtros, y pulsa consultar. |
| Reporte supera limite | Reduce el periodo o acota filtros; no se entregan totales parciales. |
| El CSV se abre en una columna | Importa como UTF-8 con separador punto y coma. |
| El reporte no coincide con caja | Separa ventas entregadas, cobros y reembolsos; usa fechas de Colombia y el conjunto correcto. |
| No abre WhatsApp/correo | Verifica telefono/correo y la aplicacion externa; puedes copiar y enviar manualmente. |
| El navegador bloquea el portapapeles | Selecciona y copia manualmente el texto del borrador. |
| Perdi el enlace privado | Busca el enlace guardado o recupera el intento en la pestaña original; si no estan, contacta privadamente al negocio. |

### 19.7 Mensajes tecnicos que pueden aparecer

| Codigo / mensaje | Interpretacion y respuesta |
| --- | --- |
| 400 / validacion | Corrige los campos o filtros indicados; revisa enteros, longitudes y requisitos. |
| 401 / sesion no valida | Inicia sesion otra vez; revisa antes de repetir operaciones que pudieran completarse. |
| 403 / sin permiso | Tu cuenta no puede hacer esa accion; solicita intervencion del rol autorizado. |
| 404 / no encontrado | Revisa el recurso o numero/codigo. En recuperacion de intento no demuestra por si solo que nunca se creo. |
| 409 / conflicto | Recarga el registro, revisa estado y cambios ajenos; en intentos recuperables no reutilices el intento con otro contenido. |
| 429 / demasiados intentos | Espera y respeta el tiempo indicado si aparece; no pulses repetidamente. Algunos limites se comparten por IP. |
| Error de conexion, timeout o servidor | Comprueba conexion y escala si persiste. Para creaciones usa recuperacion; para otras acciones revisa estado/historial antes de repetir. |
| Error de almacenamiento del navegador | Conserva numero/enlace visibles. No borres datos ni descartes intentos sin comprobar el resultado; solicita ayuda si no puede guardarse la recuperacion. |

Al informar un incidente incluye pantalla, accion, hora, numero de pedido si
corresponde y mensaje de error. **No incluyas** contrasenas, codigo privado,
enlace completo de seguimiento, comprobantes ni datos personales en un ticket publico.

### 19.8 Metodo de diagnostico sin duplicar operaciones

1. Identifica el registro y anota numero de pedido, hora y accion intentada.
2. Distingue si estas ante un formulario invalido, una restriccion de estado,
   un permiso insuficiente o una respuesta perdida. No tienen la misma solucion.
3. Si no habia envio, corrige el campo y revisa antes de guardar. Si el envio
   pudo ocurrir, consulta primero el estado e historial.
4. Para creacion de compra o solicitud de devolucion con intento pendiente,
   utiliza su recuperacion. No uses borrar datos del navegador como primer remedio.
5. Para cambios con conflicto, conserva una nota de tus ediciones, recarga y
   compara lo actual; no repitas con una version antigua ni abras otra cuenta.
6. Si hubo dinero o mercancia real, comprueba tambien el hecho externo. Una
   pantalla atrasada no prueba que falten fondos ni que las unidades no salieron.
7. Si no puedes resolverlo con un requisito documentado, escala sin inventar
   estados, stock o importes. Entrega solo la informacion necesaria y privada.

### 19.9 Traspaso de tareas entre jornadas o empleados

Al terminar, identifica pedidos aun por confirmar, comprobantes en revision,
preparaciones, listos, despachos, recepciones y reembolsos. Comunica numero,
tarea pendiente y responsable por el canal del negocio, sin contrasenas ni
enlaces secretos. El siguiente empleado debe volver a consultar el estado,
porque el listado del turno anterior no es una reserva de trabajo.

Para una incidencia financiera, separa `dinero devuelto externamente pero
registro pendiente` de `dinero aun por devolver`. Esa distincion evita una
segunda transferencia. Para inventario, separa `mercancia recibida fisicamente`
de `recepcion registrada y stock repuesto`. No repitas movimientos por
suponer que ambos hechos siempre ocurren al mismo tiempo.

### 19.10 Lista de practica para formar a una persona nueva

Realiza estos ejercicios en un entorno autorizado de desarrollo, sin fondos
ni datos personales reales, con revision de un responsable:

1. Ingresar con temporal, cambiarla y volver a autenticar.
2. Como administrador, crear categoria y explicar Nombre, URL, Orden y Activa.
3. Crear producto y explicar SKU, unidad, presentacion, precio, costo y stock.
4. Agregar imagen y verificar el orden publico.
5. Configurar y revisar una entrega con su tarifa, cobertura y condiciones.
6. Como cliente, buscar, ajustar cantidades, cotizar y explicar cada importe.
7. Crear una compra, conservar seguimiento y distinguir numero de codigo privado.
8. Como personal, confirmar stock y recorrer pago y entrega segun el medio.
9. Resolver una cancelacion antes del despacho sin duplicar reposicion.
10. Registrar solicitud y recepcion de una devolucion, distinguiendo lo vendible.
11. Como administrador, practicar el registro de un reembolso simulado en el
    entorno de prueba, sin ejecutar una transferencia real.
12. Crear un cupon, revisar minimo/tope y explicar por que no se acumula con
    otra automatica adicional.
13. Consultar reportes de pruebas, explicar venta frente a cobro y exportar.
14. Crear/restablecer una cuenta de ensayo y comprobar la revocacion de sesion.

Una persona esta lista para operar su rol cuando puede explicar que cambia
cada accion, verificar el resultado y reconocer cuando debe detenerse y
escalar. No necesita permisos administrativos para aprobar su entrenamiento
como operador; los ejercicios restringidos los realiza el administrador.

## 20. Limites, glosario y referencias

### 20.1 Lo que no hace el sistema

- No ofrece cuentas de clientes ni sincronizacion de carrito entre dispositivos.
- No cobra automaticamente ni integra una pasarela bancaria.
- No consulta fondos ni ejecuta devoluciones de dinero.
- No envia automaticamente correos o WhatsApp.
- No calcula envio por distancia/peso ni rastrea transportadoras automaticamente.
- No reserva inventario al crear el pedido.
- No ofrece recuperacion publica de claves o seguimiento por correo/telefono.
- No ofrece en estas pantallas borrado fisico de productos, categorias o usuarios.
- No reconstruye inventario historico ni sustituye un sistema de contabilidad.
- No ofrece en el portal un boton para respaldar/restaurar la base o desplegar versiones.

No prometer estas funciones al cliente ni improvisarlas cambiando estados
para simular que ocurrieron.

### 20.2 Glosario de trabajo

| Termino | Significado |
| --- | --- |
| Administrador | Cuenta con gestion de configuracion, catalogo, usuarios, reportes y reembolsos. |
| Operador de pedidos | Cuenta que atiende pedidos, pagos y recepciones, con permisos limitados. |
| SKU | Referencia unica del producto dentro del negocio. |
| URL / slug | Identificador legible. En producto forma su ruta publica; en categoria se guarda sin crear una pagina publica independiente. |
| Existencias / stock | Unidades registradas disponibles; se descuentan al confirmar. |
| Checkout | Formulario de datos, cotizacion, condiciones y creacion de compra. |
| Cotizacion / resumen | Calculo vigente de productos, descuentos y envio; no es una reserva. |
| Codigo privado | Credencial secreta que acompana el numero de pedido para seguimiento. |
| Conciliacion | Verificacion externa de fondos y registro del resultado en el sistema. |
| Beneficio adicional | Un unico descuento automatico o cupon, aplicado despues de ofertas de producto. |
| Neto | Importe despues de los descuentos que correspondan. |
| Recepcion | Registro fisico de mercancia devuelta y de unidades vendibles para reponer. |
| Reembolso | Dinero devuelto externamente y despues registrado por un administrador. |
| Conflicto de version | El registro cambio desde que se consulto; requiere recargar y revisar. |
| CSV | Archivo tabular exportado para consulta en hojas de calculo. |

### 20.3 Referencias del sistema y responsabilidad tecnica

Este manual se apoya en las rutas y pantallas implementadas. Para reglas
tecnicas, soporte y mantenimiento, consulta:

- [Catalogo y administracion](catalog.md).
- [Carrito, checkout y pedidos](checkout.md).
- [Operacion, pagos, entregas y devoluciones](operations.md).
- [Promociones y cupones](promotions.md).
- [Reportes e indicadores](reports.md).
- [Usuarios y cuenta personal](users.md).
- [Seguridad y primer acceso](security.md).
- [Produccion, publicacion y respaldos](production.md).
- [Base de datos y copias consistentes](database.md).
- [Inicio local y requisitos tecnicos](../README.md).

El administrador del negocio debe coordinar con soporte la verificacion de
respaldos y las pruebas de restauracion. Una copia completa debe incluir la
base y los archivos de imagenes y comprobantes, no solo una exportacion CSV.
No copies una base SQLite en uso por tu cuenta ni reemplaces archivos del
servidor para resolver un error de operacion.

### 20.4 Vocabulario ampliado para interpretar decisiones

**Identificador interno:** valor tecnico que mantiene relaciones aunque se
editen nombres. No es un campo que el usuario deba copiar para categorizar
productos o crear promociones: utiliza los selectores y buscadores.

**Instantanea historica:** copia de datos comerciales conservada al crear la
operacion, como precio, costo, descuento o condiciones aceptadas. Permite
atender y reportar la compra original aunque el catalogo cambie despues.

**Version del registro:** dato que el formulario conserva para detectar si
otro proceso cambio lo que abriste. Un conflicto de version protege contra
sobrescrituras; no se corrige escribiendo cualquier fecha ni recargando sin
comparar los datos que ibas a modificar.

**Idempotencia:** proteccion para que repetir el mismo intento valido no
genere dos veces su efecto. No significa que cualquier compra parecida se
detecte como duplicada ni que puedas usar un intento anterior con otro contenido.

**Reserva de devolucion:** unidades y saldos comprometidos por una solicitud.
No equivale a la reserva de stock de venta, que no se hace al crear pedidos.
La mercancia vendible solo se repone cuando se registra su recepcion, o
cuando corresponde por una cancelacion previa al despacho.

**Auditoria:** registro interno de acciones para trazabilidad. Que exista
no significa que todas sus entradas tengan una pantalla de consulta o un
boton para deshacerlas; utiliza el historial y los controles disponibles.

### 20.5 Que puede resolver cada responsable

| Caso | Responsable principal | Resultado que se debe solicitar |
| --- | --- | --- |
| Duda de precio, categoria, stock o tarifa | Administrador del negocio | Revisar el dato vigente y su efecto comercial antes de modificar. |
| Pago o reembolso no conciliado | Personal autorizado y administrador para reembolso | Comprobar movimiento externo y registrar una sola vez. |
| Condiciones, disputa o autorizacion de devolucion | Responsable comercial competente | Definir el acuerdo; el formulario no resuelve la politica por si solo. |
| Clave olvidada de empleado | Otro administrador autorizado | Asignar temporal y exigir cambio, sin revelar claves anteriores. |
| Fallo persistente, datos incoherentes, archivos inaccesibles | Soporte tecnico | Diagnostico con evidencia minima y sin secretos publicos. |
| Respaldo, restauracion, instalacion o primer administrador de produccion | Responsable tecnico autorizado | Ejecutar procedimiento documentado y comprobar su resultado. |

No se garantiza operacion segura solo por completar formularios: el personal
debe verificar los hechos fisicos, proteger el acceso y aplicar las politicas
aprobadas. El manual describe lo implementado; no agrega funciones ni elimina
restricciones de permisos, estados, cantidades o importes.