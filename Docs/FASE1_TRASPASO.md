# Fase 1 — traspaso a Sonnet / Opus (2026-11-14)

> **2026-11-15: terminada por Opus.** Pasos 1–7 hechos (mapas reconstruidos, check de la misión en el setup, tests
> adaptados, `Phase1PlayTest`, regresión completa en verde). Cambios sobre lo de abajo: la línea de objetivo del HUD
> se bajó a y = -200 (se pisaba con la vida); `ClearSpot` descarta alturas > 3,2 m (la radio caía encima del templo).
> Sigue pendiente lo de la sección 4.

Fable escribió el código de la Fase 1 («estructura de la partida») pero **no reconstruyó los mapas ni pasó ningún
play test**. El código compila (ensamblados de las 19:44 del 2026-11-14, sin `error CS`). Nada de la Fase 1 está
commiteado: el último commit es `68c5d17` (Fase 0). Este documento es la lista de trabajo para terminarla.

## 1. Qué hay escrito

| Pieza | Dónde | Qué hace |
|---|---|---|
| Misión del barco | `Scripts/Zones/BoatMission.cs` (junto al GameManager) | Estado: `Power`, `Gas`, `Radio`, `Called`, `DefenseUntilRound`, `Complete`. `PowerOn` / `DockOpen` son estáticos y valen `true` si el mapa no tiene cuadro / emisora. `Objective()` da el texto del HUD. `DebugComplete()` lo salta todo. |
| Dar la luz | `Zones/PowerSwitch.cs` | Interactuable. Sin luz, `DrinkMachine` y `PackAPunch` no dejan comprar («SIN LUZ»). |
| Piezas | `Zones/MissionItem.cs` (`Kind.Gasolina` / `Kind.Radio`) | Recogida para todo el equipo; no se lleva en la mano ni se cae. |
| Emisora | `Zones/CallStation.cs` | Con luz + las dos piezas (o una pieza + Llave Maestra, que se gasta) llama al barco. El barco llega al acabar la ronda `ronda actual + missionDefenseRounds`. |
| Muelle | `Zones/ZoneDoor.cs`, campo `requiresMission` | Cerrado hasta `Complete`; después se abre gratis. `acceptsMasterKey` ya no se pone en ninguna puerta. |
| Llave Maestra | `MasterKey.cs`, `PlayerHUD.cs` | Solo cambian los textos. En Isla del Faro se quitó el punto `L` de la Cueva (quedan Volley y Mercadillo). |
| Jefe | `Boss/BossAI.cs`, `EnemyHealth.PaysHits`, `EnemyHits.Award` | Los vecinos convocados no pagan conchas por impacto (`EconomyData.bossMinionsPayHits = false`). |
| Power-ups | `PowerUps/PowerUpManager.cs`, `PowerUps/PowerUpData.cs` | `PowerUpKind`: MaxAmmo, InstaKill («Siesta fulminante»), DoublePoints («2x1 en conchas»), Nuke («Paella para todos»). Tope por ronda y activos. `AmmoPickup` conserva el nombre y lleva `Kind`. |
| Economía | `Core/EconomyData.cs` | Campos nuevos: `bossMinionsPayHits`, `missionDefenseRounds`. |
| HUD | `UI/GameHUD.cs` | `objectiveText` (bajo la ronda) y `buffText` (bajo las conchas). En pantalla dividida el objetivo va en la franja central. |
| Debug | `UI/DebugMenu.cs` | Botón «Dar la luz y traer el barco (misión hecha)». |
| Online | `Net/Net.cs`, `NetGame.cs`, `NetSession.cs` | Mensajes `Quest` y `Buffs` (al final del enum), `PowerUpSpawn` lleva el tipo, `WorldInit` añade misión y temporizadores. `Version` = `animal-island-online-2`. |
| Editor | `TestMapSetup.cs` | `BuildPowerSwitch`, `BuildMissionItem`, `BuildCallStation`, `ClearSpot`; `BuildSystems` añade `BoatMission` y enlaza `Data/PowerUps.asset` (se crea al ejecutar el setup). Original: objetos en Jungla (luz), Playa (gasolina), Cementerio (radio), Campamento (emisora); puerta del Muelle con `requiresMission`. |
| Editor | `IslaZombieSetup.cs` + layout | Marcadores nuevos: `E` cuadro, `Y` gasolina, `T` radio, `C` emisora. En el mapa: `E` (30,6) y `C` (29,9) en el Faro, `Y` (16,10) en el Parking, `T` (21,19) en la cámara sur del Templo. La bandera `llave` de la puerta 7 ahora significa `requiresMission`. |

## 2. Lista de trabajo (en este orden)

1. **Reconstruir los dos mapas**, cada uno en su RunCommand: `IslaZombieSetup.Setup()` (leer `LastReport`) y
   `TestMapSetup.SetupAll()`. Mirar en la consola las líneas `[TestMap] misión: ... alcanzable`.
2. **Añadir al check del setup de Isla del Faro** que `E`, `Y`, `T` y `C` están en la malla y son alcanzables dentro
   de su zona (igual que los puntos de llave, ~línea 1410 de `IslaZombieSetup.cs`). Hoy no se comprueba.
3. **Adaptar los tests que usaban la llave para abrir el muelle** (ahora `AcceptsMasterKey` es `false` en todas las puertas):
   - `IslaZombiePlayTest.cs` ~118–135: buscar la puerta con `RequiresMission`; hacer la misión de verdad (luz, dos
     piezas, llamar, `DebugEndRound` hasta `Complete`) y comprobar que el muelle abre gratis y sale el jefe.
     Añadir el caso «una pieza + llave».
   - `OnlinePlayTest.cs` ~502–515 y `NetTestBot.cs` ~209: lo mismo en online; que el cliente recoja una pieza y el
     host vea el estado, y al revés.
4. **Tests que compran bebidas o usan el Microondas** (`CoopPlayTest`, `OnlinePlayTest`, `IslaZombiePlayTest` si
   aplica): dar la luz antes (`BoatMission.Instance.TurnPowerOn()`), o fallarán con «sin luz».
5. **Test nuevo `Phase1PlayTest`** (o ampliar `FeedbackPlayTest`), con EditorPrefs propios:
   - Sin luz: bebida y Microondas rechazan la compra y no cobran. Con luz: compran.
   - Muelle cerrado antes de la misión (prompt con el objetivo) y gratis después.
   - `CallBlocker` en cada estado; la llamada con llave gasta la llave.
   - Defensa: llamar a mitad de ronda N → completo al acabar N+1; llamar en el intermedio tras N → al acabar N+1.
   - Siesta fulminante: un tiro de pistola mata a un abuelo de ronda 10; no al jefe. Dura `instaKillSeconds`.
   - 2x1: una baja paga el doble; un gasto no cambia.
   - Paella: mueren todos menos el jefe, +`nukePoints` una sola vez, y esas muertes no sueltan más power-ups.
   - Tope: no más de `maxPerRound` por ronda ni `maxActive` a la vez.
   - Vecinos del jefe: un impacto no letal no suma conchas; la baja sí.
   - Captura del HUD con objetivo y power-ups activos (solo y pantalla dividida a 2 y 4).
6. **Regresión completa**: `BasePlayTest`, `IslaZombiePlayTest`, `FeedbackPlayTest`, `CoopPlayTest`,
   `MapSelectorPlayTest.RunOriginal`, `OriginalMapPlayTest`; online: `OnlinePlayTest.BuildClient()` **antes**
   (cambió el protocolo) y luego `RunDirect()`.
7. **Docs**: CLAUDE.md (estado, tabla de arquitectura: `BoatMission`, `PowerSwitch`, `MissionItem`, `CallStation`,
   `PowerUpData`; la decisión «Muelle 10.000 o llave» queda sustituida), README, marcar la Fase 1 en el plan.
8. **Commit** solo cuando el usuario lo pida.

## 3. Riesgos que Fable vio en su propio código (revisar)

- **Posiciones en Original sin verificar.** `ClearSpot` busca el hueco seco y libre más cercano a (-28,-3), (8,2),
  (-9,-6) y (-4,14). Puede caer en otra zona o pegado a una frontera; si el log dice «NO es alcanzable», mover la coordenada.
- **Solapes en el HUD.** `objectiveText` está en y = -160 bajo la ronda: en solo, la vida de J1 vive por ahí.
  `buffText` (y = -138) queda entre el texto del huevo (-100) y la llave de `PlayerHUD` (-180). Mirar capturas.
- **Postes sin colisión.** El cuadro y la emisora son primitivas sin collider (para no cerrar pasillos de 1 celda).
  Si se ve raro atravesarlos, darles un collider fino y repasar el check de pasillos.
- **`E` junto al faro**: se apoya en la celda `H`; comprobar que no queda dentro del modelo del faro.
- **Radio en un pasillo de 1 celda** del Templo (cámara sur, entre `t` y un spawn `s`): comprobar que el abuelo que
  sale de la grieta no tapa la recogida.
- **Online, jugador que carga tarde**: `ApplyWorld` lee misión y temporizadores al final del mensaje; comprobar que
  el orden coincide con `WorldMsg`.
- **`BoatMission.Awake`** decide `HasSwitch` / `HasMission` con `FindAnyObjectByType`; si algún día los objetos se
  instancian después, no los verá.
- **Paella en online**: mata con `TakeDamage` en el host; comprobar que los clientes reciben todas las muertes.
- **Siesta fulminante y cuerpo a cuerpo / habilidades**: pasa por `EnemyHits.ApplyLocal`, así que también aplica; confirmarlo.
- **`DebugOpen` de las puertas** sigue abriendo el muelle sin misión (lo usan tests y el menú debug): es intencionado.

## 4. Lo que NO se hizo de la Fase 1 del plan

- **Power-up «Bingo»** (arma de caja temporal para todos): requiere armas temporales y su reenvío online. Pendiente.
- **Vida del jefe**: no se tocó. Ya escala con la ronda (`healthInEnemies` 25 × vida del abuelo, mínimo 8.000), pero
  es poca: en ronda 7 son 18.750, unos 20 s de Thermomix sin mejorar. Subirla es trabajo de la Fase 2 (ver el plan).
- **Run log**: no registra los pasos de la misión ni los power-ups cogidos. Añadir eventos «LUZ», «GASOLINA»,
  «RADIO», «LLAMADA», «BARCO», «POWERUP <tipo>» con ronda y tiempo: hacen falta para afinar la Fase 2.
- **Criterio «jefe en ronda ≥ 12»**: no se puede cumplir solo con la Fase 1; depende de los números de la Fase 2.
