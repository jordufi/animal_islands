# Crucero "Reina del Imserso": diseño para construirlo (2026-11-17, Opus)

Sexto mapa. Las decisiones del usuario están en `Docs/mapas.md`, sección 7; aquí se convierten en plano, números y
trabajo. **Estado:**
- **Hecho:** el plano en `Tools/Maps/make_crucero_layout.py`. Pasa las comprobaciones de rejilla (`--check` imprime
  las tres plantas; con `--png` las dibuja en `Renders/Plano_Crucero.png`).
- **Sin hacer:** todo lo de Unity, que necesita antes las plantas reales (`Docs/PLANTAS_FORMATO.md`).
- **Reglas de siempre:** números en `Assets/Data`, probar jugando, no tocar scripts con una prueba en marcha, commit
  solo cuando lo pida el usuario. **Los abuelos siempre pueden seguir tu ruta.**

## 1. Ficha
| | |
|---|---|
| Idea | crucero del Imserso en dos actos: el puerto al atardecer, y la alta mar de noche, frente a Benidorm |
| Plantas | Planta 0 (el muelle y, dentro del casco, la cubierta de camarotes) · cubierta principal (4 m) · cubierta de sol (8 m) |
| Tamaño | 1296 celdas = 5184 m². Acto 1: 234 celdas; acto 2: el resto |
| Inicio | el muelle |
| Momento que cambia la partida | **zarpar en la ronda 4**: quien no ha embarcado, pierde |
| Evento | **abordaje de lanchas**, rondas 5, 10, 15... |
| Zona ancha / estrecha | la cubierta de sol / los pasillos de camarotes |
| Jefe | el capitán Anselmo, en el puente de mando |
| Salida | arriar el bote salvavidas con la manivela |

El barco mira al este. Babor = norte, estribor = sur (el lado del muelle).

## 2. Zonas
| Letra | Zona | Planta | m² | Qué es | Premios |
|---|---|---|---|---|---|
| `a` | El muelle (inicio) | 0 | 288 | muelle con contenedores y bolardos; la puerta de la pasarela `Q` | caja |
| `r` | Terminal marítima | 0 | 336 | mostradores de facturación, tienda libre de impuestos | escondite de la tarjeta `&`, bebida, caja |
| `g` | Almacén de equipajes | 0 | 312 | montañas de maletas, contenedores | escondite de la tarjeta `&`, Lentos-peta `G`, caja |
| `c` | Paseo de cubierta | 1 (+ pasarela) | 1424 | paseo de 2 casillas alrededor del barco (el circuito), cubierta de popa, proa y atrio | caja, parada de moto de agua `N`, 3 escondites de la llave `L`, disco `J` |
| `i` | Cubierta de camarotes | 0 | 732 | dos pasillos largos con cruces (laberinto de camarotes), sala de máquinas y marina de popa | 3 escondites de las cartas náuticas `O`, gasolina `Y`, corriente de los pescantes `E`, moto de agua `N`, La Chancla `U`, minibar, caja ×2, disco |
| `e` | Casino y teatro | 1 | 304 | tragaperras, ruleta y escenario con karaoke | karaoke `K`, bebida, caja (`firstbox`), disco |
| `f` | Comedor | 1 | 256 | mesas redondas, bufé | bebida, La Thermomix `M`, caja (`firstbox`) |
| `p` | Cubierta de sol | 2 | 1350 | piscina, jacuzzis, tumbonas, chimenea, torre de toboganes, cabañas y pistas de tejo (shuffleboard) que cortan la vista | **Microondas `P`** en lo alto de la torre, bebida, caja |
| `l` | Puente de mando | 2 | 240 | un anillo de cubierta y alerones alrededor de la caseta del timón: sitio para dar vueltas al jefe y para aguantar la manivela | jefe `b`, manivela del bote `X` |

## 3. Puertas
| N.º | Puerta | Precio | Une |
|---|---|---|---|
| 1 | Puerta de la terminal | 750 | Muelle – Terminal |
| 2 | Persiana del almacén | 750 | Muelle – Almacén |
| 3 | Escalera central (camarotes) | 1000 | Paseo – Camarotes (en el atrio) |
| 4 | Casino y teatro | 1000 | Paseo – Casino |
| 5 | Comedor | 1250 | Paseo – Comedor |
| 6 | Escalera de popa (marina) | 1250 | Paseo – Camarotes (en la cubierta de popa) |
| 7 | Escalera de proa | 1500 | Casino – Cubierta de sol |
| 8 | Escalera de popa | 1500 | Comedor – Cubierta de sol |
| 9 | PUENTE DE MANDO - JEFE FINAL | misión + llave del bote | Cubierta de sol – Puente |

- **La pasarela no es una puerta de pago.** Es la puerta `Q` (como el Portón del pueblo): el muelle y el paseo son del
  mismo grupo y se cruza con la tarjeta de embarque.
- **Lazos:**
  - El paseo es un anillo alrededor del barco.
  - Casino → cubierta de sol → comedor → paseo es otro lazo por dos plantas.
  - Los camarotes tienen dos salidas al paseo (puertas 3 y 6).

## 4. Cómo va una partida
1. **El puerto (rondas 1-4, atardecer):**
   - Abrir la terminal y el almacén (750 + 750). La **tarjeta de embarque** está en la que abras **segunda** (misma
     regla que el mechero del pueblo).
   - Con ella se abre la pasarela y se puede subir al barco antes de tiempo. Arriba ya se pueden abrir puertas.
2. **Ronda 4, zarpar:**
   - Al empezar la ronda suena la bocina ("¡Último aviso para embarcar!") y aparece una cuenta atrás de 2 minutos.
   - A cero: se recoge la pasarela, el puerto se aleja y se cierra para siempre, y cae la noche.
   - Quien siga en tierra: en solitario, fin de la partida; en cooperativo, queda fuera y vuelve a bordo en la ronda
     siguiente (como un desangrado). Si no ha subido nadie, fin.
   - **Sin tarjeta a tiempo, se pierde.**
3. **Ronda 5, el primer abordaje:** los abuelos que se quedaron en el puerto os persiguen en lanchas, por estribor.
   Después cada 5 rondas, alternando babor y estribor.
4. **Misión, "Tareas del zafarrancho":**
   1. Los tres discos de Julio al karaoke del teatro.
   2. Las cartas náuticas del capitán (en uno de tres camarotes `O`).
   3. La gasolina de la marina.
   4. Dar corriente a los pescantes (sala de máquinas).
   5. La llave del bote (en una de tres `L`).

   Con las cuatro primeras, el bote está listo y la puerta 9 se abre gratis, pero quien la abre tiene que llevar la
   llave.
5. **El puente:**
   - Primero el capitán Anselmo.
   - Después, la **manivela**: mantener □ en `X` 20 s en total (cuenta lo acumulado y se para si te pegan), con la
     horda encima.
   - El bote baja y os vais. Con el disco de oro, ×2.

## 5. Reglas nuevas (números a `Assets/Data`)
### 5.1 El puerto y zarpar (`Rounds/Zarpar` + `CruceroData`)
- **La tarjeta de embarque:**
  - Es un objeto del equipo, como el mechero.
  - Está en la `&` de la segunda zona del puerto que se abra. Si solo se abre una, no hay tarjeta.
  - Con ella, `Q` se abre al usarla y se queda abierta.
- **La salida:**
  - Ronda 4: bocina y `departureCountdown` = 120 s en pantalla. Ese aviso lo manda el anfitrión, en su propio mensaje.
  - A cero, la pasarela se recoge: quien esté en sus celdas pasa a la cubierta, no a tierra.
- **Al zarpar:**
  - `Zone.Close()` en el muelle, la terminal y el almacén, y fuera los abuelos que queden en tierra.
  - El puerto entero (su raíz, `Puerto`) se aleja hacia el sur en 60 s: así parece que el barco se mueve sin moverlo.
  - La ciudad del fondo se mueve más despacio, para dar profundidad.
  - El agua hace estela (el shader de Venecia con desplazamiento).
- **Quién se queda en tierra:** en solitario, fin de la partida ("Te has quedado en tierra"). En cooperativo, ese
  jugador pasa a desangrado y aparece a bordo en la ronda siguiente.
- **Luz:** hora dorada en el puerto, que baja un poco cada ronda. Al zarpar, noche para siempre
  (`DayNightCycle.FallForGood`, ya existe).

### 5.2 Abordaje (`Rounds/Abordaje` + `AbordajeData`)
- **Cuándo y por dónde:**
  - Rondas 5, 10, 15... El primer lado es estribor y luego alterna.
  - Lo anuncia el anfitrión (`NetMsg.Abordaje`): "¡Lanchas a estribor!".
- **Las lanchas:** 3 o 4, solo de decorado (malla que llega por el agua con un foco que barre la cubierta: un cono
  falso más una luz de foco sin sombras).
- **Los garfios:**
  - Los `:` del lado del abordaje (paseo y cubierta de sol) se activan como **entradas de abuelos**: el abuelo trepa
    por la cuerda y salta la borda.
  - El 80 % de los abuelos de la ronda sale por ahí.
- **Cortar un garfio:**
  - Manteniendo □ 1,5 s caen al agua todos los que suben por él. Mueren, y pagan como una baja.
  - La lancha lo vuelve a lanzar a los 20 s.
- **Al acabar la ronda:** una Nevera.

### 5.3 Moverse
- **Motos de agua** (`GondolaStop` reutilizado):
  - Van de la marina de popa (planta 0) a la proa (cubierta principal, por una escalerilla), rodeando el casco por
    fuera. 250 conchas.
  - Cuando alguien llega, a los 5 s una lancha deja 2 abuelos en esa escalerilla (entrada de abuelos). Es el lado de
    los abuelos de este atajo.
- **Toboganes** (`Zones/FloorLink`):
  - Desde lo alto de la torre: uno a la piscina y otro por fuera del casco hasta la cubierta de popa.
  - Los dos acaban en zonas que siempre están abiertas cuando lo está la de salida.
  - Los abuelos también se tiran.
- **Caerse por la borda:** `CanalRescue` (15 de daño, vuelves a la última celda). El bocinazo del capitán puede tirarte.

### 5.4 Abuelos nuevos (solo en esta escena)
| Variante | Desde | Qué hace | Cómo se le gana |
|---|---|---|---|
| **El del flotador** | ronda 3 | los tiros al cuerpo rebotan ("¡boing!", sin daño) | a la cabeza o con el ukelele |
| **El grupo de aquagym** | ronda 7 | 4 o 5 en formación detrás de la monitora del silbato, a 1,5 m, tapando el pasillo | matar a la monitora: el resto se dispersa y pasa a perseguir normal |

### 5.5 Jefe: el capitán Anselmo
- Sale en el puente con gorra de capitán (`Boss/CapitanLook`).
- **Bocinazo** cada 15 s: onda de 10 m que empuja (6 m/s durante 0,4 s) y puede tirarte por la borda desde los
  alerones.
- Vida con la fórmula de siempre.

### 5.6 Tiendas y luz de máquinas
- **La caja de armas es "La Tragaperras"** (del casino).
- **El Microondas** está en lo alto de la torre de toboganes: se sube por una escalera estrecha y se baja por el tobogán.
- **Las bebidas funcionan desde el principio:** el barco tiene luz. El `E` de la sala de máquinas es una **tarea** (la
  corriente de los pescantes), no "dar la luz" (`PowerSwitch` con una opción para no tocar las máquinas, o un
  `MissionTask` nuevo).

## 6. Luz (barata, pensando en Android)
- **El puerto:** la hora dorada de Venecia (sol bajo y cálido, bruma), con el sol bajando un poco cada ronda.
- **En alta mar, de noche:**
  - Filas de ojos de buey (emisivos en la malla del casco), guirnaldas de cubierta (las bombillas del pueblo),
    chimenea iluminada desde abajo.
  - La piscina iluminada desde dentro.
  - La luna con brillos en el agua low poly.
  - La costa de Benidorm al fondo: puntos emisivos y el neón del Hotel Jubiles.
- **Abordaje:** conos falsos de los focos de las lanchas, más una luz de foco por lancha, sin sombras.
- **Rendimiento:** oclusión horneada (el casco tapa la cubierta inferior), como mucho 4 luces por objeto, solo la luna
  con sombras.

## 7. Modelos (`Tools/Blender/make_crucero_props.py`, FBX `CR_*` en `Assets/Maps/Crucero/Models`)
Se puede empezar ya, en paralelo con las plantas.
- **El barco:**
  - Casco por tramos con ojos de buey y popa con la marina abierta.
  - Borda (pretil de barco), bote salvavidas con pescantes, chimenea.
  - Puente de mando con timón y consolas, puertas estancas (mamparos con volante: las puertas de pago).
- **Cubierta de sol:** torre de toboganes con los dos tubos, piscina y jacuzzi, tumbonas, sombrillas, bar de cubierta.
- **Interiores:**
  - Atrio con escalera y lámpara.
  - Casino: tragaperras (la caja), ruleta.
  - Teatro: escenario con karaoke.
  - Comedor: mesas redondas.
  - Camarotes: puertas de camarote numeradas (también entradas de abuelos), litera.
  - Puertas de tripulación en la superestructura, a lo largo del paseo cada ~8 celdas, y en el atrio y la caseta de la
    escalera de proa: son entradas de abuelos (2026-11-17: con solo las de antes, el paseo tenía 134 celdas donde los
    abuelos llegaban por un solo lado). Escalerillas de la piscina de la cubierta de sol: los abuelos salen del agua.
  - Sala de máquinas: motores, cuadro.
  - Marina: motos de agua y escalerilla.
- **El puerto:**
  - Terminal con mostradores, cintas de equipaje, estantes de la tienda.
  - Contenedores, maletas, carretilla elevadora.
  - Pasarela, bolardo, grúa de puerto.
  - La ciudad del fondo con siluetas de edificios de Benidorm.
- **El abordaje:** lancha del Imserso con foco, garfio con cuerda.
- **Accesorios:** tarjeta de embarque, cartas náuticas, bidón de gasolina, llave del bote.
- **Abuelos:** flotador, gorro de baño y churro de aquagym, el silbato de la monitora y la gorra del capitán.

## 8. Online (protocolo 6, el mismo cambio que el hotel)
- **Mensajes nuevos del anfitrión:** aviso de salida con cuenta atrás, momento de zarpar, abordaje (lado) y estado de
  la manivela.
- **Lo que pide el cliente:**
  - Cortar un garfio: `RequestInteract` mantenido; el anfitrión aplica.
  - Moto de agua: como la góndola online (`NetGame.Fwd.Gondola`).
- **En el estado de la misión:** tarjeta, cartas, gasolina, pescantes y llave.
- **Quedarse en tierra:** el anfitrión decide quién se ha quedado y lo manda (`NetMsg.Zarpar` con la lista).

## 9. Pruebas (`CruceroPlayTest` + las comunes)
- **Setup:** 0 errores, grupos sellados, 9 puertas, todo alcanzable y la ruta a la salida.
- **El puerto:**
  - La tarjeta está en la segunda zona abierta y no en la primera.
  - `Q` está cerrada sin tarjeta y abierta con ella.
- **Zarpar:**
  - Bocina y cuenta atrás en la ronda 4.
  - Quien está en la pasarela pasa a cubierta.
  - En solitario, en tierra = fin.
  - En cooperativo, J2 en tierra queda fuera y vuelve en la ronda 5.
  - El puerto se cierra y se aleja, y es de noche.
  - Sin tarjeta, fin.
- **Paseos con el cuerpo del jugador:** pasarela, escalera central, escalera de popa, escaleras de proa y popa a la
  cubierta de sol, torre de toboganes y el anillo del paseo.
- **Moverse:** un abuelo te sigue de los camarotes a la cubierta de sol; los dos toboganes con el jugador y con un
  abuelo.
- **Abordaje:**
  - El lado anunciado, los garfios activos solo en ese lado y el 80 % de los abuelos por ellos.
  - Cortar un garfio: caen y pagan.
  - Babor y estribor alternan.
- **Moto de agua y borda:**
  - Moto de agua: el viaje (también desde un cliente online) y la lancha con 2 abuelos al llegar.
  - Caerse por la borda: rescate.
- **Abuelos nuevos:** el del flotador (cuerpo no, cabeza sí) y el aquagym (formación, y se dispersa sin la monitora).
- **Misión y final:**
  - Las cinco tareas y la puerta 9 con la llave.
  - El capitán: el bocinazo empuja y por la borda hay rescate.
  - La manivela: 20 s acumulados que se paran con un golpe.
  - La salida y el disco ×2.
- **Además:** `CoopPlayTest` con 3 jugadores (uno se queda en tierra), `OnlinePlayTest.RunDirectCrucero` y
  `QuickChecks`.

## 10. Orden y qué va en paralelo
1. **Plantas reales**, común con el hotel. Lo primero.
2. **A la vez que 1:** los modelos `CR_*`, la ciudad del fondo y el agua en movimiento.
3. **Tema `IslaZombieSetup.Crucero.cs`:** casco, borda, ojos de buey, puerto, puntos.
4. **Reglas** (5.1 a 5.6), cada una con su bloque en `CruceroPlayTest`.
5. **Cooperativo, online, regresión, `CLAUDE.md` y `Docs/mapas.md`.**

## 11. Revisión "¿es divertido como un Black Ops?" (2026-11-17)
- **Sitios para correr en círculos con la horda:** el paseo (el más grande del juego), la piscina y la chimenea de la
  cubierta de sol, y la cuadrícula de camarotes (varios lazos estrechos).
- **Sitios arriesgados con premio:** los camarotes sin salida, la torre de toboganes con el Microondas y la marina.
- **Momento que cambia el mapa:** zarpar.
- **Movimiento propio:** motos de agua y toboganes.
- **Corregido en el plano:**
  - La cubierta de sol era muy abierta: ahora tiene cabañas y pistas de tejo.
  - El puente era una sala pequeña para un jefe gigante y para aguantar la manivela: ahora es un anillo alrededor de
    la caseta del timón.
- **Falta (propuesta, sin decidir):** un arma maravilla del mapa. Idea: **La Manguera Contraincendios**. Empuja a los
  abuelos en un cono, y los que caen por la borda mueren. Combina con el abordaje: barrer una borda llena de garfios.

## 12. Números a ajustar con personas
- **El puerto:** los precios, la cuenta atrás (120 s), y si un jugador normal consigue la tarjeta antes de la ronda 4.
  Ojo: si no la consigue, es un final duro que hay que ver con jugadores.
- **El abordaje:** el 80 % de entradas por los garfios, los 20 s hasta que se vuelve a lanzar un garfio y el 1,5 s para
  cortarlo.
- **El final:** los 20 s de manivela y el bocinazo.
- **Lo que preocupa del plano:**
  - El paseo es largo: se cortó la vista con pescantes y taquillas, pero hay que mirarlo en capturas.
  - La cubierta de sol (1372 m²) puede quedar muy abierta.
