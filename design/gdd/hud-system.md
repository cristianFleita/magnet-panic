# HUD System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Legibilidad — el jugador siempre sabe su estado sin dejar de mirar la acción

## Overview

El HUD System renderiza toda la información de gameplay en pantalla: HP del jugador, barra de carga magnética, XP/level, combo counter, score, misión activa, powerup timer, boss HP bar, y upgrade icons. Usa UI Toolkit (Unity) para UI screen-space, y `WorldSpaceHealthBar` para las barras de vida de enemigos.

Hoy existe `PlayerHealthHud` (UI Toolkit procedural, HP bar) y `WorldSpaceHealthBar` (canvas worldspace, enemy HP). El GDD formaliza todos los elementos del HUD y sus layouts.

## Player Fantasy

**"Todo lo que necesito saber está ahí — sin estorbar."**

El HUD es invisible cuando funciona bien. El jugador mira la acción, no el HUD. Pero con un vistazo periférico de 0.2s, obtiene: HP, carga, combo, misión. Es un dashboard de piloto de combate — denso pero legible.

## Detailed Design

### Core Rules

#### Regla 1 — Layout de pantalla

```
┌─────────────────────────────────────────────────┐
│ [HP Bar]              [Score]                   │
│ [Charge Bar]          [Combo ×5]                │
│ [XP Bar] Lv.3                                   │
│ [Upgrade Icons]                  [Mission Card] │
│                                                 │
│                                                 │
│                  [GAMEPLAY]                      │
│                                                 │
│                                                 │
│               [Boss HP Bar]                     │
│ [Powerup Icon+Timer]                            │
└─────────────────────────────────────────────────┘
```

| Elemento | Posición | Source System | Siempre visible |
|---|---|---|---|
| HP Bar | Top-left | `damage-health-system` | Sí |
| Charge Bar | Below HP | `magnetism-system` | Sí |
| XP Bar + Level | Below Charge | `scoring-xp-system` | Sí |
| Upgrade Icons | Below XP | `upgrade-system` | Sí (si hay upgrades) |
| Score | Top-right | `scoring-xp-system` | Sí |
| Combo Counter | Below Score | `scoring-xp-system` | Solo si combo > 1 |
| Mission Card | Right side | `mission-system` | Solo si misión activa |
| Boss HP Bar | Bottom-center | `boss-system` | Solo durante boss fight |
| Powerup Icon | Bottom-left | `powerup-system` | Solo si powerup activo |

#### Regla 2 — UI Toolkit procedural

Todo el HUD se construye en código (no UXML) siguiendo el patrón de `PlayerHealthHud`. Esto permite:
- Sin assets de UI que mantener.
- Fácil de iterar durante la jam.
- Zero-dependency de archivos externos.

#### Regla 3 — Charge Bar con estados de overload

La barra de carga cambia de color según el estado del `overload-system`:

| Estado | Color | Efecto |
|---|---|---|
| Normal (0-69%) | Cyan (#28E8FF) | Fill estático |
| Critical (70-99%) | Amarillo (#FFD42A) | Parpadeo lento (1Hz) |
| GracePeriod (100%) | Rojo (#FF3333) | Parpadeo rápido (4Hz) + glow |
| Recovery | Gris (#666666) | Barra vacía, sin interactividad |

#### Regla 4 — Combo counter animado

El combo counter aparece al primer kill de un combo y crece con cada kill:
- Texto "×2", "×3"... con scale-up animation (0.1s).
- Timer visual: ring que se vacía alrededor del número.
- Al expirar: fade out 0.3s.
- Color escala: blanco (×2) → amarillo (×5) → dorado (×10) → rojo (×15+).

#### Regla 5 — XP popup flotante

Cuando el jugador gana XP, un número flotante aparece sobre el enemigo muerto y sube 1m en 0.8s antes de desvanecerse. Color por calidad. Se renderiza en screen-space con posición proyectada desde worldspace.

### Interactions with Other Systems

| Sistema | Datos consumidos | Refresh rate |
|---|---|---|
| `damage-health-system` | CurrentHP, MaxHP | On event |
| `magnetism-system` | CurrentCharge, MaxCapacity | Per frame |
| `overload-system` | OverloadState, GraceTimeRemaining | Per frame |
| `scoring-xp-system` | CurrentXP, XPToNext, Level, ComboCount, Score | On event + per frame (score) |
| `upgrade-system` | AcquiredUpgrades list | On upgrade |
| `mission-system` | MissionName, Progress, Timer | Per frame |
| `boss-system` | BossHP, BossMaxHP, BossName | Per frame (during boss) |
| `powerup-system` | ActivePowerup, RemainingTime | Per frame (during powerup) |

## Dependencies

### Upstream (todos soft — HUD es read-only)
| Sistema | Datos |
|---|---|
| `damage-health-system` | HP |
| `magnetism-system` | Charge |
| `overload-system` | State |
| `scoring-xp-system` | XP, combo, score |
| `upgrade-system` | Acquired upgrades |
| `mission-system` | Active mission |
| `boss-system` | Boss HP |
| `powerup-system` | Active powerup |

### Downstream
Ninguno — el HUD es terminal. No escribe a ningún sistema.

## Tuning Knobs

| Knob | Default | Efecto |
|---|---|---|
| `hudOpacity` | 0.85 | Transparencia general del HUD |
| `comboFadeDelay` | 0.5s | Tiempo antes de que el combo desaparezca tras reset |
| `xpPopupDuration` | 0.8s | Cuánto dura el número flotante de XP |
| `chargeBarFlashRate` | 1-4 Hz | Velocidad de parpadeo por estado de overload |

## Acceptance Criteria

1. **AC-1**: HP bar muestra salud actual/máxima con fill proporcional.
2. **AC-2**: Charge bar cambia color según estado de overload (cyan/yellow/red/grey).
3. **AC-3**: XP bar se llena y muestra nivel actual.
4. **AC-4**: Combo counter aparece al combo > 1 con timer visual.
5. **AC-5**: Score incrementa en tiempo real.
6. **AC-6**: Mission card muestra nombre + progreso + timer cuando hay misión activa.
7. **AC-7**: Boss HP bar aparece solo durante boss fight.
8. **AC-8**: Todos los elementos usan UI Toolkit procedural (sin UXML).
9. **AC-9**: HUD no genera GC allocations per frame (cache de strings, no concatenación).

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Mini-map? Muestra posición de enemigos/chatarra. Útil pero ocupa espacio y reduce la tensión de "no saber qué hay detrás". | cris | Post-playtest |
| Q2 | ¿Settings para escalar/mover elementos del HUD? Para accesibilidad. Bajo prioridad en jam. | cris | Post-MVP |
