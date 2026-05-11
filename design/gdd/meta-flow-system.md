# Meta Flow System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Flujo de sesión — el jugador entra, juega, muere, ve su score, y reinicia sin fricciones

## Overview

El Meta Flow System es la state machine maestra del juego. Controla el flujo completo de una sesión: Menu → Tutorial → Endless Run → Death → Score Screen → Restart. Gestiona las transiciones entre estados, la pausa, y la coordinación de inicio/fin de todos los demás sistemas. Es el "director de escena" que le dice a cada sistema cuándo empezar y cuándo parar.

No hay código existente — diseño nuevo desde cero.

## Player Fantasy

**"Entro, juego, muero, veo mi score, reinicio. Sin esperas."**

El flujo debe ser instantáneo y sin fricción. Del click "Play" al gameplay: < 2 segundos. Del death al retry: < 1 segundo. Cada transición es rápida, limpia, y visualmente comunicada.

## Detailed Design

### Core Rules

#### Regla 1 — State machine

```
                          ┌──────────────────────────────────────┐
                          ▼                                      │
  Loading ──▶ Menu ──▶ Tutorial ──▶ Playing ──▶ Dead ──▶ ScoreScreen
                ▲                      │  ▲                      │
                │                   Paused │                     │
                │                      │   │                     │
                └──────────────────────┴───┴─────────────────────┘
                              (Restart)
```

| Estado | TimeScale | Input | Sistemas activos |
|---|---|---|---|
| **Loading** | 1 | Bloqueado | Ninguno (carga de assets) |
| **Menu** | 0 | UI only | HUD (menu screen), host-bridge |
| **Tutorial** | 1 | Gameplay | Todos excepto wave-director (enemies pre-placed) |
| **Playing** | 1 | Gameplay | Todos |
| **Paused** | 0 | UI only | HUD (pause menu) |
| **Dead** | 0→slow | Bloqueado 1s, luego UI | presentation (death effects) |
| **ScoreScreen** | 0 | UI only | HUD (score breakdown), host-bridge |

#### Regla 2 — Transiciones

| From → To | Trigger | Acción |
|---|---|---|
| Loading → Menu | Assets loaded | Emit `GameReady` via host-bridge |
| Menu → Tutorial | Player clicks Play / React sends `StartRun` | Spawn player, show tutorial tips |
| Tutorial → Playing | 20s elapsed or player action | Start `wave-director`, hide tutorial UI |
| Playing → Paused | ESC key | `Time.timeScale = 0`, show pause menu |
| Paused → Playing | ESC key or Resume button | `Time.timeScale = 1`, hide pause menu |
| Playing → Dead | Player HP = 0 | Death effects (slow-mo 0.5s, camera shake), lock input |
| Dead → ScoreScreen | Death animation complete (1.5s) | Show score breakdown, emit `RunEnded` |
| ScoreScreen → Menu | Player clicks "Menu" | Reset all systems |
| ScoreScreen → Playing | Player clicks "Restart" / React sends `RestartRun` | Reset all systems, skip tutorial |

#### Regla 3 — Reset de sistemas

Al reiniciar (Restart o Menu → Play):
1. `scoring-xp-system.Reset()` — score, XP, level, combo a cero.
2. `upgrade-system.Reset()` — clear all upgrades.
3. `mission-system.Reset()` — clear active mission.
4. `powerup-system.Reset()` — deactivate active powerup.
5. `wave-director.Reset()` — act 1, wave 0.
6. `magnetism-system.Reset()` — charge to 0, clear orbit.
7. `damage-health-system.Reset()` — player HP to max.
8. `Pool.DespawnAll()` — return all pooled objects.
9. Spawn player at arena center.

El orden importa — se resetea de arriba (meta) hacia abajo (core).

#### Regla 4 — Tutorial (visual, 0-20s)

El tutorial no es una escena separada — es un estado del `Playing` con overlays:
- 3-4 tips visuales que aparecen progresivamente:
  - "WASD to move" (0-5s)
  - "Hold Right Click to Pull" (5-10s)
  - "Left Click to Repel" (10-15s)
  - "Space to Counter attacks" (15-20s)
- Después de 20s, los tips se desvanecen y el wave-director comienza.
- El player puede hacer skip presionando cualquier tecla de combate.

#### Regla 5 — Death sequence

1. Player HP llega a 0.
2. `Time.timeScale = 0.15` por 0.5s (death slow-mo).
3. Camera shake grande (0.26, 0.24s).
4. Player death animation.
5. `Time.timeScale = 0` (freeze).
6. 1s de freeze (jugador procesa que murió).
7. Transición a ScoreScreen.

### Interactions with Other Systems

| Sistema | Dirección | Datos |
|---|---|---|
| `host-bridge` | **bidirectional** | Commands (StartRun, Restart) / Events (GameReady, RunEnded, StateChanged) |
| `damage-health-system` | **upstream** | Player death event triggers Dead state |
| `wave-director` | **downstream** | Start/Stop oleadas |
| `scoring-xp-system` | **downstream** | Reset + final stats |
| `upgrade-system` | **downstream** | Reset |
| `mission-system` | **downstream** | Reset |
| `powerup-system` | **downstream** | Reset |
| `hud-system` | **downstream** | Screen state (menu/gameplay/death/score) |
| ALL systems | **downstream** | `Time.timeScale` control |

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `damage-health-system` | **Hard** — player death event |
| `host-bridge` | **Hard** — external commands |

### Downstream
| Sistema | Tipo |
|---|---|
| Todos | **Hard** — controla lifecycle de todos los sistemas |

## Tuning Knobs

| Knob | Default | Rango |
|---|---|---|
| `tutorialDuration` | 20s | 10–30s |
| `deathSlowMoDuration` | 0.5s | 0.3–1.0s |
| `deathSlowMoScale` | 0.15 | 0.05–0.3 |
| `deathFreezeTime` | 1.0s | 0.5–2.0s |
| `transitionFadeDuration` | 0.3s | 0.1–0.5s |

## Acceptance Criteria

1. **AC-1**: State machine completa: Loading→Menu→Tutorial→Playing→Dead→ScoreScreen→(Restart).
2. **AC-2**: Pausa con ESC setea `Time.timeScale = 0` y muestra pause menu.
3. **AC-3**: Death sequence incluye slow-mo + freeze + transición a score.
4. **AC-4**: Restart resetea todos los sistemas y comienza nueva run.
5. **AC-5**: Tutorial muestra 4 tips progresivos en los primeros 20s.
6. **AC-6**: `RunEnded` event se emite con stats completas al morir.
7. **AC-7**: Transición Menu→Play < 2 segundos.
8. **AC-8**: Transición Death→Restart < 1 segundo.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Daily/weekly challenges? Un modo que modifica los knobs (más HP, solo Mines, etc). Agrega replayability pero requiere UI adicional. | cris | Post-MVP |
| Q2 | ¿Skip tutorial en runs posteriores? Auto-detect si el player ya jugó (localStorage). | cris | Pre-implementation |
| Q3 | ¿Cutscene de intro? Un breve "el robot cae en la arena de chatarra" (3s). Agrega contexto narrativo pero delay al gameplay. | cris | Post-playtest |
