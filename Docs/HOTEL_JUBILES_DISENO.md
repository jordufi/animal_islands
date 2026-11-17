# Hotel Jubiles: diseño para construirlo (2026-11-17, Opus)

Quinto mapa. Las decisiones del usuario están en `Docs/mapas.md`, sección 6; aquí se convierten en plano, números y
trabajo. **Estado:**
- **Hecho:** el plano en `Tools/Maps/make_hotel_layout.py`. Pasa las comprobaciones de rejilla
  (`python Tools/Maps/make_hotel_layout.py --check` imprime las cinco plantas; con `--png` las dibuja en
  `Renders/Plano_Hotel.png`).
- **Sin hacer:** todo lo de Unity, que necesita antes las plantas reales (`Docs/PLANTAS_FORMATO.md`).
- **Reglas de siempre:** números en `Assets/Data`, probar jugando antes de dar algo por hecho, no tocar scripts con una
  prueba en marcha, commit solo cuando lo pida el usuario. **Los abuelos siempre pueden seguir tu ruta.**

## 1. Ficha
| | |
|---|---|
| Idea | hotel de los 70 en Benidorm, noche de tormenta de verano, guiños a El Resplandor |
| Plantas | Sótano (−4 m) · Planta baja (0) · 1ª planta (4 m) · 2ª planta (8 m) · Azotea (12 m) |
| Tamaño | 1165 celdas = 4660 m² (Villajubilada 998, La Serenísima 1372) |
| Inicio | Recepción, **a oscuras** |
| Momento que cambia la partida | **dar la luz** en las calderas, a dos puertas del inicio |
| Evento | **noche de bingo**: la ronda siguiente a abrir el Salón Dorado, y desde ahí cada 5 rondas |
| Zona ancha / estrecha | Piscina y jardines / los pasillos con moqueta de las plantas |
| Jefe | Don Anselmo en albornoz, sale del ático a hachazos |
| Salida | helicóptero en la azotea |

## 2. Zonas
| Letra | Zona | Planta | m² | Qué es | Premios |
|---|---|---|---|---|---|
| `a` | Recepción (inicio) | baja | 416 | vestíbulo con columnas, mostrador, sofás, araña, puerta giratoria, los dos ascensores, la escalera principal | teléfono `C`, caja |
| `f` | Bufé libre | baja | 288 | mesas del bufé y la cocina, con la escalera de servicio al sótano | cafetera `S`, bebida, Lentos-peta `G`, caja (`firstbox`) |
| `c` | Sótano | sótano | 832 | calderas, lavandería (salida de las tolvas) y garaje (rampa a la terraza) | **la luz `E`**, albornoz `T`, La Thermomix `M`, caja ×2, suelo mojado |
| `p` | Piscina y jardines | baja | 1100 | terraza de la piscina, terraza este y laberinto de setos | bar de la piscina, caja ×2 (`firstbox`) |
| `d` | Salón Dorado | baja | 364 | pista de baile, mesas, tarima del animador y la barra del barman fantasma; **el bingo** | bebida (barman), caja |
| `m` | Primera planta | 1ª | 396 | pasillo en anillo + pasillo central, habitaciones abiertas, **el gimnasio** (cruza el bloque interior: un lazo más) | 2 escondites de la pulsera `O`, minibar, La Chancla `U`, caja ×2, suelo mojado, tolva |
| `n` | Segunda planta | 2ª | 444 | igual, con la escalera de la azotea y **un balcón corrido** por la fachada este, sobre la piscina (un lazo al aire libre, con la tormenta) | **Microondas `P`** (cuarto del conserje), piloto `F`, interruptor del helipuerto `E`, escondite `O`, minibar, caja ×2, suelo mojado, tolva, montacargas |
| `r` | Azotea | azotea | 820 | helipuerto, ático, letrero de neón, depósitos, aparatos de aire | jefe `b`, helicóptero `X` |

## 3. Puertas
| N.º | Puerta | Precio | Une |
|---|---|---|---|
| 1 | Puerta del comedor | 750 | Recepción – Bufé |
| 2 | Escalera de las cocinas | 1000 | Bufé – Sótano |
| 3 | Puertas de la piscina | 1000 | Recepción – Piscina |
| 4 | Rampa del garaje | 1250 | Sótano – Piscina |
| 5 | Salón Dorado (terraza) | 1250 | Piscina – Salón |
| 6 | Salón Dorado (bufé) | 1250 | Bufé – Salón |
| 7 | Escalera principal | 1250 | Recepción – 1ª planta (en el rellano de abajo) |
| 8 | Escalera de la 2ª planta | 1500 | 1ª – 2ª |
| 9 | AZOTEA - JEFE FINAL | misión + pulsera de puertas | 2ª – Azotea (arriba de la escalera) |

**Lazos:**
- **Recepción → Bufé → Sótano → Piscina → Recepción.** Un anillo que pasa por dos plantas: a las calderas se llega por
  la cocina (puertas 1 + 2, 1750) o por el garaje (3 + 4, 2250).
- **El Salón Dorado** queda entre el bufé y la terraza.
- **Las plantas altas** van en vertical: escalera (7, 8, 9) y ascensores. En cada planta, el anillo y el pasillo
  central forman dos lazos.

**Zonas con una sola salida:** ninguna salvo la azotea (la del jefe). Recepción tiene tres.

## 4. Cómo va una partida
1. **A oscuras (rondas 1-3 o 4).**
   - Solo linterna. No funcionan bebidas, Microondas ni ascensores.
   - Hay sonámbulos, relámpagos y siluetas tras las ventanas.
   - Hay que juntar 1750 conchas para cocina + escalera, o 2250 para terraza + garaje, y bajar al sótano.
2. **Dar la luz.**
   - Sale la ola de sangría de los ascensores del vestíbulo y el hotel se enciende por partes.
   - Desde ahí funcionan los ascensores (sótano, baja, 1ª y 2ª) y los minibares.
3. **Abrir el Salón Dorado.** La ronda siguiente es la primera noche de bingo, y después cada 5 rondas.
4. **La misión:**
   - Encontrar la pulsera de puertas (en una de tres habitaciones).
   - Llevarle el café al piloto, en la 2ª.
   - Encender las luces del helipuerto.
   - Llamar desde recepción.
   - Aguantar hasta el final de la ronda siguiente. Entonces la puerta 9 se abre gratis, pero quien la abre tiene que
     llevar la pulsera.
5. **Azotea:** Don Anselmo sale del ático a hachazos y la pelea es con apagones intermitentes. Al vencerlo, al
   helicóptero. Si sales con el albornoz del hotel, ×2.

## 5. Reglas nuevas (números a `Assets/Data`)
### 5.1 Apagón inicial y luz (`Zones/HotelPower` o `BoatMission` con sabor "hotel")
- **Hasta dar la luz:**
  - Ambiente casi negro y linterna encendida (la de `DayNightCycle`, ya existe).
  - Encendidos solo los carteles verdes de SALIDA (emisivos) y una luz roja de emergencia por escalera, que parpadea.
- **Relámpagos:**
  - Cada 20-40 s (`HotelData.lightningEvery`), un destello de 0,15 s que sube la luz direccional y la exposición del
    cielo. Por las ventanas se ven siluetas.
  - En la planta baja, alguna vez es un abuelo de verdad que entra después por esa ventana: una entrada de abuelos.
- **Gafas que brillan:** cada abuelo lleva un emisivo pequeño en las gafas, visible a oscuras.
- **Interruptores:**
  - El `E` del sótano es **la luz**: encienden todas las zonas, como `PowerSwitch`.
  - El `E` de la 2ª planta enciende **el helipuerto** (tarea 4): en esa zona es un interruptor de misión, no de luz.
- **Al dar la luz:**
  - Ola de sangría desde los dos ascensores de recepción: una lámina que avanza 8 m en 1,5 s y deja el suelo rojo
    unos segundos (×0,7 de velocidad dentro, `HotelData.sangriaSlow`).
  - Se encienden por grupos, con 0,3 s entre uno y otro: la araña, las lámparas de pared, la piscina, el neón y la
    bola de espejos.

### 5.2 Ascensores, montacargas y tolvas (`Zones/Lift`, `Zones/FloorLink`, `Docs/PLANTAS_FORMATO.md` 5)
- **Ascensores A y B:**
  - Paran en sótano, baja, 1ª y 2ª. No llegan a la azotea, a la que solo se sube por la escalera de la puerta 9.
  - Necesitan luz. 2,5 s por planta y solo paran en zonas abiertas.
  - Cada parada es una entrada de abuelos.
- **Montacargas:**
  - Sótano ↔ 2ª, solo para quien lleva la pulsera de puertas. Es el atajo para el albornoz y para el café.
  - Sin luz no funciona.
- **Tolvas de la lavandería:**
  - Desde el cuarto de la ropa de la 1ª y de la 2ª hasta la lavandería.
  - Atascadas mientras el sótano esté cerrado.
  - Los abuelos también se tiran.

### 5.3 Noche de bingo (`Rounds/BingoNight` + `BingoData`)
- **Cuándo:** en la ronda siguiente a abrir el Salón Dorado (puerta 5 o 6), y luego cada 5 rondas. No sustituye a la
  ronda: los abuelos son los normales de esa ronda, con número.
- **La luz de esa noche:** penumbra morada en el hotel, un foco sobre el animador, y los números de los abuelos
  brillan para que se lean.
- **El cartón:**
  - Cada jugador recibe uno de **9 números** (3 líneas de 3; del 1 al 90), que se ve en pantalla.
  - En solitario, unos 25 abuelos en la ronda 7: con 15 números no daría tiempo.
- **Los números de los abuelos:**
  - Cada abuelo lleva un número colgado del cuello.
  - El 70 % lleva un número que está en algún cartón sin tachar (`BingoData.cardShare`); el resto, al azar.
  - El número sale de `EnemyHealth.NetId` y de la semilla de la partida, así que es el mismo en todas las máquinas.
- **Tachar:** quien mata a un abuelo con un número de **su** cartón lo tacha.
- **Premios:**
  - **Línea:** cae un power-up a sus pies.
  - **Bingo:** el bote, 2000 + 500 por cada noche de bingo anterior (`potBase`, `potPerNight`).
- **Reto de la noche:**
  - Lo anuncia el animador, uno al azar de la lista de `Docs/mapas.md` (los 10, aprobados).
  - Quien cante bingo cumpliéndolo gana la **pulsera todo incluido**: bebidas gratis solo para él, el resto de la
    partida (`PlayerDrinks.freeDrinks`). No se pierde al caer.
  - Si ya la tiene, el reto le da el bote ×2.
- **Lo que hay que llevar contado de cada jugador en la ronda:**
  - Arma de cada baja (ukelele / pistola del principio / cualquier otra).
  - Tiros a la cabeza.
  - Daño recibido.
  - Si sale del Salón.
  - Conchas gastadas.
  - Orden de las tachaduras.
  - Si cae alguien del equipo.
  - Y para "el abuelo" (el 90 gigante) y "la niña bonita" (el 15 que huye), quién lo mata y con qué.
- **El animador:** una figura en la tarima con frases en la megafonía y la jerga del bingo ("¡el 22, los dos
  patitos!"). El texto, en `BingoData.calls`.

### 5.4 Las pulseras
- **Pulsera de puertas** (`Zones/DoorBracelet`, parecida a `MasterKey`):
  - Está escondida en una de las tres `O`; el anfitrión elige cuál y la manda en el estado de la misión.
  - Va en el bolsillo, con las manos libres, y se ve en pantalla.
  - Abre las puertas `$`, el montacargas y la puerta 9.
  - Si quien la lleva cae, se pierde: vuelve a esconderse en otra `O` distinta y hay que buscarla otra vez.
- **Pulsera todo incluido:** la del bingo (5.3).
- La **carta** que antes se llamaba así ahora es "Media pensión".

### 5.5 Trampa: suelo mojado (`Zones/TracaTrap` generalizada, o `MopTrap`)
- **Dónde:** un poste `%` por zona (sótano, 1ª y 2ª), y cubre el pasillo en el que está.
- **Precio y efecto:**
  - 750, con 45 s de espera.
  - Los abuelos que pisan el tramo resbalan: 2,5 s tirados y 300 de daño (los de las primeras rondas mueren).
  - **Las bajas pagan** como una baja normal (decisión del usuario para la traca, aplicada igual).
- **Jugador que lo pisa:** 0,6 s de tropiezo, sin daño.
- **Números:** en `EconomyData` (`mop*`), como los de la traca.

### 5.6 Abuelos nuevos (`EnemyVariant`, solo en esta escena)
| Variante | Desde | Qué hace | Cómo se le gana |
|---|---|---|---|
| **El sonámbulo** (pijama, gorro de dormir) | solo a oscuras: el acto 1 y los apagones del jefe | camina dormido a 1,2 m/s y no ataca; despierta si lo enfocas con la linterna 1,5 s a menos de 12 m o le das sin matarlo, y entonces carga a 6,5 m/s y pega 45 | pasar mirando a otra parte; matarlo dormido paga ×2 |
| **El de la moto de movilidad** (el triciclo) | ronda 6 | 8 m/s en recta, gira a 60°/s, no entra en el área «Escalera» (se queda en su planta); 50 de daño y empujón | meterse en el hueco de una puerta y dispararle al pasar |
| **La de la bandeja del bufé** | ronda 4 | la bandeja para todo lo que le llega de frente (cono de 120°) | piernas, rodearla, ukelele |
| **Las gemelas** (vestido azul, de la mano) | ronda 9, siempre en pareja | van juntas; si muere una, la otra ×1,5 velocidad y daño | separarlas o matarlas a la vez |

### 5.7 Jefe: Don Anselmo en albornoz
- **La entrada:** al abrirse la azotea, revienta la puerta del ático a hachazos ("¡Aquí está Anselmo!").
- **El combate:**
  - Cada 25 s corta la luz 8 s: solo linterna, y salen sonámbulos.
  - Vida con la fórmula de siempre (`EconomyData`).
- **El aspecto** (`Boss/AlbornozLook`, como `PregoneroLook`): albornoz, gorro de ducha y hacha.

### 5.8 Misión: "Tareas del check-out"
1. **Dar la luz** (calderas).
2. **Encontrar la pulsera de puertas.**
3. **Llevarle el café al piloto:**
   - La cafetera está en el bufé; el piloto, en su habitación de la 2ª.
   - El café se lleva en brazos como el disco: no deja disparar y se cae si te pegan.
   - Si se cae, se enfría a los 20 s y hay que volver a por otro.
4. **Encender las luces del helipuerto** (`E` de la 2ª).
5. **Llamar al helicóptero** desde el teléfono de recepción (pide las cuatro anteriores).

Después, aguantar hasta el final de la ronda siguiente (`missionDefenseRounds`) y abrir la puerta 9 con la pulsera.
No hay Llave Maestra en este mapa.

### 5.9 Secretos y decorados (puntos de `[points]`)
- **Puerta giratoria:** al usarla da la vuelta y te deja dentro ("Check-out no disponible"). Es la idea de Hotel
  California, sin la letra.
- **Guiños a El Resplandor:**
  - La araña del vestíbulo.
  - La foto del salón: al ganar, la pantalla final es la foto en sepia con los animales.
  - El barman fantasma.
  - La máquina de escribir del conserje ("No por mucho madrugar coges mejor tumbona").
  - "OGNIB" en la puerta de una habitación de la 1ª, con espejo.
  - La habitación 237: su bañera es una entrada de abuelos, y sale una abuela con gorro de baño.
  - Las gemelas al fondo del pasillo norte de la 2ª.
  - El laberinto de setos.
  - La moqueta de hexágonos en todos los pasillos de las plantas.
- **El albornoz:** se lleva en brazos y da ×2 si sales con él (sustituye al disco de oro). No hay discos de Julio ni
  karaoke en este mapa.
- **La caja de armas:** "Objetos perdidos".

## 6. Luz (barata, pensando en Android)
- **Acto 1 (casi todo con emisivos):**
  - Azul de luna por las ventanas.
  - Carteles verdes de SALIDA.
  - Rojo de emergencia en las escaleras.
  - Relámpagos.
  - La linterna: un foco por cámara, sin sombras.
- **Acto 2:**
  - **Luz cálida de bombilla (tungsteno):** la araña (emisivo + una luz puntual), apliques en los pasillos (emisivos y
    halos, una luz real cada 3 apliques) y la bola de espejos (puntos de luz con un shader que gira).
  - **Exterior:** la piscina turquesa desde dentro (emisivo + una luz que la ilumina hacia arriba) y el neón rosa y
    cian de la azotea.
  - **Ambiente:** el de cada zona, con `DayNightCycle.AddGlow`, como en el pueblo.
- **Halos y conos falsos:** quads aditivos con un shader sin luz (`Resources/Shaders/LightCard`), para las lámparas y
  los rayos de los relámpagos por las ventanas.
- **Si no basta:** dos juegos de lightmaps (apagón y luz) que se cambian al dar la luz. Se prueba después, con
  capturas.
- **Rendimiento:** oclusión horneada; como mucho 4 luces por objeto; ninguna sombra salvo la de la luna.

## 7. Modelos (`Tools/Blender/make_hotel_props.py`, FBX `HJ_*` en `Assets/Maps/HotelJubiles/Models`)
Se puede empezar ya, en paralelo con las plantas. Reutiliza el `Builder` de Venecia como el pueblo.
- **Fachada y vestíbulo:**
  - Fachada de la torre con balcones de Benidorm (tramos por celda) y el letrero de neón (letras sueltas).
  - Puerta giratoria, mostrador, columnas, sofá, araña.
  - Puertas de ascensor con indicador de planta, cabina, botonera.
- **Escaleras:** barandilla de escalera y pretil.
- **Bufé y Salón Dorado:**
  - Mesas del bufé con bandejas, cafetera industrial, encimeras.
  - Tarima del animador con micro, barra del salón, mesas redondas, bola de espejos, el marco de la foto.
- **Sótano:** calderas, lavadoras, carro de la ropa, cuadro eléctrico.
- **Habitaciones y plantas:** cama, bañera, minibar (la máquina de bebidas), escritorio con máquina de escribir,
  puerta de habitación numerada (también entrada de abuelos), poste de suelo mojado con cubo y fregona.
- **Exterior:** setos (bloques), tumbona, sombrilla, palmera, bar de la piscina.
- **Azotea:** helipuerto (H), helicóptero, depósito de agua, aparato de aire, góndola de limpiacristales (entrada de
  abuelos en el borde de la azotea), puerta del ático (rota y entera).
- **Accesorios:** "Objetos perdidos" (la caja), café, albornoz, las dos pulseras.
- **Abuelos:** la moto de movilidad (accesorio que se pone el abuelo), la bandeja, el gorro de dormir, el gorro de
  ducha y el hacha.
- **Figuras:** el piloto dormido y el animador.
- **Regla de Venecia:** ninguna cara de dos piezas en el mismo plano (≥ 1,5 cm).

## 8. Online (subir el protocolo a `animal-island-online-6`, una sola vez para hotel y crucero)
- **Del anfitrión:** luz, ascensores y montacargas (los mueve el dueño y los comprueba el anfitrión), puertas `$`,
  pulseras (como la llave), trampa, misión y jefe.
- **Bingo:**
  - Cartones y números salen de la semilla y del `NetId`, así que no hace falta mensaje para repartirlos.
  - El anfitrión manda `NetMsg.Bingo`: inicio con reto, tachaduras, línea, bingo y pulsera.
  - Lección del pueblo: los eventos se anuncian siempre desde el anfitrión, aunque el cliente pudiera deducirlos.
- **En el estado de la misión:** las pulseras (dónde están y quién las lleva) y la luz.

## 9. Pruebas (`HotelPlayTest` + las comunes)
- **Setup:** 0 errores, grupos sellados, 9 puertas, todo alcanzable, ruta a la salida y ninguna rendija.
- **Acto 1:** a oscuras; bebida y ascensor no responden; linterna. Las puertas 1 + 2 llevan a la luz: ascensores,
  minibares, sangría y número de luces encendidas.
- **Paseos con el cuerpo del jugador:**
  - Cada escalera: sótano–cocina, rampa del garaje, baja–1ª, 1ª–2ª, 2ª–azotea.
  - El laberinto, el anillo de cada planta y una habitación.
  - Altura libre en los huecos.
  - Un abuelo te sigue de la baja a la 2ª.
- **Ascensor, tolva y puerta `$`:**
  - Ascensor sótano → 2ª; zona cerrada saltada; abuelos que salen del ascensor.
  - Tolva: el jugador y un abuelo; atascada con el sótano cerrado.
  - Puerta `$` sin pulsera, con ella, y el abuelo que pasa detrás.
- **Bingo:**
  - Abrir el Salón y que la ronda siguiente sea de bingo; números iguales en dos máquinas.
  - Tachar, línea con power-up y bingo con el bote.
  - Cada uno de los 10 retos, con bajas simuladas, y la pulsera: bebida gratis para él, no para otro.
- **Pulsera de puertas:** cogerla, abrir con ella, perderla al caer y que aparezca en otra `O`, y que la puerta 9 la
  pida.
- **Trampa y abuelos nuevos:**
  - Suelo mojado: cobra, resbalan, las bajas pagan.
  - Sonámbulo (linterna lo despierta), moto (no sube escaleras), bandeja (frente sí, espalda no), gemelas.
- **Misión y final:**
  - Café: se cae al recibir un golpe y se enfría.
  - El piloto, el helipuerto, el teléfono y el helicóptero al acabar la ronda.
  - La puerta 9, el jefe con apagones, la salida y el albornoz ×2.
- **Además:** `CoopPlayTest` con 3 jugadores, `OnlinePlayTest.RunDirectHotel` y líneas en `QuickChecks` (bingo,
  trampa y variantes).

## 10. Orden y qué va en paralelo
1. **Plantas reales** (`Docs/PLANTAS_FORMATO.md`), común con el crucero. Lo primero, en una sola sesión.
2. **A la vez que 1:** los modelos `HJ_*` y el kit de luz (halos, relámpago, grupos de emisivos).
3. **Tema `IslaZombieSetup.Hotel.cs`:** fachadas, moqueta, setos, pretiles, neón y puntos.
4. **Reglas** (5.1 a 5.8), cada una con su bloque en `HotelPlayTest` según se termina.
5. **Cooperativo, online y regresión de los demás mapas.** Después, `CLAUDE.md` y `Docs/mapas.md`.

## 11. Revisión "¿es divertido como un Black Ops?" (2026-11-17)
- **Sitios para correr en círculos con la horda:** la terraza de la piscina, el garaje, el Salón Dorado y la azotea.
- **Sitios arriesgados con premio:** las habitaciones sin salida, el cuarto del conserje con el Microondas y el laberinto.
- **Momento que cambia el mapa:** dar la luz.
- **Movimiento propio:** ascensores, tolvas, montacargas y pasadizos.
- **Corregido en el plano:** las dos plantas de habitaciones eran iguales. La 1ª tiene ahora un gimnasio que cruza el
  bloque y la 2ª un balcón exterior: cada una juega distinto.
- **Falta (propuesta, sin decidir):** un arma maravilla del mapa, como las de los Zombies (Thundergun, Ray Gun). Idea:
  **La Aspiradora Industrial** del servicio de habitaciones. Absorbe a los abuelos de un cono cercano y los escupe
  contra la pared. Saldría solo de la caja, con poca probabilidad, y con la luz dada.

## 12. Números a ajustar con personas
- Los precios de las puertas.
- Lo que tarda en llegar la luz (1750 / 2250).
- Cartón de 9 y el 70 % de números útiles.
- El bote.
- La trampa (750, 300 de daño).
- Las rondas de cada variante.
- El tiempo del sonámbulo para despertar.
- Los cortes de luz del jefe.
