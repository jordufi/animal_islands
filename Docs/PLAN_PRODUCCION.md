# Animal Island — plan de producción (revisión de diseño, 2026-11-14)

Revisión hecha por Fable **solo leyendo** datos (`Assets/Data`), el layout de Isla del Faro, `TestMapSetup.cs` y los
run logs. **No se ha abierto Unity ni se ha jugado**: los bugs del mapa Original salen de la descripción del usuario y
hay que reproducirlos. No se han leído: vida del jugador, vida del jefe, precio del Microondas, respuestas de la
encuesta de playtest (paso 7 de CLAUDE.md). Quien ejecute cada fase (Sonnet / Opus) debe verificar con los play tests.

Nombres: **Original** = el primer mapa (BeachIsland, con relieve y escaleras). **Isla del Faro** = el segundo (rejilla plana).

---

## 1. Diagnóstico

### 1.1 El problema nº 1 es el ritmo de la partida, no los números sueltos
- Partidas reales del usuario en Original (2026-11-02): **victoria en 465–470 s (≈8 min), ronda 7**, 13.800–18.500
  conchas ganadas, 5 puertas. El objetivo acordado es **20–40 min**.
- Causa: llegar al jefe solo cuesta dinero, y poco. Original: camino mínimo 1000 + 1250 + 2500 = **4.750**. Isla del
  Faro: 750 + 1000 y la **Llave Maestra puede salir en la Cueva** (que se abre con esa misma puerta de 1000) → Muelle
  gratis con **1.750 conchas, ronda 2–3**.
- Consecuencia: nunca se llega a las rondas donde importan el Microondas, las bebidas, los culturistas (ronda 7) o la
  segunda noche (ronda 10). Todo ese contenido está sin usar. Afinar daño/vida antes de arreglar esto es perder el tiempo.

### 1.2 Economía (datos actuales)
| Cosa | Ahora | Observación |
|---|---|---|
| Ukelele | 350 de daño, mata de un golpe hasta ronda 3, paga 130, embestida 3,5 m | En los logs, rondas 1–2: **85 % del ingreso es melee**. En BO el cuchillo solo mata de un golpe en ronda 1. |
| Baja normal / cabeza | 60 / 65 (+10 por impacto) | Matar con 3 tiros al cuerpo paga 90; con 1 a la cabeza paga 75. **Apuntar bien paga menos.** |
| Vida abuelo | 150 + 100·(r−1), ×1,1 desde r9 | Igual que BO, bien. r5 550 · r10 ≈1.045 · r15 ≈1.680 · r20 ≈2.700 |
| Cantidad | 6 + 2,2r + 0,08r² (r1 8, r5 19, r10 36), máx. 24 vivos | Bien. |
| Thermomix (SMG) | 70 × 13 disparos/s ≈ 930 DPS, 280 balas | Mata rápido pero cobra 10 por bala: en la ronda final 3.000 conchas solo de impactos. |
| Catalejo | 700, penetra 4, ×4 cabeza | Un tiro hasta ronda 6 al cuerpo y ~ronda 25 a la cabeza. Sin contrapartida salvo 35 balas. |
| Armas de pared | Solo la Lentos-peta (500) | La caja es 1 uso por zona: conseguir arma es pura suerte. En BO la pared es la columna vertebral. |
| Puertas Isla del Faro | 20.500 todas; 10.500 con llave | Ingreso real ≈1.200/ronda al principio, ≈2.500 en ronda 6. |
| Daño abuelo | 25 (culturista 40) | Comprobar la vida del jugador: debería ser **3 golpes sin Piña, 5 con Piña**. |

### 1.3 Mapa Original (divertido pero roto)
- Tiene lo que le falta al otro: **relieve, escaleras, siluetas distintas por zona**. Por eso divierte.
- Rotos descritos por el usuario: (a) zonas donde los abuelos no pueden pasar (NavMesh cortado o sin enlace);
  (b) junto a la primera puerta / la «Escalera VIP» hay un borde donde **falta una valla**: parece que se puede saltar
  abajo pero hay un muro invisible porque abajo es zona de pago.
- No tiene las comprobaciones automáticas que sí tiene Isla del Faro (grafo de zonas sellado, cada spawn alcanzable,
  cobertura de spawns), ni entradas visibles, ni mapa, ni carteles.

### 1.4 Isla del Faro (correcto pero plano)
- La rejilla 2D de 2 m no tiene alturas: todo el mapa es un suelo. Sin altura no hay siluetas, ni «estoy arriba / me
  rodean abajo», ni atajos de caída.
- **Demasiados callejones**: Volley y Mercadillo solo tienen una puerta; la Cueva es un laberinto de 1 celda. En un
  juego de rondas un callejón solo es bueno si guarda el mejor premio; si no, nadie entra.
- Casi todo son pasillos de 1 celda con props en damero (Camping, Volley, Mercadillo se leen igual). Falta alternar
  **cuello de botella → sala con circuito** (donde «entrenas» la horda en círculo) → cuello de botella.
- El faro es el icono del mapa y no se puede subir.
- Nada obliga a recorrer el mapa: no hay «dar la luz» ni objetivo intermedio.

### 1.5 Lo que separa esto de un juego «de producción»
Por orden de impacto: **audio (no hay nada)** → sensación de impacto (hit markers, sonido de baja, retroceso de
cámara, cabezas) → objetivos y power-ups → modelos de máquinas/puertas → animaciones que faltan → menú/tutorial/ajustes.

---

## 2. Reglas de diseño de niveles (para arreglar los dos mapas y para los nuevos)

1. **Si parece que se puede, se puede; si no se puede, se ve.** Ningún muro invisible: valla, seto, barandilla o
   precipicio de verdad. Las caídas que saltan una puerta de pago se cierran con algo visible.
2. **Cada zona = un verbo.** Una idea jugable por zona (circuito ancho, pasillo largo de francotirador, laberinto,
   balcón, agua que frena). Si dos zonas se juegan igual, sobra una.
3. **Ritmo cuello → circuito.** Cada zona tiene al menos un circuito de 25–40 m de perímetro con carril de 3–4 m,
   conectado por cuellos de 2 m. Las líneas de visión no pasan de ~20 m.
4. **Máximo 2 callejones por mapa**, y solo si guardan lo mejor (Microondas, bebida cara, pieza de misión).
5. **Toda zona con ≥2 salidas** una vez abierta. Mejor aún si una es **de un solo sentido** (salto abajo, tobogán,
   tirolina): crea atajos de huida sin romper el grafo de puertas.
6. **Tres alturas como mínimo** (playa / terraza / mirador) con rampas y escaleras anchas; los abuelos suben por los
   mismos sitios (nada de posiciones inalcanzables).
7. **Un hito visible desde todo el mapa** (faro, noria, chimenea del crucero) para orientarse sin minimapa.
8. **El objetivo lejano primero.** «Dar la luz» está en la zona más lejana del inicio; el premio gordo cerca del
   inicio pero apagado hasta entonces. Eso fabrica idas y vueltas.
9. **Entradas de abuelos a ≤15 m de cualquier punto**, visibles, nunca dos direcciones seguras a la vez.
10. **Cada mapa aporta 4 cosas únicas**: una mecánica, un arma maravillosa, un objeto extraíble, un jefe.

---

## 3. Fases

Cada fase termina con sus play tests en verde y CLAUDE.md / README actualizados. Recomendación de modelo entre paréntesis.

### Fase 0 — Medir y arreglar lo roto (Sonnet)
**Estado 2026-11-14: hecha en lo esencial (ver `CLAUDE.md`, sección Status 2026-11-14). Pendiente de esta fase: caseta de spawn en Cementerio/Muelle (5 puntos), el conflicto Jungla-Campamento de `OriginalMapBorders.Compute`, y volver a pasar los demás play tests (Coop, Online, IslaZombie, Feedback).**

- Run log: añadir **mapa**, nº de jugadores, vida de los abuelos por ronda, daño recibido, caídas, tiempo entre
  rondas, munición gastada por arma, y posición de cada muerte / caída (para mapas de calor).
- Original: portar las comprobaciones de `IslaZombieSetup` (grupos sellados con puertas cerradas, cada puerta une
  solo sus dos zonas, todo spawn alcanza al jugador en su zona, 90 paseos aleatorios de `BasePlayTest`). La lista de
  fallos que salga es la lista de arreglos.
- Original: reproducir y arreglar el borde junto a la Escalera VIP (poner valla / barandilla donde está el muro
  invisible) y repasar **todos** los bordes entre zonas de distinto precio con el mismo criterio.
- Original: entradas de spawn visibles (reusar `SpawnEntrance`) y mapa de pausa.
- Hecho cuando: 0 celdas inalcanzables, 0 muros invisibles en la auditoría, `MapSelectorPlayTest` con ronda 1–5.

### Fase 1 — Estructura de la partida (Opus: toca flujo, online y los dos mapas)
**Estado 2026-11-15: HECHA y probada (Phase1PlayTest, IslaZombie, Online 69/0, Coop, Feedback, Base, Original). Pendiente: «Bingo» y registrar misión y power-ups en el run log.** Antes: código escrito por Fable, compila, SIN reconstruir mapas y SIN probar. La lista de trabajo para
terminarla (reconstruir, adaptar tests, test nuevo, regresión, riesgos) está en [FASE1_TRASPASO.md](FASE1_TRASPASO.md).
Cambios respecto a lo escrito abajo: la radio está en el Templo, la emisora y la luz en el Faro (no «arriba», el faro
aún no se sube); la llave vale por una pieza; no hay «Bingo»; la vida del jefe pasa a la Fase 2.**

Objetivo: que llegar al jefe exija **recorrer el mapa y llegar a ronda 12–15**, no solo pagar.
- **«Dar la luz»**: interruptor en la zona más lejana (Isla del Faro: arriba del faro). Sin luz no funcionan bebidas ni
  Microondas. Megafonía: «Se ruega a los señores clientes no tocar el cuadro eléctrico».
- **Misión del barco en 3 pasos** en vez de una puerta de 10.000: gasolina (Parking), radio (Cueva o Templo), llamar
  desde el faro y aguantar una ronda de defensa. La Llave Maestra pasa a ser un **atajo de un paso**, no de todo, y
  deja de poder salir en la Cueva.
- Jefe: su vida escala con la ronda en la que llegas (no premia correr) y su fase de refuerzos no paga conchas por impacto.
- **Power-ups clásicos** al matar (2–3 % por baja, máx. 4 por ronda): Insta-kill «Siesta fulminante», Puntos dobles
  «Happy hour», Bomba «Paella para todos», Carpintero no aplica → cambiarlo por «Bingo» (todos reciben arma de caja 30 s).
- Online: cada cosa nueva decide su lado host/cliente (ver «Online» en CLAUDE.md) y sube `Version`.
- Hecho cuando: partida escriptada completa dura 20–30 min simulados y los logs muestran jefe en ronda ≥12.

### Fase 2 — Balance con números (Sonnet, con hoja de TTK por ronda)
**Estado 2026-11-15: números de partida aplicados por Fable (compila, Isla del Faro reconstruida), SIN play tests.
Qué cambió, avisos de la hoja teórica y lista de pruebas: [FASE2_TRASPASO.md](FASE2_TRASPASO.md). Cambios respecto a
lo de abajo: las puertas no se tocan (10.500, el muelle ya es gratis), jefe 45 × en vez de 60 × (por munición), no hay
pistola de pared.**

Puntos de partida (ajustar con los logs nuevos de Fase 0, cambiando **solo** `Assets/Data`):
- Ukelele: daño 350 → **150** (un golpe solo en ronda 1, dos en ronda 2–3), paga 130 → **100**; embestida 3,5 → 2,5 m.
- Baja a la cabeza 65 → **100**; baja normal 60 → **50** (como BO: apuntar paga).
- Impacto de armas automáticas: 10 → **5** para cadencias > 8/s (o tope de conchas por abuelo = 2× su baja).
- Armas de pared en cada mitad del mapa: pistola mejorada 750, escopeta 1.500, SMG 2.500 (munición a mitad de precio).
  La caja queda para las armas raras.
- Puertas Isla del Faro: escalar de 750 → 3.000 de forma que abrirlo todo ≈ 25.000 y el ingreso lo permita en ronda 10–12.
- Daño de los abuelos: fijar «3 golpes sin Piña». Chándal: vida 0,8 → 0,6 (rápidos pero de papel). Culturista desde ronda 7 bien.
- Cartas: hoy son ±% planos. Fase 5 las hace cambiar reglas; aquí solo comprobar que ninguna rompe (–20 % conchas + 50 % caja).
- Hecho cuando: en logs de bot y de humano, ingreso/ronda y TTK por arma están dentro de la tabla objetivo de abajo.

#### Tabla objetivo de la Fase 2 (un jugador, dificultad Normal)
Supuestos: ronda media 75 s + pausa 10 s; jefe en ronda 12–15; partida 25–30 min.

| Ronda | Abuelos | Vida abuelo | Conchas ganadas en la ronda | Acumulado | Qué debería poder tener el jugador |
|---|---|---|---|---|---|
| 1 | 8 | 150 | 700–900 | ~1.300 (con las 500 iniciales) | Primera puerta (750) |
| 3 | 13 | 350 | 1.300–1.600 | ~4.500 | Segunda puerta + arma de pared |
| 5 (noche) | 11 | 550 ×0,9 | 1.400–1.800 | ~8.000 | Luz dada, una bebida |
| 7 | 25 | 750 | 2.800–3.300 | ~14.000 | Templo abierto (5.000), radio |
| 10 (noche) | 22 | ~1.045 ×0,9 | 3.000–3.500 | ~25.000–30.000 | Microondas en un arma, 2–3 bebidas |
| 12 | 44 | ~1.265 | 5.000–6.000 | ~40.000 | Barco llamado, segunda arma mejorada |
| 14 | 52 | ~1.530 | 6.000–7.000 | ~50.000–55.000 | Jefe |

Gasto previsto antes del jefe en Isla del Faro (≈40.000): puertas 10.500 (el muelle ya es gratis), bebidas 9.000,
Microondas 10.000 (dos armas), caja ~5.000, armas y munición de pared ~5.000.

**Tiempo para matar (TTK) objetivo**, a un abuelo normal, disparando al cuerpo sin fallar:

| Arma | Ronda 1 | Ronda 5 | Ronda 10 | Ronda 14 mejorada |
|---|---|---|---|---|
| Pistola | ≤ 0,6 s | 2–2,5 s | no viable (> 4 s) | — |
| Escopeta (a 3 m) | 1 tiro | 2 tiros | 4 tiros | 3 tiros |
| Thermomix | — | ≤ 0,7 s | 1,0–1,3 s | ≤ 1,0 s |
| Catalejo | 1 tiro | 1 tiro | 2 tiros al cuerpo, 1 a la cabeza | 1 tiro |
| Ukelele | 1 golpe | 4 golpes | no viable | — |

**Jefe** (sacar `healthInEnemies` y `minHealth` de `BossEncounter` a `Data/Economy.asset` o a un `BossData`):
objetivo 2,5–4 min de pelea con dos armas mejoradas. Punto de partida: vida = **60 ×** la de un abuelo de la ronda,
mínimo **30.000** (hoy 25 × y 8.000: en ronda 7 son 18.750, unos 20 s de Thermomix). El mínimo alto es lo que impide
correr al jefe en ronda 6 con la pistola.

**Otros números a fijar en esta fase** (todos en `Assets/Data`): `missionDefenseRounds` (1 → probar 2),
probabilidades de `PowerUps.asset`, coste del Microondas (5.000, hoy en el propio `PackAPunch`: moverlo a datos),
`downPenalty` en cooperativo, y la curva de 2–4 jugadores (+50 % abuelos por jugador extra con un solo bote puede
quedarse corta de conchas: medir conchas por jugador y ronda).

**Método**: (1) hoja de cálculo o script que saque ingreso y TTK teóricos de los `.asset`; (2) bot de `BasePlayTest`
jugando 10 rondas con cada arma y el run log nuevo; (3) dos partidas reales del usuario; (4) ajustar y repetir. Cambiar
un grupo de números cada vez (primero ingreso, luego precios, luego jefe).

### Fase 3 — Rediseño de Isla del Faro: altura y circuitos (Opus diseña el formato; Sonnet aplica)
- **Formato**: añadir al layout una segunda rejilla `[heights]` (un dígito por celda, nivel × 1,5 m) y marcadores de
  rampa / escalera / caída de un sentido. El setup ya hornea NavMesh; hay que generar suelos, taludes y barandillas.
- Cambios concretos propuestos (respetan lo decidido: solo dos caminos de oeste a este):
  - **Faro escalable**: rampa en espiral hasta un balcón (interruptor de la luz, puesto de francotirador con una sola
    salida + **salto al agua/lago** de un sentido).
  - **Camping en dos terrazas** con un circuito claro alrededor de las tiendas centrales (quitar el damero de props).
  - **Volley**: pista hundida entre gradas (ya existe `IZ_Grada`) = arena con circuito; segunda salida de un sentido
    (tobogán / duna) de vuelta al Chiringuito.
  - **Mercadillo**: calle principal de 4 m + callejones de 2 m entre puestos, toldos por los que saltan los senderistas;
    caída de un sentido desde el Muelle para que deje de ser callejón.
  - **Cueva**: mantener laberinto pero con una sala central con circuito y un tragaluz (hito de orientación).
  - **Templo**: bajar medio nivel (escaleras) para que el Microondas se sienta «cripta».
- Hecho cuando: pasan los checks del setup + cobertura de spawns, cada zona tiene ≥1 circuito medido, máx. 2 callejones,
  y las capturas (`Capture Isla Zombie Map`) muestran 3 alturas.

#### Especificación del formato con alturas (Fase 3)
- **Sección nueva `[heights]`** en el layout, misma anchura y altura que `[map]`: un carácter por celda.
  `0`–`4` = nivel (× **1,5 m**); `.` = igual que el vecino (para no rellenar todo). Si falta la sección, todo es nivel 0
  (los mapas actuales siguen valiendo).
- **Rampas y escaleras en `[map]`**: `^ v < >` = rampa de 1 celda que sube un nivel en esa dirección (3,5 m de
  recorrido útil → pendiente ~37°, por debajo de los 45° de la malla); `=` = escalera ancha (dos celdas seguidas hacen
  una rampa de 2 celdas, ~20°). La celda de rampa pertenece a la zona del suelo de abajo.
- **Caída de un sentido**: `!` en el borde alto. Genera hueco en la barandilla y un `NavMeshLink` unidireccional hacia
  abajo (los abuelos también saltan; de vuelta no). Altura máxima 2 niveles (3 m) y sin daño de caída.
- **Lo que genera el setup**: suelo por celda a su altura; **talud o muro** en cada borde entre niveles distintos
  (capa Wall, con textura de roca); **barandilla visible** de 1 m en los bordes altos sin rampa ni `!` (regla 1: lo
  que no se puede saltar, se ve). Props, entradas de spawn, máquinas y marcadores se colocan a la altura de su celda.
- **Mallas**: las dos (normal y Saltador) se hornean igual; añadir `NavMeshLink` en las caídas. Subir
  `agentClimb` no hace falta: los cambios de nivel son siempre por rampa.
- **Checks nuevos del setup** (fallan en voz alta): toda celda de suelo alcanzable andando desde el inicio con las
  puertas abiertas; ninguna rampa desemboca en muro; ninguna caída `!` salta una puerta de pago hacia una zona
  cerrada (comparar grupos); cada zona marcada con `circuito` en `[zones]` tiene un ciclo de celdas de ≥ 25 m con
  carril ≥ 2 celdas en al menos la mitad; máximo 2 zonas con una sola salida.
- **Cámara y combate**: comprobar la cámara en rampas y bajo voladizos, que las balas no atraviesan taludes y que
  los senderistas (Saltador) no usan las barandillas como atajo entre niveles.
- **Orden de trabajo**: (1) formato + generación con un mapa de prueba de 10×10; (2) checks; (3) aplicar a Isla del
  Faro zona por zona (Faro → Camping → Volley → Mercadillo → Templo → Cueva), con captura y play tests tras cada una;
  (4) carteles «ESTÁS AQUÍ» y mapa de pausa regenerados.
- **Riesgo principal**: `IslaZombieSetup.cs` asume y = 0 en muchos sitios (`World(c, r)`, colliders de props, entradas,
  huellas). Empezar buscando todos los usos de `World(` y de alturas fijas antes de escribir nada.

### Fase 4 — Sensación de juego y audio (Sonnet)
- Audio completo: armas, impactos, bajas, pasos, puertas de spawn, megafonía del resort como narrador, cambio de
  ronda con pasodoble, noche con grillos y transistor. Mezcla con prioridades (máx. voces simultáneas).
- Hit markers (blanco / rojo en baja / dorado en cabeza), números de daño opcionales, cabezas que saltan (gafas, boina),
  cámara: golpe al recibir daño, vibración del mando.
- Animaciones que faltan (Blender): trepar, lanzar, golpe de ukelele, recarga, caminar hacia atrás.
- Modelos para puertas, máquinas de bebida, caja y Microondas (hoy cubos).
- Flecha / texto de objetivo.

### Fase 5 — Contenido (diseño con el usuario; Opus propone, Sonnet implementa)
- **Enemigos con mecánica, no solo con estadísticas**: abuela del carrito (carga en línea recta), el del andador
  (escudo frontal, hay que rodearlo), petanquista (bolas que dejan zona peligrosa), el del silbato (acelera a los de
  alrededor: objetivo prioritario), señora del bingo (grita «¡BINGO!» y llama una oleada extra).
- Arma maravillosa de Isla del Faro (solo caja): p. ej. «El Sifón» (chorro que empuja y congela) o «La Paellera» (disco que rebota).
- Cartas que cambian reglas («los abuelos explotan en confeti y dañan», «la caja siempre da escopeta», «cada puerta
  abre también una entrada nueva»), bebidas con efectos secundarios reales (hipo que mueve la mira, etc.).
- Más objetos extraíbles por mapa.

### Fase 6 — Mapas nuevos (plantilla: sección 2 + formato con alturas de Fase 3)
| Mapa | Mecánica única | Jefe |
|---|---|---|
| **Crucero del Imserso** | Cubiertas verticales, ascensores como puertas de pago, el barco se escora cada X rondas | El capitán del crucero |
| **Hotel Todo Incluido (Benidorm)** | Torre: bufé, piscina, azotea; extracción en helicóptero; toboganes de un sentido | El animador del hotel |
| **Pueblo en fiestas** | Plaza + callejuelas; cada 5 rondas «encierro»: vaquillas cruzan las calles y arrollan a todos | El alcalde / la charanga |
| **Residencia Los Rosales** | Interior, origen del brote; sala de bingo; cortes de luz | La directora |
| **Parque acuático** | Toboganes = viaje rápido; agua que frena a jugador y abuelos | El socorrista |

### Fase 7 — Producción y salida
- Rendimiento (4 vistas, 24 abuelos, 69 entradas), opciones gráficas, remapeo de mandos, tutorial de 2 minutos,
  créditos, pruebas online reales a 3–4 jugadores, guardado de récords por mapa.
- **Antes de vender**: revisar nombres reales y marcas (Julio Iglesias, Thermomix, Imserso, Call of Duty en textos) →
  parodias propias; licencia de toda la música y sonidos.
- Página de tienda, tráiler, demo con un mapa.

---

## 4. Orden recomendado y quién lo hace
0 → 1 → 2 → 3 → 4 → 5 → 6 → 7. Las fases 0, 2 y 4 son mecánicas y bien acotadas: **Sonnet**. Las fases 1 y 3 tocan
arquitectura (flujo de partida, online, formato del mapa): **Opus**, y luego Sonnet para aplicar y probar.
No empezar la Fase 2 antes de la 1: los números dependen de cuánto dura la partida.
