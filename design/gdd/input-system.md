# Input System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Pull / Strike / Repel (el input es la interfaz directa del jugador con los tres verbos)

## Overview

El Input System es la capa Foundation que traduce las señales de hardware (teclado, mouse, gamepad) en **intents abstractos de gameplay**: `Move`, `Aim`, `PullToggle`, `Strike`, `Counter` y `UpgradeChoice`. Funciona como un intermediario de solo lectura entre los dispositivos de entrada y los sistemas que consumen acciones del jugador (`player-movement`, `combat-system`, `magnetism-system`, `upgrade-system`).

El jugador no interactúa con este sistema conscientemente — lo siente a través de la responsividad, la claridad de los controles y la ausencia de fricción. El sistema existe para aislar a los consumidores de las diferencias de plataforma (WebGL teclado+mouse vs. gamepad futuro), habilitar rebinding en runtime, y garantizar un **input buffering** corto que haga el combate se sienta responsivo incluso a 30 FPS en WebGL.

Sin este sistema, cada script seguiría leyendo acciones de Unity directamente con nombres inconsistentes (hoy `OnJump` = counter, `OnAttack` = strike), haciendo imposible cambiar bindings o agregar plataformas sin tocar código de gameplay.

## Player Fantasy

**"Mis manos desaparecen."**

El input system cumple su fantasía cuando el jugador deja de pensar en qué tecla presionar. La acción mental es "atraer eso" y el dedo ya se movió. El sistema es **infraestructura invisible** — el jugador nunca debería notarlo, solo sentir que el juego responde exactamente como espera.

La referencia directa es **Hades** (Supergiant): tres acciones de combate (attack, special, cast) mapeadas a inputs claros, sin ambigüedad, con buffering sutil que perdona el timing imperfecto. El jugador siente fluidez, no mecánica.

En Magnet Panic, esa fluidez se traduce en: click izquierdo = atraer/repeler (toggle natural), click derecho = golpear, espacio = counter. Tres dedos, tres verbos, cero confusión. Si el jugador tiene que pensar "¿cuál era la tecla?", el sistema falló.

## Detailed Design

### Core Rules

#### Regla 1 — Intents, no hardware

El sistema expone **intents de gameplay**, no acciones de hardware. Los consumidores nunca leen `Mouse.current` ni `Keyboard.current` directamente. Toda lectura pasa por el wrapper.

Los intents del MVP son:

| Intent | Tipo | Descripción |
|---|---|---|
| `Move` | `Vector2` continuo | Dirección de movimiento (8 direcciones WASD o stick) |
| `Aim` | `Vector2` continuo | Posición del cursor en pantalla (mouse) o dirección del stick derecho |
| `PullToggle` | `Button` press | Activa Pull si está inactivo, o dispara Repel si está activo |
| `Strike` | `Button` press | Ataque cuerpo a cuerpo (marca magnética) |
| `Counter` | `Button` press | Counter / pulso defensivo |
| `UpgradeChoice` | `Button` press × 3 | Seleccionar upgrade 1, 2 o 3 durante level-up |
| `Pause` | `Button` press | Toggle pausa |

#### Regla 2 — Un solo componente centralizador

Un `MonoBehaviour` llamado `GameInputProvider` vive en el player GameObject junto al `PlayerInput` de Unity. Es el único script con referencia al asset `.inputactions`. Todos los demás sistemas leen intents desde este provider, nunca del Input System de Unity directamente.

```
[PlayerInput] ──Send Messages──▶ [GameInputProvider] ──propiedades públicas──▶ consumidores
```

#### Regla 3 — Propiedades de solo lectura

`GameInputProvider` expone cada intent como propiedad pública de solo lectura:

```csharp
// Continuos (leídos cada frame)
Vector2 MoveAxis { get; }
Vector2 AimScreenPosition { get; }    // mouse position en pixels
Vector3 AimWorldDirection { get; }     // dirección en world-space (calculada con camera raycast)

// Discrete (leídos vía eventos o polling de WasPressed)
bool PullTogglePressed { get; }
bool StrikePressed { get; }
bool CounterPressed { get; }
bool Upgrade1Pressed { get; }
bool Upgrade2Pressed { get; }
bool Upgrade3Pressed { get; }
bool PausePressed { get; }
```

Los flags `*Pressed` se setean `true` en el frame donde ocurre el press y se limpian al final del frame (patrón `WasPressedThisFrame`).

#### Regla 4 — Input buffering

Para acciones discretas de combate (`Strike`, `Counter`, `PullToggle`), el sistema implementa un buffer de corta duración:

1. Cuando llega un press y el consumidor no puede procesarlo (ej: está en medio de una animación de ataque), el intent se guarda en un buffer con timestamp.
2. El buffer tiene una ventana de validez configurable (default: `0.15s`).
3. En cada frame, los consumidores pueden llamar `ConsumeBuffered(Intent)` para leer un input que llegó dentro de la ventana.
4. Una vez consumido, el buffer se limpia para ese intent.

Esto permite combos más fluidos y perdona timing imperfecto, especialmente a 30 FPS en WebGL donde un frame dura 33ms.

#### Regla 5 — Aim resolution centralizada

La conversión de screen-space mouse → world-space aim direction se hace **una sola vez por frame** dentro de `GameInputProvider`, no en cada consumidor. Hoy `MagnetismController.AimDirection()` hace un raycast propio cada vez que necesita aim — esto se mueve al provider.

```csharp
void UpdateAim()
{
    Camera cam = cameraOverride ?? Camera.main;
    if (cam == null || Mouse.current == null) return;

    aimScreenPosition = Mouse.current.position.ReadValue();
    Ray ray = cam.ScreenPointToRay(aimScreenPosition);
    Plane ground = new Plane(Vector3.up, playerTransform.position);

    if (ground.Raycast(ray, out float dist))
    {
        Vector3 hit = ray.GetPoint(dist);
        Vector3 dir = hit - playerTransform.position;
        dir.y = 0f;
        aimWorldDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : playerTransform.forward;
    }
}
```

#### Regla 6 — Gameplay lock

El provider tiene un método `SetInputEnabled(bool enabled)` que suprime todos los intents de gameplay sin desactivar la UI (para pantallas de upgrade, cutscenes, death screen). Cuando `enabled = false`:

- `MoveAxis` retorna `Vector2.zero`
- Todos los `*Pressed` retornan `false`
- `AimWorldDirection` mantiene el último valor válido (para que la cámara no salte)

### States and Transitions

El input system tiene tres estados, controlados por `meta-flow-system`:

| Estado | Action Map activo | Descripción |
|---|---|---|
| **Gameplay** | `Player` | Todos los intents de combate/movimiento habilitados |
| **UI** | `UI` | Solo navegación de menú (mouse, navigate, submit, cancel) |
| **Disabled** | Ninguno | Input completamente suprimido (transiciones, loading) |

```
┌──────────┐   RunStarted    ┌──────────┐   Pause/LevelUp   ┌──────────┐
│ Disabled │ ───────────────▶ │ Gameplay │ ◀────────────────▶ │    UI    │
└──────────┘                  └──────────┘                    └──────────┘
      ▲                             │                               │
      └─────────── Death / RunEnded ┘───────────────────────────────┘
```

**Transiciones:**
1. `Disabled → Gameplay`: al iniciar la run (`meta-flow-system` envía `RunStarted`).
2. `Gameplay → UI`: al pausar, abrir pantalla de upgrade, o death screen.
3. `UI → Gameplay`: al cerrar pausa o confirmar upgrade.
4. `Gameplay → Disabled` / `UI → Disabled`: al morir (death animation), fin de run, o loading.

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `player-movement` | downstream | `MoveAxis`, `AimWorldDirection` | Lee `GameInputProvider.MoveAxis` cada frame |
| `combat-system` | downstream | `StrikePressed`, `CounterPressed` | Lee/consume buffered intents |
| `magnetism-system` | downstream | `PullTogglePressed`, `AimWorldDirection` | Lee/consume buffered pull; usa aim para repel cone |
| `upgrade-system` | downstream | `Upgrade1/2/3Pressed` | Lee cuando UI de upgrade está activa |
| `camera-system` | downstream | `AimScreenPosition` | Para look-ahead o cursor-following opcionales |
| `meta-flow-system` | upstream | Comandos de cambio de estado | Llama `SetState(InputState)` |
| `hud-system` | downstream | `AimWorldDirection` | Para cono de repulsión y retícula |

**Ownership:**
- `input-system` **owns** la traducción hardware → intent y el input buffering.
- `input-system` **NO owns** la lógica de qué hacer con cada intent (eso es del consumidor).
- `meta-flow-system` **owns** qué estado de input está activo.

## Formulas

El input system tiene pocas fórmulas — es mayormente lógico. Las únicas relevantes son:

### Input Buffer Validity

```
isValid = (Time.time - bufferedTimestamp) <= bufferWindow
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `bufferWindow` | `float` (segundos) | 0.05 – 0.30 | 0.15 |
| `bufferedTimestamp` | `float` | — | `Time.time` al recibir press |

### Aim Deadzone (gamepad futuro)

```
effectiveAim = aimMagnitude > deadzone ? (aimRaw - deadzone) / (1 - deadzone) : Vector2.zero
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `deadzone` | `float` | 0.05 – 0.30 | 0.15 |
| `aimRaw` | `Vector2` | 0..1 por eje | stick input |

### Aim Smoothing (solo gamepad)

```
smoothedAim = Vector2.Lerp(previousAim, rawAim, 1 - e^(-aimSharpness × dt))
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `aimSharpness` | `float` | 5 – 30 | 15 |

Mouse no usa smoothing — la latencia percibida es inaceptable para aim en arena combat.

## Edge Cases

### E1 — PullToggle durante animación de Strike

**Caso:** el jugador presiona LMB mientras Strike está en ejecución.
**Resolución:** el buffer retiene el `PullToggle` intent. Cuando Strike termina y `magnetism-system` llama `ConsumeBuffered(PullToggle)`, lo procesa. Si la ventana de buffer expiró, se descarta — no hay pull fantasma.

### E2 — Strike y Counter en el mismo frame

**Caso:** el jugador presiona click derecho y espacio simultáneamente.
**Resolución:** Counter tiene **prioridad** sobre Strike. Si ambos llegan en el mismo frame, solo `CounterPressed` se reporta como `true`. Rationale: counter es defensivo y tiene cooldown — priorizarlo evita muertes injustas.

### E3 — Aim en el borde de la pantalla WebGL

**Caso:** en WebGL el cursor puede salir del canvas del juego. `Mouse.current.position` devuelve la última posición conocida, que puede estar fuera del viewport.
**Resolución:** `UpdateAim()` valida que la posición del mouse esté dentro de `Screen.width/height`. Si está fuera, mantiene el último `AimWorldDirection` válido. No se resetea a `transform.forward` para evitar snaps visuales.

### E4 — Tab-away / focus loss

**Caso:** el jugador cambia de tab en el browser. Los keys quedan "stuck" en pressed.
**Resolución:** al detectar `Application.focusChanged(false)`, el provider hace un reset forzado de todos los estados: `MoveAxis = zero`, todos los `*Pressed = false`, `pullHeld = false`. Al volver el foco, no hay inputs residuales.

### E5 — Upgrade choice fuera de pantalla de upgrade

**Caso:** el jugador presiona 1/2/3 durante gameplay normal (no en upgrade screen).
**Resolución:** `Upgrade1/2/3Pressed` solo se expone en estado `UI` y solo cuando el `upgrade-system` está activo. En estado `Gameplay`, las teclas 1/2/3 no generan ningún intent.

### E6 — Buffer de múltiples presses

**Caso:** el jugador spamea Strike 3 veces en 0.1s.
**Resolución:** el buffer retiene **solo el más reciente** por cada tipo de intent. No se acumulan múltiples presses — esto evita inputs fantasma que disparan 3 ataques seguidos sin control del jugador.

### E7 — Repel sin payload

**Caso:** el jugador hace PullToggle cuando no hay nada en órbita ni pull activo.
**Resolución:** el intent se entrega normalmente al `magnetism-system`. Es responsabilidad de ese sistema decidir si iniciar Pull (no hay payload → start pull) o ignorar. El input system no filtra por contexto de gameplay.

## Dependencies

### Upstream (este sistema depende de)

Ninguna. El input system es Layer 0 Foundation — funciona sin ningún otro sistema del juego.

Dependencia técnica (no de gameplay):
- **Unity Input System package** (`com.unity.inputsystem`): asset `.inputactions`, `PlayerInput` component, `InputValue` callbacks.
- **Camera (referencia)**: para aim resolution (screen → world). Si no hay cámara, aim fallback es `transform.forward`.

### Downstream (dependen de este sistema)

| Sistema | Tipo | Qué consume |
|---|---|---|
| `player-movement` | **Hard** | `MoveAxis` (sin esto no hay movimiento) |
| `combat-system` | **Hard** | `StrikePressed`, `CounterPressed` (sin esto no hay combate) |
| `magnetism-system` | **Hard** | `PullTogglePressed`, `AimWorldDirection` (sin esto no hay magnetismo) |
| `upgrade-system` | **Soft** | `Upgrade1/2/3Pressed` (puede usar mouse click como alternativa) |
| `camera-system` | **Soft** | `AimScreenPosition` (funciona sin esto, solo pierde look-ahead) |
| `hud-system` | **Soft** | `AimWorldDirection` (para cono de repulsión visual) |
| `meta-flow-system` | **Control** | Llama `SetState()` para cambiar action map activo |

### Interfaz bidireccional: marca magnética

El input system **no participa** en el contrato de marca magnética (`magnetism-system` ↔ `combat-system` ↔ `enemy-system`). Solo transporta el intent `Strike` — la lógica de aplicar marca es 100% del `combat-system`.

## Tuning Knobs

| Knob | Default | Rango seguro | Efecto si demasiado bajo | Efecto si demasiado alto |
|---|---|---|---|---|
| `bufferWindow` | 0.15s | 0.05 – 0.30 | Inputs se pierden, combate se siente unresponsive | Inputs se ejecutan tarde, se siente laggy y surgen combos accidentales |
| `aimDeadzone` | 0.15 | 0.05 – 0.30 | Stick drift causa aim errático | Zona muerta grande, aim se siente pegajoso |
| `aimSharpness` | 15 | 5 – 30 | Aim con stick se siente pesado | Aim con stick salta, pierde suavidad |
| `counterPriority` | true | bool | — | — (si false, strike y counter compiten en mismo frame) |

### Interacciones entre knobs

- `bufferWindow` interactúa con `attackCooldown` del `combat-system` (0.38s): si el buffer es mayor que el cooldown, el jugador puede encolar ataques de forma no intuitiva. **Regla**: `bufferWindow < attackCooldown / 2`.
- `aimDeadzone` y `aimSharpness` solo aplican a gamepad. En mouse son ignorados. No hay interacción con knobs de otros sistemas.

## Visual/Audio Requirements

### Visual

- **Sin UI propia.** El input system no tiene visualización directa. El aim indicator (línea + punta) es responsabilidad del `magnetism-system` / `presentation-system`, que leen `AimWorldDirection` del provider.
- **Rebind UI** (post-MVP): si se implementa, se presenta como panel dentro del menú de pausa. Diseño delegado a `hud-system`.

### Audio

- **Sin audio propio.** Los sonidos de click/press son responsabilidad de `presentation-system` (feedback de input). El input system solo transporta intents.

## UI Requirements

### MVP

- Sin UI de rebinding. Los controles están fijos según la tabla del GDD §4.
- Los controles se muestran en un overlay de tutorial visual (responsabilidad del `meta-flow-system`, no del input system).

### Post-MVP

- **Pantalla de rebinding**: UI que muestre cada intent con su binding actual. El jugador puede clickear un intent y presionar la nueva tecla.
- **Persistencia**: los bindings customizados se guardan en `PlayerPrefs` (WebGL) con key `"InputBindings"`.
- **Reset to defaults**: botón que restaura el `.inputactions` original.
- **Indicadores contextuales**: los prompts en pantalla (ej: "Press LMB to Pull") deben mostrar la tecla actualmente bindeada, no la default. Esto requiere que `GameInputProvider` exponga `string GetBindingDisplay(Intent intent)`.

## Acceptance Criteria

### Funcionales

1. **AC-1**: `GameInputProvider` existe como componente en el player prefab y expone `MoveAxis`, `AimWorldDirection`, `StrikePressed`, `CounterPressed`, `PullTogglePressed`.
2. **AC-2**: `ArkhamPlayerMotor` lee movimiento exclusivamente desde `GameInputProvider.MoveAxis`, no desde callbacks `OnMove(InputValue)` directos.
3. **AC-3**: `ArkhamCombatController` consume strike/counter exclusivamente desde el provider, eliminando los métodos `OnAttack(InputValue)`, `OnJump(InputValue)`, `OnDodge(InputValue)`.
4. **AC-4**: `MagnetismController` consume pull/aim desde el provider, eliminando `OnPull(InputValue)`, `OnInteract(InputValue)` y el raycast interno de `AimDirection()`.
5. **AC-5**: Input buffer funciona: presionar Strike durante una animación de ataque ejecuta el strike al terminar la animación, si está dentro de la ventana de 0.15s.
6. **AC-6**: `SetInputEnabled(false)` suprime todos los intents de gameplay; `MoveAxis` retorna zero y ningún `*Pressed` retorna true.
7. **AC-7**: Cambio de tab en WebGL resetea todos los inputs — no quedan teclas "stuck".
8. **AC-8**: Counter y Strike en el mismo frame: solo counter se procesa.

### Rendimiento

9. **AC-9**: `GameInputProvider.Update()` no genera allocations (0 GC alloc por frame).
10. **AC-10**: El aim raycast se ejecuta máximo 1 vez por frame, no por consumidor.

### Migración

11. **AC-11**: El `.inputactions` asset se actualiza para reflejar los intents del GDD (eliminar `Crouch`, `Sprint`, `Interact` legacy; renombrar `Jump` → `Counter`; agregar `UpgradeChoice1/2/3`, `Pause`).
12. **AC-12**: Los 3 scripts consumidores (`ArkhamPlayerMotor`, `ArkhamCombatController`, `MagnetismController`) migran a leer del provider sin cambiar comportamiento observable.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿El Pull debe ser toggle (click on / click off) o hold (mantener presionado = pull, soltar = repel)? El GDD dice toggle, el código actual implementa toggle, pero hold podría ser más intuitivo para jugadores nuevos. ¿Ofrecer ambos como opción de settings? | cris | Pre-implementación |
| Q2 | ¿Cuánto buffering es "correcto"? 0.15s es un default conservador. Hades usa ~0.1s. Necesita playtesting para ajustar. | gameplay testing | Post-prototipo |
| Q3 | ¿El aim con gamepad debería usar auto-aim / aim assist (snap-to-nearest-enemy) o stick puro? Esto cambia la interfaz de aim que el provider expone. | cris | Cuando se agregue gamepad |
| Q4 | ¿`UpgradeChoice` usa teclas 1/2/3 fijas o el jugador hace click en la UI de upgrades? Si es click, el input system no necesita ese intent — la UI de upgrade consume clicks del action map `UI` directamente. | cris | Pre-upgrade-system GDD |
| Q5 | ¿Se necesita soporte touch para mobile WebGL? Si sí, el aim resolution cambia fundamentalmente (no hay hover, solo tap position). Scope jam dice no, pero conviene decidir ahora. | cris | Pre-implementación |
