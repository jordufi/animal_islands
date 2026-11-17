# Plan para aplicar el feedback de usuarios 1

Fuente: `Docs/Feedback_Usuarios1.md` (fallos + 18 ideas). Plan hecho el 2026-11-17 (Opus) y revisado con el código.
El usuario ha pedido ejecutar **las fases 1, 3, 2 y 4, en ese orden**, informando del progreso por el chat.
Las fases 5 a 8 quedan para más adelante.

> **Estado al 2026-11-17 (más tarde, sesión de feedback):** las fases 1 y 2 están hechas y probadas (detalle abajo).
> El usuario respondió las 4 preguntas abiertas (ver `Docs/FEEDBACK_PROPUESTAS.md`) y pidió seguir aquí: fase 4 en
> marcha; la release (fase 3) espera a que el usuario confirme la publicación.

---

## Valoración del feedback

### Fallos (todos válidos)
| # | Fallo | General / mapa | Nota |
|---|---|---|---|
| 1 | El ratón no desaparece en pantalla completa | General | No había ningún código de cursor |
| 2 | En la pausa (Start) el mapa tapa las opciones | General (pausa) | |
| 3 | En las Fiestas se habla de un barco | Villajubilada | Textos fijos en `BoatMission`, `BossEncounter`, `ExtractionPoint`, `Karaoke`, `ZoneDoor`, `MasterKey` |
| 4 | Las "escaleras" del volley se superponen | Isla del Faro (y alturas) | Son las gradas `IZ_Grada`: el hierro y la madera compartían caras (z-fighting) |
| 5 y 8 | Subes al avión o al bus y eres inmune; hay sitios a los que saltas y los abuelos no | Todos los mapas | Rompe la regla de diseño "los abuelos siempre pueden seguirte" (salvo la góndola y el campanile) |
| 6 | "TAQUILLA" y los nombres de armas siguen a la cámara | General | `WorldLabel` hacía billboard |
| 7 | Los objetivos no salen todos a la vez en la Isla | Isla del Faro / Original | Venecia y las Fiestas ya tenían la lista con ticks |
| 9 | El selector de mapas falla; debería ser un carrusel | General (menú) | `MainMenu.OpenMapSelector` |

### Ideas
| # | Idea | Veredicto | Tipo |
|---|---|---|---|
| 1 | Release en GitHub para los amigos | Sí, la más útil | — |
| 2 | Presets gráficos God / Low / Medium / High (PS3 / PS4 / PS5, DLSS) | **Cambiada**: 4 niveles de calidad (sombras, luces, AA, postprocesado, distancia, escalado STP), no "más realismo" (sería rehacer el arte). DLSS en URP sin comprobar | General |
| 3 | Los discos de Julio motivan a los abuelos (corren más) | Sí, barato, riesgo y recompensa | General |
| 4 | "Brújula de mierdecillas": apunta a un objetivo pendiente al azar, opción del menú | Sí | General |
| 5 | Solo una puerta por ronda | Sí, como número en `Economy.asset` (`doorsPerRound`) | General |
| 6 | Armas con estrellas 0-3 (pared 0; caja 1-3 con más probabilidad de 3); estación de +1 estrella por 1000; el Microondas es aparte; atravesar hace poco daño sin mejorar | Sí, pero **antes una sesión de diseño**. Hoy la bala hace el mismo daño a todos los que atraviesa (`PlayerShooter`, solo hay caída por distancia) | General, fase propia |
| 7 | Granada graciosa | Sí. Botón R1 (libre). Ideas: dentadura postiza (muerde y aturde), petardo de feria (explota), caramelo de anís (atrae) | General |
| 8 | No se nota la diferencia entre la Chancla y el Lentos-peta | Cierto: 8 perdigones, 40 contra 45 de daño, cargador 6 contra 5; solo cambia la recarga. Se diferencian en la fase 5 | General (datos) |
| 9 | Ver más claro cuál es la bendición y cuál la maldición en la pausa | Sí | General |
| 10 | Venecia: góndola a una parada al azar aunque la zona esté cerrada, una por ronda, no en todas las paradas | **Cambiada**: al azar solo entre zonas **abiertas** (si no, un barrio cerrado sin abuelos es un escondite y una forma de saltarse puertas), una por ronda y por jugador, góndola solo en algunas paradas cada ronda | Venecia |
| 11 | Abuelo con poca vida: se le caen las gafas (más lento) o el bastón (más rápido) | Sí. Online: elegir con la semilla `NetId` | General |
| 12 | Las Fiestas son el mejor mapa; que los demás se parezcan | Guía, no tarea: apuntarlo en `GUIA_NUEVO_MAPA.md` y usarlo en el repaso de Venecia | — |
| 13 | Venecia es demasiado abierta, con líneas rectas largas (de Carnevale a Ferrovia) | Sí: comprobación de líneas de tiro en el setup (aviso a partir de ~25 m) y cortar con edificios, puentes cubiertos y quiebros. La Piazza sigue siendo la zona ancha | Venecia + herramienta |
| 14 | Ferrovia necesita más edificios | Sí | Venecia |
| 15 | Bebidas solo en Cannaregio y el Mercato | Sí | Venecia |
| 16 | El panda rojo corre más con la Furia | Sí | General (datos) |
| 17 y 18 | Tutorial de 5 min en el mapa Original y/o en el Faro | **Cambiada**: un solo tutorial, "Isla del Faro (tutorial)". El Original es el mapa de pruebas y tiene problemas | Faro |

---

## Fases

### Fase 1 — fallos rápidos — HECHA Y PROBADA (2026-11-17)
Hecho en código (sin commitear):
- **Fallo 1:** `Assets/Scripts/Core/CursorLock.cs` (nuevo, se crea solo con `RuntimeInitializeOnLoadMethod`). Cursor
  bloqueado y oculto solo con `GameManager.State == Playing` y la ventana con foco; visible en el resto.
- **Fallo 2 + idea 9:** `GameHUD.ShowPause` en 3 columnas: mapa a la izquierda (500 px, `BuildPauseMap`), botones en
  el centro, `BuildPauseCards` a la derecha (título de la carta en su color, `+ bendición` en verde, `- maldición` en rojo,
  texto rich text con best fit).
- **Fallo 3:** `BoatMission.Vehicle` ("tractor" / "vaporetto" / "barco") y `BoatMission.BoardText`, usados en
  `BossEncounter`, `ExtractionPoint`, `Karaoke`, `ZoneDoor`, `MasterKey`, `Objective()` y el título de la lista del HUD
  (antes las Fiestas decían "PARA QUE VENGA EL VAPORETTO").
- **Fallo 4:** `Tools/Blender/make_map_props.py` `grada()`: el hierro va metido (1.89 x 0.54) y por debajo del asiento.
  `IZ_Grada.fbx` regenerado con Blender CLI (los otros FBX se devolvieron a git, no cambiaban).
- **Fallo 6:** `WorldLabel` ya no hace billboard salvo en objetos que se mueven o se recogen (Rigidbody, `Carryable`,
  `DiscPickup`, `MasterKey`, `MissionItem`, `AmmoPickup`). Los demás miran al lado más abierto (16 rayos de 5 m desde el
  padre, sin agua / jugadores / enemigos, ignorando el propio objeto) y crean una copia trasera "Back" que sigue texto,
  color, tamaño y si se ve o no.
- **Fallo 7:** `BoatMission.TaskList()` para los mapas de gasolina y radio (luz, gasolina, radio, llamar, con aviso de la
  llave maestra); el HUD enseña la lista si `HasTasks || HasMission`.
- **Idea 16:** `AnimalData.furyMoveSpeed` (1.25) aplicado en `PlayerAbility` (RedFury). Descripción actualizada en
  `Data/Animals/RedPanda.asset` y `TestMapSetup`.

**Verificado (2026-11-17):** setups de la Isla y de alturas con el mismo informe; QuickChecks 42 OK; Smoke Isla 15,
Phase1 21 (la comprobación del HUD se adaptó a la lista de tareas), Venecia 56, Villajubilada 52, Base 27, Feedback 26,
IslaZombie 13, Coop 39, Smoke Venecia 15, Smoke alturas 15, todo con 0 FAIL. Capturas (`Feedback1Shots`,
`Renders/Feedback1_*.png`): pausa en 16:9 y 4:3, carteles, gradas. Arreglos que salieron al mirarlas:
- los carteles fijos se veían en espejo y montados con su copia trasera (el shader `WorldText` pintaba las dos caras):
  ahora `_Cull` y un material de una cara (`WorldLabel.OneSidedMaterial`);
- la placa "TAQUILLA" quedaba atravesada: `WorldLabel.ReadFrom(label, dir)` fija el lado de lectura (casetas de spawn);
- en la pausa se ocultan la lista de objetivos, los avisos y los power-ups (se montaban con el título del mapa).

**Lo que quedaba pendiente (ya hecho):**
1. `IslaZombieSetup.Setup()` y `IslaZombieSetup.SetupAlturas()`, cada uno en su propio RunCommand: colisiones nuevas de
   las gradas, mismo informe que antes.
2. Pruebas: `QuickChecks.Run()`, `SmokePlayTest` (Isla y `RunVenecia`), `Phase1PlayTest` (mira las líneas del HUD:
   puede que haya que adaptarla a la lista), `VeneciaPlayTest`, `VillajubiladaPlayTest`, `BasePlayTest`, `CoopPlayTest`.
3. Capturas para revisar a ojo: pausa con mapa y cartas (16:9 y 4:3), carteles fijos (taquilla, puertas, armas de pared,
   máquinas, "ESTÁS AQUÍ"), lista de objetivos en la Isla y en pantalla dividida, gradas del volley.
4. Riesgos que vigilar: un cartel mirando a una pared (si pasa, el padre tiene un colisionador raro: revisar `OpenSide`);
   las sombras negras de los carteles (`TestMapSetup` línea ~224, `IslaZombieSetup` ~1415) deben quedar alineadas con el
   texto, porque todos calculan desde el mismo padre; el cursor bloqueado en el editor durante las pruebas (solo con foco).
5. Si toca código online (no debería): recompilar el cliente y pasar `OnlinePlayTest.RunDirect()`.

### Fase 3 — release v0.1 en GitHub (va después de la 1)
- Commit de la fase 1 (el usuario pidió la release: commit + tag; mensaje con la línea Co-Authored-By).
- `BuildGame` (Tools > Island > Build Windows (.zip)) → `Builds/AnimalIsland_<fecha>.zip` (~85 MB). Abrir el exe y
  comprobar que sale la pantalla de título.
- `gh release create v0.1 <zip> --title ... --notes ...` con notas en español: cómo jugar, online por código, mapas,
  controles; el `LEEME.txt` ya lo explica.
- Antes de publicar, confirmar con el usuario el nombre y el tag. Actualizar `README.md` con el enlace a releases.

### Fase 2 — "donde subes tú, sube el abuelo" (fallos 5 y 8) — HECHA Y PROBADA (2026-11-17)
**Lo que se hizo** (cambia el enfoque de abajo: en vez de decidir sitio por sitio, los abuelos trepan):
- `Editor/ReachCheck.cs` (en el informe de cada setup y en Tools > Island > Check Player-only Spots): muestrea todas las
  superficies, inunda con lo que hace un jugador (salto 1,2 m + escalón 0,3; nunca el súper salto de la rana) y avisa de
  los sitios donde ningún abuelo le puede pegar (golpe a ≤1,3 m en plano y <1,5 m de altura).
- `EnemyAI.UpdateClimb`: si el jugador al que persigue está subido a un prop (`EnemyAI.Perched`), los abuelos a menos de
  9 m (o sin camino hasta él) pasan a la NavMesh "Saltador" y trepan como los senderistas; con `stoppingDistance` 0,1
  para meterse debajo de cajas y máquinas; vuelven a la NavMesh normal 2,5 s después de que nadie esté arriba.
- Máquinas (bebidas, Microondas, karaoke) en la capa Prop (antes Default): se trepan igual.
- Rampa de tablones al techo del bus del Imserso (`BusRamp`), lo que pidió el usuario.
- Villajubilada: el rincón al oeste de la rampa del atrio (24,10-11) pasa a ser casa.
- Resultado: los 4 mapas "none out of the old men's reach". `ClimbPlayTest` (nuevo): Isla 9/9 (bus, cajas, coche,
  avioneta, caseta, máquina, puesto, y un abuelo en el suelo no trepa), Villajubilada (máquina, tractor) y Venecia
  (cajas, máquina, puesto) todo OK.
- No probado: online (trepar va en el host; los clientes ven las posiciones por los snapshots, sin mensaje nuevo),
  Hotel / Crucero (sin tema todavía), el mapa Original (sin malla Saltador: allí no trepan).

**Plan original de la fase 2:**
- Comprobación nueva en el setup (todos los mapas): celdas o techos de props a los que el jugador llega saltando (salto
  normal; el super salto de la rana aparte) que **no** estén en la NavMesh por defecto conectada al suelo. Medir con la
  altura de salto real de `PlayerController` y del Frog (`SuperJump`, `Launch(4f)`).
- Para cada sitio, decidir: **rampa** (por ejemplo el bus del Imserso como mirador arriesgado: rampa de equipaje hasta el
  suelo, con su NavMesh, o un `NavMeshLink` de subida para los abuelos) o **que no se pueda subir** (colisionador alto o
  inclinado: alas del avión, techos).
- Empezar por la Isla del Faro (avión inicial, bus del Imserso), luego Faro (alturas), Venecia y Fiestas. Las góndolas y el
  campanile son excepciones permitidas.
- Pruebas: la comprobación sin sitios a los que solo llega el jugador; una prueba que sube al jugador al bus y mira que un
  abuelo llega hasta él; el `HeightsPlayTest` y los Smoke de cada mapa.

### Fase 4 — gameplay general
- **Una puerta por ronda (idea 5):** `EconomyData.doorsPerRound = 1`, cuenta del equipo por ronda (online: el host),
  mensaje "Ya habéis abierto una puerta esta ronda". **Pregunta abierta:** ¿cuenta la puerta del muelle / misión? (Propuesta:
  no.) Las puertas automáticas (el Portón del encierro) no cuentan.
- **Julio motiva (idea 3):** cada disco puesto en el karaoke da +X % de velocidad a los abuelos (número en Data, por ejemplo
  `EasterEgg` o Economy); aviso en pantalla. Online: se deduce del estado del easter egg en cada máquina.
- **Gafas y bastón (idea 11):** por debajo de ~25 % de vida, 50/50 con la semilla `NetId`: se caen las gafas (velocidad
  x0.6) o el bastón (x1.4, sin golpe de bastón); el objeto cae con física (solo efecto visual). Números en `EnemyVariant` o
  en un asset nuevo. Ni el jefe ni los senderistas, quizá.
- **Brújula de mierdecillas (idea 4):** opción en el menú principal y en Ajustes (`GameSettings.Compass`); flecha arriba
  en el centro del HUD de cada jugador que apunta a un objetivo pendiente al azar (`BoatMission`: tareas, piezas, emisora,
  luz; o la puerta más cercana si no hay). Cambia de objetivo cuando ese se cumple. Local, sin red.
- **Granada (idea 7):** botón R1 / G nuevo en `InputSystem_Actions` (acción Throw); 2 por jugador, se rellenan con la
  Nevera o en una máquina. Recomendación: **dentadura postiza** (explota mordiendo en un radio y aturde). Online:
  `EnemyHits` como el resto del daño; el efecto visual por `NetPlayer`. Si cambia un mensaje, **subir el protocolo**
  (ahora `animal-island-online-5`) y recompilar el cliente.
- **Selector de mapas en carrusel (fallo 9):** rehacer `MainMenu.OpenMapSelector`: tarjeta central grande con imagen,
  nombre y descripción de `MapData`, las vecinas más pequeñas, movimiento animado con ◄ ► o el stick, ratón y teclado.
  Revisar `MapSelectorPlayTest` y `CoopPlayTest` (usan el selector).
- Pruebas: Smoke de todos los mapas, Coop, `OnlinePlayTest.RunDirect` (cliente recompilado), QuickChecks con líneas
  nuevas (`doorsPerRound`, números de la granada y de las gafas o el bastón).

### Para más adelante
- **Fase 5 — armas con estrellas (ideas 6 y 8):** primero una sesión de diseño con el usuario (qué sube cada estrella, tope
  3, modelo y lugar de la máquina de +1 estrella en los 5 mapas, daño al atravesar sin mejorar y mejorada). Toca la red
  (el arma lleva sus estrellas).
- **Fase 6 — repaso de Venecia (13, 14, 15, 10 cambiada).**
- **Fase 7 — tutorial "Isla del Faro (tutorial)" (17/18 cambiada).** Pregunta abierta: ¿se quita el mapa Original del
  selector?
- **Fase 8 — presets gráficos (2 cambiada).**

## Preguntas abiertas (preguntar al usuario cuando toque)
1. Las gradas: ¿el fallo era en el Faro normal, en el de alturas o en los dos? (El arreglo cubre los dos.)
2. ¿La puerta del muelle o de la misión cuenta para "una puerta por ronda"?
3. ¿Vale la góndola solo a zonas abiertas?
4. Tutorial: ¿solo en el Faro? ¿Se quita el Original del selector?
