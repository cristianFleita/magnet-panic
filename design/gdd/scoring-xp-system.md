# Scoring & XP System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Progresión dentro de la run — cada kill y combo alimenta el ciclo de upgrades que hace al jugador más fuerte

## Overview

El Scoring & XP System rastrea la puntuación y experiencia del jugador durante una run. Cada acción de combate otorga Scrap XP que llena una barra de nivel. Al subir de nivel, el `upgrade-system` ofrece una elección de mejoras. El score final se envía al leaderboard al morir. Es el puente entre el combate momento-a-momento y la progresión run-a-run.

El jugador interactúa indirectamente: mata enemigos, hace combos, completa misiones, y ve la barra de XP subir. La decisión táctica es: ¿hago kills rápidos (menos XP cada uno) o busco combos de repel/wall slam (más XP pero más riesgo)?

No hay código existente — este sistema es diseño nuevo desde cero.

## Player Fantasy

**"Cada combo me hace más fuerte."**

La fantasía es la del poder acumulativo: cada kill bien ejecutado (repel, wall slam, multi-kill) da más XP que un kill básico. El jugador que domina las mecánicas sube de nivel más rápido y elige mejores upgrades, creando un ciclo virtuoso de skill → reward → power. El score al morir es el "trofeo" que valida la run.

Referencia: **Vampire Survivors** (XP drops + level up = upgrade choice), **Hades** (score por room con bonuses de estilo), **Devil May Cry** (style meter que recompensa variedad).

## Detailed Design

### Core Rules

#### Regla 1 — Fuentes de Scrap XP

| Fuente | XP Base | Multiplicador | Descripción |
|---|---|---|---|
| **Kill básico** (strike) | 10 | ×1.0 | Kill con golpe melee directo |
| **Kill con repel** | 10 | ×1.5 | Kill con chatarra repelida |
| **Kill con wall slam** | 10 | ×2.0 | Enemigo muere contra pared |
| **Kill con overload** | 10 | ×2.0 | Kill por explosión de overload |
| **Kill con enemy repel** | 10 | ×2.5 | Matar usando enemigo magnetizado como proyectil |
| **Multi-kill bonus** | 0 | +5 per extra | 2+ kills en 1s = bonus acumulativo |
| **Counter perfecto** | 15 | ×1.0 | Counter exitoso (no mata, pero da XP) |
| **Boss kill** | 100 | ×1.0 | Mini-boss derrotado |
| **Misión completada** | variable | ×1.0 | Del `mission-system` |
| **Oleada completada** | 5 × waveNumber | ×1.0 | Bonus al limpiar una oleada |

#### Regla 2 — Combo Meter

Las kills consecutivas dentro de una ventana de tiempo incrementan un combo counter:

```csharp
void OnEnemyKilled(KillContext ctx)
{
    comboCount++;
    comboTimer = comboWindow; // reset timer
    
    float comboMultiplier = 1f + (comboCount - 1) * comboScaling;
    int xp = Mathf.RoundToInt(ctx.baseXP * ctx.styleMultiplier * comboMultiplier);
    AddXP(xp);
}
```

| Variable | Default | Rango |
|---|---|---|
| `comboWindow` | 3.0s | 1.5–5.0s |
| `comboScaling` | 0.1 per combo | 0.05–0.2 |
| `maxComboMultiplier` | 3.0 | 2.0–5.0 |

Ejemplo: kill 5 con repel + combo ×4 = `10 × 1.5 × 1.3 = 20 XP` (redondeado).

El combo se resetea a 0 si `comboTimer` llega a 0 sin nueva kill.

#### Regla 3 — Level Up

La XP llena una barra. Al llenarla: level up → `upgrade-system` ofrece choice de 3.

```
xpToNextLevel = baseXPPerLevel + (currentLevel - 1) × xpGrowthPerLevel
```

| Variable | Default | Rango |
|---|---|---|
| `baseXPPerLevel` | 50 | 30–80 |
| `xpGrowthPerLevel` | 15 | 5–25 |

**Tabla de progresión:**

| Level | XP Needed | Cumulative | ~Kills needed (basic) |
|---|---|---|---|
| 1→2 | 50 | 50 | 5 |
| 2→3 | 65 | 115 | 12 |
| 3→4 | 80 | 195 | 20 |
| 5→6 | 110 | 420 | 42 |
| 10→11 | 185 | 1225 | 123 |

El crecimiento es lineal, no exponencial. Esto evita que los niveles altos se sientan inalcanzables.

#### Regla 4 — Score final

El score de la run es una métrica separada del XP (para leaderboard):

```
finalScore = totalXPEarned + survivalBonus + styleBonus
```

| Componente | Cálculo |
|---|---|
| `totalXPEarned` | Suma de todo el XP ganado en la run |
| `survivalBonus` | `survivalTime × 2` (2 puntos por segundo sobrevivido) |
| `styleBonus` | `maxComboReached × 10` (bonus por el combo más alto logrado) |

#### Regla 5 — XP drops visuales

Los enemigos NO dropean XP físico (no hay gems/orbs que recoger). El XP se otorga automáticamente al matar. Esto es intencional: el player no debe parar para recoger XP — el ritmo de combate es la prioridad.

El feedback es visual: número de XP que flota sobre el enemigo muerto con color según calidad (blanco = normal, amarillo = style bonus, dorado = combo alto).

#### Regla 6 — Pausa de gameplay durante level up

Al subir de nivel:
1. El gameplay se pausa (`Time.timeScale = 0`).
2. Se muestra el panel de upgrade choice (3 opciones).
3. El jugador elige.
4. Gameplay resume.

Esto evita que el jugador muera durante la selección de upgrade. La pausa es responsabilidad del `upgrade-system`, no de este sistema.

### States and Transitions

```
  Inactive ──[RunStart]──▶ Tracking ──[LevelUp]──▶ LevelUpPending ──[UpgradeChosen]──▶ Tracking
                                          │
                                     [PlayerDeath]
                                          ▼
                                     ScoreSubmit
```

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `enemy-system` | **upstream** | `OnDeath` event con kill context | UnityEvent |
| `combat-system` | **upstream** | Kill method (strike, counter) | `KillContext.method` |
| `magnetism-system` | **upstream** | Kill con repel, wall slam flag | `KillContext.wasRepel`, `KillContext.wasWallSlam` |
| `overload-system` | **upstream** | Kill por overload | `KillContext.wasOverload` |
| `boss-system` | **upstream** | Boss kill = bonus XP | `KillContext.isBoss` |
| `wave-director` | **upstream** | Wave completed event | `OnWaveCompleted(waveNumber)` |
| `mission-system` | **upstream** | Mission completed XP | `OnMissionCompleted(xpReward)` |
| `upgrade-system` | **downstream** | Level up trigger | `OnLevelUp` event |
| `hud-system` | **downstream** | XP bar, combo counter, score | Propiedades públicas: `CurrentXP`, `XPToNext`, `ComboCount`, `Score` |
| `meta-flow-system` | **downstream** | Score final al morir | `FinalScore` para death screen |
| `host-bridge` | **downstream** | Score submit al leaderboard | `RunEnded(score, stats)` |

## Formulas

### XP per kill
```
xp = baseXP × styleMultiplier × comboMultiplier
comboMultiplier = min(maxComboMult, 1 + (comboCount - 1) × comboScaling)
```

### XP to next level
```
xpToNextLevel = baseXPPerLevel + (currentLevel - 1) × xpGrowthPerLevel
```

### Final score
```
finalScore = totalXPEarned + (survivalTimeSeconds × 2) + (maxCombo × 10)
```

## Edge Cases

### E1 — Multi-kill: 5 enemigos mueren en 1 frame (overload)
**Resolución:** cada kill se procesa individualmente con combo incrementándose. Kill 1 = combo ×1, kill 5 = combo ×1.4. El multi-kill bonus se suma: `5 × 5 = 25 XP extra`.

### E2 — Level up durante boss fight
**Resolución:** la pausa aplica igual. El jugador elige upgrade. Esto puede ser estratégico (healing upgrade mid-boss).

### E3 — Combo timer en pausa (level up screen)
**Resolución:** como `Time.timeScale = 0`, el combo timer se congela. El combo sobrevive la pantalla de upgrade. Esto es generous-to-player.

### E4 — Player muere con XP parcial
**Resolución:** el XP parcial NO se pierde — se suma al score final. El level up no ocurre, pero el XP cuenta para el score.

### E5 — Kill method ambiguo (repel + wall slam + overload simultáneo)
**Resolución:** se usa el multiplicador más alto. NO se acumulan multiplicadores de estilo. `max(repelMult, wallSlamMult, overloadMult)`.

### E6 — Score overflow
**Resolución:** score es `long` (64-bit). Máximo teórico en una run de 30 min con combo perfecto: ~50,000. No hay riesgo de overflow.

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `enemy-system` | **Hard** — death events |
| `combat-system` | **Hard** — kill context (method, style) |
| `magnetism-system` | **Hard** — repel/wall slam flags |
| `wave-director` | **Soft** — wave bonus |
| `mission-system` | **Soft** — mission XP |

### Downstream
| Sistema | Tipo |
|---|---|
| `upgrade-system` | **Hard** — level up trigger |
| `hud-system` | **Soft** — XP bar, combo, score display |
| `meta-flow-system` | **Soft** — final score |
| `host-bridge` | **Soft** — leaderboard submit |

## Tuning Knobs

| Knob | Default | Rango | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `baseXPPerLevel` | 50 | 30–80 | Level ups muy frecuentes, upgrades sin esfuerzo | Level ups raros, player no siente progresión |
| `xpGrowthPerLevel` | 15 | 5–25 | Curva plana, siempre fácil subir | Curva empinada, niveles altos inalcanzables |
| `comboWindow` | 3.0s | 1.5–5.0s | Combos difíciles de mantener | Combos triviales, siempre activos |
| `comboScaling` | 0.1 | 0.05–0.2 | Combos casi no dan bonus | Combos altos = XP explosivo |
| `repelKillMultiplier` | 1.5 | 1.0–2.5 | No incentiva usar repel | Repel es la única forma viable de farmear |
| `wallSlamMultiplier` | 2.0 | 1.5–3.0 | Wall slams poco gratificantes | Wall slams trivializan la progresión |
| `bossKillXP` | 100 | 50–200 | Boss kill decepcionante | Boss kill = 2 level ups, broken |

## Visual/Audio Requirements

- **XP popup**: número flotante sobre enemigo muerto. Color por calidad (blanco→amarillo→dorado→rojo). Tamaño escala con amount.
- **Combo counter**: texto "×3" que crece con el combo. Shake al incrementar. Flash al resetear.
- **Level up**: flash dorado en pantalla + SFX ascendente + "LEVEL UP!" popup.
- **Score**: incrementa visiblemente en el HUD. No muestra decimal, solo entero.

## UI Requirements

### MVP
- **XP bar**: barra horizontal bajo el HP bar. Se llena de izquierda a derecha. Flash al completar.
- **Level indicator**: "Lv.3" junto a la XP bar.
- **Combo counter**: "×5" en la esquina, visible solo cuando combo > 1. Timer visual como ring que se vacía.
- **Score**: número en la esquina superior derecha. Incrementa en tiempo real.
- **Death screen**: score final desglosado (kills, style bonus, survival, combo max).

## Acceptance Criteria

1. **AC-1**: Kill básico otorga 10 XP. Kill con repel otorga 15 XP (×1.5).
2. **AC-2**: Wall slam kill otorga 20 XP (×2.0).
3. **AC-3**: Combo counter incrementa con cada kill dentro de `comboWindow`.
4. **AC-4**: Combo multiplier aplica correctamente: combo ×5 = ×1.4 multiplier.
5. **AC-5**: Combo se resetea a 0 cuando `comboTimer` expira.
6. **AC-6**: XP bar se llena y triggerea level up. XP to next level escala linealmente.
7. **AC-7**: Level up pausa gameplay y triggerea `upgrade-system`.
8. **AC-8**: Boss kill otorga 100 XP base.
9. **AC-9**: Score final = totalXP + survivalBonus + styleBonus. Se envía al leaderboard.
10. **AC-10**: Kill method ambiguo usa el multiplicador más alto (no acumula).

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Style meter visual (tipo DMC: D→C→B→A→S→SS→SSS)? Agrega feedback gratificante pero es redundante con el combo counter. ¿Vale la complejidad? | cris | Post-playtest |
| Q2 | ¿XP compartido entre score y level? Hoy XP = score. ¿Deberían ser independientes? (XP para level, score separado para leaderboard). Propongo mantener unificado para MVP. | cris | Post-playtest |
| Q3 | ¿Streak bonus? Kills sin recibir daño = multiplicador extra. Recompensa habilidad defensiva pero puede hacer el scoring snowball-y. | cris | Post-playtest |
| Q4 | ¿"Reciclaje" de chatarra como fuente de XP? Atraer chatarra y repelerla sin matar = XP mínimo (3 por pieza). Da XP por usar el magnetismo incluso sin kills. | cris | Pre-implementation |
