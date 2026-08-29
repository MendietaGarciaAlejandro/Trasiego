# Trasiego

ERP de inventario con valoración. Un trasiego es pasar algo de un recipiente a otro, que es
literalmente lo que hace un movimiento de existencias.

La idea no es abarcar mucho, sino ir hondo en una sola cosa. Un CRUD de almacén lo hace
cualquiera; lo que tiene miga es que el **valor** del almacén cuadre siempre, y que siga
cuadrando cuando alguien mete un movimiento con fecha del mes pasado, devuelve material que
compró a otro precio, o dos personas descuentan del mismo stock a la vez.

De ahí sale la única regla que no se puede romper nunca:

> El valor de un almacén es siempre igual a la suma de sus movimientos valorados.

Todo lo demás del proyecto está para que esa frase siga siendo verdad.

## Stack

- .NET 10, capas separadas (Dominio, Aplicación, Infraestructura, Api)
- SQL Server 2022 y EF Core 10
- xUnit; los tests de integración corren sobre LocalDB
- ASP.NET Core con controllers, y Scalar para probarla a mano (`/scalar` en desarrollo)
- Escritorio en WPF alojando un `BlazorWebView`, con las pantallas en una biblioteca aparte
  para que la web pueda reusarlas tal cual

## Cómo levantarlo

Hace falta SQL Server (vale Developer Edition) y LocalDB para los tests. No hace falta Docker.

```
dotnet tool restore
dotnet ef database update --project src/Trasiego.Infraestructura --startup-project src/Trasiego.Api
dotnet run --project src/Trasiego.Api
```

La cadena de conexión de desarrollo está en `appsettings.Development.json` y apunta a
`localhost` con autenticación integrada. No la puse en user-secrets como en Camar porque aquí
no hay ningún secreto que guardar: no lleva usuario ni contraseña. En producción sí saldría
de user-secrets o de una variable de entorno.

Para el escritorio, con la Api levantada:

```bash
dotnet run --project src/Trasiego.Escritorio
```

`dotnet test` ejecuta todo (unos 118 tests). Los de la API levantan la aplicación entera con `WebApplicationFactory` contra la misma base de datos de pruebas. Los tests de dominio no tocan la base de datos y son
instantáneos; los de integración crean una base de datos suya en LocalDB al empezar y la
borran al terminar.

## Alcance

Lo que se pretende que haga:

- Artículos, almacenes y movimientos de existencias
- Valoración por capas FIFO y por precio medio ponderado, conviviendo
- Ficha de artículo (kardex) con saldo corrido de cantidad y de valor
- Informes de valoración a una fecha
- Cierre de periodo

Lo que no va a hacer, para que quede claro desde el principio:

- Multiempresa
- Contabilidad
- Compras ni ventas: los movimientos entran directamente, sin documento detrás
- Lotes ni números de serie
- Ubicaciones dentro del almacén

## Decisiones tomadas hasta ahora

### Cuatro decimales aunque se enseñen dos

Tres unidades que costaron 10,00 € salen a 3,333333... € cada una. Si ese número se redondea
a dos decimales antes de seguir operando, la diferencia se va acumulando movimiento a
movimiento hasta que el valor del almacén deja de cuadrar con la suma de sus movimientos, que
es justo lo único que no puede pasar.

Los importes se guardan con cuatro decimales y se presentan con dos. El redondeo es comercial
(`MidpointRounding.AwayFromZero`), no el bancario que trae .NET por defecto: 0,125 son 0,13,
que es lo que espera cualquiera que mire una factura.

### No se guarda ningún coste unitario

Una capa de existencias guarda cantidad y valor total. El coste unitario se calcula cuando
hay que enseñarlo y se devuelve como `decimal`, no como `Importe`, precisamente para que no
apetezca guardarlo ni seguir operando con él.

### El resto se resta, nunca se recalcula

Al consumir parte de una capa se calcula lo que sale y lo que queda es una resta. Pedir la
otra proporción por separado descuadra: un tercio de 10,00 € es 3,3333, y tres tercios
calculados así suman 9,9999. Hay un test para cada mitad de esto, porque es el error que más
caro sale luego y no se ve leyendo el código.

### Una cantidad nunca es negativa

`Cantidad` es una magnitud: lo que entra, lo que sale, lo que queda. El signo lo pone el tipo
de movimiento, no el número. Restar de más lanza una excepción en vez de devolver un negativo,
y así una capa no puede quedarse en negativo por una resta mal hecha.

El saldo de un almacén sí puede ser negativo, porque en la vida real se sirve mercancía antes
de registrar la compra. Pero eso es otro concepto y llevará su propio tipo con signo.

### Fecha contable y momento de registro, separados desde el primer día

Cada movimiento va a llevar dos fechas: el día al que pertenece y el instante en que se
registró. Son cosas distintas en cuanto alguien mete un movimiento retroactivo, y mezclarlas
hace imposible explicar por qué un informe de ayer da hoy un número diferente. En Camar
simplifiqué las zonas horarias y me costó rehacerlo; aquí prefiero pagarlo al principio.

### LocalDB para los tests, no un contenedor

En Camar los tests de integración levantan un Postgres con Testcontainers, porque allí hacía
falta una `EXCLUDE USING gist` que no existe en ningún proveedor en memoria. Aquí también
hace falta SQL Server de verdad (índices únicos, precisión de los `decimal`, aislamiento de
transacciones), pero no hace falta que sea el mismo binario que en producción. LocalDB ya
está instalado con las herramientas de SQL Server, arranca solo y no obliga a tener Docker
abierto para pasar los tests.

Cada ejecución crea su propia base de datos y la borra al terminar, así que tampoco toca la
instancia de desarrollo.

### El saldo se calcula sumando movimientos

No hay tabla de existencias. El saldo de un artículo en un almacén es la suma de sus
movimientos, que es literalmente la invariante del proyecto convertida en consulta. Una tabla
de existencias mantenida aparte sería más rápida, pero es también otra cosa que puede dejar
de cuadrar, y todavía no hay ningún problema que la justifique. Cuando llegue la
concurrencia se verá.

Esa suma va en SQL escrito a mano. `Cantidad` se guarda con un `ValueConverter`, y EF sabe
convertir el valor de ida y vuelta pero no sabe sumar el tipo del dominio: para agregarlo
tendría que traerse todos los movimientos y sumarlos en memoria, que es justo lo que no puede
hacer un saldo de almacén.

### Cada entrada abre una capa, y la salida vacía capas por antigüedad

FIFO no se calcula, se guarda. Cada entrada abre una capa con su cantidad y su coste, y una
salida va vaciando capas hasta cubrir lo que se pide. El coste de una salida no lo teclea
nadie: es la suma de lo que ha ido saliendo de cada capa.

El orden lo pone la **fecha contable**, no la de registro. Un albarán traspapelado que se
teclea hoy con fecha de la semana pasada es más antiguo que otro tecleado ayer con fecha de
ayer, y en FIFO sale antes. Es la primera vez que la separación de las dos fechas cambia un
número y no solo un informe.

Cada salida deja además una fila por capa de la que sacó algo. Sin eso, el coste de una
salida es un número sin explicación, y no habría manera de devolver material al precio al que
entró.

### Precio medio es FIFO con una sola capa

Los dos criterios comparten casi todo el código, y no por ahorrar sino porque son lo mismo
visto de otra manera. FIFO abre una capa por entrada para poder sacar cada una a su coste;
precio medio mete todas las entradas en la capa que ya estaba abierta. Sacar una parte
proporcional de esa capa única *es* la media ponderada.

Así que **lo que cambia entre un método y otro es la entrada, no la salida**: el código que
consume capas por antigüedad vale igual para los dos, porque a precio medio recorrer las
capas abiertas se queda en recorrer una.

El criterio va por artículo y no por almacén. Mover el mismo material de un almacén a otro no
puede cambiar lo que vale, y la norma contable pide poder explicar con qué criterio se ha
valorado cada cosa. Y no se cambia una vez el artículo tiene movimientos: si ya se ha
valorado una salida con un criterio, cambiarlo deja el almacén contando una cosa y los
movimientos otra.

### Lo que entra se teclea en total, no por unidad

Una entrada pide lo que costó entera, no lo que cuesta cada unidad. Si se pidiera el precio
unitario habría que multiplicarlo por la cantidad, y el redondeo de esa multiplicación ya no
cuadraría con la factura que hay encima de la mesa.

### Lo devuelto vuelve al coste al que salió

Para esto estaba la tabla de consumos. Una devolución no entra al precio de hoy ni a la media
del momento: se mira de qué capas salió aquella salida y a qué coste, y eso es lo que vuelve.
Diez unidades que costaron 1 € vuelven valiendo 1 €, aunque entre medias haya entrado
material a 8 €.

Dónde acaba lo devuelto sí depende del criterio. En FIFO cada trozo repone la capa de la que
salió, que es lo que mantiene su coste separado del de las demás. A precio medio no hay capas
que distinguir: entra en la que esté abierta y rehace la media, que es lo que se espera de una
media. El coste es el original en los dos casos; lo que cambia es dónde cae.

Devolver a plazos tampoco pierde céntimos: cada consumo lleva cuánto se ha devuelto ya, y el
coste se calcula sobre lo que queda por devolver restando, igual que en las capas.

### Un recuento por encima entra al precio de lo que ya había

Si el inventario dice que hay dos unidades más de las que el sistema creía, esas dos valen lo
mismo que las demás: la regularización entra al precio de las existencias y el valor unitario
del almacén no se mueve. Es el único sitio del proyecto donde se usa un coste unitario para
calcular algo, y va comentado como tal.

Si no había existencias no hay precio del que tirar, y en vez de inventarse uno la operación
se rechaza pidiendo que se registre como una entrada normal con su coste.

### Bajar de cero se permite, pero es una decisión del almacén

Por defecto una salida que dejaría el saldo en negativo se rechaza. Un almacén puede
marcarse como que admite descubierto, y entonces sirve lo que no tiene: una obra gasta
material que todavía no se ha dado de alta, una tienda no.

Lo que sale sin estar hay que valorarlo con algo, y se valora **al último precio conocido**,
que es la mejor suposición disponible. Si por ese almacén no ha pasado nunca ese artículo no
hay ningún precio del que tirar, y en vez de inventarse uno la operación se rechaza.

El descubierto se guarda: es lo contrario de una capa, una deuda de existencias con un coste
provisional que **resta** del valor del almacén. Cuando llega la entrada, lo primero que hace
es taparlo.

Aquí está lo que me parece más interesante de toda esta parte. Si la entrada que tapa el
descubierto costó otra cosa de lo que se supuso, esa diferencia es real y tiene que quedar en
algún sitio. Como lo ya valorado no se revisa, **la carga lo que quede en el almacén**: cinco
unidades que salieron valoradas a 2 € y llegaron costando 4 € dejan tres unidades valiendo
22 € en vez de 12 €. Eso no lo he decidido yo, sale solo de mantener la invariante.

Y hay un caso en el que no queda nada que la cargue: si la entrada tapa justo el descubierto,
el almacén se queda **sin existencias y valiendo 15 €**. No es un fallo, es la diferencia de
coste de lo que se sirvió sin tener, que sigue siendo real aunque ya no haya género donde
apoyarla. Un sistema con contabilidad la llevaría a una cuenta de resultados; Trasiego no
tiene contabilidad, así que la deja a la vista. Hay un test que fija ese comportamiento para
que sea una decisión y no una casualidad.

### El valor a una fecha es un group by

Como cada movimiento lleva su coste, sumar los movimientos hasta una fecha ya da lo que valía
el almacén ese día. No hace falta reconstruir capas ni reproducir nada. Eso es consecuencia
directa de la invariante, y es lo que hace que los informes de valoración a fecha sean
baratos.

Con una condición: que nadie meta después un movimiento con fecha anterior al corte. De eso
se encarga el cierre.

### El cierre va por almacén

Cerrar un almacén hasta un día fija que por debajo de esa fecha ya no se registra nada. Va
por almacén y no de golpe para todos porque aquí no hay contabilidad que obligue a un único
corte: cada almacén se inventaría cuando le toca, y cerrar el de la obra no tiene por qué
esperar a que alguien cuente el de la tienda.

Al cerrar se guarda lo que había, artículo a artículo. Es redundante — se puede volver a
calcular sumando movimientos — y se guarda **precisamente por eso**: `Comprobar` vuelve a
sumar y compara con lo declarado, así que un cierre que deja de cuadrar es la señal de que
alguien ha tocado el pasado por debajo de la fecha de cierre. Debería salir vacío siempre.

No hay reapertura. Un cierre que se puede deshacer no garantiza nada, y el día que haga falta
será una operación con su rastro, no un botón.

### Lo que llega tarde queda marcado

Un movimiento cuya fecha contable es anterior a algo que ya estaba registrado se marca como
retroactivo. No cambia cómo se valora: lo ya valorado no se revisa. Lo que dice es que la
valoración de ese artículo **no es la que saldría de recalcularla desde cero**, y eso hay que
poder verlo antes de fiarse de un informe.

Recalcular de verdad es lo siguiente, y ahora ya se puede: el cierre da el punto desde el que
empezar y el límite de hasta dónde se puede tocar.

### Reproducir el histórico para saber cuánto se aparta

`Recalculo.Reproducir` vuelve a valorar un histórico desde el último cierre, en el orden en
que los movimientos deberían haber llegado, y dice en cuánto se aparta cada salida de lo que
se registró en su día. No cambia nada: solo mira.

Las piezas son las mismas que usa el servicio: las mismas capas, el mismo consumo por
antigüedad, el mismo reparto de devoluciones. Lo único escrito dos veces es el orquestado —
una versión con persistencia y otra sin ella —, y de que no se separen se encarga un test que
reproduce históricos sin retroactivos y exige que salga **exactamente** lo mismo, hasta el
céntimo y en los dos criterios de valoración.

`Aplicar` sí toca: deshace todo lo que hay por encima del último cierre — las capas que
abrieron esos movimientos, lo que consumieron y lo que dejaron a deber —, devuelve las capas
anteriores a como estaban el día del cierre y lo reconstruye todo en orden, corrigiendo el
coste de las salidas que valoraran distinto. Por debajo del cierre no toca nada.

### Al cerrar se guarda el desglose, no solo el saldo

Que el saldo cuadre no basta para poder reproducir un histórico. El saldo dice cuánto había y
cuánto valía; en FIFO hace falta saber además **en cuántas capas estaba repartido**, porque eso
es lo que decide lo que cuesta la siguiente salida. Cinco unidades a 1 € y cinco a 10 € suman
lo mismo que diez a 5,50 €, pero la siguiente salida de cinco cuesta 5 € en un caso y 27,50 €
en el otro.

Así que el cierre guarda también una foto de las capas abiertas. Sin ella el recálculo daba un
número distinto y ningún test lo cogía, porque en todos los que había nunca hubo dos capas
vivas al cerrar.

Dos reglas salen de aquí, y las dos me parecen correctas por su cuenta:

- **No se cierra debiendo género.** Un descubierto vale lo que se supuso, y todavía puede
  resultar que costara otra cosa; cerrar sobre eso es cerrar sobre una suposición.
- **No se devuelve una salida de un periodo cerrado.** Una devolución toca los consumos de la
  salida original, y esos están congelados. Lo que vuelve se registra como una entrada normal,
  con el coste que le corresponda.

### Dos salidas a la vez no gastan el mismo género

Este es el equivalente aquí del problema que en Camar resolvió una constraint de PostgreSQL,
y la solución es la contraria. Dos salidas simultáneas del mismo artículo leen las mismas
capas, las dos descuentan sobre lo que leyeron, y la segunda escritura pisa a la primera: el
mismo género sale dos veces.

En Camar se podía delegar en la base de datos porque «dos reservas no se solapan» se puede
escribir como una restricción. Aquí no: «no consumas una capa que otro está consumiendo» no
es una condición sobre una fila, es sobre una operación entera. Así que va con concurrencia
optimista — una marca de versión en la capa, y si al guardar alguien se ha adelantado, se
repite **la operación entera**. Repetir solo el guardado no valdría: si otra salida se ha
llevado las existencias entre medias, hay que volver a mirar cuánto queda y de qué capas sale.

La marca de versión va como propiedad en la sombra, así que el dominio no se entera de que
existe.

Y hace falta esperar un poco entre intentos. Sin esa espera, los que chocan reintentan todos
a la vez y se vuelven a estorbar: con diez peticiones peleando por la misma capa se agotaban
los intentos sin que entrara nadie. El rato es distinto para cada uno, para que no vuelvan en
bloque.

Hay tests con diez peticiones simultáneas: sobre cinco unidades entran cinco y las otras
cinco se van con un aviso claro; sobre diez entran las diez y el almacén queda a cero exacto;
y con capas de coste distinto, cinco salen a 1 € y cinco a 9 €.

### Los errores los lee alguien de almacén

Los fallos de negocio salen como ProblemDetails con el mensaje entero, tal y como lo escribió
quien puso la regla. `No hay bastante DEMO-1 en CEN: quedan 6 ud y se piden 20.` sale con un
422, no con un 500 y un texto genérico.

Cada tipo de excepción tiene su código: `NoEncontrado` va a 404, `Conflicto` a 409 y
`ReglaDeNegocio` a 422. Y las `ArgumentException`, que nacieron para avisar de errores de
programación, en el borde de la API las provoca quien manda una cantidad negativa o una
referencia en blanco, así que ahí valen como 400.

Lo que entra y sale de la API va en `decimal`, no en `Cantidad` ni `Importe`. Esos tipos
existen para que dentro no se pueda operar mal con ellos; fuera solo estorbarían, porque
serializados serían un objeto con un campo dentro. Los enums sí viajan por su nombre: un
`"Fifo"` se entiende leyendo la respuesta y un `1` no, y además ata al cliente al orden en que
están declarados.

### El escritorio habla con la Api, no con la base de datos

Podría inyectarle los servicios de aplicación directamente y ahorrarse el salto HTTP, pero
entonces las pantallas sabrían de dónde salen los datos y no valdrían para la versión web. Así
son las mismas pantallas con el mismo cliente, y lo único que cambia es quién las aloja.

Por eso los contratos viven en su propio proyecto, compartido por la Api y por el cliente. Y
por eso el cliente lee el `detail` del ProblemDetails y lo enseña tal cual: esos mensajes se
escribieron pensando en quien los iba a leer, y reescribirlos en la interfaz sería tirarlos.

### El kardex

Es la pantalla principal, y es la invariante leída de arriba abajo en vez de de golpe: cada
movimiento con el saldo de cantidad y de valor que dejaba detrás. El saldo corrido no se
guarda en ningún sitio, se saca recorriendo los movimientos en el orden en que cuentan.

La hoja de estilos está escrita a mano en vez de traer una biblioteca de componentes. Esto es
una herramienta de almacén: lo que tiene que hacer bien es enseñar muchas filas de números y
que se lean de un vistazo. Las cifras van a la derecha y con los dígitos del mismo ancho, que
es lo que deja comparar una columna sin ir leyéndola; lo que está en negativo va en rojo, y
los movimientos que llegaron tarde llevan su marca.

### El saldo lleva signo y la cantidad no

Desde el principio dije que una `Cantidad` es una magnitud y nunca es negativa, y que el
saldo de un almacén sí puede serlo. Aquí es donde hizo falta el tipo aparte: `Saldo` lleva
signo, sabe si está en descubierto, y su `Disponible` es cero cuando lo está. Una cantidad
sigue sin poder ser negativa, así que una capa tampoco.

## Por dónde va

Hecho:

- **Fase 0 · Andamiaje.** Solución, capas, conexión a SQL Server, primera migración,
  `Cantidad` e `Importe` con sus reglas de redondeo, artículos y almacenes.
- **Fase 1 · Movimientos sin valorar.** Entradas y salidas con fecha contable y momento de
  registro separados, saldo de cantidades y saldo a fecha.
- **Fase 2 · Valoración FIFO.** Capas de existencias, consumo por antigüedad contable, y la
  invariante comprobada movimiento a movimiento en los tests.

Lo que viene, en orden:

1. Precio medio ponderado conviviendo con FIFO
3. Los casos feos: movimientos retroactivos, devoluciones al coste original, regularizaciones,
   stock negativo
4. Concurrencia sobre las capas y cierre de periodo
5. API
6. Escritorio en Blazor, con el kardex como pantalla principal
7. Informes de valoración a fecha

Las cuatro primeras de esa lista son el proyecto de verdad; el resto es lo que hace falta
para poder verlas.
