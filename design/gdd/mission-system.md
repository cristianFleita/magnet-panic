# Mission System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Objetivos tácticos — las misiones guían al jugador hacia uso creativo del magnetismo

## Overview

El Mission System presenta objetivos tácticos de corta duración durante la run. Cada 45-60 segundos, una misión activa aparece (ej: "mata 3 enemigos con wall slam", "countera 5 ataques"). Completarla otorga una recompensa (XP bonus, curación, powerup). El jugador puede ignorar la misión sin penalización, pero completarla le da una ventaja significativa.

Las misiones cumplen dos funciones: 1) guiar al jugador hacia mecánicas que no está usando ("nunca hago wall slams" → misión de wall slam → descubre que es divertido), y 2) crear micro-objetivos que dan dirección al gameplay infinito. Sin misiones, el endless run se siente "sin rumbo". Con misiones, cada minuto tiene un propósito.

No hay código existente — diseño nuevo desde cero.

## Player Fantasy

**"Tengo un objetivo — y la recompensa vale el riesgo."**

La fantasía es la del mercenario que acepta contratos en medio del caos. "3 wall slams en 30 segundos — ¿puedo hacerlo?" La misión crea un sub-juego dentro del juego, dando al jugador un foco táctico que transforma el combate genérico en un desafío específico.

Referencia: **Fortnite** (challenges durante partida), **Risk of Rain 2** (shrines y challenges opcionales), **Deep Rock Galactic** (secondary objectives con bonus reward).

## Detailed Design

### Core Rules

#### Regla 1 — Misión activa: 1 a la vez

Solo hay una misión activa en cada momento. Al completar o expirar, se espera `missionCooldown` (15-20s) antes de ofrecer la siguiente.

#### Regla 2 — Catálogo MVP (5 misiones)

| # | Nombre | Objetivo | Tiempo | Recompensa | Dificultad |
|---|---|---|---|---|---|
| 1 | **Combo Hunter** | Alcanza combo ×5 | 30s | 50 XP + powerup aleatorio | Media |
| 2 | **Counterstorm** | Countera 3 ataques enemigos | 45s | 40 XP + heal 2 HP | Media |
| 3 | **Scrap Collector** | Atrae y orbita 6 piezas de chatarra simultáneamente | 40s | 60 XP | Fácil-Media |
| 4 | **Wall Slam** | Mata 2 enemigos con wall slam | 35s | 50 XP + heal 1 HP | Difícil |
| 5 | **No Hands** | Mata 4 enemigos solo con repel (sin strikes) | 40s | 70 XP | Difícil |

#### Regla 3 — Selección de misión

La próxima misión se elige al azar del catálogo, evitando repetir la misión anterior. No hay pesos por dificultad en MVP — todas tienen la misma probabilidad.

```csharp
MissionData SelectNextMission(MissionData previous)
{
    var candidates = catalog.Where(m => m != previous).ToList();
    return candidates[Random.Range(0, candidates.Count)];
}
```

#### Regla 4 — Tracking de progreso

Cada misión tiene un `IMissionTracker` que se suscribe a eventos del sistema relevante:

| Misión | Evento tracked | Source system |
|---|---|---|
| Combo Hunter | `OnComboChanged(count)` | `scoring-xp-system` |
| Counterstorm | `OnCounterSuccessful` | `combat-system` |
| Scrap Collector | `OnOrbitCountChanged(count)` | `magnetism-system` |
| Wall Slam | `OnWallSlamKill` | `arena-system` + `enemy-system` |
| No Hands | `OnKill(method)` where method == Repel | `scoring-xp-system` |

#### Regla 5 — Expiración

Si el timer llega a 0 sin completar la misión:
- La misión desaparece del HUD.
- Sin penalización.
- Cooldown de 15-20s antes de la siguiente.
- El progreso parcial se pierde.

#### Regla 6 — Recompensa inmediata

Al completar:
1. HUD muestra "MISSION COMPLETE!" con flash.
2. Recompensa se aplica inmediatamente (XP se suma, HP se cura, powerup se activa).
3. Cooldown de 15-20s antes de la siguiente misión.

### States and Transitions

```
  Cooldown ──[timer expires]──▶ Active ──[completed]──▶ Rewarding ──[0.5s]──▶ Cooldown
                                   │
                              [timer expires]
                                   ▼
                               Expired ──[0s]──▶ Cooldown
```

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `scoring-xp-system` | **upstream** | Combo count, kill events | Eventos |
| `combat-system` | **upstream** | Counter events | `OnCounterSuccessful` |
| `magnetism-system` | **upstream** | Orbit count | `OnOrbitCountChanged` |
| `arena-system` | **upstream** | Wall slam events | `OnWallSlamKill` |
| `enemy-system` | **upstream** | Kill method (repel, strike) | `KillContext` |
| `scoring-xp-system` | **downstream** | XP reward | `AddXP(amount)` |
| `damage-health-system` | **downstream** | HP reward | `combatHealth.Heal(amount)` |
| `powerup-system` | **downstream** | Powerup reward | `ActivateRandomPowerup()` |
| `hud-system` | **downstream** | Mission name, progress, timer | Propiedades públicas |

## Formulas

### Mission cooldown
```
cooldown = Random(minCooldown, maxCooldown)
```

| Variable | Default | Rango |
|---|---|---|
| `minCooldown` | 15s | 10–30s |
| `maxCooldown` | 20s | 15–45s |

### XP reward scaling (post-MVP)
```
adjustedReward = baseReward × (1 + currentAct × 0.1)
```

## Edge Cases

### E1 — Player sube de nivel durante misión
**Resolución:** la misión se pausa durante la pantalla de upgrade (Time.timeScale = 0). El timer no avanza. Al volver, continúa.

### E2 — Player muere durante misión activa
**Resolución:** la misión se aborta. Sin recompensa. El progreso parcial no cuenta.

### E3 — "No Hands" pero el player hizo un strike antes
**Resolución:** la misión trackea kills solo-repel desde que la misión se activó. Strikes previos no cuentan. Si el player hace un strike DURANTE la misión, las kills con strike no cuentan para el objetivo pero no fallan la misión — solo las kills con repel suman.

### E4 — Misión imposible por estado del juego
**Resolución:** "Scrap Collector: orbita 6 piezas" pero solo hay 3 en la arena. El `wave-director` spawea chatarra continuamente, así que eventualmente habrá suficiente. Si no, la misión expira sin penalización.

### E5 — Boss fight durante misión
**Resolución:** la misión sigue activa durante boss fight. Puede ser más difícil de completar, pero el timer sigue corriendo. Es una complicación intencional.

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `scoring-xp-system` | **Hard** — combo events, kill context |
| `combat-system` | **Hard** — counter events |
| `magnetism-system` | **Hard** — orbit count |
| `arena-system` | **Soft** — wall slam events |
| `enemy-system` | **Soft** — kill method |

### Downstream
| Sistema | Tipo |
|---|---|
| `scoring-xp-system` | **Soft** — XP reward |
| `damage-health-system` | **Soft** — HP reward |
| `powerup-system` | **Soft** — powerup reward |
| `hud-system` | **Soft** — UI display |

## Tuning Knobs

| Knob | Default | Rango | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `missionDuration` (per mission) | 30–45s | 15–60s | Misión imposible de completar | Misión trivial, sin presión |
| `missionCooldown` | 15–20s | 5–45s | Misiones constantes, overwhelm | Misiones raras, pierde relevancia |
| `xpReward` (per mission) | 40–70 | 20–150 | No vale la pena hacerla | Rompe la curva de XP |
| `healReward` | 1–2 HP | 0–3 | Reward irrelevante | Heal excesivo, trivializa daño |

## Visual/Audio Requirements

- **Mission appear**: slide-in desde la derecha con SFX de "radio transmission".
- **Progress bar**: barra circular o segmentada debajo del nombre de misión.
- **Complete**: flash verde + "MISSION COMPLETE" + SFX de reward (monedas/chime).
- **Expired**: fade out suave + SFX sutil de "misión perdida" (descending tone).

## UI Requirements

### MVP
- **Mission card**: esquina superior derecha. Nombre + objetivo + progress (2/3) + timer circular.
- **Compact**: no más de 120×60px para no obstruir la vista.

## Acceptance Criteria

1. **AC-1**: Una misión se activa cada 45-60s de gameplay.
2. **AC-2**: Solo 1 misión activa a la vez.
3. **AC-3**: Completar la misión otorga la recompensa inmediatamente.
4. **AC-4**: El timer de misión se pausa durante upgrade screen.
5. **AC-5**: Misión expirada no penaliza al jugador.
6. **AC-6**: "Wall Slam" trackea correctamente kills por wall slam.
7. **AC-7**: "No Hands" solo cuenta kills con repel (no strikes).
8. **AC-8**: No se repite la misión anterior.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Misiones elegibles? El player elige entre 2 misiones al inicio de cada ciclo. Agrega agency pero UI más compleja. | cris | Post-playtest |
| Q2 | ¿Difficulty scaling de misiones por acto? Misiones más difíciles con mejores rewards en actos tardíos. | cris | Post-playtest |
| Q3 | ¿Chain missions? Completar 3 misiones seguidas = mega reward. Agrega meta-objetivo. | cris | Post-MVP |
