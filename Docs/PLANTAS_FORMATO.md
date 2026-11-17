# Plantas reales: formato y lo que tiene que hacer el setup (2026-11-17, Opus)

Especificación para construir mapas con **plantas de verdad, una encima de otra**. La necesitan el Hotel Jubiles
(sótano, planta baja, dos plantas de habitaciones y azotea) y el Crucero (muelle y cubierta inferior, cubierta principal
y cubierta de sol). Amplía `Docs/FASE3_FORMATO.md` (alturas); todo lo de allí sigue valiendo **dentro de cada planta**.

## Estado (2026-11-17, tarde)
- **Hecho y probado:**
  - Generadores: `Tools/Maps/floors.py` (formato y comprobaciones), `make_hotel_layout.py`, `make_crucero_layout.py`,
    `make_prueba_plantas_layout.py` (`--check` imprime cada planta sin escribir nada).
  - C#: lector, construcción por planta (`IslaZombieSetup.Floors.cs`), comprobaciones de rejilla y de malla, piezas de
    juego `Lift`, `LiftButton`, `ServiceDoor`, `FloorLink`.
  - Mapa de prueba `PruebaPlantas` (Tools > Island > Setup Prueba de plantas) y `PlantasPlayTest` (Tools > Island >
    Play Test Plantas): **21 OK / 0 fallos**.
  - Hotel Jubiles y Crucero construidos **sin tema** (aspecto genérico de Isla del Faro): **0 errores** los dos.
    - Hotel: 5 plantas, las 9 puertas unen sus grupos, 112 m hasta la salida, 229 islas cortadas, altura libre OK,
      cobertura 37/1163 celdas seguras.
    - Crucero: 3 plantas, 9 puertas OK, 84 m hasta la salida, 182 islas cortadas, cobertura 29/1296 (tenía 252: se
      añadieron puertas de tripulación en la superestructura cada ~8 celdas y escalerillas para salir de la piscina).
  - Regresión: Isla del Faro sale con el mismo informe, Smoke 15 OK, QuickChecks 0 fallos.
- **Arreglos que salieron al construir (valen para todo mapa con plantas):**
  - Los colisionadores altos (10 m) de muros a cielo abierto, cabinas de abuelos y barreras de puertas (5 m) atravesaban
    las plantas de arriba: una cabina del sótano cortaba el bufé y un muro de la planta baja tapaba el balcón de la 2ª.
    Ahora paran bajo el forjado de la primera planta que tenga algo encima (`CeilingY`, `CapDoor`).
  - Las islas de malla se cortan celda a celda (una isla de techos ocupaba todo el edificio y se comía un rellano).
  - Sin muros de costa en mapas con plantas (eran paredes invisibles en las plantas de arriba).
  - Agua de piscina `w` junto a una `s`: escalerilla para salir de la piscina.
  - El informe guarda la lista de celdas seguras (`IslaZombieSetup.LastSafeCells`) para colocar entradas.
- **Sin hacer:** rescate en caídas desde plantas altas, mapa de la pausa y carteles por planta, área 15 «Escalera»,
  paradas de ascensor como entradas de abuelos, entradas propias del tema en plantas altas (en la azotea del hotel y
  las cubiertas, una `s` junto al vacío sale hoy como escalerilla del mar), etiquetas de zona en plantas donde la zona
  solo tiene una escalera (el crucero pone «CUBIERTA DE SOL» en la cubierta principal), oclusión horneada.

## 1. Decisiones y por qué

| Decisión | Motivo |
|---|---|
| **Una rejilla por planta, todas del mismo tamaño** | La celda (c, r) está en la misma x, z en todas las plantas. Cada planta se construye con el código de hoy (2,5D), desplazada a su altura. No hay que reescribir el setup, solo ejecutarlo planta a planta. |
| **Una planta = 2 niveles = 4 m** (3,6 m de sala y 0,4 m de forjado) | Las paredes ya miden 3,5 m y el nivel ya es de 2 m. Una escalera son dos tramos de rampa, como en el mapa con alturas. |
| **La base de cada planta, en niveles, en `[floors]`** (puede ser negativa) | Así el sótano queda en −2 (−4 m) y la planta baja en 0, que es donde está el suelo de hoy. |
| **`~` = hueco** (ni suelo ni forjado en esa planta) | Las escaleras necesitan altura libre encima, y una sala de doble altura también. Es la única forma de "ver" la planta de abajo. |
| **Escaleras: rampas normales en la planta de abajo; la última llega a la altura de la planta (nivel 2) y sale a la planta de arriba** | Reutiliza las rampas, los bordillos y las cuñas que ya existen. La única regla nueva es que el lado alto del último tramo se busca en la planta de arriba. |
| **Escaleras de ida y vuelta de 3 × 3 celdas, apiladas cada dos plantas** | Una escalera de una planta no cabe encima de la de la planta de abajo: la cabeza no pasa (1,6 m libres). Por eso se alternan dos huecos, como en las escaleras de tijera. `Plan.stair()` la dibuja entera, con muros y huecos. |
| **Las mallas de navegación se hornean una sola vez para todo el mapa** | NavMesh ya admite pisos superpuestos y `CheckMap` ya trabaja con caminos de la malla. Las comprobaciones de puertas y grupos valen tal cual entre plantas. |
| **Barandillas en todo borde con `~` o con el vacío (`.`) de una planta alta; nada de caídas entre plantas** | Regla de siempre: los abuelos te siguen. Si se salta una barandilla, el rescate de Venecia (`CanalRescue`) te devuelve donde estabas. |

## 2. Formato del archivo
```
[zones]                      igual que hoy; bandera nueva "techo" = interior (lleva techo donde no hay nada encima)
[doors]                      igual que hoy (máximo 9; cada puerta en UNA planta)
[floors]
S | Sotano | -2             id | nombre | base en niveles (×2 m), de abajo arriba
0 | Planta baja | 0
1 | Primera planta | 2
[lifts]
A | Ascensor A | 22,19 | S 0 1 2          id | nombre | celda de la cabina | plantas en las que para
[links]
1 | Tolva de la lavandería | 1:17,24 > S:13,27 | tolva     de un sentido: planta:c,r > planta:c,r | tipo (tolva / tobogan)
[points]
puerta_giratoria | 0:21,29   puntos con nombre para el tema (decorados y objetos únicos)
[heights S] ... [map S] ... [heights 0] ... [map 0] ...   una pareja por planta, en el orden de [floors]
```
- **Compatibilidad:** un archivo sin `[floors]` es una sola planta "0" con base 0 (`[heights]` + `[map]`, como hoy).
  Los cinco mapas actuales no cambian.
- **Caracteres nuevos en `[map N]`:**
  - `~` hueco.
  - `=` cabina de ascensor: la misma celda en cada planta en la que para; en las que se salta, `#` (el hueco del ascensor).
  - `$` puerta de servicio: solo para quien lleva la pulsera de puertas del hotel; en las comprobaciones cuenta como cerrada.
  - `:` garfio de abordaje (crucero).
- **Las letras de zona siguen sin poder ser caracteres de la leyenda.** `floors.py` lo comprueba.

## 3. Qué construye el setup (por planta, con `cur` = esa planta)
- **`World(c, r)`** suma `cur.baseY` (base × 2 m). Todo lo que hoy se coloca con `World()` sube solo de planta.
- **Suelo:** en la planta más baja, como hoy (caja desde −3). En las demás, un **forjado** de 0,4 m bajo cada celda que
  no sea `.` ni `~`. Ese forjado es a la vez el techo de la planta de abajo.
- **Sólidos (`#`):** desde la base de la planta hasta la base de la de arriba si hay algo encima, para que no quede
  rendija bajo el forjado. Si no hay nada encima, a la altura de hoy (más un remate).
- **Techo:** las zonas con bandera `techo` llevan techo a base + 4 m en cada celda (de zona o sólida) que no tenga nada
  encima en la planta de arriba (`.`). Las zonas sin `techo` quedan a cielo abierto (terraza, cubierta, azotea).
- **Escaleras:** las rampas como hoy (cuñas rellenas por debajo hasta la base de su planta). Además:
  - un muro divisorio entre los dos tramos (ya viene como `#` en el mapa);
  - una barandilla en cada borde de `~` salvo por donde se llega desde la escalera.
- **Barandillas:** 1,1 m de alto, con colisión de 1,2 m, capa Wall, también para la malla de navegación.
  - Van en cada borde entre una celda pisable y un `~`, salvo la llegada de la escalera.
  - También entre una celda pisable de una planta alta y `.` (azotea, cubiertas, pasarela).
  - El tema decide el aspecto: pretil de obra en el hotel, borda de barco en el crucero.
- **Ascensores:** en cada parada, una cabina de 2 × 2 m abierta hacia su único rellano, con dos botones dentro (SUBIR y
  BAJAR), y la puerta del ascensor como entrada de abuelos (ver 5).
- **Enlaces (`[links]`):** una boca en el origen (trampilla de la tolva o salida del tobogán) y una salida en el
  destino. Además, un `NavMeshLink` de un sentido en el área 13 «Tobogan», para los dos tipos de agente.
- **Malla:**
  - Se hornea una vez con todas las plantas construidas. Después, `CutNavIslands`: los forjados y techos a los que no
    se llega darían islas.
  - Áreas nuevas: **15 «Escalera»** en las celdas de rampa entre plantas (coste 1), para que la moto de movilidad no
    las use. **16 «Servicio»** en las puertas `$` (obstáculo que talla mientras está cerrada).
- **Cosas por planta:**
  - **Mapa de la pausa y carteles "ESTÁS AQUÍ":** una imagen por planta. La pausa enseña la planta del jugador (con
    pestañas o el nombre de la planta) y cada cartel, la planta de su puerta.
  - **Cobertura de entradas de abuelos:** por planta, con caminos de la malla.
  - **Etiquetas:** a la altura de su planta.
- **Rendimiento (pensando en Android):** marcar todo como estático y hornear la oclusión (Occlusion Culling). Dentro
  de un hotel o un barco tapa casi todo, y es gratis en tiempo de ejecución.

## 4. Comprobaciones
**De rejilla** (las hace `floors.py` y el setup las repite en C#):
1. Caracteres válidos. Las letras de zona no son de la leyenda.
2. Cada `~` tiene algo debajo.
3. **Altura libre:** toda celda pisable con nivel ≥ 1 o de rampa tiene `~` encima si hay una planta justo encima.
4. Tramos de rampa de 2-3 celdas con suelo plano al pie. El lado alto es suelo de nivel +1 en la misma planta o, si el
   tramo llega a la altura de la planta, suelo de nivel 0 en la planta de arriba.
5. Las entradas de abuelos (`s`) tienen algo de lo que salir al lado: sólido, objeto, vacío, ascensor o agua.
6. Las coberturas bajas (`x`) nunca están en un pasillo de una celda.
7. Cada puerta está en una sola planta y toca exactamente sus dos zonas.
8. **Grupos:** con las puertas cerradas (y las `$` siempre cerradas), cada zona es una sola pieza; dos zonas están
   unidas si y solo si son del mismo grupo; cada puerta sola une exactamente sus dos grupos. Se calcula sobre un grafo
   de celdas que cruza plantas por las escaleras.
9. Cada parada de ascensor tiene un rellano y solo uno. Los enlaces empiezan y acaban en suelo.

**De malla** (setup, como hoy):
- `CheckMap` sin cambios: grupos sellados, cada puerta une sus grupos, puntos alcanzables, ruta a la salida.
- Además, **altura libre real**: un rayo hacia arriba de 2,2 m en cada celda pisable no toca nada.
- Además, **sin rendijas**: entre forjado y muro no se ve el cielo.

## 5. Piezas de juego comunes (runtime)
- **`Zones/Lift`:**
  - Cabina con SUBIR y BAJAR. Cierra puertas (1 s), viaja (`travelPerFloor` 2,5 s, con temblor de cámara e indicador
    de planta) y deja al pasajero en la misma x, z de la planta de destino.
  - Solo para en plantas cuyo rellano es de una zona abierta. Puede pedir luz (hotel) o la pulsera (montacargas).
  - Los abuelos no lo usan (van por la escalera). Cada parada es una **entrada de abuelos** que sale por las puertas
    del ascensor: así "a veces el ascensor llega con abuelos dentro".
  - Online: lo mueve el dueño del jugador (como la góndola) y el anfitrión comprueba luz y zonas.
- **`Zones/FloorLink`** (tolva, tobogán):
  - **Jugador:** entra por la boca y aparece en la salida tras `travel` segundos, con fundido y sonido.
  - **Abuelos:** cruzan el `NavMeshLink` escondidos (como los que atajan en Anselmo cagón) y salen a la vez.
  - Solo funciona si la zona de destino está abierta; si no, la boca dice "Atascada".
- **`Zones/ServiceDoor`** (`$`): se abre 3 s solo si quien la usa lleva la pulsera de puertas y las zonas de los dos
  lados están abiertas. Mientras está abierta deja de tallar la malla y pasa quien vaya detrás, abuelos incluidos.
- **Rescate en los bordes:** `CanalRescue` vale también para quien caiga desde una planta alta. Si cae por debajo de la
  base de su última planta menos 3 m, vuelve a la última celda abierta que pisó (15 de daño, nunca mortal). En el
  crucero, la caída al mar es lo mismo.

## 6. Orden de trabajo
1. **Lector:** `[floors]`, `[lifts]`, `[links]`, `[points]`, `[map N]`, `[heights N]`, compatible con los mapas de hoy.
   Después de este paso, Isla del Faro tiene que salir exactamente igual: mismo informe y pruebas Smoke (15) y
   Feedback (26) en verde.
2. **Construcción por planta** (base, forjados, techos, sólidos hasta el forjado, huecos, barandillas) y
   **comprobaciones de rejilla** en C# (las de la sección 4).
3. **Mapa de prueba `PruebaPlantas`:**
   - Contenido: tres plantas, una escalera entre cada dos, un ascensor, una tolva, un hueco de doble altura, una
     puerta de pago por planta y una `$`.
   - Prueba `PlantasPlayTest`:
     - subir y bajar andando con el cuerpo del jugador;
     - el abuelo te sigue escaleras arriba;
     - la cámara no atraviesa el forjado;
     - barandilla: no se cae, y si se salta, rescate;
     - ascensor (luz, zona cerrada, parada con abuelos);
     - tolva (el jugador y un abuelo; atascada si la zona está cerrada);
     - la `$` con y sin pulsera;
     - el mapa de la pausa por planta.
4. **Hotel y crucero:** generar sus layouts con los scripts (sin `--check`) y construirlos.

Riesgo conocido: los forjados son cajas grandes. Si en algún sitio se ve una rendija o la cámara se mete en uno,
hay que medir antes de tocar (capturas desde dentro de cada planta).
