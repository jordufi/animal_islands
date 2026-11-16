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
- Mapas: **Fiestas de Villajubilada** (nuevo, en pruebas: un pueblo en fiestas en dos actos; en la ronda 5 sueltan los toros, se abre el portón, anochece y hay que correr calle arriba sin vuelta atrás; después se enciende el pueblo zona a zona, hay encierro cada 5 rondas, tracas que se compran como trampa y abuelas que tiran macetas), **La Serenísima** (nuevo, en pruebas: Venecia con puentes de pago, canales, góndolas que dan la vuelta al mapa pasando bajo los
  puentes de arco, agua low poly, acqua alta en la que se nada por los canales (y los abuelos te siguen), vallas en los barrios cerrados, abuelos con máscara y un cementerio monumental en el centro), **Isla del Faro** (principal), **Isla del Faro (alturas)** (la misma isla con tres alturas, rampas, caídas,
  toboganes, la cripta del Templo y la sala del tragaluz en la Cueva) y **Original** (mapa de pruebas).

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
| `Docs/GUIA_NUEVO_MAPA.md` | Guía para hacer un mapa nuevo desde cero: diseño, pasos, pruebas y errores ya cometidos |

## Herramientas (menú Tools > Island)
- **Setup Isla Zombie**: reconstruye la escena desde el layout y comprueba el mapa. **Setup Isla del Faro (alturas)** y
  **Setup Prueba de alturas** hacen lo mismo con la copia con alturas y con el mapa de prueba (formato en `Docs/FASE3_FORMATO.md`).
- **Setup Test Map**: regenera datos, prefabs y la escena Original.
- **Quick Checks** (reglas de diseño sobre los números de `Assets/Data`, al instante) y **Play Test Smoke** (partida entera por el camino corto, ~10 s): para entre cambio y cambio.
- **Play Test ...** (Base, Isla Zombie, Feedback, Coop, Original, Phase 1, Online): pruebas automáticas jugando; la regresión completa solo antes de un commit.
- **Build Windows (.zip)**: exporta el juego a `Builds/` (también `Tools\Build-Game.ps1` con Unity cerrado).

## Control de versiones
Se versiona solo el código y los assets (~20 MB). **No se suben** `Library/`, `Temp/`, `Builds/`, `Logs/`,
`UserSettings/` ni `Renders/` (se regeneran). No se usa Git LFS: ningún archivo supera los 40 MB.

## Ideas para próximos mapas (sin hacer, solo ideas)
Cada mapa rellena los mismos huecos que La Serenísima: forma, tipo de puerta, evento cada 5 rondas, abuelos nuevos,
misión y extracción. Regla de diseño: ningún atajo para el jugador sin su versión para los abuelos.

### 1. Hotel Gran Imserso (torre de Benidorm)
Mapa vertical: vestíbulo y piscina amplios abajo, pasillos de habitaciones estrechos arriba y la azotea.
- **Puertas:** rellanos de escalera y botones del ascensor de pago; cada planta es una zona.
- **Ascensor:** se paga por llamarlo, es lento y ruidoso. Los abuelos suben por la escalera o en el otro ascensor.
  Una tolva de lavandería es un tobogán de un solo sentido hasta el vestíbulo.
- **Cada 5 rondas, noche de bingo:** la megafonía canta una planta. Esa planta paga el doble de conchas durante la
  ronda, pero recibe casi todos los abuelos. Aquí encaja el power-up "Bingo" pendiente.
- **Abuelos:** el de la moto de movilidad (rápido en línea recta, no sube escaleras, gira mal) y la abuela con la
  bandeja del bufé como escudo frontal (piernas o rodearla).
- **Extracción:** helicóptero en la azotea, tras dar corriente al ascensor, encontrar la tarjeta maestra y encender
  las luces de la azotea.

### 2. Fiestas de Villajubilada (HECHO: primera versión jugable, ver arriba)
Mapa llano: un anillo de calles alrededor de la plaza, callejones y la iglesia.
- **Puertas:** las barricadas de las peñas.
- **Cada 5 rondas, el encierro:** un cohete avisa y los toros (o cabezudos, más ligero) dan tres vueltas al anillo.
  Arrollan todo lo que haya en el recorrido, abuelos incluidos: puedes llevar a la horda al recorrido o meterte en
  un portal.
- **Primera trampa comprable:** pagar por encender una traca a lo largo de una calle.
- **Abuelos:** el del bombo de la charanga (acelera a los de alrededor mientras toca, objetivo prioritario) y la
  abuela de los petardos (niega una zona).
- **Ambiente y extracción:** la caja de armas es la tómbola; se sale en el remolque del tractor tras las tareas del pregón.

### 3. Crucero "Reina del Imserso"
Mapa largo y estrecho en tres cubiertas: pasillos de camarotes (estrechos) contra la cubierta de la piscina (abierta).
- **Puertas:** mamparos.
- **Cada 5 rondas, marejada:** el barco escora. Todos derivan hacia el lado bajo, las tumbonas sueltas resbalan y
  golpean a jugadores y abuelos, y la mira se balancea. Se puede fingir con una fuerza lateral y giro de cámara,
  sin rotar el mundo.
- **Por la borda:** caerse funciona como el rescate de los canales de Venecia.
- **Abuelos:** el del flotador (los tiros al cuerpo rebotan: cabeza o ukelele) y el grupo de aquagym, que avanza
  en bloque.
- **Extracción:** arriar el bote salvavidas manteniendo la manivela mientras el resto cubre. En solitario, a ratos.

### 4. Ambulatorio "Su turno"
Centro de salud: pasillos estrechos, salas de espera y un aparcamiento en el sótano.
- **Números de turno:** las bebidas y el Microondas atienden por número. Coges ticket y te atienden cuando el equipo
  lleva N bajas, así que las compras se planean.
- **Cada 5 rondas, campaña de la gripe:** todos los abuelos van primero a la sala de vacunas y salen más rápidos;
  puedes emboscar la cola o huir de ella.
- **Abuelos:** el del gotero (mucho alcance) y la abuela del andador (escudo frontal).
- **Extracción:** la ambulancia, con sus llaves y el alta firmada.

### 5. Aqualandia (parque acuático)
Torres de toboganes sobre piscinas; aprovecha el formato con alturas.
- **Río lento:** una corriente en bucle que lleva a jugadores y abuelos. Viaje gratis, pero no eliges compañía.
- **Toboganes:** bajadas de un solo sentido entre zonas.
- **Cada 5 rondas, piscina de olas:** olas periódicas cruzan la zona central y tumban a quien esté de pie en ella.
- **Abuelos:** el de los manguitos, que cruza el agua a toda velocidad.

### 6. Sierra Jubilada (estación de esquí)
Mapa en pendiente: el pueblo abajo y las pistas arriba.
- **Medidor de frío:** lejos de un brasero o un puesto de chocolate demasiado tiempo vas más lento. Castiga
  acampar al raso.
- **Telesilla:** un bucle continuo al que se sube cualquiera, abuelos incluidos. Sentado estás expuesto.
- **Cada 5 rondas, ventisca:** la visibilidad baja a unos 10 m.
- **Abuelos:** el esquiador, muy rápido cuesta abajo y lento cuesta arriba, así que la altura importa.

### 7. El Corte Jubilés en rebajas (centro comercial)
Tres plantas alrededor de un atrio, con tiendas sin salida.
- **Escaleras mecánicas:** cintas de un solo sentido para los dos bandos.
- **Cada 5 rondas, rebajas:** todo cuesta la mitad durante la ronda, pero salen el doble de abuelos. La economía
  se vuelve una decisión de riesgo.
- **Carritos:** se empujan como barricada móvil o ariete.
- **Abuelos:** la abuela que embiste en línea recta con su carrito.
- **Extracción:** en coche desde la planta P-3, tras validar el ticket del parking.

### 8. Camino de Santiago (mapa lineal)
El único mapa en el que no se puede volver: cada etapa es una zona y la verja de atrás se cierra una ronda después
de abrir la siguiente.
- **Dejar cosas atrás:** la caja, las bebidas y las armas de pared se quedan en la etapa que abandonas; avanzar es
  la decisión principal.
- **Misión:** sellar la credencial en cada albergue.
- **Cada 5 rondas, niebla gallega.**
- **Abuelos:** los senderistas pasan a ser el enemigo base.
- **Jefe:** en la catedral, con el botafumeiro cruzando la nave en un arco fijo y golpeando a todo el que pille.

### Orden propuesto
- De los tres primeros: **Fiestas** (el más barato: llano, el encierro es un peligro sobre un recorrido fijo como
  las góndolas, y estrena las trampas), luego **Hotel** (pone las alturas en el centro) y por último **Crucero**
  (la escora es lo más arriesgado, sobre todo online).
- De los cinco siguientes: **Camino** (cambia la estructura de la partida, no solo el decorado) y **El Corte
  Jubilés** (las rebajas encajan con la economía de conchas, que aún hay que ajustar).
