# Arena System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-12
> **Implements Pillar**: El escenario es el arma - la arena provee puertas legibles para oleadas, paredes para wall slam, bounds para spawns dinamicos y pads de curacion.

## Overview

El Arena System define el mapa jugable donde ocurre la run. Para la siguiente reimplementacion debe dejar de ser un plano generico con spawn points sueltos y pasar a ser una **arena sci-fi cerrada con 4 puertas cardinales**, inspirada en la referencia visual del Wave Director.

El Wave Director depende de este sistema para:

- saber donde esta el area jugable;
- elegir puertas de entrada para enemigos;
- validar posiciones de chatarra cerca del jugador;
- encontrar pads de curacion;
- contener player/enemigos/chatarra con paredes fisicas;
- disparar wall slams cuando enemigos repelidos chocan contra paredes.

El objetivo inmediato es una arena gris reimplementable con cubos/cilindros y colliders simples. El arte final puede llegar despues.

## Current Code Snapshot

Codigo actual en `MetalPanic/Assets/Combat/Scripts/Arena`:

| Archivo | Estado actual | Decision para reimplementacion |
|---|---|---|
| `ArenaSystem.cs` | Ya expone `PlayableBounds`, `IsInsideArena`, `ClampToArena`, spawn selection por categoria y wall slam events. | Mantener como nucleo, pero extender contrato para puertas y scrap sampling. |
| `ArenaSpawnPoint.cs` | Tiene categorias `Enemy`, `Scrap`, `Pickup` y `occupancyRadius`. | Mantener para pickups; para enemigos conviene reemplazar/expandir con `ArenaDoor`. Scrap points pasan a fallback/debug, no fuente primaria. |
| `ArenaMapColliderBuilder.cs` | Puede generar mesh colliders y ground proxies desde meshes. | Usarlo solo si el mesh visual viene separado. Para la jam, preferir colliders manuales simples. |

La base existe, pero el Wave Director nuevo necesita un contrato mas explicito que "dame cualquier spawn point".

## Player Fantasy

**"Estoy encerrado en una base industrial, las puertas se abren, y convierto el metal del lugar en mi respuesta."**

La arena debe comunicar tres cosas sin texto:

1. De donde viene el peligro: puertas con warning.
2. Donde puedo hacer jugadas grandes: paredes claras para wall slam.
3. Donde hay decisiones de riesgo: chatarra cerca pero no gratis, curacion en pads laterales.

## Layout Target

### Forma Base

La arena MVP es rectangular con paredes perimetrales y 4 puertas.

```text
                 [North Door]
┌────────────────────┬────────────────────┐
│   pickup pad        │        pickup pad  │
│        scrap/props  │  landmark/center   │
│                    [Core]                │
[West Door]                              [East Door]
│                     │                    │
│   pickup pad        │        pickup pad  │
└────────────────────┴────────────────────┘
                 [South Door]
```

Dimensiones sugeridas para placeholder:

| Propiedad | Valor inicial |
|---|---:|
| Ancho X | 36 m |
| Largo Z | 28 m |
| Altura paredes | 3 m |
| Centro/player start | `(0, 0, 0)` |
| Margen interno sin spawns | 2 m |
| Radio libre alrededor del player start | 4 m |

La referencia visual tiene muchos props internos. En la primera version, esos props deben ser visuales o colliders simples muy controlados. No bloquear rutas desde las puertas hacia el centro hasta validar el combate.

### Centro

El centro puede tener un reactor/landmark, pero para la vertical slice recomiendo:

- Fase 1: centro visual sin collider fuerte.
- Fase 2: collider pequeno opcional, radio 1.2-1.8 m.
- Nunca crear una barrera grande que parta la arena y haga pathing raro.

## Door Contract

### Nueva pieza recomendada: `ArenaDoor`

Para el Wave Director, los enemigos deben salir de puertas, no de spawn points anonimos. Crear un componente nuevo o extender `ArenaSpawnPoint` con metadata equivalente.

Contrato recomendado:

```csharp
public enum ArenaDoorId
{
    North,
    South,
    East,
    West
}

public sealed class ArenaDoor : MonoBehaviour
{
    public ArenaDoorId DoorId { get; }
    public Transform Exit { get; }
    public Transform Queue { get; }
    public Light WarningLight { get; }
    public float SpawnRadius { get; }
    public float OccupancyRadius { get; }
    public bool IsEnabled { get; }
    public Vector3 ExitPosition { get; }
    public Vector3 FacingDirection { get; }
}
```

`ExitPosition` es donde aparece el enemigo. `FacingDirection` debe apuntar hacia el centro o hacia el player.

### Posiciones sugeridas

Para una arena 36x28:

| DoorId | Posicion local | Facing |
|---|---:|---|
| North | `(0, 0, 14)` | `Vector3.back` |
| South | `(0, 0, -14)` | `Vector3.forward` |
| East | `(18, 0, 0)` | `Vector3.left` |
| West | `(-18, 0, 0)` | `Vector3.right` |

Cada puerta puede tener un `Queue` justo afuera de la pared si mas adelante se quiere animar entrada. Para MVP, alcanza con instanciar en `Exit`.

### API de puertas en `ArenaSystem`

```csharp
public IReadOnlyList<ArenaDoor> Doors { get; }
public bool TryGetDoor(ArenaDoorId id, out ArenaDoor door);
public ArenaDoor GetDoor(ArenaDoorId id);
public ArenaDoor GetFarthestDoorFrom(Vector3 position);
public float DistanceToNearestDoor(Vector3 position);
public bool IsNearDoor(Vector3 position, float radius);
```

El Wave Director puede hacer su scoring de puertas, pero Arena debe proveer datos confiables y baratos.

## Spawn Rules

### Enemigos

Los enemigos salen por puertas:

1. El Wave Director elige puertas.
2. Arena valida que la puerta esta habilitada y no ocupada.
3. Wave Director spawnea una cola corta en la puerta.
4. Enemigos entran con warning y no atacan instantaneamente.

Arena no decide **que** enemigo aparece ni **cuando**. Solo define puertas, bounds y validaciones.

### Chatarra

La chatarra ya no depende de `ScrapSpawns` fijos. El Wave Director necesita chatarra cerca del jugador, siempre dentro del mapa.

Arena debe ofrecer un helper de sampling/validacion:

```csharp
public struct ScrapSpawnQuery
{
    public Vector3 PlayerPosition;
    public float MinRadius;
    public float MaxRadius;
    public float PlayerMinDistance;
    public float DoorMinDistance;
    public float EnemyMinDistance;
    public float WallInset;
    public int Attempts;
}

public bool TryFindScrapSpawnPoint(
    ScrapSpawnQuery query,
    IReadOnlyList<Vector3> enemyPositions,
    out Vector3 position);
```

Reglas:

- Samplear en un anillo alrededor del jugador.
- Clampear a `PlayableBounds`.
- Rechazar si queda muy cerca del player.
- Rechazar si queda muy cerca de puertas.
- Rechazar si queda fuera del inset interno de paredes.
- Rechazar si queda sobre enemigos/obstaculos.
- Si falla, usar fallback dentro del mapa pero fuera del radio inmediato del player.

`ScrapSpawns` pueden quedar como fallback/editor/debug, pero no son la fuente primaria de la run.

### Curaciones

Las curaciones usan spawn points/pads dedicados. Se pueden mantener como `ArenaSpawnPoint` con `ArenaSpawnCategory.Pickup`.

Cantidad MVP:

- 4 pads.
- Uno por cuadrante.
- No en el centro.
- Visibles y con buen acceso.

Posiciones sugeridas para 36x28:

| Pad | Posicion local |
|---|---:|
| NW | `(-12, 0, 8)` |
| NE | `(12, 0, 8)` |
| SW | `(-12, 0, -8)` |
| SE | `(12, 0, -8)` |

API:

```csharp
public IReadOnlyList<ArenaSpawnPoint> PickupSpawns { get; }
public bool TryGetPickupSpawn(Vector3 playerPosition, out ArenaSpawnPoint point);
```

El Wave Director/Healing Director decide cooldown y si el player necesita vida. Arena solo da pads validos.

## Bounds And Validation

El `PlayableBounds` actual debe seguir siendo AABB O(1). No reemplaza paredes fisicas; es una API de consulta para camera, spawns y safety nets.

API base:

```csharp
Bounds PlayableBounds { get; }
Vector3 PlayerStart { get; }

bool IsInsideArena(Vector3 position);
bool IsInsidePlayableArea(Vector3 position, float wallInset);
Vector3 ClampToArena(Vector3 position);
Vector3 ClampToPlayableArea(Vector3 position, float wallInset);
Vector3 RandomPointInsideArena(float wallInset = 0f);
Vector3 GetNearestWallNormal(Vector3 position);
```

Validaciones recomendadas:

```csharp
public struct ArenaSpawnValidation
{
    public float PlayerMinDistance;
    public float DoorMinDistance;
    public float WallInset;
    public float OccupancyRadius;
    public LayerMask BlockingMask;
}

public bool IsValidSpawnPosition(
    Vector3 position,
    Vector3 playerPosition,
    ArenaSpawnValidation validation);
```

Esto evita duplicar reglas en Wave Director, Scrap Director y Healing Director.

## Colliders And Layers

### Layers requeridas

| Layer | Uso |
|---|---|
| `Ground` | Piso navegable y raycasts de aim |
| `ArenaWall` | Paredes/obstaculos que frenan entidades y cuentan wall slam |

### Estructura del prefab

```text
ArenaRoot
├── ArenaSystem
├── Visuals
├── Colliders
│   ├── Ground
│   ├── OuterWalls
│   └── OptionalInteriorBlockers
├── Doors
│   ├── Door_North
│   ├── Door_South
│   ├── Door_East
│   └── Door_West
├── PickupPads
│   ├── Pickup_NW
│   ├── Pickup_NE
│   ├── Pickup_SW
│   └── Pickup_SE
└── Debug
```

### Manual colliders first

Para reimplementar rapido:

- Ground: 1 BoxCollider grande en layer `Ground`.
- OuterWalls: 4 BoxColliders en layer `ArenaWall`.
- Puertas: visuales, no agujeros fisicos necesarios en MVP. El enemigo puede aparecer justo adentro de la puerta.
- Interior blockers: off por defecto o pocos colliders simples.

`ArenaMapColliderBuilder` queda como herramienta opcional cuando haya modelo 3D. No usarlo como sustituto de colliders manuales si el mesh visual mezcla piso, paredes y decoracion.

## Wall Slam

Se mantiene la regla actual:

```csharp
wallSlamDamage = baseSlamDamage + floor(impactSpeed / speedDamageRatio)
```

Defaults:

| Knob | Default |
|---|---:|
| `baseSlamDamage` | 2 |
| `speedDamageRatio` | 5 |

Eventos:

```csharp
public event Action<GameObject, Vector3, int, float> WallSlammed;
public WallSlamUnityEvent OnWallSlam { get; }
```

La deteccion puede seguir viviendo en `ArkhamEnemy` al chocar con layer `ArenaWall`, llamando `ArenaSystem.ReportWallSlam(...)`.

## States

| Estado | Descripcion |
|---|---|
| `Inactive` | Arena prefab cargado pero no inicializado |
| `Active` | Bounds, puertas, pads y colliders listos |
| `Disabled` | Run terminada, queries validas pero spawns no usados |

Para MVP, alcanza con `Active`. Los otros estados son utiles si `RunBootstrap` recrea la arena entre runs.

## Interactions With Other Systems

| Sistema | Que necesita de Arena |
|---|---|
| `wave-director` | `Doors`, door positions, pickup pads, bounds, spawn validation |
| `player-movement` | Paredes fisicas y opcional clamp safety |
| `camera-system` | `PlayableBounds` para limitar seguimiento |
| `enemy-system` | Paredes `ArenaWall`, wall slam normal/damage, door-facing direction |
| `attractables-system` | Paredes para consumir proyectiles y bounds para despawn safety |
| `magnetism-system` | Ground para aim raycast, bounds para chatarra/pull safety |
| `damage-health-system` | No consume Arena directamente; healing pads usan pickups |
| `presentation-system` | Warning lights de puertas, wall slam VFX/SFX, pad glow |
| `hud-system` | Puede mostrar warnings de puerta si Presentation lo publica |

Ownership:

- Arena owns: geometria, colliders, bounds, puertas, pickup pads, spawn validation, wall normals.
- Wave Director owns: timing, presupuesto, puerta elegida, tipo/cantidad de enemigos, chatarra/cuando.
- Healing Director owns: cooldown de curacion, max pickups activos y condicion de HP.

## Tuning Knobs

| Knob | Default | Rango seguro | Efecto si bajo | Efecto si alto |
|---|---:|---:|---|---|
| `arenaWidth` | 36 m | 28-44 | Poco espacio para dodge | Enemigos tardan, baja presion |
| `arenaDepth` | 28 m | 22-36 | Muy claustrofobica | Demasiado downtime |
| `wallHeight` | 3 m | 2-5 | Visual debil | Sin impacto jugable |
| `wallInsetForSpawns` | 1.5 m | 0.5-3 | Spawns pegados a pared | Menos area util |
| `doorPlayerMinDistance` | 6 m | 3-9 | Spawns encima | Puertas bloqueadas |
| `pickupPadCount` | 4 | 2-6 | Pocas rutas de curacion | Curacion demasiado accesible |
| `baseSlamDamage` | 2 | 0-5 | Wall slam flojo | Wall slam trivializa |
| `speedDamageRatio` | 5 | 3-10 | Todo wall slam pega fuerte | Solo impactos rapidos importan |

## Visual/Audio Requirements

### MVP placeholder

- Puertas visibles con color rojo/naranja.
- Pickup pads en azul/cyan.
- Centro con landmark simple.
- Paredes claras y legibles.
- Ground oscuro neutro.

### Polish despues

- Warning light por puerta.
- Puerta activa con beep/alarma corta.
- Wall slam con chispas y golpe metalico.
- Reactor central pulseando al cambiar de acto.
- Props industriales que no escondan enemigos ni chatarra.

## UI Requirements

Arena no tiene UI directa. Solo publica o expone datos para:

- warnings de puerta;
- debug overlay de bounds/doors/pads;
- posible minimap futuro.

Debug recomendado en editor/play mode:

- Gizmo de `PlayableBounds`.
- Gizmos de puertas con flecha de facing.
- Gizmos de pickup pads.
- Gizmo del anillo de scrap sampling alrededor del jugador.

## Acceptance Criteria

### Para reimplementar Arena

1. Existe un `ArenaSystem` con `PlayableBounds`, `PlayerStart`, `Doors` y `PickupSpawns`.
2. La arena tiene exactamente 4 puertas principales: North, South, East, West.
3. Cada puerta expone posicion, facing direction, enabled state, spawn radius y occupancy radius.
4. El player spawnea en el centro sin objetos dentro de su radio libre.
5. Las paredes exteriores contienen player, enemigos y chatarra.
6. Las paredes usan layer `ArenaWall`; el piso usa layer `Ground`.
7. `ClampToArena` y `IsInsideArena` son O(1) y no usan physics query.
8. Arena puede validar una posicion de spawn contra player, puertas, paredes y ocupacion.
9. Arena puede devolver una posicion valida de chatarra cerca del jugador sin usar `ScrapSpawns` fijos.
10. Arena tiene 4 pickup pads validos y visibles.

### Para Wave Director

11. El Wave Director puede pedir puertas por id y obtener salida/facing.
12. El Wave Director puede saber si una posicion esta cerca de una puerta.
13. El Wave Director puede pedir posiciones de chatarra cercanas al player y siempre recibir puntos dentro del mapa o un fallback seguro.
14. El Healing Director puede pedir un pickup pad disponible.
15. El debug de playtest permite ver puertas, pads y bounds.

### Wall Slam

16. Un enemigo repelido contra layer `ArenaWall` reporta wall slam.
17. `ReportWallSlam` publica target, normal, damage e impact speed.
18. Un Scrapling repelido contra pared muere con los defaults actuales.

## Migration Notes

### Desde el codigo actual

1. Mantener `ArenaSystem` como componente raiz.
2. Mantener `PlayableBounds`, `ClampToArena`, `IsInsideArena`, `GetNearestWallNormal`, `ReportWallSlam`.
3. Agregar `ArenaDoor` o metadata equivalente para reemplazar `EnemySpawns` anonimos.
4. Mantener `ArenaSpawnPoint` para `Pickup`.
5. Dejar `Scrap` como categoria legacy/fallback; la chatarra principal debe usar sampling dinamico.
6. Agregar queries `TryFindScrapSpawnPoint` y `IsValidSpawnPosition`.
7. Revisar default spawns generados: deben crear 4 puertas y 4 pickup pads, no 8 enemy spawns genericos.

### Desde el prefab/prototipo

1. Reemplazar el `Plane` simple por `ArenaRoot`.
2. Crear 4 BoxColliders de paredes.
3. Crear 1 BoxCollider de ground.
4. Crear 4 puertas con salida hacia adentro.
5. Crear 4 pickup pads.
6. Desactivar obstaculos internos hasta el primer playtest.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|
| Q1 | El reactor central debe tener collider real o ser solo landmark visual? Recomendacion: primero visual-only. | cris | Primer playtest |
| Q2 | Los props industriales deben ser colliders o decoracion? Recomendacion: decoracion hasta validar flow. | cris | Primer playtest |
| Q3 | Las puertas deben tener animacion real o solo warning light? Recomendacion: warning light para jam, animacion en polish. | cris | Polish |
| Q4 | Conviene mantener `ArenaSpawnCategory.Enemy` como legacy o migrarlo a `ArenaDoor` completamente? | cris | Reimplementacion |
