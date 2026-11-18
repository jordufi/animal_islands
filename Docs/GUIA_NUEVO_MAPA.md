# Guía: hacer un mapa nuevo desde cero

Para quien construya el siguiente mapa (persona o modelo). Recoge cómo se hicieron La Serenísima y Fiestas de
Villajubilada: el orden de trabajo, qué archivos tocar, qué pruebas pasar y qué hace que un mapa sea divertido.
Léela entera antes de empezar. `CLAUDE.md` tiene el detalle de cada sistema; esto es el camino.

Ejemplo completo para copiar: Villajubilada.
- Mapa: `Tools/Maps/make_villajubilada_layout.py`
- Modelos: `Tools/Blender/make_villajubilada_props.py`
- Tema: `Assets/Scripts/Editor/IslaZombieSetup.Pueblo.cs`
- Prueba: `Assets/Scripts/Editor/VillajubiladaPlayTest.cs`

---

## 1. Antes de escribir nada: el diseño

Un mapa no es un decorado nuevo. Es **una idea de juego** con un decorado encima. Decide estas siete cosas con el
usuario antes de tocar un archivo y apúntalas en `CLAUDE.md` como "decidido, no volver a preguntar".

| Hueco | Pregunta | Isla del Faro | La Serenísima | Villajubilada |
|---|---|---|---|---|
| Forma | ¿Qué dibujo hace el mapa visto desde arriba? | isla redonda, dos rutas | ocho barrios y un cementerio en medio | anillo de calles alrededor de una plaza |
| Puertas | ¿Qué es una puerta de pago aquí? | barreras de tela | puentes | barricadas de las peñas |
| Evento | ¿Qué pasa cada 5 rondas? | noche con senderistas | acqua alta | encierro |
| Abuelos | ¿Qué variantes propias tiene? | chándal, culturista | máscaras de carnaval | el del bombo, la de los petardos |
| Movimiento | ¿Hay alguna forma nueva de moverse? | — | góndola, campanile | — |
| Misión | ¿Qué hay que hacer para que venga el rescate? | luz, gasolina, radio, emisora | lista de 5 tareas | lista de 5 tareas |
| Salida | ¿En qué te vas y contra quién? | barco, Don Anselmo | vaporetto, el Dux | tractor, el pregonero |

**El modelo a seguir es Villajubilada** (feedback de los amigos, 2026-11-17: "es el mapa más divertido; me gustaría que
el resto fueran un poco de este estilo"). Lo que la hace distinta: dos actos, calles estrechas que giran, una plaza
ancha rodeada de un anillo, premios en los fondos de saco, cada zona con su luz y su momento (el encierro). Venecia se
repasó con esa idea (fachadas sobre el agua, quiebros) porque los amigos la encontraron demasiado abierta.

**Lo que mejor ha funcionado: que el evento cambie la estructura de la partida y no solo el ambiente.** En
Villajubilada el primer encierro cierra la zona inicial para siempre y abre el pueblo: la partida tiene dos actos.
Fue idea del usuario y vale más que cualquier modelo bonito. Busca ese momento en cada mapa: algo que pase una vez,
que se vea venir y que cambie lo que el jugador tiene que hacer.

### Reglas de diseño que no se discuten
- **Los abuelos siempre pueden seguir la ruta del jugador.** Cualquier atajo, altura o vehículo nuevo necesita su lado
  enemigo (NavMesh o `NavMeshLink`). Únicas excepciones aceptadas: la góndola y el campanile de La Serenísima.
- **Nada de sitios seguros.** Un rincón al que solo se llega por un lado es un sitio donde acampar. El setup lo mide
  (cobertura de spawns).
- **Que no se vea venir a los abuelos de lejos.** Calles de 2 a 4 m, giros, cosas que tapan. Si una zona se ve muy
  abierta en la captura, añade obstáculos antes de tocar otra cosa. El setup avisa de las líneas de tiro de 40 m o más
  (`IslaZombieSetup.SightLines.cs`); marca tu zona ancha con la bandera `ancha` para que no cuente.
- **Donde subes tú, sube el abuelo.** Si el jugador se sube a un prop, los abuelos trepan detrás (malla Saltador). El
  setup avisa de cualquier sitio al que solo llega el jugador (`ReachCheck`).
- **Un contraste claro: una zona ancha y una estrecha.** Piazza / Cannaregio, Plaza Mayor / Barrio de las Cuestas.
- **Las entradas de abuelos se ven y son predecibles**, todas del mismo color, y nunca se confunden con una puerta de pago.
- **Tiene que ser difícil.** Si dudas entre dos números, elige el que aprieta más. Se ajustará con partidas reales.
- **Humor.** Nombres con gracia en castellano (Peña El Sintrom, Ponte dei Pensionati). Todo el texto del juego en español.

### Cómo hacer que una zona sea interesante
- Dale **una razón para ir**: bebida, tómbola, arma de pared, disco, llave, tarea. Una zona sin premio no se abre.
- Dale **un circuito**: un anillo para correr en círculos con la horda detrás. Los callejones sin salida se usan para
  esconder premios (riesgo a cambio de recompensa), no como forma general de la zona.
- Dale **dos salidas** cuando se pueda. Zonas de una sola salida: solo el inicio y la del jefe.
- **Alturas con cabeza**: una terraza con dos rampas hace un circuito en dos niveles. Una sola rampa hace una trampa.
- Reparte los premios: lo mejor (el Microondas) lejos del inicio y en un sitio incómodo.
- Lo que el jugador deja atrás tiene que doler un poco y lo que gana tiene que notarse (en Villajubilada pierdes el bar
  y ganas un pueblo entero a oscuras que vas encendiendo zona a zona).

---

## 2. Los archivos de un mapa

| Qué | Dónde | Notas |
|---|---|---|
| Generador del mapa | `Tools/Maps/make_<mapa>_layout.py` | rectángulos → rejilla ASCII; lleva sus propias comprobaciones |
| Mapa generado | `Assets/Maps/<Mapa>/<Mapa>_Layout.txt` | **no se edita a mano** |
| Modelos | `Tools/Blender/make_<mapa>_props.py` → `Assets/Maps/<Mapa>/Models/*.fbx` | Blender en segundo plano |
| Tema | `Assets/Scripts/Editor/IslaZombieSetup.<Tema>.cs` | parcial de `IslaZombieSetup` |
| Reglas propias | `Assets/Scripts/Rounds/`, `Zones/`, `Combat/`, `Boss/` | código de juego nuevo |
| Números | `Assets/Data/*.asset` | nunca en el código |
| Prueba | `Assets/Scripts/Editor/<Mapa>PlayTest.cs` | una sesión de juego automática |
| Ficha del selector | `Assets/Data/Maps/<Mapa>.asset` | se crea en `TestMapSetup.CreateData` |

Formato del layout: `Docs/FASE3_FORMATO.md`. 1 carácter = 2 × 2 m, fila 0 = norte.

---

## 3. Paso a paso

### Paso 1. El mapa en rectángulos
Copia `make_villajubilada_layout.py` y cambia los rectángulos. Trabaja por zonas: `fill` para el suelo, `marks` para
objetos y marcadores, `level` y `ramp` para alturas. Al final van las tablas `ZONES` y `DOORS`.

Ejecuta con `--check` (no escribe nada) hasta que diga `checks OK`. El script comprueba:
- cada zona es una sola pieza y ninguna toca a otra sin puerta;
- cada puerta toca exactamente sus dos zonas;
- todo `s` (spawn) tiene algo sólido al lado; toda `x` (cobertura baja) está fuera de pasillos de una casilla;
- cuadros de luz y armas de pared tienen pared al lado;
- las letras de zona no son caracteres de la leyenda.

Errores que ya se han cometido (no los repitas):
- **Letra de zona = carácter de objeto.** `o` es objeto alto, `t` y `q` ponen techo, `R` es roca, `V W H h A w x` son
  props. Usa letras libres y deja que el script lo compruebe.
- **Máximo 9 puertas.** Son dígitos `1`–`9` y cada una ocupa un área de NavMesh. Si necesitas más pasos, une zonas en
  un mismo `group` y sepáralas con un objeto de juego (el Portón de Villajubilada no es una puerta).
- **Marcadores nuevos**: añádelos a `Markers` en `IslaZombieSetup.cs` y comprueba antes que ningún otro mapa usa ese
  carácter (`I` y `*` ya están cogidos en Isla del Faro; por eso se usaron `%`, `Q`, `&`).
- **Una puerta nunca toca una rampa.** Un pasillo de una casilla nunca lleva cobertura.
- La zona inicial necesita un `B` aunque la caja no aparezca allí (flag `firstbox` en las zonas donde sí aparece).

### Paso 2. Los modelos
Copia `make_villajubilada_props.py`. Reutiliza el `Builder` del script de Venecia (se carga con `exec` del principio
del archivo). Convenciones: origen en el centro de la huella a ras de suelo, frente = −Y de Blender (= +Z en Unity),
tiras de fachada de 2 m de ancho colgadas en el plano y = 0.

```
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python Tools/Blender/make_<mapa>_props.py
```

- Nunca dos caras en el mismo plano (parpadean): separa cada pieza 1,5 cm de la vecina.
- La cobertura baja (`x`) mide como mucho 0,9 m: se tiene que poder saltar.
- Si algo tiene que moverse (patas de toro, hojas de un portón), sácalo en un modelo aparte o constrúyelo con
  primitivas en Unity: el `Builder` exporta una sola malla por archivo.
- Ranuras de material con nombre fijo para recolorear o encender en Unity (`VN_AwningPaint`, `VJ_Bulb_R`, `VN_Glass`).

### Paso 3. El tema
Crea `IslaZombieSetup.<Tema>.cs` con un `MapConfig`, un booleano `static bool Xxx => cfg.theme == "xxx"` y un
`[MenuItem]` que llame a `Setup(config)`. Los ganchos están en `IslaZombieSetup.cs`: busca dónde aparece `Pue` y añade
tu tema al lado. Si tu mapa es de edificios, añádelo a `Urb` y hereda gratis las fachadas, los portales de entrada, los
carteles en poste y la comprobación de cobertura baja.

Lo mínimo que escribe un tema:
- `XxxGroundMaterial` (suelo por zona);
- `XxxBuildProps` (qué modelo va en cada `o`, `x` y demás);
- `XxxBuildDecor` (decoración sin colisión: nada que parezca sólido);
- `XxxBuildRuntime` (objetos de juego propios; se llama **antes** de hornear el NavMesh);
- `XxxAfterChecks` (lo que no debe hornearse ni tallar el NavMesh durante las comprobaciones).

Trampas conocidas:
- **Un `NavMeshObstacle` que talla, talla también en el editor.** Las puertas y cualquier barrera que desaparezca se
  añaden después de las comprobaciones, o el setup creerá que el mapa está partido.
- **Un collider en capa Wall presente al hornear corta el NavMesh para siempre.** Si la barrera se quita en la partida,
  créala después de hornear.
- `NavMeshModifierVolume` "Not Walkable" no corta aquí: usa geometría real.
- Usa siempre `NavAgents.Sample` / `NavAgents.Filter`, nunca `NavMesh.SamplePosition(.., AllAreas)`.
- Nunca `??` ni `?.` sobre objetos de Unity.
- Materiales que brillan: URP apaga `_EMISSION` si el material dice "emisivo es negro". Pon
  `globalIlluminationFlags = BakedEmissive` además de activar la palabra clave.
- El render de PC es Forward+: muchas luces pequeñas sin sombra van bien.
- `TestMapSetup.Asset<T>(ruta, init)` crea un asset de datos y **nunca lo sobrescribe**. Úsalo para los números.

Registra el mapa en `TestMapSetup.CreateData` (lista `d.maps` y su `preview`). El orden de escenas del build sigue el
nombre de los assets de `Data/Maps`: un nombre que quede el último alfabéticamente no mueve los índices de los demás.

### Paso 4. Construir la escena
Desde un RunCommand, comprobando antes que nadie está jugando y que la escena abierta no tiene cambios sin guardar:

```csharp
IslaZombieSetup.SetupVillajubilada();
result.Log("{0}", IslaZombieSetup.LastReport);
```

El informe tiene que salir sin ninguna línea `ERROR`. Lee también los `WARN`: zonas con una sola salida y celdas
"seguras" (abuelos desde una sola dirección). Apunta en `CLAUDE.md` las cifras: entradas, celdas andables, metros hasta
la extracción, celdas seguras.

Después saca capturas (cámara temporal a un `RenderTexture`, `File.WriteAllBytes` a `Renders/`) y **míralas**. La
captura es la única forma de saber si algo se ve mal: un fajín que parece un tablón, un polvo que brilla de noche o
una calle que sigue oscura con la luz dada no los detecta ninguna prueba.

### Paso 5. Las reglas propias
Cada regla nueva es una decisión de red. Antes de escribirla, decide:

| Tipo de cosa | Quién manda | Ejemplo |
|---|---|---|
| Estado del mundo (abuelos, puertas, conchas) | anfitrión, y lo difunde | matar abuelos con la traca |
| Cosas del jugador (vida, posición, daño recibido) | la máquina del dueño | el toro hiere a tu jugador en tu máquina |
| Efectos que se deducen de un estado ya compartido | cada máquina por su cuenta | la marea, el recorrido de los toros |

- Todo lo que depende de `Net.Authority` va detrás de esa comprobación; a los jugadores remotos se les reconoce con
  `PlayerSlot.IsProxy`.
- **No confíes en que el cliente deduzca cuándo empieza algo.** El encierro se deducía del número de ronda y un
  encierro forzado nunca llegó al cliente. El anfitrión avisa del inicio (`NetMsg.Encierro`) y a partir de ahí cada
  máquina hace el resto. Regla: el inicio de un evento se manda; su desarrollo determinista se calcula.
- Si añades un mensaje o un campo a uno existente, **sube `Version` en `NetSession`** y recompila el cliente de pruebas
  (`OnlinePlayTest.BuildClient()`).
- Las muertes que no deben pagar van por `EnemyHealth.TakeDamage(vida + 1, false, true)`; las que pagan, por
  `EnemyHits.ApplyLocal`.
- Si reutilizas un sistema con otras palabras (la lista de tareas), cambia los textos con un "sabor" por escena
  (`BoatMission.flavor`), no dupliques la clase.

### Paso 6. La prueba del mapa
Copia `VillajubiladaPlayTest.cs`. Es una máquina de pasos sobre `EditorApplication.update`: cada `case` hace una cosa,
comprueba con `Check(condición, "frase")` y pasa al siguiente con `Next()`.

Qué tiene que cubrir, como mínimo:
1. Ronda 1 arranca y salen abuelos.
2. Cada regla propia: que hace lo que dice **y que no hace lo contrario** (el jugador en la calle recibe el golpe, el
   jugador en el portal no; la traca cobra, mata dentro de su franja, se queda en recarga).
3. Lo que no debe pasar nunca: nadie entra en una zona cerrada, ninguna muerte de evento paga, nadie queda atrapado.
4. La misión entera hasta el rescate, la última puerta, el jefe y la extracción (`GameState.Victory`).
5. **Paseos con el cuerpo del jugador** (`Walk`) por todas las rampas, puertas, portales y callejones: el NavMesh y
   la cápsula del jugador no siempre coinciden.
6. 0 errores en la consola.

Trucos y trampas:
- `player.GodMode = true` al principio; quítalo solo para medir un daño concreto.
- Para forzar cosas hay ganchos (`ForceNext`, `rm.DebugEndRound()`, `rm.SpawnEnemy(variante, punto)`, `door.DebugOpen()`).
  Si tu regla no tiene gancho, añádelo.
- **Los subpasos no pueden usar números que caigan dentro de la secuencia principal.** Usar 20 y 21 como subpasos del 2
  hizo que la prueba saltara del 21 al 22 y se dejara medio mapa sin probar, dando "todo OK". Usa 200, 201 y vuelve con
  `step = 3`. Cuenta siempre los OK: si salen menos de los esperados, la prueba se ha saltado algo.
- Una prueba que pasa a la primera no prueba nada hasta que has visto un fallo suyo. Lee el registro entero la primera vez.
- Si una comprobación falla, decide primero si falla el juego o falla la prueba (el jugador reanimado tarda un segundo
  en levantarse; el jugador estaba lejos de las ventanas). Arregla lo que corresponda, no la frase.

### Paso 7. Cooperativo y online
- `CoopPlayTest`: añade `Run<Mapa>()` con su `mapId` y un bloque propio. Prueba el caso feo: varios jugadores y uno
  **caído** en el momento del evento.
- `OnlinePlayTest`: añade `RunDirect<Mapa>()` con su guion. Para ver el estado del cliente, añade campos al informe de
  `Net/NetTestBot.cs` (`Kv("clave", valor)`) y recompila el cliente. Comprueba cada regla **desde el cliente**: que la
  ve, que le afecta y que lo que hace llega al anfitrión.

---

## 4. Las pruebas: cuáles, cuándo y cómo

### Cómo se lanzan
Desde un RunCommand (`internal class CommandScript : IRunCommand`):

```csharp
if (EditorApplication.isPlaying || EditorApplication.isCompiling) { result.LogError("busy"); return; }
VillajubiladaPlayTest.Run();
```

Se espera desde Git Bash mirando el registro de Windows, sin dormir a ciegas:

```bash
until reg query "HKCU\Software\Unity Technologies\Unity Editor 5.x" | grep -i "VillajubiladaPlayTest.Status" | grep -qv "72756E6E696E6700"; do sleep 5; done
```

(`72756E6E696E6700` = "running", `646F6E6500` = "done", `6661696C656400` = "failed".) Luego se lee el registro con otro
RunCommand: `EditorPrefs.GetString("<Prueba>.Log")`.

### Reglas que evitan perder horas
- **Nunca edites un script con una prueba en marcha.** Unity recompila, la prueba muere en silencio y se queda en
  "running" para siempre.
- Después de editar scripts: `AssetDatabase.Refresh()` desde un RunCommand y espera a que
  `Library/ScriptAssemblies/Assembly-CSharp(-Editor).dll` sea más nuevo que el archivo editado. Mira `error CS` al
  final de `Logs/Editor.log`. Si hay un error de compilación, `EnterPlaymode` se niega y la prueba no arranca.
- Setup y prueba en RunCommands separados si el setup ha cambiado assets.
- Para parches grandes de código usa un script de Python guardado en el scratchpad y ejecútalo. Un heredoc de Bash con
  un número impar de comillas simples dentro falla.
- Comprueba `EditorApplication.isPlaying` antes de nada: el usuario puede estar jugando.

### Qué pasar y cuándo
| Momento | Pruebas |
|---|---|
| Entre cambio y cambio | `QuickChecks.Run()` (instantáneo) y la prueba de tu mapa |
| Tras tocar código compartido (misión, luz, abuelos, zonas) | `SmokePlayTest.Run()` y `RunVenecia()`, y la prueba larga de cada mapa cuyo sistema hayas tocado |
| Antes de dar el mapa por hecho | todo lo de la tabla de abajo |

Lista completa antes de un commit (resultados de referencia del 16/11/2026):

| Prueba | Qué mira | OK esperados |
|---|---|---|
| `QuickChecks.Run()` | reglas sobre los números de `Assets/Data` | 0 fallos |
| `SmokePlayTest.Run()` / `RunVenecia()` | partida corta entera | 15 / 15 |
| `<Mapa>PlayTest.Run()` | tu mapa | Villajubilada 52 |
| `VeneciaPlayTest.Run()` | La Serenísima | 56 |
| `Phase1PlayTest.Run()` | misión, luz, power-ups | 21 |
| `BasePlayTest.Run()` | colisiones, cámara, entradas, noche | 28 |
| `FeedbackPlayTest.Run()` | caja, armas de pared, Anselmo cagón | 26 |
| `IslaZombiePlayTest.Run()` | Isla del Faro entera | 13 |
| `CoopPlayTest.Run()` / `Run<Mapa>()` | pantalla dividida | 39 / Villajubilada 18 |
| `OnlinePlayTest.BuildClient()` y luego `RunDirect()` / `RunDirect<Mapa>()` | online con un cliente real | 69 / Villajubilada 16 |

`OnlinePlayTest.RunDirect` tiene fallos intermitentes conocidos (chanclas de noche, la tirada de la caja, la nevera
del amanecer). Si falla una sola línea que no has tocado, repítela antes de investigar, y dilo en el informe.

**Cada número nuevo con una regla de diseño detrás lleva una línea en `QuickChecks`** ("la manada no se puede dejar
atrás corriendo", "una maceta es lenta y se esquiva"). Así un ajuste de equilibrio que rompa la intención salta al instante.

---

## 5. Contar la verdad al acabar

Al terminar, actualiza `CLAUDE.md` (sección del mapa) y `README.md`, y di al usuario:
- qué pruebas se han pasado y con qué resultado, **con el código final** (si retocas algo después, repite);
- qué no se ha pasado y qué no se ha comprobado;
- qué falló y cómo se arregló, y qué fallo queda sin explicar;
- qué se ve flojo en las capturas;
- que el equilibrio son primeras cifras hasta que lo juegue una persona.

Commit y push solo cuando el usuario lo pida.

## 6. Lista corta

1. Diseño con el usuario: los siete huecos y el momento que cambia la partida.
2. Layout en rectángulos hasta `checks OK`.
3. Modelos en Blender en segundo plano.
4. Tema, ficha del selector, setup con 0 errores, capturas miradas.
5. Reglas propias, decidiendo antes quién manda en red; números en `Assets/Data`.
6. Prueba del mapa con paseos; líneas nuevas en Quick Checks.
7. Cooperativo y online del mapa (cliente recompilado, protocolo subido si cambió).
8. Regresión completa con el código final.
9. `CLAUDE.md`, `README.md` e informe honesto.
