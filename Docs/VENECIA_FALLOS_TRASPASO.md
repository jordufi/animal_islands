# La Serenísima — plan de arreglos (traspaso de Fable a Opus, 2026-11-16)

Fable planifica, Opus ejecuta y prueba. Origen: la primera partida real del usuario (mensajes del 15/11 y 4 capturas)
más lo que quedó pendiente del bloque anterior. Orden = prioridad. Cada punto dice síntoma, causa (comprobada en el
código o **por confirmar**), arreglo y cómo verificarlo. Reglas de siempre: números en `Assets/Data`, tests antes de dar
algo por hecho, no editar scripts con un play test en marcha, commit solo cuando el usuario lo pida.

Flujo de trabajo: `python Tools/Maps/make_venecia_layout.py` → `IslaZombieSetup.SetupVenecia()` (leer `LastReport`) →
`VeneciaPlayTest.Run()` + `SmokePlayTest.RunVenecia()`. Modelos: `Tools/Blender/make_venecia_props.py` (Blender en
segundo plano). Capturas de comprobación: `WallpaperShots` (Open / Animal / OldMan / Render / Close sin guardar).

## Preguntas abiertas para el usuario (preguntar antes de tocar A7 y A5)
1. **"Con agua alta no debería haber barreras, porque no las ves"**: ¿qué barreras? Hipótesis de Fable (la más
   probable): el borde del canal queda bajo el agua, no se ve dónde acaba el muelle y caes sin querer. Otras lecturas:
   las barandillas de los puentes, los bordillos de las rampas, o el rebote de `CanalRescue` al entrar en un barrio
   cerrado. La propuesta A7 asume la primera.
2. **"En el mercado también se bugea"**: ¿qué pasa exactamente? En su captura hay dos abuelos metidos entre los puestos
   (uno parece dentro de un puesto). Hipótesis en A5.

## A. Fallos de la partida del usuario

### A1. Cajas que vuelan (comprobado)
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

### A5. "En el mercado se bugea" (por confirmar con el usuario)
- Lo que se ve en la captura 2: abuelos metidos entre los puestos, uno aparentemente dentro de uno.
- Sospechas, por orden: (1) los puestos (`IZ_Puesto`, celdas `o` en zona `r`) son entradas de abuelos tipo "Objeto" y
  el abuelo sale atravesando el mostrador; (2) el bloque central 4 × 2 de puestos deja huecos entre colliders por los
  que el NavMesh pasa y el jugador no (o al revés); (3) la pala (`S` en (12, 22)) está en el pasillo de una casilla
  entre la fila de puestos y el bloque central, y el `MissionTask` + los abuelos lo taponan.
- Plan: reproducir con `WallpaperShots`/play test colocando al jugador en (12, 22); mirar con el visor de colliders del
  menú de debug; comparar NavMesh y paseos del jugador por todos los pasillos del Mercato (añadir 4 paseos al test).
  Arreglos probables: bloquear con un `Blocker` el interior de cada bloque de puestos, mover la pala a un puesto de
  esquina con sitio (por ejemplo junto a (15, 26)), y que los puestos del bloque central no sean entradas.

### A6. Las máscaras podrían estar más chulas (diseño)
- Ahora son una elipse blanca con dos ojos y un sombrero plano.
- Propuesta (Blender, `mascara()`): bauta con mandíbula en cuña marcada, cejas, filigrana dorada y tricornio con pluma;
  médico de la peste con pico largo curvo, lentes redondas de cristal con aro de latón, sombrero de ala ancha y cuello
  de cuero; **tres variantes de color** por máscara (marfil / negro y oro / rojo) elegidas al azar por abuelo
  (`Repaint`, sin tocar la red: semilla = `NetId`); máscara del Dux propia (`VN_Mascara_Dux`): dorada, con el corno
  ducal y gemas. Al romperse: 3-4 trozos que saltan en vez de la máscara entera.
- Verificar: capturas de cerca con `WallpaperShots.OldMan` de las tres; `VeneciaPlayTest` sigue en verde.

### A7. Acqua alta: "no debería haber barreras porque no las ves" (pendiente de la respuesta 1)
- Con la hipótesis del borde invisible: durante la marea el agua tapa el bordillo y el canal no se distingue de la
  calle inundada.
- Propuesta: (1) que el borde se vea siempre: los `Bordillo` blancos pasan a ser postes bajos/balizas que sobresalen
  0.6 m del agua alta cada 2 m (o una línea de boyas que sube con `Flotantes`); (2) el agua del canal más oscura que la
  de la calle (dos planos: canal y lámina de inundación clara y semitransparente); (3) durante la marea, caer al canal
  no quita vida (solo te devuelve), porque no es culpa del jugador.
- Si el usuario se refería a otra cosa (barandillas, rebote del barrio cerrado), replantear.
- Verificar: captura en marea alta desde la Piazza y desde un puente; test: caída durante la marea = 0 daño.

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
2. Preguntar 1 y 2 al usuario; mientras, A4 (góndola) y B4 (arcos), que van juntos.
3. A5 y A7 con sus respuestas.
4. A6 y B3 (Blender), B5.
5. B2 (toca la red), B7, B6. Commit cuando el usuario lo diga.
