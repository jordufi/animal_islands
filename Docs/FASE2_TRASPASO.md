# Fase 2 — traspaso a Sonnet / Opus (2026-11-15)

Fable aplicó los números de partida de la Fase 2 («balance con números») y el código que hacía falta para tenerlos
en datos. **Compila, Isla del Faro está reconstruida con su check en verde, pero no se ha pasado ningún play test.**
Nada de la Fase 2 está commiteado (último commit: `ee43285`, Fase 1).

## 1. Qué ha cambiado

| Cosa | Antes | Ahora | Dónde |
|---|---|---|---|
| Baja normal / cabeza | 60 / 65 | **50 / 100** | `Data/Economy.asset` |
| Baja con ukelele | 130 | **100** | `Data/Economy.asset` |
| Impacto con arma rápida (> 8 disparos/s: Thermomix) | 10 | **5** (`fastHitPoints`, `fastFireRate`) | `EconomyData.HitPointsFor`, `EnemyHits.HitPay` |
| Ukelele | 350 de daño, embestida 3,5 m | **150**, **2,5 m** | `Data/Weapons/Ukulele.asset` |
| Daño de los abuelos | 25 (culturista 40, senderista 20) | **35** (culturista **50**, senderista **25**) | `Data/Enemies/*.asset` |
| Chándal | vida ×0,8 | **×0,6** | `Data/Enemies/JubiladoChandal.asset` |
| Catalejo mejorado | daño ×2 | **×2,5** (un tiro en ronda 14) | `Data/Weapons/Sniper.asset` |
| Vida del jefe | 25 × abuelo, mín. 8.000 (en `BossEncounter`) | **45 ×, mín. 30.000** (`bossHealthInEnemies`, `bossMinHealth`) | `Data/Economy.asset` |
| Coste del Microondas | 5.000 en `PackAPunch` | 5.000 en `Economy.asset` (`packAPunchCost`) | `PackAPunch.cs` |
| Armas de pared (Isla del Faro) | Lentos-peta 500 (Camping) | + **La Chancla 1.500** (Parking, pared oeste) y **La Thermomix 2.500** (Lago, pared este); munición a mitad de precio | layout: marcadores `U` y `M`; `WeaponData.wallCost` |
| Run log | — | eventos `LUZ`, `GASOLINA`, `RADIO`, `LLAVE POR PIEZA`, `LLAMADA`, `BARCO`, `POWERUP <tipo>` | `RunLog.Note` |
| Hoja de balance | — | `python Tools/balance_sheet.py` (lee los `.asset`) | `Tools/balance_sheet.py` |

Con 100 de vida y 35 de daño son **3 golpes sin Piña y 5 con Piña** (175 de vida). Los precios de las puertas no se
han tocado: la tabla objetivo del plan ya cuenta con 10.500 (el muelle es gratis desde la Fase 1).

Tests ya adaptados sin ejecutar: `FeedbackPlayTest` (busca la Lentos-peta por id; la baja a la cabeza se compara con
`Economy`), `OnlinePlayTest` (ídem), `BasePlayTest` (la embestida usa `lungeRange − 0,4`).

## 2. Lo que dice la hoja teórica (un jugador, Normal, tiros al cuerpo sin fallar)

- Ingreso por ronda dentro de la tabla objetivo: ronda 1 = 560–800, ronda 3 = 780–1.560, ronda 7 con Thermomix 2.500,
  ronda 12 con Thermomix mejorada 4.180, ronda 14 = 5.200.
- TTK dentro de la tabla para todas las armas (Thermomix 0,5 s en ronda 5 y 1,0 s en ronda 10; Chancla 2 tiros en
  ronda 5 y 3 en ronda 7–10).
- **Avisos que salen de la hoja:**
  - La **pistola** es el arma que más paga en rondas altas (19 tiros × 10 por abuelo en ronda 9 = 7.360 la ronda).
    La frena la munición (72 balas ≈ 4 abuelos), pero con munición de pared sería un grifo: no ponerla en pared.
  - El **ukelele** no gasta munición: 7 golpes por abuelo en ronda 9 pagan 5.120 la ronda. Si los logs lo confirman,
    quitar el pago por golpe no letal del ukelele (`PlayerMelee` llama a `EnemyHits.Apply` con `payHit: true`).
  - **Jefe y munición**: en ronda 14 (68.850 de vida) toda la munición de una Thermomix mejorada es el 61 % de su
    vida y la de un Catalejo mejorado el 95 %. Con dos armas mejoradas se llega, justo. Hay que comprobar jugando
    si durante la pelea cae alguna Nevera; si no, soltar una fija al 50 % de vida del jefe (toca `BossAI` y online).

## 3. Lista de trabajo

1. `TestMapSetup.SetupAll()` en su propio RunCommand (Original y prefabs; Isla del Faro ya está reconstruida).
2. Regresión completa: `BasePlayTest`, `IslaZombiePlayTest`, `FeedbackPlayTest`, `CoopPlayTest`, `Phase1PlayTest`,
   `OriginalMapPlayTest`, `MapSelectorPlayTest.RunOriginal`; online: `OnlinePlayTest.BuildClient()` y `RunDirect()`.
   Fallos esperables por los números nuevos:
   - Peleas contra el jefe escriptadas (la vida mínima pasa de 8.000 a 30.000): pueden agotar el tiempo.
   - Cualquier check que mate a golpes de ukelele por encima de la ronda 1 (ahora son 2 golpes en ronda 2–3).
   - Checks que cuenten golpes hasta caer el jugador (35 de daño en vez de 25).
   - `Phase1PlayTest`: el pago 2x1 se calcula con `Economy`, debería seguir bien.
3. Test nuevo (o bloque en `FeedbackPlayTest`): un impacto no letal de Thermomix paga 5 y uno de pistola 10, también
   cuando dispara el cliente online (el host lee el arma en mano del proxy); comprar La Chancla y La Thermomix en la
   pared y su munición; jefe en ronda 1 con 30.000 de vida; 3 golpes de un jubilado tumban al jugador.
4. **Bot de 10 rondas** con cada arma (método del plan, paso 2) y comparar el run log con `balance_sheet.py`.
5. Dos partidas reales del usuario; ajustar un grupo de números cada vez (ingreso → precios → jefe).
6. Docs y commit cuando el usuario lo pida.

## 3b. Pruebas más rápidas (decidido por Fable, 2026-11-15; lo implementa Sonnet)

**Hecho por Sonnet (2026-11-15):** regresión completa en verde (Base 27, Feedback 26, IslaZombie 13, Phase1 21, Coop 39,
Original 3, selector OK, Online 69/0; `IslaZombiePlayTest` necesitó `GodMode` en el paso 0 porque el jugador quieto moría con
35 de daño). Puntos 1 y 2 hechos (`QuickChecks` 31 OK, `SmokePlayTest` 15 OK en 11 s). Punto 4: `TestClock` creado y aplicado
solo a la espera de la persecución de `IslaZombiePlayTest` (20 s en total, 13 OK); el resto de tramos de relleno (Coop) sin
tocar. Pendiente: puntos 3-5 de la lista de trabajo de la sección 3 (test de pago del cliente online, bot de 10 rondas por
arma, partidas reales).

Orden: **primero terminar esta regresión tal cual**, luego montar lo siguiente.

1. **`QuickChecks`** (`Scripts/Editor/QuickChecks.cs`, Tools > Island > Quick Checks, EditorPrefs `QuickChecks.Log/.Status`):
   comprobaciones de lógica pura **sin Play Mode**, menos de 1 s. No usar Unity Test Framework: el proyecto no tiene
   asmdef y un ensamblado de tests no puede referenciar `Assembly-CSharp`. Contenido inicial:
   `EconomyData.HitPointsFor` (Thermomix 5, pistola 10), `RoundManager.BaseHealth` (r1 150, r9 950, r10 1045),
   vida del jefe (mínimo 30.000 en ronda 1; 45 × en ronda 14), cabeza > cuerpo, `wallAmmoCost` = mitad de `wallCost`,
   3 golpes sin Piña / 5 con Piña a partir de los `.asset`, y que toda arma de la caja tenga modelo y `boxWeight` > 0.
   Regla: todo número nuevo de `Assets/Data` con una regla de diseño detrás lleva su línea aquí.
2. **`SmokePlayTest`** (una sola sesión de Play en Isla del Faro, objetivo ≤ 90 s): ronda 1 aparece y persigue; un
   impacto y una baja pagan lo que dice `Economy`; puerta 1 + carta; compra de pared; dar la luz y comprar una
   bebida; `BoatMission.DebugComplete` → muelle gratis → jefe con la vida esperada → matarlo por daño directo →
   extracción → victoria; 0 errores en consola. **Es lo que se pasa entre cambio y cambio.**
3. **Regresión completa** (todos los play tests + online): solo antes de un commit o al cerrar una fase.
   Entre medias: `QuickChecks` + `SmokePlayTest` + el test de la zona tocada.
4. **`Time.timeScale` ×3** solo en tramos de relleno (paseos, esperar a que acabe una ronda, abrir puertas en
   serie), con un helper común (`TestClock.Fast()` / `Normal()`) que restaura 1 al terminar o fallar. **Nunca** en
   checks que miden tiempos o física fina: segundos a la vista de un spawn, revive, desangrado, recarga, embestida,
   colisiones / paseos de `BasePlayTest` y `OriginalMapPlayTest`, y todo `OnlinePlayTest` (el cliente va a ×1).
   Empezar por `IslaZombiePlayTest` y los tramos de espera de `CoopPlayTest`. Un fallo a ×3 se repite a ×1 antes
   de darlo por fallo.
5. **Descartado**: partir de estados guardados (caro de mantener; `DebugOpen` / `DebugComplete` ya lo cubren).

## 4. Sin hacer de la Fase 2

- Armas de pared en el mapa Original (no tiene muros de rejilla; hay que elegir sitios a mano).
- «Pistola mejorada» de pared a 750: no existe ese arma; es contenido (Fase 5).
- `missionDefenseRounds` sigue en 1 (probar 2), probabilidades de `PowerUps.asset`, `downPenalty` y la curva de
  2–4 jugadores: sin tocar, necesitan logs.
- Comprobar las cartas extremas (–20 % conchas, +50 % caja).
