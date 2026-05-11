# Enemy System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Presión constante — los enemigos son la razón por la que el jugador necesita el magnetismo

## Overview

El Enemy System define los tipos de enemigos, su AI de combate estilo Arkham (approach/strafe/attack/retreat), el sistema de marca magnética (`IMarkable`), la interacción con magnetismo (atracción de enemigos magnetizados), y el Attack Director que coordina quién ataca cuándo. Es el sistema Content de Layer 2 que da propósito a todo lo demás: sin enemigos no hay combate, sin combate no hay magnetismo.

Hoy está implementado como `ArkhamEnemy` (985 líneas) + `ArkhamEnemyManager` (172 líneas). El enemy tiene: CharacterController, CombatHealth, marca magnética (Normal→Marked→Magnetized→Stunned), AI por coroutines (strafe, approach, attack, retreat), y repulsión magnética. El Manager mantiene el registro y corre el Attack Director.

## Player Fantasy

**"Los enemigos son mi munición."**

La fantasía no es solo "matar robots" — es que cada enemigo es una oportunidad de combo. Un Scrapling marca 1 → marca 2 → magnetizado → atraído → repelido contra otros → wall slam. El enemigo pasa de ser amenaza a ser herramienta. Los enemigos metálicos son permanentemente atraíbles — son "super munición" que siempre está disponible para un repel demoledor.

Referencia: **Batman Arkham** (enemigos que atacan de a uno, counter-based), **Hades** (variedad de archetypes que fuerzan diferentes tácticas), **Katamari Damacy** (enemigos como recurso que acumular).

## Detailed Design

### Core Rules

#### Regla 1 — Tipos de enemigo MVP

| Tipo | HP | Velocidad | Masa | Marca | Comportamiento |
|---|---|---|---|---|---|
| **Scrapling** | 3 | approach 5, strafe 1.25 | 3 | 2 marks → magnetizado | Horda, ataca en melee, fácil de matar |
| **Metal Enemy** | 5 | approach 4, strafe 1 | 2.2 | Always pullable | Siempre atraíble, más HP, premio magnético |
| **Runner Bot** | 4 | approach 7, strafe 0 | 4 | 2 marks → magnetizado | Carga lineal con aviso, no strafea |
| **Heavy Bot** | 8 | approach 3, strafe 0.8 | 5 | 3 marks → magnetizado | Lento, mucho HP, knockback alto |

Post-MVP: Tank (escudo frontal), Bomber (explota al morir), Flyer (inmune a ground attacks).

#### Regla 2 — Marca magnética (IMarkable)

El sistema de marca es el puente entre `combat-system` y `magnetism-system`:

```
Normal ──[Strike hit]──▶ Marked (1 stack)
Marked ──[Strike hit]──▶ Magnetized (2+ stacks)
Magnetized ──[Pull]──▶ Attracted → repelable como projectile
Any ──[Counter]──▶ Stunned → decay → Normal
```

| Estado | Efecto | Visual |
|---|---|---|
| **Normal** | Comportamiento estándar | Sin indicador |
| **Marked** | 1 stack. Próximo hit magnetiza | Leve glow amarillo |
| **Magnetized** | Atraíble por Pull. AI pausada durante atracción | Glow amarillo fuerte + indicador |
| **Stunned** | Post-counter. No se mueve, no ataca | Estrellas/chispas sobre cabeza |

Las marcas decaen después de `markDecayTime = 6s`. Si no se aplica segundo hit a tiempo, la marca se pierde.

#### Regla 3 — AI de combate: Attack Director

El `ArkhamEnemyManager` corre un Attack Director que coordina ataques:

1. Espera `attackDelayRange` (0.65–1.5s) aleatorio.
2. Selecciona un enemigo disponible (`CanDirectorSelect`), evitando repetir el anterior.
3. El enemigo elegido ejecuta `AttackRoutine()`.
4. Director espera a que el ataque termine (hit, counter, o muerte).
5. Si el enemigo sobrevive, ejecuta `RetreatRoutine()`.
6. Loop.

**Solo 1 enemigo ataca a la vez** (Arkham-style). Los demás strafean alrededor del player.

#### Regla 4 — AI de movimiento: Strafe + Approach

Enemigos no atacando ejecutan un loop de idle movement:
1. **Strafe** izquierda/derecha alrededor del player (velocidad 1.25 m/s).
2. Alternan dirección cada 1.5–3.5s.
3. Si están lejos del player, **Approach** (velocidad 5 m/s) hasta rango de strafe.

El enemy siempre mira al player (`FacePlayer()` en Update).

#### Regla 5 — Attack Routine

```
PrepareAttack (0.35s) → Show counter cue → Approach player
→ In range? → Attack hit (0.2s delay) → Apply damage → Recovery (0.55s)
→ Director calls BeginRetreat()
```

Durante `PrepareAttack`, el counter cue (esfera brillante sobre la cabeza) se muestra. El jugador puede Counter en esta ventana. Si counterea: el ataque se cancela, enemigo queda Stunned.

#### Regla 6 — Repulsión magnética

Un enemigo magnetizado puede ser atraído por Pull y luego repelido como "proyectil viviente":
- Durante atracción: `isMagneticallyControlled = true`, AI pausada.
- Al repeler: el enemigo vuela en la dirección del repel.
- Si impacta a otro enemigo: ambos reciben `magnetizedEnemyDamage` (5).
- Si impacta una pared: `wallSlamDamage` del `arena-system`.
- Después de la repulsión: marca se resetea a Normal.

#### Regla 7 — Muerte y despawn

Al llegar a 0 HP:
1. `isDead = true`, AI se detiene.
2. Animator trigger "Death".
3. Evento `OnDeath` (para scoring, wave-director).
4. Después de `deathDespawnDelay` (0.8s): `Pool.Despawn(gameObject, delay)`.

### States and Transitions

```
         ┌─── Director selects ───┐
         ▼                        │
  Idle/Strafe ──▶ PrepareAttack ──▶ Attacking ──▶ Retreat ──▶ Idle/Strafe
      │                │                              
      │           [Countered]                         
      │                ▼                              
      │            Stunned ─── decay ──▶ Idle/Strafe  
      │                                               
      ├── [Magnetized + Pulled] ──▶ MagControlled ──▶ Repelled ──▶ Idle or Dead
      │                                               
      └── [HP = 0] ──▶ Dead ──▶ Despawn              
```

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `damage-health-system` | **upstream** | HP, TakeDamage, IsAlive, death events | `CombatHealth` component |
| `arena-system` | **upstream** | Paredes para wall slam, spawn points | Colliders ArenaWall, `ArenaSystem` API |
| `object-pooling` | **upstream** | Spawn/Despawn de instancias | `Pool.Spawn/Despawn` |
| `player-movement` | **upstream** | Player position para approach/strafe/facing | Lee `player.transform.position` |
| `magnetism-system` | **upstream** | Pull de enemigos magnetizados, repulsión | `IMarkable` interface, magnetic pull API |
| `combat-system` | **downstream** | `IsCounterable`, `IsAttackable`, target scanning | Propiedades públicas |
| `combat-system` | **upstream** caller | `CounteredBy()`, `ReceiveStrikeDamage()` | Métodos públicos |
| `wave-director` | **upstream** caller | Spawning, registro en manager | `Pool.Spawn()` + `manager.Register()` |
| `scoring-xp-system` | **downstream** | `OnDeath` event para XP/combo | UnityEvent |
| `presentation-system` | **downstream** | `OnDamaged`, `OnCountered`, `OnMagnetized` para VFX/SFX | UnityEvents |

## Formulas

### Marca magnética → Magnetización
```
if (magneticMarks >= magneticMarksToMagnetize)
    markState = Magnetized
```

| Enemy Type | Marks to Magnetize | Time to Magnetize (2 strikes) |
|---|---|---|
| Scrapling | 2 | ~1.6s (2 attack routines) |
| Metal Enemy | N/A (always pullable) | Instant |
| Runner Bot | 2 | ~1.6s |
| Heavy Bot | 3 | ~2.4s (3 attack routines) |

### Damage al impactar como proyectil
```
magnetizedEnemyDamage = 5 (flat)
// Applies to: target enemy AND repelled enemy
```

### Attack Director Timing
```
delayBetweenAttacks = Random(0.65, 1.5) seconds
// At 8 enemies alive: effective DPS pressure ≈ 1 attack every ~1s
```

## Edge Cases

### E1 — Enemigo seleccionado para ataque muere antes de atacar
**Resolución:** el `WaitUntil` del Director detecta `enemy == null || !enemy.IsAlive` y sale del ciclo. Selecciona otro.

### E2 — Todos los enemigos están stunned/magnetized
**Resolución:** `RandomAvailableEnemy()` retorna null. Director espera 0.25s y reintenta.

### E3 — Enemigo repelido contra otro enemigo magnetizado
**Resolución:** ambos reciben daño. El segundo enemigo NO se convierte en proyectil — solo recibe knockback. Chain combos de repulsión no son automáticos (serían OP).

### E4 — Marca decae justo cuando el player lanza segundo strike
**Resolución:** hay un buffer de 0.2s en el decay check. Si el strike llega dentro del buffer, la marca se aplica antes del decay. Esto es generous-to-player por diseño.

### E5 — Metal Enemy muere mientras es atraído
**Resolución:** `isMagneticallyControlled` se resetea, el cadáver se suelta. El `magnetism-system` detecta `!IsAlive` y remueve de la lista de atraídos.

### E6 — Spawn de enemigo encima del player
**Resolución:** `arena-system.GetSpawnPointAwayFromPlayer()` garantiza distancia mínima de 8m.

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `damage-health-system` | **Hard** — HP, muerte |
| `arena-system` | **Hard** — spawn points, wall slam |
| `object-pooling` | **Hard** — spawn/despawn |
| `player-movement` | **Hard** — posición del player |
| `magnetism-system` | **Hard** — marca magnética, pull/repel |

### Downstream
| Sistema | Tipo |
|---|---|
| `combat-system` | **Hard** — target scanning, counter, strike |
| `wave-director` | **Hard** — spawning por oleada |
| `scoring-xp-system` | **Soft** — death events para XP |
| `boss-system` | **Soft** — extiende enemy con patrones especiales |

## Tuning Knobs

| Knob | Default | Rango | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `maxHealth` | 3–8 | 1–15 | Mueren muy fácil, sin tensión | Esponjas de daño, frustración |
| `attackDelayRange` | 0.65–1.5s | 0.3–3.0s | Ataques muy frecuentes, overwhelm | Ataques raros, jugador aburrido |
| `magneticMarksToMagnetize` | 2–3 | 1–5 | Magnetiza con 1 hit, demasiado fácil | Nunca magnetiza, loop roto |
| `markDecayTime` | 6s | 3–10s | Marcas decaen rápido, difícil magnetizar | Marcas persisten mucho, trivializa |
| `approachSpeed` | 3–7 | 2–10 | Enemigos nunca llegan, sin presión | Enemigos encima instantáneamente |
| `magneticMass` | 2.2–5 | 1–8 | Se atraen muy rápido | Se atraen muy lento |
| `magnetizedEnemyDamage` | 5 | 2–8 | Repel de enemigo no vale la pena | Repel de enemigo one-shots todo |

## Visual/Audio Requirements

- **Counter cue**: esfera brillante cyan sobre la cabeza durante `PrepareAttack`. Debe ser muy visible.
- **Magnetized indicator**: glow amarillo pulsante cuando `markState == Magnetized`.
- **Hit flash**: el material del enemigo flashea blanco por 0.1s al recibir daño.
- **Death**: animación de "colapso" + partículas de debris. El cadáver persiste 0.8s antes de despawn.
- **Repel como proyectil**: trail visible durante el vuelo. Sonido de impacto al golpear otro enemigo/pared.

## UI Requirements

- **World-space health bar**: barra flotante sobre cada enemigo (ya implementada en `WorldSpaceHealthBar`).
- **Magnetic mark indicator**: iconos sobre el enemigo mostrando stacks de marca (1 punto, 2 puntos, glow full).
- Sin HUD elements — toda la info del enemigo es worldspace.

## Acceptance Criteria

1. **AC-1**: Scrapling (3HP) muere en 2 LightScrap hits o 1 Plate hit.
2. **AC-2**: Metal Enemy es siempre atraíble por Pull sin necesitar marks.
3. **AC-3**: 2 strikes sobre un Scrapling lo magnetizan. Pull lo atrae. Repel lo lanza como proyectil.
4. **AC-4**: Attack Director solo permite 1 enemigo atacando a la vez.
5. **AC-5**: Counter durante PrepareAttack cancela el ataque y stunsea al enemigo.
6. **AC-6**: Enemigo repelido contra pared recibe `wallSlamDamage` adicional.
7. **AC-7**: Enemigo repelido contra otro enemigo: ambos reciben `magnetizedEnemyDamage`.
8. **AC-8**: Las marcas magnéticas decaen después de 6s sin nuevo strike.
9. **AC-9**: `Pool.Despawn()` reemplaza `Destroy(gameObject, delay)` en Die().
10. **AC-10**: Health bar worldspace se muestra correctamente y hace billboard hacia la cámara.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Agregar un "Runner Bot" que carga lineal con aviso visual (línea roja en el suelo)? Agrega variedad de threats y obliga al player a esquivar lateralmente. | cris | Pre-wave-director |
| Q2 | ¿Enemigos deberían soltar chatarra al morir? Crearía un loop de munición auto-sostenible. Riesgo: el player nunca se queda sin ammo. | cris | Post-playtest |
| Q3 | ¿El daño de repulsión magnética (5) debería escalar con la velocidad de repel? Más velocidad = más daño. Haría que la posición del player (cerca de pared = más recorrido) importe más. | cris | Post-playtest |
| Q4 | ¿Implementar "aggro" system donde enemigos priorizan al player que los marcó? Crearía más predictibilidad táctica. | cris | Post-MVP |
