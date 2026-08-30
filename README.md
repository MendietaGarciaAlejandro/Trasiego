# Trasiego

[![Pruebas](https://github.com/MendietaGarciaAlejandro/Trasiego/actions/workflows/pruebas.yml/badge.svg)](https://github.com/MendietaGarciaAlejandro/Trasiego/actions/workflows/pruebas.yml)

ERP de inventario con valoración. Un trasiego es pasar algo de un recipiente a otro, que es lo
que hace un movimiento de existencias.

No pretende abarcar mucho, sino ir hondo en una cosa: que el **valor** del almacén cuadre
siempre, incluso cuando alguien mete un movimiento con fecha del mes pasado, devuelve material
que compró a otro precio, o dos personas descuentan del mismo stock a la vez.

> El valor de un almacén es siempre igual a la suma de sus movimientos valorados.

Todo lo demás está para que esa frase siga siendo verdad.

## Cómo se ve

Al arrancar en desarrollo se siembra un almacén con dos meses de historia, así que esto sale
sin teclear nada.

![Ficha de artículo](docs/kardex.png)

Los tornillos entraron a 2 € y luego a 2,60. La salida del 31 de julio agota la primera
entrada y sigue por la segunda: 90 unidades cuestan 192 €, no 234. La del 9 de agosto llegó
con fecha anterior a movimientos ya registrados y va marcada como **tarde**.

![Lotes y caducidades](docs/lotes.png)

En el orden en que va a ir saliendo. Del primer lote quedaron cuatro botes vencidos: siguen
contando y siguen valiendo dinero, pero no se sirven.

![Valoración a fecha](docs/valoracion.png)

Lo que valía un almacén un día concreto. No reconstruye nada: es sumar movimientos.

![Documentos](docs/documentos.png)

Un albarán. En borrador no ha movido nada; al registrarlo genera sus movimientos de una vez, o
no genera ninguno.

## Stack

- .NET 10, capas separadas (Dominio, Aplicación, Infraestructura, Api)
- SQL Server 2022 y EF Core 10
- JWT y BCrypt
- xUnit; los tests de integración sobre LocalDB
- ASP.NET Core con controllers, y Scalar en `/scalar` para probarla a mano
- Blazor en una biblioteca aparte, con dos hosts: WPF con `BlazorWebView` y WebAssembly

## Cómo levantarlo

Hace falta SQL Server (vale Developer Edition) y LocalDB para los tests. Docker no.

```bash
dotnet tool restore
dotnet user-secrets set "Jwt:Clave" "una clave larga y aleatoria" --project src/Trasiego.Api
dotnet ef database update --project src/Trasiego.Infraestructura --startup-project src/Trasiego.Api
dotnet run --project src/Trasiego.Api
```

Eso levanta la Api y el cliente web en `http://localhost:5248`. Para el escritorio, con la Api
ya arriba:

```bash
dotnet run --project src/Trasiego.Escritorio
```

Se siembran dos usuarios, los dos con la contraseña `trasiego-demo-2026`:
`encargada@trasiego.test` (responsable) y `operario@trasiego.test`. Y el almacén de las
capturas: cuatro artículos, tres almacenes, capas a dos precios, un traspaso, una devolución,
un descubierto tapado, tres lotes con caducidades escalonadas, un albarán y un cierre. Se
siembra por los servicios de siempre, no metiendo filas a mano: por debajo, las capas valdrían
lo que yo creo en vez de lo que valen.

La clave de firma va en user-secrets; sin ella la Api se niega a arrancar y dice el comando. La
cadena de conexión no, porque va a `localhost` con autenticación integrada y no lleva ningún
secreto.

`dotnet test` ejecuta unos 187 tests. En cada push corre lo mismo en GitHub Actions sobre
Windows, que es donde están a la vez el WPF y el SQL Server. Antes de los tests comprueba que
el modelo y las migraciones dicen lo mismo: es el despiste fácil, porque los tests pasan contra
la base de datos que ya tenías creada y el fallo aparece en el despliegue.

## Alcance

Lo que hace:

- Artículos, almacenes y movimientos
- Valoración FIFO y precio medio ponderado, conviviendo
- Lotes con caducidad
- Kardex con saldo corrido de cantidad y de valor
- Valoración a una fecha
- Cierre de periodo

Lo que no: multiempresa, contabilidad, compras y ventas, números de serie y ubicaciones
dentro del almacén.

## Decisiones

### El dinero

Los importes se guardan con **cuatro decimales** y se enseñan con dos. Tres unidades de 10,00 €
salen a 3,3333 cada una; redondear a dos antes de seguir operando descuadra el almacén a los
pocos movimientos. El redondeo es comercial, no el bancario de .NET: 0,125 son 0,13.

No se guarda ningún coste unitario. Una capa guarda cantidad y valor total; el unitario se
calcula al enseñarlo y se devuelve como `decimal` y no como `Importe`, para que no apetezca
operar con él.

Lo que queda es una resta, nunca otro cálculo. Un tercio de 10,00 € es 3,3333, y tres
tercios calculados por separado suman 9,9999. Hay un test para cada mitad.

Y una entrada pide lo que costó entera, no el precio por unidad: multiplicar por la
cantidad daría un total que ya no cuadra con la factura.

### Cantidad y saldo son tipos distintos

`Cantidad` es una magnitud y nunca es negativa; el signo lo pone el tipo de movimiento. Restar
de más lanza en vez de devolver un negativo, así que una capa no puede quedarse por debajo de
cero.

`Saldo` sí lleva signo, porque en la vida real se sirve mercancía antes de registrar la compra.

### Fecha contable y momento de registro

Cada movimiento lleva dos fechas: el día al que pertenece y el instante en que se tecleó. Son
cosas distintas en cuanto hay un movimiento retroactivo, y mezclarlas hace imposible explicar
por qué un informe de ayer da hoy otro número.

Manda la contable. Un albarán traspapelado con fecha de la semana pasada es más antiguo que
otro tecleado ayer, y en FIFO sale antes.

### El saldo no se guarda

No hay tabla de existencias: el saldo es la suma de los movimientos, que es la invariante
convertida en consulta. Una tabla aparte sería más rápida y sería otra cosa que puede dejar de
cuadrar.

Esa suma va en SQL a mano. `Cantidad` se persiste con un `ValueConverter`, y EF sabe
convertirla pero no sumarla: agregarla en LINQ significaría traerse todos los movimientos a
memoria.

### FIFO no se calcula, se guarda

Cada entrada abre una **capa** con su cantidad y su coste. Una salida va vaciando capas hasta
cubrir lo que se pide, y su coste es la suma de lo que salió de cada una: no lo teclea nadie.

Cada salida deja además una fila por capa de la que sacó algo. Sin eso, el coste de una salida
es un número sin explicación y no hay forma de devolver material al precio al que entró.

### Precio medio es FIFO con una sola capa

FIFO abre una capa por entrada; precio medio mete todas en la que ya estaba abierta. Sacar una
parte proporcional de esa capa única **es** la media ponderada.

Así que lo que cambia entre un criterio y otro es la entrada, no la salida: el mismo código
consume capas en los dos casos, porque a precio medio recorrerlas es recorrer una.

El criterio va por artículo, no por almacén: mover material de sitio no puede cambiar lo que
vale. Y no se toca una vez hay movimientos.

### Lo devuelto vuelve al coste al que salió

Para esto está la tabla de consumos. Diez unidades que costaron 1 € vuelven valiendo 1 €,
aunque entre medias haya entrado material a 8 €.

Dónde cae sí depende del criterio: en FIFO cada trozo repone su capa, a precio medio entra en
la abierta y rehace la media. Devolver a plazos tampoco pierde céntimos, porque cada consumo
lleva cuánto se ha devuelto ya.

### Un recuento por encima entra al precio de lo que había

Si aparecen dos unidades de más, valen lo mismo que las demás y el valor unitario del almacén
no se mueve. Es el único sitio del proyecto donde se usa un coste unitario para calcular algo.

Si no había existencias no hay precio del que tirar, y la operación se rechaza en vez de
inventarse uno.

### Bajar de cero: decisión del almacén

Por defecto una salida que dejaría el saldo en negativo se rechaza. Un almacén puede marcarse
como que admite descubierto: una obra gasta material que aún no se ha dado de alta, una tienda
no.

Lo que sale sin estar se valora al último precio conocido. El descubierto se guarda: es lo
contrario de una capa, una deuda de existencias que resta del valor del almacén, y lo
primero que hace la siguiente entrada es taparla.

Si esa entrada costó otra cosa de lo que se supuso, hay una diferencia que tiene que quedar en
algún sitio. Como lo ya valorado no se revisa, **la carga lo que quede**: cinco unidades que
salieron a 2 € y llegaron a 4 € dejan tres unidades valiendo 22 € en vez de 12 €.

Y si la entrada tapa justo el descubierto, el almacén queda **sin existencias y valiendo
15 €**. No es un fallo, es esa misma diferencia sin género donde apoyarse. Con contabilidad
iría a una cuenta de resultados; aquí se queda a la vista, con un test que lo fija.

### El valor a una fecha es un `GROUP BY`

Como cada movimiento lleva su coste, sumarlos hasta una fecha ya da lo que valía el almacén ese
día. Ni capas ni reconstrucciones. Con una condición: que nadie meta después algo con fecha
anterior al corte, y de eso se encarga el cierre.

Un artículo a cero de cantidad y de valor no sale en el informe. A cero de cantidad **pero con
valor** sí: es la diferencia que deja un descubierto, y esconderla sería mentir sobre el total.

### El cierre va por almacén

Cada almacén se inventaría cuando le toca; cerrar el de la obra no espera a que cuenten el de
la tienda.

Al cerrar se guarda lo que había. Es redundante y por eso mismo útil: `Comprobar` vuelve a
sumar y compara, así que un cierre que deja de cuadrar delata que alguien tocó el pasado.

Se guarda también una **foto de las capas**, no solo el saldo. Cinco unidades a 1 € y cinco a
10 € suman lo mismo que diez a 5,50 €, pero la siguiente salida de cinco cuesta 5 € o 27,50 €
según cómo estuviera repartido. Sin esa foto el recálculo daba otro número y ningún test lo
cogía.

De ahí salen dos reglas: no se cierra debiendo género (un descubierto vale lo que se supuso)
y no se devuelve una salida de un periodo cerrado (tocaría consumos congelados).

No hay reapertura. Un cierre que se deshace no garantiza nada.

### Lo que llega tarde queda marcado

Un movimiento con fecha anterior a algo ya registrado se marca como retroactivo. No cambia cómo
se valora, pero avisa de que la valoración de ese artículo no es la que saldría de recalcularla
desde cero.

### Recálculo

`Reproducir` vuelve a valorar un histórico desde el último cierre y dice en cuánto se aparta
cada coste derivado. No toca nada. `Aplicar` sí: deshace lo que hay por encima del cierre,
devuelve las capas a la foto y lo reconstruye en orden.

Las piezas son las del servicio: las mismas capas, el mismo consumo, el mismo reparto de
devoluciones. Lo único escrito dos veces es el orquestado, y un test reproduce históricos sin
retroactivos exigiendo que salga exactamente lo mismo, al céntimo y en los dos criterios.

Si al rehacer un almacén cambia el coste de una salida traspasada, se pone al día el destino y
se rehace también, y así hasta que deja de moverse nada. No hace falta ordenar los almacenes ni
preocuparse por los ciclos: **un traspaso siempre se alimenta de una salida anterior**, así que
los costes van hacia delante en el tiempo y la cadena se acaba sola.

### Dos salidas a la vez no gastan el mismo género

Dos salidas simultáneas leen las mismas capas, las dos descuentan sobre lo que leyeron y la
segunda escritura pisa a la primera. Va con concurrencia optimista: marca de versión en la capa
—propiedad en la sombra, el dominio no se entera— y, si alguien se adelantó, se repite la
operación entera. Repetir solo el guardado no vale: hay que volver a mirar cuánto queda y de
qué capas sale.

Con espera aleatoria entre intentos. Sin ella, los que chocan reintentan a la vez y se vuelven
a estorbar: con diez peticiones peleando por la misma capa se agotaban los intentos sin que
entrara nadie.

Hay tests con diez peticiones simultáneas: sobre cinco unidades entran cinco, sobre diez entran
las diez y el almacén queda a cero exacto, y con capas de distinto coste cinco salen a 1 € y
cinco a 9 €.

## Lotes y caducidades

### FEFO es FIFO con una línea más

Una capa ya era casi un lote: lo que queda de una entrada concreta, con su coste y su fecha.
Solo le faltaban el número y hasta cuándo vale.

```csharp
.OrderBy(capa => capa.Caducidad ?? DateOnly.MaxValue)
.ThenBy(capa => capa.FechaContable)
.ThenBy(capa => capa.MomentoDeRegistro)
```

Un artículo sin lotes no tiene caducidades, así que el primer criterio no desempata nada y
queda el orden de siempre. Lo que no caduca va al final. La caducidad se mira al día contable
del movimiento, no a hoy.

### Lo caducado no se sirve, pero sigue estando

Sigue contando en el saldo y sigue valiendo dinero, porque está ahí. La única forma de darlo de
baja es un recuento, y como el orden es por caducidad, el recuento se lo lleva primero sin que
nadie se lo pida.

### Al recibir se declara el lote; al servir se pide

Una entrada dice de qué lote llega, y es obligatorio. Una salida dice de cuál servir, y es
opcional: lo normal es callarse y que salga lo que antes caduque.

Pedirlo es para una retirada de producto o para el cliente que exige el mismo lote de siempre.
Vale en salidas, traspasos y líneas de albarán. Pedir un lote por su nombre **no lo hace
apto**: si está caducado sigue sin servirse.

### Un artículo con lotes no admite descubierto

No se sirve un lote que no se tiene: no habría número que poner en el albarán. De esta regla
depende la siguiente.

### Con lotes no hay nada que recalcular

El recálculo existe porque, sin lotes, de qué capa sale cada cosa **no es un hecho sino un
convenio**: diez tornillos de enero y diez de marzo son indistinguibles, y decimos que salieron
los de enero porque lo dice FIFO. Cuando aparece un albarán atrasado, el convenio cambia de
opinión.

Con lotes son cajas distintas y salió una concreta, y quedó apuntado. Reproducir el histórico
daría la versión que *habría tocado* según FEFO, y sería falsa. Además, como no admiten
descubierto, ninguna salida espera a una entrada posterior que la revalorice: los costes
registrados ya son los definitivos.

### Precio medio y lotes son incompatibles

A precio medio todas las entradas caen en la misma capa, y esa capa es lo que distingue un lote
de otro. Se podría arreglar separando lote y coste en dos entidades, pero entonces deja de ser
verdad que precio medio es FIFO con una sola capa, que es la frase sobre la que se apoya medio
proyecto. Es una decisión, no un muro.

### El traspaso lleva los lotes

Una salida de ocho unidades que vacía dos lotes abre dos capas en el destino, cada una con
su número, su caducidad y su parte del coste.

## La aplicación

### Un documento agrupa lo que llegó junto

Un albarán de doce líneas eran doce movimientos sueltos con el número escrito a mano en un
campo de texto. No es una compra: no hay proveedor, ni tarifas, ni impuestos.

Nace en borrador, se le ponen líneas y al registrarlo genera sus movimientos todos de una
vez: la mercancía llegó junta y no tiene sentido que la sexta línea falle y las cinco
primeras entren. Registrado ya no se toca; lo que haya que corregir se corrige con otro
movimiento.

### Un traspaso no es una salida y una entrada sueltas

Si fueran dos movimientos, el coste de la entrada lo teclearía alguien, y mover algo de sitio
no puede cambiar lo que vale. El coste es el que sale del origen. Las dos mitades van atadas y
se confirman de una vez, para que no quede mercancía que salió de un almacén y no llegó a
ninguno.

### Los errores los lee alguien de almacén

Los fallos de negocio salen como ProblemDetails con el mensaje entero:
`No hay bastante DEMO-1 en CEN: quedan 6 ud y se piden 20.`, con un 422 y no con un 500 y un
texto genérico. `NoEncontrado` va a 404, `Conflicto` a 409, `ReglaDeNegocio` a 422, y las
`ArgumentException` a 400, porque en el borde las provoca quien manda una cantidad negativa.

Por la Api viajan `decimal`, no `Cantidad` ni `Importe`: esos tipos existen para que dentro no
se pueda operar mal, fuera serían un objeto con un campo. Los enums sí van por su nombre.

### Los clientes hablan con la Api

Al escritorio podría inyectarle los servicios y ahorrarme el salto HTTP, pero entonces las
pantallas sabrían de dónde salen los datos y no valdrían para la web. La apuesta se cobró
sola: la versión web son **las mismas pantallas y el mismo cliente**, y solo cambia quién las
aloja. No hubo que tocar ninguna.

La sirve la propia Api, así que es el mismo origen y no hace falta CORS.

El CSS está escrito a mano. Esto es una herramienta de almacén: lo que tiene que hacer bien es
enseñar muchas filas de números y que se lean de un vistazo. Cifras a la derecha, dígitos del
mismo ancho, negativos en rojo.

### Quién hizo cada movimiento

Cada movimiento se queda con quién lo registró, sacado del token y de ningún otro sitio: si
viajara en el cuerpo, firmaría quien dijera el que la manda.

No va como parámetro de cada método sino detrás de una interfaz de una propiedad. Quién teclea
no es un dato de la entrada, es el contexto de la petición; como argumento habría que
arrastrarlo por las ocho operaciones y hasta dentro de las mitades de un traspaso.

A un usuario con movimientos la base de datos no le deja borrarse. Ojo con una trampa que
encontré probándolo: si el movimiento está cargado en el mismo contexto, EF le quita la firma
antes de intentar el borrado y entonces sí pasa. El test lo hace desde un contexto limpio.

### Roles

Un **operario** mueve mercancía y consulta. Un **responsable** además cuadra inventarios,
cierra periodos, recalcula y toca el catálogo: son las operaciones de las que no se vuelve.

Quien manda es la Api, que responde 403; que la interfaz no le enseñe al operario botones que
no funcionan es cortesía, no seguridad.

Entrar con un correo que no existe da el mismo aviso que entrar con la contraseña cambiada. Si
fueran distintos, probando correos se sabría cuáles están dados de alta.

### La sesión

El **token de acceso** dura quince minutos y vive en memoria. La **renovación** dura una semana
y va en una cookie `HttpOnly`, que es lo que la hace mejor que guardar el JWT en el
almacenamiento del navegador. No sale nunca por el cuerpo de una respuesta.

Cada renovación gasta la anterior y emite otra. Una gastada que reaparece significa que alguien
tiene una copia, así que se tiran todas las de ese usuario. De la renovación solo se guarda su
huella, con SHA-256 y no BCrypt: BCrypt va lento a propósito porque una contraseña se puede
adivinar, y esto son treinta y dos bytes de azar.

En el escritorio no hay navegador que guarde la cookie, así que el host de WPF la deja en el
administrador de credenciales de Windows: la cifra con la cuenta de quien ha entrado y la
enseña en el panel del sistema, para poder olvidarla sin abrir Trasiego.

Un proceso tira cada doce horas las caducadas, porque si no la tabla solo crece. Se borran solo
por fecha aunque estén gastadas: una gastada sigue haciendo falta mientras pueda presentarse,
porque es lo que delata la copia.

## Por dónde ha ido

El plan eran doce fases y salieron diez cosas más por el camino.

1. **Andamiaje.** Capas, SQL Server, `Cantidad` e `Importe`.
2. **Movimientos sin valorar.** Las dos fechas y el saldo.
3. **Valoración FIFO.** Capas y la invariante comprobada movimiento a movimiento.
4. **Precio medio.** Que resultó ser FIFO con una sola capa.
5. **Devoluciones y regularizaciones.**
6. **Stock negativo.** Descubierto por almacén.
7. **Cierre de periodo.**
8. **Recálculo que compara.**
9. **Recálculo que se aplica.** Foto de las capas al cerrar.
10. **Concurrencia.** Marca de versión y reintento con espera.
11. **API.** Controllers y ProblemDetails.
12. **Escritorio.** WPF con BlazorWebView.
13. **Informes.** Valoración a fecha, cierres y recálculo.
14. **Versión web.** WebAssembly con las mismas pantallas.
15. **Traspasos entre almacenes.**
16. **Autenticación.** JWT, dos roles y sesión que aguanta.
17. **Documentos.** Albaranes que se registran enteros o no se registran.
18. **Quién.** La firma de cada movimiento.
19. **Integración continua.**
20. **Lotes y caducidades.** FEFO.
21. **Que se vea.** Almacén de demostración y capturas.
22. **Servir un lote concreto.**

Las fases 3 a 9 son el proyecto; el resto es lo que hace falta para poder verlo.

## Siguientes pasos

Ninguno, y es a propósito. Lo que queda ya no es Trasiego: multiempresa, contabilidad, compras
y ventas, ubicaciones. Están fuera del alcance desde la primera línea y meterlos convertiría un
proyecto con una idea clara en un ERP genérico a medio hacer.

Si esto se usara de verdad, lo primero sería medir: el saldo suma movimientos y ese `GROUP BY`
crece con el histórico. La tabla de existencias está descartada arriba por una razón, así que
antes de meterla haría falta el número que la justifique.
