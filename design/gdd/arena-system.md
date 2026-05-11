# Arena System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: El escenario es el arma — la arena provee paredes para wall slams, chatarra para atraer, y presión espacial para combos

## Overview

El Arena System define la geometría jugable, sus límites físicos y las reglas espaciales del nivel donde ocurre la run. Es un sistema Foundation de Layer 0 sin dependencias: provee el "escenario como arma" que el GDD describe como pilar #1. Paredes para wall slam, spawn points para enemigos y chatarra, zonas de peligro y colisiones estáticas — todo vive acá.

El jugador no interactúa con la arena como sistema, pero la siente constantemente: las paredes son herramientas (repeler enemigo → pared → daño extra), los bordes crean presión espacial (te arrinconan, generan peligro), y el layout determina qué jugadas son posibles. Un buen arena system hace que cada metro cuadrado del mapa tenga un propósito táctico.

Hoy la arena es un `Plane` escalado a 1.6x creado por `ArkhamCombatSetup.CreateArena()`. No tiene paredes, no tiene spawn points definidos, no tiene colisiones de borde. Los enemigos se colocan manualmente en el editor. Este GDD lleva esa base a un sistema completo.

## Player Fantasy

**"El mapa es mi caja de herramientas."**

El jugador debería sentir que el entorno trabaja para él — no es un espacio vacío donde peleas, es un arsenal ambiental. Cada pared es una oportunidad de wall slam, cada esquina es un kill zone si la usás bien. La fantasía es la de un héroe de película de acción que usa todo lo que tiene alrededor.

Referencia directa: **Hades** (cámaras cerradas donde paredes y pilares crean pockets tácticos), **Devil May Cry** (arenas cerradas con bordes que definen la zona de combate) y **Vampire Survivors** (presión espacial del borde del mapa como mecánica de riesgo).

La arena debería sentirse justa pero opresiva — suficientemente grande para esquivar y planear, suficientemente chica para que la densidad de combate nunca baje.

## Detailed Design

### Core Rules

#### Regla 1 — Geometría cerrada rectangular

La arena MVP es un rectángulo cerrado con paredes sólidas en los 4 bordes. No hay geometría interior compleja para la jam — la complejidad viene del contenido (enemigos, chatarra, minas), no del layout.

```
┌────────────────────────────────────────┐
│                                        │
│    [Spawn Zone NW]    [Spawn Zone NE]  │
│                                        │
│              ┌──────┐                  │
│              │Player│                  │
│              │Start │                  │
│              └──────┘                  │
│                                        │
│    [Spawn Zone SW]    [Spawn Zone SE]  │
│                                        │
└────────────────────────────────────────┘
```

Dimensiones MVP:

| Propiedad | Valor |
|---|---|
| Ancho (X) | 32 m |
| Largo (Z) | 32 m |
| Altura de paredes | 3 m |
| Player start | Centro (0, 0, 0) |

#### Regla 2 — Paredes como herramienta (Wall Slam)

Las paredes del borde de la arena son superficies de colisión sólida con un tag/layer especial `ArenaWall`. Cuando un enemigo repelido o empujado impacta contra una ArenaWall:

1. Se aplica **daño de impacto adicional** (`wallSlamDamage`).
2. Se genera **knockback reducido** (el enemigo rebota levemente desde la pared).
3. Se dispara un evento `OnWallSlam(enemy, wallNormal, damage)` para feedback visual/audio.
4. Se incrementa el **combo counter** (vía `combat-system` / `scoring-xp-system`).

La pared NO destruye al enemigo directamente — el daño de wall slam se suma al daño de impacto de la repulsión/chatarra. Si el HP resultante llega a 0, el enemigo muere.

#### Regla 3 — Suelo navegable

El suelo de la arena es un plano sólido con colisión. Todo el interior de la arena es navegable — no hay obstáculos estáticos en el MVP.

Propiedades del suelo:
- Layer: `Ground` (para raycasts de aim del `input-system` y del `magnetism-system`)
- Material: mate, no resbaloso visualmente
- Sin física especial (no hay ice, no hay mud)

#### Regla 4 — Spawn Points

La arena define puntos de spawn para tres categorías:

| Categoría | Cantidad MVP | Ubicación | Consumidor |
|---|---|---|---|
| **Enemy Spawn** | 8 | Distribuidos en los bordes, fuera de la vista inicial del jugador | `wave-director` |
| **Scrap Spawn** | 12 | Distribuidos uniformemente en el interior | `wave-director` o `attractables-system` |
| **Pickup Spawn** | 4 | Cuadrantes del mapa, alejados del centro | `powerup-system`, `mission-system` |

Cada spawn point es un `Transform` vacío agrupado bajo un contenedor (`EnemySpawns`, `ScrapSpawns`, `PickupSpawns`). El `wave-director` consume estas listas para decidir dónde instanciar.

**Regla de distancia mínima**: ningún enemy spawn point está a menos de 8m del player start. Ningún scrap spawn está a menos de 2m de otro scrap spawn.

#### Regla 5 — Arena Bounds (query API)

El sistema expone una API para que otros sistemas consulten los límites:

```csharp
public class ArenaSystem : MonoBehaviour
{
    // Bounds del área jugable (excluyendo grosor de paredes)
    Bounds PlayableBounds { get; }

    // ¿Está esta posición dentro de la arena?
    bool IsInsideArena(Vector3 position);

    // Punto más cercano dentro de la arena (para clamping)
    Vector3 ClampToArena(Vector3 position);

    // Spawn point aleatorio de una categoría
    Vector3 GetRandomSpawnPoint(SpawnCategory category);

    // Spawn point aleatorio a distancia mínima del jugador
    Vector3 GetSpawnPointAwayFromPlayer(SpawnCategory category, Vector3 playerPos, float minDistance);

    // Normal de la pared más cercana (para wall slam direction)
    Vector3 GetNearestWallNormal(Vector3 position);
}
```

#### Regla 6 — Camera Bounds

La arena define un Bounds para la cámara que impide que la cámara muestre más allá de las paredes. El `camera-system` consume `ArenaSystem.PlayableBounds` para clampar su posición.

#### Regla 7 — Spawn de chatarra por oleada

Cuando una oleada comienza (disparado por `wave-director`), la arena es responsable de:
1. Recibir la solicitud: "necesito N piezas de chatarra tipo X".
2. Elegir N spawn points de la categoría `ScrapSpawns` que no estén ocupados.
3. Retornar las posiciones. La instanciación es responsabilidad del `wave-director` o `object-pooling`.

La arena no instancia objetos — solo provee posiciones válidas.

### States and Transitions

La arena tiene un solo estado para el MVP:

| Estado | Descripción |
|---|---|
| **Active** | Arena cargada, colisiones activas, spawn points disponibles |

Post-MVP (fuera de jam), se pueden agregar:

| Estado | Descripción |
|---|---|
| **Loading** | Arena en construcción (generación procedural o transición entre biomas) |
| **Mutating** | Arena cambia durante la run (se agregan obstáculos, se achica el espacio) |
| **Hazard** | Una zona de peligro se activa temporalmente (electrificación de paredes, suelo dañino) |

Para la jam, la arena es estática e inmutable. No tiene transiciones de estado.

### Arena Shrink (post-MVP, documentado como referencia)

Si se implementa presión espacial creciente:
1. A los 4-5 minutos, las paredes comienzan a avanzar hacia el centro.
2. El área jugable se reduce un 15-20%.
3. Esto aumenta la densidad de combate sin agregar más enemigos.
4. El shrink se pausa durante boss encounters.

Esto NO está en el MVP pero el `PlayableBounds` ya lo soporta si se actualiza dinámicamente.

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `wave-director` | downstream consume | Spawn points para enemigos y chatarra | `GetSpawnPointAwayFromPlayer()`, `GetRandomSpawnPoint()` |
| `player-movement` | downstream consume | Límites para evitar que el player salga | `ClampToArena()` (o paredes con collider) |
| `enemy-system` | downstream consume | Detección de wall slam al impactar paredes | Layer `ArenaWall` + evento `OnWallSlam` |
| `camera-system` | downstream consume | Bounds para clampar cámara | `PlayableBounds` |
| `magnetism-system` | downstream consume | Suelo para aim raycast (plano Ground) | Layer `Ground` en el suelo |
| `attractables-system` | downstream consume | Paredes como superficie de colisión para chatarra repelida | Colliders de ArenaWall |
| `combat-system` | downstream consume | Wall slam como fuente de daño/combo | `OnWallSlam` evento |
| `scoring-xp-system` | downstream consume | Wall slam kills como fuente de XP y misión "Wall Slam" | Indirecto vía `combat-system` |
| `presentation-system` | downstream consume | Impacto visual/audio en paredes | `OnWallSlam` evento → VFX/SFX |

**Ownership:**
- `arena-system` **owns** la geometría, los límites y los spawn points.
- `arena-system` **owns** la detección de wall slam (colisión pared-enemigo).
- `arena-system` **NO owns** qué pasa después del wall slam (daño = `combat-system`, score = `scoring-xp`, VFX = `presentation-system`).
- `wave-director` **owns** la decisión de qué spawnar y cuándo — la arena solo da posiciones.

## Formulas

### Wall Slam Damage

```
wallSlamDamage = baseSlamDamage + floor(impactSpeed / speedDamageRatio)
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `baseSlamDamage` | `int` | 1 – 5 | 2 |
| `impactSpeed` | `float` | 0 – 30 | velocidad del enemigo al momento de impacto |
| `speedDamageRatio` | `float` | 3 – 10 | 5 |

**Ejemplo**: un enemigo repelido a velocidad 16 impacta una pared:
`wallSlamDamage = 2 + floor(16/5) = 2 + 3 = 5`

Este daño se suma al daño del impacto de la repulsión. Un Scrapling (3 HP) recibe `magnetizedEnemyDamage` (5) + `wallSlamDamage` (5) = 10 → muere.

### Spawn Point Selection Weight

```
weight(spawnPoint) = distanceToPlayer × (1 + offscreenBonus)
```

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `offscreenBonus` | `float` | 0 – 2 | 1.5 |

Los spawn points fuera del frustum de la cámara tienen peso 2.5× mayor. Esto evita que los enemigos aparezcan a la vista del jugador.

## Edge Cases

### E1 — Enemigo repelido sale de la arena

**Caso:** un enemigo es repelido con tanta fuerza que atraviesa la pared (CharacterController no garantiza colisión perfecta a velocidades altas).
**Resolución:** `ArenaSystem` hace un check por frame para enemigos fuera de `PlayableBounds`. Si un enemigo está fuera, se hace `ClampToArena()` + se aplica wall slam damage + se reposiciona dentro. Este es un safety net, no el camino normal.

### E2 — Player atrapado contra la pared

**Caso:** el jugador queda acorralado contra la pared por muchos enemigos, sin espacio para esquivar.
**Resolución:** esto es **diseño intencional** — la pared crea presión. El counter (espacio) existe precisamente para esta situación: empuja enemigos y da espacio. Si el jugador no counterea, recibe daño. La arena no interviene para "salvar" al jugador.

### E3 — Spawn point ocupado

**Caso:** el `wave-director` pide un spawn point pero todos los de una categoría están ocupados por entidades existentes.
**Resolución:** `GetSpawnPointAwayFromPlayer()` verifica un overlap sphere pequeño (radio 1m) alrededor de cada candidato. Si todos están ocupados, retorna una posición aleatoria dentro de `PlayableBounds` a distancia mínima del player. Nunca falla — siempre retorna una posición válida.

### E4 — Chatarra acumulada en esquinas

**Caso:** la chatarra no recogida se acumula en las esquinas de la arena, creando pilas inútiles.
**Resolución:** los scrap spawn points están distribuidos para evitar esquinas. La chatarra que termina en esquinas después de repulsiones es reciclable (el jugador puede atraerla de vuelta). Post-MVP, chatarra sin interacción por >30s se puede desvanecer y respawnear.

### E5 — Wall slam de chatarra (no enemigo)

**Caso:** un MagneticObject repelido impacta una pared.
**Resolución:** hoy `MagneticObject.OnCollisionEnter` ya llama `Consume()` cuando impacta algo que no es un enemigo. Esto incluye paredes. La chatarra se destruye, pero NO genera wall slam damage (el wall slam es para enemigos). Opcionalmente, se puede agregar un VFX/SFX de impacto contra pared para feedback.

### E6 — Enemigos spawneando dentro de paredes

**Caso:** un spawn point está demasiado cerca de la pared y el CharacterController del enemigo queda parcialmente dentro del collider.
**Resolución:** los spawn points se colocan a mínimo 2m del borde interior de la pared. El `ArenaSystem.GetRandomSpawnPoint()` valida esto al construir la lista de spawn points.

### E7 — Arena demasiado grande o demasiado chica

**Caso:** el tamaño de la arena no se siente bien para la densidad de enemigos.
**Resolución:** el tamaño es un tuning knob (`arenaSize`). La recomendación de 32x32 está calibrada para 5-8 enemigos simultáneos. Si el `wave-director` sube la densidad a 12+, el arena shrink (post-MVP) entra como solución. Para la jam, ajustar `arenaSize` es la primera herramienta de balance.

## Dependencies

### Upstream (este sistema depende de)

Ninguna. El arena system es Layer 0 Foundation — funciona sin ningún otro sistema.

Dependencia técnica:
- **Unity Physics** (built-in): para colliders de paredes y suelo.
- **Unity Layers/Tags**: necesita layers `Ground` y `ArenaWall` definidos en el proyecto.

### Downstream (dependen de este sistema)

| Sistema | Tipo | Qué consume |
|---|---|---|
| `player-movement` | **Hard** | Paredes impiden que el player salga (colliders) |
| `enemy-system` | **Hard** | Paredes para wall slam + contención de enemigos |
| `wave-director` | **Hard** | Spawn points para instanciar enemigos y chatarra |
| `camera-system` | **Soft** | `PlayableBounds` para clampar cámara (funciona sin esto, solo pierde polish) |
| `magnetism-system` | **Soft** | Suelo para aim raycast + paredes para colisión de chatarra repelida |
| `attractables-system` | **Soft** | Paredes como superficie de colisión |
| `combat-system` | **Soft** | Wall slam events para combo/daño bonus |
| `scoring-xp-system` | **Soft** | Wall slam como fuente de XP (indirecto) |
| `presentation-system` | **Soft** | Wall slam para VFX/SFX de impacto |

## Tuning Knobs

| Knob | Default | Rango seguro | Efecto si demasiado bajo | Efecto si demasiado alto |
|---|---|---|---|---|
| `arenaWidth` | 32 m | 16 – 48 | Arena claustrofóbica, jugador no puede esquivar | Arena vacía, enemigos dispersos, combate no tiene tensión |
| `arenaDepth` | 32 m | 16 – 48 | Igual que arenaWidth | Igual que arenaWidth |
| `wallHeight` | 3 m | 2 – 5 | Chatarra sale volando por encima | No afecta gameplay, solo visual |
| `baseSlamDamage` | 2 | 0 – 5 | Wall slam no vale la pena, jugador ignora paredes | Wall slam mata todo, trivializa el combate |
| `speedDamageRatio` | 5 | 3 – 10 | Incluso impactos lentos hacen mucho daño | Solo impactos muy rápidos hacen daño extra |
| `minSpawnDistanceFromPlayer` | 8 m | 4 – 12 | Enemigos aparecen encima del jugador (unfair) | Enemigos aparecen muy lejos, tardan en llegar (boring) |
| `spawnPointOverlapRadius` | 1 m | 0.5 – 2 | Entidades se superponen al spawnar | Spawn points se rechazan fácilmente, spawning falla |

### Interacciones entre knobs

- `arenaWidth/Depth` × `minSpawnDistanceFromPlayer`: si la arena es 16m y la distancia mínima es 12m, casi todos los spawn points quedan inválidos. **Regla**: `minSpawnDistanceFromPlayer < arenaWidth / 3`.
- `baseSlamDamage` × `magnetizedEnemyDamage` (del `magnetism-system`, default 5): la suma determina si un wall slam es one-shot para Scraplings (3 HP). Con defaults (5 + 2 = 7 > 3), sí es one-shot — esto es intencional para satisfacer el pilar "el daño grande viene del magnetismo".

## Visual/Audio Requirements

### Visual

- **Suelo**: material industrial/metálico con textura sutil. No distrae del combate. Color base gris oscuro con variaciones sutiles (scratches, grids).
- **Paredes**: deben ser visualmente distintas del suelo. Material más claro o con bordes luminosos para que el jugador identifique dónde puede hacer wall slam. Post-MVP: glow breve al recibir impacto.
- **Wall slam VFX**: partículas de chispas + dust al momento del impacto. Flash breve de luz en el punto de contacto. Delegado a `presentation-system`.
- **Spawn VFX**: indicador visual breve donde aparece un enemigo (para fairness — el jugador ve que algo va a aparecer). Un círculo de energía en el suelo 0.3s antes del spawn.

### Audio

- **Wall slam SFX**: impacto metálico pesado, proporcional al daño. Es uno de los 9 eventos obligatorios de feedback del GDD §15.
- **Ambiente**: hum industrial bajo y constante. Define la atmósfera de la arena. Volumen sube levemente cuando la sobrecarga del jugador está alta.

## UI Requirements

### MVP

- Sin UI directa. La arena es geometría pura — su información se comunica visualmente, no con UI.
- Los spawn points NO son visibles para el jugador. Son datos internos del sistema.

### Post-MVP

- **Minimap**: representación simplificada de la arena con posiciones de enemigos, chatarra y pickups. Esquina de la pantalla, semi-transparente.
- **Arena Shrink Warning**: indicador visual en el borde del HUD cuando las paredes están por avanzar. Tipo battle royale zone warning.
- **Heat Map (debug)**: visualización para el diseñador de dónde mueren los enemigos / dónde el jugador pasa más tiempo. Para calibrar spawn points.

## Acceptance Criteria

### Funcionales

1. **AC-1**: Existe un componente `ArenaSystem` que expone `PlayableBounds`, `IsInsideArena()`, `ClampToArena()` y `GetRandomSpawnPoint()`.
2. **AC-2**: La arena tiene 4 paredes con colliders en layer `ArenaWall` que contienen al player y a los enemigos.
3. **AC-3**: Un enemigo repelido contra una pared recibe `wallSlamDamage` adicional y dispara el evento `OnWallSlam`.
4. **AC-4**: Un Scrapling (3 HP) repelido contra una pared muere en un solo impacto (magnetizedEnemyDamage 5 + wallSlamDamage 2+ > 3).
5. **AC-5**: `GetSpawnPointAwayFromPlayer()` nunca retorna una posición a menos de `minSpawnDistanceFromPlayer` del jugador.
6. **AC-6**: `GetSpawnPointAwayFromPlayer()` prioriza posiciones fuera del frustum de la cámara.
7. **AC-7**: Ningún spawn point está a menos de 2m del borde interior de las paredes.
8. **AC-8**: El suelo está en layer `Ground` y responde correctamente al raycast de aim del `magnetism-system` / `input-system`.

### Rendimiento

9. **AC-9**: `IsInsideArena()` y `ClampToArena()` son operaciones O(1) (AABB check, no physics query).
10. **AC-10**: La detección de wall slam usa la colisión existente del CharacterController — no agrega queries de física adicionales.

### Migración desde prototipo

11. **AC-11**: El `Plane` creado por `ArkhamCombatSetup.CreateArena()` se reemplaza por la nueva geometría con paredes.
12. **AC-12**: Los enemigos actualmente posicionados manualmente en el editor son re-creados usando los spawn points del `ArenaSystem`.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Arena cuadrada o circular? Cuadrada tiene esquinas (kill zones naturales). Circular es más justa (sin corner traps). El GDD no especifica forma. Defaulteamos a cuadrada por simplicidad de implementación y más oportunidades de wall slam táctico. | cris | Pre-implementación |
| Q2 | ¿Obstáculos internos en la arena (pilares, cobertura)? El MVP dice no, pero un par de pilares podrían crear pockets tácticos interesantes (como Hades). ¿Agregamos 2-4 pilares destructibles como "Día 9 si hay tiempo"? | cris | Día 8-9 |
| Q3 | ¿Arena shrink es scope de jam? El GDD menciona "presión espacial" en la escalada post-4min. El shrink es la forma más elegante de implementar eso, pero es scope extra. ¿Lo cortamos? | cris | Día 7 decision point |
| Q4 | ¿La chatarra debe respawnear automáticamente o solo aparece con nuevas oleadas? Si respawnea sola, el jugador nunca se queda sin ammo. Si no respawnea, las oleadas tardías sin chatarra son aburridas. | cris | Pre-wave-director GDD |
| Q5 | ¿Las paredes hacen daño al PLAYER si es empujado contra ellas por un enemigo? Si sí, agrega riesgo interesante. Si no, las paredes son solo herramientas del jugador y se siente asimétrico. | cris | Pre-implementación |
| Q6 | ¿Wall slam debería generar chatarra? (El enemigo "se rompe" al impactar y deja scrap). Esto crearía un loop interesante: repeler → wall slam → chatarra nueva → atraer → repeler. | cris | Post-playtest |
