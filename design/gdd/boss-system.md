# Boss System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Hito dramático — los bosses son el clímax de cada acto que valida el dominio del jugador

## Overview

El Boss System define los encuentros de mini-boss que marcan la transición entre actos del `wave-director`. Cada boss es una extensión del `enemy-system` con patrones de ataque únicos, fases de comportamiento, y mecánicas que explotan el loop Pull→Orbit→Repel de formas no vistas en combate normal. Son los "exámenes" que prueban que el jugador domina las mecánicas introducidas en el acto.

El boss MVP es el **Scrap Brute** — un enemigo grande que persigue al player, absorbe chatarra del suelo para curarse/fortalecerse, y tiene una ventana de vulnerabilidad post-overload donde el player puede hacer daño máximo.

Hay 4 boss prefabs ya creados (George, Leela, Mike, Stan) listos para recibir la lógica de comportamiento.

## Player Fantasy

**"Este es mi momento — todo lo que aprendí se pone a prueba."**

Los bosses son el pico emocional de cada acto. El jugador pasa de "estoy matando hordas" a "estoy peleando algo que me obliga a pensar". La tensión sube, la música cambia, y cada error duele más. Pero cuando el boss cae, la satisfacción es proporcional.

Referencia: **Hades** (bosses con fases claras y patterns aprendibles), **Cuphead** (patterns que usan las mecánicas base de formas creativas), **Risk of Rain 2** (bosses como hitos de dificultad que testean el build).

## Detailed Design

### Core Rules

#### Regla 1 — Scrap Brute (Boss MVP)

| Stat | Valor |
|---|---|
| HP | 40 (Acto 1) → +15 por acto posterior |
| Velocidad approach | 3.5 m/s |
| Velocidad charge | 8 m/s |
| Masa magnética | 8 (no atraíble, demasiado pesado) |
| Marcas para stun | 5 (muchos strikes necesarios) |

#### Regla 2 — 4 Fases del Scrap Brute

| Fase | Trigger | Comportamiento | Duración |
|---|---|---|---|
| **Chase** | Inicio / post-recovery | Persigue al player. Ataques melee (slam, sweep). | Hasta absorber o hasta 10s |
| **Absorb** | Cada 8-10s | Se detiene, atrae chatarra del suelo hacia sí mismo. Se cura 2 HP por pieza absorbida. Invulnerable. | 3s |
| **Overcharge** | HP < 50% | Emite pulso AoE que empuja al player. Spawea 3 Scraplings. Más agresivo post-pulse. | 2s pulse + 8s agresión |
| **Vulnerable** | Post-Overcharge | Queda aturdido 3s. Recibe 2× daño. La ventana para máximo DPS del player. | 3s |

```
Chase ──[8-10s]──▶ Absorb ──[3s]──▶ Chase
  │                                     │
  └──[HP<50%]──▶ Overcharge ──[10s]──▶ Vulnerable ──[3s]──▶ Chase
```

#### Regla 3 — Absorb como mecánica anti-jugador

Durante la fase Absorb, el boss atrae chatarra del suelo hacia sí:
- Cada pieza absorbida cura 2 HP.
- El jugador puede contrarrestar esto: **repeler chatarra LEJOS del boss antes de la absorb**, o **atraer la chatarra primero** para que no esté disponible.
- Esto crea un meta-game: el jugador compite con el boss por los recursos de chatarra de la arena.

#### Regla 4 — Overcharge y Vulnerable

Cuando el boss baja al 50% HP:
1. Emite un pulso magnético AoE (radio 8m) que empuja al player y toda la chatarra.
2. Spawea 3 Scraplings como distracción.
3. Entra en modo agresivo: velocidad ×1.5, ataques más rápidos.
4. Después de 10s de agresión: colapsa y queda **Vulnerable** por 3s.
5. Durante Vulnerable: 2× daño recibido. El jugador debe aprovechar con repel de chatarra pesada.

#### Regla 5 — Ataques del boss

| Ataque | Daño | Rango | Telegraph | Counter? |
|---|---|---|---|---|
| **Slam** | 3 | 2m frontal | Levanta brazo 0.6s | Sí (counter window 0.4s) |
| **Sweep** | 2 | 3m arco 120° | Gira torso 0.5s | No (demasiado amplio) |
| **Charge** | 4 | Línea recta 12m | Agacha y brilla 0.8s | Sí (sidestep + counter) |
| **Ground Pound** | 5 AoE | 4m radio | Salta 1s | No (esquivar por posición) |

#### Regla 6 — Scaling por acto

Cada aparición del boss (Acto 2, 3, 4...) escala:
- HP: +15 por acto.
- Nuevos ataques desbloqueados (Acto 1: Slam + Sweep. Acto 2: + Charge. Acto 3+: + Ground Pound).
- Absorb cura más (2→3→4 HP por pieza).
- Vulnerable window se acorta (3s→2.5s→2s).

#### Regla 7 — Boss no es atraíble

El boss tiene `magneticMass = 8` y NO es atraíble por Pull (demasiado pesado). El player debe usar chatarra como proyectil contra él. Los strikes aplican marca, pero necesita 5 marks para stun temporal (0.8s) — no se magnetiza como los enemigos normales.

### States and Transitions

(Ver diagrama en Regla 2.)

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `enemy-system` | **upstream** base | Hereda de ArkhamEnemy (HP, marks, damage) | Extiende `ArkhamEnemy` o componente paralelo |
| `wave-director` | **upstream** caller | Trigger de spawn al final de acto | `SpawnBoss(bossConfig)` |
| `damage-health-system` | **upstream** | HP, TakeDamage con multiplicador en Vulnerable | `CombatHealth` con vulnerability flag |
| `attractables-system` | **upstream** | Chatarra en la arena para mecánica Absorb | Queries `MagneticObject` en radio |
| `combat-system` | **upstream** | Strike/Counter del player | Counter window en Slam/Charge |
| `magnetism-system` | **upstream** | Repel de chatarra como daño | Impacto de `MagneticObject` projectiles |
| `camera-system` | **downstream** | Shake en ataques pesados (slam, ground pound) | `cameraRig.Shake()` |
| `hud-system` | **downstream** | Boss HP bar grande | `OnBossSpawned` / `OnBossDefeated` events |
| `presentation-system` | **downstream** | VFX/SFX de fases, ataques, transiciones | Events por fase |
| `scoring-xp-system` | **downstream** | Boss kill = XP bonus grande | `OnDeath` event con `isBoss` flag |

## Formulas

### Boss HP scaling
```
bossHP = baseHP + (actNumber - 1) × hpPerAct
```

| Variable | Default | Rango |
|---|---|---|
| `baseHP` | 40 | 25–60 |
| `hpPerAct` | 15 | 5–25 |

### Vulnerable damage multiplier
```
effectiveDamage = incomingDamage × vulnerableMultiplier  // durante Vulnerable
effectiveDamage = 0                                       // durante Absorb (invuln)
```

| Variable | Default |
|---|---|
| `vulnerableMultiplier` | 2.0 |

### Absorb healing
```
healPerPiece = baseHeal + (actNumber - 1)
```

| Variable | Default |
|---|---|
| `baseHeal` | 2 HP |

## Edge Cases

### E1 — No hay chatarra en la arena durante Absorb
**Resolución:** el boss hace la animación de absorb pero no cura. Continúa a Chase. Esto beneficia al jugador que limpió la chatarra.

### E2 — Player muere durante boss fight
**Resolución:** `meta-flow-system` toma control. El boss se despawnea. Run ends.

### E3 — Boss empujado contra pared por overload del player
**Resolución:** el boss NO recibe wall slam damage (inmune). Es demasiado pesado para ser afectado por knockback significativo. Recibe daño de overload normal.

### E4 — Counter del Charge attack
**Resolución:** si el player counterea durante la carga, el boss se stuneea 1.2s (mayor que el stun normal de 0.8s). Recompensa por timing difícil.

### E5 — Boss absorbe chatarra que el player estaba atrayendo
**Resolución:** si una pieza de chatarra está en estado `Attracting` (hacia el player), el boss NO puede absorberla. Solo chatarra `InWorld`. First come, first served.

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `enemy-system` | **Hard** — base AI, HP, marks |
| `wave-director` | **Hard** — trigger de spawn |
| `damage-health-system` | **Hard** — HP, vulnerability |
| `attractables-system` | **Hard** — chatarra para absorb |

### Downstream
| Sistema | Tipo |
|---|---|
| `hud-system` | **Soft** — boss HP bar |
| `scoring-xp-system` | **Soft** — bonus XP |
| `presentation-system` | **Soft** — VFX/SFX |

## Tuning Knobs

| Knob | Default | Rango | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `baseHP` | 40 | 25–60 | Boss muere rápido, anticlimático | Boss es esponja, tedioso |
| `vulnerableMultiplier` | 2.0 | 1.5–3.0 | Vulnerable window poco impactante | Vulnerable = insta-kill, trivializa |
| `vulnerableDuration` | 3s | 1.5–5 | Muy poco tiempo para hacer daño | Demasiado fácil explotar |
| `absorbHealPerPiece` | 2 | 1–5 | Absorb irrelevante | Boss se cura completamente, frustración |
| `chargeSpeed` | 8 m/s | 5–12 | Charge esquivable sin esfuerzo | Charge imposible de esquivar |
| `marksToStun` | 5 | 3–8 | Se stuneea fácil con strikes | Nunca se stuneea, strikes inútiles |

## Visual/Audio Requirements

- **Spawn**: efecto de "terremoto" + partículas de debris al aparecer. Cámara shake.
- **Chase**: pisadas pesadas, cada paso shake micro (0.05, 0.05s).
- **Absorb**: vórtice de partículas magnéticas convergiendo al boss. Hum ascendente.
- **Overcharge**: flash electromagnético + shockwave visual.
- **Vulnerable**: boss "humea", efectos de chispa, glow de weakness.
- **Death**: explosión masiva de partículas + shake grande + slow-mo 0.3s.

## UI Requirements

- **Boss HP bar**: barra grande centrada en la parte superior de pantalla. Nombre del boss. Segmentos por fase (50% line visible).
- **Phase indicator**: ícono o texto breve al cambiar de fase ("VULNERABLE!").

## Acceptance Criteria

1. **AC-1**: Scrap Brute spawea al final de cada acto con HP escalado.
2. **AC-2**: Las 4 fases (Chase, Absorb, Overcharge, Vulnerable) transicionan correctamente.
3. **AC-3**: Durante Absorb, el boss atrae chatarra InWorld y se cura 2 HP por pieza.
4. **AC-4**: Durante Vulnerable, el boss recibe 2× daño.
5. **AC-5**: Slam y Charge son counterables. Sweep y Ground Pound no.
6. **AC-6**: El boss NO es atraíble por Pull (masa 8).
7. **AC-7**: Boss kill otorga XP bonus significativo.
8. **AC-8**: HP bar de boss aparece al spawnear y desaparece al morir.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Múltiples tipos de boss o solo Scrap Brute con variantes? 4 prefabs ya existen (George, Leela, Mike, Stan). ¿Cada uno tiene patterns únicos o son skins? | cris | Pre-implementation |
| Q2 | ¿El boss debería tener attacks que marquen magnéticamente AL PLAYER? Invertiría el loop — el player debe "desmarcarse" moviéndose. Mecánica espejo interesante. | cris | Post-playtest |
| Q3 | ¿Loot drop del boss? Un upgrade garantizado o chatarra especial. Recompensa tangible inmediata. | cris | Pre-upgrade-system GDD |
