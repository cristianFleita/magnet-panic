# Player Movement

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Fluidez de combate — el movimiento es la base de todo posicionamiento táctico para Pull, Strike y Repel

## Overview

El Player Movement es el sistema Core que traduce el intent `MoveAxis` del `input-system` en desplazamiento físico del personaje dentro de la arena. Controla velocidad, rotación suave hacia la dirección de movimiento, gravedad, sprint, y las penalizaciones que otros sistemas aplican sobre la movilidad (carga magnética, lock de combate, stun).

El jugador interactúa con este sistema constantemente — es lo primero que toca y lo último que suelta. Cada frame de gameplay pasa por aquí. Si el movimiento se siente pesado, sluggish o impreciso, el juego entero se siente mal. Si se siente fluido y responsivo, todo lo demás (combate, magnetismo) se beneficia.

Hoy está implementado como `ArkhamPlayerMotor` — un `MonoBehaviour` con `CharacterController` que lee `OnMove(InputValue)` vía Send Messages. El GDD formaliza la interfaz, centraliza el consumo de input desde `GameInputProvider`, y documenta los contratos con los sistemas que modifican la velocidad del jugador.

## Player Fantasy

**"Soy rápido y estoy en control."**

El movimiento no es la fantasía principal del juego (esa es el magnetismo), pero es el **habilitador** de todas las demás fantasías. El jugador necesita sentir que puede:
1. **Posicionarse tácticamente** — ir exactamente donde quiere para alinear un repel contra la pared.
2. **Escapar del peligro** — salir de un cerco de enemigos con un movimiento fluido.
3. **Nunca pelear contra los controles** — la dirección del movimiento es siempre relativa a la cámara, intuitiva, sin dead zones extrañas.

Referencia directa: **Hades** (movimiento 8-dir fluido, rotación instantánea, dash como extensión del movimiento base), **Devil May Cry 5** (peso justo entre responsividad y feedback visual). La diferencia con Hades es que Magnet Panic no tiene dash — el counter (espacio) cumple la función de escape defensivo, y la velocidad base es lo suficientemente alta para esquivar sin habilidad dedicada.

## Detailed Design

### Core Rules

#### Regla 1 — Movimiento en 8 direcciones relativo a cámara

El player se mueve en las 8 direcciones cardinales + diagonales. La dirección es **relativa a la cámara**, no al mundo. Esto significa que "arriba" (W) siempre va "lejos de la cámara", sin importar la orientación del player.

```csharp
// Cálculo de dirección mundo desde input
Vector3 forward = camera.transform.forward;
Vector3 right = camera.transform.right;
forward.y = 0f; right.y = 0f;
forward.Normalize(); right.Normalize();
worldMoveDirection = forward * moveAxis.y + right * moveAxis.x;
if (worldMoveDirection.sqrMagnitude > 1f) worldMoveDirection.Normalize();
```

Este cálculo ya existe en `ArkhamPlayerMotor.UpdateWorldMoveDirection()` y se preserva tal cual.

#### Regla 2 — Rotación suave hacia dirección de movimiento

El player rota suavemente hacia la dirección en la que se mueve. No hay snap instantáneo — la rotación usa interpolación exponencial para sentirse orgánica sin ser laggy.

```csharp
Quaternion targetRotation = Quaternion.LookRotation(worldMoveDirection);
transform.rotation = Quaternion.Slerp(
    transform.rotation,
    targetRotation,
    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
```

`rotationSharpness = 14` produce un giro que se siente inmediato pero tiene un micro-ease perceptible que da sensación de peso.

#### Regla 3 — Velocidad base con multiplicadores

La velocidad efectiva del player se calcula como:

```
effectiveSpeed = baseSpeed × acceleration × sprintMultiplier × externalModifiers
```

Donde:
- `baseSpeed` = 5 m/s (constante, solo cambiable en Inspector)
- `acceleration` = 0.0 – 1.0 (modificado externamente por `magnetism-system`)
- `sprintMultiplier` = 1.25 cuando sprint está held (1.0 si no)
- `externalModifiers` = multiplicadores apilables de otros sistemas (upgrades, debuffs)

#### Regla 4 — Gravedad propia

El player usa `CharacterController.Move()`, no `Rigidbody`. La gravedad se aplica manualmente:

```csharp
if (controller.isGrounded && verticalVelocity < 0f)
    verticalVelocity = groundedStickForce; // -2, pega al suelo

verticalVelocity += gravity * Time.deltaTime; // gravity = -25
controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
```

`groundedStickForce = -2` mantiene al player pegado al suelo en pendientes leves y evita el "bouncing" del CharacterController al bajar rampas.

**No hay salto.** El espacio está reservado para Counter. El player siempre está en el suelo.

#### Regla 5 — Movement lock

Otros sistemas pueden bloquear el movimiento del player temporalmente:

```csharp
public void SetMovementLocked(bool locked, bool clearInput = false)
```

Cuando `locked = true`:
- `MoveAxis` se ignora — el player no se desplaza horizontalmente.
- La gravedad sigue activa (el player no flota si estaba cayendo).
- El animator recibe `InputMagnitude = 0` (idle animation).
- Si `clearInput = true`, el `moveAxis` interno se resetea a zero.

**Callers de lock:**

| Sistema | Cuándo lockea | Duración |
|---|---|---|
| `combat-system` | Durante Attack Routine (lunge + hit + cooldown) | ~0.76s |
| `combat-system` | Durante Counter Routine (dodge + attack) | ~0.81s |
| `combat-system` | Durante Damage Routine (hit reaction) | ~0.42s |
| `combat-system` | Al morir | Permanente |
| `magnetism-system` | NO lockea movimiento (el player puede moverse mientras pull/repel) | — |
| `overload-system` | Durante Overload explosion (post-MVP) | ~0.5s |
| `meta-flow-system` | Durante upgrade screen, pausa, cutscenes | Variable |

#### Regla 6 — Penalización por carga magnética

El `magnetism-system` modifica `motor.Acceleration` en función de la carga actual:

```csharp
float ratio = currentCharge / maxCapacity;
motor.Acceleration = Mathf.Max(0.25f, 1f - ratio * chargePenaltyAtFull);
```

Con `chargePenaltyAtFull = 0.2` y carga completa:
- `Acceleration = 0.8` → velocidad efectiva baja de 5 a 4 m/s.
- Nunca baja de `0.25 × 5 = 1.25 m/s` (floor de seguridad).

Esto crea una decisión táctica: cargar más chatarra = más daño de repel pero menos movilidad.

#### Regla 7 — Animator feedback

El motor alimenta el Animator con un float `InputMagnitude` (0-1) que controla la transición idle↔walk↔run. El valor se suaviza con `SetFloat(..., 0.1f, Time.deltaTime)` para evitar snaps de animación.

```csharp
float inputMagnitude = Mathf.Clamp01(moveAxis.sqrMagnitude);
float animatedMagnitude = movementLocked ? 0f : inputMagnitude * acceleration;
animator.SetFloat(InputMagnitudeHash, animatedMagnitude, 0.1f, Time.deltaTime);
```

#### Regla 8 — External displacement

El motor expone un método público para que otros sistemas muevan al player sin pasar por el input:

```csharp
public void MoveController(Vector3 displacement)
{
    controller.Move(displacement);
}
```

Usado por:
- `combat-system` para el lunge hacia el target durante Attack Routine.
- Potencialmente por `overload-system` para knockback de la explosión.

Esto NO modifica rotación ni acceleration — solo desplaza.

### States and Transitions

El motor no tiene estados explícitos — el movement lock es un flag binario controlado externamente. Pero conceptualmente:

| Estado | `movementLocked` | `moveAxis` | Movimiento | Rotación | Animator |
|---|---|---|---|---|---|
| **Free** | `false` | from input | activo | activa | reflects `InputMagnitude` |
| **Locked** | `true` | ignored (o cleared) | suprimido | congelada | `InputMagnitude = 0` |
| **Dead** | `true` (permanente) | cleared | suprimido | congelada | death animation |

```
        SetMovementLocked(true)          Die()
  Free ─────────────────────────▶ Locked ────▶ Dead
    ▲                                │
    └────────────────────────────────┘
        SetMovementLocked(false)
```

No hay transición Dead → Free. La muerte es permanente dentro de la run.

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `input-system` | **upstream** | `MoveAxis` (Vector2), `SprintPressed` (bool) | Lee `GameInputProvider.MoveAxis` cada frame |
| `arena-system` | **upstream** | Paredes contienen al player vía colliders | Implicit (CharacterController colisiona con ArenaWall) |
| `combat-system` | **downstream** | `SetMovementLocked()`, `MoveController()`, `WorldMoveDirection` | Llama motor API directamente |
| `magnetism-system` | **downstream** | `Acceleration` setter (penalización por carga) | Escribe `motor.Acceleration` cada frame |
| `camera-system` | **downstream** | `transform.position` como follow target | La cámara sigue `player.transform` |
| `enemy-system` | **downstream** | `transform.position` como referencia para aim/approach | Enemigos leen posición del player |
| `scoring-xp-system` | **downstream** | Distancia recorrida (para misión "run X meters") | Lee `transform.position` delta (indirecto) |
| `presentation-system` | **downstream** | Velocidad actual para VFX de movimiento (dust trails, etc.) | Lee `MoveAxis.magnitude × speed` |
| `upgrade-system` | **downstream** | Potencialmente modifica `baseSpeed` o agrega dash | Setter de `baseSpeed` o nuevo ability |

**Ownership:**
- `player-movement` **owns** la posición, rotación y velocidad del player.
- `player-movement` **owns** la interfaz `SetMovementLocked` y `MoveController`.
- `player-movement` **NO owns** cuándo lockear (eso es del caller).
- `player-movement` **NO owns** la penalización de carga (eso es del `magnetism-system`, que solo escribe `Acceleration`).

## Formulas

### Velocidad efectiva

```
effectiveSpeed = baseSpeed × acceleration × sprintMult × Π(externalMods)
```

| Variable | Tipo | Rango | Default | Source |
|---|---|---|---|---|
| `baseSpeed` | `float` (m/s) | 3 – 8 | 5 | Inspector |
| `acceleration` | `float` | 0.25 – 1.0 | 1.0 | `magnetism-system` setter |
| `sprintMult` | `float` | 1.0 – 1.5 | 1.25 | input sprint held |
| `externalMods` | `float[]` | 0.5 – 2.0 each | 1.0 | `upgrade-system` (post-MVP) |

**Tabla de velocidades resultantes:**

| Carga | Sprint | Velocity | m/s | Sensación |
|---|---|---|---|---|
| 0% | No | 5.0 × 1.0 × 1.0 | 5.0 | Ágil, fluido |
| 0% | Sí | 5.0 × 1.0 × 1.25 | 6.25 | Rápido |
| 50% | No | 5.0 × 0.9 × 1.0 | 4.5 | Levemente pesado |
| 100% | No | 5.0 × 0.8 × 1.0 | 4.0 | Pesado, intencional |
| 100% | Sí | 5.0 × 0.8 × 1.25 | 5.0 | Compensado, empuja contra el peso |

### Rotación exponencial

```
t = 1 - e^(-rotationSharpness × dt)
rotation = Slerp(current, target, t)
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `rotationSharpness` | `float` | 5 – 30 | 14 |

Con `rotationSharpness = 14` y 60 FPS: el player alcanza 99% de la rotación target en ~0.33s. A 30 FPS (WebGL): ~0.35s. Diferencia imperceptible gracias a la interpolación frame-independent.

### Gravedad

```
verticalVelocity += gravity × dt
displacement.y = verticalVelocity × dt
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `gravity` | `float` (m/s²) | -10 – -40 | -25 |
| `groundedStickForce` | `float` (m/s) | -1 – -5 | -2 |

`gravity = -25` es ~2.5× la gravedad real. Esto hace que el player "se pegue" al suelo rápido si alguna vez pierde contacto (ej: borde de la arena, displacement de combat lunge).

## Edge Cases

### E1 — Input durante lock

**Caso:** el jugador mueve WASD mientras el movimiento está lockeado (durante ataque).
**Resolución:** `moveAxis` se sigue actualizando desde el input (para que `WorldMoveDirection` refleje la intención), pero el desplazamiento horizontal se suprime. Esto permite que el `combat-system` use `WorldMoveDirection` para elegir el siguiente target en la dirección deseada, incluso durante un combo.

### E2 — Sprint + carga máxima

**Caso:** el jugador hace sprint con carga magnética al 100%.
**Resolución:** `5.0 × 0.8 × 1.25 = 5.0 m/s`. Sprint compensa la penalización de carga, devolviendo la velocidad base. Esto es intencional — da una herramienta al jugador para mitigar la penalización a costa de una mano ocupada en Shift.

### E3 — MoveController durante lock

**Caso:** `combat-system` llama `MoveController(displacement)` mientras movement está locked (es el uso normal — el lunge ocurre durante lock).
**Resolución:** `MoveController` siempre funciona, lock o no lock. El lock solo suprime el input del jugador, no los desplazamientos programáticos. Esto es crucial para que el lunge hacia el target funcione correctamente.

### E4 — Diagonal normalización

**Caso:** WASD en diagonal (W+D) produce `moveAxis = (1, 1)` con magnitud √2.
**Resolución:** `UpdateWorldMoveDirection()` ya normaliza si `sqrMagnitude > 1f`. La velocidad diagonal es idéntica a la cardinal. Sin esto, el jugador se movería ~41% más rápido en diagonal.

### E5 — Camera forward = zero (player directamente debajo de cámara)

**Caso:** si la cámara mira directamente hacia abajo, `camera.transform.forward` proyectado al plano XZ es zero.
**Resolución:** improbable con la cámara isométrica actual (offset Y=10, Z=-8), pero si ocurre: `forward = Vector3.forward`, `right = Vector3.right` (fallback). El código existente ya tiene este check.

### E6 — Acceleration setter abuse

**Caso:** múltiples sistemas intentan setear `motor.Acceleration` en el mismo frame.
**Resolución:** hoy solo `magnetism-system` escribe `Acceleration`. Si en el futuro otros sistemas necesitan modificar velocidad, se debe cambiar a un modelo multiplicativo (`AddSpeedModifier(key, value)` / `RemoveSpeedModifier(key)`) en vez de un setter directo. Para la jam, el setter simple es suficiente.

### E7 — Framerate-dependent speed

**Caso:** a 30 FPS vs 60 FPS, ¿el player se mueve a la misma velocidad?
**Resolución:** sí. `controller.Move(direction * speed * Time.deltaTime)` es frame-independent. La rotación usa `Mathf.Exp(-sharpness * deltaTime)` que también es frame-independent. El movimiento es determinista respecto a `deltaTime`.

### E8 — Player empujado fuera de la arena

**Caso:** un `MoveController(displacement)` excesivo empuja al player a través de una pared (ej: bug en combat lunge).
**Resolución:** `CharacterController.Move()` respeta colisiones — no puede atravesar colliders. Si la displacement es muy grande, el CC se detendrá contra la pared. Adicionalmente, `arena-system` tiene un safety check `ClampToArena()` como backup.

## Dependencies

### Upstream (este sistema depende de)

| Sistema | Tipo | Qué consume |
|---|---|---|
| `input-system` | **Hard** | `MoveAxis` (Vector2), `SprintPressed` (bool) — sin input no hay movimiento |
| `arena-system` | **Hard** | Colliders de paredes que contienen al player (implícito vía CharacterController) |

### Downstream (dependen de este sistema)

| Sistema | Tipo | Qué consume |
|---|---|---|
| `camera-system` | **Hard** | `transform.position` como follow target |
| `combat-system` | **Hard** | `SetMovementLocked()`, `MoveController()`, `WorldMoveDirection` |
| `magnetism-system` | **Hard** | `Acceleration` setter, `WorldMoveDirection` (para aim fallback) |
| `enemy-system` | **Soft** | `transform.position` como referencia para approach/retreat |
| `scoring-xp-system` | **Soft** | Posición delta para misiones de distancia |
| `presentation-system` | **Soft** | Velocidad para VFX de movimiento |
| `upgrade-system` | **Soft** | Setter de `baseSpeed` para upgrade de velocidad |

### Nota sobre CharacterController

El `player-movement` depende técnicamente de `CharacterController` (Unity built-in). Esto trae restricciones:
- El player NO tiene `Rigidbody` — no responde a fuerzas físicas.
- Las colisiones son resueltas por el CC, no por Physics.
- El CC tiene `skinWidth` que puede causar micro-gaps con paredes. Esto es aceptable.

## Tuning Knobs

| Knob | Default | Rango seguro | Efecto si demasiado bajo | Efecto si demasiado alto |
|---|---|---|---|---|
| `baseSpeed` | 5 m/s | 3 – 8 | Player se siente lento, no puede esquivar | Player se siente resbaloso, pierde precisión posicional |
| `sprintMultiplier` | 1.25 | 1.0 – 1.5 | Sprint no se siente diferente, nadie lo usa | Sprint demasiado rápido, trivializa esquivar |
| `rotationSharpness` | 14 | 5 – 30 | Giro pesado, "tanquecito", frustrante | Giro instantáneo, pierde sensación de peso |
| `gravity` | -25 | -10 – -40 | Player flota al salir de bordes, se siente flotante | Player se pega al suelo agresivamente, glitchy en pendientes |
| `groundedStickForce` | -2 | -1 – -5 | Bouncing al bajar rampas | Snapping agresivo, visual jitter |
| `chargePenaltyAtFull` | 0.2 | 0.0 – 0.5 | Carga no afecta velocidad, no hay decisión táctica | Carga paraliza al jugador, frustración |
| `accelerationFloor` | 0.25 | 0.1 – 0.5 | Player casi inmóvil con carga (frustración) | Penalización de carga es imperceptible |

### Interacciones entre knobs

- `baseSpeed` × `chargePenaltyAtFull`: el piso efectivo de velocidad es `baseSpeed × accelerationFloor`. Con defaults: `5 × 0.25 = 1.25 m/s`. Esto debe ser más rápido que la velocidad de approach del enemigo más rápido (~3 m/s → PROBLEMA: no lo es con defaults). **Decisión**: el piso es para la animación de movimiento pesado, no para escapar. Escapar con carga completa requiere sprint (5.0 m/s) o repeler la carga primero.
- `rotationSharpness` × `baseSpeed`: si rotación es lenta y velocidad alta, el player "derrapa" visualmente. Con defaults (14 + 5), el player completa el giro antes de avanzar visiblemente. Ratio seguro: `rotationSharpness > baseSpeed × 2`.
- `sprintMultiplier` × `baseSpeed`: velocidad máxima = `baseSpeed × sprintMultiplier`. No debe exceder la velocidad de la cámara de seguimiento (`followSharpness` del `camera-system` = 8). Con defaults: `6.25 m/s << 8 follow sharpness`. OK.

## Visual/Audio Requirements

### Visual

- **Dust trail**: partículas sutiles de polvo al moverse. Intensidad proporcional a `InputMagnitude`. Desaparecen en idle.
- **Sprint trail**: efecto visual adicional durante sprint (speed lines sutiles o afterimage leve). Comunica "estoy corriendo más rápido".
- **Heavy movement**: cuando `acceleration < 0.6`, el personaje se inclina levemente hacia adelante y las partículas de polvo son más pesadas/lentas. Comunica "estoy cargado".
- **Locked visual**: durante movement lock, NO hay feedback visual especial (la animación de ataque/counter/hit es suficiente contexto).

### Audio

- **Footsteps**: SFX de pasos sincronizados con la animación de walk/run. Tempo aumenta con velocidad. Material del suelo = metálico (arena industrial).
- **Sprint whoosh**: SFX sutil de viento al sprintear. Loop con fade in/out.
- **Carga pesada**: cuando `acceleration < 0.6`, footsteps son más pesados (pitch más bajo, más reverb). Comunica peso sin texto.

## UI Requirements

### MVP

- Sin UI directa. El movimiento se comunica enteramente a través de la animación, VFX y SFX.
- La penalización de carga se muestra indirectamente por la barra de capacidad magnética en el HUD (responsabilidad del `hud-system`).

### Post-MVP

- **Speed boost indicator**: icono temporal en el HUD cuando un upgrade de velocidad está activo.
- **Slowdown indicator**: efecto de borde de pantalla cuando la penalización de carga supera el 15%. Vignette leve o tinte visual.

## Acceptance Criteria

### Funcionales

1. **AC-1**: El player se mueve en 8 direcciones relativas a la cámara con WASD. Velocidad diagonal = velocidad cardinal (normalizado).
2. **AC-2**: La rotación es suave (`rotationSharpness = 14`), no instantánea ni laggy.
3. **AC-3**: Sprint (Shift held) aumenta la velocidad un 25%.
4. **AC-4**: `SetMovementLocked(true)` suprime el movimiento horizontal. El player no se desplaza con WASD.
5. **AC-5**: `SetMovementLocked(true)` no suprime `MoveController()` — el lunge de combate sigue funcionando.
6. **AC-6**: `WorldMoveDirection` refleja la intención del jugador incluso durante movement lock (para target scanning).
7. **AC-7**: `Acceleration` setter de `magnetism-system` reduce la velocidad proporcionalmente a la carga. Con carga 100%: velocidad = `baseSpeed × 0.8`.
8. **AC-8**: Sprint + carga 100% = velocidad base (5 m/s). Sprint compensa la penalización.
9. **AC-9**: El Animator recibe `InputMagnitude` suavizado (damping 0.1f) que refleja la velocidad de movimiento.
10. **AC-10**: El player no puede salir de la arena — `CharacterController` colisiona con paredes `ArenaWall`.

### Rendimiento

11. **AC-11**: `Update()` del motor no genera GC allocations (0 alloc per frame).
12. **AC-12**: Movimiento es frame-independent — velocidad idéntica a 30 y 60 FPS.

### Migración

13. **AC-13**: `ArkhamPlayerMotor` lee `MoveAxis` de `GameInputProvider` en lugar de `OnMove(InputValue)` de Send Messages.
14. **AC-14**: `ArkhamPlayerMotor` lee `SprintPressed` de `GameInputProvider` en lugar de `OnSprint(InputValue)`.
15. **AC-15**: Se eliminan los métodos `OnMove(InputValue)` y `OnSprint(InputValue)` del motor.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Dash como ability de upgrade? El movimiento base no tiene dash, pero un upgrade que agregue un dash corto (ej: double-tap dirección o tecla dedicada) sería un power-up de movilidad interesante. ¿Lo diseñamos como extensión del motor o como sistema separado? | cris | Pre-upgrade-system GDD |
| Q2 | ¿Sprint debería existir en el juego final? En un endless run con combate constante, el jugador casi nunca tiene una mano libre para Shift. Si sprint es poco usado, el multiplicador es complexity innecesaria. Alternativa: velocidad base más alta y no hay sprint. | cris | Post-playtest |
| Q3 | ¿Debería haber un slow-down visual al recibir daño (además del movement lock de 0.42s)? Tipo hitstun que reduce velocidad en vez de lockear. Más responsive, menos frustrating, pero más complejo. | cris | Post-playtest |
| Q4 | ¿El modelo multiplicativo de `Acceleration` es suficiente o necesitamos `AddSpeedModifier(key, value)` desde el arranque? Con solo `magnetism-system` escribiendo, el setter directo funciona. Si `upgrade-system` y `powerup-system` también modifican velocidad, necesitamos el modelo aditivo. | cris | Pre-upgrade-system GDD |
| Q5 | ¿El player debería rotar hacia el aim direction (mouse) en vez de hacia la dirección de movimiento? En juegos twin-stick el personaje mira al cursor. En Magnet Panic el aim es para repel, no para el cuerpo — pero podría sentirse más natural mirar al cursor cuando se tiene chatarra en órbita. | cris | Post-playtest |
