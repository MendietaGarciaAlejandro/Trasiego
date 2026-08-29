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
- Scalar para probar la API a mano (`/scalar` en desarrollo)
- Más adelante, Blazor: primero escritorio con BlazorWebView, luego web con los mismos componentes

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

`dotnet test` ejecuta todo. Los tests de dominio no tocan la base de datos y son
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

### De momento no se deja bajar de cero

Una salida que dejaría el saldo en negativo se rechaza, y el aviso dice cuánto queda. Es lo
que hace un ERP por defecto. Permitirlo es uno de los casos de la fase 4, y entonces será una
opción del almacén, no la norma.

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
