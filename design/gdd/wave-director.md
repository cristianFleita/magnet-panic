# Wave Director

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Presion escalante - la arena genera amenazas legibles, municion magnetica cerca del jugador y oleadas cada vez mas intensas.

## Overview

El Wave Director controla la run dinamica: spawnea enemigos, chatarra y curaciones dentro de una arena cerrada de estilo sci-fi industrial. La arena de referencia tiene un centro tecnico, paredes perimetrales, zonas de chatarra y **4 puertas principales** desde donde entran los enemigos. La fantasia es que la base esta siendo invadida: las puertas anuncian peligro, la chatarra aparece como municion tactica cerca del jugador, y las curaciones se vuelven pequenos objetivos de posicionamiento.

El objetivo inmediato no es arte final, sino una **vertical slice gris y dinamica**: una escena casi vacia donde el sistema cree arena, player, enemigos, chatarra y pickups sin depender de objetos puestos a mano.

Documento complementario: [combat-vertical-slice-plan.md](combat-vertical-slice-plan.md).

## Player Fantasy

**"Me cierran desde las cuatro puertas, pero el mapa me da metal para responder."**

El jugador debe leer de donde viene la proxima amenaza, moverse para no quedar encerrado, atraer chatarra cercana y convertir el ataque enemigo en una oportunidad. Las oleadas no deben sentirse como una lista de spawns invisibles; deben sentirse como una base que se activa: puertas, luces, alarmas, entradas, respiraciones cortas y picos de caos.

## Arena Layout

### Forma Base

La arena MVP se inspira en la imagen de referencia:

- Rectangulo/square top-down con paredes perimetrales.
- 4 puertas cardinales: Norte, Sur, Este y Oeste.
- Centro visual fuerte: reactor/nucleo/maquina central.
- Esquinas y laterales con props industriales: barreras bajas, pilas de tubos, cajas, generadores.
- Pads visuales para pickups y objetivos secundarios.

Para el primer playable dinamico, los props interiores deben ser mayormente **decorativos o de colision simple**. La prioridad es que el combate no se atasque. Si se agregan obstaculos, deben dejar carriles claros desde las puertas hacia el centro.

### Puertas

Cada puerta es un punto de entrada de enemigos:

| Puerta | Rol de gameplay | Uso temprano | Uso avanzado |
|---|---|---|---|
| Norte | Presion frontal | Oleadas simples | Enemigos pesados o mini-boss |
| Sur | Presion de retaguardia | Refuerzos | Oleadas sorpresa |
| Este | Flanqueo lateral | Acto 2+ | Runners |
| Oeste | Flanqueo lateral | Acto 2+ | Mezcla o pinza |

Cada puerta debe tener:

- `doorId`
- `exitTransform`
- `spawnQueueTransform` opcional fuera de la pared
- `warningLight` opcional
- `spawnRadius`
- `cooldown`
- `enabled`

Los enemigos **no aparecen en cualquier punto del borde**. Salen de una puerta, con un aviso corto, para que el jugador pueda aprender el espacio.

## Core Rules

### Regla 1 - Run por actos, oleadas por presupuesto

La dificultad escala por actos, pero cada oleada se arma por **presupuesto de amenaza**, no solo por cantidad. Esto evita que un Heavy valga lo mismo que un Scrapling.

| Acto | Tiempo | Presupuesto/oleada | Puertas activas | Enemigos habilitados |
|---|---:|---:|---:|---|
| Acto 1 | 0:00-1:30 | 3-5 | 1-2 | Scrapling |
| Acto 2 | 1:30-3:30 | 6-9 | 2 | Scrapling, Metal Enemy |
| Acto 3 | 3:30-6:00 | 10-14 | 2-3 | + Runner Bot |
| Acto 4 | 6:00-9:00 | 15-20 | 3-4 | + Heavy Bot |
| Acto 5+ | 9:00+ | 20-28 cap | 4 | Todos |

Costos sugeridos:

| Enemigo | Threat cost |
|---|---:|
| Scrapling | 1 |
| Metal Enemy | 2 |
| Runner Bot | 3 |
| Heavy Bot | 4 |
| Mini-boss | Oleada especial |

### Regla 2 - Refuerzos por umbral

La siguiente oleada puede empezar cuando quedan pocos enemigos vivos:

```csharp
if (AliveEnemyCount <= reinforcementThreshold && restTimerDone)
    SpawnNextWave();
```

Defaults:

- `reinforcementThreshold`: 2
- `baseRest`: 2.0 s
- `minRest`: 0.55 s
- `maxEnemiesAlive`: 18 en WebGL MVP

Esto evita downtime por perseguir al ultimo enemigo, pero el cap impide que la pantalla se vuelva ilegible.

### Regla 3 - Seleccion de puertas

Las puertas se eligen con reglas de legibilidad:

1. Acto 1 usa una puerta principal y ocasionalmente una segunda.
2. Acto 2 introduce pinzas suaves: dos puertas no opuestas todo el tiempo.
3. Acto 3+ permite opuestas para obligar al jugador a reposicionarse.
4. No repetir la misma puerta mas de `maxSameDoorStreak = 2`.
5. Si el jugador campea una puerta, esa puerta baja prioridad y sube la puerta opuesta.
6. Cada spawn tiene aviso de `doorWarningTime = 0.6-1.0 s` con luz/audio antes de aparecer.

### Regla 4 - Spawn de enemigos desde puertas

Al spawnear una oleada:

1. Se eligen 1-4 puertas segun acto.
2. Se reparte el presupuesto entre puertas.
3. Cada puerta crea una cola corta de enemigos, con `spawnInterval = 0.2-0.45 s`.
4. Los enemigos salen mirando hacia el centro o hacia el jugador.
5. El Attack Director registra cada enemigo al aparecer.

Anti-injusticia:

- No spawnear si el player esta a menos de `doorPlayerMinDistance = 6 m`, salvo que sea la unica puerta disponible.
- Si todas las puertas estan cerca, usar la puerta mas lejana y aplicar warning mas largo.
- Los enemigos nuevos tienen una breve ventana de entrada antes de atacar, para que no golpeen en el frame 1.

### Regla 5 - Chatarra cerca del jugador, no por collider fijo

La chatarra no usa spawn colliders predefinidos. El director genera posiciones dinamicas en un anillo alrededor del jugador, siempre dentro del mapa.

Reglas de sampling:

```csharp
Vector3 candidate = playerPos + Random.insideUnitCircle.normalized * Random.Range(minRadius, maxRadius);
candidate = Arena.ClampToArena(candidate);
if (IsValidScrapPoint(candidate))
    SpawnScrap(candidate);
```

Defaults:

| Knob | Valor |
|---|---:|
| `scrapNearMinRadius` | 4 m |
| `scrapNearMaxRadius` | 10 m |
| `scrapPlayerMinDistance` | 2.5 m |
| `scrapDoorMinDistance` | 3 m |
| `scrapEnemyMinDistance` | 1.5 m |
| `scrapMaxActive` | 14 |
| `scrapAmmoFloorNearPlayer` | 4 |

El director debe mantener un **ammo floor**: si hay poca chatarra activa cerca del jugador, spawnea 2-4 piezas nuevas en posiciones validas. Esto protege el core del juego: Pull/Repel siempre tiene combustible, pero el jugador igual debe moverse para alcanzarlo.

Tipos por acto:

| Acto | LightScrap | Plate | Mine | Heavy |
|---|---:|---:|---:|---:|
| 1 | Alta | Baja | No | No |
| 2 | Alta | Media | Baja | No |
| 3 | Media | Media | Baja | Baja |
| 4+ | Media | Media | Media | Baja |

### Regla 6 - Curaciones por spawn points

Por ahora las curaciones usan spawn points definidos en arena:

- 4 pads de curacion, preferentemente cerca de cuadrantes o laterales.
- Maximo `1` pickup activo al mismo tiempo en MVP.
- Cooldown base: `healingSpawnCooldown = 25-35 s`.
- Spawn preferido cuando el player esta a `HP <= 50%`.
- Si el player esta full HP, no se spawnea curacion nueva.
- Si el pickup queda sin tomar por mucho tiempo, puede persistir; no hace falta reciclarlo agresivamente.

Esto convierte la curacion en decision de posicionamiento: salir del centro para curarse puede abrir una ventana de riesgo.

### Regla 7 - Mini eventos de oleada

Para la jam, conviene sumar variedad sin construir sistemas enormes:

| Evento | Frecuencia | Efecto |
|---|---|---|
| Scrap Burst | cada 3-4 oleadas | Spawnea chatarra extra cerca del jugador |
| Door Surge | acto 2+ | Una puerta spawnea 3-5 Scraplings rapido |
| Pinza | acto 3+ | Dos puertas opuestas con warning simultaneo |
| Heavy Arrival | acto 4+ | Una puerta anuncia Heavy con warning mas largo |

Estos eventos hacen que la run cambie de textura sin necesitar arte nuevo.

## States and Transitions

```
Idle
  -> Bootstrapping
  -> Warmup
  -> WaveActive
  -> Rest
  -> WaveActive
  -> ActTransition
  -> MiniBoss optional
  -> WaveActive
  -> Stopped
```

| Estado | Responsabilidad |
|---|---|
| `Idle` | Espera start de run |
| `Bootstrapping` | Crea arena, player, managers, pools y referencias |
| `Warmup` | 2-3 s iniciales para leer arena |
| `WaveActive` | Spawnea puertas, chatarra y refuerzos |
| `Rest` | Respiracion corta y posible curacion |
| `ActTransition` | Sube dificultad, activa puertas/tipos nuevos |
| `MiniBoss` | Opcional; pausa oleadas normales |
| `Stopped` | Player muerto o run terminada |

## Interactions with Other Systems

| Sistema | Datos que usa/provee |
|---|---|
| `arena-system` | Bounds, puertas, pickup points, validacion de posiciones |
| `enemy-system` | Prefabs, registro en `ArkhamEnemyManager`, eventos de muerte |
| `attractables-system` | Prefabs de chatarra, estados reseteables por pooling |
| `damage-health-system` | HP del player para curaciones y fin de run |
| `object-pooling` | Spawn/despawn de enemigos, chatarra, pickups y VFX |
| `combat-system` | Eventos de kill/counter/hit para tuning futuro de presion |
| `magnetism-system` | Capacidad/carga actual para ajustar ammo floor |
| `hud-system` | Wave, acto, warning de puerta, estado de run |
| `presentation-system` | Luces de puerta, alarmas, spawn VFX, shake |
| `meta-flow-system` | StartRun, PlayerDeath, RestartRun |

## Formulas

### Threat budget

```
budget = baseBudgetByAct + floor(waveIndex * waveBudgetGrowth)
budget = min(budget, maxBudgetByAct)
```

### Door weight

```
weight = distanceToPlayer
       + oppositeDoorBonus
       + underusedDoorBonus
       - sameDoorPenalty
       - playerCampingPenalty
```

### Scrap ammo floor

```
if (ActiveScrapNearPlayer < scrapAmmoFloorNearPlayer)
    SpawnScrap(scrapAmmoFloorNearPlayer - ActiveScrapNearPlayer + bonusByAct);
```

### Rest period

```
rest = max(minRest, baseRest - actIndex * restReduction)
```

## Edge Cases

### E1 - Player campea una puerta

La puerta campeada baja prioridad. El director favorece puerta opuesta o lateral y alarga el warning si no hay alternativa.

### E2 - La chatarra dinamica cae fuera del mapa

Todo candidato pasa por `Arena.ClampToArena()` y por `IsValidScrapPoint()`. Si falla N veces, se usa un fallback cerca del centro pero fuera del radio inmediato del jugador.

### E3 - No hay chatarra suficiente para jugar

El ammo floor ignora el ritmo de oleada y spawnea chatarra si el jugador esta sin municion cercana. No debe esperar a que termine la oleada.

### E4 - Demasiados enemigos activos

Si `AliveEnemyCount >= maxEnemiesAlive`, el director no spawnea enemigos aunque el presupuesto lo permita. Puede seguir spawneando chatarra.

### E5 - Curacion aparece cuando no sirve

Si el player esta full HP, no se crea pickup. Si ya hay pickup activo, no se crea otro.

### E6 - Spawn visible pero injusto

En esta arena las puertas son visibles por diseno. La justicia viene del warning: luz, audio y un delay corto antes de que el enemigo pueda atacar.

## Dependencies

### Upstream

| Sistema | Tipo |
|---|---|
| `arena-system` | Hard - puertas, bounds, pickup points |
| `object-pooling` | Hard - performance WebGL |
| `enemy-system` | Hard - prefabs, manager, eventos |
| `attractables-system` | Hard - prefabs de chatarra |
| `damage-health-system` | Hard - HP player, curacion, muerte |

### Downstream

| Sistema | Tipo |
|---|---|
| `hud-system` | Soft - muestra wave/act/warnings |
| `presentation-system` | Soft - feedback de puerta y spawn |
| `scoring-xp-system` | Soft - bonuses por wave/tiempo |
| `upgrade-system` | Soft - puede modificar spawn rates |

## Tuning Knobs

| Knob | Default | Rango | Riesgo si bajo | Riesgo si alto |
|---|---:|---:|---|---|
| `maxEnemiesAlive` | 18 | 10-24 | Poco caos | Ilegible/performance |
| `reinforcementThreshold` | 2 | 0-5 | Downtime | Solapamiento excesivo |
| `doorWarningTime` | 0.75 s | 0.4-1.5 | Spawn injusto | Ritmo lento |
| `spawnInterval` | 0.3 s | 0.1-0.7 | Masa instantanea | Puertas flojas |
| `scrapAmmoFloorNearPlayer` | 4 | 2-8 | Sin municion | Spam visual |
| `scrapNearMaxRadius` | 10 m | 6-14 | Chatarra encima | Chatarra irrelevante |
| `healingSpawnCooldown` | 30 s | 15-60 | Demasiada vida | Muy castigador |
| `doorPlayerMinDistance` | 6 m | 3-9 | Spawns encima | Puertas bloqueadas |

## Visual/Audio Requirements

- Puerta activa: luz roja/naranja, beep o alarma corta.
- Spawn enemigo: efecto breve de apertura/materializacion, no explosivo.
- Act transition: flash sutil del reactor central y texto corto.
- Scrap spawn: chatarra cae o aparece con pulso magnetico discreto.
- Healing pickup: pad azul/cyan encendido mientras el pickup esta activo.

## UI Requirements

- Wave actual y acto actual, discretos.
- Warning opcional de puerta si queda fuera del encuadre.
- Indicador simple cuando aparece curacion.
- Debug overlay para desarrollo: enemigos vivos, presupuesto, puerta elegida, chatarra cercana.

## Acceptance Criteria

1. La run puede iniciar en una escena vacia y crear player, arena, enemigos, chatarra y pickups dinamicamente.
2. Los enemigos spawnean desde 4 puertas, con warning visible antes de aparecer.
3. Las oleadas escalan por threat budget y no exceden `maxEnemiesAlive`.
4. La seleccion de puertas evita repetir siempre la misma entrada.
5. La chatarra spawnea cerca del jugador por sampling dinamico, no por colliders fijos.
6. La chatarra siempre queda dentro del mapa y a distancia valida del player/puertas/enemigos.
7. El director mantiene un ammo floor de chatarra cercana.
8. Las curaciones usan spawn points dedicados y respetan HP/cooldown/max activo.
9. La siguiente oleada entra al quedar pocos enemigos vivos o al terminar la anterior.
10. El sistema tiene knobs suficientes para balancear en menos de 5 minutos de playtest.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|
| Q1 | El reactor central debe tener collider real o ser solo landmark visual? Recomiendo empezar sin collider fuerte para no trabar combate. | cris | Primer playtest |
| Q2 | Las puertas deberian cerrarse/abrirse con animacion o basta warning de luz para la jam? | cris | Polish |
| Q3 | Los props de chatarra decorativa deben convertirse en attractables reales o mantenerse separados para legibilidad? | cris | Post-MVP |
| Q4 | Mini-boss sale siempre por puerta Norte o por la puerta menos usada? | cris | Cuando exista boss |
