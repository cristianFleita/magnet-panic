# Camera System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Legibilidad de combate — la cámara enmarca la acción para que el jugador siempre vea lo que necesita para decidir Pull, Strike o Repel

## Overview

El Camera System controla la cámara principal del juego: posición, rotación, seguimiento del jugador, screen shake y clamp a los límites de la arena. Es un sistema Core que consume la posición del `player-movement` y provee el viewport que todos los demás sistemas usan para renderizar, apuntar (aim raycast) y posicionar UI worldspace (health bars de enemigos).

El jugador no controla la cámara directamente — es automática. Su rol es mantener la acción legible: el player centrado, los enemigos visibles, y los impactos comunicados con shake. Si la cámara funciona bien, el jugador nunca piensa en ella. Si funciona mal, el jugador no puede jugar.

Hoy está implementado como `ArkhamSimpleCameraFollow` — un `MonoBehaviour` en `LateUpdate` con offset fijo (0, 10, -8), interpolación exponencial, look-ahead basado en `forward`, y screen shake por coroutine. El GDD formaliza esta implementación, agrega arena bounds clamping, y documenta los contratos de shake con `combat-system` y `presentation-system`.

## Player Fantasy

**"Veo todo lo que necesito."**

La cámara no tiene fantasía de jugador — tiene fantasía de **director de cine de acción**. Cada plano enmarca la acción para máximo drama sin sacrificar legibilidad. El jugador siempre ve:
1. Su personaje (centro de pantalla).
2. El enemigo más cercano (dentro del viewport).
3. La chatarra en órbita y la dirección del repel (para apuntar).

Referencia directa: **Hades** (cámara isométrica fija por room, nunca se pierde el player), **Diablo III** (follow suave con leash distance), **Devil May Cry 5** (shake proporcional al daño, comunica peso de cada hit). La diferencia es que Magnet Panic no tiene rooms — es una arena única, y la cámara solo necesita seguir al player dentro de ella.

## Detailed Design

### Core Rules

#### Regla 1 — Posición: follow con offset fijo

La cámara sigue la posición del player con un offset constante en espacio mundo:

```csharp
Vector3 desired = target.position + offset + target.forward * lookAhead + shakeOffset;
transform.position = Vector3.Lerp(
    transform.position,
    desired,
    1f - Mathf.Exp(-followSharpness * Time.deltaTime));
```

| Parámetro | Default | Descripción |
|---|---|---|
| `offset` | (0, 10, -8) | Desplazamiento fijo. Y=10 da vista top-down. Z=-8 da ángulo iso |
| `followSharpness` | 8 | Velocidad de follow. 8 = suave pero no laggy |
| `lookAhead` | 0.5 | Anticipo en `target.forward`. 0.5m = sutil, no agresivo |

El ángulo resultante es ~51° respecto al suelo — una vista "three-quarter" clásica para action RPG.

#### Regla 2 — Rotación: LookAt al player

La cámara siempre mira hacia el player, con un offset vertical de 0.8m (centro visual del personaje, no sus pies):

```csharp
Vector3 lookTarget = target.position + Vector3.up * 0.8f;
transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);
```

La rotación NO se interpola — es directa cada frame. Con el follow interpolado y la rotación directa, la cámara tiene un efecto sutil de "tilt" al moverse que se siente natural.

#### Regla 3 — Screen Shake

El screen shake es el principal canal de feedback visual de impacto. Se activa por eventos de `combat-system` y `presentation-system`:

```csharp
public void Shake(float amplitude, float duration)
```

**Implementación:** random offset 3D con damping lineal:

```csharp
float damping = 1f - Mathf.Clamp01(elapsed / duration);
shakeOffset = Random.insideUnitSphere * (amplitude * damping);
shakeOffset.y *= 0.35f; // atenuar eje Y, más horizontal
```

**Callers actuales:**

| Evento | Amplitude | Duration | Source |
|---|---|---|---|
| Player strike hits enemy | 0.11 – 0.36 (distance-based) | 0.14s | `ArkhamCombatController.ApplyHit()` |
| Player counters enemy | 0.15 | 0.16s | `ArkhamCombatController.CounterRoutine()` |
| Player receives damage | 0.20 | 0.20s | `ArkhamCombatController.DamageRoutine()` |
| Player dies | 0.26 | 0.24s | `ArkhamCombatController.Die()` |
| Wall slam (post-arena) | 0.18 | 0.15s | `arena-system` → `OnWallSlam` |
| Magnetic repel impact | 0.12 | 0.12s | `presentation-system` (pending) |

Si un shake nuevo llega mientras otro está activo, el anterior se cancela (StopCoroutine) y el nuevo toma el control. No se acumulan.

#### Regla 4 — Arena Bounds Clamping

La cámara no debe mostrar más allá de las paredes de la arena. La posición de la cámara se clampa a los bounds de la arena más un padding:

```csharp
void ClampToArenaBounds()
{
    if (arenaBounds == null) return;

    Bounds playable = arenaSystem.PlayableBounds;
    Vector3 pos = transform.position;

    // Clamp horizontal para que el viewport no exceda la arena
    float halfViewWidth = CalculateViewHalfWidth();
    float halfViewDepth = CalculateViewHalfDepth();

    pos.x = Mathf.Clamp(pos.x,
        playable.min.x + halfViewWidth,
        playable.max.x - halfViewWidth);
    pos.z = Mathf.Clamp(pos.z,
        playable.min.z + halfViewDepth - offset.z,
        playable.max.z - halfViewDepth - offset.z);

    transform.position = pos;
}
```

Esto impide ver el "vacío" más allá de las paredes. Si la arena es más chica que el viewport (no debería pasar con 32×32), la cámara queda centrada.

#### Regla 5 — Ejecución en LateUpdate

La cámara se ejecuta en `LateUpdate`, después de que `player-movement` actualice la posición en `Update`. Esto garantiza que la cámara siempre sigue la posición más reciente del player, sin un frame de lag.

#### Regla 6 — Referencia para aim

La cámara es referenciada por `input-system` (`GameInputProvider.UpdateAim()`) y `player-movement` (`UpdateWorldMoveDirection()`) para convertir input de pantalla a mundo. La cámara no hace este cálculo — solo provee su `Transform` para que otros lo usen.

**Nota de rendimiento**: hoy `ArkhamPlayerMotor` y `MagnetismController` ambos buscan `Camera.main` como fallback. Con el GDD de `input-system`, el aim se centraliza en `GameInputProvider` que cachea la referencia de cámara. Los scripts ya no necesitan buscarla.

#### Regla 7 — Billboard support

Los elementos worldspace (health bars de enemigos via `WorldSpaceHealthBar`) copian la rotación de la cámara para el efecto billboard:

```csharp
canvas.transform.rotation = camera.transform.rotation;
```

La cámara no es responsable de esto — solo provee su rotación. El billboard es responsabilidad del componente de UI.

### States and Transitions

La cámara tiene un estado principal y efectos transitorios:

| Estado | Descripción |
|---|---|
| **Following** | Estado normal. Sigue al player con interpolación |
| **Shaking** | Following + shake offset activo. Se superpone |

```
                Shake(amp, dur)
  Following ─────────────────────▶ Shaking
      ▲                                │
      └──────── duration expires ──────┘
```

El shake NO es un estado separado — es un modifier que se suma a la posición de follow. La cámara siempre está "following", a veces con shake adicional.

**Estados post-MVP (fuera de jam):**

| Estado | Descripción |
|---|---|
| **Zooming** | Zoom-in/out temporario para eventos (boss intro, death) |
| **Transitioning** | Transición suave a nueva posición/offset (cambio de arena) |
| **Cinematic** | Cámara controlada externamente para cutscenes |

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `player-movement` | **upstream** | `transform.position` del player como target de follow | Campo `target` (Transform reference) |
| `arena-system` | **upstream** | `PlayableBounds` para clamp | Lee `ArenaSystem.PlayableBounds` |
| `input-system` | **downstream** | `camera.transform` para aim resolution (screen → world ray) | `GameInputProvider` cachea referencia a Camera |
| `player-movement` | **downstream** | `camera.transform.forward/right` para dirección relativa a cámara | `ArkhamPlayerMotor` cachea referencia a Camera |
| `combat-system` | **downstream** caller | `Shake(amplitude, duration)` en hits/counter/damage/death | `ArkhamCombatController` llama `cameraRig.Shake()` |
| `presentation-system` | **downstream** caller | `Shake()` para impactos de chatarra, wall slams | Llama `cameraRig.Shake()` |
| `hud-system` | **downstream** | Camera viewport para posicionar UI overlay | UI Toolkit usa Screen coordinates |
| `enemy-system` (UI) | **downstream** | Camera rotation para billboard de health bars | `WorldSpaceHealthBar` copia `camera.rotation` |
| `magnetism-system` | **downstream** | Camera para aim raycast (legacy, migrar a input-system) | Referencia directa a Camera (a eliminar) |

**Ownership:**
- `camera-system` **owns** la posición, rotación y estado de la cámara principal.
- `camera-system` **owns** el screen shake como efecto.
- `camera-system` **NO owns** cuándo hacer shake (eso es del caller).
- `camera-system` **NO owns** el aim resolution (eso es del `input-system`).
- `arena-system` **owns** los bounds; la cámara solo los consulta.

## Formulas

### Follow Interpolation (exponencial frame-independent)

```
t = 1 - e^(-followSharpness × dt)
position = lerp(currentPos, desiredPos, t)
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `followSharpness` | `float` | 3 – 20 | 8 |
| `dt` | `float` | `Time.deltaTime` | — |

**Tabla de tiempos de convergencia:**

| Sharpness | 90% convergence | 99% convergence | Sensación |
|---|---|---|---|
| 5 | 0.46s | 0.92s | Cinemático, lento |
| 8 | 0.29s | 0.58s | **Balanced** — suave sin lag |
| 12 | 0.19s | 0.38s | Responsivo, casi instantáneo |
| 20 | 0.12s | 0.23s | Rígido, se siente pegado |

Con `followSharpness = 8`, la cámara alcanza el 90% de su posición target en 0.29s. Imperceptible en gameplay normal, visible en cambios de dirección bruscos como un leve "slide" satisfactorio.

### Shake Damping

```
shakeOffset = randomSphere × amplitude × (1 - elapsed/duration)
shakeOffset.y *= yAttenuation
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `amplitude` | `float` (m) | 0.05 – 0.5 | per-caller |
| `duration` | `float` (s) | 0.05 – 0.5 | per-caller |
| `yAttenuation` | `float` | 0.0 – 1.0 | 0.35 |

El damping es lineal (no exponencial). Esto hace que el shake sea brusco al inicio y suave al final — perfecto para impactos. El `yAttenuation = 0.35` reduce el movimiento vertical para que el shake sea primariamente horizontal, que se siente más como "impacto" y menos como "terremoto".

### Look-Ahead Distance

```
lookAheadOffset = target.forward × lookAhead
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `lookAhead` | `float` (m) | 0 – 3 | 0.5 |

Con `lookAhead = 0.5`, la cámara se desplaza medio metro en la dirección que el player mira. Es muy sutil — suficiente para dar un sesgo visual de "hacia dónde voy" sin desorientar al cambiar de dirección rápido.

### View Half-Width (para arena clamping)

```
halfViewWidth = camera.orthographicSize × camera.aspect  // ortho
halfViewWidth = tan(fov/2) × distance × camera.aspect     // perspective
```

Con la cámara perspective actual (offset Y=10, Z=-8, distancia ~12.8m al player):
- FOV 60° → halfViewWidth ≈ 7.4m
- Arena 32m → la cámara puede moverse ~17.2m antes de mostrar el borde. Más que suficiente.

## Edge Cases

### E1 — Player se mueve rápido, cámara se atrasa

**Caso:** con sprint (6.25 m/s) + cambio de dirección brusco, la cámara se atrasa visiblemente.
**Resolución:** con `followSharpness = 8`, el lag máximo en cambio de dirección a 6.25 m/s es ~0.78m (6.25 × 0.125s). A la distancia de cámara (12.8m), esto es ~3.5° de desplazamiento visual — imperceptible. Si se siente laggy, subir `followSharpness` a 10-12.

### E2 — Múltiples shakes simultáneos

**Caso:** un strike hit + un wall slam ocurren en el mismo frame, generando dos `Shake()` calls.
**Resolución:** el segundo shake cancela el primero. NO se acumulan — eso crearía shake descontrolado. El shake más reciente "gana". Esto es el comportamiento actual y es correcto para impactos rápidos.

### E3 — Shake durante transición de muerte

**Caso:** el player muere → shake de muerte (0.26, 0.24s) → death screen se muestra. ¿La cámara sigue shaking durante el fade?
**Resolución:** el shake se completa naturalmente. Si la death screen tiene fade overlay, el shake se ve a través del fade, lo cual es un efecto dramático positivo. No se cancela el shake al morir.

### E4 — Arena más chica que viewport

**Caso:** si alguien configura `arenaSize = 8` (muy chico), el viewport (15m wide) es más grande que la arena.
**Resolución:** la cámara se centra en la arena y no se mueve horizontalmente. El player sigue visible pero el viewport muestra el borde de la arena. Esto es un caso de tunning erróneo — el arena-system GDD establece rango mínimo de 16m, y con FOV 60° el viewport es ~15m → funciona con margen.

### E5 — Target null (player destruido)

**Caso:** el player es destruido (fin de run) pero la cámara sigue viva.
**Resolución:** check `if (target == null) return;` en `LateUpdate()`. La cámara se congela en su última posición. El `meta-flow-system` muestra la death screen encima.

### E6 — Camera.main fallback eliminado

**Caso:** con la migración del `input-system`, varios scripts dejan de buscar `Camera.main` como fallback. Si la referencia de cámara no está seteada, el aim no funciona.
**Resolución:** el `ArkhamCombatSetup` ya setea la cámara en el player setup. `GameInputProvider.Configure(camera)` recibe la referencia. Si es null, `Awake()` cachea `Camera.main` como fallback de último recurso. Un solo fallback centralizado, no N dispersos.

### E7 — Resolución de pantalla variable en WebGL

**Caso:** el canvas de WebGL puede ser resizeado por el usuario. Esto cambia el aspect ratio, que afecta `halfViewWidth` y por ende el arena clamping.
**Resolución:** `CalculateViewHalfWidth()` se recalcula cada frame usando `camera.aspect`, que Unity actualiza automáticamente al cambiar el viewport. No requiere handling especial.

## Dependencies

### Upstream (este sistema depende de)

| Sistema | Tipo | Qué consume |
|---|---|---|
| `player-movement` | **Hard** | `transform.position` del player como target de follow (sin esto la cámara no sabe qué seguir) |
| `arena-system` | **Soft** | `PlayableBounds` para clamping (sin esto la cámara funciona pero puede mostrar fuera de la arena) |

### Downstream (dependen de este sistema)

| Sistema | Tipo | Qué consume |
|---|---|---|
| `input-system` | **Hard** | Camera transform para aim resolution (screen → world). Sin cámara, no hay aim |
| `player-movement` | **Hard** | Camera forward/right para movimiento relativo a cámara |
| `combat-system` | **Soft** | `Shake()` para feedback de impacto (funciona sin shake, solo pierde juiciness) |
| `presentation-system` | **Soft** | `Shake()` para impactos ambientales |
| `enemy-system` (UI) | **Soft** | Camera rotation para billboard (health bars se ven mal sin esto, pero no crashea) |
| `hud-system` | **Soft** | Camera viewport para coordenadas de screen-space UI |

### Nota sobre la dependencia bidireccional player-movement ↔ camera

`player-movement` depende de `camera-system` para dirección relativa, y `camera-system` depende de `player-movement` para la posición de follow. Esto NO es un ciclo — es una relación read-read:
- `camera` lee `player.position` en `LateUpdate` (después de que player se movió).
- `player` lee `camera.forward/right` en `Update` (antes de que la cámara se actualice → usa posición del frame anterior).

El frame de lag es imperceptible y es el pattern estándar de Unity para cámara de tercera persona.

## Tuning Knobs

| Knob | Default | Rango seguro | Efecto si demasiado bajo | Efecto si demasiado alto |
|---|---|---|---|---|
| `offset` | (0, 10, -8) | Y: 5-15, Z: -4 a -12 | Cámara muy baja/cercana, pierde overview | Cámara muy alta/lejos, player es tiny |
| `followSharpness` | 8 | 3 – 20 | Cámara laggy, player se pierde | Cámara rígida, pierde suavidad cinemática |
| `lookAhead` | 0.5 m | 0 – 3 | Sin anticipación, cámara reactiva | Anticipación excesiva, desorientación al girar |
| `shakeYAttenuation` | 0.35 | 0 – 1 | Shake solo horizontal, se siente artificial | Shake con mucho bounce vertical, mareo |
| `lookAtHeightOffset` | 0.8 m | 0.3 – 1.5 | Cámara mira a los pies, perspectiva baja | Cámara mira al aire, player no está centrado |

### Interacciones entre knobs

- `offset.y` × `offset.z` × FOV: estos tres definen el ángulo de visión y la distancia percibida. Cambiar uno requiere rebalancear los otros. La combinación (10, -8, FOV 60°) da un ángulo de ~51° que es sweet spot para iso-action.
- `followSharpness` × `player.baseSpeed`: si sharpness es bajo y speed es alto, la cámara se atrasa. **Regla**: `followSharpness > player.baseSpeed` (actualmente 8 > 5 ✓, pero con sprint 6.25 es justo).
- `lookAhead` × `rotationSharpness` (del player-movement, default 14): si lookAhead es alto y rotación es suave, la cámara oscila al cambiar de dirección. Con defaults (0.5 × 14), la oscilación es imperceptible.

## Visual/Audio Requirements

### Visual

- **Sin visualización propia.** La cámara es invisible — define qué ve el jugador.
- **Post-processing stack**: bloom sutil para efectos magnéticos, vignette leve durante overload (delegado a `presentation-system`, pero necesita la cámara para el render pipeline).
- **Clear flags**: Skybox (configurable). El cielo/fondo de la arena se ve al borde del viewport si el clamping no es perfecto.

### Audio

- **Audio Listener**: la cámara lleva el `AudioListener`. El sonido se escucha desde la perspectiva de la cámara, no del player. Esto es estándar para top-down.
- **Sin SFX propios de la cámara.** Los sonidos de shake son responsabilidad del evento que triggerea el shake (hit SFX del `combat-system`).

## UI Requirements

### MVP

- Sin UI directa. La cámara no tiene HUD elements propios.
- El `AudioListener` component vive en el GameObject de la cámara.

### Post-MVP

- **Zoom control**: slider en settings para ajustar `offset.y/z` a preferencia del jugador (más cerca = más inmersivo, más lejos = más overview). Rango: 80% – 120% del offset default.
- **Shake intensity**: slider en settings (0% – 200%) para jugadores con motion sensitivity. 0% desactiva shake completamente.

## Acceptance Criteria

### Funcionales

1. **AC-1**: La cámara sigue al player con interpolación exponencial (`followSharpness = 8`). No hay snap ni lag visible.
2. **AC-2**: La cámara siempre mira al player (point 0.8m arriba de su posición).
3. **AC-3**: `Shake(amplitude, duration)` produce un shake que comienza fuerte y decae linealmente. El eje Y está atenuado al 35%.
4. **AC-4**: Múltiples `Shake()` calls en rápida sucesión: solo el más reciente está activo.
5. **AC-5**: La posición de la cámara se clampa a los `PlayableBounds` de la arena — el viewport no muestra más allá de las paredes.
6. **AC-6**: El `AudioListener` está en el GameObject de la cámara.
7. **AC-7**: Si `target` es null, la cámara se congela en su última posición sin errores.

### Rendimiento

8. **AC-8**: `LateUpdate()` de la cámara no genera GC allocations (0 alloc per frame).
9. **AC-9**: El shake no causa jitter visible a 30 FPS (el random offset es suave gracias al damping).

### Cross-system

10. **AC-10**: `GameInputProvider` usa esta cámara para aim resolution — un click del mouse se traduce correctamente a una dirección en world space.
11. **AC-11**: `ArkhamPlayerMotor` usa esta cámara para dirección relativa — WASD se traduce correctamente relativo a la vista de la cámara.
12. **AC-12**: Las health bars de enemigos (`WorldSpaceHealthBar`) se orientan correctamente hacia esta cámara (billboard effect).

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿El look-ahead debería seguir el aim direction (mouse) en vez de `target.forward`? Esto mostraría más espacio en la dirección a la que el jugador apunta, mejorando la visibilidad para repel. Riesgo: la cámara se mueve al mover el mouse, puede ser distracting. | cris | Post-playtest |
| Q2 | ¿Zoom-out dinámico cuando hay muchos enemigos? Si hay 10+ enemigos en pantalla, hacer un leve zoom-out (5-10%) para mostrarlos a todos. Riesgo: el player se achica y pierde presencia visual. | cris | Post-playtest |
| Q3 | ¿Hitstop (time freeze de 1-2 frames) además de shake? Hitstop es el complemento clásico del shake para feedback de impacto. Es barato de implementar (`Time.timeScale = 0` por 1 frame). ¿Lo diseñamos como parte de camera-system o de presentation-system? | cris | Pre-presentation-system GDD |
| Q4 | ¿La cámara debería ser ortográfica en vez de perspective? Ortho elimina distorsión de perspectiva y es más clean para pixel art. Pero Magnet Panic usa modelos 3D → perspective es mejor para profundidad. Mantener perspective salvo feedback contrario. | cris | Pre-art-direction |
| Q5 | ¿Necesitamos un "death camera" (zoom-in lento al player muerto)? Agregaría dramatismo al momento de muerte. Es un estado adicional (Zooming) que no está en el MVP pero es bajo costo. | cris | Post-playtest |
