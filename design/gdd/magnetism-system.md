# magnetism-system

> **Status:** In Design
> **Última actualización:** 2026-05-10
> **Capa:** Core · **Tier:** MVP · **Roadmap:** Día 1
> **Implements Pillars:** P2 (Pull/Strike/Repel), P3 (el daño grande viene del magnetismo), P1 parcial (el escenario es el arma)
> **Riesgo:** ⚠ Bottleneck — ~8 sistemas dependen de este

## Overview

El `magnetism-system` es el bucle central de gameplay de Magnet Panic:
Scrapstorm. Con un solo botón del mouse el jugador ejecuta tres acciones
acopladas: **Pull** (primer click para proyectar un campo magnético que atrae
objetos metálicos y enemigos magnetizados hacia el jugador), **Orbit** (los
objetos atraídos giran alrededor del jugador, ocupan capacidad y bloquean
parcialmente proyectiles) y **Repel** (segundo click para disparar todos los
objetos orbitando en un cono hacia el cursor — la fuente principal de daño
del juego).

El sistema también es dueño de dos contratos que otros sistemas leen o
escriben: el estado de **marca magnética** sobre enemigos (lo escribe
`combat-system`, lo consume Pull) y el recurso **carga actual** (lo
consume Repel, lo observa `overload-system`). Sin este sistema no hay
juego — cada verbo de gameplay del jugador pasa por acá.

## Player Fantasy

El jugador debe sentirse como una **tormenta de metal en flow constante**:
una fuerza que atrae el peligro, lo retiene bajo tensión y lo libera en
descargas encadenadas sin nunca estar parado. La fantasía es de **combate en
estado de fluidez**, no de "cargar y disparar":

1. **Pull — el peligro viene a vos.** Mientras te movés, el campo queda
   abierto tras el primer click. Chatarra resbala por el piso. Enemigos
   magnetizados son arrastrados contra su voluntad hacia un punto frente al
   jugador. La pantalla se llena de cosas orbitando y amenazas retenidas. Es
   el momento de "yo soy la tormenta".
2. **Hold — leer el caos.** La órbita se llena, te movés más lento (-20%
   con carga completa), la sobrecarga sube. Estás midiendo el campo,
   ajustando posición, eligiendo el ángulo del cono. La decisión no es
   "seguir cargando o repeler", es "repeler **dónde** y **cuándo** dentro de un combo".
3. **Repel — liberación dirigida.** Explosión direccional satisfactoria.
   Minas detonan, enemigos chocan contra paredes, los números de combo se
   apilan. El siguiente Pull empieza antes de que termine la animación.

### Referencias

- **Hades** — ritmo de attack/special/dash en flujo continuo. Magnet Panic
  equivalentes: Pull / Repel / Strike. La fantasía es **estado de flow**,
  no acciones discretas con tiempo muerto entre medio.
- **Hi-Fi Rush** — sensación de que cada acción se enlaza con la siguiente
  y el posicionamiento es parte del ritmo.
- **Half-Life 2 Gravity Gun** — placer físico de agarrar y disparar objetos
  arbitrarios. Aporta el *feel* táctil de mover metal, no la estructura del
  combate (HL2 es puntual, Magnet Panic es sostenido).

### Por qué importa

Este es un **sistema que el jugador ama usar**, no infraestructura
silenciosa. Debe sentirse pesado en cada beat individual y fluido entre
beats. Sirve a:
- **P2** (Pull/Strike/Repel) — es 2 de los 3 verbos del juego.
- **P3** (el daño grande viene del magnetismo) — fuente principal de daño.
- **P4** (riesgo/recompensa) — la retención es riesgo creciente explícito.

Si Pull/Repel no entra en flow en 30 segundos de juego, fallamos el
Criterio #2 de éxito en `game-concept.md` ("atraer y repeler se siente
bien"). Esto justifica prototipar este sistema con `/prototype` antes de
cerrar el GDD.

## Detailed Design

El sistema cubre tres sub-mecánicas (**Pull**, **Orbit**, **Repel**) y es
dueño de dos contratos cross-system: el estado de **marca magnética** y el
recurso **carga actual**.

### Core Rules

#### Pull

1. Activado por el intento `PullToggle` (click izquierdo en el
   default; abstraído por `input-system`).
2. Mientras `PullActive == true`, se proyecta un campo magnético circular de
   radio `pullRadius` (default 5 m) centrado en `playerPosition`.
3. Cada frame, todo `IAttractable` con su pivot dentro del campo recibe
   una fuerza hacia el jugador con magnitud `pullSpeed × (1 / mass)`,
   limitada por `pullSpeedMax`. La velocidad efectiva no es uniforme:
   chatarra liviana llega rápido, objetos pesados llegan lento (P4 —
   riesgo/recompensa).
4. Los enemigos sólo son afectados por Pull si `MarkState == Magnetizado` o
   si tienen `AlwaysPullableByMagnet` activo, como los enemigos metálicos.
   Enemigos `Marcado` o `Normal` sin esa propiedad ignoran el campo.
5. El jugador puede moverse y apuntar mientras Pull está activo.
6. Pull no consume carga; *alimenta* la órbita.
7. Pull no tiene cooldown propio. La transición a Repel ocurre con el
   siguiente click izquierdo.

#### Orbit

1. Cuando un attractable alcanza distancia ≤ `orbitRadius` (default 1.5 m)
   del jugador, se evalúa la entrada al ring.
2. Si `currentCharge + objectMass ≤ maxCapacity`: el objeto entra al ring.
   Se asigna un slot equidistante con los demás objetos en órbita.
3. Si `currentCharge + objectMass > maxCapacity`: el objeto **rebota fuera**
   del campo (velocidad radial opuesta al jugador, magnitud
   `orbitRejectSpeed`). Queda nuevamente atraíble si vuelve a entrar al
   radio. **No se expulsa nada de lo que ya está orbitando.**
4. La órbita es determinística: N objetos uniformemente espaciados sobre
   un círculo de radio `orbitRadius`, girando a `orbitAngularSpeed` rad/s
   (sentido fijo). No hay física caótica.
5. Cuando el jugador se mueve, los objetos en órbita lo siguen
   rígidamente (la órbita se traslada con el jugador).
6. Los objetos en órbita **bloquean parcialmente proyectiles enemigos**:
   los proyectiles que colisionen con un objeto en órbita son destruidos
   (placas absorben mejor; chatarra puede dejar pasar por hueco).
7. Los objetos en órbita **dañan levemente por contacto** a enemigos que
   entren al radio de órbita (`orbitContactDamage`, daño por segundo).
8. La capacidad del jugador es modulada por upgrades (`+3 capacidad
   máxima` → `maxCapacity += 3`).

#### Repel

1. Activado por el intento `RepelClick` (segundo click izquierdo mientras Pull
   está activo o hay carga retenida).
2. **Cooldown global** de `repelCooldown` (default 0.25 s) tras cualquier
   Repel. Mientras esté en cooldown, `PullToggle` se ignora hasta que
   termine.
3. Al ejecutar Repel:
   - Se calcula el cono frontal de ángulo `repelConeAngle` (default 50°)
     centrado en la dirección `playerPosition → Aim`.
   - Cada objeto orbitando es convertido en proyectil. Su dirección
     inicial es un ángulo aleatorio dentro del cono. Su velocidad inicial
     es `repelSpeed × objectSpeedMod` (chatarra rápida, pesado lento).
   - El objeto deja la órbita; `currentCharge` baja por `objectMass`.
4. **Dry whoosh:** si `currentCharge == 0` al repeler, el sistema dispara el
   evento `OnRepelFired(empty=true)`, reproduce el VFX/SFX reducido, y
   aplica el cooldown completo. **No** se penaliza al jugador con un
   cooldown extra ni con stagger.
5. Cada proyectil vive según su `IAttractable.OnImpact` (ver tabla abajo).
6. El cooldown corre incluso si el dry whoosh se produjo (evita spam).

#### Comportamiento de proyectiles repelidos por tipo

| Tipo | `mass` | `objectSpeedMod` | `damage` | `OnImpact` |
|---|---:|---:|---:|---|
| Chatarra liviana | 1 | 1.4× | bajo | Single-hit. Aplica daño + knockback leve. Se consume. |
| Placa metálica | 2 | 1.0× | medio | **Pierce 2 enemigos** antes de consumirse. Aplica daño + knockback medio en cada hit. |
| Mina metálica | 2 | 0.8× | alto (AOE) | Detona en primer impacto: daño AOE en `mineExplosionRadius` (~2.5 m). Inmune a auto-detonación durante 0.5 s tras ser atraída. |
| Objeto pesado | 3 | 0.6× | alto | Single-hit, knockback grande, atraviesa hitstop más largo. |

#### Marca magnética (contrato)

`magnetism-system` define el contrato; `enemy-system` hospeda el estado
por instancia; `combat-system` lo escribe; `magnetism-system` lo lee.

| Estado | Cómo se aplica | Efecto | Decay |
|---|---|---|---|
| Normal | Default al spawn | Pull no lo afecta. | — |
| Marcado (1 stack) | `combat-system` invoca `IMarkable.ApplyMark()` (Strike hit) | Bonus +20% daño cuando es golpeado por chatarra repelida. | 6 s sin nuevo Strike → vuelve a Normal |
| Magnetizado (2 stacks) | Segundo Strike hit dentro de 6 s del primero | Atraíble por Pull. Puede ser repelido como proyectil contra otros enemigos. | 6 s sin nuevo Strike → step-down a Marcado |
| Aturdido | (Fuera de MVP) | — | — |

API expuesta por magnetism:

```
IMarkable.MarkState : enum { Normal, Marcado, Magnetizado, Aturdido }
IMarkable.ApplyMark(stacks: int) : void
IMarkable.SetMarkState(state: MarkState) : void  // bypass de stacks; usado por Counter
IMarkable.GetTimeSinceLastMark() : float
```

#### Carga actual (recurso)

```
currentCharge: float ∈ [0, maxCapacity]
maxCapacity: float (default 8, modificable por upgrades)
```

Reglas:
- `currentCharge += objectMass` cuando un objeto entra al ring.
- `currentCharge -= objectMass` cuando un objeto deja el ring (Repel,
  Overload eject, o despawn).
- `currentCharge` se reporta cada frame al `hud-system` (barra de
  capacidad) y al `overload-system` (pressure input).

Eventos emitidos:
- `OnChargeAdded(delta, source)`
- `OnChargeRemoved(delta, source)`
- `OnChargeFull()` — disparado al alcanzar `maxCapacity` (consumido por
  HUD para feedback visual).

---

### States and Transitions

#### Player-magnetism FSM

```
        ┌──────┐  PullToggle ┌─────────┐  object reaches player  ┌──────────┐
        │ Idle │ ──────────> │ Pulling │ ──────────────────────> │ Orbiting │
        └──────┘            └─────────┘                         └──────────┘
           ▲                    │  RepelClick                        │
           │                    │  (currentCharge == 0)              │
           │                    │  ──> dry whoosh                    │
           │                    ▼                                    │
           │              ┌──────────────┐  RepelClick (charge>0)    │
           │              │  Cooldown    │ <───────────────────────  │
           │              │  (0.25s)     │                           │
           │              └──────────────┘                           │
           │                    │                                    │
           │                    │  cooldown done                     │
           └────────────────────┴────── ForcedEject (overload) <─────┘
```

Transiciones:

| Desde | Evento | A | Side effect |
|---|---|---|---|
| Idle | `PullToggle` | Pulling | Activa campo magnético, VFX |
| Pulling | object enters orbit | Orbiting | `currentCharge += mass` |
| Pulling | `RepelClick` (charge==0) | Cooldown | Dry whoosh VFX/SFX |
| Orbiting | `RepelClick` (charge>0) | Repelling | Convierte objetos en proyectiles |
| Repelling | animación termina | Cooldown | — |
| Cooldown | timer 0.25s | Idle | Listo para nuevo Pull |
| Orbiting/Pulling | `OverloadTriggered` event | ForcedEject | Expulsa todo, vacía `currentCharge` |
| ForcedEject | done | Cooldown | — |

#### Per-object FSM

```
InWorld ──in pullRadius──> Attracting ──dist ≤ orbitRadius──> InOrbit
InOrbit ──Repel──> Projectile ──OnImpact──> Consumed (despawn)
InOrbit ──ForcedEject──> InWorld (radial pushback)
Attracting ──out of radius / RepelClick──> InWorld
```

| Desde | Evento | A | Notas |
|---|---|---|---|
| InWorld | dentro de `pullRadius` y Pull activo | Attracting | Aplica fuerza cada frame |
| Attracting | distancia ≤ `orbitRadius` y capacidad OK | InOrbit | Asigna slot |
| Attracting | capacidad llena | InWorld (rebote) | `orbitRejectSpeed` radial |
| Attracting | RepelClick o sale del radio | InWorld | Pierde fuerza, deja de atraer |
| InOrbit | Repel | Projectile | Disparado en cono |
| Projectile | OnImpact | Consumed | Despawn vía pool |
| InOrbit | ForcedEject (overload) | InWorld | Radial pushback fuerte |

#### Mark FSM (en enemy-system, contrato definido aquí)

```
Normal ──Strike──> Marcado ──Strike <6s──> Magnetizado
Marcado ──6s sin Strike──> Normal
Magnetizado ──6s sin Strike──> Marcado ──6s más──> Normal
Magnetizado ──Pulled + Repelled──> Normal (retenido al frente y disparado)
```

| Desde | Evento | A |
|---|---|---|
| Normal | Strike hit | Marcado |
| Marcado | Strike hit dentro de 6s | Magnetizado |
| Marcado | 6s sin Strike | Normal |
| Magnetizado | 6s sin Strike | Marcado |
| Magnetizado | Pulled + Repelled (retenido al frente y lanzado contra otro enemigo o pared) | Normal |

---

### Interactions with Other Systems

| Sistema | Dirección | Interfaz / Datos |
|---|---|---|
| `input-system` | in | Lee `PullToggle`/`RepelClick` (click izquierdo contextual) y `Aim` (Vector3 cursor world pos). El sistema desconoce qué tecla mapea — sólo escucha intents. |
| `player-movement` | in (read) + out (modifier) | Lee `playerPosition`, `playerFacing`. Escribe `chargePenalty: float ∈ [0,1]` calculado como `currentCharge / maxCapacity` × `chargePenaltyMax` (default 0.2). `player-movement` aplica este modificador a su `baseSpeed`. |
| `object-pooling` | in | Spawn/despawn de proyectiles in flight. API: `pool.Spawn(prefabId, pos, vel)` → `Handle`; `pool.Despawn(handle)`. |
| `attractables-system` | in (contract consumer) | Consume `IAttractable` interface: `mass`, `objectSpeedMod`, `OnEnterOrbit()`, `OnLeaveOrbit()`, `OnRepel(direction, force)`, `OnImpact(target, hitInfo)`. Cada attractable owns su comportamiento de impacto. |
| `enemy-system` | bidirectional | Define el contrato `IMarkable`; enemy-system lo implementa por instancia. magnetism lee `MarkState` durante Pull (decide a qué enemigos atraer). magnetism también lee `enemy.position` para queries en radio. |
| `combat-system` | out (contract owner) | Provee `IMarkable.ApplyMark(stacks)` (Strike) y `IMarkable.SetMarkState(state)` (Counter). magnetism define reglas de transición y decay; combat sólo aplica. |
| `damage-health-system` | out | Cuando un proyectil impacta, llama a `damage(target, amount, type, source=projectileType)`. magnetism no calcula daño final; sólo dispara el evento con `amount` base del attractable. damage-health resuelve resistencias. |
| `overload-system` | out (publish) + in (subscribe) | Publica `currentCharge`, `chargeDelta` por frame. Subscribe a `OverloadTriggered` event → ejecuta ForcedEject. |
| `upgrade-system` | in (modifiers) | Recibe runtime modifiers: `pullRadius += X`, `pullSpeed *= X`, `repelSpeed *= X`, `maxCapacity += X`, `repelDamageMod`, `magneticGripRange` (Pull de magnetizados desde más lejos), etc. |
| `powerup-system` | in (timed effects) | Recibe efectos temporales: `Repeler360` (próxima Repel es radial 360°), `MagnetFever` (`pullRadius`/`pullSpeed` aumentados durante 8s), `EnemyPull` (Pull afecta enemigos pequeños no-magnetizados durante 6s). |
| `scoring-xp-system` | out (events) | Emite `OnObjectOrbited`, `OnEnemyMagnetized` (después de Pulled), `OnRepelHit(numTargets)`, `OnEnemyKilledByRepel`. scoring filtra estos eventos para puntos de estilo. |
| `mission-system` | out (events) | Mismos eventos que scoring. mission cuenta para misiones tipo "Combo Hunter", "Scrap Collector", "Wall Slam" (cuando el target del impact es una pared). |
| `hud-system` | out (publish) | Publica `currentCharge`, `maxCapacity`, `pullActive`, `repelCooldownRemaining`, `repelConeDirection`. HUD renderiza barra de capacidad, indicador de cono de repulsión, alerta de full. |
| `presentation-system` | out (events) | Emite eventos para SFX/VFX/screen shake/hitstop: `OnPullStart`, `OnObjectAttracted`, `OnObjectOrbited`, `OnRepelFired(empty)`, `OnProjectileImpact`, `OnEnemyMagnetizedRepelled`. |

#### Notas de ownership

- **Contrato `IMarkable`** → owned por `magnetism-system`. Definir acá las
  enum, las reglas de stacks y decay. `enemy-system` hospeda el estado.
  `combat-system` lo escribe vía API.
- **Recurso `currentCharge`** → owned por `magnetism-system`.
  `overload-system` lee, no escribe directo.
- **Damage de proyectiles** → `magnetism-system` orquesta el lanzamiento,
  pero el cálculo de daño final lo resuelve `damage-health-system` con el
  `amount` base que envía el attractable.

## Formulas

Todas las constantes son **valores de arranque**. El balance real se hace
con el juego corriendo (criterios de éxito #2 y #4 dependen del *feel*, no
del paper math). Cualquier número marcado con (*) viene literal del GDD;
los demás son extrapolaciones consistentes.

### Pull force por objeto (por frame)

```
direction     = normalize(playerPos - O.position)
desiredSpeed  = min(pullSpeed / O.mass, pullSpeedMax)
O.velocity    = lerp(O.velocity, direction × desiredSpeed, pullAccel × dt)
```

| Variable | Default | Notas |
|---|---:|---|
| `pullSpeed` | 8 m/s | Numerador antes de mass-divide |
| `pullSpeedMax` | 12 m/s | Cap absoluto |
| `pullAccel` | 8 | Lerp factor — alto = snappy |

**Velocidades efectivas resultantes:** chatarra (mass 1) = 8 m/s · placa
(2) = 4 m/s · mina (2) = 4 m/s · pesado (3) = ~2.7 m/s.

### Penalización de movimiento por carga

```
chargeRatio   = currentCharge / maxCapacity   ∈ [0, 1]
playerSpeed   = baseSpeed × (1 − chargeRatio × chargePenaltyMax)
```

| Variable | Default | Notas |
|---|---:|---|
| `baseSpeed` | 5 m/s (*) | GDD §6.1 |
| `chargePenaltyMax` | 0.20 (*) | GDD §6.1 ("-20% con carga completa") |

A full charge: `playerSpeed = 5 × 0.8 = 4 m/s`.

### Posiciones en órbita (determinístico)

Para N objetos en órbita, objeto `i` en tiempo `t`:

```
α_i(t)   = (2π × i / N) + orbitAngularSpeed × t
pos_i    = playerPos + (cos α_i, sin α_i) × orbitRadius
```

| Variable | Default | Notas |
|---|---:|---|
| `orbitRadius` | 1.5 m | Distancia visual cómoda |
| `orbitAngularSpeed` | 4.2 rad/s | ~1 vuelta cada 1.5 s |

Cuando N cambia (objeto entra/sale), reflow uniforme con lerp de **0.15 s**
para evitar snap visual.

### Repel cone — dirección y velocidad por proyectil

```
aimDir          = normalize(Aim - playerPos)
randomOffset    = uniform(−repelConeAngle/2, +repelConeAngle/2)
projectileDir   = rotate(aimDir, randomOffset)
projectileSpeed = repelSpeed × O.objectSpeedMod
```

| Variable | Default | Notas |
|---|---:|---|
| `repelConeAngle` | 50° (*) | GDD §6.4 |
| `repelCooldown` | 0.25 s (*) | GDD §6.4 |
| `repelSpeed` | 18 m/s | Visiblemente rápido pero trazable |

### Mina — falloff de explosión (lineal)

Al impactar, para cada entidad `E` dentro de `mineExplosionRadius`:

```
distance = ||E.position − impactPos||
damage   = mineBaseDamage × max(0, 1 − distance / mineExplosionRadius)
```

| Variable | Default | Notas |
|---|---:|---|
| `mineBaseDamage` | 25 | En el centro |
| `mineExplosionRadius` | 2.5 m | Compacto, no domina la arena |
| `mineImmunityTime` | 0.5 s (*) | Tras ser atraída no detona |

### Mark decay (step-down)

Cada frame, para cada enemigo con `markState != Normal`:

```
si (now − lastStrikeTime > markDecaySeconds):
  si (markState == Magnetizado): markState = Marcado;  lastStrikeTime = now
  sino si (markState == Marcado): markState = Normal
```

| Variable | Default | Notas |
|---|---:|---|
| `markDecaySeconds` | 6 s | Step-down, no reset duro |

### Pull eligibility de enemigo

```
canPullEnemy = (enemy.MarkState == Magnetizado || enemy.AlwaysPullableByMagnet)
            && (enemy.mass ≤ pullableEnemyMaxMass)
```

| Variable | Default | Notas |
|---|---:|---|
| `pullableEnemyMaxMass` | 4 | Sube a 6 con upgrade *Garra magnética* |

Boss (`mass` ≈ 10) **nunca** es atraído. El upgrade *Garra magnética* no
abre la puerta al boss; sólo a Shield Bot pesado.

### Tabla de daños base de proyectil

Estos son los `amount` que `magnetism-system` envía a
`damage-health-system`. La resolución final (resistencias, invulnerabilidad,
multipliers) la hace `damage-health-system`.

| Fuente | Base damage | Tipo |
|---|---:|---|
| Chatarra repel impact | 5 | Kinetic |
| Placa repel impact (cada pierce) | 10 | Kinetic |
| Mina centro | 25 | Explosive (falloff lineal a 0) |
| Pesado repel impact | 20 | Kinetic + Knockback alto |
| Orbit contact (continuo) | 1/s | Kinetic |
| Enemigo magnetizado lanzado vs target | 15 | Kinetic + Knockback |
| Wall slam bonus (impacto contra pared) | +5 | Impact |

**Referencia de HP de enemigos** (definitivos en `enemy-system`):
Scrapling ~10 · Runner ~15 · Shield Bot ~30 (frontal bloqueado) · Spitter
~12 · Scrap Brute ~150.

Implicaciones intencionadas:
- Una placa repelida one-shottea Scraplings y casi mata Runner.
- Una mina centrada mata Scrapling+Runner cerca; daña al Shield Bot ~25 si
  pega de costado.
- Un enemigo magnetizado lanzado contra otro causa 15 + 5 wall slam si
  además pega contra pared = 20.

## Edge Cases

### Boundary values

- `currentCharge == 0` con Repel → dry whoosh (cubierto en Detailed Design).
- `currentCharge == maxCapacity` con Pull intentando agregar más → rebote
  fuera del campo (cubierto).
- `pullRadius == 0` (caso patológico de modificador adverso): Pull no
  atrae nada. No error, no log. Diseño defensivo.
- Upgrade aumenta o reduce `maxCapacity` con objetos ya en órbita:
  - Si el nuevo `maxCapacity ≥ currentCharge` → no acción.
  - Si el nuevo `maxCapacity < currentCharge` → eject del exceso por
    antigüedad (oldest first) hasta cumplir, restando `chargeDelta` y
    emitiendo `OnChargeRemoved` por cada eject.

### Race conditions / simultáneos

- 2 Strikes en mismo frame al mismo enemigo Normal → suma 2 stacks → llega
  directo a Magnetizado, `lastStrikeTime = now` única.
- Strike + chatarra repelida impactando al mismo enemigo en mismo frame →
  el orden lo resuelve `damage-health-system` por timestamp; convención:
  Strike se procesa primero (input local del jugador es síncrono al frame
  actual), luego el bonus por chatarra repelida ya cuenta el estado
  Marcado.
- Pull radius cubre a magnetized enemy el preciso frame de su muerte →
  cancelar Attracting, marcar InWorld inmediatamente, no entrar a InOrbit
  (evita "fantasma" en órbita).
- Mina con timer de inmunidad activo es repelida antes de armar → su
  `OnImpact` se ejecuta normal: la inmunidad sólo aplica a
  auto-detonación dentro del radio del jugador, no al impacto post-Repel.

### Lifecycle / muerte

- Jugador muere mientras orbita N objetos → todos pasan a InWorld con
  velocidad radial pequeña (`deathScatterSpeed` ≈ 4 m/s). Pool los
  despawnea tras `corpseLifetime` (~3 s) si no son re-atraídos.
- Enemy magnetizado muere por daño no-jugador (otro enemigo o ambiente)
  mientras está Attracting → abort, dissolve corpse, no cuenta como kill
  para `scoring-xp-system` por magnetism.
- Enemy con `mass > pullableEnemyMaxMass` magnetizado por Strike: queda
  Magnetizado pero **no es atraído**. Diseño expreso: el indicador visual
  aparece, el jugador aprende que ese enemigo es "demasiado pesado", el
  upgrade *Garra magnética* lo desbloquea. No es bug, es señalización.

### Exploits previstos (autobalanceados, no bloqueados)

- **Spam Pull/Repel vacío:** respeta cooldown 0.25 s, no gana nada. No
  bloquear.
- **Órbita indefinida para farm de orbit contact damage:** contrarrestado
  por `overload-system`, que escala con tiempo en alta carga, fuerza
  ForcedEject y deja al jugador vulnerable 0.5 s (GDD §6.9).
- **Magnetized enemy como escudo orbital:** ya no aplica al prototype actual.
  El enemigo magnetizado se retiene al frente del jugador, estilo
  atracción/repulsión dirigida, y no entra a la órbita de chatarra.

### Powerups y modificadores

- `MagnetFever` expira mientras Pull activo: `pullRadius` y `pullSpeed`
  hacen lerp-down al default durante 0.5 s para evitar snap visual y
  pérdida brusca de objetos atraídos.
- `Repeler360` con orbit vacío: dry whoosh radial (sin proyectiles).
  Cooldown aplicado. **El powerup NO se consume** — espera al próximo
  Repel con carga. Decisión: el powerup vale lo suficiente como para no
  quemarse en input vacío.
- `EnemyPull` activo + enemigo ya Magnetizado: doble elegibilidad no
  duplica fuerza, sólo lo mantiene en el pool de atraíbles.

### WebGL / runtime

- Tab pierde foco / browser pause: `Time.deltaTime` puede saltar al
  resume. Clamp `dt` a `dtMax = 0.05 s` por frame para evitar saltos de
  decay y posiciones de órbita.
- Object pool exhausto en momento de Repel: log warning, skip spawn del
  proyectil afectado, los demás objetos del Repel se disparan normal.
  El objeto sin spawn se considera "consumido" igual (currentCharge
  baja).
- Cursor (`Aim`) fuera de la arena: Repel dispara hacia esa dirección;
  proyectiles viajan hasta `projectileMaxDistance` (≈30 m) y despawnean.

### Asunciones provisionales sobre dependencias no diseñadas

Marcamos estas explícitamente porque las dependencias todavía no tienen
GDD. Si el GDD final difiere, este sistema se ajusta:

- **`damage-health-system`:** asumimos API `Damage(target, amount, type,
  source)` queueada y resuelta al final del frame. Resistencias/multipliers
  resueltos allí, no acá.
- **`overload-system`:** asumimos que escucha `currentCharge` y publica
  evento `OverloadTriggered` que magnetism procesa al inicio del próximo
  frame para ejecutar ForcedEject.
- **`enemy-system`:** asumimos `EnemyData.mass`, `EnemyData.MarkState`
  (vía `IMarkable`), y `Transform.position` accesibles. Los enemigos
  hospedan el estado; magnetism nunca lo cachea entre frames.
- **`object-pooling`:** asumimos pools dimensionados para 30 proyectiles
  simultáneos, 50 attractables in-world, 16 partículas en flight.

## Dependencies

### Upstream (sistemas de los que `magnetism-system` depende)

| Dep | Hard/Soft | Interfaz | Status |
|---|---|---|---|
| `input-system` | Hard | Lee `PullToggle`/`RepelClick` contextual y `Aim` (Vector3) | Not Designed (asunción provisional) |
| `player-movement` | Hard | Lee `playerPosition`, `playerFacing`. Escribe `chargePenalty: float ∈ [0,1]` | Not Designed (asunción) |
| `object-pooling` | Hard | `pool.Spawn(prefabId, pos, vel) → Handle`; `pool.Despawn(handle)` | Not Designed (asunción) |
| `attractables-system` | Hard | Consume `IAttractable` interface (definido en este GDD) | Not Designed |
| `damage-health-system` | Hard | `Damage(target, amount, type, source)` queueado al final de frame | Not Designed (asunción) |
| `enemy-system` | Soft | Lee `IMarkable.MarkState`, `enemy.mass`, `Transform.position` | Not Designed |
| `combat-system` | Soft | `combat-system` invoca `IMarkable.ApplyMark(stacks)` | Not Designed |
| `overload-system` | Soft | Magnetism publica `currentCharge`/`chargeDelta`; subscribe a `OverloadTriggered` | Not Designed |
| `upgrade-system` | Soft | Recibe modifiers runtime (ver Tuning Knobs) | Not Designed |
| `powerup-system` | Soft | Recibe efectos timed (`Repeler360`, `MagnetFever`, `EnemyPull`) | Not Designed |

**Hards** son los 5 primeros: sin ellos magnetism no compila/corre. Las
**softs** son enriquecimientos: sin combat ni overload el sistema
funciona pero el juego pierde profundidad táctica. **Sin combat,
enemy-system y overload, magnetism todavía permite "atraer chatarra y
dispararla a paredes"** — eso es el prototipo mínimo válido para
validar Criterio #2 de éxito (atraer y repeler se siente bien).

### Downstream (sistemas que dependen de `magnetism-system`)

| Sistema | Razón |
|---|---|
| `overload-system` | Consume `currentCharge` y eventos de carga |
| `attractables-system` | Implementa contrato `IAttractable` definido aquí |
| `enemy-system` | Implementa `IMarkable` definido aquí |
| `combat-system` | Escribe en `IMarkable` (Strike → Marcado/Magnetizado) |
| `scoring-xp-system` | Subscribe a `OnRepelHit`, `OnEnemyKilledByRepel`, `OnEnemyMagnetized` |
| `mission-system` | Mismas subscripciones para Combo Hunter / Wall Slam / Scrap Collector |
| `upgrade-system` | Aplica modifiers a `pullRadius`, `pullSpeed`, `repelSpeed`, `maxCapacity`, etc. |
| `powerup-system` | Aplica efectos timed |
| `hud-system` | Renderiza barra de capacidad, indicador de cono de Repel |
| `presentation-system` | SFX/VFX/shake/hitstop para todos los eventos |

### Contratos definidos por `magnetism-system`

Estos son **propiedad de este GDD**. Cualquier cambio acá debe propagarse
a los consumidores.

1. **`IAttractable`** — interfaz que objetos del mundo implementan:
   - `mass: float`
   - `objectSpeedMod: float`
   - `OnEnterOrbit() : void`
   - `OnLeaveOrbit() : void`
   - `OnRepel(direction: Vector3, force: float) : void`
   - `OnImpact(target: GameObject, hitInfo: HitInfo) : void`

2. **`IMarkable`** — interfaz para entidades marcables:
   - `MarkState : enum { Normal, Marcado, Magnetizado, Aturdido }`
   - `ApplyMark(stacks: int) : void` — suma stacks aplicando reglas de
     transición. Usado por Strike (combat).
   - `SetMarkState(state: MarkState) : void` — fuerza el estado sin pasar
     por stacks. **Solo usado por Counter** (combat) para magnetizar al
     atacante directamente. Reset de `lastStrikeTime = now` al invocar.
   - `GetTimeSinceLastMark() : float`

3. **`currentCharge` resource** — single source of truth, owned aquí.
   Otros sistemas leen vía publicación de eventos / queries de read-only.

### Eventos publicados (consumibles por cualquier sistema)

```
OnPullStart()
OnPullEnd(empty: bool)
OnObjectAttracting(handle, type)
OnObjectOrbited(handle, type)
OnRepelFired(numProjectiles, empty: bool)
OnProjectileImpact(handle, target, position, hitType)
OnEnemyMagnetizedRepelled(handle, hitTarget)
OnChargeAdded(delta, source)
OnChargeRemoved(delta, source)
OnChargeFull()
OnForcedEject(reason)
```

### Notas de bidireccional consistency

Cuando este GDD se cierre, actualizar `design/gdd/systems-index.md`:
- Agregar `damage-health-system` a la lista de dependencies upstream de
  `magnetism-system` (no estaba en el mapa de capas original; surgió
  durante este diseño como Hard dep para resolver impactos).
- Confirmar que el index ya refleja Bottleneck ⚠ con ~8 dependientes — sí,
  está marcado.

## Tuning Knobs

Tabla canónica para ScriptableObject de configuración. Valores marcados con
(*) vienen literal del GDD jam y son decisiones de pilar — no tunear sin
razón fuerte.

| Knob | Default | Safe range | Extremos |
|---|---:|---|---|
| `pullRadius` | 5 m (*) | 3 – 10 | Bajo: chatarra escasa, frustrante. Alto: todo viene solo, fantasy perdida. |
| `pullSpeed` | 8 m/s | 4 – 15 | Bajo: rompe flow. Alto: snap instantáneo, sin tensión. |
| `pullSpeedMax` | 12 m/s | 8 – 20 | Cap absoluto sin importar mass. |
| `pullAccel` | 8 | 4 – 16 | 4 = floaty, 16 = snap. Afecta sensación de "atraer". |
| `maxCapacity` | 8 (*) | 4 – 16 | Bajo: Repel insignificante. Alto: sobrecarga inerte. |
| `chargePenaltyMax` | 0.20 (*) | 0 – 0.5 | 0: sin riesgo (P4 muerto). 0.5: full charge unplayable. |
| `orbitRadius` | 1.5 m | 1 – 3 | <1: clip con player collider. >3: tapa visión. |
| `orbitAngularSpeed` | 4.2 rad/s | 2 – 8 | Cosmético; afecta legibilidad. |
| `orbitContactDamage` | 1/s | 0 – 3 | >5/s: orbit-farming exploit. |
| `orbitRejectSpeed` | 3 m/s | 1 – 6 | Cosmético. |
| `repelConeAngle` | 50° (*) | 20 – 90 | <20°: combos imposibles. >90°: spray, sin skill. |
| `repelCooldown` | 0.25 s (*) | 0.1 – 0.5 | <0.1: spam. >0.5: rompe flow combat (P2). |
| `repelSpeed` | 18 m/s | 10 – 30 | Bajo: enemigos esquivan. Alto: visual untrackable. |
| `mineBaseDamage` | 25 | 10 – 50 | Calibrar contra Brute HP 150 y enemy bunching. |
| `mineExplosionRadius` | 2.5 m | 1.5 – 5 | >5: una mina mata oleada entera, sin decisión. |
| `mineImmunityTime` | 0.5 s (*) | 0.3 – 1 | <0.3: self-damage. >1: minas se sienten laggy. |
| `markDecaySeconds` | 6 s | 3 – 10 | <3: Strike-Strike-Pull casi imposible. >10: stockpile pre-mark. |
| `pullableEnemyMaxMass` | 4 | 2 – 8 | 2: solo Scraplings. 8: Brute pulleable (rompe boss). |
| `dtMax` (frame clamp) | 0.05 s | 0.03 – 0.1 | WebGL safety. |
| `projectileMaxDistance` | 30 m | 15 – 60 | Pool concern, no feel. |
| `deathScatterSpeed` | 4 m/s | 2 – 8 | Cosmético al morir. |

### Knobs que interactúan (tunear juntos)

- **`pullRadius` × `pullSpeed`** — ambos altos = invencibilidad. Si subís
  uno, bajar el otro.
- **`maxCapacity` × `chargePenaltyMax`** — más capacidad con misma
  penalización → menos penalty por punto, riesgo diluido. Subir capacidad
  implica subir penalty proporcional.
- **`repelConeAngle` × `repelSpeed`** — cono ancho con proyectiles lentos
  = imposible combos. Cono angosto con proyectiles rápidos = sniper.
  Tunear como par.
- **`markDecaySeconds` × `repelCooldown`** — si el cooldown crece, decay
  debería crecer también para que el ritmo Strike-Strike-Repel siga
  viable.

### Modifiers que `upgrade-system` aplica sobre estos knobs

`upgrade-system` no debe duplicar estos valores en su lado — sólo los
modifica vía API expuesta por magnetism (`magnetism.AddModifier(knob, op,
value)`).

| Upgrade | Knob | Operación |
|---|---|---|
| Campo amplio | `pullRadius` | × 1.20 |
| Bobina rápida | `pullSpeed` | × 1.25 |
| Garra magnética | `pullableEnemyMaxMass` | + 2 |
| Cañón de chatarra | `repelSpeed` | × 1.25 |
| Impacto brutal | (damage table base) | × 1.20 |
| Bolsillos magnéticos | `maxCapacity` | + 3 |
| Núcleo estable | (overload pressure rate) | × 0.80 — vive en `overload-system` |

`powerup-system` aplica modifiers timed por la misma API; los modifiers se
desapilan al expirar el powerup.

## Visual/Audio Requirements

`magnetism-system` no implementa VFX/SFX directamente — emite eventos
consumidos por `presentation-system`. Esta sección lista los **hooks que
magnetism garantiza emitir** y el feel objetivo, no las implementaciones
(esas viven en el GDD de `presentation-system`).

### Eventos emitidos para feedback

| Evento | Cuándo | Feel objetivo |
|---|---|---|
| `OnPullStart` | Click de `PullToggle` | Hum magnético low-end arranca, partículas radiales convergen al jugador |
| `OnPullActive` (por frame) | Mientras Pull activo | Loop modulado por `currentCharge` (sube en frecuencia con la carga) |
| `OnObjectAttracting(handle)` | Objeto entra al campo | Trail tenue del objeto al jugador |
| `OnObjectOrbited(handle, type)` | Objeto entra al ring | Click metálico distinto por type (chatarra: tink; placa: thunk; mina: clack-fizz; pesado: deep clunk) |
| `OnChargeFull` | `currentCharge == maxCapacity` | Pulso visual + tick high-pitched (alerta sin ser molesta) |
| `OnRepelFired(numProjectiles, empty)` | Repel con/sin carga | Empty: dry whoosh quiet. Con carga: punchy bang escalado por count |
| `OnProjectileImpact(handle, target)` | Proyectil pega | Crunch + hitstop 0.05 s |
| `OnMineExploded(pos)` | Mina detona | Boom low-pass + screen shake medio + flash |
| `OnEnemyMagnetizedRepelled(handle, target)` | Magnetizado se lanza | Sonido eléctrico desgarrando + trail magnético |
| `OnMarkApplied(stacks)` | Strike marca enemigo | Tick eléctrico crisp |
| `OnEnemyMagnetized` | 2 stacks aplicados | Loop eléctrico sutil sobre el enemigo (continuo hasta decay/muerte) |
| `OnForcedEject(reason)` | Overload trigger | Descarga radial violenta + screen shake fuerte + flash blanco breve |
| `OnPullEnd(empty)` | RepelClick | Hum se corta seco |

### Feel obligatorio (de GDD §15)

De los 9 elementos obligatorios, **magnetism es responsable directo de**:
sonido de atracción, sonido de objetos entrando en órbita, sonido potente
de repulsión, impactos claros, hitstop breve, screen shake moderado,
slow-motion en jugadas grandes. Los otros (combo popups, alerta de
sobrecarga) son responsabilidad de `scoring-xp-system` y
`overload-system`, pero magnetism aporta los eventos que los disparan.

### Camera hooks

- Screen shake: `OnRepelFired(empty=false)` → small. `OnMineExploded` →
  medium. `OnForcedEject` → heavy.
- Hitstop: `OnProjectileImpact` → 0.05 s. `OnEnemyMagnetizedRepelled` →
  0.08 s.
- Slow-mo: combo de 5+ kills derivado de un solo Repel → 0.5× tiempo por
  0.4 s. Driver: `scoring-xp-system` lee eventos y dispara la slow-mo;
  magnetism sólo emite la materia prima.

## UI Requirements

`hud-system` implementa los elementos. Esta sección define qué datos
expone magnetism y qué cues visuales son no-negociables para legibilidad.
HUD 2D usa UI Toolkit; ayudas espaciales como retícula, cono, glows y cues
sobre enemigos usan UI/meshes/VFX world-space de Unity.

### Datos publicados (read-only desde HUD)

```
magnetism.currentCharge      : float
magnetism.maxCapacity        : float
magnetism.pullActive         : bool
magnetism.repelCooldownLeft  : float (0 si listo)
magnetism.repelConeDirection : Vector3 (sólo válido cuando pullActive)
magnetism.repelConeAngle     : float (knob, raramente cambia)
magnetism.orbitedObjects     : List<{handle, type, mass}>
```

### Elementos UI no-negociables

1. **Barra de capacidad** — `currentCharge / maxCapacity`. Visible
   siempre. Pulsa al alcanzar full (`OnChargeFull`). Color por default;
   segmentos por type del objeto en órbita es opcional pero deseable.
2. **Indicador de cono de Repel** — cono semitransparente desde el
   jugador hacia `Aim`, ángulo `repelConeAngle`. **Visible cuando
   `pullActive == true` o hay payload retenido.** Anti screen clutter.
3. **Cooldown de Repel** — indicador radial pequeño cerca del reticle
   mientras `repelCooldownLeft > 0`.
4. **Indicador de enemigo Magnetizado** — glow world-space sobre cada
   enemigo Magnetizado. Color distinto al de Marcado (un stack).
5. **Indicador de "demasiado pesado"** — cuando un enemigo Magnetizado
   tiene `mass > pullableEnemyMaxMass`, mostrar un tick adicional ("masa
   excedida") para que el jugador entienda por qué no lo atrae. Resuelve
   confusión potencial.

### Anti-clutter

- `pullRadius` **no** se renderiza por default. El jugador aprende el
  alcance por feel. Setting de accesibilidad opcional para visualizarlo.
- Trails de attracting NO deben ser tan brillantes que tapen enemigos o
  el cursor.

## Acceptance Criteria

### Funcional

1. Click de `PullToggle` activa el campo dentro de 1 frame; `IAttractable` en
   `pullRadius` se mueve hacia el jugador con velocidad consistente con su
   `mass` y `objectSpeedMod`.
2. Segundo click con órbita > 0 dispara todos los objetos en cono
   `repelConeAngle`, vacía `currentCharge` a 0, inicia cooldown
   `repelCooldown`.
3. Segundo click con órbita == 0 ejecuta dry whoosh: sin proyectiles, mismo
   cooldown, sin penalización extra.
4. Strike (vía `combat-system`) aplica marca a enemigo Normal → Marcado;
   segundo Strike dentro de 6 s → Magnetizado. Pull arrastra Magnetizados
   con `mass ≤ pullableEnemyMaxMass`.
5. Marca decae step-down: 6 s sin Strike → Magnetizado → Marcado; otros
   6 s → Marcado → Normal.
6. Si `currentCharge + objectMass > maxCapacity`, el objeto entrante
   rebota radial con `orbitRejectSpeed`; los objetos en órbita no son
   expulsados.
7. Mina con immunity 0.5 s no detona dentro del campo. Tras Repel detona
   en primer impacto con falloff lineal en `mineExplosionRadius`.
8. `OverloadTriggered` ejecuta ForcedEject: vacía órbita
   instantáneamente, lleva `currentCharge` a 0, transita a Cooldown.
9. Upgrades modifican knobs en runtime sin restart; powerups aplican y
   desapilan modifiers timed.

### Feel (observable)

10. Un tester sin tutorial entiende el verbo Pull/Repel en menos de 30 s
    (apoya Criterio #2 de éxito de `game-concept.md`).
11. En una run típica de 4 min hay al menos un combo de 3+ enemigos
    derribados con un solo Repel.
12. La penalización de movimiento al full charge es perceptible (-20% del
    base, no invisible) sin ser frustrante.

### Performance (WebGL — hard requirement)

13. Tick de `magnetism-system` ≤ 0.5 ms por frame a 60 fps con 30
    attractables dentro de `pullRadius` y 8 objetos en órbita.
14. Cero allocations por frame en el hot path: query de attractables,
    update de órbita, lerp de carga. Pool reuse para todos los proyectiles
    en flight.
15. `dt` clamped a `dtMax = 0.05 s` cada update; tras tab refocus no hay
    simulation jumps visibles (objetos no teleportan, decay no salta).

### Cross-system

16. Writes a `IMarkable` desde `combat-system` son visibles a
    `magnetism-system` en el mismo frame.
17. `OverloadTriggered` procesado en el siguiente frame ejecuta
    ForcedEject sin orphan projectiles ni leaks de pool.
18. Enemigo Magnetizado lanzado por Repel impacta y aplica daño +
    knockback a enemigos secundarios (transitivo, vía
    `damage-health-system`).
19. Powerup `MagnetFever` expira mid-Pull: `pullRadius` / `pullSpeed`
    lerp al default durante 0.5 s, sin snap.

### QA first checks (5 min de validación)

20. ¿Se ve hacia dónde apuntás? Cono indicador visible cuando
    `currentCharge > 0`.
21. ¿Se sabe cuánta carga tenés? HUD bar consistente con número de
    objetos en órbita.
22. ¿Pull/Repel con 0 cooldown remaining permite el flujo
    Strike→Strike→Pull→Repel sin micro-pausa percibida?

### Resistencia a abuso

23. Pull spam (alternar PullToggle/RepelClick ~10 Hz durante 60 s): sin
    excepciones, sin pérdida de fps, pool sin overflow.
24. Repel apuntando fuera de arena: proyectiles vuelan hasta
    `projectileMaxDistance` y despawnean; pool libera handles.

## Open Questions

| # | Pregunta | Owner sugerido | Target |
|---|---|---|---|
| Q1 | ¿Renderizar `pullRadius` como anillo visual default-on, default-off, o setting de accesibilidad? | `ux-designer` + `tech-artist` | Tras prototipo (Día 2-3) |
| Q2 | ¿Los objetos en órbita colisionan entre sí o son intangibles? Diseño actual: intangibles (slot determinístico). Argumento contra: pierde "physicality". | `gameplay-programmer` | Prototipo Día 1 |
| Q3 | ¿Existe un input de "manual eject" para cancelar la órbita sin disparar? GDD no lo menciona; agregar tendría costo de input extra. | `game-designer` | Alpha balance (Día 9) |
| Q4 | ¿Cómo se comunica visualmente que el Pull aspira algo desde el límite del campo (efecto "succión")? Afecta el sentido del campo. | `tech-artist` | Día 2 |
| Q5 | Cuando upgrade *Garra magnética* sube `pullableEnemyMaxMass` a 6, ¿cue visual de que ahora Shield Bot es atraíble? | `ux-designer` | Diseño de `upgrade-system` |
| Q6 | ¿La marca persiste a través de "extracción" si el run termina con un boss magnetizado vivo? Edge irrelevante para jam pero conviene cerrarlo. | `game-designer` | Diseño de `meta-flow-system` |
| Q7 | ¿`presentation-system` decide la duración de la slow-mo o lo hace `scoring-xp-system`? Magnetism sólo emite la materia prima. | `game-designer` | Cuando se diseñen esos sistemas |
