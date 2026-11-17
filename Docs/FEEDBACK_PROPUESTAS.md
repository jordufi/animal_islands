# Feedback y propuestas del usuario

Registro de lo que el usuario propone o decide en la sesión de feedback. Cada entrada: fecha, mapa o sistema, la
propuesta o la respuesta en sus palabras, dudas abiertas y estado. Lo que aquí pone "decidido" no se vuelve a preguntar.

---

## 2026-11-17 — Respuestas a las preguntas abiertas de `FEEDBACK1_PLAN.md`

| # | Pregunta | Respuesta del usuario | Qué cambia en el plan | Estado |
|---|---|---|---|---|
| 1 | Gradas del volley: ¿en el Faro normal, en el de alturas o en los dos? | "En de alturas" | El arreglo de `IZ_Grada` ya cubre los dos mapas. Al probar la fase 1, la captura de las gradas se hace en **Isla del Faro (alturas)** | Decidido, pendiente de probar |
| 2 | ¿La puerta del muelle / de la misión cuenta para "una puerta por ronda"? | "Sí cuentan" | En la fase 4, `doorsPerRound` cuenta **todas** las puertas que se abren pagando o con la misión (incluida la del muelle / la última de la misión). Las que se abren solas (el Portón del encierro) siguen sin contar | Decidido, pendiente |
| 3 | Góndola de Venecia: ¿al azar solo entre zonas abiertas? | "Sí, me vale" | Fase 6: góndola a una parada al azar **de una zona abierta**, una por ronda y por jugador, y no todas las paradas tienen góndola cada ronda | Decidido, pendiente |
| 4 | Tutorial: ¿solo en el Faro? ¿Se quita el Original del selector? | "El mapa original déjalo. El tutorial que sea sencillo y sólo en el mapa del faro" | Fase 7: un tutorial **sencillo** solo en "Isla del Faro (tutorial)". El mapa **Original se queda** en el selector | Decidido, pendiente |

**Consecuencia de la respuesta 2:** si el barco o el vaporetto llega en una ronda en la que el equipo ya ha abierto una
puerta, la puerta del muelle tendrá que esperar a la ronda siguiente. Es coherente con "solo una puerta por ronda".
Si no gusta en la primera partida, se puede excluir.

## Orden de trabajo acordado (2026-11-17)
1. Cerrar la fase 1: probarla y hacer un commit **solo con sus archivos** (no los del hotel).
2. Fase 3: release v0.1 en GitHub.
3. Fase 2 convertida en **una comprobación del setup para todos los mapas** (que el hotel y el crucero ya nazcan sin
   sitios donde el jugador sube y los abuelos no).
4. Fase 4, de más barato a más caro: una puerta por ronda + Julio motiva → gafas / bastón → brújula → carrusel →
   granada la última, en el mismo cambio de protocolo que el hotel y el crucero (protocolo 6).
5. Más adelante: fase 5 (armas con estrellas, sesión de diseño antes), 6 (Venecia), 7 (tutorial del Faro), 8 (presets gráficos).

Estado (2026-11-17, más tarde): el usuario paró la sesión principal y pidió hacerlo en esta. **Fases 1 y 2 hechas y
probadas** (detalle en `FEEDBACK1_PLAN.md`); la fase 4 está en marcha; la release (fase 3) espera a que el usuario dé
el visto bueno para publicar.
