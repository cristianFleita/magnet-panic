# Overload System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Riesgo/Recompensa — cargar más chatarra = más daño pero riesgo de explosión involuntaria

## Overview

El Overload System es la mecánica de riesgo/recompensa del magnetismo. Monitorea la carga magnética del jugador (`MagnetismController.CurrentCharge / MaxCapacity`) y aplica estados de presión creciente: Normal → Crítico → Overload. En Overload, el jugador explota radialmente, empuja a todos los enemigos cercanos, vacía su carga y queda vulnerable brevemente.

El jugador interactúa indirectamente — la carga sube al atraer chatarra y enemigos magnetizados. La decisión táctica es: ¿retengo chatarra para un repel masivo (más daño) arriesgando overload, o la suelto antes? Sin este sistema, el jugador acumularía carga infinita sin consecuencia, eliminando la tensión del loop Pull→Orbit→Repel.

Hoy no hay código de overload — solo existe `currentCharge`/`maxCapacity` en `MagnetismController` con `OnChargeChanged` event. Este GDD diseña el sistema completo sobre esa base.

## Player Fantasy

**"Estoy al borde del colapso... ¡y eso me da poder!"**

La fantasía es la del reactor nuclear que el jugador controla — cuanto más cerca del límite, más intenso todo se siente. La barra de carga se llena, la pantalla vibra, el hum sube de tono, y el jugador decide: ¿repelo ahora por daño masivo, o aguanto un segundo más para atraer ese último enemigo? El overload no es un castigo — es una herramienta que el jugador avanzado provoca intencionalmente para AoE damage.

Referencia: **Splatoon** (tanque de tinta como recurso que limita la agresividad), **Risk of Rain 2** (items acumulativos que generan poder y caos simultáneamente), **Nuclear Throne** (nombre literal de la fantasía: el trono nuclear, el poder al borde del descontrol).

## Detailed Design

### Core Rules

#### Regla 1 — Tres estados de carga

| Estado | Rango de carga | Efecto |
|---|---|---|
| **Normal** | 0% – 69% | Sin efectos especiales. Gameplay base |
| **Critical** | 70% – 99% | Warnings visuales/audio. Movimiento penalizado (vía `player-movement`). Gameplay más tenso |
| **Overload** | 100% (se mantiene > `overloadGracePeriod` segundos) | Explosión radial AoE, carga vaciada, vulnerabilidad |

La transición a Overload NO es instantánea al llegar a 100% — hay un `overloadGracePeriod` (default 1.5s) donde el jugador puede repeler para bajar la carga. Esto evita overloads accidentales y da una ventana de reacción justa.

#### Regla 2 — Overload Grace Period

Cuando `currentCharge >= maxCapacity`:
1. Un timer de gracia comienza (`overloadGracePeriod = 1.5s`).
2. Si el jugador repele y baja la carga a < 100% antes del timer, el timer se resetea. Crisis evitada.
3. Si el timer llega a 0: Overload se dispara automáticamente.

Durante la gracia, el feedback visual/audio se intensifica (parpadeo rápido, pitch del hum sube). El HUD muestra un countdown visual.

#### Regla 3 — Explosión de Overload

Cuando el overload se dispara:

1. **AoE Damage**: todos los enemigos dentro de `overloadRadius` reciben `overloadDamage`.
2. **Knockback radial**: enemigos empujados desde el player con fuerza `overloadKnockbackForce`.
3. **Carga vaciada**: `currentCharge = 0`. Toda la chatarra en órbita se suelta (expulsada radialmente).
4. **Vulnerability window**: el jugador no puede usar Pull/Repel por `overloadCooldown` segundos.
5. **Movement lock**: el player queda lockeado `overloadStunDuration` segundos (animación de "colapso magnético").
6. **Camera shake**: shake de alta amplitud (`0.30, 0.3s`).

#### Regla 4 — Overload como herramienta

El jugador avanzado puede provocar overload intencionalmente:
- Atraer chatarra hasta 100%, esperar la gracia, dejar que explote → AoE clear.
- Esto es viable porque `overloadDamage` es alto (mata Scraplings de un golpe) y el knockback crea espacio.
- El costo es la vulnerability window — si hay enemigos fuera del radio, el jugador queda expuesto.

#### Regla 5 — Carga pasiva por retención

Mientras el jugador tiene chatarra en órbita, la carga NO sube pasivamente — solo sube al atraer nuevos objetos. Pero si se implementa `magnetized enemy attraction` (atraer enemigos marcados), eso sí sube carga, creando presión pasiva.

#### Regla 6 — Overload NO mata al jugador

El overload daña enemigos, no al jugador. La penalización es la vulnerability window + pérdida de toda la chatarra acumulada. El jugador no pierde HP por overload.

### States and Transitions

```
                    charge >= 70%            charge >= 100%
  Normal ──────────────────────▶ Critical ───────────────────▶ GracePeriod
    ▲                                ▲                              │
    │ charge < 70%                   │ charge < 100%                │ timer expires
    │                                │ (repel in time)              ▼
    │                                └────────────────────── Overload (explosion)
    │                                                               │
    └───────────────── cooldown expires ────────────────────────────┘
                       (recovery → Normal)
```

| Estado | `CanPull` | `CanRepel` | Visual | Audio |
|---|---|---|---|---|
| **Normal** | ✅ | ✅ | Barra normal | Hum base |
| **Critical** | ✅ | ✅ | Barra parpadea amarillo, partículas de chispa | Hum más agudo, beeps de warning |
| **GracePeriod** | ❌ | ✅ (urgente!) | Barra roja parpadeante, screen edge glow | Alarma, countdown audible |
| **Overload** | ❌ | ❌ (explosión automática) | Flash blanco, shockwave VFX | Explosión + silence momentáneo |
| **Recovery** | ❌ | ❌ | Barra vacía, player "aturdido" | Hum reiniciándose, tono bajo |

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `magnetism-system` | **upstream** | `CurrentCharge`, `MaxCapacity`, `OnChargeChanged` event | Lee propiedades + suscribe evento |
| `magnetism-system` | **downstream** | Disable Pull durante GracePeriod/Overload/Recovery | `magnetism.SetPullEnabled(false)` |
| `magnetism-system` | **downstream** | Forzar release de chatarra en órbita | `magnetism.ForceReleaseAll()` |
| `player-movement` | **downstream** | Movement lock durante explosion + recovery | `motor.SetMovementLocked(true, true)` |
| `combat-system` | **downstream** | Disable Strike/Counter durante recovery (opcional) | `combat.SetCombatEnabled(false)` |
| `damage-health-system` | **downstream** | Aplicar daño AoE a enemigos en radio | `CombatHealth.TakeDamage(overloadDamage)` |
| `camera-system` | **downstream** | Shake de explosión | `cameraRig.Shake(0.30, 0.3)` |
| `presentation-system` | **downstream** | VFX/SFX de warning, explosión, recovery | Eventos `OnCritical`, `OnOverload`, `OnRecovery` |
| `hud-system` | **downstream** | Estado de overload + grace timer para UI | `OverloadState`, `GraceTimeRemaining` |
| `upgrade-system` | **downstream** | Upgrades que modifican overload (más capacidad, menos cooldown, más daño) | Tuning knob setters |
| `scoring-xp-system` | **downstream** | Overload kills como fuente de XP/combo bonus | Indirecto vía `damage-health` death events |

## Formulas

### Overload Damage

```
overloadDamage = baseOverloadDamage + floor(chargeAtOverload / damagePerCharge)
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `baseOverloadDamage` | `int` | 2 – 8 | 4 |
| `chargeAtOverload` | `float` | siempre `maxCapacity` al momento de overload | 8 |
| `damagePerCharge` | `float` | 1 – 4 | 2 |

Ejemplo: `4 + floor(8/2) = 8 damage`. Un Scrapling (3 HP) muere. Un Heavy Bot (8 HP) sobrevive con 0 HP → muere.

### Knockback Force

```
knockbackForce = overloadKnockbackForce × (1 - distance/overloadRadius)
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `overloadKnockbackForce` | `float` | 10 – 30 | 18 |
| `overloadRadius` | `float` (m) | 4 – 10 | 6 |

Enemigos cercanos reciben knockback fuerte. Los del borde del radio reciben knockback mínimo. Esto crea una "onda expansiva" natural.

### Grace Period Timer

```
if (currentCharge >= maxCapacity)
    graceTimer -= Time.deltaTime;
else
    graceTimer = overloadGracePeriod; // reset
    
if (graceTimer <= 0) TriggerOverload();
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `overloadGracePeriod` | `float` (s) | 0.5 – 3.0 | 1.5 |
| `overloadCooldown` | `float` (s) | 1.0 – 4.0 | 2.0 |
| `overloadStunDuration` | `float` (s) | 0.3 – 1.0 | 0.5 |

## Edge Cases

### E1 — Overload con 0 enemigos en radio

**Caso:** el jugador overloadea en una zona sin enemigos.
**Resolución:** la explosión ocurre igual (VFX/SFX), la carga se vacía, la vulnerability window se aplica. Es un overload "desperdiciado" — el jugador pierde chatarra sin beneficio. Esto es un error del jugador, no un bug.

### E2 — Repel durante GracePeriod

**Caso:** el jugador repele durante la gracia, bajando la carga a 50%.
**Resolución:** el timer se resetea. El estado baja a Normal (o Critical si > 70%). Crisis evitada. Este es el gameplay loop — la gracia existe para esto.

### E3 — Atraer chatarra durante GracePeriod

**Caso:** Pull está deshabilitado durante GracePeriod, pero ¿qué pasa con chatarra que ya estaba siendo atraída?
**Resolución:** la chatarra en tránsito se suelta inmediatamente al entrar en GracePeriod (la atracción se corta). Solo Repel funciona como escape.

### E4 — Player muere durante overload

**Caso:** un enemigo mata al player durante la vulnerability window post-overload.
**Resolución:** la muerte tiene prioridad. El recovery se aborta. `meta-flow-system` toma el control. Esto es un riesgo intencional del overload.

### E5 — Overload + Wall Slam combo

**Caso:** la explosión de overload empuja un enemigo contra una pared → wall slam.
**Resolución:** sí, el knockback radial puede causar wall slams. El daño se acumula: `overloadDamage + wallSlamDamage`. Esto es un combo emergente deseable — recompensa al jugador que overloadea cerca de una pared.

### E6 — Múltiples overloads rápidos

**Caso:** el jugador overloadea, recovery termina, inmediatamente atrae y overloadea de nuevo.
**Resolución:** no hay cooldown para entrar en Critical/GracePeriod de nuevo. El `overloadCooldown` solo aplica a Pull/Repel. Si el jugador logra llenar la carga tan rápido, merece el segundo overload.

### E7 — Upgrade que aumenta maxCapacity

**Caso:** un upgrade sube `maxCapacity` de 8 a 12. ¿Overload es más difícil de alcanzar?
**Resolución:** sí, intencionalmente. Más capacidad = más chatarra = más daño por repel, y overload es más raro. Es un tradeoff: seguridad vs frecuencia de AoE explosiva.

## Dependencies

### Upstream

| Sistema | Tipo | Qué consume |
|---|---|---|
| `magnetism-system` | **Hard** | `CurrentCharge`, `MaxCapacity`, `OnChargeChanged`. Sin esto no hay carga que monitorear |

### Downstream

| Sistema | Tipo | Qué consume |
|---|---|---|
| `magnetism-system` | **Hard** | `SetPullEnabled(false)`, `ForceReleaseAll()` para controlar Pull |
| `player-movement` | **Hard** | `SetMovementLocked()` durante stun |
| `damage-health-system` | **Hard** | `TakeDamage()` para el AoE |
| `camera-system` | **Soft** | `Shake()` para feedback de explosión |
| `presentation-system` | **Soft** | Eventos de warning/explosión/recovery para VFX/SFX |
| `hud-system` | **Soft** | Estado + timer para visualización |
| `upgrade-system` | **Soft** | Setters para modificar knobs |
| `scoring-xp-system` | **Soft** | Overload kills como fuente de XP |

## Tuning Knobs

| Knob | Default | Rango seguro | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `criticalThreshold` | 0.7 (70%) | 0.5 – 0.9 | Warning muy temprano, pierde urgencia | Warning muy tarde, no da tiempo a reaccionar |
| `overloadGracePeriod` | 1.5s | 0.5 – 3.0 | Overloads accidentales, frustración | Overload nunca ocurre, pierde tensión |
| `baseOverloadDamage` | 4 | 2 – 8 | Overload no vale la pena provocarlo | Overload trivializa el combate |
| `overloadRadius` | 6m | 4 – 10 | Solo afecta melee range, poco impacto | Afecta toda la arena, demasiado potente |
| `overloadKnockbackForce` | 18 | 10 – 30 | Enemigos apenas se mueven | Enemigos salen volando, wall slams garantizados |
| `overloadCooldown` | 2.0s | 1.0 – 4.0 | Recovery casi instantáneo, sin castigo | Recovery demasiado largo, frustrante |
| `overloadStunDuration` | 0.5s | 0.3 – 1.0 | Stun imperceptible | Player queda inmóvil mucho tiempo, muere |

### Interacciones entre knobs

- `overloadGracePeriod` × `criticalThreshold`: si threshold es 0.9 y grace es 0.5s, el jugador tiene ~0.5s de warning total — muy poco. **Regla**: `(1 - criticalThreshold) × avgChargeTime + gracePeriod > 2s` para dar reacción suficiente.
- `baseOverloadDamage` × `overloadRadius`: daño alto + radio grande = screen clear. Con defaults (4+4=8 dmg, 6m radio) mata Scraplings (3HP) pero no Heavy Bots solos.
- `overloadCooldown` × `overloadStunDuration`: ambos son "castigo". La suma es el downtime total (`2.0 + 0.5 = 2.5s`). Más de 3s total se siente excesivo en un action game a 30 FPS.

## Visual/Audio Requirements

### Visual

- **Normal**: sin efectos especiales.
- **Critical (70-99%)**: barra de carga parpadea amarillo. Partículas de chispa eléctrica alrededor del player. Intensidad proporcional a la carga.
- **GracePeriod (100%)**: barra roja parpadeante rápido. Glow rojo en el borde de la pantalla (vignette). Partículas intensas. Countdown visual en la barra.
- **Overload (explosión)**: flash blanco breve (2 frames), shockwave ring VFX expandiéndose desde el player, partículas de debris radiales. La chatarra en órbita sale disparada visiblemente.
- **Recovery**: player con efecto visual "apagado" (desaturación leve), partículas de humo/vapor.

### Audio

- **Critical**: hum magnético sube de pitch. Beeps de warning intermitentes (tipo reactor).
- **GracePeriod**: alarma continua, pitch ascendente. Heartbeat bajo.
- **Overload**: explosión electromagnética profunda + silence de 0.3s post-explosión (para impacto dramático).
- **Recovery**: tono bajo descendente, "powering back up" hum.

## UI Requirements

### MVP

- **Barra de carga**: ya existe en concepto (HUD). Debe cambiar color: azul (Normal) → amarillo (Critical) → rojo parpadeante (GracePeriod).
- **Grace countdown**: indicador visual en la barra de carga mostrando el tiempo restante antes del overload. Puede ser un fill inverso o un número.
- **Recovery indicator**: icono de "disabled" sobre la barra de carga durante el cooldown.

### Post-MVP

- **Overload kill counter**: notificación "OVERLOAD ×3" mostrando cuántos enemigos mató la explosión.
- **Grace period audio cue**: countdown audible (3, 2, 1...) para accesibilidad.

## Acceptance Criteria

### Funcionales

1. **AC-1**: Al alcanzar 70% de carga, el estado cambia a Critical con feedback visual/audio.
2. **AC-2**: Al alcanzar 100% de carga, comienza el GracePeriod de 1.5s. Pull se deshabilita.
3. **AC-3**: Si el jugador repele y baja de 100% durante la gracia, el timer se resetea.
4. **AC-4**: Si el grace timer llega a 0, se dispara la explosión de overload.
5. **AC-5**: La explosión hace `overloadDamage` a todos los enemigos dentro de `overloadRadius`.
6. **AC-6**: Los enemigos reciben knockback radial proporcional a la distancia inversa.
7. **AC-7**: La carga se vacía a 0 tras el overload. Toda la chatarra en órbita se suelta.
8. **AC-8**: El player queda movement-locked por `overloadStunDuration` y sin Pull/Repel por `overloadCooldown`.
9. **AC-9**: Un Scrapling (3 HP) dentro del radio muere por overload (damage 8 > 3).
10. **AC-10**: Knockback de overload puede causar wall slams (daño acumulativo).

### Rendimiento

11. **AC-11**: La detección de enemigos en radio usa `Physics.OverlapSphere` (1 query por overload, no per-frame).
12. **AC-12**: Los warnings visuales de Critical no agregan más de 0.5ms al frame time.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿El overload debería hacer daño al jugador? El GDD original dice "no". Pero un costo de 1 HP por overload agregaría riesgo real. Propongo: no en MVP, testear post-playtest. | cris | Post-playtest |
| Q2 | ¿Upgrade "Controlled Overload" que permite triggerear overload manualmente (tecla dedicada) sin esperar grace period? Sería un power move táctico. ¿Lo diseñamos como upgrade o como feature base? | cris | Pre-upgrade-system GDD |
| Q3 | ¿La carga debería decaer lentamente con el tiempo (passive discharge) para que el jugador no se quede en Critical eternamente? Esto reduciría la presión pero haría el sistema más forgiving. | cris | Post-playtest |
| Q4 | ¿Overload frequency scaling? En oleadas tardías, ¿la carga debería subir más rápido para forzar más overloads y crear más caos? Esto podría ser un knob del `wave-director`. | cris | Pre-wave-director GDD |
