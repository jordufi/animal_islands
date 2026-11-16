# Fiestas de Villajubilada — primer diseño (2026-11-16) — SUPERADO

> El usuario cambió el diseño el mismo día (dos actos, portón, luz por zonas) y pidió que lo construyera Fable.
> Lo construido y probado está descrito en `CLAUDE.md`, sección "Fiestas de Villajubilada". Este documento queda
> como registro del primer planteamiento.

Cuarto mapa. Fable diseña, Opus construye y prueba. El usuario pidió "diseña y haz el mapa"; el diseño está cerrado
aquí salvo las **preguntas abiertas** del final, que no bloquean (cada una lleva un valor por defecto). Reglas de
siempre: números en `Assets/Data`, tests antes de dar algo por hecho, no editar scripts con un play test en marcha,
commit solo cuando el usuario lo pida, **los abuelos siempre pueden seguir la ruta del jugador**.

## 1. Idea
Un pueblo de interior, de noche, en plenas fiestas patronales. Los jubilados han tomado el pueblo; los animales
tienen que conseguir que venga el tractor de la peña para salir por Las Eras.
- **Forma:** una plaza grande en el centro, una **manzana de casas** alrededor y un **anillo de calles de 4 m** que la
  rodea. Fuera del anillo: la parada del autobús (inicio), un barrio de callejones en cuesta, el patio de las peñas,
  el atrio de la iglesia, el recinto ferial y Las Eras.
- **Contraste** (como Piazza / Cannaregio): **Plaza Mayor = la ancha** (24 × 20 m, casi vacía); **Barrio de las
  Cuestas = el estrecho** (callejones de una casilla, dos niveles, callejones sin salida). El ferial es grande pero
  troceado por atracciones y puestos.
- **Lo propio del mapa:** el **encierro** (cada 5 rondas, en lugar de noche / acqua alta), las **tracas** (primera
  trampa comprable del juego), **dar la luz enciende las guirnaldas** (el pueblo pasa de oscuro a iluminado), dos
  abuelos nuevos (el del bombo, la de los petardos).
- No hay agua ni caídas al vacío: no hace falta `CanalRescue`, ni vallas de zona, ni suelo de nado.

## 2. Mapa (borrador comprobado)
Generador: `Tools/Maps/make_villajubilada_layout.py` (ya escrito por Fable, **borrador**). Sin argumentos imprime el
mapa y pasa sus comprobaciones sin escribir nada; con `--write` crea `Assets/Maps/Villajubilada/Villajubilada_Layout.txt`.
Rejilla 58 × 46 (116 × 92 m), origen propuesto celda (28, 22). Comprobado por el script (16/11): cada zona es una
sola pieza, ninguna zona toca a otra sin puerta, cada puerta toca exactamente sus dos zonas, todo `s` tiene algo
sólido al lado, ninguna `x` en pasillo de una casilla, el recorrido del encierro no pisa celdas sólidas.
**No comprobado:** NavMesh, cobertura de spawns, anchos reales con los modelos puestos (lo dirá el setup).

```
..#######################################################.
..########################ZZZZZZ############tttttttstttt#.
..########################ZZZZZZ############ttottttttttX#.
..########################ZZZZZZ############stttttbttttt#.
..######################JnsnnnnsnE##########tttotttttttt#.
..######################nnnnnBnnnn##########tttttttttots#.
..######################nnnnnnnnnn##########tttttttttttt#.
..######################snnnnnnnnn##eseeB#######99#######.
..####uuuuuuuuuuu#######nnnnnnnnnn##eeeee#fffffsfffffffB#.
..####s####R####u#######nnnnnonnns##seeeO#ffSfffffffffff#.
..##Luu####u####u#######Dnnxnnxnnn##eeees#fffffffffoofff#.
..##uOu####uuuD#u##usu4snnnnnnnnnnn5ees###fffoooofffffff#.
..##suu####u####u##uuu4nnnnnnnsnnns5eee###sffoooofffffff#.
..####u####u####u##uu#######77#######eRe##fffoooofffffff#.
..####uuuuuuusuuuuuuu#######pp#######ee###fffoooosfffxxf#.
..####u####u####u##su##spppppFpsppP##es###ffffffffffffff#.
..####uuu##u####s##uu##poppoppoppop##ee8fffffffffffffffs#.
..####u#u##U####u##uu##pppppppppppp##ee8ffffffffffffffff#.
..####s#u##u####u##uu##pxpppppppppp##se###ffoofooffoooff#.
..####u#u##uuusuuuuuu##pppppKppppxp##eee##fffffffffoooff#.
..####u#J##u####u##uu##spppppppppps##ee###fRfffffffoooff#.
..####u####s####u##22##pppppppppppp##33###ffffxfffffsfff#.
..####u####u####u##cc##pxxppppppxpp##cc###fffffffffffffM#.
..####uuuuuusuuuB##sc##ppppppppoppp##cc###ffooofffffffff#.
..################ccc##Bppsppppppps##ccc##sfffsffoofffff#.
..#################cc#######pp#######cs###fffffffffffffs#.
..#################cc###c###66###c###cc###ffffffffffxfff#.
..#################cccccccscccccccccRcs###fDffffffffffJf#.
..#################scccccccccccsccccscc###OfffffffsffffL#.
..##################cccccc##cc###G#c#####################.
..##################DsxcxB##11###########################.
..#########################saaa##########################.
..#########################aaas##########################.
..######################aaaaaaaaas#######################.
..######################s@aaaaaoaa#######################.
..######################aaxaaaaaaa#######################.
..######################aaxaaaaaaa#######################.
..######################aaaaaaaaas#######################.
..######################aaaaVVaaaa#######################.
..######################asaaaaaasa#######################.
```
(filas 1 a 41 de 46; el resto son casas y campo.)

### Zonas (8, cada una su grupo)
| Letra | Zona | m² | Qué es | Recompensas |
|---|---|---|---|---|
| `a` | La Parada (inicio) | 300 | marquesina, camión de ganado (`V`: de aquí salen los toros) | — |
| `c` | Calle Mayor | 320 | calle sur del anillo + las dos patas bajas + plazuela del bar; 6 portales | bebida, tómbola (`firstbox`), Lentos-peta `G`, traca |
| `u` | Barrio de las Cuestas | 468 | callejones de 1 casilla, mitad oeste a nivel 1 (3 rampas), placita-mirador | bebida, tómbola, arma `U`, disco, llave, traca |
| `e` | Calle de las Peñas | 176 | calle este + patio de las peñas | tómbola (`firstbox`), traca |
| `n` | Calle de la Iglesia | 372 | calle norte + atrio (nivel 1, 2 rampas) ante la iglesia `Z` | **cuadro de luces `E`**, bebida, tómbola, disco |
| `p` | Plaza Mayor | 476 | templete (nivel 1, 4 rampas), soportales del ayuntamiento, mesas | karaoke `K` (en el templete), Microondas `P`, tómbola, balcón `F` |
| `f` | Recinto ferial | 1048 | coches de choque, noria, churrería, puestos | bebida, tómbola, arma `M`, disco, llave, puesto de petardos `S`, traca |
| `t` | Las Eras | 276 | era con pacas de paja: jefe `b` y tractor `X` | — |

### Puertas (9 = el máximo: las áreas de NavMesh 3–11 son las puertas, 12 y 13 ya están cogidas)
| N.º | Nombre | Precio | Une |
|---|---|---|---|
| 1 | Barricada de la Parada | 750 | a – c |
| 2 | Peña El Sintrom | 1000 | c – u |
| 3 | Peña Los del Imserso | 1000 | c – e |
| 4 | Peña La Dentadura | 1500 | u – n |
| 5 | Peña El Bastón | 1500 | e – n |
| 6 | Arco de la Plaza | 1250 | c – p |
| 7 | Arco del Ayuntamiento | 1250 | n – p |
| 8 | Entrada del ferial | 1500 | e – f |
| 9 | LAS ERAS – JEFE FINAL | misión (`llave`) | f – t |

Las puertas 2, 3, 4 y 5 están **sobre el anillo**: con las cuatro abiertas el anillo se cierra y el encierro da
vueltas completas. La plaza tiene dos entradas (6 y 7), así que hay tres lazos: anillo, anillo-plaza por el sur y
anillo-plaza por el norte. Zonas con una sola salida: La Parada y Las Eras (igual que en los otros mapas).

## 3. Reglas nuevas

### 3.1 El encierro (`Rounds/Encierro.cs` + `Data/Encierro.asset`)
Sustituye a la ronda nocturna: en esta escena `RoundManager.nightVariant` = null, como en Venecia.
- **Cuándo:** cada 5 rondas (`every`), o forzado desde el menú de depuración (`ForceNext`, como `AcquaAlta`). La ronda
  de abuelos es normal; el encierro se suma.
- **Recorrido** (lo hornea el setup, como los caminos de góndola): polilínea por el centro de la calle de dos
  casillas. Sale del camión (`V`), sube el tallo hasta la Calle Mayor y da la vuelta al anillo en sentido horario:
  `(29,40) → (29,30) → (20,30) → (20,14) → (38,14) → (38,30) → (29,30)`, en esquinas de celda. 156 m con el tallo,
  anillo de 136 m.
- **Solo corre por zonas abiertas** (regla: nada entra en una zona cerrada). Al empezar, el anfitrión calcula el tramo
  transitable desde el camión: avanza por el recorrido mientras la celda sea de una zona abierta o de una puerta
  abierta, en los dos sentidos del anillo.
  - Anillo completo abierto → `laps` vueltas (3) y de vuelta al camión.
  - Anillo a medias → van hasta la barricada cerrada, **dan la vuelta** y vuelven; lo repiten `laps` veces por cada
    brazo abierto (primero el oeste, luego el este).
  - Puerta 1 cerrada → corren solo el tallo dentro de La Parada (ida y vuelta). Quedarse en el inicio no libra.
- **Aviso** (visible y predecible, como los spawns): toast "¡ENCIERRO! Fuera de la calle" + cohete + el camión
  tiembla `warning` s (6) antes de abrir el portón. Línea de serrín / vallas de madera a lo largo del recorrido para
  que se vea siempre por dónde pasa.
- **Manada:** `bulls` (6) en dos filas de 3, ocupan los 4 m de calle; `speed` 9 m/s (el jugador esprinta a 5: no se
  les gana corriendo, hay que salirse). Un pase completo del anillo dura ~15 s.
- **Daño:** al jugador `playerDamage` 60 + empujón lateral hacia el borde de la calle, con 1 s de inmunidad al
  encierro (no mata de dos golpes seguidos del mismo pase; sí puede tumbar a un jugador herido). A los abuelos: los
  mata (`TakeDamage(.., pierce)`), **sin pagar conchas** y sin soltar power-ups (como la Paella), pero cuentan para
  acabar la ronda. Al jefe no le hace nada (Las Eras no está en el recorrido).
- **Dónde ponerse a salvo:** cualquier celda fuera de las dos casillas de calle: los 8 portales de una casilla
  (6 en la Calle Mayor, 2 en la de las Peñas), las bocacalles, la plaza. Los abuelos entran al portal igual que tú.
- **Recompensa:** al volver los toros al camión, una Nevera de munición (misma lógica que `AcquaAlta.Reward`).
- **Online:** el movimiento es determinista (recorrido horneado + tramo + hora de inicio). Un mensaje nuevo
  `NetMsg.Encierro` (tramo oeste, tramo este, vueltas, tiempo de inicio del anfitrión) y cada máquina mueve los toros
  sola. El anfitrión mata abuelos; **cada máquina comprueba a su jugador local** contra los toros y se aplica el daño
  (los jugadores son del dueño). `WorldInit` lleva el encierro en curso para quien carga tarde. **Subir el protocolo
  a `animal-island-online-5`** y recompilar el cliente de pruebas.
- **Números** (todos en `Data/Encierro.asset`): every 5, laps 3, bulls 6, speed 9, warning 6, playerDamage 60,
  playerImmunity 1, turnPause 1.5 (lo que tardan en dar la vuelta ante una barricada).

### 3.2 Tracas (`Zones/TracaTrap.cs : Interactable`, marcador `R`)
- Un poste con mecha en un extremo de una recta. El setup tiende la traca desde la `R` a lo largo del pasillo, en la
  dirección con más recorrido libre, hasta un giro, puerta u objeto (máximo `tracaCells` 8 = 16 m).
- Pagas (`EconomyData.tracaCost` 750) y arde de un extremo a otro en `tracaBurn` 3 s; lo que pise la franja al pasar
  la llama recibe `tracaDamage` (mata abuelos normales: usar `pierce`; al jefe no). Sus muertes no pagan conchas.
  Al jugador le hace `tracaPlayerDamage` 25. Recarga `tracaCooldown` 90 s (el poste lo indica).
- Hay 4: Calle Mayor (36,29), Cuestas (11,11), Peñas (38,15), ferial (43,22).
- Online: solo el anfitrión cobra y aplica daño a abuelos; difunde el encendido (se puede reutilizar el mensaje de
  `Interact` + un evento visual; si hace falta mensaje nuevo, va en la misma subida de protocolo). Daño al jugador:
  cada máquina al suyo.

### 3.3 Dar la luz enciende las fiestas
- El mapa es **siempre de noche** (luna + linterna de la cámara, como una ronda nocturna de Isla del Faro pero algo
  más clara). `PowerSwitch` con `action` "Encender las luces de las fiestas" en el atrio (`E`, (33,6)).
- Al dar la luz se encienden las guirnaldas y farolillos (`Zones/FiestaLights.cs`: activa un grupo de materiales
  emisivos + pocas luces reales, máximo ~12, y sube el ambiente). Sigue mandando `BoatMission.PowerOn`: sin luz no
  hay bebidas ni Microondas. Online ya viaja en `Quest`.
- Riesgo: que a oscuras no se lea el mapa. Verificar con capturas antes y después (ver §6).

### 3.4 Abuelos nuevos (`Data/Enemies/`, solo en `specialVariants` de esta escena)
- **El del bombo** (`AbueloBombo`): lento (×0.8), vida ×1.5, desde la ronda 3, máximo 2 vivos. Mientras vive, los
  abuelos a menos de `auraRadius` 8 m corren ×`auraSpeedFactor` 1.3. Campos nuevos en `EnemyVariant`; la velocidad se
  recalcula cada 0.5 s en el anfitrión (los clientes solo interpolan, no hay mensaje). Lleva el bombo (`VJ_Bombo`) y un
  aro en el suelo marca el radio.
- **La de los petardos** (`AbuelaPetardos`): desde la ronda 6. Lanza como el senderista (`throwsChanclas` y sus
  números) pero el proyectil deja un parche de petardos 3 s donde cae: reutilizar `GasCloud` con campos nuevos
  `throwCloudRadius` 1.5 / `throwCloudSeconds` 3 / `throwCloudDps` 15 y otro aspecto (chispas, no gas).
- Siguen saliendo el del chándal y el culturista. Sin senderistas ni máscaras.
- Cada número nuevo lleva su línea en `QuickChecks`.

### 3.5 Misión: "el pregón" (lista de tareas, sin código de misión nuevo)
Reutiliza `BoatMission.HasTasks` de Venecia cambiando textos y modelos (hoy los textos de `MissionTask` / `TaskList()`
están pensados para Venecia: **hacerlos configurables por escena**, no duplicar la clase).
| Tarea | En Venecia era | Aquí |
|---|---|---|
| 1 | 3 discos al karaoke | igual, el karaoke está en el templete de la plaza |
| 2 | tocar la campana | dar la luz (`E`) |
| 3 | gondolero de oro | **el cabezudo de oro** (`O`: Cuestas (5,13), patio de las peñas (40,11), ferial (42,30); el anfitrión elige uno) |
| 4 | pala + desenterrar a Giuseppe | **mechero** (puesto de petardos `S`, `shovelCost` 500) + **tirar el chupinazo** desde el balcón del ayuntamiento (`F`, (29,17)) |
| 5 | la llave | igual (`L`: Cuestas (4,12), ferial (55,30)) |
Con 1–4 hechas el tractor viene y llega al final de la ronda siguiente; la barricada de Las Eras se abre gratis con la
llave en el bolsillo. Jefe: Don Anselmo de **pregonero** (fajín rojo y pañuelo, mismo sistema que `DuxMask`).
Extracción: el remolque del tractor (`X`).

### 3.6 Cambios de nombre solo en este mapa
La caja de armas es **"La Tómbola"** (cartel y texto del prompt por escena); las puertas son barricadas de peña con
pancarta del precio; las entradas de abuelos son portales de casa con persiana (el código de fachada de Venecia) del
color único de entrada.

## 4. Trabajo, en orden
1. **Tema `pueblo`** en `IslaZombieSetup`: `MapConfig Villajubilada` (`theme = "pueblo"`, `menu = true`, origen 28/22,
   carpeta `Assets/Maps/Villajubilada`, escena `Villajubilada`), `SetupVillajubilada()` + menú Tools > Island. Nuevo
   parcial `IslaZombieSetup.Pueblo.cs` copiando la estructura de `.Venecia.cs` (suelo, sólidos con fachadas,
   props, decoración, puertas, carteles, entradas). `Markers` gana `R` y `V` (sólido). `.` = campo oscuro, sin mar.
   Ojo: `S F O` ya existen con otro significado; que el tema decida qué construye. Mejor aún: sacar lo común de
   Venecia a funciones compartidas en vez de copiar 1.100 líneas.
2. **Modelos** (`Tools/Blender/make_villajubilada_props.py`, prefijo `VJ_`, mismo nivel que el kit veneciano):
   fachadas A–D (cal, piedra, balcón con geranios, persiana), bar, frente del ayuntamiento (reloj, balcón, banderas),
   frente de iglesia + espadaña, templete, barricada de peña, talanquera, toro (dos poses de galope alternadas,
   sin esqueleto), camión de ganado, tractor + remolque, tómbola, churrería, pista de coches de choque, noria,
   puesto, puesto de petardos, mesa plegable, paca de paja, fuente, marquesina, guirnalda de bombillas, farolillo,
   poste de traca + ristra, bombo, cabezudo (y el de oro), cohete. ~28 modelos.
3. **Escena en gris con el kit** → `SetupVillajubilada()` hasta 0 errores (grupos sellados, 9 puertas, entradas,
   cobertura). Ajustar el layout en el script, no a mano.
4. **Encierro** (§3.1): hornear recorrido en el setup, `Encierro.cs`, datos, toros, aviso, menú de depuración.
5. **Tracas** (§3.2), **luces** (§3.3), **abuelos** (§3.4), **misión y textos** (§3.5–3.6).
6. **Online**: `NetMsg.Encierro`, tracas, protocolo 5, `WorldInit`; recompilar cliente.
7. `MapData` del selector (`Data/Maps/Villajubilada.asset`), mapa del menú de pausa y carteles "ESTÁS AQUÍ".
8. `CLAUDE.md` y `README.md` (añadir el mapa; en la lista de ideas marcarlo como hecho).

## 5. Cosas en las que es fácil equivocarse
- **Letra de zona ≠ carácter de objeto.** El primer borrador usaba `o` para las Cuestas y para los objetos altos:
  por eso la zona es `u`. Comprobar que ninguna letra de `[zones]` coincide con la leyenda.
- Las puertas del anillo cortan las dos casillas de la calle; el recorrido del encierro las cruza. La barricada debe
  quitar su collider al abrirse (ya lo hace) y el recorrido debe tratar la puerta abierta como calle.
- Los portales de una casilla son suelo normal: que el NavMesh entre (radio 0.35) y que ninguna farola o talanquera
  los tape.
- Las tracas y las talanqueras no deben hornearse como obstáculo que estreche el pasillo de las Cuestas.
- El atrio y el templete tienen bordes abiertos (caídas de un nivel con `NavMeshLink` de un sentido): vale, pero
  las rampas necesitan su murete (`BuildRamps`) y ninguna puerta toca una rampa (no lo hace).
- El ferial son 1.000 m² y la regla del juego es no ver venir a los abuelos de lejos: si en las capturas se ve
  demasiado abierto, añadir una fila de puestos (en el script) antes que tocar otra cosa.
- Toros y abuelos: matar con `pierce` y con `PaysHits` / pago desactivado; comprobar que `RunLog` no cuenta esas
  muertes como del jugador.
- La noche permanente: `DayNightCycle` hoy alterna; hace falta un modo fijo por escena y que el encierro no dependa
  de `IsNight`.

## 6. Verificación
- `python Tools/Maps/make_villajubilada_layout.py` → "checks OK".
- `SetupVillajubilada()` → `LastReport` con 0 errores; apuntar entradas, celdas andables, metros hasta la extracción
  y celdas "seguras".
- **`VillajubiladaPlayTest`** nuevo (Tools > Island > Play Test Villajubilada), como `VeneciaPlayTest`:
  ronda 1; las 9 puertas; paseos con el cuerpo del jugador por las 9 rampas, los 8 portales y los callejones de las
  Cuestas; encierro forzado con solo La Parada (tallo ida y vuelta), con medio anillo (dan la vuelta en la barricada,
  0 toros dentro de una zona cerrada) y con el anillo entero (3 vueltas); jugador en la calle recibe 60 y sale
  empujado, jugador en un portal recibe 0; abuelos en la calle mueren sin pagar; Nevera al acabar; traca: cobra,
  mata en la franja, no fuera, recarga 90 s, 25 al jugador; sin luz no hay bebida, dar la luz enciende el grupo de
  luces; aura del bombo (velocidad ×1.3 dentro, ×1 fuera y al morir él); parche de petardos; lista de tareas
  completa, tractor tras la ronda, barricada final con llave, jefe, extracción; 0 errores de consola.
- `SmokePlayTest.RunVillajubilada()`, `QuickChecks` (números nuevos), `MapSelectorPlayTest` con el mapa nuevo.
- Regresión antes de dar por hecho: Smoke en Isla del Faro y Venecia, `VeneciaPlayTest` (se toca `MissionTask` y
  `EnemyVariant`), `Phase1PlayTest`, `CoopPlayTest`, `OnlinePlayTest.RunDirect` + un `RunDirectVillajubilada()` con
  encierro y traca vistos desde el cliente.
- Capturas en `Renders/Villajubilada_*.png`: planta, plaza, una calle del anillo con el recorrido, Cuestas, ferial,
  **a oscuras y con las luces dadas**, y el encierro pasando.

## 7. Preguntas abiertas para el usuario (con valor por defecto si no contesta)
1. **¿Toros o cabezudos?** Por defecto **toros** (se entiende al instante). Los cabezudos serían más ligeros de tono.
2. **¿Las muertes del encierro y de la traca pagan conchas?** Por defecto **no** (como la Paella y las trampas del
   Zombies clásico): son para sobrevivir, no para ganar dinero.
3. **¿Siempre de noche, o atardecer hasta dar la luz?** Por defecto **siempre de noche**, con las luces como premio.
4. **Tamaño:** ~3.400 m² andables (Isla del Faro ~2.000, La Serenísima ~5.500). ¿Vale, o se recorta el ferial?
5. **Precios** de puertas, traca (750) y daño del toro (60): primeras cifras, sin partida real detrás.
