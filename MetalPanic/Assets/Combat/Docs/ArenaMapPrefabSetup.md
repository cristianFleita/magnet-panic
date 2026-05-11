# Arena Map Prefab Setup

Guia para configurar un prefab de mapa jugable para el Arena System.

La regla importante: si el modelo visual viene todo unido en un solo mesh, no uses ese mesh como collider unico. Crea colliders manuales separados para piso y paredes.

## Layers requeridas

En `Project Settings > Tags and Layers` deben existir:

| Layer | Uso |
|---|---|
| `Ground` | Piso navegable y raycasts de aim/suelo |
| `ArenaWall` | Paredes y obstaculos donde cuentan wall slams |

## Estructura recomendada del prefab

```text
Map
├── Visuals
│   └── FactoryMapMesh
├── Colliders
│   ├── Ground
│   └── Walls
├── EnemySpawns
├── ScrapSpawns
└── PickupSpawns
```

## Scripts en el root `Map`

Agregar al GameObject raiz del prefab:

1. `ArenaSystem`
2. Opcional: `ArenaMapColliderBuilder`

Si vas a configurar colliders manuales, deja `ArenaMapColliderBuilder` desactivado o removido. Ese builder sirve solo cuando las piezas visuales vienen separadas de forma util; si el mesh incluye piso y paredes en una sola pieza, puede marcar demasiado como `ArenaWall`.

## Configuracion de `ArenaSystem`

En `ArenaSystem` ajustar:

| Campo | Que poner |
|---|---|
| `Local Playable Center` | Centro local aproximado del area jugable |
| `Local Playable Size` | Tamano local del area jugable completa |
| `Min Spawn Distance From Player` | Recomendado `8` |
| `Base Slam Damage` | Recomendado `2` |
| `Speed Damage Ratio` | Recomendado `5` |
| `Create Default Map Spawns When Empty` | Apagar si vas a crear spawns manuales |

`PlayableBounds` no reemplaza las paredes fisicas. Es un fallback/query API para clamp, camara, spawns y safety net si algo sale del mapa.

## Piso

Crear uno o varios GameObjects bajo `Colliders/Ground`.

Configuracion:

- Layer: `Ground`
- Collider: `BoxCollider` o `MeshCollider`
- `Is Trigger`: apagado
- Cubrir solo zonas caminables
- Evitar que el piso sea layer `ArenaWall`

Para muchos cuartos, es mejor usar varios `BoxCollider` bajos y simples en vez de un mesh collider gigante.

## Paredes y obstaculos

Crear GameObjects bajo `Colliders/Walls`.

Configuracion:

- Layer: `ArenaWall`
- Collider: `BoxCollider` o `MeshCollider`
- `Is Trigger`: apagado
- Cubrir paredes exteriores, paredes interiores, columnas, maquinas grandes y cualquier objeto solido que deba frenar player/enemigos

El wall slam de enemigos repelidos solo se dispara cuando el `CharacterController` del enemigo choca lateralmente contra un collider en layer `ArenaWall`.

## Visuals

El mesh visual puede quedar en `Default` o cualquier layer visual.

No pongas el mesh visual completo en `ArenaWall` si tambien contiene piso, puertas abiertas o decoracion no solida. Eso puede generar choques falsos y wall slams raros.

## Spawn points

Crear empties con `ArenaSpawnPoint`.

### EnemySpawns

- Script: `ArenaSpawnPoint`
- Category: `Enemy`
- Colocarlos lejos del player start
- Mantenerlos dentro del area caminable
- Evitar colocarlos dentro de colliders

Recomendado: 8+ puntos repartidos por cuartos y corredores.

### ScrapSpawns

- Script: `ArenaSpawnPoint`
- Category: `Scrap`
- Colocarlos en zonas caminables
- Separarlos entre si para que la chatarra no aparezca apilada

Recomendado: 12+ puntos.

### PickupSpawns

- Script: `ArenaSpawnPoint`
- Category: `Pickup`
- Colocarlos lejos del centro y en lugares visibles

Recomendado: 4 puntos.

## Checklist rapido

- `Map` tiene `ArenaSystem`.
- `Ground` existe en layers.
- `ArenaWall` existe en layers.
- Piso navegable tiene colliders en layer `Ground`.
- Paredes/obstaculos solidos tienen colliders en layer `ArenaWall`.
- Colliders no son trigger.
- El mesh visual completo no esta en `ArenaWall` si contiene piso.
- Spawns tienen `ArenaSpawnPoint` con categoria correcta.
- `Create Default Map Spawns When Empty` esta apagado si los spawns son manuales.
- `ArenaMapColliderBuilder` esta desactivado/removido si los colliders son manuales.

## Prueba en Play Mode

1. El player no debe atravesar paredes.
2. Enemigos no deben atravesar paredes durante movimiento normal.
3. Un enemigo magnetizado repelido contra una pared debe recibir dano extra de wall slam.
4. Chatarra repelida debe desaparecer al impactar contra pared/obstaculo.
5. `ArenaSystem.GetSpawnPointAwayFromPlayer()` no debe devolver puntos encima del player.

Si un wall slam no ocurre, revisar primero que el collider golpeado este en layer `ArenaWall` y que no sea trigger.
