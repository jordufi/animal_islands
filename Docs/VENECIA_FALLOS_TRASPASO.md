# La Serenísima — plan de arreglos (traspaso de Fable a Opus, 2026-11-16)

Fable planifica, Opus ejecuta y prueba. Origen: la primera partida real del usuario (mensajes del 15/11 y 4 capturas)
más lo que quedó pendiente del bloque anterior. Orden = prioridad. Cada punto dice síntoma, causa (comprobada en el
código o **por confirmar**), arreglo y cómo verificarlo. Reglas de siempre: números en `Assets/Data`, tests antes de dar
algo por hecho, no editar scripts con un play test en marcha, commit solo cuando el usuario lo pida.

Flujo de trabajo: `python Tools/Maps/make_venecia_layout.py` → `IslaZombieSetup.SetupVenecia()` (leer `LastReport`) →
`VeneciaPlayTest.Run()` + `SmokePlayTest.RunVenecia()`. Modelos: `Tools/Blender/make_venecia_props.py` (Blender en
segundo plano). Capturas de comprobación: `WallpaperShots` (Open / Animal / OldMan / Render / Close sin guardar).

## Estado (Opus, 16/11 tarde) — hecho y probado salvo lo marcado
- **Hecho:** A1, A2, A3, A4 (con la ruta solo por agua abierta: 3 redes de canal), A5, A6, A7 (balizas + nado de jugador y
  abuelos; el agua del canal NO se oscurece), A8, A9 (sin cambios), B1 (sol a 22°), B2 (protocolo 4), B3, B5 (mismo verde de
  entrada: no se ha preguntado otro tono), B6, B7 en parte (Coop 39 OK leído; online solo en Isla del Faro).
- **Hecho después (Opus, 16/11 noche):** agua low poly; B4 (los dos puentes grandes, 3 y 7, tienen el vano central abierto
  con bóveda y la góndola pasa por debajo: las 7 paradas quedan en una sola red); `OnlinePlayTest.RunDirectVenecia()`
  (22 OK); `CoopPlayTest.RunVenecia()` (42 OK). Ver `CLAUDE.md`.
- **Pendiente:** los puentes pequeños (nivel 0) siguen macizos (no caben ni góndola ni pasajero); 4 muestras del casco
  rozan un cruce en T estrecho (21, 31); origen del cambio de `MainMenu.unity` (no se repitió); equilibrio.
- Resultados: `VeneciaPlayTest` 56 OK / 0 FAIL, Smoke Venecia 15, Smoke Isla 15, Feedback 26, Coop 39, Online RunDirect 69 / 0.
  Detalle en `CLAUDE.md` (sección La Serenísima).

## Preguntas al usuario (contestadas el 16/11)
1. Agua alta: cayó al canal mientras "nadaba" por la calle inundada y la interacción fue rara → ver A7.
2. Mercado: no recuerda el detalle; Fable lo investigó → ver A5.
3. `Assets/Scenes/MainMenu.unity` aparece modificado sin que nadie lo pidiera (15/11 21:32, +132.000 líneas): la isla
   de fondo del menú ha dejado de ser una instancia del prefab `IslandEnvironment` y está guardada como ~1.400 modelos
   sueltos más sus colliders (`Huella`, `Tronco`, `Colision`). Se ve igual pero pesa mucho más. **No está en ningún
   commit.** Opus: averiguar qué lo provoca (sospechosos: `CoopPlayTest`, que arranca en el menú, o
   `TestMapSetup.SetupMenuMaps` / `BuildMenuScene` tras regenerar el prefab) y, salvo que el usuario diga otra cosa,
   descartarlo con `git checkout -- Assets/Scenes/MainMenu.unity`. El cambio de `GB_Spawn_Puerta.mat` (modo de mezcla)
   lo regenera el setup: descartarlo también.

## Decisiones del usuario (16/11, segunda ronda) — mandan sobre lo demás
- **Regla de diseño nueva (vale para todo el juego): los abuelos siempre deben poder seguir la misma ruta que el
  jugador, salvo en la góndola y en el campanile** (con su límite de 20 s). No se crean más escapes donde no llegan.
- **Los barrios cerrados llevan una valla alta** en su borde con el agua (se ve que no se puede entrar); desaparece al
  abrir el barrio. Sustituye al rebote invisible como barrera normal.
- Balizas en el borde del canal: sí.
- Mercado: hacer lo propuesto en A5 **pero la pala se queda en (12, 22)**.
- Cajas que flotan: arreglarlas en todo el mapa (vio varias).
- Escena del menú: descartado el cambio (hecho por Fable con `git checkout`; falta averiguar qué lo provocó).

## A. Fallos de la partida del usuario

### A1. Cajas que vuelan (comprobado; el usuario vio varias: arreglar en todo el mapa)
- Síntoma: en las pilas de cajas la tercera flota a ~2 m (captura 3; también en el muelle del Vaporetto).
- Causa: `VenBuildProps`, rama `default` (pila de cajas): la tercera caja suma la altura dos veces, en la posición
  (`p + (…, 1.05, …)`) y en `baseY: 1.05f` de `PlaceInRect` (que ya suma `center.y + baseY`). El mismo código está en
  `IslaZombieSetup.BuildProps` (Isla del Faro): revisar si allí también flota y arreglar los dos.
- Arreglo: quitar el `1.05f` de la posición y, mejor, no fiarse de una constante: colocar la caja de arriba sobre el
  `bounds.max.y` real de la caja de abajo.
- Verificar: captura de una pila en el muelle y en el Arsenale; añadir a `VeneciaPlayTest` (o a un chequeo de setup) que
  ningún collider de la capa Prop tenga su base a más de 0.1 m sobre otro collider o el suelo.

### A2. La parte de arriba de las puertas se ve rara (comprobado)
- Síntoma: sobre cada puerta de madera hay una mancha oscura dentada (capturas 3 y 4).
- Causa: `fachada_a()` en `make_venecia_props.py`: el montante oscuro es un disco completo (`b.disc(0.47, 0.1, (0, -0.13,
  2.2), "VN_Dark")`) en el mismo plano y con el mismo grosor que la hoja de madera (`box … (0, -0.13, 1.05)`, alta 2.1):
  la mitad inferior del disco se solapa con la puerta y hace z-fighting.
- Arreglo: montante = medio disco solo por encima de z = 2.2 (recortar el cilindro o construirlo con un abanico de
  caras), puerta hasta 2.2, y el montante 2 cm por detrás del marco. Añadir un travesaño de piedra entre puerta y
  montante. Repasar lo mismo en `mausoleo()` (puerta + disco oscuro) y en `window()` con `arch="round"` (cristal + disco).
- Verificar: captura a pie de calle del campo de Ferrovia, sin dientes sobre ninguna puerta.

### A3. Una tumba junto a la góndola que no se puede saltar (comprobado)
- Síntoma: en el Cimitero, al lado del embarcadero, una tumba baja corta el paso y no se salta (captura 1).
- Causa: es la `x` de (29, 30), en el pasillo sur de una casilla. Las `x` son "cobertura baja" pero `VN_Tumba_A` mide
  1.77 m (lápida) y `VN_Tumba_B` 1.3 m; el salto normal llega a 1.2 m.
- Arreglo: regla nueva: **toda `x` se puede saltar** (collider ≤ 0.9 m). Rehacer `tumba_a` (losa + lápida baja de 0.8 m)
  y `tumba_b` (sarcófago de 0.85 m); la mesa de café ya es baja salvo la sombrilla (dejar la sombrilla sin collider).
  Además quitar esa `x` del pasillo de 1 casilla (moverla a (28, 30) junto al muro o cambiarla por decoración sin
  collider): un pasillo de una casilla no debe llevar cobertura.
- Verificar: chequeo en el setup: ningún collider de una celda `x` supera 0.9 m sobre el suelo; paseo del test por la
  fila 30 del Cimitero de oeste a este.

### A4. La góndola atraviesa cosas (comprobado en parte)
- Síntomas: (a) la góndola amarrada en los embarcaderos se mete en el muelle y en los postes (captura 1); (b) durante
  el viaje el casco atraviesa esquinas de muelle y los puentes.
- Causas: el casco mide 9.4 × 1.3 m y los canales pequeños 4 m. (a) `VenBuildGondolas` la amarra a 0.15 m del borde,
  paralela al muelle, sin comprobar que haya 5 casillas de agua libres a lo largo ni dónde caen los dos `VN_PaloRayas`.
  (b) El camino va por los centros de celda con giros de 90° y los puentes son bloques macizos hasta el agua.
- Arreglo:
  1. Góndola de pasaje más corta: modelo nuevo `VN_GondolaCorta` (~5.5 m) o escala 0.6; amarre con comprobación de
     hueco (agua libre a lo largo; si no cabe paralela, perpendicular con la proa hacia fuera), postes fuera del casco.
  2. Camino suavizado: esquinas redondeadas (puntos intermedios) y el barco siempre por el centro del canal; en
     `GondolaStop.Ride` orientar por la tangente suavizada.
  3. Puentes con arco de verdad (ver B4): con el vano abierto la góndola pasa por debajo sin atravesar nada. Mientras no
     estén, la ruta no debe pasar por puentes grandes (BFS sin cruzar celdas de puente de 2 de ancho).
  4. Las barcas de decoración (`VenBuildDecor`) tampoco deben solaparse con embarcaderos ni con la ruta: reservar las
     celdas de agua de los embarcaderos y de los caminos antes de sembrar barcas.
- Verificar: test: durante cada trayecto, muestrear el casco (caja orientada) contra colliders de Wall/Default cada
  0.2 s y exigir 0 solapes; captura de cada embarcadero.

### A5. "En el mercado se bugea" (investigado por Fable el 16/11; el usuario no recuerda el detalle)
Medido en la escena (rejilla de 0.5 m: NavMesh frente a cápsula del jugador, y lista de entradas del Mercato):
- **Causa más probable de lo que vio:** el puesto de (14, 21) es una entrada de abuelos tipo "Objeto"
  (`Entrada35_Objeto`, sale a (14, 22)). Está pegado a la pala (`S` en (12, 22)), en el mismo pasillo de una casilla.
  El abuelo aparece dentro del puesto y atraviesa el mostrador para salir: en la captura 2 es el que asoma tras el
  toldo rojo, con otro ya fuera a su lado. Parece un fallo aunque sea la animación de entrada.
- **Segundo hallazgo:** hay islas de NavMesh encima de los mostradores del bloque central (celdas (10-13, 24)) y sobre
  los tejados de los edificios del Mercato. Están aisladas, pero cualquier código que busque "el NavMesh más cercano"
  (empujón, `SpawnEnemy(near)`, salida de un atajo) puede dejar a un abuelo subido o metido en un puesto.
- Los pasillos están bien: el jugador y el NavMesh coinciden en todo el Mercato (la franja sin malla de la columna 7.5
  es el borde de la logia, normal).
- Arreglo:
  1. En el tema Venecia los puestos no son entradas (`EntranceScore('o')` para la zona `r` = 1): los abuelos salen de
     portales de fachada, escalerillas del canal y, en el Cimitero, de tumbas. Mover el `s` de (14, 22) a una celda con
     fachada al lado.
  2. Quitar las islas de NavMesh: `NavMeshModifier` "Not Walkable" no corta aquí (ya se probó en la fase 3), así que
     hornear con un volumen de exclusión por altura o poner sobre cada puesto / tejado bajo un collider inclinado que no
     deje superficie plana. Comprobación de setup nueva: ninguna isla de NavMesh inalcanzable desde el inicio con todas
     las puertas abiertas (en los tres mapas).
  3. **La pala se queda donde está** (decisión del usuario). Como el puesto vecino deja de ser entrada, el pasillo
     queda libre.
- Verificar: 4 paseos del jugador por los pasillos del Mercato en `VeneciaPlayTest`, la comprobación de islas, y una
  captura del puesto de palas.

### A6. Las máscaras podrían estar más chulas (diseño)
- Ahora son una elipse blanca con dos ojos y un sombrero plano.
- Propuesta (Blender, `mascara()`): bauta con mandíbula en cuña marcada, cejas, filigrana dorada y tricornio con pluma;
  médico de la peste con pico largo curvo, lentes redondas de cristal con aro de latón, sombrero de ala ancha y cuello
  de cuero; **tres variantes de color** por máscara (marfil / negro y oro / rojo) elegidas al azar por abuelo
  (`Repaint`, sin tocar la red: semilla = `NetId`); máscara del Dux propia (`VN_Mascara_Dux`): dorada, con el corno
  ducal y gemas. Al romperse: 3-4 trozos que saltan en vez de la máscara entera.
- Verificar: capturas de cerca con `WallpaperShots.OldMan` de las tres; `VeneciaPlayTest` sigue en verde.

### A7. Acqua alta: caes al canal sin verlo (decidido con el usuario, 16/11)
- Lo que pasó: con la marea alta iba por la calle inundada, no se ve dónde acaba el muelle, cayó al canal y el rescate
  lo devolvió: "la interacción es rara".
- Diseño cerrado:
  1. **Balizas** en todos los bordes de muelle: postes cada 2 m que sobresalen del agua alta (suben con `Flotantes` o
     miden lo bastante), y el agua del canal más oscura que la lámina sobre la calle (dos planos).
  2. **Durante la marea el canal se cruza nadando, el jugador y los abuelos** (regla: ellos siguen tu ruta). Suelo de
     nado a y = aguaAlta − 1.1 m en las celdas de agua interiores, activo solo con `AcquaAlta.Flooding`:
     - jugador: velocidad ×0.5 en agua profunda (`ExternalSpeedFactor`), sale andando por cualquier muelle (hace falta
       un escalón o rampa de salida: el muelle queda 0.65 m por encima del suelo de nado; resolver con un escalón
       sumergido en el borde o subiendo el suelo de nado);
     - abuelos: el fondo de nado se hornea como NavMesh con un área propia ("Canal", coste alto). Fuera de la marea esa
       área está fuera de su `areaMask`; con la marea se añade. Nadan más lento (×0.6) y solo asoman de cintura para
       arriba. Los `NavMeshLink` de entrada / salida del agua se crean en cada borde de muelle.
     - Con la marea baja el canal sigue siendo una caída con rescate (`CanalRescue`), como ahora: ahí nadie nada.
     - Al bajar la marea, jugadores y abuelos que sigan en el agua salen al muelle abierto más cercano (jugador sin
       daño; abuelos recolocados sobre el NavMesh normal).
  3. Online: como la marea, cada máquina lo activa sola por el número de ronda; los abuelos los mueve el anfitrión.
- Verificar: test con marea forzada: entrar al Canal Grande desde Ferrovia = 0 rescates y 0 daño; un abuelo perseguidor
  entra detrás y llega al jugador en el agua; salir por un muelle abierto; al acabar la ronda nadie queda en el agua.
  Captura en marea alta desde la Piazza con las balizas.

### A8. Vallas altas en los barrios cerrados (decisión del usuario)
- Ahora un barrio cerrado solo se "defiende" con el rebote de `CanalRescue`, que no se ve.
- Arreglo: `VenBuildRuntime` crea por cada zona un grupo `Valla_<zona>`: verja de hierro de 3.2 m (modelo nuevo
  `VN_Verja`, tramo de 2 m, con pinchos) en cada borde zona-agua que no sea ya edificio, incluidos los lados de los
  puentes que entran en la zona. El grupo se desactiva cuando la zona se abre (`ZoneManager.ZoneOpened`; en clientes
  ya llega por `ZoneDoor.NetOpened`). La zona inicial no lleva valla.
- El salto de la rana sube 4 m (`PlayerAbility.SuperJump` → `Launch(4f)`): el collider de la verja debe llegar a
  ~6 m aunque el modelo mida 3.2 (decirlo en un comentario: es la única pared más alta que lo que se ve; alternativa,
  verja visible de 5 m con remate). Capa Wall. No debe hornearse en el NavMesh de forma que corte pasillos: va en el
  borde, sobre el bordillo.
- `CanalRescue` se queda como red de seguridad (por si alguien acaba dentro igualmente).
- Con la marea (A7): la verja también impide salir del agua hacia un barrio cerrado, que es lo que se quiere.
- Verificar: test: desde Ferrovia, saltar con la rana hacia el Mercato cerrado = choca y cae al agua, 0 entradas;
  al abrir el puente 1 las vallas del Mercato desaparecen en anfitrión y cliente. Captura de un barrio cerrado.

### A9. El campanile se queda como está (decisión del usuario, 16/11)
- Fable propuso que los abuelos subieran para cumplir la regla; el usuario dijo que no: **no deben seguirte al
  campanile**. Es la segunda excepción aceptada a la regla, junto con la góndola.
- No tocar: los abuelos no suben, el límite de 20 s (`TowerClimb.MaxStay`) sigue, se baja saltando.
- Sí revisar con la regla el resto: tejados y mostradores de puestos (A5) y cualquier sitio nuevo que se cree.

## B. Pendiente del bloque anterior (el usuario ya lo pidió)

- **B1. Mirar las capturas que nadie ha mirado**: atardecer (¿demasiado naranja / niebla?), columnatas de la Piazza, Dux.
  En las capturas del usuario la luz de atardecer se ve bien pero las sombras son muy largas y duras: probar sol a 22°.
- **B2. Gondolero de oro en sitio aleatorio**: varios marcadores `O` (callejones sin salida de Carnevale, Cannaregio y
  Arsenale); el anfitrión elige uno al empezar y lo manda en `WorldInit` (un byte más → **subir el protocolo a 4** y
  recompilar el cliente). Los no elegidos no se construyen visibles.
- **B3. Modelos de verdad** para Giuseppe (esqueleto con boina y bastón saliendo de la fosa) y el gondolero (figura
  dorada con sombrero de paja, camiseta de rayas y remo), en `make_venecia_props.py`; sustituir las formas básicas de
  `VenBuildTasks`.
- **B4. Puentes con arco por debajo**: las celdas de puente no deben generar bloque de suelo hasta -3 m; en su lugar,
  tablero fino + modelo `VN_Puente_Arco` (pequeño, 4 m) y `VN_Puente_Grande` (12 m, con las rampas). Cuidado: el
  NavMesh se hornea con colliders; el tablero necesita collider propio y las barandillas siguen igual. Repetir el
  paseo del test por los 9 puentes.
- **B5. Entradas de abuelos con aspecto veneciano**: ahora llevan la valla verde de Isla del Faro (captura 4). Marco de
  piedra con persiana metálica a medio subir, del color de entrada; en el Cimitero, losa que se levanta. Mantener un
  único color de entrada (regla del juego) pero puede ser otro tono si el verde no encaja con Venecia: preguntar.
- **B6. `CLAUDE.md`**: recoger el bloque de nube de la peste, Dux, góndola online (`Fwd.Gondola`), atardecer,
  columnatas, `WallpaperShots` y este plan; quitar las frases que ya no son ciertas ("NOT committed yet", "clients
  cannot ride yet", "gas cloud is NOT done").
- **B7. Leer el resultado de `CoopPlayTest`** (se lanzó y no se leyó) y pasar online en La Serenísima: hoy
  `OnlinePlayTest` solo recorre Isla del Faro; añadir `RunDirectVenecia()` con los pasos propios (tareas, góndola del
  cliente, campana).

## Orden recomendado
1. A1, A2, A3 (rápidos y muy visibles) → setup + tests + capturas.
2. A5 (puestos sin entradas, islas de NavMesh) y A8 (vallas de barrios cerrados).
3. A7 (balizas + nadar con marea, jugador y abuelos): toca NavMesh y enlaces. A9 no requiere cambios.
4. A4 (góndola) con B4 (arcos de los puentes).
5. A6 y B3 (Blender), B5.
6. B2 (toca la red), B7, B6. Averiguar qué reescribe `MainMenu.unity`. Commit cuando el usuario lo diga.
