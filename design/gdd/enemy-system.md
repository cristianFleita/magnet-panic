# Enemy System

> **Status**: In Design → Implementation
> **Author**: cris + agents
> **Last Updated**: 2026-05-13
> **Implements Pillar**: Presión constante — los enemigos son la razón por la que el jugador necesita el magnetismo

## Overview

El Enemy System define los tipos de enemigos, su AI de combate estilo Arkham (approach/strafe/attack/retreat), el sistema de marca magnética (`IMarkable`), la interacción con magnetismo (atracción de enemigos magnetizados), y el Attack Director que coordina quién ataca cuándo. Es el sistema Content de Layer 2 que da propósito a todo lo demás: sin enemigos no hay combate, sin combate no hay magnetismo.

Hoy está implementado como `ArkhamEnemy` (~1500 líneas) + `ArkhamEnemyManager` (~200 líneas). El enemy tiene: CharacterController, CombatHealth, marca magnética (Normal→Marked→Magnetized→Stunned), AI por coroutines (strafe, approach, attack, retreat), repulsión magnética, y soporte para comportamientos modulares vía componentes add-on (`SpitterDroneBehavior`, `ScrapThiefBehavior`).

## Player Fantasy

**"Los enemigos son mi munición."**

La fantasía no es solo "matar robots" — es que cada enemigo es una oportunidad de combo. Un Scrapling marca 1 → marca 2 → magnetizado → atraído → repelido contra otros → wall slam. El enemigo pasa de ser amenaza a ser herramienta. Los enemigos metálicos son permanentemente atraíbles — son "super munición" que siempre está disponible para un repel demoledor.

**"Los enemigos roban mi munición."**

El ScrapThief agrega tensión extra: los enemigos pueden robar chatarra del suelo y lanzártela. Si no lo matás rápido, tu ammo se convierte en un arma en tu contra. Pero podés atraer el scrap de vuelta mid-air. (Estilo Resident Evil 4).

**"Sus proyectiles son mi munición."**

El Spitter Drone dispara proyectiles metálicos — pero son atraíbles. Pull para interceptarlos, Repel para devolverlos. Cada bala enemiga es munición potencial. (Referencia: GDD §8.4)

Referencia: **Batman Arkham** (enemigos que atacan de a uno/dos, counter-based), **Marvel's Spider-Man** (enemigos ranged + melee combinados, brutes que lanzan objetos), **Hades** (variedad de archetypes que fuerzan diferentes tácticas), **Katamari Damacy** (enemigos como recurso que acumular), **Resident Evil 4** (Ganados que lanzan objetos del entorno).

## Detailed Design

### Core Rules

#### Regla 1 — Tipos de enemigo MVP

| Tipo | HP | Velocidad | Masa | Marca | Comportamiento |
|---|---|---|---|---|---|
| **Scrapling** | 3 | approach 5, strafe 1.25 | 3 | 2 marks → magnetizado | Horda, ataca en melee, fácil de matar. **Counterable.** |
| **Metal Enemy** | 5 | approach 4, strafe 1 | 2.2 | Always pullable | Siempre atraíble, más HP, premio magnético. **Counterable.** |
| **Runner Bot** | 4 | approach 7, strafe 0 | 4 | 2 marks → magnetizado | **Mediana distancia:** orbita ~6.5m, carga lineal telegrafiada de 0.65s. Counterable, esquivable con dodge lateral. |
| **Heavy Bot** | 8 | approach 3, strafe 0.8 | 5 | 3 marks → magnetizado | Lento, mucho HP, knockback alto. **Puede agarrar scraps y lanzarlos.** ⚠ **No counterable** — sus ataques deben esquivarse con dodge. Cuesta 2 attack tokens. |
| **Spitter Drone** | 4 | approach 3.5, strafe 1.4 | 2.5 | 2 marks → magnetizado | **Ranged:** dispara proyectiles metálicos atraíbles. Se mantiene a distancia. Counterable. |

Post-MVP: Tank (escudo frontal), Bomber (explota al morir), Flyer (inmune a ground attacks).

#### Regla 1b — Comportamientos modulares add-on

Los comportamientos especiales se implementan como componentes Unity separados que coexisten con `ArkhamEnemy`:

| Componente | Qué hace | Quién lo usa |
|---|---|---|
| `SpitterDroneBehavior` | Reemplaza ataque melee por disparo de proyectiles con burst y aimInaccuracy | Spitter Drone |
| `ScrapThiefBehavior` | El enemigo agarra scraps del piso y los lanza al jugador | Heavy Bot (40% chance), cualquier archetype futuro |
| `EnemyProjectile` | Proyectil volador con Pool lifecycle, attractable vía `MagneticObject` | Creado por SpitterDrone |

Cada add-on es **opcional** — ArkhamEnemy los detecta con `GetComponent<>()` en Awake/Reset y delega solo si existen.

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

#### Regla 3 — AI de combate: Attack Director (queue + late-game double-team)

El `ArkhamEnemyManager` corre un Attack Director estilo Arkham/Spider-Man simplificado: **cola estricta de 1 a la vez** durante el opening, abre un segundo slot **solo cuando ya pasó tiempo de combate** para que el jugador tenga espacio para aprender los patrones.

1. Espera `attackDelayRange` (0.65–1.5s) con **reducción progresiva** por tiempo de run.
2. Calcula `targetAttackers` con dos puertas:
   - **Default: 1 attacker** (queue). El director sólo elige al siguiente cuando el actual terminó (hit, counter o muerte).
   - **Segunda slot:** se abre cuando `minutesElapsed ≥ secondAttackerAfterMinutes` (default 1.5min) **y** `aliveCount ≥ secondAttackerMinAlive` (default 4).
   - Cap absoluto: `maxSimultaneousAttackers = 2`.
3. Selecciona el primer enemigo disponible (`CanDirectorSelect`, ya respeta `spawnAttackGracePeriod`), evitando repetir el anterior.
4. **Scrap Thief check:** si el enemigo tiene `ScrapThiefBehavior`, primero intenta grab+throw.
5. **Spitter Drone check:** si el enemigo tiene `SpitterDroneBehavior`, ejecuta `RangedAttackRoutine`.
6. Si no, ejecuta `AttackRoutine()` o `LinearChargeRoutine()` normal.
7. **Stagger entre attackers:** entre cada attacker adicional el director espera `attackerStaggerRange` (0.18–0.45s) para que los windups no colapsen en el mismo frame.
8. Director espera a que **todos** los attackers terminen su ataque (hit, counter, o muerte).
9. Para cada enemigo sobreviviente ejecuta `RetreatRoutine()`.

**Threat-token cost:** los `EnemyDefinition` declaran `attackTokenCost` (1 por default, 2 para HeavyBot). El director cuenta slots, no costos crudos — pero el cost extra del Heavy indica que ese enemigo bloquea más espacio cognitivo en pantalla; el balance se hace bajando su frecuencia en `WaveDirector` cuando coexiste con otros archetypes.

**Spawn grace:** cada enemigo expone `spawnAttackGracePeriod` (default 1.2s, HeavyBot 1.4s, Spitter 1.5s). Mientras está dentro de la ventana de gracia post-spawn, `CanDirectorSelect` devuelve `false`. Esto elimina la sensación de "el bot apareció y me pegó en el mismo frame".

**Fairness rules (Spider-Man style):**
- Mientras el player está atacando o counter-eando, `GetScaledDelay()` suma `fairnessExtraDelay = 0.25s` extra antes del próximo ciclo.
- Si el HP del player ≤ `lowHpFairnessThreshold = 25%`, se suma medio `fairnessExtraDelay` para aliviar la presión.
- Heavy Bot ataques **no son counterables** — el player debe esquivarlos con dodge, no parar-los. Esto rompe el "auto-pilot" del counter y obliga a usar todo el toolkit.

**Delay scaling:** `baseDelay - (minutesElapsed × 0.06)`, floor = 0.35s. Esto crea la curva de dificultad natural sin necesitar un sistema de dificultad separado.

#### Regla 3b — Engagement Slots (anillo de combate cercano)

Inspirado en el slot system de Spider-Man y en el "circle of combat" de
Arkham. Cap duro de enemigos que pueden estar **pegados al jugador** al
mismo tiempo, para que no se conviertan en un muro impenetrable.

1. `ArkhamEnemyManager` reevalúa slots cada `engagementUpdateInterval`
   (default 0.2s).
2. Lista de vivos ordenados por distancia al jugador.
3. Los primeros `closeEngagementSlots` (default **3**, configurable) o
   los que estén comprometidos en `isPreparingAttack`/`isAttacking`
   conservan su slot — `forcedKeepDistance = false`.
4. El resto entra como **reservas**: `SetForcedKeepDistance(true, reserveOrbitDistance)`
   con default `reserveOrbitDistance = 6.5m`.
5. Una reserva orbita: retreat si está a <ring−0.6m, approach si está a
   >ring+1.5m, strafe en la banda. Esto pinta el efecto de "rondita".
6. Apenas un slot se libera (kill, retreat, knockback fuera del anillo),
   la próxima tick promueve a la reserva más cercana.

**Dispersión:** el vector de separación en `Move()` ahora usa radio
2.6m (antes 1.8m) y suma un componente tangencial (45%) para que dos
bots se "deslicen" alrededor en vez de quedarse cara a cara empujándose.
El signo del tangencial es estable por par (hash de instance ID) — sin
oscilación.

**Tuning rápido:**
- `closeEngagementSlots = 3` para arenas chicas, 4-5 para arenas grandes.
- `closeEngagementRadius = 4.5m` se siente bien con `attackRange` 1.8-2m.
- `reserveOrbitDistance` debe ser > `closeEngagementRadius + 1.5m`.

#### Regla 4 — AI de movimiento: Strafe + Approach

Enemigos no atacando ejecutan un loop de idle movement:
1. **Strafe** izquierda/derecha alrededor del player (velocidad 1.25 m/s).
2. Alternan dirección cada 1.5–3.5s.
3. Si están lejos del player, **Approach** (velocidad 5 m/s) hasta rango de strafe.
4. **Ranged enemies (SpitterDrone):** mantienen distancia ideal (~7m) retrocediendo si el player se acerca, pero strafean cuando están en sweet spot.

El enemy siempre mira al player (`FacePlayer()` en Update).

#### Regla 5 — Attack Routine

**Melee:**
```
PrepareAttack (0.35s) → Show counter cue → Approach player
→ In range? → Attack hit (0.2s delay) → Apply damage → Recovery (0.55s)
→ Director calls BeginRetreat()
```

**Ranged (SpitterDrone):**
```
PrepareAttack (0.5s) → Show counter cue → SpitterDroneBehavior.FireProjectile()
→ Wait for burst completion → Recovery (0.65s) → Director calls BeginRetreat()
```

**Scrap Throw (ScrapThief):**
```
ScrapThiefBehavior.TryScrapAttack() → 40% chance roll
→ Find nearby scrap → Grab (0.4s) → Prepare throw (0.35s)
→ MagneticObject.ForcedEject() toward player → Recovery (0.55s)
```

Durante `PrepareAttack`, el counter cue (esfera brillante sobre la cabeza) se muestra. El jugador puede Counter en esta ventana. Si counterea: el ataque se cancela, enemigo queda Stunned.

#### Regla 6 — Spitter Drone Projectile System

Los proyectiles del Spitter Drone tienen doble naturaleza:

1. **Como amenaza:** vuelan hacia el player, hacen 1 daño al impacto.
2. **Como recurso:** tienen `MagneticObject` component → pueden ser atraídos por Pull, entran en órbita, y se repelen como chatarra normal.

Esto crea un counter-play loop:
- Spitter dispara → Player activa Pull → proyectil se convierte en munición orbital → Player repele de vuelta

| Stat del proyectil | Valor |
|---|---|
| Velocidad | 10 m/s |
| Daño al player | 1 |
| Lifetime | 4s |
| hitRadius | 0.5m |
| Attractable | Sí |
| Magnetic Mass | 0.8 |
| Burst count | 2 |
| Burst interval | 0.3s |
| Aim inaccuracy | ±6° |

#### Regla 7 — Scrap Thief System

Enemigos con `ScrapThiefBehavior` pueden:

1. **Detectar** scraps (`MagneticObject` en estado `InWorld`) dentro de `grabRadius` (3.5m).
2. **Agarrar** el scrap, deshabilitando su física y pegándolo al `grabPoint`.
3. **Lanzar** el scrap como proyectil (`ForcedEject`) hacia el player.

El scrap lanzado sigue siendo un `MagneticObject` en estado Projectile — puede:
- Golpear al player
- Ser atraído mid-air por el player (Pull intercepta el throw)
- Golpear otros enemigos si el player lo repele

| Stat | Valor |
|---|---|
| Grab radius | 3.5m |
| Grab cooldown | 5s |
| Throw speed | 12 m/s (Heavy), 10 m/s (others) |
| Throw chance | 40% (cuando Director selecciona + hay scrap) |
| Throw inaccuracy | ±8° |

Esto implementa la fantasía estilo Resident Evil 4 de los Ganados tirando hachas: el entorno se convierte en arma para ambos lados.

#### Regla 8 — Repulsión magnética

Un enemigo magnetizado puede ser atraído por Pull y luego repelido como "proyectil viviente":
- Durante atracción: `isMagneticallyControlled = true`, AI pausada.
- Al repeler: el enemigo vuela en la dirección del repel.
- Si impacta a otro enemigo: ambos reciben `magnetizedEnemyDamage` (5).
- Si impacta una pared: `wallSlamDamage` del `arena-system`.
- Después de la repulsión: marca se resetea a Normal.

#### Regla 9 — Muerte y despawn

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
      │                │                │              
      │           [Countered]      [Ranged: Fire]       
      │                ▼                ▼              
      │            Stunned ─── decay ──▶ Idle/Strafe  
      │                                               
      ├── [Magnetized + Pulled] ──▶ MagControlled ──▶ Repelled ──▶ Idle or Dead
      │                                               
      ├── [ScrapThief] ──▶ GrabScrap ──▶ ThrowScrap ──▶ Idle/Strafe
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
| Spitter Drone | 2 | ~1.6s |

### Damage al impactar como proyectil
```
magnetizedEnemyDamage = 5 (flat)
// Applies to: target enemy AND repelled enemy
```

### Attack Director Timing
```
delayBetweenAttacks = Max(0.35, Random(0.65, 1.5) - minutesElapsed * 0.06)
// At 8 enemies alive with 5+ threshold: up to 2 simultaneous attackers
// At minute 5+: effective delay ≈ 0.35-1.2s
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

### E7 — Spitter Drone projectile attracted while another burst is in-flight
**Resolución:** cada proyectil es independiente con su propio `MagneticObject`. Múltiples proyectiles pueden estar en órbita simultáneamente sin conflicto.

### E8 — ScrapThief grabs scrap that player is currently attracting
**Resolución:** ScrapThief solo grabs `MagneticObjectState.InWorld` — si el scrap ya está `Attracting` o `InOrbit`, no lo toma.

### E9 — Two simultaneous attackers both counter-cued
**Resolución:** el player puede Counter al más cercano. El segundo atacante continúa su ataque normalmente, creando presión real.

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
| `maxSimultaneousAttackers` | 2 | 1–3 | Combate solitario, predecible | Player abrumado, ilegible |
| `secondAttackerAfterMinutes` | 1.5min | 0–5min | Double-team muy temprano, opening duro | Double-team casi nunca, run plano |
| `secondAttackerMinAlive` | 4 | 2–8 | Double-team con pocos enemigos en arena | Double-team casi nunca, run plano |
| `attackerStaggerRange` | 0.25–0.55s | 0.05–1.0s | Telegraphs colapsan en mismo frame | Attackers se sienten desconectados |
| `spawnAttackGracePeriod` | 1.2s (Heavy 1.4, Spitter 1.5) | 0–3s | "El bot apareció y me pegó" | Spawns demasiado pasivos |
| `fairnessExtraDelay` | 0.25s | 0–1s | El combo del player se interrumpe | Enemigos demasiado pasivos durante combo |
| `lowHpFairnessThreshold` | 0.25 | 0–0.5 | Sin compasión en HP bajo | Demasiada compasión en HP bajo |
| `delayReductionPerMinute` | 0.06 | 0–0.15 | Sin escalada de presión | Ataques cada vez más rápidos |
| `canBeCountered` (per def) | true (Heavy=false) | bool | Todos los enemigos parrieables, counter trivializa | Nadie counterable, counter es inútil |
| `counterStunDuration` | 1s | 0.5–2s | Counter casi no recompensa | Counter trivializa enemigos |
| `attackTokenCost` | 1 (Heavy=2) | 1–3 | Heavies se acumulan en pantalla | Heavies casi nunca atacan |
| `closeEngagementSlots` | 3 | 1–6 | Combate solitario | Player rodeado de carne |
| `closeEngagementRadius` | 4.5m | 3–8m | Ring muy chico, reserves casi adentro | Ring inmenso, todos engaged |
| `reserveOrbitDistance` | 6.5m | 5–10m | Reservas pegadas, sensación de pared | Reservas inalcanzables, sin presión |
| `magneticMarksToMagnetize` | 2–3 | 1–5 | Magnetiza con 1 hit, demasiado fácil | Nunca magnetiza, loop roto |
| `markDecayTime` | 6s | 3–10s | Marcas decaen rápido, difícil magnetizar | Marcas persisten mucho, trivializa |
| `approachSpeed` | 3–7 | 2–10 | Enemigos nunca llegan, sin presión | Enemigos encima instantáneamente |
| `magneticMass` | 2.2–5 | 1–8 | Se atraen muy rápido | Se atraen muy lento |
| `magnetizedEnemyDamage` | 5 | 2–8 | Repel de enemigo no vale la pena | Repel de enemigo one-shots todo |
| `projectileSpeed` | 10 | 5–18 | Fácil de esquivar, sin presión | Muy rápido, injusto |
| `burstCount` | 2 | 1–4 | Poca amenaza ranged | Demasiados proyectiles |
| `scrapThrowChance` | 0.40 | 0–1 | Raramente tira scraps | Siempre tira, sin melee |
| `scrapThrowSpeed` | 10–12 | 6–16 | Fácil de esquivar | Muy rápido |

## Visual/Audio Requirements

- **Counter cue (sense)**: el indicador de counter ya **no vive en la cabeza del enemigo**; se mudó al jugador como un "sentido magnético" (`CounterSenseIndicator`). Ver `combat-system.md` § Counter Sense. El enemigo ya no aporta cue de pre-counter.
- **Counter stun VFX (post-counter)**: cuando el jugador acierta el counter, se spawnea `counterStunVfxPrefab` sobre la cabeza del enemigo (default 2.15 m), lifetime ≈ 1 s. Idealmente estrellas/chispas pulsantes (lectura "K.O. temporal").
- **Magnetized indicator**: glow amarillo pulsante cuando `markState == Magnetized`.
- **Hit flash**: el material del enemigo flashea blanco por 0.1s al recibir daño.
- **Death**: animación de "colapso" + partículas de debris. El cadáver persiste 0.8s antes de despawn.
- **Repel como proyectil**: trail visible durante el vuelo. Sonido de impacto al golpear otro enemigo/pared.
- **Spitter Drone fire**: VFX de muzzle flash en FirePoint. Sonido de disparo. Projectile trail durante vuelo.
- **Scrap grab**: VFX de chispas al agarrar. Scrap visualmente pegado al GrabPoint del enemigo.
- **Scrap throw**: VFX de arco + trail. Sonido de lanzamiento diferenciado del repel del player.

## UI Requirements

- **World-space health bar**: barra flotante sobre cada enemigo (ya implementada en `WorldSpaceHealthBar`).
- **Magnetic mark indicator**: iconos sobre el enemigo mostrando stacks de marca (1 punto, 2 puntos, glow full).
- **Spitter Drone aim telegraph**: línea punteada o laser pointer durante PrepareAttack para que el player sepa que viene un disparo.
- Sin HUD elements — toda la info del enemigo es worldspace.

## Acceptance Criteria

1. **AC-1**: Scrapling (3HP) muere en 2 LightScrap hits o 1 Plate hit.
2. **AC-2**: Metal Enemy es siempre atraíble por Pull sin necesitar marks.
3. **AC-3**: 2 strikes sobre un Scrapling lo magnetizan. Pull lo atrae. Repel lo lanza como proyectil.
4. **AC-4**: Attack Director solo permite 1 enemigo atacando a la vez (2 con ≥5 enemigos vivos).
5. **AC-5**: Counter durante PrepareAttack cancela el ataque y stunsea al enemigo.
6. **AC-6**: Enemigo repelido contra pared recibe `wallSlamDamage` adicional.
7. **AC-7**: Enemigo repelido contra otro enemigo: ambos reciben `magnetizedEnemyDamage`.
8. **AC-8**: Las marcas magnéticas decaen después de 6s sin nuevo strike.
9. **AC-9**: `Pool.Despawn()` reemplaza `Destroy(gameObject, delay)` en Die().
10. **AC-10**: Health bar worldspace se muestra correctamente y hace billboard hacia la cámara.
11. **AC-11**: Spitter Drone dispara 2 proyectiles en burst. Proyectiles son atraíbles por Pull.
12. **AC-12**: Heavy Bot puede agarrar scrap del suelo y lanzarlo al player.
13. **AC-13**: Scrap lanzado por enemigo puede ser atraído mid-air por Pull del player.
14. **AC-14**: Con ≥5 enemigos, el Director envía 2 atacantes simultáneos con stagger.
15. **AC-15**: El delay del Director se reduce progresivamente durante la run.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿El Spitter Drone debería tener un VFX de apuntado (laser telegraph) durante PrepareAttack? Mejoraría la legibilidad pero reduciría sorpresa. | cris | Pre-playtest |
| Q2 | ¿Enemigos deberían soltar chatarra al morir? Crearía un loop de munición auto-sostenible. Riesgo: el player nunca se queda sin ammo. | cris | Post-playtest |
| Q3 | ¿El daño de repulsión magnética (5) debería escalar con la velocidad de repel? Más velocidad = más daño. Haría que la posición del player (cerca de pared = más recorrido) importe más. | cris | Post-playtest |
| Q4 | ¿Implementar "aggro" system donde enemigos priorizan al player que los marcó? Crearía más predictibilidad táctica. | cris | Post-MVP |
| Q5 | ¿Agregar un "Bomber" que explota al morir y convierte su cadáver en chatarra heavy? Combo con magnetism: magnetizar → repeler → explota entre grupo. | cris | Post-MVP |
| Q6 | ¿El ScrapThief debería poder agarrar scraps que están en órbita del player? Sería más agresivo pero podría frustrar. | cris | Post-playtest |
