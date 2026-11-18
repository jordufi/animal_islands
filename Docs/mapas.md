# Mapas de Animal Island

Todo lo que define cada mapa: cómo es, sus mecánicas y secretos, las decisiones de diseño (para no volver a
preguntarlas), lo que se descartó y lo que se podría mejorar. Actualizado el 2026-11-17.

- **Decidido** = lo acordó el usuario; no se vuelve a preguntar.
- **Propuesta** = idea sin decidir.
- **Descartado** = se pensó y se quitó; queda apuntado para no repetir la discusión.

El detalle técnico (archivos, código, resultados de las pruebas) está en `CLAUDE.md`. Cómo se hace un mapa paso a
paso: `Docs/GUIA_NUEVO_MAPA.md`. Las ideas de mapas sin empezar están en el `README.md`.

| Mapa | Estado | Evento cada 5 rondas | Jefe | Salida |
|---|---|---|---|---|
| Isla del Faro | jugable, el principal | noche con senderistas | Don Anselmo, presidente de la comunidad | barco de rescate |
| Isla del Faro (alturas) | jugable, copia con alturas | noche con senderistas | Don Anselmo | barco de rescate |
| La Serenísima | jugable, en pruebas | acqua alta | el Dux | vaporetto |
| Fiestas de Villajubilada | jugable, en pruebas | encierro | el pregonero | tractor |
| Original | mapa de pruebas | noche con senderistas | Don Anselmo | barco |
| Hotel Jubiles | en diseño | noche de bingo | Don Anselmo en albornoz | helicóptero |
| Crucero "Reina del Imserso" | en diseño | abordaje de lanchas | el capitán Anselmo | bote salvavidas |

---

## Lo común a todos los mapas

- **El bucle:** rondas de abuelos (cada ronda con más vida) → conchas → puertas de pago que abren zonas → misión →
  jefe → extracción. Sin límite de tiempo. Partidas de 20 a 40 minutos, difíciles a propósito, sin progresión entre
  partidas.
- **Cada puerta abierta = 1 carta de 3** (bendición + maldición para toda la partida). Las elige quien abre la puerta.
- **Una puerta por ronda** para todo el equipo (feedback 1, decidido: también cuenta la del muelle / la misión; el
  Portón del encierro se abre solo y no cuenta). Número en `Economy.asset` (`doorsPerRound`).
- **Caja de armas** (Máquina de Granizados; tómbola en Villajubilada): un uso por zona abierta; después reaparece en
  otra zona abierta. Las primeras zonas donde puede salir van marcadas (`firstbox`).
- **Armas con estrellas** (feedback 1, fase 5, decidido 2026-11-18): de 0 a 3 estrellas, se ven junto al nombre
  ("La Chancla ★★").
  - Pistola del principio 1★; armas de pared 0★; la caja da 1★ (60 %), 2★ (30 %) o 3★ (10 %).
  - Cada estrella sube el daño, la cadencia y la recarga, y la bala pierde menos al atravesar: el daño se multiplica en
    **cada** abuelo atravesado (0★ ×0,25, 1★ ×0,4, 2★ ×0,6, 3★ ×0,8). Atravesar nunca suma daño.
  - **La Lijadora del Taller** (marcador `I`, 1000 conchas): +1 estrella al arma en la mano, hasta 3, también a las de
    pared. Una por mapa, lejos del Microondas: Parking (Isla y alturas), Arsenale (Venecia), mirador de las Cuestas
    (Villajubilada), cerca de la gasolina (Original).
  - **El Microondas es una categoría aparte:** cualquier arma pasada por él queda mejor que una de 3★ en todo y ya no
    admite estrellas.
  - Los daños de los `.asset` son los de 1★; el Microondas conserva su daño de antes. **Pendiente: ajustar los daños con
    partidas reales** (usuario).
- **Bebidas** (necesitan luz): Carajillo del Abuelo, Mojito Turbo, Piña Colapso, Tinto de Verano Eterno.
- **El Microondas** (Pack-a-Punch, 5000), siempre lejos del inicio y en un sitio incómodo.
- **Armas:** La Jubilada (la pistola del principio), La Chancla (escopeta), El Catalejo (francotirador) y La
  Thermomix (subfusil) salen de la caja. El Lentos-peta pega más fuerte y más abierto que la Chancla, pero recarga
  más lento y lleva menos cartuchos (4 + 24 contra 6 + 48).
  - En la pared: Lentos-peta 500 y, en algunos mapas, La Chancla 1500 y La Thermomix 2500.
  - Todos llevan el ukelele para el cuerpo a cuerpo.
- **Power-ups:** Nevera de munición, Siesta fulminante, 2x1 en conchas, Paella para todos.
- **Modos:** Clásico y **Anselmo cagón** (la mitad de los abuelos atajan por las entradas).
- **Secreto común:** tres discos falsos de Julio Iglesias → cantar en el karaoke → Disco de Oro, que se lleva en
  brazos, se cae si te pegan y, si sales con él, duplica la puntuación. **En cuanto suena Julio, los abuelos se
  vienen arriba:** corren un 25 % más el resto de la partida (riesgo y recompensa, feedback 1).
- **Abuelos heridos** (feedback 1): con un 25 % de vida a uno se le caen las gafas (va un 40 % más lento) y a otro el
  bastón (corre un 40 % más). Mitad y mitad.
- **Dentadura postiza** (R1 / G, la granada): muerde en 3,5 m, quita 400 y aturde 2,5 s. Dos por jugador; la Nevera
  las devuelve.
- **Brújula de mierdecillas** (opción): flecha arriba que apunta a una tarea pendiente al azar.
- **Tutorial** (feedback 1, fase 7): "Isla del Faro (tutorial)" en el selector, solo para un jugador. Unos 5 minutos
  guiados antes de la ronda 1 (moverse, mirar, saltar, disparar, recargar, ukelele, dentadura, habilidad, puerta +
  carta, bebida con la luz ya dada y la caja); luego sigue la partida normal. Con pantalla dividida u online es la Isla
  normal.
- **Llave Maestra:** está en un callejón sin salida al azar y sustituye una pieza de la misión. Si quien la lleva cae,
  se queda en el suelo.
- **Reglas de diseño (todos los mapas, decididas):**
  - Los abuelos siempre pueden seguir la ruta del jugador. Las únicas excepciones son la góndola y el campanile.
    Desde el feedback 1 (2026-11-17): **donde subes tú, sube el abuelo.** Si te subes a un prop (coche, cajas, bus,
    caseta, puesto, máquina, tractor...), los abuelos cercanos trepan detrás de ti como los senderistas de noche. El
    setup avisa de cualquier sitio al que llegue el jugador saltando y donde ningún abuelo le pueda pegar
    (`ReachCheck`); hoy no queda ninguno en los cuatro mapas.
  - Nada de sitios seguros.
  - Que no se vea venir a los abuelos de lejos.
  - En cada mapa, una zona ancha y una estrecha.
  - **Sin líneas rectas largas** (feedback 1): el setup avisa de las líneas de tiro de 40 m o más
    (`IslaZombieSetup.SightLines.cs`); la zona ancha va marcada `ancha` y no cuenta.
  - Las entradas de abuelos se ven, son predecibles y van todas en el mismo verde.
  - Tiene que ser difícil.
  - Todo el texto, en castellano y con humor.
- **Cooperativo:** bote de conchas compartido; al caer vas a la última resistencia, con un compañero que te reanima;
  2-4 jugadores en pantalla dividida u online. Escala con más jugadores.

---

## 1. Isla del Faro (el mapa principal)

| | |
|---|---|
| Escena | `IslaZombie` · mapa en `Assets/Maps/IslaZombie/IslaZombie_Layout.txt` |
| Forma | isla redonda de unos 70 m con un muelle; ~2000 m² andables, pasillos de 2-4 m, muy apretada |
| Luz | de día (las rondas nocturnas, de noche) |
| Inicio | el Chiringuito (playa oeste) |

### Zonas y puertas
Diez zonas: Chiringuito (inicio) · Camping · Parking · Volley · Templo · Cueva Secreta · Faro · Lago + Bosque ·
Mercadillo · Muelle (jefe y salida).

| Puerta | Precio | Une |
|---|---|---|
| Hamacas apiladas | 750 | Chiringuito – Camping |
| Barrera del parking | 1000 | Camping – Parking (abre también Cueva y Faro) |
| Red de vóley caída | 1000 | Camping – Volley |
| Puerta del Templo | 5000 | Camping – Templo (abre también el Lago) |
| Verja del faro | 1500 | Faro – Lago |
| Toldo del mercadillo | 1250 | Lago – Mercadillo |
| MUELLE - JEFE FINAL | misión | Faro – Muelle |

### Mecánicas propias
- **Solo dos caminos de izquierda a derecha:** Camping → Templo → Lago, o Parking → Cueva → Faro. Parking y Faro se
  tocan por una pared sin paso.
- **El Templo es un cuello de botella:** un pasillo de 2 m y una sala de 3 × 3 casillas con el Microondas. Se paga
  5000 una vez y se cruza andando.
- **La Cueva Secreta:** un laberinto oscuro y techado, sin puertas dentro y lleno de entradas de abuelos.
- **Ronda nocturna cada 5:** oscurece y solo vienen los senderistas verdes (como mucho 5 vivos a la vez), que se suben
  a los objetos y tiran chanclas. Al amanecer cae una Nevera de munición.
- **Entradas de abuelos:** 69 en total: el WC, el autobús del Imserso, tiendas, la grieta de la roca, la escalerilla de
  la piscina desde el mar... Todas en verde, nunca confundibles con una puerta de pago.
- **El bus del Imserso tiene una rampa de tablones** hasta el techo, en el hueco del final (feedback 1: "deberia haber
  una rampita"). Es un mirador arriesgado: los abuelos suben por la rampa o trepan.
- **Carteles "ESTÁS AQUÍ"** junto a cada puerta, y el mismo mapa en el menú de pausa.
- **Premios repartidos:**
  - Bebidas en Camping, Parking, Faro y Lago.
  - Karaoke en el Chiringuito.
  - Discos de Julio en Volley, Cueva y Mercadillo.
  - Armas de pared: Lentos-peta (Camping), La Chancla (Parking) y La Thermomix (Lago).
  - La caja sale primero en Parking o Volley.

### Misión y salida
Dar la luz (cuadro del Faro) → gasolina (Parking) + radio (cámara sur del Templo) → llamar desde la emisora (Faro) →
aguantar hasta el final de la ronda siguiente → el muelle se abre gratis → Don Anselmo → barco. La Llave Maestra
sustituye una de las dos piezas.

### Secretos
- Los tres discos de Julio y el Disco de Oro, que da ×2 si sales con él.
- La Llave Maestra, en uno de los callejones sin salida (Volley, Mercadillo).

### Decisiones de diseño
- De día, no de noche como las imágenes de referencia.
- Pequeña, caótica y claustrofóbica, con muchos cuellos de botella.
- Nunca hay paso Camping ↔ Lago ni Volley ↔ Mercadillo.
- El Templo cuesta 5000 a propósito.

### Descartado
- El primer prototipo grande (unos 150 × 120 m): "lento y nada divertido". Se rehízo a 1/8 de tamaño.
- El muelle por 10000 conchas o con la llave: lo sustituyó la misión del barco.
- La llave escondida en la Cueva (ese sitio se quitó).

### Propuestas para mejorarlo
- Modelos de verdad para puertas, máquinas de bebidas, caja y Microondas (siguen siendo cubos con cartel).
- Ajustar la economía con los registros de partida (abre puertas demasiado pronto) y aplicar la encuesta de la
  primera prueba con jugadores.
- Audio (no hay ninguno en el juego).
- Un fondo del menú principal hecho con este mapa (hoy es la isla de pruebas).
- Elementos que cambien entre partidas: qué puerta está averiada, dónde está la luz, qué bebida falta.
- Decidir si la versión con alturas lo sustituye.

---

## 2. Isla del Faro (alturas)

| | |
|---|---|
| Escena | `IslaFaroAlturas` · formato de alturas en `Docs/FASE3_FORMATO.md` |
| Qué es | la misma isla con tres niveles (cada nivel, 2 m), rampas, caídas de un solo sentido y toboganes |

### Lo que cambia respecto a la isla normal
- **Faro:** mirador alrededor del faro, con el cuadro de luz arriba. Dos rampas hacen un circuito en dos niveles.
- **Camping:** un anillo de pasillos alrededor de un bloque de tiendas, y una terraza que sube hasta la puerta del
  Templo.
- **Templo = cripta:** el pasillo empieza arriba y baja por una rampa hasta la sala del Microondas.
- **Volley:** un paseo elevado sobre la pista hundida y un tobogán al Chiringuito.
- **Cueva:** una sala central con un estanque bajo un tragaluz.
- **Mercadillo:** un balcón con tobogán al Lago (su segunda salida) y un mirador en el nivel 2.
- **Bordes de las rampas:** bordillos y pretiles en los lados, para que los abuelos no suban de lado por donde el
  jugador no puede.

### Decisiones de diseño
- La isla original no se toca: esta es una copia aparte en el selector.
- **Regla de los toboganes:** solo pueden llevar a una zona que esté siempre abierta cuando la de salida lo está.
- Las zonas con circuito (Camping, Parking, Volley, Cueva, Mercadillo) tienen un anillo para correr.

### Descartado
- Saltar del Faro al Lago: rompía la regla de los toboganes, porque el Faro se puede abrir sin el Lago. Ahora te
  dejas caer dentro del propio Faro.
- Una caída desde el Muelle: rompía la misma regla.

### Propuestas
- Decidir si sustituye a la isla original.
- Probarlo con 4 jugadores en pantalla dividida.

---

## 3. La Serenísima (Venecia + cementerio monumental)

| | |
|---|---|
| Escena | `Venecia` · generador `Tools/Maps/make_venecia_layout.py` |
| Forma | Venecia con un cementerio en una isla en el centro de la dársena; ~30 % más grande que la isla |
| Luz | atardecer: sol cálido y bajo, bruma; agua low poly hecha a medida |
| Inicio | Ferrovia (la estación) |

### Zonas y puertas (las puertas son puentes de pago)
Ocho barrios:
- **Ferrovia:** el inicio. Desde el feedback 1, con más edificios: fachadas sobre el agua, un quiosco y una manzana
  que obliga a rodear para llegar al gran puente.
- **Mercato di Rialto:** puede salir la caja la primera vez.
- **Cannaregio:** la zona estrecha. Solo callejones de una casilla que giran y acaban sin salida.
- **Campo del Carnevale:** un escenario. También puede salir la caja la primera vez.
- **Cimitero Monumentale:** una isla con un anillo, una terraza y una torre en espiral con el Microondas arriba.
- **Piazza del Dux:** la zona ancha. Una plaza abierta de 36 × 26 m con el campanile; sus lados norte y oeste son
  fachadas de palacio (feedback 1).
- **Arsenale:** una dársena interior y una muralla.
- **Vaporetto:** el muelle del jefe y la salida.

| Puente | Precio | Une |
|---|---|---|
| Ponte dei Sospiri (de cansancio) | 750 | Ferrovia – Mercato |
| Ponte del Bingo | 1000 | Mercato – Cannaregio |
| Ponte della Ferrovia (gran arco) | 1500 | Ferrovia – Carnevale |
| Ponte del Cimitero | 1250 | Mercato – Cimitero |
| Ponte delle Vedove | 1500 | Cimitero – Piazza |
| Ponte del Carnevale | 1250 | Piazza – Carnevale |
| Ponte dell'Arsenale (gran arco) | 2000 | Cannaregio – Arsenale |
| Ponte dei Pensionati | 1500 | Piazza – Arsenale |
| VAPORETTO - JEFE FINAL | misión | Piazza – Vaporetto |

Hay dos lazos que pasan por la isla del cementerio y forman un ocho.

**Repaso del feedback 1 (2026-11-18): sin líneas rectas largas.** Los amigos: "desde el Carnevale apuntas hasta la
Ferrovia". Antes había líneas de tiro de 110 m a lo largo de los canales. Ahora:
- Los bordes de los barrios que daban a un canal son fachadas (como en Venecia, las casas dan al agua). Solo quedan
  muelles en las cabezas de los puentes y en las paradas de góndola.
- Las calles largas tienen un quiebro cada ~20 m.
- La línea más larga que queda es de 40 m (sin contar la Piazza ni mirar a lo largo de un canal desde un puente); en
  las Fiestas, el mapa favorito, es de 60 m.
- Hay 80 entradas de abuelos (antes 75) para que los nuevos fondos de saco no sean sitios seguros.
- **Bebidas solo en Cannaregio y el Mercato** (dos máquinas en cada uno, en callejones sin salida).

### Mecánicas propias
- **Acqua alta cada 5 rondas** (en lugar de la noche):
  - El mar sube 0,45 m. En la calle vas a ×0,6 con el agua por los tobillos.
  - Los canales se pueden cruzar nadando a ×0,5, y los abuelos te siguen al agua.
  - Al bajar la marea vuelves al muelle sin daño y cae una Nevera.
  - Unas balizas rojas marcan el borde del muelle.
- **Canales:** no hay paredes invisibles. Si caes al agua, o saltas a un barrio cerrado, vuelves al último sitio
  abierto que pisaste (15 de daño, nunca mortal).
- **Verjas de 3,2 m** en los bordes con agua de los barrios cerrados. Desaparecen al abrirlos.
- **Góndolas:**
  - 7 paradas, 250 conchas.
  - Te llevan a **una parada abierta al azar** (nunca a un barrio cerrado). Son una sola red de canales, que pasa bajo
    los dos puentes de arco.
  - **Un viaje por ronda y por jugador**, y cada ronda solo hay góndola en algunas paradas (~60 %, al menos una
    abierta). Cada máquina lo calcula con el número de ronda: no hace falta mensaje de red (feedback 1).
  - Te agachas al pasar bajo un puente bajo.
  - Los abuelos no las usan (excepción aceptada).
- **El campanile:**
  - Entras por la puerta y te sube al campanario.
  - Puedes saltar desde arriba sin daño (el juego no tiene daño por caída).
  - A los 20 s te echan.
  - Los abuelos no suben (excepción aceptada).
- **Abuelos enmascarados:**
  - **Anselmo Bauta:** desde la ronda 2. Máscara de 2 golpes.
  - **Anselmo Peste** (médico de la peste): desde la ronda 6. Máscara de 3 golpes; es más rápido y al morir deja una
    nube de gas.
  - Cada golpe rompe un trozo de máscara y nunca cuenta como tiro a la cabeza.
  - Aquí no hay senderistas.

### Misión y salida: las tareas del vaporetto
1. Los tres discos de Julio al karaoke (Ferrovia).
2. Tocar la campana del campanile (es "dar la luz").
3. Encontrar al gondolero de oro (en uno de tres callejones sin salida).
4. Desenterrar a Giuseppe en el Cimitero (hace falta la pala del puesto del Mercato, 500).
5. Encontrar la llave del vaporetto.

Con las cuatro primeras hechas el vaporetto viene, y llega al final de la ronda siguiente. La última puerta es
gratis, pero pide la llave en el bolsillo de quien la abre. Después, el Dux: Don Anselmo con máscara dorada (10
golpes) → vaporetto.

### Secretos
- El gondolero de oro (cambia de sitio en cada partida).
- Giuseppe: un esqueleto con boina y bastón que lleva cuarenta años esperando el vaporetto.
- Las máscaras rotas salen volando en cuatro trozos; su color (marfil, negro o rojo) cambia con cada abuelo.

### Decisiones de diseño
- **Mapa:**
  - Los puentes son las puertas.
  - Acqua alta en lugar de rondas nocturnas.
  - La misión del barco se reutiliza con otros nombres.
- **Góndolas y caer al canal:** al principio eran "una prueba; si no, serán barrera". Se quedaron.
- **Modelos:**
  - Entre un 30 y un 50 % más detallados que los de la isla.
  - Los barrios se entregaron ya decorados, sin fase de prototipo gris.
  - Toda cobertura baja (`x`) se puede saltar.

### Descartado
- La misión de gasolina, radio y emisora: la sustituyó la lista de tareas.
- Las paredes invisibles en la orilla: las sustituyeron el rescate del canal y las verjas.
- Los bloques de casas dentro de la Piazza y el campo de Cannaregio: lo ancho tenía que ser más ancho y lo estrecho,
  más estrecho.
- Los puestos del mercado como entradas de abuelos.
- El gondolero siempre en el mismo sitio.

### Propuestas para mejorarlo
- Los puentes pequeños siguen siendo macizos: no se puede pasar nadando por debajo.
- Las fachadas nuevas son las mismas casas de siempre: quizá pidan algún detalle (balcones, toldos) al verlas jugando.
- Ajustar las probabilidades de las máscaras, la marea y el precio de la góndola, que son una primera estimación.
- La góndola roza una esquina en un cruce estrecho (21, 31).
- Audio: campanas, agua, el carnaval.
- Sin explicar: algo reescribió `MainMenu.unity` el 2026-11-15 y no ha vuelto a pasar.

---

## 4. Fiestas de Villajubilada (pueblo en fiestas, en dos actos)

| | |
|---|---|
| Escena | `Villajubilada` · generador `Tools/Maps/make_villajubilada_layout.py` |
| Forma | un anillo de calles de 4 m alrededor de la Plaza Mayor, con barrios alrededor |
| Luz | acto 1 al atardecer; desde el primer encierro, de noche para siempre, con el pueblo encendiéndose zona a zona |
| Inicio | La Parada (del autobús) |

### Zonas y puertas
- **Acto 1:** La Parada (inicio, con el camión de ganado) + El Bar y Los Corrales, que se abren pagando. La salida, el
  **Portón del encierro**, está cerrada.
- **Acto 2:**
  - **Calle Mayor:** con la Calle del Encierro.
  - **Barrio de las Cuestas:** la estrecha. Dos niveles, callejones y un mirador.
  - **Calle de la Iglesia:** con el atrio.
  - **Plaza Mayor:** la ancha. Templete con el karaoke, Microondas y el balcón del ayuntamiento.
  - **Recinto ferial:** coches de choque, noria, churrería y puestos.
  - **Las Eras:** el jefe y el tractor.

| Puerta | Precio | Une |
|---|---|---|
| Barricada del Bar | 750 | La Parada – El Bar |
| Barricada de los Corrales | 750 | La Parada – Los Corrales |
| Peña El Sintrom | 1000 | Calle Mayor – Cuestas |
| Peña Los del Imserso | 1000 | Calle Mayor – Iglesia |
| Peña La Dentadura | 1500 | Cuestas – Iglesia (cierra el anillo) |
| Arco de la Plaza | 1250 | Calle Mayor – Plaza |
| Arco del Ayuntamiento | 1250 | Iglesia – Plaza |
| Entrada del ferial | 1500 | Iglesia – Ferial |
| LAS ERAS - JEFE FINAL | misión | Ferial – Las Eras |

### Mecánicas propias
- **El primer encierro (ronda 5, fijo) cambia la partida:**
  - Tiran un cohete y el Portón se abre solo; nunca se paga.
  - Se hace de noche para siempre y se abre la Calle Mayor.
  - Hay que correr calle arriba por delante de los toros, mientras las abuelas tiran macetas lentas desde las
    ventanas (se pueden esquivar).
  - El Portón se cierra detrás de la manada: las zonas del principio se cierran y no se puede volver. A quien se queda
    atrás, lo arrastran los toros (vivo, aunque esté caído).
- **Encierro cada 5 rondas:**
  - Los toros solo corren por zonas abiertas: si el anillo está cerrado dan vueltas; si no, van y vuelven por cada
    brazo abierto.
  - Golpean fuerte (60). Matar abuelos con los toros no paga.
  - Los portales de una casilla sirven para apartarse.
- **Cada zona tiene su cuadro de luces:**
  - Al encenderlo se iluminan sus guirnaldas una tras otra, la mitad de las ventanas y unas pocas luces de verdad.
  - Sus máquinas solo funcionan con luz.
- **Tracas:** la primera trampa del juego. Se paga por encender una traca a lo largo de una calle, y sus bajas pagan
  como una baja normal.
- El rincón al oeste de la rampa del atrio de la iglesia (casillas 24,10-11) es ahora parte de la casa: era un hueco
  donde solo se metía un jugador saltando (feedback 1, 2026-11-17).
- **Abuelos nuevos:**
  - **El del bombo:** lento él, pero los que tiene cerca andan ×1,3. Es objetivo prioritario.
  - **La de los petardos:** donde cae lo que tira, deja una mancha que quema.
- **La caja de armas es La Tómbola.**

### Misión y salida: las tareas del pregón
1. Los discos de Julio al karaoke del templete.
2. Encender todo el pueblo (todas las zonas).
3. Encontrar al cabezudo de oro (en uno de tres sitios).
4. Tirar el chupinazo desde el balcón del ayuntamiento (hace falta el mechero).
5. Encontrar la llave del tractor.

Después, el pregonero (Don Anselmo con pañuelo rojo y fajín) en Las Eras → huir en el remolque del tractor.

### Secretos
- **El mechero** está siempre en la segunda zona del acto 1 que abras. Si abres solo una, no lo tienes.
- **El cabezudo de oro.**
- **Nombres de las peñas:** El Sintrom, Los del Imserso, La Dentadura.

### Decisiones de diseño (del usuario)
- **Encierro:**
  - Toros, no cabezudos.
  - Matar con los toros no paga; las tracas sí pagan.
  - El primer encierro, fijo en la ronda 5.
- **Los dos actos:**
  - La zona inicial son La Parada y dos zonas que se abren pagando.
  - Al pasar el Portón ya no se puede volver.
- **Zonas:**
  - La segunda zona que abras siempre tiene un requisito de salida.
  - Cada zona hay que iluminarla para usar su bebida o sus cosas.
- **Tamaño y precios:** aceptados hasta que jueguen personas.

### Descartado
- **El primer diseño sin actos** (`Docs/VILLAJUBILADA_TRASPASO.md`): empezar con el pueblo entero, un único cuadro de
  luces para todo, la Calle de las Peñas como zona propia y tres vueltas fijas al anillo.
- **Cabezudos en vez de toros** (más ligero de hacer).
- **Que las bajas de los toros pagasen.**

### Propuestas para mejorarlo
- Audio (lo haremos más adelante, por decisión del usuario).
- Ajustar los números con partidas de personas: velocidad de los toros 6,5 y 9, daño 60, traca 750, cuatro rondas
  encerrados en el acto 1.
- Una opción en el menú de depuración para forzar un encierro.
- El cuerpo del toro es una sola pieza rígida (solo se mueven las patas).
- El fajín del pregonero apenas se ve.
- Nunca se ha probado con 4 jugadores, ni la misión entera online.
- **Idea:** una vaquilla suelta en la plaza como minievento entre encierros.
- **Idea:** la orquesta del templete como segunda trampa: los abuelos que bailan no atacan durante unos segundos.

---

## 5. Original (mapa de pruebas)

| | |
|---|---|
| Escena | `BeachIsland` · la distribución está en el código (`TestMapSetup` + `OriginalMap*.cs`) |
| Qué es | la primera isla pequeña (~50 m); sirve para pruebas |

Seis zonas:
- **Chiringuito:** el inicio.
- **Playa.**
- **Cementerio y Templo.**
- **Campamento.**
- **Jungla y Estanque.**
- **Muelle.**

| Puerta | Precio |
|---|---|
| Toalla gigante | 750 |
| Escalera VIP | 1000 |
| Hamacas apiladas | 1000 |
| Cinta de obra | 1250 |
| Escalera del jardinero | 1250 |
| Valla del camping | 1500 |
| MUELLE - JEFE FINAL | misión |

- Las entradas de abuelos son cabinas de WC verdes sueltas (25 de los 30 puntos).
- Los objetos de la misión se colocan por código.

### Propuestas
- Faltan cabinas en el Cementerio y el Muelle (podrían ser puertas de cripta).
- Sigue teniendo bordes invisibles entre zonas.
- Hay un contacto Jungla – Campamento sin puerta que nadie sabe explicar.
- Decidir si se queda en el selector o pasa a ser solo de pruebas.

---

## 6. Hotel Jubiles (en diseño)

Antes se llamaba "Hotel Gran Imserso". Un hotel de los años 70 en Benidorm, en una noche de tormenta de verano, lleno
de guiños a El Resplandor.

| | |
|---|---|
| Forma | **plantas reales, una encima de otra** (herramienta nueva, compartida con el crucero) |
| Contraste | lo ancho abajo (vestíbulo, piscina); lo estrecho arriba (pasillos de habitaciones con moqueta) |
| Inicio | recepción, **a oscuras** |
| Evento | **noche de bingo** |
| Jefe | Don Anselmo en albornoz, el huésped de la suite presidencial |
| Salida | helicóptero en la azotea |

### Plano (diseñado el 2026-11-17)
Plano completo, números y trabajo: `Docs/HOTEL_JUBILES_DISENO.md`. El plano se genera con
`Tools/Maps/make_hotel_layout.py`, que pasa sus comprobaciones. Cinco plantas, 4660 m².

| Zona | Planta | Qué hay |
|---|---|---|
| Recepción (inicio) | baja | vestíbulo, mostrador, araña, puerta giratoria, ascensores, escalera principal, teléfono |
| Bufé libre | baja | mesas, cocina, cafetera; la escalera de servicio baja al sótano |
| Sótano | −4 m | calderas (la luz), lavandería (albornoz, salida de las tolvas), garaje con rampa a la terraza |
| Piscina y jardines (la ancha) | baja | piscina, terraza este, laberinto de setos |
| Salón Dorado | baja | el bingo: pista, tarima del animador, barra del barman fantasma |
| Primera planta | 4 m | pasillo en anillo + pasillo central con moqueta, habitaciones, el gimnasio, dos escondites de la pulsera |
| Segunda planta | 8 m | igual, con un balcón exterior sobre la piscina, el Microondas (cuarto del conserje), el piloto, la 237 y las gemelas |
| Azotea (jefe) | 12 m | helipuerto, ático del jefe, neón |

Puertas: Comedor 750 · Escalera de las cocinas 1000 · Piscina 1000 · Rampa del garaje 1250 · Salón Dorado (terraza)
1250 · Salón Dorado (bufé) 1250 · Escalera principal 1250 · Escalera de la 2ª planta 1500 · Azotea (misión + pulsera).

A la luz se llega por la cocina (750 + 1000) o por la terraza y el garaje (1000 + 1250). Ascensores: sótano, baja, 1ª
y 2ª. El montacargas (sótano ↔ 2ª) y dos tolvas (1ª y 2ª → lavandería) completan la vertical.

### Acto 1: el apagón (decidido)
- **Cómo es:**
  - Empiezas en recepción sin luz y solo ves con la linterna, que sigue a la cámara.
  - Lo único encendido son los carteles verdes de SALIDA y las luces rojas de emergencia de las escaleras.
  - Los relámpagos dejan ver un instante las siluetas de los abuelos tras las ventanas.
  - Las gafas de los abuelos brillan en la oscuridad.
- **Cuándo acaba:** la sala de calderas está **a dos puertas** de recepción. Se da la luz después de abrir esas dos,
  así que suelen ser 3 o 4 rondas a oscuras.
- **El abuelo del acto 1 es el sonámbulo.** Sale solo a oscuras: en este acto y cuando el jefe corta la luz.
- **Al dar la luz (el momento que cambia la partida):**
  - Se abren los ascensores del vestíbulo y sale una ola de sangría (guiño al ascensor de El Resplandor).
  - El hotel se enciende por partes: lámparas cálidas, la araña del vestíbulo, la piscina turquesa iluminada desde
    dentro, la bola de espejos del Salón Dorado y el neón rosa "HOTEL JUBILES".
  - Empiezan a funcionar los ascensores y los minibares, que son las bebidas.

### La noche de bingo (decidido)
- **Cuándo:** la primera, en la ronda siguiente a abrir el Salón Dorado; después, cada 5 rondas. Si no abres el
  salón, no hay bingo.
- **Cómo se juega:**
  - El animador abre la noche y cada jugador recibe un cartón gratis, que se ve en pantalla.
  - Cada abuelo de esa ronda lleva un número colgado del cuello, y el número brilla.
  - Quien mata a un abuelo con un número de su cartón lo tacha.
  - Los números se reparten para que se pueda cantar bingo en una ronda.
  - El animador canta con la jerga del bingo ("¡el 22, los dos patitos!", "¡el 90, el abuelo!").
- **Premios:** una línea da un power-up; el bingo da el bote de conchas.
- **El reto de la noche:** al empezar, el animador anuncia un reto al azar de la lista. Quien cante bingo
  cumpliéndolo gana la **pulsera todo incluido**.
  - Le da bebidas gratis **solo a quien la lleva**, el resto de la partida.
  - No se pierde al caer (propuesta aceptada con el resto del bingo).
  - Si fallas el reto, hay otra oportunidad 5 rondas después.
- **Luz del bingo:** el hotel en penumbra morada y un foco sobre el animador.
- **Retos** (el usuario los aprobó todos):
  1. **A ukelele:** cantar bingo matando solo con el ukelele.
  2. **Sin que te toquen:** ni un golpe hasta cantar.
  3. **Con lo puesto:** solo con la pistola del principio.
  4. **Todo a la cabeza:** cada número tachado, de un tiro a la cabeza.
  5. **En la pista:** sin salir del Salón Dorado en toda la ronda.
  6. **El abuelo:** sale un abuelo gigante con el 90 y hay que tacharlo a ukelele.
  7. **La niña bonita:** el 15 lo lleva un abuelo que huye; hay que cazarlo antes de cantar.
  8. **Por orden:** tachar una línea de menor a mayor.
  9. **Sin gastar:** ni una concha gastada en la ronda.
  10. **Nadie cae (cooperativo):** el equipo canta bingo sin que caiga nadie. La pulsera es para quien cante.

### La pulsera de puertas (decidido)
- **Dónde está:** escondida en una de tres habitaciones, que cambia en cada partida.
- **Qué hace:**
  - Abre los **pasadizos**: pasillos y escaleras de servicio ("SOLO PERSONAL") que unen las plantas por detrás.
    Funciona solo para quien la lleva.
  - La puerta se cierra a los 3 segundos. Un abuelo que vaya pegado a ti pasa, para cumplir la regla de que te siguen.
  - También abre la azotea para la misión (sustituye a la tarjeta maestra).
- **Si quien la lleva cae, se pierde:** vuelve a esconderse en otra de las habitaciones y hay que encontrarla otra vez
  (decidido; así la misión nunca se queda bloqueada).

### Moverse
- **Ascensores** (como en Five y Die Rise):
  - Se llaman con un botón, tardan y a veces llegan con abuelos dentro.
  - Solo van con luz.
  - Los abuelos usan la escalera, que llega a todas las plantas.
- **La tolva de la lavandería:** un tobogán de un solo sentido desde las plantas hasta el sótano, para escapar. Los
  abuelos también se tiran.
- **Los pasadizos de la pulsera.**
- **Puertas de pago:** los rellanos de escalera de cada planta y las puertas entre las partes de la planta baja.
- **La puerta giratoria de la entrada** te devuelve al vestíbulo ("Check-out no disponible"). Es la idea de Hotel
  California, sin la letra de la canción: del hotel no se sale por la puerta, y por eso el rescate es en helicóptero.

### Trampa: suelo mojado
Pagas, la limpiadora friega un pasillo y los abuelos resbalan y caen (aturdidos; alguno no se levanta). Esas bajas
pagan, como la traca.

### Abuelos
- **El sonámbulo** (pijama y gorro de dormir; la idea viene de la Witch de Left 4 Dead):
  - Camina dormido y no ataca.
  - Si lo enfocas con la linterna un segundo y medio, o le disparas sin matarlo, despierta y carga muy rápido.
  - Para pasar a su lado hay que mirar a otra parte.
  - Matarlo dormido paga extra.
- **El de la moto de movilidad (el triciclo):** muy rápido en recta, gira fatal y no sube escaleras. En los pasillos
  de moqueta es un tren; te salvas metiéndote en el hueco de una puerta.
- **La de la bandeja del bufé:** la bandeja la protege de frente. Hay que tirarle a las piernas, rodearla o darle con
  el ukelele.
- **Las gemelas:** dos abuelas idénticas con vestido azul que van siempre juntas y de la mano ("Ven a jugar al bingo
  con nosotras"). Si matas a una, la otra se enfurece.

### Misión y salida: las tareas del check-out
1. Dar la luz (sala de calderas).
2. Encontrar la pulsera de puertas, que abre la azotea.
3. Despertar al piloto, que duerme la siesta en su habitación de la 2ª planta: hay que llevarle un café de la
   cafetera del bufé en las manos. Se te cae si te pegan y se enfría a los 20 s.
4. Encender las luces del helipuerto (el interruptor está en la 2ª planta, junto a la escalera de la azotea).
5. Llamar al helicóptero desde el teléfono de recepción.

Después, aguantar hasta el final de la ronda siguiente; la puerta de la azotea se abre gratis si quien la abre lleva
la pulsera. Arriba, Don Anselmo en albornoz:
- Sale del ático (la suite presidencial) reventando la puerta a hachazos: "¡Aquí está Anselmo!".
- Durante el combate corta la luz cada cierto tiempo, así que parte de la pelea es a linterna.
- Al vencerlo, se sale en el helicóptero.

### Secretos y referencias (todas aprobadas)
- **El albornoz del hotel:** se lleva en brazos como el disco, y si sales con él, puntuación ×2.
- **Referencias a El Resplandor (en parodia, nada copiado literal):**
  - La moqueta de hexágonos naranjas, rojos y marrones en todos los pasillos de habitaciones.
  - El triciclo: el abuelo de la moto de movilidad.
  - Las gemelas.
  - La habitación 237: su bañera es una entrada de abuelos, y sale una abuela con gorro de baño.
  - El ascensor de sangría al dar la luz.
  - El Salón Dorado con su barman fantasma (una de las bebidas).
  - La máquina de escribir: en el cuarto del conserje, folios con "No por mucho madrugar coges mejor tumbona" repetido.
  - "OGNIB" escrito con pintalabios en una puerta, que en el espejo se lee BINGO.
  - El hacha del jefe.
  - El laberinto de setos.
  - La foto final: al ganar, una foto en sepia del Salón Dorado ("Verbena del Imserso, 4 de julio de 1921") con
    vuestros animales.
- **Las tiendas del hotel:** la caja de armas es "Objetos perdidos", el Microondas está en el cuarto del conserje de
  la 2ª planta (lejos e incómodo, junto a la máquina de escribir) y las bebidas son los minibares.

### Luz
Luz pintada y barata, pensando en Android:
- **Lo que brilla:** emisivos, halos y conos de luz falsos, sombra de las esquinas horneada en los modelos y niebla.
- **Luces reales:** pocas y sin sombras; la linterna tampoco tiene sombras.
- **Si el hotel encendido no queda lo bastante bonito:** hornear dos juegos de lightmaps (apagón y luz) y
  cambiarlos al dar la luz.

### Decisiones del usuario
- **El hotel:**
  - Nombre: Hotel Jubiles.
  - Plantas reales, una encima de otra.
  - Todas las referencias a El Resplandor y la idea de Hotel California.
- **El apagón y el bingo:**
  - Empieza a oscuras y la luz se da después de abrir dos puertas.
  - El evento es el bingo, con animador y retos. Solo hay bingo en este mapa.
- **Las pulseras (solo en este mapa):**
  - La de las bebidas se gana con el reto del bingo y es solo para quien la lleva.
  - La de puertas está escondida y abre pasadizos. Si quien la lleva cae, se pierde y hay que volver a encontrarla.
- **Choque de nombres resuelto:** la carta que se llamaba "Pulsera todo incluido" ahora es **"Media pensión"** (mismo
  efecto: caja un 40 % más barata, bebidas un 50 % más caras).

### Descartado
- **Eventos:**
  - El apagón cada 5 rondas (el evento es el bingo).
  - La "noche de bingo" del README, en la que la megafonía cantaba una planta que pagaba el doble.
- **Pulsera de puertas:**
  - Como llave de un solo uso.
  - Como rebaja a mitad de precio.
  - La tarjeta maestra (la sustituye la pulsera).
- **Otros:**
  - Un hotel de una sola altura por casilla, con una torre de mentira.
  - Que la pulsera todo incluido la soltase un guiri que huye (era la idea del crucero).

### Propuestas sin decidir
- **Final con incendio:** al llamar al helicóptero, la cocina se incendia y las plantas bajas se llenan de humo y luz
  naranja, empujando a todos hacia la azotea.
- **Habitaciones que se registran:** algunas puertas de habitación se abren y dentro hay conchas, munición o nada.
- **Carrito del servicio de habitaciones:** una barricada que se empuja por el pasillo.
- **Un atrio:** que las plantas se asomen al vestíbulo con barandillas, para disparar desde arriba. Se quitó del
  primer plano porque el vestíbulo quedaba debajo del anillo de pasillos; se puede probar más adelante.
- **La piscina para nadar** (como los canales de Venecia con acqua alta). De momento es agua que no se pisa.
- **Arma maravilla del mapa** (como las de los Zombies): **La Aspiradora Industrial**, que absorbe a los abuelos
  cercanos y los escupe contra la pared. Saldría de la caja, poco probable y solo con la luz dada.

---

## 7. Crucero "Reina del Imserso" (en diseño)

Un crucero del Imserso en dos actos: el puerto y la alta mar. Al fondo, la costa de Benidorm con el neón del Hotel
Jubiles.

| | |
|---|---|
| Forma | **plantas reales** (cubiertas, una encima de otra), barco largo y estrecho |
| Contraste | lo ancho: la cubierta de sol con la piscina; lo estrecho: los pasillos de camarotes |
| Inicio | el muelle del puerto (terminal marítima) |
| Evento | **abordaje de lanchas**, rondas 5, 10, 15... |
| Jefe | el capitán Anselmo |
| Salida | arriar el bote salvavidas |

### Acto 1: en el puerto, zarpar (decidido)
- **El puerto:** empiezas en el muelle, con la terminal, la facturación, las maletas y la tienda libre de impuestos.
  Hay otras dos zonas del puerto que se abren pagando.
- **La tarjeta de embarque** está siempre en la **segunda** zona del puerto que abras (la misma regla que el mechero
  del pueblo). Con ella se cruza la pasarela al barco.
- **La salida, en la ronda 4:** al empezar la ronda suena la bocina ("¡Último aviso para embarcar!") y aparece una
  **cuenta atrás de 2 minutos**. Al llegar a cero, el barco zarpa y el puerto se queda atrás para siempre.
- **Si no has embarcado:**
  - **En solitario:** pierdes la partida.
  - **En cooperativo:** se queda fuera solo quien no subió, que vuelve a bordo en la ronda siguiente, como un jugador
    que se ha desangrado. Si no ha subido nadie, perdéis.
- **La luz del acto 1:** la hora dorada en el puerto, como la de Venecia. El sol baja un poco cada ronda y el barco
  zarpa al atardecer.

### Acto 2: en alta mar
- **La luz:** de noche, el barco encendido como una ciudad:
  - Filas de ojos de buey iluminados.
  - Guirnaldas en cubierta y la chimenea iluminada.
  - La luna brillando en el agua low poly de Venecia.
  - La costa a lo lejos.
- **Puertas de pago:** mamparos estancos con volante rojo.

### Plano (diseñado el 2026-11-17)
Plano completo, números y trabajo: `Docs/CRUCERO_DISENO.md`. Se genera con `Tools/Maps/make_crucero_layout.py`, que
pasa sus comprobaciones. 5184 m² en tres plantas. El barco mira al este; el muelle está a estribor (sur).

| Zona | Planta | Qué hay |
|---|---|---|
| El muelle (inicio) | 0 | contenedores, bolardos, la puerta de la pasarela |
| Terminal marítima | 0 | facturación, tienda libre de impuestos; puede tener la tarjeta de embarque |
| Almacén de equipajes | 0 | maletas, contenedores; puede tener la tarjeta |
| Paseo de cubierta | 4 m | el paseo que rodea el barco (el circuito), popa, proa y atrio |
| Cubierta de camarotes (la estrecha) | 0, dentro del casco | dos pasillos largos con cruces, sala de máquinas, marina de popa |
| Casino y teatro | 4 m | tragaperras (la caja), ruleta, escenario con karaoke |
| Comedor | 4 m | mesas redondas, bufé |
| Cubierta de sol (la ancha) | 8 m | piscina, jacuzzis, chimenea, cabañas, pistas de tejo, torre de toboganes con el Microondas arriba |
| Puente de mando (jefe) | 8 m | un anillo alrededor de la caseta del timón: el capitán y la manivela del bote |

Puertas: Terminal 750 · Almacén 750 · Escalera central (camarotes) 1000 · Casino y teatro 1000 · Comedor 1250 ·
Escalera de popa (marina) 1250 · Escalera de proa (cubierta de sol) 1500 · Escalera de popa (cubierta de sol) 1500 ·
Puente de mando (misión + llave del bote). La pasarela no es de pago: la abre la tarjeta.

### Abordaje cada 5 rondas (decidido: rondas 5, 10, 15...)
- **Cómo llegan:**
  - Suena la sirena: "¡Lanchas a estribor!".
  - Llegan 3 o 4 lanchas del Imserso con focos que barren la cubierta.
  - Los abuelos trepan por cuerdas con garfios a lo largo de la borda, muchos a la vez y por un solo lado.
- **Cómo se defienden:** manteniendo □ se corta un garfio, y caen al agua todos los que suben por él. Esas bajas
  pagan.
- **Cada abordaje viene por un lado distinto.**
- **El primero llega justo después de zarpar:** son los abuelos que se quedaron en el puerto, que os persiguen en
  lanchas.

### Moverse
- **Toboganes del parque acuático de la cubierta de sol:**
  - Uno baja a la piscina.
  - El otro sale por fuera del casco y te deja en popa.
  - Solo bajan, y los abuelos también los usan.
- **Motos de agua (decidido: transporte):**
  - Salen de la marina de popa y rodean el casco hasta la escalerilla de proa, y al revés.
  - Son de pago, como la góndola.
  - Los abuelos os siguen en lanchas.
- **Por la borda:** si te caes, subes por una escalerilla y te cuesta algo de vida (como el rescate de los canales de
  Venecia).

### Abuelos
- **El del flotador:** los tiros al cuerpo rebotan; hay que darle en la cabeza o con el ukelele.
- **El grupo de aquagym:**
  - Un bloque de 4 o 5 con churros de piscina que avanza a la vez y tapa el pasillo.
  - Si matas a la monitora del silbato, se dispersan.

### Misión y salida: las tareas del zafarrancho
1. Los tres discos de Julio al karaoke del teatro.
2. Las cartas náuticas del capitán (en uno de tres camarotes, cambia cada partida).
3. La gasolina del bote (en la marina).
4. Dar corriente a los pescantes (sala de máquinas).
5. La llave del bote (en uno de tres sitios del paseo).

Con las cuatro primeras, la puerta del puente se abre gratis si quien la abre lleva la llave.
- **El jefe:** el capitán Anselmo, en el puente de mando. Su bocinazo es una onda que empuja y te puede tirar por la
  borda.
- **La salida:** después del capitán, mantener la manivela del bote 20 s en total (se para si te pegan) con la horda
  encima. Con el disco de oro, ×2.
- **Las tiendas:** la caja de armas es "La Tragaperras" del casino; las bebidas funcionan desde el principio (el
  barco tiene luz).

### Decisiones del usuario
- **El puerto:**
  - El barco zarpa en la ronda 4, con cuenta atrás de 2 minutos.
  - Si no has embarcado, pierdes. En cooperativo, solo se queda fuera él.
  - La tarjeta de embarque, en la segunda zona del puerto que abras.
- **En alta mar:**
  - Abordajes en las rondas 5, 10 y 15.
  - Motos de agua para rodear el barco.
- **El barco:**
  - Plantas reales.
  - Sin bingo y sin pulseras (son del hotel).

### Descartado
- **Otros momentos que cambiaban la partida:**
  - **La vía de agua:** en el primer abordaje una lancha embestía el casco y la cubierta inferior se inundaba para
    siempre.
  - **Ningún momento:** el barco igual de principio a fin.
- **Bingo y pulsera:**
  - El bingo del Salón Neptuno (se fue al hotel).
  - La pulsera todo incluido, que soltaba "el guiri de la pulsera dorada" al huir hacia los botes (se fue al hotel, y
    allí se gana con un reto).
- **Motos de agua como huida final.**
- **La marejada del README:** el barco escoraba, las tumbonas resbalaban y la mira se balanceaba.

### Propuestas sin decidir
- **El guiri que huye**, con otro premio: por ejemplo, el protector solar (un power-up) o un fajo de conchas.
- **La cena de gala con el capitán:** un evento único a mitad de partida en el comedor.
- **La marejada** como segundo evento más adelante.
- **Un objeto propio para sacar del barco** en lugar del disco de oro (por ejemplo, la toalla de reservar tumbona).
- **El tobogán de evacuación** del barco como parte de la salida.
- **Una trampa propia** (el hotel tiene el suelo mojado).
- **Arma maravilla del mapa:** **La Manguera Contraincendios**, que empuja a los abuelos y los tira por la borda.
  Encaja con el abordaje: barrer una borda llena de garfios.

---

## Cómo se van a hacer el hotel y el crucero
**Diseño hecho (2026-11-17).** Los documentos de trabajo:
- **Plantas reales:** `Docs/PLANTAS_FORMATO.md` (formato, lo que construye el setup, comprobaciones, mapa de prueba).
- **Mapas:** `Docs/HOTEL_JUBILES_DISENO.md` y `Docs/CRUCERO_DISENO.md` (plano, reglas con sus números, luz, modelos,
  online, pruebas).
- **Planos:** `Tools/Maps/make_hotel_layout.py` y `Tools/Maps/make_crucero_layout.py`, con las comprobaciones
  comunes en `Tools/Maps/floors.py`. Pasan con `--check`.

Orden de construcción:
1. **Primero, lo que comparten** (en una sola sesión):
   - Plantas reales en el setup de Unity, con su mapa de prueba.
   - El kit de luz barata: grupos de emisivos que se encienden, halos y conos falsos, la linterna.
   - Las piezas comunes: ascensor, tolva y tobogán entre plantas, puerta de servicio.
2. **A la vez que el paso 1:** los modelos de Blender de los dos mapas, que no dependen de las plantas.
3. **Después, cada mapa en sus propios archivos**, que se pueden construir a la vez en dos sesiones: tema, reglas
   propias y su prueba. Los archivos comunes (protocolo online, selector de mapas, `CLAUDE.md`) los une una sola
   sesión.
4. **Por último, cada mapa con sus pruebas** (las de `Docs/GUIA_NUEVO_MAPA.md`), y este documento al día.

**Hecho (2026-11-17, tarde):** el paso 1 salvo el kit de luz. Las plantas reales funcionan (mapa de prueba con 21
comprobaciones jugando, todas bien) y el hotel y el crucero ya se construyen en Unity sin errores, todavía con el
aspecto genérico de Isla del Faro. En el crucero se añadieron entradas de abuelos (puertas de tripulación a lo largo del
paseo, escalerillas de la piscina) porque el paseo tenía demasiados sitios donde los abuelos llegaban por un solo lado.
Los modelos de Blender (paso 2) están en marcha en dos sesiones aparte.
