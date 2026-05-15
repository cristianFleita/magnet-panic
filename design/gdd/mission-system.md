# Mission System

> **Status**: In Design (v2 — catálogo expandido con powerup tables)
> **Author**: cris + agents
> **Last Updated**: 2026-05-15
> **Implements Pillar**: Objetivos tácticos — las misiones guían al jugador hacia uso creativo del magnetismo y recompensan con picos de poder temporal

## Overview

El Mission System presenta objetivos tácticos de corta duración durante la run. Cada 45-60 segundos, una misión activa aparece. Completarla otorga XP bonus + un **powerup temporal aleatorio con pesos temáticos** según la misión completada. El jugador puede ignorar la misión sin penalización, pero completarla activa un pico de poder que cambia el ritmo del combate.

Las misiones cumplen tres funciones:
1. **Guiar** al jugador hacia mecánicas que no está usando.
2. **Crear micro-objetivos** que dan dirección al gameplay infinito.
3. **Alimentar el powerup-system** — son la **única fuente** de powerups temporales en el MVP.

El catálogo tiene **12 misiones** organizadas en **3 tiers de dificultad** que se desbloquean progresivamente por acto. Cada misión tiene una tabla de probabilidad de powerup propia, favoreciendo el powerup que mejor complementa la jugada que la misión pide.

No hay código existente — diseño nuevo desde cero.

## Player Fantasy

**"Tengo un objetivo — y la recompensa vale el riesgo."**

La fantasía es la del mercenario que acepta contratos en medio del caos. "3 wall slams en 30 segundos — ¿puedo hacerlo?" La misión crea un sub-juego dentro del juego, dando al jugador un foco táctico que transforma el combate genérico en un desafío específico. Y al completarla, el juego responde con un estallido de poder que amplifica la hazaña.

Referencia: **Fortnite** (challenges durante partida), **Risk of Rain 2** (shrines y challenges opcionales), **Deep Rock Galactic** (secondary objectives con bonus reward), **DMC** (style meter → reward).

## Detailed Design

### Core Rules

#### Regla 1 — Misión activa: 1 a la vez

Solo hay una misión activa en cada momento. Al completar o expirar, se espera `missionCooldown` (15-20s) antes de ofrecer la siguiente.

#### Regla 2 — Catálogo MVP (12 misiones, 3 tiers)

##### Tier 1 — Fundamentos (disponible desde Acto 1)

Misiones que enseñan los verbos básicos del juego. Accesibles sin upgrades ni dominio previo.

| # | Nombre | Objetivo | Tiempo | XP | Dificultad |
|---|---|---|---|---|---|
| 1 | **Scrap Collector** | Atrae y orbita 6 piezas de chatarra simultáneamente | 40s | 40 | Fácil |
| 2 | **Combo Hunter** | Alcanza combo ×5 | 30s | 50 | Media |
| 3 | **Iron Rain** | Mata 3 enemigos con chatarra repelida (no strikes) | 35s | 50 | Media |
| 4 | **Counterstorm** | Countera 2 ataques enemigos | 40s | 45 | Media |

##### Tier 2 — Dominio (disponible desde Acto 2)

Misiones que exigen dominio de mecánicas avanzadas: wall slam, magnetización, hordas densas.

| # | Nombre | Objetivo | Tiempo | XP | Dificultad |
|---|---|---|---|---|---|
| 5 | **Wall Slam** | Mata 2 enemigos con wall slam | 35s | 55 | Difícil |
| 6 | **Magnet Maestro** | Magnetiza 3 enemigos (que lleguen a estado Magnetized) | 30s | 50 | Media |
| 7 | **No Hands** | Mata 4 enemigos solo con repel (sin strikes durante la misión) | 40s | 65 | Difícil |
| 8 | **Chain Reaction** | Mata 2+ enemigos con un solo repel de enemigo magnetizado | 35s | 60 | Difícil |

##### Tier 3 — Estilo (disponible desde Acto 3)

Misiones que fuerzan jugadas espectaculares, combinando múltiples mecánicas.

| # | Nombre | Objetivo | Tiempo | XP | Dificultad |
|---|---|---|---|---|---|
| 9 | **Overload Artist** | Llega a sobrecarga crítica y libera la explosión (sin morir) | 45s | 70 | Difícil |
| 10 | **Wrecking Ball** | Mata 3 enemigos con un solo repel (multi-kill en 1 acción) | 30s | 75 | Muy difícil |
| 11 | **Bullet Catcher** | Atrae 3 proyectiles enemigos (Spitter Drone) a tu órbita | 40s | 60 | Media-Difícil |
| 12 | **Living Ammo** | Usa un enemigo magnetizado como proyectil y mata a otro enemigo con él | 30s | 70 | Difícil |

#### Regla 3 — Desbloqueo por tier / acto

| Acto | Tiers disponibles | Pool de misiones |
|---|---|---|
| Acto 1 (0:00–1:30) | Tier 1 | 4 misiones |
| Acto 2 (1:30–3:30) | Tier 1 + 2 | 8 misiones |
| Acto 3+ (3:30+) | Tier 1 + 2 + 3 | 12 misiones |

#### Regla 4 — Tabla de probabilidad de powerup por misión

Cada misión tiene **pesos temáticos** para el powerup que otorga al completarse. El powerup más afín a la jugada requerida tiene mayor probabilidad, pero nunca es 100% — siempre hay chance de cualquiera de los 3.

**Powerups del pool:**
- **Slow Time** (ST) — control temporal, 8s
- **Overload Pulse** (OP) — defensivo AoE, 10s
- **Magnetic Mine** (MM) — trampa/setup magnético

##### Tier 1 — Powerup weights

| Misión | Slow Time | Overload Pulse | Magnetic Mine | Lógica temática |
|---|---|---|---|---|
| **Scrap Collector** | 20% | 30% | **50%** | Acumulaste chatarra → la mina complementa con AoE magnético para convertir todo en munición |
| **Combo Hunter** | **50%** | 30% | 20% | Combo alto → Slow Time extiende la ventana para seguir encadenando |
| **Iron Rain** | 30% | **40%** | 30% | Kills con repel → Overload Pulse da respiro para recargar chatarra |
| **Counterstorm** | **45%** | **45%** | 10% | Counters → tanto Slow Time (más ventanas de counter) como Pulse (separar horda) son útiles |

##### Tier 2 — Powerup weights

| Misión | Slow Time | Overload Pulse | Magnetic Mine | Lógica temática |
|---|---|---|---|---|
| **Wall Slam** | 30% | **50%** | 20% | Wall slams requieren posicionamiento → Pulse repele enemigos hacia paredes |
| **Magnet Maestro** | 25% | 25% | **50%** | Magnetizaste muchos → mina magnetiza una horda entera para multiplicar tu setup |
| **No Hands** | **50%** | 25% | 25% | Sin strikes → Slow Time te da tiempo para Pull/Repel sin presión |
| **Chain Reaction** | 20% | 30% | **50%** | Multi-kill con enemigo → mina magnetiza más enemigos para repetir la jugada |

##### Tier 3 — Powerup weights

| Misión | Slow Time | Overload Pulse | Magnetic Mine | Lógica temática |
|---|---|---|---|---|
| **Overload Artist** | 30% | **50%** | 20% | Post-overload estás vulnerable → Pulse da protección continua |
| **Wrecking Ball** | **45%** | 35% | 20% | Multi-kill en 1 repel → Slow Time para planear la siguiente jugada masiva |
| **Bullet Catcher** | **40%** | 20% | **40%** | Interceptaste proyectiles → Slow Time ayuda a interceptar más, Mina magnetiza al Spitter para acabarlo |
| **Living Ammo** | 20% | 20% | **60%** | Usaste enemigo como arma → mina magnetiza más enemigos para repetir la fantasía |

#### Regla 5 — Selección de misión

La próxima misión se elige del pool de tiers desbloqueados según el acto actual:

```csharp
MissionData SelectNextMission(MissionData previous, int currentAct)
{
    var pool = catalog.Where(m =>
        m.tier <= GetMaxTier(currentAct) &&
        m != previous
    ).ToList();

    // Bias hacia tiers más altos en actos avanzados
    if (currentAct >= 3)
    {
        var weighted = pool.SelectMany(m =>
            Enumerable.Repeat(m, m.tier) // tier 3 = 3x weight
        ).ToList();
        return weighted[Random.Range(0, weighted.Count)];
    }

    return pool[Random.Range(0, pool.Count)];
}

int GetMaxTier(int act) => act switch
{
    1 => 1,
    2 => 2,
    _ => 3
};
```

**Anti-repetición:** no se repite la misión anterior. En actos 3+, las misiones de tier alto aparecen con más frecuencia (peso = tier).

#### Regla 6 — Tracking de progreso

Cada misión tiene un `IMissionTracker` que se suscribe a eventos del sistema relevante:

| Misión | Evento tracked | Source system |
|---|---|---|
| Scrap Collector | `OnOrbitCountChanged(count)` | `magnetism-system` |
| Combo Hunter | `OnComboChanged(count)` | `scoring-xp-system` |
| Iron Rain | `OnKill(method)` where method == Repel | `scoring-xp-system` |
| Counterstorm | `OnCounterSuccessful` | `combat-system` |
| Wall Slam | `OnWallSlamKill` | `arena-system` + `enemy-system` |
| Magnet Maestro | `OnEnemyMagnetized` | `magnetism-system` |
| No Hands | `OnKill(method)` where method == Repel | `scoring-xp-system` |
| Chain Reaction | `OnMultiKillWithEnemyRepel(count)` | `scoring-xp-system` + `enemy-system` |
| Overload Artist | `OnOverloadExplosion` | `overload-system` |
| Wrecking Ball | `OnMultiKill(count, method)` where count ≥ 3 | `scoring-xp-system` |
| Bullet Catcher | `OnProjectileAttracted` | `magnetism-system` |
| Living Ammo | `OnKillWithMagnetizedEnemy` | `enemy-system` + `scoring-xp-system` |

#### Regla 7 — Expiración

Si el timer llega a 0 sin completar la misión:
- La misión desaparece del HUD.
- Sin penalización.
- Cooldown de 15-20s antes de la siguiente.
- El progreso parcial se pierde.

#### Regla 8 — Recompensa inmediata

Al completar:
1. HUD muestra "MISSION COMPLETE!" con flash.
2. XP se suma inmediatamente.
3. Se elige powerup random según la tabla de pesos de esa misión.
4. El powerup se activa inmediatamente vía `powerup-system.ActivateWeightedPowerup(weights)`.
5. Banner: `"MISSION! → ¡{POWERUP_NAME}!"` por 1s.
6. Cooldown de 15-20s antes de la siguiente misión.

```csharp
void OnMissionComplete(MissionData mission)
{
    scoringXP.AddXP(mission.xpReward);
    
    PowerupType selected = SelectWeightedPowerup(mission.powerupWeights);
    powerupSystem.ActivatePowerup(selected);
    
    hud.ShowMissionBanner(mission.name, selected);
    StartCooldown();
}

PowerupType SelectWeightedPowerup(PowerupWeights w)
{
    float roll = Random.Range(0f, 1f);
    if (roll < w.slowTime) return PowerupType.SlowTime;
    if (roll < w.slowTime + w.overloadPulse) return PowerupType.OverloadPulse;
    return PowerupType.MagneticMine;
}
```

#### Regla 9 — Validación contextual de misiones

Algunas misiones requieren condiciones del juego para ser viables:

| Misión | Condición | Si no se cumple |
|---|---|---|
| Bullet Catcher | Hay Spitter Drones desbloqueados (acto 3+) | No entra al pool |
| Chain Reaction | Hay ≥3 enemigos vivos | Sigue en pool, puede expirar |
| Overload Artist | Player tiene ≥40% carga | Sigue en pool, jugador debe acumular |

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
| `scoring-xp-system` | **upstream** | Combo count, kill events, multi-kill | Eventos |
| `combat-system` | **upstream** | Counter events | `OnCounterSuccessful` |
| `magnetism-system` | **upstream** | Orbit count, magnetize events, projectile attract | `OnOrbitCountChanged`, `OnEnemyMagnetized`, `OnProjectileAttracted` |
| `arena-system` | **upstream** | Wall slam events | `OnWallSlamKill` |
| `enemy-system` | **upstream** | Kill method, magnetized enemy kills | `KillContext` |
| `overload-system` | **upstream** | Overload explosion events | `OnOverloadExplosion` |
| `wave-director` | **upstream** | Current act (para tier unlock) | `CurrentAct` |
| `scoring-xp-system` | **downstream** | XP reward | `AddXP(amount)` |
| `powerup-system` | **downstream** | Weighted powerup activation | `ActivateWeightedPowerup(weights)` |
| `hud-system` | **downstream** | Mission name, progress, timer, banner | Propiedades públicas |

## Formulas

### Mission cooldown
```
cooldown = Random(minCooldown, maxCooldown)
```

| Variable | Default | Rango |
|---|---|---|
| `minCooldown` | 15s | 10–30s |
| `maxCooldown` | 20s | 15–45s |

### XP reward scaling por acto (post-MVP)
```
adjustedReward = baseReward × (1 + currentAct × 0.1)
```

### Weighted powerup selection
```
roll = Random(0, 1)
if roll < w_slowTime → Slow Time
elif roll < w_slowTime + w_overloadPulse → Overload Pulse
else → Magnetic Mine
```

### Tier weight en selección (acto 3+)
```
effectiveWeight(mission) = mission.tier
P(mission) = mission.tier / Σ(all eligible missions' tiers)
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

### E6 — Bullet Catcher sin Spitter Drones en arena
**Resolución:** la Regla 9 filtra esta misión si no hay Spitters desbloqueados. Si hay Spitters desbloqueados pero ninguno vivo en ese momento, la misión queda en pool y puede expirar si no aparecen.

### E7 — Dos misiones con powerup activation en el mismo frame
**Resolución:** imposible por Regla 1 (solo 1 misión activa a la vez).

### E8 — Living Ammo: el enemigo magnetizado muere antes de impactar
**Resolución:** si el enemigo "proyectil" muere antes de impactar a otro, la kill no cuenta para la misión. El jugador debe completar el ciclo: magnetizar → repeler → impactar a otro enemigo vivo.

### E9 — Overload Artist: player explota por overload pero muere por daño externo en la ventana vulnerable
**Resolución:** si el player muere durante la ventana de vulnerabilidad post-overload (0.5s), la misión se completó (la explosión ocurrió) pero el player muere. La recompensa se aplica y el powerup se activa, pero inmediatamente se cancela por muerte (ver E2 de powerup-system).

### E10 — Misión completada justo cuando expira el timer
**Resolución:** si el evento de completion llega en el mismo frame que el timer llega a 0, el completion tiene prioridad. El powerup se otorga.

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `scoring-xp-system` | **Hard** — combo events, kill context |
| `combat-system` | **Hard** — counter events |
| `magnetism-system` | **Hard** — orbit count, magnetize, projectile attract |
| `overload-system` | **Hard** — overload explosion |
| `arena-system` | **Soft** — wall slam events |
| `enemy-system` | **Soft** — kill method, magnetized enemy kills |
| `wave-director` | **Soft** — current act for tier unlock |

### Downstream
| Sistema | Tipo |
|---|---|
| `scoring-xp-system` | **Soft** — XP reward |
| `powerup-system` | **Hard** — weighted powerup activation (única fuente de powerups) |
| `hud-system` | **Soft** — UI display |

## Tuning Knobs

| Knob | Default | Rango | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `missionDuration` (per mission) | 30–45s | 15–60s | Misión imposible de completar | Misión trivial, sin presión |
| `missionCooldown` | 15–20s | 5–45s | Misiones constantes, overwhelm | Misiones raras, pierde relevancia |
| `xpReward` (per mission) | 40–75 | 20–150 | No vale la pena hacerla | Rompe la curva de XP |
| `tierBiasWeight` | tier value | 1–5 | Sin bias, todas igual chance | Solo tier 3 en actos tardíos |
| `dominantPowerupWeight` | 40-60% | 30–80% | Powerup casi random | Siempre el mismo powerup |

## Visual/Audio Requirements

- **Mission appear**: slide-in desde la derecha con SFX de "radio transmission". Color del borde indica tier (blanco T1, amarillo T2, rojo T3).
- **Progress bar**: barra circular o segmentada debajo del nombre de misión.
- **Complete**: flash verde + "MISSION COMPLETE" + SFX de reward (chime ascendente) → transición inmediata al banner de powerup.
- **Expired**: fade out suave + SFX sutil de "misión perdida" (descending tone).
- **Tier indicator**: icono pequeño (★/★★/★★★) junto al nombre de misión.

## UI Requirements

### MVP
- **Mission card**: esquina superior derecha. Nombre + objetivo + progress (2/3) + timer circular.
- **Compact**: no más de 120×60px para no obstruir la vista.
- **Tier stars**: 1-3 estrellas junto al nombre para indicar dificultad.
- **Powerup banner**: al completar, transición fluida de "MISSION COMPLETE" → "¡{POWERUP}!" centrado.

## Acceptance Criteria

1. **AC-1**: Una misión se activa cada 45-60s de gameplay.
2. **AC-2**: Solo 1 misión activa a la vez.
3. **AC-3**: Completar la misión otorga XP + powerup inmediatamente.
4. **AC-4**: El timer de misión se pausa durante upgrade screen.
5. **AC-5**: Misión expirada no penaliza al jugador.
6. **AC-6**: Las 12 misiones se desbloquean correctamente por acto/tier.
7. **AC-7**: El powerup otorgado sigue los pesos temáticos de la misión (verificable con log de 100 completions).
8. **AC-8**: "No Hands" solo cuenta kills con repel (no strikes).
9. **AC-9**: "Bullet Catcher" no aparece si no hay Spitter Drones desbloqueados.
10. **AC-10**: No se repite la misión anterior.
11. **AC-11**: En acto 3+, misiones de tier alto aparecen con más frecuencia.
12. **AC-12**: "Living Ammo" solo cuenta si el enemigo repelido mata a otro enemigo al impactar.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Misiones elegibles? El player elige entre 2 misiones al inicio de cada ciclo. Agrega agency pero UI más compleja. | cris | Post-playtest |
| Q2 | ¿Difficulty scaling de misiones por acto? Ej: Combo Hunter pide ×5 en acto 1, ×8 en acto 3. | cris | Post-playtest |
| Q3 | ¿Chain missions? Completar 3 misiones seguidas = mega reward (ej: los 3 powerups a la vez por 5s). | cris | Post-MVP |
| Q4 | ¿El jugador debería poder elegir entre 2 powerups al completar misión, o el weighted random es mejor para mantener ritmo? | cris | Post-playtest |
| Q5 | ¿Agregar misiones específicas para upgrades adquiridos? Ej: "Slide Kill" solo si tiene Magnetic Slide. Aumenta rejugabilidad pero complica el pool. | cris | Post-MVP |
