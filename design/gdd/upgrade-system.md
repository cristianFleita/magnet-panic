# Upgrade System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Personalización de build — cada run se siente diferente gracias a las combinaciones de upgrades elegidos

## Overview

El Upgrade System presenta al jugador una elección de 3 mejoras cada vez que sube de nivel (triggereado por `scoring-xp-system`). Los upgrades son permanentes dentro de la run y modifican los sistemas Core (magnetismo, combate, movimiento, capacidad) para crear builds emergentes. Es el sistema que transforma una run genérica en "mi run" — la que yo construí con mis decisiones.

El jugador interactúa activamente: al subir de nivel, el gameplay se pausa, se muestran 3 opciones, y el jugador elige una. La decisión es irreversible dentro de la run. No hay código existente — diseño nuevo desde cero.

## Player Fantasy

**"Cada elección me define — esta run es mía."**

La fantasía es la de un inventor que modifica su equipo en tiempo real. Cada upgrade cambia cómo se siente el juego: "esta run voy full repel" o "esta run voy combo melee". La variedad de combinaciones hace que cada run sea única y cada death screen tenga un "la próxima pruebo otra cosa".

Referencia: **Vampire Survivors** (level up = choice of 3, builds emergentes), **Hades** (boons con sinergias entre dioses), **Slay the Spire** (card choice que define el deck/build).

## Detailed Design

### Core Rules

#### Regla 1 — Choice of 3

Al subir de nivel:
1. `Time.timeScale = 0` (pausa).
2. Se seleccionan 3 upgrades aleatorios del pool disponible.
3. Se muestran al jugador con nombre, ícono y descripción.
4. El jugador elige uno. Los otros 2 se descartan.
5. `Time.timeScale = 1` (resume).

No se puede "saltar" la elección. No hay reroll en MVP (ver Open Questions).

#### Regla 2 — Pools de upgrades

Los upgrades se organizan en pools temáticos para garantizar variedad en las opciones:

| Pool | Upgrades | Sistemas afectados |
|---|---|---|
| **Pull** | Pull Range+, Pull Speed+, Auto-Pull (chatarra cercana se atrae sola) | `magnetism-system` |
| **Repel** | Repel Damage+, Repel Speed+, Repel Cone Width+, Piercing+ | `magnetism-system`, `attractables-system` |
| **Combo** | Strike Speed+, Strike Range+, Counter Window+, Counter Damage+ | `combat-system` |
| **Capacity** | Max Capacity+, Charge Penalty Reduction, Overload Damage+, Overload Radius+ | `magnetism-system`, `overload-system` |
| **Special** | Sprint Speed+, Max HP+, XP Gain+, Healing on Kill | `player-movement`, `damage-health`, `scoring-xp` |

#### Regla 3 — Selection algorithm: 1 per pool, no repeats

Al ofrecer 3 opciones:
1. Elegir 3 pools diferentes al azar (de los 5 disponibles).
2. De cada pool elegido, seleccionar 1 upgrade no adquirido.
3. Si un pool está agotado (todos sus upgrades ya adquiridos), elegir otro pool.

Esto garantiza que las 3 opciones siempre son de categorías diferentes — no se ofrecen 3 upgrades de Repel juntos.

#### Regla 4 — Upgrade stacking

Algunos upgrades son stackeables (se pueden elegir múltiples veces con efecto acumulativo):

| Upgrade | Stackeable | Max stacks | Efecto por stack |
|---|---|---|---|
| Pull Range+ | Sí | 3 | +2m rango |
| Repel Damage+ | Sí | 3 | +1 daño base |
| Max Capacity+ | Sí | 3 | +3 capacidad |
| Max HP+ | Sí | 2 | +1 HP (y heal +1) |
| Strike Speed+ | Sí | 2 | -15% cooldown |
| Auto-Pull | No | 1 | Toggle, no stackea |
| XP Gain+ | Sí | 2 | +20% XP |
| Counter Window+ | No | 1 | +0.15s window |
| Healing on Kill | No | 1 | +1 HP por kill |

#### Regla 5 — Catálogo MVP (9-12 upgrades)

| # | Nombre | Pool | Efecto | Stackeable |
|---|---|---|---|---|
| 1 | **Magnetic Reach** | Pull | Pull range +2m | ×3 |
| 2 | **Quick Draw** | Pull | Pull speed +25% | ×2 |
| 3 | **Auto-Attract** | Pull | Chatarra en 3m se atrae automáticamente | ×1 |
| 4 | **Scrap Cannon** | Repel | Repel damage +1 | ×3 |
| 5 | **Railgun** | Repel | Repel speed +30%, piercing +1 | ×2 |
| 6 | **Quick Strike** | Combo | Strike cooldown -15% | ×2 |
| 7 | **Perfect Counter** | Combo | Counter window +0.15s, counter damage +2 | ×1 |
| 8 | **Deep Pockets** | Capacity | Max capacity +3 | ×3 |
| 9 | **Pressure Valve** | Capacity | Charge penalty reduction 50% | ×1 |
| 10 | **Meltdown** | Capacity | Overload damage +3, radius +1m | ×2 |
| 11 | **Reinforced** | Special | Max HP +1, heal +1 on pickup | ×2 |
| 12 | **Scavenger** | Special | Healing on kill (+1 HP per kill) | ×1 |

#### Regla 6 — Aplicación del upgrade

Cada upgrade modifica directamente el sistema target vía setters públicos:

```csharp
void ApplyUpgrade(UpgradeData upgrade)
{
    switch (upgrade.id)
    {
        case "magnetic_reach":
            magnetismController.PullRange += 2f;
            break;
        case "scrap_cannon":
            magnetismController.RepelDamageBonus += 1;
            break;
        case "deep_pockets":
            magnetismController.MaxCapacity += 3;
            break;
        // ...
    }
    acquiredUpgrades.Add(upgrade);
}
```

### States and Transitions

```
  Inactive ──[LevelUp event]──▶ Showing ──[PlayerChooses]──▶ Applying ──▶ Inactive
```

| Estado | TimeScale | Input | Descripción |
|---|---|---|---|
| **Inactive** | 1 | Gameplay | No hay UI de upgrade visible |
| **Showing** | 0 | UI only (mouse/keyboard) | 3 opciones mostradas, gameplay pausado |
| **Applying** | 0→1 | Bloqueado 0.3s | Upgrade aplicado, VFX de power-up, resume |

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `scoring-xp-system` | **upstream** trigger | `OnLevelUp` event | Desencadena la pantalla de choice |
| `magnetism-system` | **downstream** | Pull range, repel damage/speed, capacity | Setters públicos |
| `combat-system` | **downstream** | Strike cooldown, counter window | Setters públicos |
| `player-movement` | **downstream** | Sprint speed | `motor.SprintMultiplier` setter |
| `overload-system` | **downstream** | Overload damage, radius | Setters públicos |
| `damage-health-system` | **downstream** | Max HP, healing on kill | `CombatHealth` setters |
| `hud-system` | **downstream** | Lista de upgrades adquiridos para iconos | `AcquiredUpgrades` list |
| `meta-flow-system` | **downstream** | Pausa/resume de gameplay | `Time.timeScale` |

## Formulas

### Selection probability
```
Cada pool tiene probabilidad uniforme: 1/availablePools
Cada upgrade dentro del pool: 1/availableInPool
```

No hay pesos (MVP). Post-MVP: raridades (común/rara/épica) con pesos diferentes.

## Edge Cases

### E1 — Todos los upgrades adquiridos
**Resolución:** si todos los upgrades están al max stack, el level up no muestra pantalla de choice. El XP se convierte en score bonus (+50 por level). Esto es improbable con 12 upgrades × 2 avg stacks = ~24 level ups necesarios.

### E2 — Solo queda 1 pool con upgrades
**Resolución:** se muestran 3 opciones del mismo pool (excepción a la regla de diversidad). Si solo quedan 1-2 upgrades totales, se muestran los que hay + "Skip" (bonus XP).

### E3 — Level up durante level up (double level up)
**Resolución:** imposible — XP no se gana durante pausa (`Time.timeScale = 0`). Si por algún bug ocurre, se encolan y se muestran secuencialmente.

### E4 — Upgrade contradictorio
**Resolución:** no hay upgrades mutuamente excluyentes en MVP. Todos se acumulan positivamente. Post-MVP: "cursed upgrades" con tradeoffs.

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `scoring-xp-system` | **Hard** — level up trigger |

### Downstream
| Sistema | Tipo |
|---|---|
| `magnetism-system` | **Hard** — modifica pull/repel/capacity |
| `combat-system` | **Hard** — modifica strike/counter |
| `player-movement` | **Soft** — modifica sprint |
| `overload-system` | **Soft** — modifica overload |
| `damage-health-system` | **Soft** — modifica HP |
| `hud-system` | **Soft** — muestra upgrades |

## Tuning Knobs

| Knob | Default | Rango | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `choiceCount` | 3 | 2–4 | Menos opciones, menos agency | Más opciones, decision paralysis |
| `maxStacks` (per upgrade) | 1–3 | 1–5 | Upgrade se agota rápido | Player stackea infinito, broken |
| Efecto per stack (varies) | varies | varies | Upgrade imperceptible | Upgrade OP, trivializa |

## Visual/Audio Requirements

- **Panel de choice**: 3 tarjetas con ícono, nombre, descripción. Hover para detalle.
- **Selección**: tarjeta elegida se agranda + flash dorado. Las otras se desvanecen.
- **Power-up VFX**: al aplicar, el player emite un flash de energía breve.
- **SFX**: "power up" ascendente al elegir. "whoosh" al abrir panel.

## UI Requirements

### MVP
- **Choice panel**: 3 tarjetas centradas en pantalla. Background dimmed.
- **Tarjeta**: ícono (placeholder geometric shape) + nombre + 1 línea de efecto.
- **Acquired upgrades**: iconos pequeños junto a la XP bar mostrando qué se tiene.

## Acceptance Criteria

1. **AC-1**: Al subir de nivel, gameplay se pausa y se muestran 3 opciones de upgrade.
2. **AC-2**: Las 3 opciones son de pools diferentes (cuando es posible).
3. **AC-3**: Elegir un upgrade lo aplica inmediatamente al sistema correspondiente.
4. **AC-4**: Upgrades stackeables se pueden elegir múltiples veces hasta su max.
5. **AC-5**: El gameplay resume al elegir.
6. **AC-6**: Pull Range+ incrementa el rango de pull en 2m por stack.
7. **AC-7**: Healing on Kill otorga +1 HP por cada kill.
8. **AC-8**: Deep Pockets incrementa maxCapacity del magnetism en 3 por stack.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Reroll? Un botón para reroll las 3 opciones (1 vez por level up, o consumiendo XP). Agrega control pero reduce la tensión de la elección. | cris | Post-playtest |
| Q2 | ¿Raridades? Común/Rara/Épica con efectos más fuertes y menor probabilidad. Agrega depth pero complejidad de balanceo. | cris | Post-MVP |
| Q3 | ¿Sinergias explícitas? "Si tienes Scrap Cannon + Railgun = Mega Railgun". Agrega descubrimiento pero mucho más contenido. | cris | Post-MVP |
| Q4 | ¿Los upgrades deberían persistir entre runs? (meta-progression). El GDD original dice que no — cada run empieza de cero. Pero un unlock system de nuevos upgrades disponibles por milestone daría long-term goals. | cris | Post-MVP |
