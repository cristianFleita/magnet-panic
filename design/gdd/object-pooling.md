# Object Pooling

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Infraestructura — habilita el loop infinito sin degradación de rendimiento en WebGL

## Overview

El Object Pooling es la capa Foundation que pre-instancia y recicla GameObjects en lugar de crear y destruir con `Instantiate`/`Destroy`. En un endless run donde cada oleada genera enemigos, chatarra, proyectiles, partículas y pickups, la cadencia de allocación/destrucción sería constante y creciente. En WebGL (target principal), el Garbage Collector de Unity/IL2CPP genera stutters perceptibles al fragmentar memoria.

El jugador nunca sabe que este sistema existe — lo siente cuando NO hay stutters, cuando el frame rate se mantiene estable a los 5+ minutos de run. Es infraestructura pura: si funciona bien, es invisible; si funciona mal, el juego se siente roto.

Hoy el codebase tiene exactamente 3 puntos de `Destroy(gameObject)` que necesitan migración:
1. `MagneticObject.Consume()` — chatarra consumida tras impacto (línea 300)
2. `ArkhamEnemy.Die()` — enemigo muere con delay (línea 784)
3. `HealingPickup.OnTriggerEnter()` — pickup usado (línea 50)

Sin pool, una run de 10 minutos con 50 enemigos y 200 piezas de chatarra genera ~250+ `Instantiate`/`Destroy`. Con pool, genera 0 después del warmup inicial.

## Player Fantasy

**"El juego nunca tartamudea."**

Este sistema no tiene fantasía de jugador — tiene fantasía de **jugador frustrado que NO ocurre**. El momento donde un action game a 30 FPS se traba durante 80ms justo cuando estás en un combo perfecto. Ese momento no existe gracias al pool.

La referencia técnica es **Vampire Survivors** (miles de entidades simultáneas sin stutters en WebGL) y el mantra de **DOTS/ECS** de Unity: pooling como pattern por defecto, no como optimización tardía.

## Detailed Design

### Core Rules

#### Regla 1 — API genérica por prefab

El pool expone una API estática global:

```csharp
public static class Pool
{
    // Sacar un objeto del pool (equivale a Instantiate)
    static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        where T : Component;

    static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null);

    // Devolver un objeto al pool (equivale a Destroy)
    static void Despawn(GameObject instance, float delay = 0f);
    static void Despawn<T>(T instance, float delay = 0f) where T : Component;

    // Pre-calentar un pool (llamado durante loading)
    static void Warmup(GameObject prefab, int count);

    // Limpiar todos los pools (entre runs)
    static void ReleaseAll();
}
```

#### Regla 2 — Keying por prefab instance ID

Cada pool se identifica por el `GetInstanceID()` del prefab. Cuando se llama `Pool.Spawn(enemyPrefab, ...)`, el sistema busca o crea un pool para ese prefab específico. Esto soporta múltiples pools (uno por tipo de enemigo, uno por tipo de chatarra, etc.) sin configuración manual.

```
Pool Registry (Dictionary<int, PoolBucket>)
  ├── Scrapling Prefab (ID: 12340) → [idle: 5, active: 8]
  ├── Light Scrap Prefab (ID: 12341) → [idle: 12, active: 3]
  ├── Metal Plate Prefab (ID: 12342) → [idle: 4, active: 2]
  └── Healing Pickup Prefab (ID: 12343) → [idle: 2, active: 1]
```

#### Regla 3 — Ciclo de vida Spawn/Despawn

```
           Warmup / Spawn
                │
                ▼
┌─────────┐  SetActive(true)  ┌─────────┐
│  Pool   │ ────────────────▶ │  Scene  │
│ (idle)  │                   │(active) │
└─────────┘ ◀──────────────── └─────────┘
           SetActive(false)
              Despawn
```

1. **Spawn**: el pool busca un objeto inactivo. Si hay uno, lo activa y reposiciona. Si no hay, instancia uno nuevo (grow-on-demand).
2. **Despawn**: el pool desactiva el objeto y lo devuelve a la cola de idle. NO llama `Destroy`.
3. **Reset**: al despawnear, el pool invoca `IPoolable.OnDespawn()` en el objeto para que limpie su estado. Al spawnear, invoca `IPoolable.OnSpawn()`.

#### Regla 4 — Interfaz IPoolable (opcional)

Los objetos que necesitan reset al ser reciclados implementan:

```csharp
public interface IPoolable
{
    void OnSpawn();    // Llamado al salir del pool (después de SetActive(true))
    void OnDespawn();  // Llamado al volver al pool (antes de SetActive(false))
}
```

Si el objeto no implementa `IPoolable`, solo se hace `SetActive(true/false)`. Esto permite migración gradual — se puede poolear algo sin implementar la interfaz, y agregar reset después.

**Ejemplo de uso en MagneticObject:**

```csharp
public sealed class MagneticObject : MonoBehaviour, IPoolable
{
    public void OnSpawn()
    {
        state = MagneticObjectState.InWorld;
        projectileAge = 0f;
        hitEnemies.Clear();
        objectCollider.enabled = true;
        RestoreColliderMode();
    }

    public void OnDespawn()
    {
        StopParticle(orbitParticle);
        if (trail != null) trail.Clear();
        SetKinematic(true);
        body.linearVelocity = Vector3.zero;
    }
}
```

#### Regla 5 — Grow on demand, no hard cap

Si el pool está vacío y se pide un `Spawn`, se crea una nueva instancia (como si fuera `Instantiate`). El pool crece dinámicamente. No hay hard cap — el `wave-director` es responsable de limitar la cantidad de entidades activas, no el pool.

**Rationale**: un hard cap crearía bugs silenciosos (enemy no aparece porque el pool está lleno). Es preferible un grow ocasional (1 allocation) a entidades faltantes.

#### Regla 6 — Despawn con delay

`Pool.Despawn(enemy, 0.8f)` es el equivalente directo de `Destroy(gameObject, deathDespawnDelay)`. El pool espera el delay (vía coroutine interna) y luego desactiva el objeto. Esto permite que animaciones de muerte/impacto se completen antes del reciclado.

#### Regla 7 — Warmup durante loading

Antes de iniciar la run, el `meta-flow-system` llama `Pool.Warmup()` para cada prefab con la cantidad esperada:

| Prefab | Warmup Count | Rationale |
|---|---|---|
| Scrapling (enemy) | 12 | Oleada máxima esperada |
| Heavy Enemy | 4 | Aparecen poco |
| Metal Enemy | 4 | Aparecen poco |
| Light Scrap | 15 | Muy frecuente |
| Metal Plate | 8 | Frecuente |
| Mine | 4 | Ocasional |
| Heavy Scrap | 3 | Raro |
| Healing Pickup | 4 | Fijo por oleada |
| Impact VFX | 10 | Ráfagas de impactos simultáneos |
| Spark VFX | 10 | Idem |

Total warmup: ~74 objetos. Estimado < 200ms en WebGL. Se ejecuta durante la pantalla de loading antes de la run.

#### Regla 8 — ReleaseAll entre runs

Al terminar una run (`meta-flow-system` → death screen → menu), se llama `Pool.ReleaseAll()` que:
1. Destruye todas las instancias idle (liberan memoria).
2. Las instancias activas que quedan en escena se marcan como "orphaned" y se destruyen con un safety `Destroy` en el siguiente frame.
3. Se limpian todos los `PoolBucket` del registry.

Esto evita memory leaks entre runs.

### States and Transitions

Cada instancia pooleable tiene un estado implícito:

| Estado | Location | `gameObject.activeSelf` |
|---|---|---|
| **Idle** | En el pool (hijo del PoolRoot) | `false` |
| **Active** | En la escena (reparented o no) | `true` |
| **Orphaned** | En escena, el pool fue released | `true` (se destruye next frame) |

```
    Warmup          Spawn           Despawn          ReleaseAll
       │               │               │                 │
       ▼               ▼               ▼                 ▼
   ┌──────┐       ┌────────┐      ┌──────┐         Destroy all
   │ Idle │ ────▶ │ Active │ ───▶ │ Idle │         Idle + Active
   └──────┘       └────────┘      └──────┘
```

El pool manager no tiene estados propios — siempre está disponible.

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `wave-director` | upstream caller | `Spawn(enemyPrefab, pos, rot)` para crear oleadas | `Pool.Spawn<ArkhamEnemy>()` |
| `wave-director` | upstream caller | `Spawn(scrapPrefab, pos, rot)` para chatarra por oleada | `Pool.Spawn<MagneticObject>()` |
| `enemy-system` | upstream caller | `Despawn(gameObject, deathDelay)` al morir | `Pool.Despawn()` reemplaza `Destroy()` |
| `magnetism-system` | upstream caller | `Despawn(gameObject)` al consumirse chatarra | `Pool.Despawn()` reemplaza `Destroy()` |
| `attractables-system` | upstream caller | Spawn/Despawn de objetos magnéticos | `Pool.Spawn/Despawn<MagneticObject>()` |
| `powerup-system` | upstream caller | Spawn/Despawn de pickups | `Pool.Spawn/Despawn()` |
| `presentation-system` | upstream caller | Spawn/Despawn de VFX particles | `Pool.Spawn/Despawn<ParticleSystem>()` |
| `meta-flow-system` | upstream caller | `Warmup()` al inicio de run, `ReleaseAll()` al final | `Pool.Warmup()`, `Pool.ReleaseAll()` |
| `arena-system` | downstream provider | Posiciones de spawn (el pool no decide dónde, solo instancia) | No hay interfaz directa — el caller usa arena API para posición |

**Ownership:**
- `object-pooling` **owns** el ciclo de vida de instancias (create, activate, deactivate, destroy).
- `object-pooling` **NO owns** la lógica de cuándo spawnar o despawnear (eso es del caller).
- `object-pooling` **NO owns** el reset de estado del objeto (eso es del `IPoolable` implementado en cada script).

## Formulas

El object pooling no tiene fórmulas de gameplay. Las métricas relevantes son de rendimiento:

### Memory Budget por Pool

```
memoryPerPool = warmupCount × bytesPerInstance
totalPoolMemory = Σ memoryPerPool (para todos los pools)
```

| Categoría | Bytes estimados por instancia | Warmup | Total |
|---|---|---|---|
| Enemy (Scrapling) | ~8 KB (mesh + collider + scripts) | 12 | ~96 KB |
| MagneticObject | ~4 KB | 30 | ~120 KB |
| Healing Pickup | ~3 KB | 4 | ~12 KB |
| VFX particles | ~6 KB | 20 | ~120 KB |
| **Total** | | **66** | **~348 KB** |

348 KB es trivial incluso en WebGL con su límite de ~2 GB.

### Grow Rate

```
growEvents = max(0, peakActive - warmupCount)
```

Si el warmup está bien calibrado, `growEvents = 0`. Si no, el pool crece on-demand. Un grow event = 1 `Instantiate` = 1 posible GC spike. El objetivo es `growEvents = 0` después de calibrar warmup counts con datos de playtest.

## Edge Cases

### E1 — Despawn de objeto ya inactivo

**Caso:** alguien llama `Pool.Despawn(obj)` sobre un objeto que ya está idle en el pool.
**Resolución:** el pool verifica `gameObject.activeSelf` antes de despawnear. Si ya está inactivo, es un no-op. Log warning en debug build.

### E2 — Spawn de prefab nunca registrado

**Caso:** `Pool.Spawn(prefab)` con un prefab que nunca tuvo warmup.
**Resolución:** el pool crea un nuevo `PoolBucket` on-demand, instancia el primer objeto, y lo retorna. Funciona como `Instantiate` normal. Log info en debug build para detectar pools no warmeados.

### E3 — Destroy manual de objeto pooleable

**Caso:** un script legacy llama `Destroy(gameObject)` en lugar de `Pool.Despawn()`.
**Resolución:** para la migración, se puede agregar un `PoolableTracker` MonoBehaviour al objeto que intercepta `OnDestroy()` y loguea un error. Post-migración, esto detecta bypasses. En la jam, si un objeto se destruye manualmente, el pool simplemente lo ignora — la próxima vez que necesite un spawn, instancia uno nuevo.

### E4 — ReleaseAll con objetos activos en escena

**Caso:** `ReleaseAll()` se llama pero hay enemigos vivos en escena (el jugador murió pero los enemigos siguen ahí).
**Resolución:** `ReleaseAll()` primero desactiva todos los objetos activos (con `OnDespawn()` si implementan `IPoolable`), luego destruye todos. El caller (`meta-flow-system`) es responsable de llamar esto en el momento correcto (después de death screen, no durante gameplay).

### E5 — Pool crece sin límite

**Caso:** un bug en `wave-director` spawnea 500 enemigos. El pool crece a 500.
**Resolución:** el pool tiene un `softCap` configurable por prefab (default: `warmupCount × 4`). Si se supera, el pool loguea un warning pero sigue funcionando. No hay hard cap — el bug está en el caller, no en el pool. El soft cap sirve solo para detección temprana en development.

### E6 — Componentes con estado persistente

**Caso:** un `MagneticObject` pooleable tiene un `TrailRenderer` cuyo trail persiste al reciclarse.
**Resolución:** el `IPoolable.OnDespawn()` debe limpiar `trail.Clear()`. Si no se implementa `IPoolable`, el trail se muestra brevemente en la siguiente posición de spawn — es un bug visual leve. La migración debe asegurar que todo objeto con estado visual implemente `IPoolable`.

### E7 — Despawn con delay cancelado

**Caso:** `Pool.Despawn(obj, 0.8f)` se llama, pero antes del delay el pool recibe `ReleaseAll()`.
**Resolución:** `ReleaseAll()` cancela todas las coroutines de delayed despawn pendientes antes de limpiar. Los objetos se destruyen inmediatamente.

### E8 — Orden de OnSpawn vs Awake/OnEnable

**Caso:** un objeto sale del pool. ¿En qué orden se ejecutan callbacks?
**Resolución:** el orden es: `SetActive(true)` → Unity dispara `OnEnable()` → Pool dispara `IPoolable.OnSpawn()`. Esto significa que `OnEnable` se ejecuta primero. Los scripts deben estar preparados para `OnEnable` sin `OnSpawn` (primer uso desde `Instantiate`) y `OnEnable` + `OnSpawn` (reciclado). El pattern recomendado es hacer el setup mínimo en `Awake` (1 vez) y el reset en `OnSpawn`.

## Dependencies

### Upstream (este sistema depende de)

Ninguna. El object pooling es Layer 0 Foundation — funciona sin ningún otro sistema.

Dependencia técnica:
- **Unity GameObject lifecycle**: `SetActive`, `Instantiate`, `Destroy`, `OnEnable`/`OnDisable`.
- **Coroutines**: para `Despawn` con delay.

### Downstream (dependen de este sistema)

| Sistema | Tipo | Qué consume |
|---|---|---|
| `magnetism-system` | **Hard** | `Despawn()` para chatarra consumida; `Spawn()` si el wave-director delega |
| `enemy-system` | **Hard** | `Despawn()` reemplaza `Destroy(gameObject, delay)` en Die() |
| `wave-director` | **Hard** | `Spawn()` para instanciar oleadas enteras sin allocation |
| `attractables-system` | **Hard** | `Spawn/Despawn` para objetos magnéticos |
| `presentation-system` | **Soft** | `Spawn/Despawn` para VFX particles |
| `powerup-system` | **Soft** | `Spawn/Despawn` para pickups |
| `meta-flow-system` | **Control** | `Warmup()` y `ReleaseAll()` para lifecycle de la run |

### Nota sobre acoplamiento

El pool NO conoce a sus consumidores. Los consumidores llaman la API estática `Pool.Spawn/Despawn`. No hay registración, no hay eventos, no hay callbacks del pool al consumidor. La comunicación es unidireccional: consumidor → pool.

La única excepción es `IPoolable`, que es una interfaz **implementada por el objeto pooleable**, no por el pool ni por el consumidor.

## Tuning Knobs

| Knob | Default | Rango seguro | Efecto si demasiado bajo | Efecto si demasiado alto |
|---|---|---|---|---|
| `warmupCount[prefab]` | varía | 0 – 100 | Grow on demand = GC spikes durante gameplay | Memoria desperdiciada + loading lento |
| `softCap` | `warmupCount × 4` | warmupCount – 500 | Warnings falsos si picos legítimos | No detecta bugs de spawn excesivo |
| `despawnDefaultDelay` | 0 s | 0 – 2 | Objetos desaparecen abruptamente | Objetos inactivos quedan visible demasiado tiempo |
| `releaseDelay` | 0 s | 0 – 1 | Limpieza agresiva, posibles race conditions | Memoria no se libera rápido entre runs |

### Interacciones entre knobs

- `warmupCount` × `totalPoolMemory`: si se warmean 200 objetos, son ~1 MB. Trivial. Si se warmean 2000 (bug), son ~10 MB. Todavía OK para WebGL pero innecesario.
- `despawnDefaultDelay` solo aplica cuando el caller NO especifica delay. Si el caller llama `Despawn(obj, 0.8f)`, el knob se ignora.

## Visual/Audio Requirements

### Visual

- **Sin visualización directa.** El pool es invisible al jugador.
- **Debug overlay (dev only)**: panel en la esquina que muestra pools activos, idle counts, active counts, y grow events por segundo. Solo visible con `#define POOL_DEBUG` o en el Editor.

### Audio

- Sin audio propio. Los sonidos de spawn/despawn son responsabilidad del script del objeto (ej: `MagneticObject.OnSpawn()` podría triggerear un SFX).

## UI Requirements

### MVP

- Sin UI. El pool es 100% infraestructura backend.

### Post-MVP

- **Profiler integration**: marcadores custom de profiling (`Profiler.BeginSample("Pool.Spawn")`) para identificar en el Unity Profiler cuánto tiempo toma cada spawn/despawn.
- **Editor inspector**: un MonoBehaviour `PoolDebugger` que muestra en Inspector cuántos pools hay, cuántos objetos idle/active, y cuántos grows han ocurrido.

## Acceptance Criteria

### Funcionales

1. **AC-1**: `Pool.Spawn<T>(prefab, pos, rot)` retorna una instancia activa posicionada correctamente. Si hay un idle disponible, lo reutiliza (no instancia).
2. **AC-2**: `Pool.Despawn(instance)` desactiva el objeto y lo retorna al pool. El objeto queda disponible para el siguiente `Spawn`.
3. **AC-3**: `Pool.Despawn(instance, delay)` espera el delay antes de desactivar. La animación de muerte del enemigo se ve completa.
4. **AC-4**: `Pool.Warmup(prefab, count)` crea `count` instancias inactivas. Después de warmup, los primeros `count` spawns no generan `Instantiate`.
5. **AC-5**: `Pool.ReleaseAll()` destruye todas las instancias (idle y active) y limpia el registry.
6. **AC-6**: Objetos que implementan `IPoolable` reciben `OnSpawn()` al salir del pool y `OnDespawn()` al volver.
7. **AC-7**: Objetos que NO implementan `IPoolable` funcionan correctamente con solo `SetActive(true/false)`.
8. **AC-8**: `MagneticObject.Consume()` reemplaza `Destroy(gameObject)` con `Pool.Despawn(gameObject)`.
9. **AC-9**: `ArkhamEnemy.Die()` reemplaza `Destroy(gameObject, delay)` con `Pool.Despawn(gameObject, delay)`.
10. **AC-10**: `HealingPickup.OnTriggerEnter()` reemplaza `Destroy(gameObject)` con `Pool.Despawn(gameObject)`.

### Rendimiento

11. **AC-11**: Durante una run de 5+ minutos, hay 0 `Instantiate` calls después del warmup inicial (asumiendo warmup counts correctos).
12. **AC-12**: `Pool.Spawn` y `Pool.Despawn` no generan GC allocations (0 alloc per call).
13. **AC-13**: Warmup de 74 objetos completa en < 500ms en WebGL.

### Migración

14. **AC-14**: Los 3 puntos de `Destroy(gameObject)` en el codebase actual están migrados a `Pool.Despawn()`.
15. **AC-15**: `MagneticObject`, `ArkhamEnemy` y `HealingPickup` implementan `IPoolable` con reset correcto de su estado.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Pool estático global (`Pool.Spawn`) o singleton MonoBehaviour (`PoolManager.Instance.Spawn`)? Estático es más ergonómico pero no aparece en el Inspector. Singleton permite debug visual pero agrega boilerplate. Propongo API estática con un `PoolManager` MonoBehaviour hidden que maneja las coroutines de delay. | cris | Pre-implementación |
| Q2 | ¿Reparent al PoolRoot al despawnear o dejar en su posición en la jerarquía? Reparent mantiene la hierarchy limpia pero tiene costo de reparent. Dejar en su lugar es gratis pero la hierarchy se ensucia. Propongo reparent solo en Editor (para debug), en build no reparent. | cris | Pre-implementación |
| Q3 | ¿Los VFX particles necesitan pool o basta con `ParticleSystem.Play/Stop`? Si un VFX particle se reutiliza in-situ (solo cambia posición), no necesita pool — solo reposicionar y Play(). Pool es para VFX que se spawnean en posiciones distintas cada vez. | cris | Pre-presentation-system GDD |
| Q4 | ¿Warmup progresivo (pre-instanciar de a N por frame para evitar spike de loading) o warmup de golpe? En WebGL, un warmup de 74 objetos debería ser < 500ms. Si crece a 200+, conviene progresivo. | cris | Post-profiling |
| Q5 | ¿Conviene agregar un `PoolableTracker` component automáticamente a cada instancia para detectar `Destroy` accidentales? Es safety útil en dev pero agrega un component por objeto en runtime. Propongo solo en `#if UNITY_EDITOR`. | cris | Pre-implementación |
