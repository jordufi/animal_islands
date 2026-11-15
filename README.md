# Animal Island

Juego **cooperativo de supervivencia por rondas** estilo *Zombies* (Black Ops) en tercera persona, hecho con
Unity. Animales de un resort de playa contra **jubilados hostiles**, con tono irónico. Se juega en solitario,
en pantalla dividida (2-4) u online (2-4 por código de sala).

## El juego
- Una isla turística tomada por jubilados. Sobrevive rondas, gana **conchas** (puntos) y abre puertas para
  desbloquear zonas.
- **Misión del barco**: da la luz en el cuadro eléctrico (sin luz no hay bebidas ni Microondas), encuentra la
  **gasolina** y la **radio** (la Llave Maestra vale por una de las dos), llama al barco desde la emisora y aguanta
  hasta que llegue. Entonces se abre el **Muelle**: vence al jefe (*Don Anselmo, presidente de la comunidad*) y
  **extráete** en el barco de rescate.
- **Power-ups** al matar abuelos: Nevera de munición, Siesta fulminante (mueren de un golpe), 2x1 en conchas y
  Paella para todos (acaba con todos los abuelos).
- Cada puerta abierta ofrece **1 de 3 cartas** (bendición + maldición) para toda la partida.
- Armas en la **Máquina de Granizados** (una vez por zona), **Pack-a-Punch** en el Microondas del Chiringuito,
  bebidas con efectos secundarios, ukelele cuerpo a cuerpo.
- 5 animales con pasiva y habilidad activa. Rondas nocturnas cada 5 rondas.
- Modos: **Clásico** y **Anselmo cagón** (la mitad de los abuelos usan atajos por las entradas).
- Mapas: **Isla del Faro** (principal) y **Original** (mapa de pruebas).

## Requisitos
- **Unity 6.6 (6000.6.4f1)**, URP, Input System. Windows 11.
- Mando PS4 (DualShock 4) recomendado; teclado y ratón también funcionan.
- Online: Netcode for GameObjects + Unity Relay (el anfitrión crea la sala y comparte un código de 6 letras).
  El proyecto debe estar enlazado a Unity Cloud para que Relay funcione.

## Abrir el proyecto
1. Clona el repositorio y ábrelo con Unity Hub (la primera vez Unity regenera la carpeta `Library`, tarda un rato).
2. Abre `Assets/Scenes/MainMenu.unity` y pulsa Play.

## Estructura
| Ruta | Contenido |
|---|---|
| `Assets/Scripts/` | Código del juego (Core, Player, Combat, Rounds, Zones, Shop, Cards, Net...) |
| `Assets/Data/` | ScriptableObjects con **todo el balance** (armas, enemigos, economía, cartas...) |
| `Assets/Scenes/` | `MainMenu`, `IslaZombie` (Isla del Faro), `BeachIsland` (Original) |
| `Assets/Maps/IslaZombie/IslaZombie_Layout.txt` | El mapa de la Isla del Faro como rejilla ASCII |
| `Tools/` | Scripts de compilación y copia de seguridad; scripts de Blender en `Tools/Blender/` |
| `CLAUDE.md` | Contexto técnico y decisiones de diseño detalladas del proyecto |

## Herramientas (menú Tools > Island)
- **Setup Isla Zombie**: reconstruye la escena desde el layout y comprueba el mapa.
- **Setup Test Map**: regenera datos, prefabs y la escena Original.
- **Play Test ...** (Base, Isla Zombie, Feedback, Coop, Original, Phase 1, Online): pruebas automáticas jugando.
- **Build Windows (.zip)**: exporta el juego a `Builds/` (también `Tools\Build-Game.ps1` con Unity cerrado).

## Control de versiones
Se versiona solo el código y los assets (~20 MB). **No se suben** `Library/`, `Temp/`, `Builds/`, `Logs/`,
`UserSettings/` ni `Renders/` (se regeneran). No se usa Git LFS: ningún archivo supera los 40 MB.
