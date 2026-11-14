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
Puntos de partida (ajustar con los logs nuevos de Fase 0, cambiando **solo** `Assets/Data`):
- Ukelele: daño 350 → **150** (un golpe solo en ronda 1, dos en ronda 2–3), paga 130 → **100**; embestida 3,5 → 2,5 m.
- Baja a la cabeza 65 → **100**; baja normal 60 → **50** (como BO: apuntar paga).
- Impacto de armas automáticas: 10 → **5** para cadencias > 8/s (o tope de conchas por abuelo = 2× su baja).
- Armas de pared en cada mitad del mapa: pistola mejorada 750, escopeta 1.500, SMG 2.500 (munición a mitad de precio).
  La caja queda para las armas raras.
- Puertas Isla del Faro: escalar de 750 → 3.000 de forma que abrirlo todo ≈ 25.000 y el ingreso lo permita en ronda 10–12.
- Daño de los abuelos: fijar «3 golpes sin Piña». Chándal: vida 0,8 → 0,6 (rápidos pero de papel). Culturista desde ronda 7 bien.
- Cartas: hoy son ±% planos. Fase 5 las hace cambiar reglas; aquí solo comprobar que ninguna rompe (–20 % conchas + 50 % caja).
- Hecho cuando: en logs de bot y de humano, ingreso/ronda y TTK por arma están dentro de la tabla objetivo que se escriba aquí.

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
