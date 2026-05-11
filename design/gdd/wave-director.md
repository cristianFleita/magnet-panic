# Wave Director

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Presión escalante — la dificultad sube orgánicamente para que cada oleada se sienta más intensa que la anterior

## Overview

El Wave Director es el sistema de Encuentros que controla el flujo infinito de oleadas de enemigos. Define qué enemigos aparecen, cuántos, en qué orden, y cómo escala la dificultad a lo largo de la run. Es un "director invisible" que mantiene la presión constante sin que el jugador sienta que hay un algoritmo detrás — solo siente que "cada oleada es más difícil".

Hoy la funcionalidad está parcialmente en `ArkhamEnemyManager` (Attack Director) y `ArkhamCombatSetup` (spawn estático). No hay sistema de oleadas — los enemigos se colocan manualmente. Este GDD diseña el director completo con oleadas por actos, scaling de dificultad, y mini-bosses como hitos.

## Player Fantasy

**"Siempre hay más viniendo — y yo puedo con todos."**

La fantasía es la de la supervivencia heroica: cada oleada trae más caos, pero el jugador también se hace más fuerte (upgrades del `upgrade-system`). La curva de dificultad debe hacer que el jugador sienta que está "apenas sobreviviendo" todo el tiempo — ni aplastado ni aburrido. Las oleadas tempranas enseñan, las medias desafían, las tardías exigen dominio.

Referencia: **Vampire Survivors** (oleadas infinitas con escalado por minuto), **Hades** (encounters por room con variedad), **20 Minutes Till Dawn** (director que sube densidad gradualmente).

## Detailed Design

### Core Rules

#### Regla 1 — Estructura por Actos

La run se divide en Actos de duración fija. Cada acto escala la dificultad:

| Acto | Duración | Enemigos/oleada | Tipos | Evento |
|---|---|---|---|---|
| **Acto 1** | 0:00 – 1:30 | 3–5 | Scraplings only | Tutorial implícito |
| **Acto 2** | 1:30 – 3:30 | 5–8 | Scraplings + Metal Enemies | Variedad básica |
| **Acto 3** | 3:30 – 6:00 | 8–12 | + Runner Bots | Presión de velocidad |
| **Acto 4** | 6:00 – 9:00 | 10–15 | + Heavy Bots | Esponjas + horda |
| **Acto 5+** | 9:00+ | 12–20 (cap) | Todos, mezcla agresiva | Endgame, supervivencia |

Cada transición de acto incluye un mini-boss (ver `boss-system`).

#### Regla 2 — Oleadas dentro de cada Acto

Dentro de un acto, las oleadas se generan continuamente:

```csharp
while (actTimer > 0)
{
    WaveConfig wave = GenerateWave(currentAct, waveIndex);
    SpawnWave(wave);
    yield return WaitUntil(() => AliveEnemyCount() <= wave.reinforcementThreshold);
    yield return WaitForSeconds(wave.restPeriod);
    waveIndex++;
}
```

- **`reinforcementThreshold`**: cuando quedan N enemigos vivos, la siguiente oleada spawea. Default: 2. Esto evita tiempos muertos cazando al último enemigo.
- **`restPeriod`**: pausa entre oleadas. Default: 2s (Acto 1) → 0.5s (Acto 5+). La pausa se comprime con el acto.

#### Regla 3 — Composición de oleada

Cada oleada tiene una composición definida por el acto y un factor de variedad:

```csharp
WaveConfig GenerateWave(int act, int waveIndex)
{
    int totalEnemies = baseCount + (act - 1) * countPerAct + Mathf.FloorToInt(waveIndex * 0.5f);
    totalEnemies = Mathf.Min(totalEnemies, maxEnemiesAlive);
    
    // Distribución por tipo según acto
    float scraplingRatio = Mathf.Max(0.3f, 1f - act * 0.15f);
    // ... distribuir resto entre tipos disponibles
}
```

#### Regla 4 — Spawning espacial

Los enemigos spawnean en spawn points del `arena-system`:
- Distancia mínima del player: 8m (no spawnean encima).
- Se prefieren spawn points fuera del viewport de la cámara (no spawnean a la vista del jugador).
- Se distribuyen en 2-3 spawn points diferentes por oleada (no todos del mismo lado).

#### Regla 5 — Chatarra por oleada

Cada oleada spawea chatarra (`attractables-system`) como munición:
- Ratio: 1 attractable por cada 2 enemigos.
- Tipos: mayoría LightScrap, 1 Mine cada 3 oleadas, 1 Heavy cada 5 oleadas.
- Spawean en posiciones aleatorias dentro de la arena (no en spawn points de enemigos).

#### Regla 6 — Scaling de Attack Director

El `attackDelayRange` del `ArkhamEnemyManager` se comprime con el acto:

| Acto | Attack Delay Range | Efecto |
|---|---|---|
| 1 | 1.2 – 2.0s | Ataques lentos, aprendizaje |
| 2 | 0.9 – 1.5s | Ritmo normal |
| 3 | 0.7 – 1.2s | Presión |
| 4+ | 0.5 – 0.9s | Agresivo |

#### Regla 7 — Mini-boss como hito de acto

Al final de cada acto (antes de la transición), spawea un mini-boss (ver `boss-system`). La oleada se pausa hasta que el mini-boss muere. Esto crea un "pico de dificultad" satisfactorio que cierra el acto.

### States and Transitions

```
  Idle ──[RunStart]──▶ Act1 ──[timer]──▶ MiniBoss1 ──[boss dies]──▶ Act2 ──▶ ...
                                                                         
  Any ──[PlayerDeath]──▶ Stopped                                         
```

| Estado | Spawning | Attack Director | Descripción |
|---|---|---|---|
| **Idle** | No | No | Pre-run |
| **ActN** | Oleadas continuas | Activo, velocidad por acto | Gameplay normal |
| **MiniBoss** | Pausado (solo boss alive) | Activo (boss patterns) | Hito de acto |
| **Stopped** | No | No | Player muerto, fin de run |

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `enemy-system` | **downstream** | Spawn configs, enemy types | `Pool.Spawn(prefab)` + `enemy.Configure()` |
| `boss-system` | **downstream** | Trigger de mini-boss spawn | `SpawnBoss(bossConfig)` |
| `attractables-system` | **downstream** | Spawn de chatarra como munición | `Pool.Spawn(scrapPrefab)` |
| `arena-system` | **upstream** | Spawn points, arena bounds | `ArenaSystem.GetSpawnPoints()` |
| `object-pooling` | **upstream** | Pools pre-configurados | `Pool.Spawn/Despawn` |
| `scoring-xp-system` | **downstream** | Wave number para bonus | `OnWaveCompleted` event |
| `upgrade-system` | **downstream** | Trigger de level up (indirecto via XP) | Via `scoring-xp-system` |
| `meta-flow-system` | **upstream** | RunStart / PlayerDeath | State machine events |
| `camera-system` | **downstream** | Shake en transición de acto | `cameraRig.Shake()` |

## Formulas

### Enemigos por oleada
```
totalEnemies = baseCount + (act - 1) × countPerAct + floor(waveIndex × 0.5)
totalEnemies = min(totalEnemies, maxEnemiesAlive)
```

| Variable | Default | Rango |
|---|---|---|
| `baseCount` | 3 | 2–6 |
| `countPerAct` | 3 | 1–5 |
| `maxEnemiesAlive` | 20 | 10–30 |

### Rest period
```
restPeriod = max(0.5, baseRest - (act - 1) × restReduction)
```

| Variable | Default | Rango |
|---|---|---|
| `baseRest` | 2.0s | 1–4s |
| `restReduction` | 0.3s per act | 0.1–0.5s |

## Edge Cases

### E1 — Todos los enemigos mueren instantáneamente (overload AoE)
**Resolución:** `reinforcementThreshold` trigger spawns siguiente oleada. Rest period aún se respeta para dar un beat de respiración.

### E2 — Player ignora al mini-boss
**Resolución:** el mini-boss es el único enemigo durante su fase. Sin matarlo, no continúa. El boss persigue al player, así que ignorarlo no es viable.

### E3 — Pool agotado (demasiados enemigos vivos)
**Resolución:** `maxEnemiesAlive` cap impide spawning si ya hay 20 activos. El director espera.

### E4 — Spawn point visible por la cámara
**Resolución:** filtrar spawn points fuera del frustum de cámara. Si todos son visibles (arena chica), usar el más lejano al player con 0.3s de delay y VFX de "teleport in".

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `arena-system` | **Hard** — spawn points |
| `object-pooling` | **Hard** — spawn/despawn |
| `meta-flow-system` | **Hard** — RunStart/PlayerDeath |

### Downstream
| Sistema | Tipo |
|---|---|
| `enemy-system` | **Hard** — spawning de enemigos |
| `boss-system` | **Hard** — spawning de bosses |
| `attractables-system` | **Hard** — spawning de chatarra |
| `scoring-xp-system` | **Soft** — wave number bonus |

## Tuning Knobs

| Knob | Default | Rango | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `baseCount` | 3 | 2–6 | Oleadas vacías, aburridas | Overwhelm inmediato |
| `countPerAct` | 3 | 1–5 | Scaling lento, se siente estancado | Scaling brusco, spike de dificultad |
| `maxEnemiesAlive` | 20 | 10–30 | Pocas amenazas simultáneas | Performance risk en WebGL |
| `reinforcementThreshold` | 2 | 0–5 | Player debe matar todos antes de refuerzo | Oleadas se solapan, caos total |
| `baseRest` | 2.0s | 1–4s | Sin respiro | Demasiado downtime |

## Visual/Audio Requirements

- **Transición de acto**: flash sutil + texto "ACT 2" por 1.5s + shake leve.
- **Oleada nueva**: no hay indicador explícito (los enemigos spawnean y ya).
- **Mini-boss incoming**: alerta visual (borde rojo de pantalla) + SFX grave 1s antes del spawn.
- **Spawn VFX**: breve efecto de "materialización" al spawnear enemigos fuera de vista.

## UI Requirements

- **Wave counter**: "Wave 12" discreto en el HUD. No es central, es informativo.
- **Act indicator**: "ACT 2" aparece brevemente al inicio de cada acto.
- **Boss HP bar**: barra grande en la parte superior de pantalla durante mini-boss (responsabilidad del `hud-system`).

## Acceptance Criteria

1. **AC-1**: Las oleadas escalan correctamente: Acto 1 = 3-5 enemigos, Acto 4 = 10-15.
2. **AC-2**: La siguiente oleada spawea cuando quedan ≤ `reinforcementThreshold` enemigos vivos.
3. **AC-3**: Rest period entre oleadas se comprime por acto.
4. **AC-4**: Mini-boss spawea al final de cada acto. Oleada se pausa hasta que muere.
5. **AC-5**: Chatarra spawea como munición: 1 attractable por cada 2 enemigos.
6. **AC-6**: `maxEnemiesAlive` impide spawn si el cap está alcanzado.
7. **AC-7**: Enemigos NO spawnean dentro del viewport de la cámara ni a menos de 8m del player.
8. **AC-8**: Attack Director se comprime por acto (delay range baja).

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Eventos especiales entre actos? Ej: "Scrap Rain" (lluvia de chatarra bonus), "Overcharge" (carga empieza al 50%). Agrega variedad sin nuevos enemigos. | cris | Post-playtest |
| Q2 | ¿El jugador debería poder ver qué enemigos vienen en la próxima oleada? (preview). Agrega planificación pero rompe la sorpresa. | cris | Post-playtest |
| Q3 | ¿Scaling infinito o cap de dificultad? Si hay cap, ¿en qué acto? Si es infinito, ¿cuándo se vuelve imposible? | cris | Post-playtest |
