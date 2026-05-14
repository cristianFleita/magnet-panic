# Attractables System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: El magnetismo es el arma — los attractables son la munición del jugador

## Overview

El Attractables System define los objetos magnéticos que el jugador atrae, orbita y repele como proyectiles. Es la "munición" del loop Pull→Orbit→Repel del `magnetism-system`. Cada tipo de attractable tiene masa, daño, velocidad, comportamiento de impacto y costo de capacidad diferentes, creando decisiones tácticas sobre qué atraer y cuándo.

Hoy está implementado como `MagneticObject` — un MonoBehaviour con 4 tipos (`LightScrap`, `Plate`, `Mine`, `Heavy`), estados (InWorld→Attracting→InOrbit→Projectile), y la interfaz `IAttractable`. El GDD formaliza los tipos, sus stats, y las reglas de interacción con otros sistemas.

## Player Fantasy

**"Cada pieza de chatarra es un arma esperando ser usada."**

El jugador ve chatarra tirada por la arena y piensa "eso es mío". La fantasía es la de Magneto controlando un arsenal ambiental — cada pieza de metal es una bala que solo necesita ser atraída. La variedad de tipos (chatarra rápida para spam, minas explosivas para AoE, pesados para one-shot) da profundidad táctica al loop de munición.

Referencia: **Control** (Jesse lanzando objetos del entorno como ataque principal), **Half-Life 2** (gravity gun convirtiendo cualquier objeto en proyectil).

## Detailed Design

### Core Rules

#### Regla 1 — Cuatro tipos de attractable

| Tipo | Masa | Daño | Pierce | Velocidad | Comportamiento especial |
|---|---|---|---|---|---|
| **LightScrap** | 1.0 | 2 | 2 (atraviesa 1 enemigo) | ×1.4 | Rápido, abundante, spam |
| **Plate** | 2.0 | 3 | 3 (atraviesa 2 enemigos) | ×1.0 | Más grande, más piercing |
| **Mine** | 1.5 | 3 + AoE 4 | 1 (explota al impactar) | ×1.2 | Explosión radio 2.5m |
| **Heavy** | 3.0 | 7 | 1 (no atraviesa) | ×0.6 | Lento, demoledor, knockback 1.8m |

**Masa** determina: costo de capacidad (cuánto "pesa" en la carga del `magnetism-system`), y velocidad de atracción (inversamente proporcional).

#### Regla 2 — Ciclo de vida: 4 estados

```
InWorld → Attracting → InOrbit → Projectile → Consumed (despawn)
                ↓              ↓
          StopAttracting   ForcedEject
                ↓              ↓
             InWorld         InWorld
```

| Estado | Kinematic | Collider | Física |
|---|---|---|---|
| **InWorld** | No (Rigidbody activo) | Normal | Reposa en el suelo |
| **Attracting** | Sí | Normal | Se mueve hacia el player |
| **InOrbit** | Sí | Normal | Sigue posición de órbita |
| **Projectile** | No (Rigidbody activo) | Trigger | Vuela con `linearVelocity`, detecta hits con OverlapSphere |

#### Regla 3 — Atracción y órbita

- `BeginAttract(magnet)`: transiciona a Attracting, se mueve hacia el player a velocidad `pullSpeed / mass`.
- `EnterOrbit(orbitCenter)`: transiciona a InOrbit, se interpola suavemente a su posición orbital (`orbitSnapSharpness = 24`).
- `TickOrbit(orbitPosition, dt)`: mantiene la posición orbital con interpolación exponencial. Mira "hacia afuera" desde el player.

#### Regla 4 — Repulsión como proyectil

`Repel(direction, speed)`: transiciona a Projectile. El objeto sale disparado con `linearVelocity = direction × speed × objectSpeedModifier`. Detecta enemigos con `Physics.OverlapSphereNonAlloc` cada frame. Al impactar un enemigo: daño + knockback. Tras agotar `pierceRemaining`: `Consume()` (despawn/pool return).

#### Regla 5 — Mine: explosión AoE

Cuando un Mine impacta cualquier cosa, en vez de piercing: `Explode()` → `OverlapSphere(explosionRadius)` → daño AoE a todos los enemigos en radio. Luego se consume.

#### Regla 6 — Collision con paredes/suelo

Si un projectile impacta algo que NO es un enemigo (pared, suelo), se consume inmediatamente (`OnCollisionEnter`/`OnTriggerEnter` con check de `GetComponentInParent<ArkhamEnemy>() == null`).

#### Regla 7 — Projectile lifetime

Los proyectiles que no impactan nada se auto-consumen después de `projectileLifetime = 3s`. Safety net contra objetos voladores infinitos.

### States and Transitions

(Ver diagrama en Regla 2. La tabla de estados define claramente los flags por estado.)

### Interactions with Other Systems

| Sistema | Dirección | Datos que fluyen | Interfaz |
|---|---|---|---|
| `magnetism-system` | **upstream** owner | Controla todo el ciclo: Pull, Orbit, Repel | `IAttractable` interface completa |
| `damage-health-system` | **downstream** | `ReceiveMagneticImpact(damage, pos, knockback)` sobre enemigos | `ArkhamEnemy.ReceiveMagneticImpact()` |
| `object-pooling` | **downstream** | `Pool.Despawn()` reemplaza `Destroy(gameObject)` en `Consume()` | `Pool.Despawn(gameObject)` |
| `arena-system` | **downstream** | Paredes como superficie de colisión → consume el proyectil | Implicit (colliders ArenaWall) |
| `wave-director` | **upstream** | Spawning de attractables al inicio de oleada | `Pool.Spawn()` en spawn points |
| `overload-system` | **upstream** | `ForceReleaseAll()` suelta toda la chatarra en órbita | `ForcedEject()` |
| `presentation-system` | **downstream** | Eventos `OnRepelled`, `OnImpact` para VFX/SFX | UnityEvents |

## Formulas

### Velocidad de atracción
```
attractSpeed = pullSpeed / max(0.35, magneticMass)
```

### Daño de proyectil
```
hitDamage = baseDamage  // (no escalea — es fijo por tipo)
explosionDamage = baseDamage  // (Mine, aplicado a todos en radio)
```

| Tipo | Damage | vs Scrapling (3HP) | vs Heavy Bot (8HP) |
|---|---|---|---|
| LightScrap | 2 | 2 hits to kill | 4 hits to kill |
| Plate | 3 | 1-hit kill | 3 hits to kill |
| Mine | 3 + 4 AoE | 1-hit kill (any in radius) | 1-hit kill if direct + AoE |
| Heavy | 7 | 1-hit kill | 1-hit kill |

## Edge Cases

### E1 — Attractable ya en órbita de otro sistema
**Resolución:** `CanEnterOrbit` chequea `state == InWorld || Attracting`. Un objeto ya en órbita no puede ser re-atraído.

### E2 — Capacidad insuficiente para atraer
**Resolución:** `magnetism-system.CanFit(mass)` verifica antes de `BeginAttract`. Si no cabe, el objeto no se atrae.

### E3 — Mine explota cerca del player
**Resolución:** el AoE de Mine solo daña enemigos (layer mask). No daña al player.

### E4 — Heavy demasiado lento para acertar
**Resolución:** `objectSpeedModifier = 0.6` es lento pero predecible. El jugador debe apuntar con anticipación. Es intencional — high risk, high reward.

### E5 — Attractable despawneado mientras está en órbita
**Resolución:** `MagnetismController` verifica nulls en su lista de orbit. Si un objeto desaparece, se remueve silenciosamente.

## Dependencies

### Upstream
| Sistema | Tipo | Qué consume |
|---|---|---|
| `magnetism-system` | **Hard** | Controla todo el ciclo de vida vía `IAttractable` |
| `damage-health-system` | **Hard** | Aplica daño a enemigos |
| `object-pooling` | **Hard** | Recicla instancias en vez de Destroy |

### Downstream
| Sistema | Tipo | Qué consume |
|---|---|---|
| `wave-director` | **Soft** | Spawning de attractables (el sistema funciona con colocación manual) |
| `presentation-system` | **Soft** | VFX/SFX de impacto y órbita |

## Tuning Knobs

| Knob | Default | Rango seguro | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `magneticMass` (per type) | 1-3 | 0.5 – 5 | Se atrae muy rápido, poco peso | Se atrae muy lento, frustración |
| `damage` (per type) | 2-7 | 1 – 10 | No mata nada, chatarra inútil | One-shot todo, trivializa |
| `maxPierceCount` | 1-3 | 1 – 5 | Objeto se consume rápido | Objeto atraviesa todo el mapa |
| `objectSpeedModifier` | 0.6-1.4 | 0.3 – 2.0 | Proyectil lento, difícil acertar | Proyectil instantáneo, sin skill |
| `explosionRadius` (Mine) | 2.5m | 1 – 5 | AoE irrelevante | AoE cubre media arena |
| `projectileLifetime` | 3s | 1 – 5 | Objetos desaparecen rápido | Objetos vuelan indefinidamente |

## Visual/Audio Requirements

- **Órbita**: partículas de energía magnética. Cada tipo tiene color distinto (steel, teal, orange, dark).
- **Repel**: trail renderer activo durante vuelo. Flash de impacto al golpear enemigo.
- **Mine**: glow naranja pulsante en órbita. Explosión con shockwave VFX.
- **Heavy**: efecto de peso visual (partículas de gravedad, velocidad de órbita más lenta).

## UI Requirements

- Sin UI directa. Los attractables se comunican visualmente (tamaño, color, partículas).
- La carga del magnetism-system en el HUD muestra indirectamente cuánta chatarra se carga.

## Acceptance Criteria

1. **AC-1**: Los 4 tipos (LightScrap, Plate, Mine, Heavy) tienen stats distintos y comportamiento de impacto diferenciado.
2. **AC-2**: El ciclo InWorld→Attracting→InOrbit→Projectile→Consumed funciona sin bugs.
3. **AC-3**: Mine explota en AoE al impactar, dañando todos los enemigos en radio.
4. **AC-4**: Plate atraviesa 2 enemigos antes de consumirse.
5. **AC-5**: Heavy hace 7 de daño y knockback 1.8m.
6. **AC-6**: `Consume()` usa `Pool.Despawn()` en vez de `Destroy()`.
7. **AC-7**: Proyectiles que impactan paredes se consumen inmediatamente.
8. **AC-8**: `IPoolable.OnSpawn()` resetea correctamente el estado del MagneticObject.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Agregar un 5to tipo "Shield" que bloquea proyectiles enemigos mientras está en órbita? Crearía un uso defensivo del magnetismo. | cris | Post-playtest |
| Q2 | ¿Los attractables deberían tener durabilidad (se degradan con cada uso) o son single-use? Hoy son single-use (se consumen al impactar). | cris | Post-playtest |
| Q3 | ¿Chatarra combinable? Ej: 3 LightScrap en órbita se fusionan en 1 Plate. Agrega profundidad pero complejidad. | cris | Post-MVP |
