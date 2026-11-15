# Fase 3 — formato del mapa con alturas (diseño de Fable, 2026-11-15)

Especificación para que Opus / Sonnet la implementen en `IslaZombieSetup*.cs`. **Sustituye** al apartado
«Especificación del formato con alturas» de `PLAN_PRODUCCION.md` (cambia la altura de nivel, dónde se escriben las
rampas y cómo son las caídas).

> **Estado 2026-11-15 (Opus): herramientas hechas y probadas; trazado de las zonas pendiente de aprobación.**
> Pasos 1–4 del orden de trabajo hechos y el 5 preparado: `Setup(MapConfig)` (Isla del Faro, «Isla del Faro (alturas)»,
> «Prueba de alturas»), lector de `[heights]`, suelo por niveles, sólidos que suben, rampas (cuñas), bordillos, enlaces
> de un sentido (área 12 «Caida», 13 «Tobogan»), `EnemyAI.TraverseLink` (salto en arco), `Zones/OneWaySlide`, checks 1–9
> (`IslaZombieSetup.Heights.cs`), mapa de prueba y `HeightsPlayTest` (20 OK). «Isla del Faro (alturas)» existe como copia
> idéntica en el selector (`Assets/Maps/IslaFaroAlturas`, escena `IslaFaroAlturas`); Isla del Faro sale igual que antes.
> Diferencias con lo escrito abajo: el check 6 (grupos sellados) cuenta los bordes y excluye los toboganes, que tienen su
> propio check 7; el circuito (check 8) se mide como el anillo de celdas alrededor del bloque cerrado más grande de la
> zona; el check 9 da aviso en Isla del Faro (Chiringuito, Volley, Mercadillo, Muelle tienen una sola salida). Sin hacer:
> el trazado de cada zona (decisión de diseño), la cobertura de spawns no cambia (ya usa caminos de la malla, que
> respetan los enlaces de un sentido).

## 1. Decisiones y por qué

| Decisión | Motivo (medido en el proyecto) |
|---|---|
| **Un nivel = 2 m** (no 1,5 m) | El salto normal sube 1,2 m y el escalón del `CharacterController` 0,3 m: 1,5 m quedaba justo en el límite. Con 2 m nadie sube un nivel saltando. Además celda = 2 m, así que la rejilla es cúbica. |
| **Rampas de 2 o 3 celdas por nivel** (26,6° / 18,4°), nunca de 1 | 1 celda serían 45° = el límite exacto de la malla (`agentSlope` 45) y casi el del jugador (`slopeLimit` 50). |
| **Las alturas y las rampas van en `[heights]`, no en `[map]`** | En `[map]` cada celda ya lleva una letra de zona o un marcador; una rampa ahí borraría la zona. `[map]` no cambia: los mapas actuales siguen valiendo tal cual. |
| **Sin pisos superpuestos** (campo de alturas 2,5D) | Un nivel por celda. No hay puentes ni voladizos; los únicos techos son los de Templo y Cueva. Simplifica malla, cámara y colisiones. Si un mapa futuro los necesita, se añade una sección `[bridges]`. |
| **Los bordes dentro de un mismo grupo son caídas abiertas** | Regla 1 (nada de muros invisibles): si ves el borde, puedes tirarte. Sin daño de caída. Los abuelos también bajan por ahí (enlace de un sentido); subir, solo por rampa. Eso crea «arriba aguanto, y si se complica salto». |
| **Entre grupos distintos solo se cruza por puerta o por tobogán `!`** | Los grupos ya están separados por celdas sólidas; eso no cambia. El tobogán es un mecanismo (trigger), no un hueco: la Rana salta 4 m con su habilidad y treparía por cualquier hueco. |
| **Un tobogán solo puede acabar en una zona que ya esté abierta seguro** | Si no, el jugador caería en una zona sin abrir. Regla exacta en la sección 5. |

**Consecuencia para el plan:** dos atajos propuestos en la Fase 3 **no cumplen** la última regla y hay que cambiarlos:
- «Caída desde el Muelle al Mercadillo»: se puede llegar al Muelle sin haber abierto el Mercadillo. Alternativa válida: Muelle → Faro.
- «Salto del balcón del faro al Lago»: se puede llegar al Faro por el Parking sin abrir el Lago. Alternativa válida: el salto cae dentro del propio Faro (es el mismo grupo, borde abierto normal).
- Sí vale: Volley → Chiringuito (el Chiringuito es el inicio).

## 2. Formato

### Sección nueva `[heights]`
Misma anchura y altura que `[map]`, un carácter por celda. Va **antes** de `[map]` (el lector trata `[map]` como última
sección). Si falta, todo es nivel 0.

| Carácter | Significado |
|---|---|
| `0`–`5` | Nivel de la celda (× 2 m). Obligatorio en toda celda pisable, de prop (`o h x V W A H`) y de agua `w`. |
| `.` | Sin dato: mar, sólidos (`# R Z`), puertas (`1`–`9`) y toboganes (`!`). En una celda pisable cuenta como `0`. |
| `^ v < >` | Celda de **rampa**; la flecha apunta **cuesta arriba** (`^` = hacia la fila de arriba del texto). |

Reglas de las rampas:
- Un **tramo** = 2 o 3 flechas iguales seguidas en la dirección de la flecha. Sube exactamente 1 nivel.
- La celda anterior al tramo (lado bajo) es pisable de nivel L; la posterior (lado alto), pisable de nivel L+1.
- Tramos paralelos pegados forman una rampa ancha. Para subir 2 niveles: dos tramos con un rellano entre medias.
- En `[map]` la celda de rampa es **suelo liso** (letra de zona): sin marcador, prop ni puerta.

### Cambios en `[map]`
- Marcador nuevo **`!` = tobogán de un sentido**. Ocupa una celda de la línea de muro entre dos zonas. Debe tener
  exactamente dos vecinos pisables, opuestos; el de **mayor nivel es la entrada**. Si están al mismo nivel, error.
- Todo lo demás, igual.

### Cambios en `[zones]`
- Bandera opcional **`circuito`**: el setup exige que la zona tenga un circuito (sección 5).

## 3. Qué genera el setup

- **Suelo**: rectángulos por (zona, nivel); caja desde y = −3 hasta `nivel × 2`. Los lados ya usan el material de roca,
  así que **el talud entre niveles sale solo**.
- **Rampa**: cuña por tramo (malla propia + `MeshCollider`), enrasada con los dos suelos, material del suelo de la zona.
  Rellena por debajo y con caras laterales.
- **Sólidos** (`# R Z`): la base sigue en 0 y la parte alta sube: tope = `nivel máximo de los vecinos pisables × 2 +
  3,5` (rocas y templo + 6). El collider sigue midiendo 10 m por encima del tope visible.
- **Techos** (Templo, Cueva): a `nivel × 2 + 3,4`.
- **Props, árboles, máquinas, entradas de spawn, puertas, marcadores, carteles, etiquetas**: a la altura de su celda.
  Las huellas de colisión («columnas desde el suelo») parten del suelo de su celda, no de y = 0.
- **Borde abierto** (dos celdas pisables vecinas del mismo grupo con distinto nivel y sin rampa entre ellas):
  - Visual: bordillo de 0,15 m sin collider en el lado alto.
  - Abuelos: un `NavMeshLink` de un sentido (arriba → abajo) por cada tramo recto de borde, del ancho del tramo menos
    0,35 m por lado, **uno por tipo de agente** (normal y Saltador). Los lados de las rampas no llevan enlace.
- **Tobogán `!`**: la celda sigue siendo muro sólido (collider de 10 m). Encima se pone:
  - Arriba: boca visible (tubo / duna) con un trigger `OneWaySlide` que lleva al jugador en arco hasta el punto de
    salida (0,6 s, sin control, sin daño).
  - Abajo: boca de salida con trampilla; no es un hueco.
  - Abuelos: `NavMeshLink` de un sentido entre los dos lados (los dos tipos de agente). No hace falta mensaje de red:
    el movimiento del jugador es del dueño y los abuelos los mueve el host.
- **Mar y costa**: solo cuenta como costa el nivel 0. Una escalerilla de piscina (`s` junto al mar) exige nivel 0.

## 4. Cambios de código

`IslaZombieSetup.cs` asume y = 0 en `World(c, r)` (21 usos, 3 más en `IslaZombieSetup.Entrances.cs`), en `WallBox`
(base 0), `Blocker`, `RoofY` y las huellas de props.

1. `Layout`: `int[,] level`, `char[,] ramp`; `float Y(c, r)` (en rampa, altura en el centro de la celda) y
   `Vector3 Ground(c, r)` = `World` con esa altura. Repasar **uno por uno** los usos de `World(` y decidir cuál pasa a
   `Ground(`. `Rects(...)` agrupa además por nivel donde haga falta.
2. `Setup(layoutPath, scenePath, navMeshPaths)` con parámetros, para poder generar el mapa de prueba sin tocar Isla del Faro.
3. `BuildGround`, `BuildSolids`, `BuildProps`, `BuildDecor`, `BuildCoast`, `BuildDoors`, `PlaceMarkers`,
   `BuildGameplay`, `BuildLabels`, `BuildMapAndSigns`, entradas: alturas.
4. Nuevos: `BuildRamps`, `BuildLedges` (bordillos + enlaces), `BuildSlides`.
5. Runtime:
   - `Zones/OneWaySlide.cs` (trigger del tobogán).
   - `EnemyAI`: cruzar los enlaces con un salto en parábola (reutilizar el salto de los senderistas, `UpdateHop`) en vez
     del deslizamiento en línea recta que hace `NavMeshAgent` por defecto. Comprobar que los clientes online lo ven bien
     (los abuelos son marionetas interpoladas).
   - `RunLog` ya apunta caídas: confirmar que no las cuenta como algo malo ni hay daño de caída en ningún sitio.
6. Tests que colocan al jugador con y = 0 y buscan malla a 4 m (`Teleport` en los play tests): con niveles 3–5 no la
   encuentran. Usar la altura real del punto.
7. **Cobertura de spawns** (`IslaZombieSetup.Entrances.cs`): hoy recorre celdas vecinas sin dirección. Con alturas el
   grafo es dirigido: un borde se cruza solo hacia abajo; una rampa, en los dos sentidos.

## 5. Comprobaciones nuevas del setup (fallan en voz alta)

1. `[heights]` mide lo mismo que `[map]`; toda celda pisable / prop / agua tiene nivel.
2. Rampas: tramo de 2–3 celdas, lado bajo L y lado alto L+1, celda de `[map]` lisa. Ninguna rampa acaba en sólido.
3. Puertas: la celda de puerta y sus dos lados pisables están al mismo nivel y no son rampa.
4. Agua `w`: mismo nivel que todos sus vecinos pisables.
5. Toda celda pisable es alcanzable **andando** (malla, sin contar enlaces de bajada) desde el inicio con todas las
   puertas abiertas: no hay terrazas a las que solo se llegue cayendo, ni pozos sin salida.
6. Los grupos siguen sellados con las puertas cerradas **contando los enlaces** (bordes y toboganes).
7. Tobogán de la zona A a la zona B: quitando el grupo de B del grafo de puertas, A **no** es alcanzable desde el
   inicio (B está abierto siempre que A lo esté). Si no, error con el nombre de las dos zonas.
8. Zona con bandera `circuito`: existe un ciclo de celdas pisables de ≥ 25 m en el que al menos la mitad tiene carril
   de 2 celdas de ancho. Informar la longitud.
9. Como mucho 2 zonas con una sola salida (puertas + toboganes de salida). Aviso, no error, hasta rediseñar el mapa.
10. Los checks actuales (rutas, puntos alcanzables en su zona, cobertura) siguen pasando.

## 6. Mapa de prueba (primer entregable)

`Assets/Maps/PruebaAlturas/PruebaAlturas_Layout.txt`, escena `Assets/Scenes/PruebaAlturas.unity` (fuera del build).

```
[zones]
a | Abajo  | Abajo  | 0 | EDD699 | start
b | Arriba | Arriba | 1 | 73AD4D

[doors]
1 | Puerta de prueba | 500 | a b

[heights]
................
................
..0>>1111.1111..
..0001111.1111..
..0000000.1111..
..0000000.1111..
..0000000.1111..
................
................

[map]
................
.##############.
.#aaaaaaa#bbbs#.
.#a@aaaaa1bbbb#.
.#aaaaaaa#bbbb#.
.#saaaaaa!bbbb#.
.#aaaaaaa#bbBb#.
.##############.
................
```

Lo que tiene: una rampa de 2 celdas (fila 2), una terraza de nivel 1 con dos bordes abiertos (hacia el oeste en la
fila 3 y hacia el sur entre las filas 3 y 4), una puerta a nivel 1 y un tobogán de `Arriba` (nivel 1) a `Abajo`
(nivel 0), válido porque `Abajo` es el inicio.

`HeightsPlayTest` sobre este mapa:
- El jugador sube la rampa andando y no sube el borde saltando (sí con el supersalto de la Rana).
- Salta el borde y aterriza sin daño. Un abuelo en la terraza baja por el enlace; uno abajo sube por la rampa.
- Con la puerta cerrada no se llega a `Arriba` de ninguna forma (incluido el supersalto contra el tobogán).
- Con la puerta abierta, el tobogán lleva al jugador y a un abuelo de `Arriba` a `Abajo`, y no al revés.
- Una bala no atraviesa el talud ni el tobogán. La cámara no se mete en el talud al pegarse a él.
- El check 7 falla si se invierte el tobogán en una copia del layout (comprobar que el error sale).

## 7. Orden de trabajo

1. Parametrizar `Setup` y añadir `[heights]` al lector, sin generar nada nuevo: Isla del Faro debe salir **idéntica**
   (mismo informe, `SmokePlayTest` y `QuickChecks` en verde).
2. Suelo por niveles, sólidos y rampas en el mapa de prueba. Capturas.
3. Bordes, enlaces y salto de `EnemyAI`. Tobogán.
4. Checks nuevos + `HeightsPlayTest`.
5. **Mapa nuevo, sin tocar Isla del Faro** (pedido del usuario 2026-11-15): copiar el layout a
   `Assets/Maps/IslaFaroAlturas/IslaFaroAlturas_Layout.txt`, con escena `Assets/Scenes/IslaFaroAlturas.unity`, sus dos
   NavMesh, su mapa de pausa y sus carteles en su carpeta, y una entrada nueva en el selector («Isla del Faro (alturas)»,
   `MapData` + escena en el build). Isla del Faro y su escena no se regeneran durante esta fase. `SmokePlayTest` y
   `IslaZombiePlayTest` reciben la escena como parámetro para poder pasarlos en los dos mapas. Al final, el usuario
   decide si el nuevo sustituye al viejo, conviven, o se descarta (borrar la carpeta, la escena y la entrada del selector).
   Se aplica zona por zona (Faro → Camping → Volley → Mercadillo → Templo → Cueva). Cada zona: captura, setup en
   verde, `SmokePlayTest`; regresión completa al acabar las seis. **El trazado de cada zona es decisión de diseño:
   lo propone Fable / Opus y lo aprueba el usuario antes de aplicarlo.**
6. Carteles «ESTÁS AQUÍ» y mapa de pausa regenerados; online (`RunDirect`) al final.

## 8. Riesgos

- **Rejilla de voxels de la malla**: los enlaces deben caer sobre malla en los dos extremos; separarlos 0,5 m del borde.
- **Entradas de spawn en un talud**: una `s` cuyo sólido vecino es un talud y no un muro. De momento exigir muro, roca,
  prop o mar al mismo nivel (ampliar el check actual).
- **Anselmo cagón**: `PlanTunnel` compara longitudes de camino; con enlaces de bajada los caminos directos se acortan.
  Repetir su bloque de `FeedbackPlayTest`.
- **Pantalla dividida**: cuatro cámaras con más geometría; medir FPS tras la primera zona con altura.
- **Senderistas (Saltador)**: su malla ignora los props; comprobar que no usan los bordillos ni las bocas de tobogán
  como atajo hacia arriba.
