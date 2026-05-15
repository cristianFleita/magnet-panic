# Upgrade System

> **Status**: In Design (v2 — jam-focused skill tree)
> **Author**: cris + agents
> **Last Updated**: 2026-05-15
> **Implements Pillar**: Personalización ofensiva — un mini-árbol de habilidades centrado en magnetismo y movilidad que cambia cómo se siente cada run de 5 minutos.

## Overview

El Upgrade System ofrece una elección de 3 mejoras cada vez que el jugador sube de nivel (triggereado por `scoring-xp-system`). Para una jam de 10 días con runs de ~5 minutos, el catálogo se redujo deliberadamente a **10 upgrades activos** organizados en 4 ramas: **Magnetismo, Combate, Capacidad y Movilidad**. La rama de Movilidad incluye un **mini árbol de prerrequisito** (Slam requiere Slide), introduciendo decisión de inversión.

Decisión clave: **se eliminaron todos los pasivos puros** (XP+, heal on kill, max HP+, sprint speed flat). En una run corta no generan decisiones interesantes; los reemplazan upgrades que cambian *cómo* jugás (nuevas mecánicas, desbloqueos de objetos, habilidades de movimiento). Cada upgrade modifica algo que el jugador siente en el siguiente segundo de gameplay.

No hay código existente — diseño nuevo desde cero.

## Player Fantasy

**"Mi imán crece conmigo — cada elección abre una nueva forma de jugar."**

La fantasía es la del inventor que ensambla su arsenal magnético en tiempo real. Una run puede ir hacia control de zona (Magnetic Chain + Deep Pockets), otra hacia movilidad agresiva (Magnetic Slide + Magnetic Slam + Wrecking Core), otra hacia destrucción pura (Scrap Cannon + Railgun). El árbol es chico a propósito: el jugador descubre los 10 nodos rápido y empieza a *combinarlos* en vez de leer cards.

Inspiraciones: el momentum y traversal de **Batman Arkham** y **Spider-Man (PS4)** para Magnetic Slide / Slam, las elecciones de boons de **Hades** para el flow de level up, **Vampire Survivors** para la legibilidad de cada upgrade.

## Detailed Design

### Core Rules

#### Regla 1 — Choice of 3
Al subir de nivel:
1. `Time.timeScale = 0`.
2. Se seleccionan 3 upgrades del pool disponible, priorizando ramas distintas.
3. El jugador elige uno (mouse o teclas 1-3).
4. `Time.timeScale = 1`.

No hay reroll en MVP. No hay skip salvo edge case E1.

#### Regla 2 — Catálogo MVP (10 upgrades, 4 ramas)

| # | Nombre | Rama | Efecto | Stacks | Por stack | Requiere |
|---|---|---|---|---|---|---|
| 1 | **Magnetic Reach** | Magnetismo | Pull range +1.5m | ×3 | +1.5m | — |
| 2 | **Quick Coil** | Magnetismo | Pull speed +30% | ×2 | +30% | — |
| 3 | **Magnetic Chain** | Magnetismo | Strike sobre enemigo magnetizado: chance de aplicar +1 marca a otro enemigo dentro de 3m | ×3 | 30% / 50% / 70% | — |
| 4 | **Scrap Cannon** | Combate | Repel damage +1 | ×3 | +1 | — |
| 5 | **Railgun** | Combate | Repel speed +30% y piercing +1 | ×2 | +30% / +1 pierce | — |
| 6 | **Deep Pockets** | Capacidad | Max capacity +3 | ×3 | +3 | — |
| 7 | **Heavy Lifter** | Capacidad | Desbloquea el atractable **Wrecking Core** (objeto super pesado, -60% movimiento mientras orbita, repel con piercing infinito y daño masivo) | ×1 | unlock | — |
| 8 | **Magnetic Slide** | Movilidad | Nueva habilidad: **doble-tap WASD** desliza al player 6m en esa dirección. 0.2s de invuln + 2 dmg y knockback a enemigos en el trayecto (escapar o golpear). Cooldown 4s | ×2 | -1s cooldown | — |
| 9 | **Magnetic Slam** | Movilidad | Nueva habilidad (tecla **F**): salto + caída magnética sobre el enemigo más cercano dentro de 5m. AoE 4m al aterrizar, 5 dmg y **knockdown** (1.5s stun). Cooldown 6s | ×1 | unlock | **Magnetic Slide** |
| 10 | **Iron Stride** | Movilidad | Reduce penalización de movimiento por carga 50% | ×1 | -50% | — |

#### Regla 3 — Selection algorithm: 1 por rama
1. Filtrar pool por upgrades **disponibles** (no maxeados **y con prerequisitos cumplidos**).
2. Si hay ≥3 ramas con disponibles → 1 random por rama.
3. Si hay <3 ramas → permitir duplicar rama, pero nunca el mismo upgrade.
4. Si solo queda 1 upgrade → mostrar ese + opción **Skip (+25 score bonus)**.

**Prerequisitos:** Magnetic Slam solo aparece como opción si Magnetic Slide ya fue adquirido (al menos stack 1). Sin Slide, Slam no entra al pool.

#### Regla 4 — Magnetic Chain (detalle)
- Trigger: cada Strike que conecta con un enemigo en estado `Magnetized`.
- Búsqueda: `Physics.OverlapSphere(targetPos, 3f, enemyLayer)`, excluye al target original.
- Roll: tirada *única* contra el enemigo más cercano. Si pasa, le aplica +1 magnetic mark.
- No encadena recursivamente en el mismo Strike (evita explosión combinatoria).
- VFX: chispa eléctrica conectando target original al chained.

#### Regla 5 — Heavy Lifter / Wrecking Core
- Antes del unlock: el Wrecking Core no está en el spawn pool del `wave-director`.
- Después del unlock: spawnea ~1 cada 30s en la arena.
- En órbita:
  - Ocupa **5 puntos** de capacidad.
  - Aplica **-60% movement** al jugador mientras esté en órbita (combinable con Iron Stride hasta cap -40%).
  - Solo 1 Wrecking Core puede estar en órbita a la vez (los demás ignoran al player hasta que el slot se libere).
- Al ser repelido:
  - Viaja lento (≈40% velocidad de chatarra normal).
  - **Piercing infinito**: atraviesa todos los enemigos sin perder daño.
  - Choque contra pared = explosión AoE 4m + screen shake fuerte.

#### Regla 6 — Magnetic Slide (detalle)
- **Input: doble-tap WASD** en la dirección deseada (ventana de 0.25s entre taps).
- Movimiento: impulso instantáneo de 6m en la dirección del segundo tap, capeado por raycast contra `WallLayer`.
- 0.2s de invulnerabilidad durante el slide.
- **Daño en trayecto**: cualquier enemigo que toca al player durante el slide recibe **2 dmg + knockback** radial. El jugador decide si lo usa para escapar (slide hacia espacio libre) o para golpear (slide a través de hordas).
- La órbita del player se mantiene (los objetos siguen al player con offset).
- Cooldown base 4s, baja a 3s con stack 2.

#### Regla 7 — Magnetic Slam (detalle)
- **Requiere Magnetic Slide adquirido** (prerequisito en el árbol — ver Regla 3).
- **Input: tecla F** (rebindable). Solo activo si Magnetic Slam está adquirido.
- Comportamiento:
  1. El player **salta** brevemente (~0.4s en el aire, durante los cuales es invulnerable y no recibe input de movimiento).
  2. Auto-targetea al **enemigo más cercano dentro de 5m** al activar.
  3. Cae sobre la posición del target (o sobre la posición actual si no hay enemigo en rango).
  4. Al aterrizar: **AoE 4m**, 5 dmg, **knockdown** (1.5s stun) a todos los enemigos dentro del radio.
- Cooldown base 6s.
- Si no hay enemigo en rango: el slam ejecuta igual en la posición actual del player (utility como wave-clear / interrupt).
- Sinergia con Slide: combo natural "Slide → Slam" para reposicionarse y limpiar (ver Open Questions Q1).

#### Regla 8 — Iron Stride
Reduce el penalty de mobility por carga (GDD §6.1, base -20% a carga llena) al **50%**. Independiente del penalty especial de Wrecking Core, que se mantiene fijo en -60% (ver Formulas para combinación).

#### Regla 9 — Aplicación
```csharp
void ApplyUpgrade(UpgradeData u) {
    switch (u.id) {
        case "magnetic_reach":  magnetism.PullRange += 1.5f; break;
        case "quick_coil":      magnetism.PullSpeedMult += 0.3f; break;
        case "magnetic_chain":  combat.ChainProbability += u.IsFirstStack ? 0.3f : 0.2f; break;
        case "scrap_cannon":    magnetism.RepelDamageBonus += 1; break;
        case "railgun":         magnetism.RepelSpeedMult += 0.3f; magnetism.RepelPiercing += 1; break;
        case "deep_pockets":    magnetism.MaxCapacity += 3; break;
        case "heavy_lifter":    waveDirector.UnlockSpawn("wrecking_core"); break;
        case "magnetic_slide":  movement.SlideUnlocked = true;
                                if (!u.IsFirstStack) movement.SlideCooldown -= 1f; break;
        case "magnetic_slam":   movement.SlamUnlocked = true; break;
        case "iron_stride":     magnetism.ChargeMobilityPenaltyMult *= 0.5f; break;
    }
    acquiredUpgrades.Add(u);
}
```

### States and Transitions

```
Inactive ──[OnLevelUp]──▶ Showing ──[PlayerChooses]──▶ Applying ──▶ Inactive
```

| Estado | TimeScale | Input |
|---|---|---|
| Inactive | 1 | Gameplay |
| Showing | 0 | UI only |
| Applying | 0→1 | Bloqueado 0.3s (VFX power-up) |

### Interactions with Other Systems

| Sistema | Dirección | Datos | Interfaz |
|---|---|---|---|
| `scoring-xp-system` | upstream (Hard) | `OnLevelUp` event | trigger del choice |
| `magnetism-system` | downstream (Hard) | range, speed, damage, capacity, piercing | setters públicos |
| `combat-system` | downstream (Hard) | chain probability, chain radius | setters públicos |
| `player-movement` | downstream (Hard) | slide unlock + cooldown, slam unlock, charge penalty mult, double-tap detection | setters públicos |
| `wave-director` | downstream (Hard) | unlock Wrecking Core spawn | `UnlockSpawn(id)` |
| `attractables-system` | downstream (Hard) | nuevo prefab Wrecking Core | registro de prefab |
| `hud-system` | downstream (Soft) | iconos de upgrades adquiridos, slide/slam cooldowns | `AcquiredUpgrades`, `SlideReady`, `SlamReady` |
| `presentation-system` | downstream (Soft) | VFX/SFX de power-up al elegir | event `OnUpgradeApplied` |

## Formulas

### Magnetic Chain probability per stack
```
P(chain) en stack 1 = 0.30
P(chain) en stack 2 = 0.50  (+0.20)
P(chain) en stack 3 = 0.70  (+0.20)
```
La curva no es lineal a propósito — el primer stack se siente, los siguientes son refinement.

### Charge mobility penalty (con Iron Stride)
```
basePenalty       = 0.20  (a carga llena, GDD §6.1)
penaltyEffective  = chargeRatio × basePenalty × (0.5 ^ ironStrideStacks)
sin stride: chargeRatio × 0.20  →  -20% a carga llena
con stride: chargeRatio × 0.10  →  -10% a carga llena
```

### Wrecking Core movement penalty (combinación)
```
playerSpeed = baseSpeed × (1 - 0.6 × wreckingMultiplier)
wreckingMultiplier_sinStride = 1.0  →  speed = 0.40 × base
wreckingMultiplier_conStride = 0.66 →  speed = 0.60 × base  (cap)
```
El cap evita que el Wrecking Core se vuelva trivial. Es siempre un trade-off.

### Magnetic Slide (distance / damage / cooldown)
```
slideDistance      = 6m (capeado por raycast a WallLayer)
slideIFrame        = 0.2s
slideContactDamage = 2 (a cada enemigo tocado en trayecto)
slideKnockbackForce = 600 (radial outward desde el path del slide)
cooldown           = 4s (stack 1) → 3s (stack 2)
doubleTapWindow    = 0.25s (entre los dos taps de la misma dirección)
```

### Magnetic Slam (AoE / knockdown / cooldown)
```
slamSearchRadius = 5m (auto-target del enemigo más cercano al activar)
slamAirTime      = 0.4s (invuln durante este lapso)
slamAoERadius    = 4m (al aterrizar)
slamDamage       = 5
slamKnockdownDuration = 1.5s (stun a enemigos en AoE)
cooldown         = 6s
```

## Edge Cases

### E1 — Pool agotado
Todos los upgrades al max stack. El level up entrega `+25 score bonus` y no muestra UI. Improbable: 10 upgrades × ~2 stacks promedio = ~19 level ups antes de saturar (más que una run típica).

### E2 — Magnetic Chain en strike sin magnetizado
Sin trigger. Magnetic Chain solo aplica si el target principal del Strike es `Magnetized`. Strikes sobre `Normal` o `Marked x1` no chequean cadena.

### E3 — Heavy Lifter elegido pero no spawnea Wrecking Core
El `wave-director` debe garantizar al menos 1 Wrecking Core spawn en los siguientes 60s post-unlock. Si no hay spawn natural en ese tiempo, forzar uno.

### E4 — Iron Stride + Wrecking Core
Penalty del Wrecking Core se reduce al 66% del original con Iron Stride (ver Formulas). No anula el trade-off.

### E5 — Slide a través de pared
Raycast desde player en la dirección del segundo tap; capear destino al primer hit con `WallLayer`. Si el tap apunta directo a pared cercana (<1m), el slide ejecuta el mínimo posible (0.5m) para mantener feedback de input.

### E6 — Slide con órbita activa
La órbita del player se mantiene (los objetos siguen con offset). La mina plantada (powerup) NO se mueve con el slide — es entidad estática.

### E7 — Doble-tap accidental durante movimiento normal
Detección estricta: ambos taps en la misma tecla, con la primera tecla soltada antes del segundo tap, dentro de la ventana 0.25s. Mantener WASD presionado no dispara slide.

### E8 — Magnetic Slam sin enemigos en rango
El slam ejecuta igual en la posición actual del player. AoE 4m sin auto-target. Útil para clear defensivo cuando enemigos están a >5m pero acercándose.

### E9 — Magnetic Slam targetea enemigo que muere mid-air
El target original se invalida, pero el slam ya está en aire. Cae en la **última posición conocida del target** (no se cancela). Si esa posición queda dentro de pared, snap al borde más cercano.

### E10 — Magnetic Slam mientras Wrecking Core en órbita
El slam ejecuta normal (no afecta órbita). El penalty de movimiento del Wrecking Core no aplica al air-time del slam (el player está en aire, no caminando).

### E11 — Magnetic Chain encadena en estado de overload
La marca se aplica igual. Si el enemigo sube a `Magnetized` mid-overload, queda elegible para pull/repel del próximo ciclo.

### E12 — Magnetic Slam ofrecido sin tener Slide
Imposible — Regla 3 filtra Slam del pool si Slide no fue adquirido. Si por bug llegara a aparecer, ApplyUpgrade aplica el unlock y el sistema queda en estado válido (Slam funciona stand-alone también, simplemente no fue la intención).

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `scoring-xp-system` | **Hard** — emite `OnLevelUp` |

### Downstream
| Sistema | Tipo |
|---|---|
| `magnetism-system` | **Hard** — recibe modifiers |
| `combat-system` | **Hard** — recibe chain config |
| `player-movement` | **Hard** — recibe dash y mobility |
| `wave-director` | **Hard** — recibe unlock de Wrecking Core |
| `attractables-system` | **Hard** — provee prefab Wrecking Core |
| `hud-system` | **Soft** — muestra adquiridos + dash cd |
| `presentation-system` | **Soft** — VFX al aplicar |

## Tuning Knobs

| Knob | Default | Rango | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `choiceCount` | 3 | 2–4 | menos agency | decision paralysis |
| `magneticChainRadius` | 3m | 2–5 | chain raro | trivializa hordas |
| `magneticChainProb[stack]` | 30/50/70 | 20–90 | upgrade flat | upgrade OP |
| `slideDistance` | 6m | 4–10 | sensación leve | teleport feel |
| `slideCooldown` | 4s | 2–8 | spam | irrelevante |
| `slideIFrame` | 0.2s | 0.1–0.5 | difícil dodge | invuln easy mode |
| `slideContactDamage` | 2 | 1–4 | utility puro | melee broken |
| `doubleTapWindow` | 0.25s | 0.15–0.4 | difícil de ejecutar | activación accidental |
| `slamSearchRadius` | 5m | 3–8 | sin target frecuente | auto-target lejano OP |
| `slamAoERadius` | 4m | 2–6 | wave clear pobre | trivializa hordas |
| `slamDamage` | 5 | 3–10 | sin punch | one-shots elites |
| `slamKnockdownDuration` | 1.5s | 0.5–3 | sin respiro | crowd control eterno |
| `slamCooldown` | 6s | 4–12 | spam | irrelevante |
| `wreckingCoreCapacity` | 5 | 3–8 | sin tradeoff | no cabe nunca |
| `wreckingCoreMovePenalty` | -60% | -40% a -80% | unlock free | inutilizable |
| `wreckingCoreSpawnInterval` | 30s | 20–60 | spam | nunca aparece |
| `ironStrideMult` | 0.5 | 0.3–0.8 | upgrade débil | overrides charge tradeoff |

## Visual/Audio Requirements

- **Choice panel**: 3 cards centradas, ícono geométrico + nombre + 1 línea efecto. Hover = detalle.
- **Selección**: card crece + flash dorado, otras se oscurecen.
- **Power-up VFX**: flash magnético azul desde el player al aplicar.
- **Magnetic Slide**: trail de afterimages cyan en la dirección del slide + burst magnético al destino. Streak particles en el path. SFX "whoosh + clack metálico". Chispa al impactar enemigo en trayecto.
- **Magnetic Slam**: takeoff = burst magnético hacia arriba (líneas de fuerza convergiendo). Air-time = silueta del player con trail. Landing = onda expansiva radial + grietas en el suelo (decal temporal) + screen shake fuerte. SFX "boom" grave con metal.
- **Magnetic Chain**: chispa eléctrica conectando target original al chained. SFX corto agudo.
- **Wrecking Core**: prefab grande oxidado (concept: bola de demolición magnetizada). Hum profundo en órbita. Impacto = boom + screen shake fuerte.
- **Heavy Lifter unlock**: cinemática breve (1s) mostrando el primer Wrecking Core entrando a la arena.

## UI Requirements

### MVP
- Choice panel: 3 tarjetas centradas, background dimmed.
- Tarjeta: ícono + nombre + 1 línea de efecto.
- **Acquired upgrades**: iconos pequeños debajo de la XP bar (con stack count si stackeable).
- **Magnetic Slide**: barra/ícono de cooldown sobre el HP bar (solo visible si adquirido).
- **Magnetic Slam**: ícono separado con cooldown junto a Slide (solo visible si adquirido). Tip on-screen "F" para enseñar input.
- **Wrecking Core en órbita**: ícono de "carga pesada" parpadeante.

## Acceptance Criteria

1. **AC-1**: Level up pausa el juego y muestra 3 opciones de upgrade de ramas distintas (cuando hay variedad).
2. **AC-2**: Elegir un upgrade lo aplica inmediatamente y resume el juego.
3. **AC-3**: Magnetic Reach aumenta el pull range en 1.5m por stack (verificable en debug overlay).
4. **AC-4**: Magnetic Chain con 1 stack tiene 30% chance de marcar un enemigo dentro de 3m del target del strike (test estadístico sobre 100 strikes ±5%).
5. **AC-5**: Adquirir Heavy Lifter desbloquea el spawn del Wrecking Core en el `wave-director`.
6. **AC-6**: Wrecking Core en órbita reduce el `playerSpeed` al 40% del base (60% con Iron Stride).
7. **AC-7**: Repeler un Wrecking Core lo lanza con piercing infinito (atraviesa todos los enemigos en su trayectoria sin perder daño).
8. **AC-8**: Magnetic Slide se activa con doble-tap WASD (ventana 0.25s) y desplaza al player 6m en esa dirección con 0.2s de invuln, respetando colisiones con paredes.
9. **AC-9**: Enemigos tocados por el player durante el slide reciben 2 dmg + knockback radial.
10. **AC-10**: Magnetic Slam **solo aparece como opción de upgrade** si Magnetic Slide ya fue adquirido.
11. **AC-11**: Magnetic Slam (tecla F) hace al player saltar (~0.4s invuln en aire), auto-targetea al enemigo más cercano dentro de 5m, y aterriza aplicando 5 dmg + knockdown 1.5s en AoE 4m.
12. **AC-12**: Si Slam se activa sin enemigos en rango, ejecuta el AoE en la posición actual del player.
13. **AC-13**: Iron Stride reduce la penalty de mobility por carga al 50% (verificable comparando playerSpeed con misma carga antes/después).
14. **AC-14**: El catálogo total son 10 upgrades. **No existen** upgrades de XP+, heal on kill, max HP+, ni sprint flat.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | Slide → Slam combo: ¿el Slam puede encadenarse inmediatamente después de un Slide (sin cooldown propio del slam reseteado)? Combo natural muy satisfactorio, pero podría romper pacing. | cris | Post-playtest |
| Q2 | ¿Magnetic Slide debería romper la órbita o mantenerla? Mantenerla = más fluido, romperla = trade-off táctico. | cris | Post-playtest |
| Q3 | ¿Wrecking Core debería tener un cap de instancias en arena (no en órbita) para no llenar el spawn pool? | cris | Implementación |
| Q4 | ¿Magnetic Chain debería tener VFX persistente que muestre el "potencial" de cadena (líneas tenues entre magnetizados cercanos) para incentivar setups? | cris | Post-playtest |
| Q5 | ¿Magnetic Slam debería tener un segundo stack que reduzca cooldown (5s → 4s)? Reservado como buffer si la habilidad se siente subusada. | cris | Post-playtest |
| Q6 | ¿Conviene un upgrade "Counter Mastery" (counter window +0.15s) si el counter resulta clave en playtests? Reservada como 11º slot buffer. | cris | Post-playtest |
