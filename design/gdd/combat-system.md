# combat-system

> **Status:** In Design
> **Última actualización:** 2026-05-10
> **Capa:** Content (Layer 2) · **Tier:** MVP · **Roadmap:** Día 3
> **Implements Pillars:** P2 (Strike es 1 de los 3 verbos), P3 (Strike *prepara*, no mata), P1 parcial (Counter convierte ataque enemigo en oportunidad)

## Overview

El `combat-system` cubre los dos verbos cuerpo-a-cuerpo y defensivos del
jugador: **Strike** (ataque cónico frontal corto, daño bajo, aplica
marca magnética a enemigos) y **Counter** (ventana defensiva ajustada
activada con Espacio cuando un enemigo telegrafia un ataque; niega el
daño y repele al atacante). También rastrea eventos transientes de
combate (hits, kills, counters exitosos) y los publica para
`scoring-xp-system`, `mission-system` y `presentation-system`.

Strike es el **verbo de setup**: no carga el peso del kill (eso lo hace
`magnetism-system`), pero convierte enemigos neutrales en magnetizables y
mantiene vivo el ritmo de combate. Counter es la **herramienta de flow
defensivo**: premia leer telegraphs enemigos con negación de daño y un
reposicionamiento libre. Junto con `magnetism-system`, completa el Pilar
P2 (los tres verbos: Pull / Strike / Repel).

Sin este sistema no hay marca magnética posible — `magnetism-system`
puede atraer chatarra pero no enemigos, y la mitad del techo de skill
desaparece. Sin Counter, los ataques enemigos sólo se evitan con
movimiento, aplanando la curva de aprendizaje.

## Player Fantasy

Strike y Counter son los verbos **staccato** del juego — los breves
momentos de control entre las grandes descargas magnéticas. La fantasía
no es ser un espadachín; es ser un **director que tagea con destellos
eléctricos**.

1. **Strike — tap-tap-tag.** Cada Strike es un golpe corto que no busca
   matar. Busca **marcar**. Es plantar una bandera eléctrica sobre un
   enemigo: "ahora vos sos parte de mi órbita". Verbo de ritmo, no de
   fuerza.
2. **Counter — leer la jugada.** Cuando un enemigo carga un ataque, hay
   un cue visual con ventana de 0.4 s. Acertar el Counter niega el daño,
   repele al atacante y deja al jugador parado en el lugar exacto. Es
   sublimación defensiva *premiada pero no única*: con cooldown de 4 s,
   el jugador no puede defender todo con Counter — el movimiento
   también es defensa.
3. **Combo — el ritmo.** El combo no se construye golpeando rápido por
   golpear rápido; se construye **alternando verbos**. Strike → Strike →
   Pull → Repel → Strike → Counter es el flow target. El sistema premia
   la variedad sobre la repetición.

### Referencias

- **Hades** — ritmo de attack/cast/dash. Strike ≈ attack, Counter ≈
  perfect dodge.
- **Bayonetta witch time** — Counter generoso (no requiere frame-perfect)
  pero premiado con repercusión grande. Modelo más cercano que Sekiro
  para el alcance jam.
- **Doom 2016 chainsaw** — "este hit no es para daño, es para preparar
  el siguiente". Strike opera con la misma lectura.
- **Hi-Fi Rush** — timing de cómo cada verbo enlaza con los demás
  (consistencia con `magnetism-system`).

### Por qué importa

Sirve a:
- **P2** (Pull/Strike/Repel) — Strike es 1 de los 3 verbos.
- **P3** (el daño grande viene del magnetismo) — Strike *prepara*, no
  mata.
- **P1** (el escenario es el arma) — Counter convierte un ataque enemigo
  en una oportunidad: el atacante repelido puede impactar contra otros
  enemigos o contra paredes.

Si Strike se siente como "ataque básico de RPG", el sistema falla. Si
Counter no produce un momento de "puff, esquivé eso", la mecánica
defensiva muere y el jugador empieza a *spammear* Pull/Repel para
sobrevivir.

## Detailed Design

El sistema cubre dos sub-mecánicas (**Strike**, **Counter**) y emite los
eventos de combate consumidos por scoring/mission/HUD/presentation.
**No** es dueño del combo — eso vive en `scoring-xp-system`.

### Core Rules

#### Strike

1. Activado por intent `Strike` (default click derecho; abstraído por
   `input-system`).
2. Cooldown global de `strikeCooldown` (0.35 s). Mientras está en
   cooldown, presionar el input no hace nada (silenciosamente
   ignorado, sin animación).
3. Al ejecutar:
   - Define un cono de ángulo `strikeConeAngle` (70°) y rango
     `strikeRange` (2.5 m), centrado en la dirección
     `playerPosition → Aim`.
   - Detecta colliders con componente `IMarkable` cuyo pivot esté
     dentro del cono.
4. **Hit-all-in-cone**: cada `IMarkable` detectado recibe:
   - `damage-health-system.Damage(target, strikeBaseDamage,
     type=Kinetic, source=Strike)` — daño bajo (~3, sirve para
     preparar, no para matar — Pilar P3).
   - `IMarkable.ApplyMark(1)` — suma 1 stack. Magnetism decide si pasa a
     Marcado o Magnetizado según las reglas de stacks (Normal+1 →
     Marcado; Marcado+1 dentro de 6s → Magnetizado).
5. Strike **no afecta** `IAttractable` (chatarra, placas, minas, pesados).
   Sólo afecta enemigos.
6. Strike **interrumpe el windup** de cualquier enemigo golpeado: si el
   enemigo estaba en `attackingState == Windup`, su ataque se cancela y
   entra en stagger breve (`enemyStaggerDuration` ≈ 0.2 s — owned por
   `enemy-system`).
7. Strike **no bloquea** otras acciones del jugador. Pull/Repel siguen
   funcionando en paralelo (combat y magnetism son FSMs independientes).
8. Eventos publicados:
   - `OnStrikeFired()` — cada press exitoso (cooldown OK)
   - `OnStrikeHit(target, position)` — por enemigo impactado
   - `OnStrikeKill(target, position)` — si Strike llevó HP del target a 0
   - `OnEnemyAttackInterrupted(enemy, attackId)` — por windup cancelado

#### Counter (pulso)

1. Activado por intent `Counter` (default Espacio).
2. Cooldown de `counterCooldown` (4 s).
3. Al press:
   - Se abre una ventana `counterWindow` (0.4 s) durante la cual el
     sistema observa ataques enemigos.
   - **Pulso radial omnidireccional** de radio `counterRadius` (4 m)
     centrado en el jugador.
4. Durante la ventana, para cada enemigo dentro de `counterRadius`:
   - **Filtro de elegibilidad:** el enemigo debe tener
     `EnemyDefinition.canBeCountered = true`. Heavy Bot y futuros
     archetypes "pesados" devuelven `false` aquí — sus ataques
     **no pueden ser parrieados** y deben esquivarse con dodge.
   - Si `enemy.attackingState == Windup` y es elegible: counter
     inmediato (resuelto en ese frame), antes de que el daño se aplique.
   - Si un ataque enemigo llega al jugador (proyectil o melee
     contact-hit) y la fuente es elegible: el daño se niega y el
     atacante es counterado.
5. **Por cada atacante counterado**:
   - El daño que iba a recibir el jugador se anula (no llama `Damage`).
   - Knockback radial fuerte: `counterKnockbackForce` (10 m/s) en
     dirección `enemyPos → playerPos`, invertida (sale del jugador).
   - `IMarkable.SetMarkState(Magnetizado)` — bypass de stacks. Combat
     es el único caller de este API; magnetism lo expone explícitamente
     para Counter.
   - **Stun de counter:** el enemigo entra en `isStunned = true` durante
     `counterStunDuration` (default 1 s, configurable por
     `EnemyDefinition`). Durante ese segundo no puede moverse ni atacar.
   - **VFX de stun en cabeza del enemigo:** se spawnea
     `counterStunVfxPrefab` parented al transform del enemigo a
     `counterStunVfxHeight` (default 2.15 m), con lifetime
     `counterStunVfxLifetime` (default 1.05 s).
   - Publica `OnCounterSuccess(attacker, position)`.
6. Si la ventana cierra sin atrapar a nadie:
   - Cooldown completo aplicado (player learns timing — sin "free reset").
   - Publica `OnCounterFired(succeeded=false)`.
7. Counter **no bloquea** otras acciones (Pull/Repel/Strike pueden estar
   activos durante la ventana).
8. Eventos publicados:
   - `OnCounterFired(succeeded: bool)` — cada press
   - `OnCounterSuccess(attacker, position)` — por atacante counterado
   - `OnCounterFailed()` — alias semántico de `OnCounterFired(false)`

#### Counter Sense (telegraph en el jugador)

Inspirado en el "spider sense" de Marvel's Spider-Man: la advertencia
visual del counter **vive en la cabeza del jugador**, no en el enemigo.

1. El componente `CounterSenseIndicator` se auto-attachea al jugador
   junto al `ArkhamCombatController` (similar a `StrikeTargetIndicator`).
2. Cada frame consulta
   `ArkhamEnemyManager.HasCounterTargetInRadius(playerPos, counterRadius)`:
   true cuando **al menos un enemigo elegible para counter** está
   en windup/attack dentro del radio.
3. Cuando true → activa un VFX sobre la cabeza del jugador
   (`senseVfxPrefab` parented al jugador a `vfxHeight = 2.1 m`).
   Si el prefab no está asignado, crea un fallback (esfera coloreada)
   para que el cue siga legible durante prototipado.
4. Cuando false → desactiva el VFX. La transición es instantánea (set
   active on/off) para que el cue siga la cadencia del telegraph.
5. **Filtros importantes:**
   - HeavyBot **nunca** dispara el sense — sus ataques no son
     counterables, mostrar el cue sería mentir al jugador.
   - Mientras el jugador está mid-strike o mid-counter
     (`hideWhileBusy = true`), el cue se oculta para no pelear con la
     animación de combate.
6. **Consecuencia de diseño:** un único cue agregado limpia la lectura
   visual cuando hay 2-3 atacantes simultáneos. En lugar de tres
   triángulos amarillos sobre tres cabezas, el jugador ve "estoy bajo
   amenaza counterable, ahora" — el targeting lo hace `Counter` mismo
   (siempre toma el más cercano).

#### Combo (delegado)

`combat-system` **no posee** el contador de combo. Sólo emite eventos
de hit/kill/counter. **`scoring-xp-system` agrega** los eventos
(combinados con `OnEnemyKilledByRepel` y `OnEnemyMagnetizedRepelled` de
magnetism) y mantiene `comboCount` con timer de `comboTimerSeconds`
(3 s). HUD lee `scoring.comboCount`, no `combat.comboCount`.

Diseño justificado: combo cruza Strike + Repel + Counter, así que su
dueño natural es scoring (que ya agrega XP de todas las fuentes).

### States and Transitions

#### Player-combat FSM (independiente de magnetism)

```
        ┌──────┐  Strike (CD ok)         ┌──────────┐
        │ Idle │ ──────────────────────> │ Striking │
        └──────┘                         └──────────┘
           ▲                                  │
           │  StrikeRecovery done             │  resolve hits
           │                                  ▼
           │                          ┌──────────────────┐
           └──────────────────────────│ StrikeRecovery   │
                                      │ (rest of 0.35s)  │
                                      └──────────────────┘

        ┌──────┐  Counter (CD ok)        ┌────────────────┐
        │ Idle │ ──────────────────────> │ CounterWindow  │
        └──────┘                         │ (0.4s)         │
           ▲                             └────────────────┘
           │                                  │
           │  CounterRecovery done            │  window closes
           │                                  ▼
           │                          ┌────────────────────┐
           └──────────────────────────│ CounterRecovery    │
                                      │ (rest of 4s)       │
                                      └────────────────────┘
```

| Desde | Evento | A | Side effect |
|---|---|---|---|
| Idle | `Strike` + cooldown ok | Striking | Resuelve cone hits en mismo frame |
| Striking | hits resolved | StrikeRecovery | Aplica damage + ApplyMark + interrupt |
| StrikeRecovery | timer 0.35 s | Idle | — |
| Idle | `Counter` + cooldown ok | CounterWindow | Abre ventana, listo para counter |
| CounterWindow | enemy attack catched | CounterWindow | Aplica counter (window sigue abierta) |
| CounterWindow | timer 0.4 s | CounterRecovery | — |
| CounterRecovery | timer (4 - 0.4 = 3.6 s) | Idle | — |

**Striking** y **CounterWindow** pueden ocurrir simultáneamente con los
estados de magnetism (Pulling, Orbiting, Repelling, Cooldown). Combat y
magnetism son FSMs paralelos no acoplados.

#### Per-Strike instance

Cada press genera una instancia transient:
```
Spawned ──cone hit detection (1 frame)──> Resolve hits ──> Done
```

#### Per-Counter instance

Cada press genera una ventana:
```
WindowOpen ──for counterWindow seconds──> WindowClose
  └─[for each frame: check enemy windups, check incoming attacks]
```

#### Mark state machine

Vive en `magnetism-system` (no se redefine acá). Combat invoca:
- `IMarkable.ApplyMark(1)` por cada Strike hit
- `IMarkable.SetMarkState(Magnetizado)` por cada Counter exitoso

Las reglas de transición y decay son responsabilidad exclusiva de
magnetism.

### Interactions with Other Systems

| Sistema | Dirección | Interfaz / Datos |
|---|---|---|
| `input-system` | in | Lee `Strike` (event), `Counter` (event), `Aim` (Vector3 cursor world pos) |
| `player-movement` | in | Lee `playerPosition`, `playerFacing`. Combat resuelve dirección de cono usando `Aim - playerPosition` |
| `damage-health-system` | out | `Damage(target, amount, type=Kinetic, source=Strike\|CounteredAttack)` |
| `enemy-system` | bidirectional | Lee `enemy.attackingState` (Windup/Active/Recovery — owned por enemy-system); lee `Transform.position`. Llama `IMarkable.ApplyMark(1)` y `IMarkable.SetMarkState(Magnetizado)`. Publica `OnEnemyAttackInterrupted` que enemy-system puede consumir para cancelar windup. |
| `magnetism-system` | out (consumer of contract) | Invoca `IMarkable.ApplyMark(1)` y `IMarkable.SetMarkState(Magnetizado)`. magnetism es dueño del contrato; combat es consumer. magnetism también lee de combat los eventos `OnEnemyAttackInterrupted` por si afecta la lógica de marca. |
| `scoring-xp-system` | out (events) | Subscribe a `OnStrikeKill`, `OnCounterSuccess`. **Owns combo logic**: combina con eventos de magnetism para mantener `comboCount`. |
| `mission-system` | out (events) | Subscribe a `OnCounterSuccess` (Counterstorm: 2 counters), `OnStrikeHit` (No Hands: missed if any Strike connects). |
| `upgrade-system` | in (modifiers) | Recibe modifiers para `strikeBaseDamage`, `strikeRange`, `strikeCooldown`, `counterWindow`, `counterCooldown`, `counterRadius`. |
| `powerup-system` | in (timed effects) | Slow Time afecta tiempo enemigo → ventana relativa de Counter se siente más amplia. |
| `hud-system` | out (publish) | Publica `strikeReady: bool`, `counterReady: bool`, `counterCooldownLeft: float`, `inCounterWindow: bool` (highlight breve). |
| `presentation-system` | out (events) | Todos los eventos de combate para VFX/SFX/hitstop/screen shake/popups. |

#### Notas de ownership

- **Strike y Counter como acciones** → owned por `combat-system`.
- **Mark state machine** → owned por `magnetism-system`. Combat es consumer
  vía API.
- **Combo counter** → owned por `scoring-xp-system`. Combat sólo emite
  primitivas.
- **Enemy attack windup detection** → owned por `enemy-system`. Combat
  consume read-only.
- **Damage application** → owned por `damage-health-system`. Combat envía
  primitivas.

## Formulas

Combat tiene menos matemática que magnetism. La mayoría son detecciones
geométricas + cooldowns lineales. Valores marcados con (*) son literales
del GDD jam.

### Strike — cone hit detection

Una vez por press exitoso (cooldown OK):

```
for each enemy E with IMarkable in arena:
  dist = ||E.position − playerPos||
  if (dist > strikeRange): skip
  angleToEnemy = angleBetween(playerFacing, E.position − playerPos)
  if (|angleToEnemy| ≤ strikeConeAngle / 2):
    hit(E)
```

| Variable | Default | Notas |
|---|---:|---|
| `strikeRange` | 2.5 m | Cuerpo a cuerpo, no melee largo |
| `strikeConeAngle` | 70° (*) | GDD §6.5 |

`playerFacing` puede derivar de `Aim` (recomendado) o del último vector
de movement (fallback si no hay cursor — gamepad future-proof).

### Strike — daño aplicado por hit

```
damage = strikeBaseDamage × (1 + strikeDamageMod)
damage-health.Damage(E, damage, type=Kinetic, source=Strike)
```

| Variable | Default | Notas |
|---|---:|---|
| `strikeBaseDamage` | 3 | Bajo, intencional (P3) |
| `strikeDamageMod` | 0 | Subido por upgrade *Impacto brutal* a +0.20 |

### Counter — detection (cada frame durante ventana)

```
for each enemy E within counterRadius:
  if (E.attackingState == Windup):
    triggerCounter(E)

for each pendingEnemyAttack A targeting player:
  if (counterWindowOpen):
    cancelDamage(A)
    triggerCounter(A.attacker)
```

| Variable | Default | Notas |
|---|---:|---|
| `counterRadius` | 4 m | Pulso radial, lectura espacial generosa |
| `counterWindow` | 0.4 s | Centro de rango GDD §6.8 (0.35-0.5s) |

### Counter — knockback al atacante

```
dir = normalize(E.position − playerPos)
E.velocity = dir × counterKnockbackForce
// damping en enemy-system reduce a 0 en ~0.6 s
```

| Variable | Default | Notas |
|---|---:|---|
| `counterKnockbackForce` | 10 m/s | Suficiente para sacar al atacante del rango melee |

### Cooldown ticking (Strike y Counter, mismo patrón)

```
strikeCooldownLeft  = max(0, strikeCooldownLeft − dt)
counterCooldownLeft = max(0, counterCooldownLeft − dt)
canStrike  = (strikeCooldownLeft  == 0)
canCounter = (counterCooldownLeft == 0)
```

Al ejecutar:
- `strikeCooldownLeft  = strikeCooldown`  (0.35 s)
- `counterCooldownLeft = counterCooldown` (4 s)

### Mark stack (vive en magnetism, combat lo invoca)

- Strike hit por enemigo: `IMarkable.ApplyMark(1)`
- Counter exitoso por atacante: `IMarkable.SetMarkState(Magnetizado)`

Las reglas de transición (Normal+1 → Marcado, Marcado+1 → Magnetizado,
decay 6 s step-down) son responsabilidad de magnetism.

### Tabla de daños base

| Fuente | Base damage | Tipo | Notas |
|---|---:|---|---|
| Strike per enemy in cone | 3 | Kinetic | × (1 + strikeDamageMod) |
| Counter (atacante repelido) | 0 directo | — | Knockback + Magnetizado, no daño |
| Counter de proyectil enemigo | 0 | — | Proyectil cancelado, no devuelto (jam scope) |

### Referencia HP enemigos (calibración tiempo-a-kill por Strike puro)

| Enemigo | HP | Strikes para matar | Tiempo @ 0.35s cooldown |
|---|---:|---:|---:|
| Scrapling | 10 | 4 | ~1.4 s |
| Runner Bot | 15 | 5 | ~1.75 s |
| Shield Bot | 30 (frontal blocked) | 10 (flanqueando) | ~3.5 s |
| Spitter Drone | 12 | 4 | ~1.4 s |
| Scrap Brute | 150 | 50 | ~17.5 s |

Strike puro es **deliberadamente ineficiente** contra enemigos con HP
mayor a Scrapling. El diseño correcto: Strike marca → Pull/Repel mata.
Esto refuerza P3 (el daño grande viene del magnetismo).

### Sin curvas exponenciales

Todos los valores de combat son **constantes o escalados lineales por
upgrade**. Combat no necesita curvas porque su rol es preparar, no
escalar daño con el tiempo.

## Edge Cases

### Boundary values

- Strike sin enemigos en cono → emite `OnStrikeFired` solo, sin
  `OnStrikeHit`. No error.
- Counter sin enemigos en radio → ventana abre, cierra vacía, cooldown
  aplicado, `OnCounterFailed`.
- Strike contra enemigo ya muerto (HP=0 mismo frame) → skip silencioso,
  no double-kill.
- `IMarkable.ApplyMark` sobre enemigo muerto → no-op (magnetism debe
  handlearlo).
- Counter contra enemigo que NO está en Windup → ese enemigo no es
  counterado; otros en radio sí. Si ningún enemigo en Windup, ventana
  cierra vacía aunque haya enemigos cerca.
- Counter contra **HeavyBot** (o cualquier definition con
  `canBeCountered=false`) → ese enemigo se ignora silenciosamente
  (no consume el counter, no rompe la animación). `CounteredBy()`
  hace early-return si `!canBeCountered`. El cue de counter sense
  tampoco se activa por su windup, así que el jugador aprende a
  diferenciarlo visualmente.

### Race conditions / simultáneos

- 2 enemigos atacando en mismo frame con Counter activo → ambos
  counterados (la ventana atrapa todo lo que entra durante 0.4 s).
- Strike + Counter en mismo frame → ambos ejecutan (FSMs independientes).
- Strike kill target que estaba en Windup → kill prevalece (target muerto
  antes de que Counter pueda interceptar).
- Strike → Counter en frames consecutivos al mismo enemigo → ambos
  eventos publicados (`OnEnemyAttackInterrupted` + `OnCounterSuccess`).

### Modifier interactions

- `counterWindow` modificado mid-window (powerup expira) → usar valor al
  `OnCounterFired`. No re-evaluar mid-window.
- `strikeCooldown = 0` por upgrade extremo → rate-limited a 1 strike por
  frame por input system (event no continuo). Cone hits podrían stackear
  si Strike re-arma frame siguiente — aceptar.
- `counterRadius` cambiado mientras ventana abierta → snap al nuevo valor
  para los frames restantes.

### Cross-system

- Strike interrumpe windup del Brute → `boss-system` decide si su windup
  es interruptible. **Asunción provisional**: ataques específicos del
  Brute marcados `interruptible = false`.
- Counter contra enemigo en knockback (flying tras un Repel): si todavía
  está en radio y en Windup, counter; si no, skip.
- Strike o Counter durante ForcedEject de magnetism → funcionan normal
  (FSMs paralelas).
- Strike o Counter durante muerte del jugador → ambos disabled. Hook:
  `combat-system` subscribe a `damage-health.OnPlayerDeath` y deshabilita
  inputs.
- Counter contra proyectil de Spitter Drone: el proyectil se **cancela**
  (no se devuelve en jam scope, ver Open Questions Q1).

### Powerup interactions

- *Slow Time*: enemigos se ralentizan, su Windup tarda más en tiempo
  real. `counterWindow` queda en real-time → Counter se siente más fácil.
  **Comportamiento intencional**, no es bug.
- *Magnet Fever*: sin interacción con combat (afecta sólo magnetism).
- *Repeler 360*: sin interacción con combat directa.

### Exploits previstos (autobalanceados)

- *Spam Counter para fish ataques entrantes* → cooldown 4 s lo previene.
  Failed counter aplica cooldown completo.
- *Strike infinito a un enemigo Magnetizado* manteniéndolo eternamente
  vivo: cada Strike resetea `lastStrikeTime` en magnetism. **Feature**, no
  exploit — el jugador mantiene presión, pero el daño bajo y los
  contraataques enemigos balancean.
- *Counter para magnetizar Brute*: Brute es immune por mass
  (`mass > pullableEnemyMaxMass`). Counter aún niega daño y aplica
  knockback, pero `SetMarkState(Magnetizado)` no rinde gameplay porque
  Pull rechaza por mass. **Decisión**: aceptar — Counter sigue siendo
  útil contra Brute por knockback y daño negation aunque no lo magnetice.

### WebGL

- `dt` clamp heredado de magnetism (`dtMax = 0.05 s`). Combat usa el
  mismo clamp para cooldown ticking, evitando que un tab refocus
  resetee cooldowns prematuramente.

### Asunciones provisionales sobre deps no diseñadas

- **`enemy-system`** expone:
  - `attackingState ∈ { Idle, Windup, Active, Recovery }` per enemy
  - duración de Windup queryable
  - handler `OnAttackInterrupted(attackId)` que cancela el ataque
- **`damage-health-system`**:
  - queues damage al final del frame (consistente con magnetism)
  - publica `OnPlayerDeath` que combat consume para deshabilitar inputs
- **`magnetism-system`** (DESIGNED, ya documenta):
  - `IMarkable.ApplyMark(stacks)` y `IMarkable.SetMarkState(state)`
    disponibles. Combat es único caller de `SetMarkState`.

## Dependencies

### Upstream (sistemas de los que `combat-system` depende)

| Dep | Hard/Soft | Status | Interfaz |
|---|---|---|---|
| `input-system` | Hard | Not Designed (asunción) | `Strike` event, `Counter` event, `Aim` (Vector3) |
| `player-movement` | Hard | Not Designed (asunción) | `playerPosition`, `playerFacing` |
| `damage-health-system` | Hard | Not Designed (asunción) | `Damage(target, amount, type, source)`, `OnPlayerDeath` |
| `enemy-system` | Hard | Not Designed (asunción) | `attackingState ∈ {Idle, Windup, Active, Recovery}`, `Transform.position`, handler `OnAttackInterrupted(attackId)`. Hosts `IMarkable`. |
| `magnetism-system` | Soft (contract consumer) | ✅ DESIGNED | Invoca `IMarkable.ApplyMark(1)` (Strike) y `IMarkable.SetMarkState(Magnetizado)` (Counter). magnetism es dueño del contrato. |

**Sin `enemy-system`, combat no tiene a quién golpear** (Hard).
**Sin `magnetism-system`, Strike sigue funcionando (sólo daño) pero
pierde el 50% de su valor** — Counter pierde el efecto de magnetizar al
atacante. Hard práctico aunque conceptualmente soft.

### Downstream (sistemas que dependen de `combat-system`)

| Sistema | Razón |
|---|---|
| `scoring-xp-system` | Subscribe `OnStrikeKill`, `OnCounterSuccess`. **Owns combo logic** (no combat). |
| `mission-system` | Subscribe a `OnStrikeHit`, `OnStrikeKill`, `OnCounterSuccess`. Soporta misiones Counterstorm, No Hands. |
| `upgrade-system` | Aplica runtime modifiers a knobs de combat |
| `powerup-system` | Slow Time afecta indirectamente (windup más largo en real-time) |
| `hud-system` | Lee `strikeReady`, `counterReady`, `counterCooldownLeft`, `inCounterWindow` |
| `presentation-system` | Consume todos los eventos para VFX/SFX/hitstop/screen shake/popups |

### Contratos definidos por `combat-system`

**Ninguno.** Combat es **consumer** de contratos:
- `IMarkable` (definido por magnetism)
- `attackingState` (definido por enemy-system)
- `Damage` API (definido por damage-health)

Combat sólo emite **eventos**, no contratos. Esto es consciente: combat
es un orquestador de inputs hacia sistemas que conocen el estado.

### Eventos publicados (consumibles por cualquier sistema)

```
OnStrikeFired()
OnStrikeHit(target, position)
OnStrikeKill(target, position)
OnEnemyAttackInterrupted(enemy, attackId)
OnCounterFired(succeeded: bool)
OnCounterSuccess(attacker, position)
OnCounterFailed()
```

### Bidirectional consistency

- `magnetism-system` GDD ya lista combat como downstream con
  `IMarkable.ApplyMark` y `SetMarkState`. ✓
- `systems-index` actual lista combat dependiendo de `input +
  player-movement + damage-health + enemy-system`. **Magnetism no
  aparece como upstream de combat** — corregir al cerrar este GDD
  agregando `magnetism-system` a las deps en el mapa de capas.

## Tuning Knobs

[To be designed]

## Visual/Audio Requirements

- **Counter Sense VFX (player head):** halo/spark pulsante sobre la cabeza
  del jugador a 2.1 m, color cálido contrastante (default naranja
  `#FF6B29`). On/off instantáneo siguiendo `HasCounterTargetInRadius`.
- **Counter Stun VFX (enemy head):** estrellas/chispas sobre el enemigo
  counterado por ≈ 1 s, parented al transform del enemigo.
- **Heavy "uncounterable" tell:** color de telegraph distinto (default
  rojo en charge windup vs amarillo en archetypes counterables) —
  refuerza al jugador que "esto no se contraataca, esquivá".

## UI Requirements

- No HUD nuevo: counter sense y stun viven en worldspace.
- Mantener el cue de Strike target inalterado.

## Acceptance Criteria

- [ ] Un Scrapling preparando ataque dentro de `counterRadius` enciende
      el counter sense sobre la cabeza del jugador.
- [ ] Un HeavyBot preparando ataque **no** enciende el counter sense.
- [ ] `Counter` mientras el HeavyBot está en windup no lo afecta;
      `Counter` mientras un Scrapling está en windup lo aturde por 1 s
      y spawna stun VFX sobre su cabeza.
- [ ] El Attack Director envía hasta 3 atacantes simultáneos con
      stagger ≥ 0.18 s entre cada uno (verificable con Debug.Log).
- [ ] Enemigos recién spawneados no son seleccionados por el director
      antes de su `spawnAttackGracePeriod`.

## Open Questions

- ¿El counter sense debería tener un timing-window indicator (anillo que
  se cierra) o queda como on/off binario? Pendiente de playtest.
- ¿Heavy debería tener un counter "tardío" (perfect-parry frame) o
  permanece 100% inmune? MVP: 100% inmune.
