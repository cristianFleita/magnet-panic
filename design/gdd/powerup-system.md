# Powerup System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Momentos de poder — los powerups crean picos de poder temporal que hacen al jugador sentirse imparable

## Overview

El Powerup System gestiona efectos temporales de alto impacto que alteran radicalmente el gameplay por un tiempo limitado. A diferencia de los upgrades (permanentes, incrementales), los powerups son explosiones de poder que duran 8-15 segundos. Se obtienen como reward de misiones, drops de bosses, o eventos especiales del `wave-director`.

El jugador no controla cuándo aparece un powerup, pero puede decidir cuándo activarlo (si es un pickup) o simplemente disfrutarlo (si es reward inmediato). Los powerups están diseñados para crear "momentos de highlight reel" — esos 10 segundos donde el jugador se siente invencible.

No hay código existente — diseño nuevo desde cero.

## Player Fantasy

**"¡PODER ABSOLUTO... por 10 segundos!"**

La fantasía es la del power trip temporal: todo se amplifica, todo es más fácil, todo explota más. El jugador pasa de "estoy sobreviviendo" a "soy un dios magnético" por un breve instante. Cuando el powerup termina, el jugador vuelve a la normalidad con la memoria de lo que fue — y quiere más.

Referencia: **Mario** (Star = invencibilidad temporal), **Vampire Survivors** (rosario = screen clear), **Geometry Wars** (smart bomb = momento de alivio).

## Detailed Design

### Core Rules

#### Regla 1 — Catálogo MVP (4 powerups)

| # | Nombre | Efecto | Duración | Fuente |
|---|---|---|---|---|
| 1 | **Repel 360** | El repel se dispara en 360° en vez de cono frontal. Toda la chatarra sale en todas las direcciones. | 10s | Misión / Boss drop |
| 2 | **Slow Time** | `Time.timeScale = 0.4`. Todo se mueve lento excepto el input del jugador (el player se mueve a velocidad normal en time scale reducido). | 8s | Misión / Boss drop |
| 3 | **Magnet Fever** | Pull range ×3, pull speed ×2. Toda la chatarra de la arena vuela hacia el jugador. Auto-atrae enemigos magnetizados. | 12s | Misión |
| 4 | **Scrap Storm** | Chatarra en órbita gira más rápido y daña enemigos que toquen al pasar (1 dmg per tick, 0.3s interval). El jugador se convierte en una tormenta ambulante. | 10s | Boss drop |

#### Regla 2 — Solo 1 powerup activo

No se acumulan. Si un nuevo powerup se activa mientras otro está activo, el anterior se cancela inmediatamente. El nuevo toma el control.

#### Regla 3 — Activación

Dos modos de activación según la fuente:

| Fuente | Activación | Descripción |
|---|---|---|
| **Reward de misión** | Inmediata | Se activa al completar la misión. Sin pickup. |
| **Boss drop** | Pickup en arena | Aparece como objeto brillante en el suelo. El jugador camina sobre él para activar. Persiste 15s antes de desaparecer. |
| **Wave event** (post-MVP) | Pickup en arena | Spawneado por el `wave-director` como bonus raro. |

#### Regla 4 — Implementación de efectos

Cada powerup implementa `IPowerupEffect`:

```csharp
interface IPowerupEffect
{
    void Activate(PowerupContext ctx);
    void Tick(float deltaTime);
    void Deactivate(PowerupContext ctx);
}
```

- `Activate`: aplica modificaciones (ej: Slow Time setea `Time.timeScale = 0.4`).
- `Tick`: lógica per-frame si es necesario (ej: Scrap Storm chequea hits).
- `Deactivate`: revierte modificaciones (ej: Slow Time restaura `Time.timeScale = 1`).

#### Regla 5 — Timer visual

Durante un powerup activo, el HUD muestra:
- Ícono del powerup.
- Timer circular que se vacía.
- Flash de warning cuando quedan 2s.

#### Regla 6 — Slow Time: player speed compensation

Slow Time reduce `Time.timeScale` a 0.4, pero el player debe moverse a velocidad "normal". Esto se logra dividiendo el `deltaTime` del motor del player:

```csharp
// En player-movement durante Slow Time
float effectiveDt = Time.deltaTime / Time.timeScale;
// Esto hace que el player se mueva a velocidad real
// mientras todo lo demás se mueve al 40%
```

El input también se compensa. El efecto neto: el jugador tiene 2.5× más tiempo de reacción.

### States and Transitions

```
  Inactive ──[Activate(powerup)]──▶ Active ──[timer expires]──▶ Deactivating ──[0.3s]──▶ Inactive
                                        │
                                   [New powerup]
                                        ▼
                                   Deactivate old → Activate new
```

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `mission-system` | **upstream** trigger | Reward = powerup aleatorio | `ActivateRandomPowerup()` |
| `boss-system` | **upstream** trigger | Boss drop = powerup pickup | `SpawnPowerupPickup(position)` |
| `magnetism-system` | **downstream** | Repel 360 (cone angle override), Magnet Fever (range/speed mult) | Temporary setters con restore |
| `player-movement` | **downstream** | Slow Time (time scale compensation) | Motor deltaTime override |
| `attractables-system` | **downstream** | Scrap Storm (orbit damage tick) | Temporary damage component |
| `hud-system` | **downstream** | Active powerup icon + timer | `ActivePowerup`, `RemainingTime` |
| `presentation-system` | **downstream** | VFX/SFX per powerup (aura, particles, music change) | Events `OnPowerupActivated/Deactivated` |
| `meta-flow-system` | **downstream** | Slow Time affects timeScale | `Time.timeScale` |

## Formulas

### Scrap Storm DPS
```
orbitDamagePerSecond = orbitDamage / orbitDamageInterval
```

| Variable | Default |
|---|---|
| `orbitDamage` | 1 |
| `orbitDamageInterval` | 0.3s |
| Effective DPS | 3.3 per enemy touching orbit |

### Slow Time player speed
```
effectivePlayerSpeed = baseSpeed / timeScale = 5 / 0.4 = 12.5 m/s (perceived)
// But in real time: player moves at normal 5 m/s
// Enemies move at 0.4 × their speed
```

## Edge Cases

### E1 — Slow Time + upgrade screen
**Resolución:** si level up ocurre durante Slow Time, el `Time.timeScale` se setea a 0 (pausa upgrade). Al salir del upgrade, se restaura a 0.4 (no a 1.0). El timer del powerup NO avanza durante pausa.

### E2 — Powerup pickup despawns mientras jugador se acerca
**Resolución:** visual warning (parpadeo) en los últimos 3s del pickup lifetime. Si expira, flash + despawn.

### E3 — Repel 360 sin chatarra en órbita
**Resolución:** el powerup se activa pero no tiene efecto hasta que el jugador atraiga chatarra. El timer corre igual — es responsabilidad del jugador tener ammo.

### E4 — Scrap Storm + Overload
**Resolución:** Scrap Storm no previene overload. Si la carga llega a 100%, el overload se dispara normalmente. La chatarra se expulsa (termina el efecto de Storm damage para esas piezas).

### E5 — Player muere con powerup activo
**Resolución:** `Deactivate()` se llama automáticamente. `Time.timeScale` se restaura a 1. Ningún efecto persiste post-muerte.

### E6 — Dos powerups se activan en el mismo frame
**Resolución:** el primero en llegar gana. El segundo reemplaza inmediatamente (Deactivate del primero → Activate del segundo). Orden determinista por prioridad interna.

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `mission-system` | **Hard** — principal fuente de powerups |
| `boss-system` | **Soft** — drops de boss |

### Downstream
| Sistema | Tipo |
|---|---|
| `magnetism-system` | **Hard** — Repel 360, Magnet Fever |
| `player-movement` | **Hard** — Slow Time compensation |
| `attractables-system` | **Soft** — Scrap Storm damage |
| `hud-system` | **Soft** — UI display |
| `presentation-system` | **Soft** — VFX/SFX |

## Tuning Knobs

| Knob | Default | Rango | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `powerupDuration` (per type) | 8–12s | 5–20s | Powerup termina antes de aprovecharlo | Powerup dura tanto que trivializa |
| `slowTimeScale` | 0.4 | 0.1–0.7 | Casi freeze, demasiado fácil | Casi velocidad normal, imperceptible |
| `magnetFeverRangeMult` | 3.0 | 2–5 | Rango no se siente ampliado | Toda la arena se atrae, overwhelm |
| `scrapStormDamage` | 1 | 1–3 | Damage insignificante | Kills automáticos, player no hace nada |
| `scrapStormInterval` | 0.3s | 0.1–0.5 | DPS excesivo | DPS bajo, no vale |
| `pickupLifetime` | 15s | 8–30s | Pickup desaparece muy rápido | Pickup persiste mucho, pierde urgencia |

## Visual/Audio Requirements

- **Repel 360**: aura electromagnética circular alrededor del player. Partículas radiando en 360°.
- **Slow Time**: post-processing desaturado + chromatic aberration leve. Audio pitch-shifted down. Player tiene trail de afterimages.
- **Magnet Fever**: campo magnético visible (líneas de fuerza convergiendo al player). SFX de magnetismo intenso. Chatarra volando de todas las direcciones.
- **Scrap Storm**: órbita de chatarra con trail intenso. Chispas y sparks en los puntos de daño. SFX de metal rotando.
- **Pickup**: glow pulsante brillante en el suelo. Columna de luz sutil.
- **Deactivation**: flash descendente + SFX de "power down".

## UI Requirements

### MVP
- **Active powerup indicator**: ícono grande en el HUD con timer circular. Posición: debajo del HP bar o esquina inferior izquierda.
- **Warning**: parpadeo del ícono cuando quedan 2s.
- **Pickup**: indicador worldspace (glow + nombre del powerup flotando).

## Acceptance Criteria

1. **AC-1**: Solo 1 powerup activo a la vez. Activar uno nuevo cancela el anterior.
2. **AC-2**: Repel 360 dispara chatarra en todas las direcciones durante su duración.
3. **AC-3**: Slow Time reduce `Time.timeScale` a 0.4 pero el player se mueve a velocidad normal percibida.
4. **AC-4**: Magnet Fever triplica el rango de pull.
5. **AC-5**: Scrap Storm hace 1 dmg cada 0.3s a enemigos que tocan la órbita de chatarra.
6. **AC-6**: El timer del powerup se muestra en el HUD y se pausa durante upgrade screen.
7. **AC-7**: Al expirar, todos los efectos se revierten correctamente (timeScale, ranges, etc).
8. **AC-8**: Powerup pickup en arena desaparece después de 15s si no se recoge.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Powerup "Shield" que absorbe 1 hit sin daño? Es más defensivo que los otros. ¿Encaja con el feel del juego (ofensivo)? | cris | Post-playtest |
| Q2 | ¿Los powerups deberían ser seleccionables (el player elige cuál activar) en vez de aleatorios? Agrega control pero UI más compleja. | cris | Post-playtest |
| Q3 | ¿Cooldown global de powerups? Si hay muchas fuentes (misiones + bosses + events), el player podría encadenar powerups sin descanso. ¿10s cooldown entre powerups? | cris | Post-playtest |
