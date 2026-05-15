# Powerup System

> **Status**: In Design (v2 — mission-driven, defensive)
> **Author**: cris + agents
> **Last Updated**: 2026-05-15
> **Implements Pillar**: Recompensa al estilo — completar misiones genera picos de poder defensivo/control que dan respiro y crean highlights.

## Overview

El Powerup System otorga **efectos temporales potentes** como recompensa **inmediata y automática** por completar misiones de estilo. A diferencia de los upgrades (permanentes, parte del árbol), los powerups duran 8-12 segundos y se activan **solos** al cumplir un objetivo — no hay pickups, no hay slots, no hay decisión "cuándo activarlo".

El loop es:
1. **Misión visible en HUD** ("Mata 3 enemigos con un solo lanzamiento").
2. **Jugador busca la jugada espectacular**.
3. **Mission complete → powerup random se activa instantáneo**.
4. **8-12 segundos de poder absoluto**.

Los 3 powerups del MVP son **defensivos / control de área**, dando un respiro cuando el caos sube. Esto crea ritmo: presión → highlight (mission clear) → respiro (powerup) → presión.

Decisión clave: **Magnet Fever fue movido a Upgrades** (las stats que daba — pull range/speed × N — son exactamente lo que hacen Magnetic Reach + Quick Coil stackeados, así que pasaron a ser permanentes en el árbol).

No hay código existente — diseño nuevo desde cero.

## Player Fantasy

**"Limpié 4 enemigos con un solo lanzamiento — la arena entera reacciona."**

La fantasía es la del clutch heroico. El jugador ejecuta una jugada que *de por sí* es satisfactoria (kill múltiple, counter perfecto), y el juego responde amplificando ese momento con una habilidad temporal que cambia la arena. No es un drop random — es **causalidad directa**: el estilo se transforma en poder, y el poder permite más estilo.

Referencia: **DMC** (style meter desbloquea ataques), **Bayonetta** (Witch Time tras dodge perfecto), **Vampire Survivors** (rosario = explosion of power tras un build-up).

## Detailed Design

### Core Rules

#### Regla 1 — Catálogo MVP (3 powerups)

| # | Nombre | Efecto | Duración | Tipo |
|---|---|---|---|---|
| 1 | **Slow Time** | `Time.timeScale = 0.4`. El input del player se compensa para mantener velocidad real percibida. Todo lo demás va al 40%. | 8s | Control temporal |
| 2 | **Overload Pulse** | Cada 1s el player emite un pulso radial de 6m que repele enemigos (knockback) y daña 1. Limpia y separa hordas. | 10s | Defensivo / AoE |
| 3 | **Magnetic Mine** | Planta **una mina única** en la posición exacta donde el jugador completó la misión. Se arma tras 0.5s y queda inerte hasta que un enemigo la pise. Al detonar: **AoE 10m**, aplica +2 marcas magnéticas (magnetiza) a TODOS los enemigos en el área **+ 4 dmg** radial. Una mina activa a la vez. | hasta 30s sin pisar (timeout) | Trampa / setup magnético |

#### Regla 2 — Solo 1 powerup activo
Si se activa un nuevo powerup mientras otro está activo:
- El anterior se cancela (`Deactivate()` se llama, modifiers se revierten).
- El nuevo se activa.
- **Excepción Magnetic Mine:** la mina plantada **persiste** como entidad independiente. Sin embargo, si Magnetic Mine es el powerup que se vuelve a sortear y **ya hay una mina activa**, la anterior se destruye sin detonar (solo 1 mina activa a la vez).

#### Regla 3 — Activación: solo por mission complete
- **No hay pickups en arena**.
- **No hay drops de boss en MVP**.
- Cada powerup activado es **random con pesos temáticos** definidos por la misión completada (ver `mission-system.md` Regla 4).
- Se activa **inmediatamente** al recibir `OnMissionComplete(weights)`, sin UI de elección.
- Banner breve en pantalla: `"MISSION! → ¡SLOW TIME!"` por 1s.
- **Magnetic Mine** captura la **posición del player en el momento exacto** de `OnMissionComplete` y planta ahí. El feedback visual lo conecta a la jugada que disparó la misión (la mina aparece donde "ganaste").

| Fuente | Activación |
|---|---|
| Mission complete | Inmediata, weighted random según misión completada |

#### Regla 4 — Implementación
```csharp
interface IPowerupEffect {
    void Activate(PowerupContext ctx);
    void Tick(float deltaTime);
    void Deactivate(PowerupContext ctx);
}
```
- `Activate`: aplica modifiers, spawnea entidades (caso minas), inicia VFX/SFX/HUD.
- `Tick`: lógica per-frame (caso Overload Pulse: chequea timer del próximo pulso).
- `Deactivate`: revierte modifiers (no destruye entidades plantadas — esas tienen su propio lifecycle).

#### Regla 5 — Slow Time: player speed compensation
Slow Time setea `Time.timeScale = 0.4`, pero el player se mueve a velocidad real. Compensación en el motor del player:
```csharp
// En player-movement, durante isInSlowTime
float effectiveDt = Time.deltaTime / Time.timeScale;
```
El input también se compensa. Resultado neto: **2.5× tiempo de reacción percibido** sin que el player se sienta lento.

#### Regla 6 — Overload Pulse mecánica
- Al activar: timer interno = 0.
- Cada `pulseInterval` (1s):
  - `Physics.OverlapSphere(player.position, 6f, enemyLayer)` busca enemigos.
  - Para cada uno: aplica `AddForce` radial outward con `pulseForce` y daña 1.
  - Spawnea VFX de onda expansiva (ring particle, expansión 0→6m en 0.3s) + SFX "thump" grave.
- 10 pulsos totales en la duración (10s × 1 pulse/s).
- No interactúa con la órbita del player (no consume chatarra, no aplica al sistema magnetism).
- Aura magnética azul-eléctrica permanente alrededor del player durante el efecto.

#### Regla 7 — Magnetic Mine mecánica
- **Al activar (`OnMissionComplete`):**
  - Captura `mineSpawnPos = player.position` en ese frame.
  - Spawnea **una sola** instancia del prefab `MagneticMine` en `mineSpawnPos` (snap al ground via raycast).
  - Si ya existe una mina activa de este powerup → la anterior se destruye sin detonar (solo 1 mina en arena).
- **Estado armed (tras 0.5s):**
  - LED magnético azul-eléctrico parpadeante en el suelo.
  - Genera un **trigger collider** circular de `triggerRadius` (0.6m) sobre el suelo.
  - Cualquier enemigo que entra al trigger → **detona la mina** (no se requiere player presente).
- **Detonación (AoE 10m):**
  - `Physics.OverlapSphere(minePos, 10f, enemyLayer)` busca todos los enemigos en rango.
  - Por cada enemigo:
    - Aplica **+2 magnetic marks** (los lleva al estado `Magnetized` aunque estén `Normal`).
    - Aplica **4 dmg** (radial, sin falloff por distancia para mantenerlo legible).
    - Knockback radial leve (no knockdown — el rol es magnetizar, no derribar).
  - VFX: pulso magnético azul expansivo 0→10m en 0.4s + grid de líneas de fuerza visible 0.5s.
  - SFX: "thunk" magnético grave + chispas eléctricas.
- **Timeout (30s sin pisar):** la mina detona igual con el mismo efecto, evitando acumulación si el jugador se aleja del spot.
- **Propósito mecánico:** convierte el punto de la jugada espectacular en un **trap-setup** que magnetiza una horda entera, dejando todo listo para Pull/Repel masivo después. Combina natural con Wrecking Core o repulsión cargada.

#### Regla 8 — Timer / HUD
HUD muestra:
- Ícono del powerup activo (esquina inferior izquierda).
- Timer circular vaciándose (Slow Time, Overload Pulse).
- Indicador de mina activa (Magnetic Mine): worldspace marker sobre la mina + ícono pequeño "MINE READY" en HUD.
- Flash en los últimos 2s del timer.

### States and Transitions

```
Inactive ──[OnMissionComplete + RNG]──▶ Active ──[timer expires]──▶ Deactivating ──[0.3s]──▶ Inactive
                                            │
                                       [New powerup]
                                            ▼
                                       Cancel old → Activate new
```

### Interactions with Other Systems

| Sistema | Dirección | Datos | Interfaz |
|---|---|---|---|
| `mission-system` | upstream (Hard) | `OnMissionComplete` event + powerup weights | `ActivateWeightedPowerup(PowerupWeights)` |
| `meta-flow-system` | downstream (Hard, Slow Time) | `Time.timeScale` override | direct write con restore |
| `player-movement` | downstream (Hard, Slow Time) | deltaTime compensation | flag `isInSlowTime` |
| `combat-system` | downstream (Hard, Pulse) | aplicar repel + damage radial | `ApplyAreaRepel(pos, radius, force, damage)` |
| `attractables-system` | downstream (Hard, Mine) | nuevo prefab `MagneticMine` | registro de prefab |
| `combat-system` | downstream (Hard, Mine) | apply magnetic marks + damage en AoE 10m | `ApplyAreaMagnetize(pos, radius, marks, damage)` |
| `hud-system` | downstream (Soft) | active icon + timer + mine indicator | `ActivePowerup`, `RemainingTime`, `ActiveMine` |
| `presentation-system` | downstream (Soft) | VFX/SFX/post-FX por powerup | events `OnPowerupActivated/Deactivated` |

## Formulas

### Overload Pulse output
```
totalPulses = duration / pulseInterval = 10 / 1 = 10 pulsos
DPS sostenido por enemigo = 1 dmg × 1 pulse/s = 1 DPS
```
El propósito principal **no es DPS**, es separación + respiro. Daño secundario para finishear chatarra.

### Magnetic Mine output
```
AoE                  = 10m
damagePerEnemy       = 4 (flat, sin falloff)
magnetizeMarks       = +2 (eleva Normal → Magnetized en 1 tick)
typicalEnemiesInAoE  = 4-8 (en una horda densa)
maxDamageBurst       = 4 dmg × 8 enemies = 32 dmg
strategicValue       = >> daño puro: convierte 4-8 enemigos en munición instantánea
timeoutFallback      = 30s
```

### Slow Time player speed (perceived)
```
effectivePerceivedSpeed = baseSpeed × (1 / timeScale) = 5 × 2.5 = 12.5 m/s percibido
real speed: 5 m/s. Enemigos: 0.4 × baseSpeed.
```

## Edge Cases

### E1 — Slow Time + level up
Si level up dispara durante Slow Time → `Time.timeScale = 0` (pausa upgrade choice). Al salir, restaurar a **0.4** (no a 1.0). El timer del powerup **no avanza** durante la pausa.

### E2 — Player muere con powerup activo
`Deactivate()` se llama automáticamente. `Time.timeScale = 1`. **La mina plantada se destruye** (no persiste post-muerte para evitar daño "póstumo" raro).

### E3 — Overload Pulse sin enemigos en rango
El pulso se dispara igual (VFX visual), no aplica daño/repel. No cancela el efecto.

### E4 — Magnetic Mine plantada en pared / fuera del NavMesh
La posición de spawn = `player.position` en el frame del mission complete. Si por alguna razón el player está clipping en geometría no-walkable (raro), raycast hacia abajo busca el ground válido más cercano (max 2m). Si falla, snap al último `validGroundPos` registrado del player.

### E5 — Mine no es pisada por ningún enemigo
Tras 30s, detona automáticamente con efecto completo (timeout). Esto evita que la mina quede como decoración permanente si el jugador se aleja.

### E6 — Mission complete con Magnetic Mine ya activa en arena
Si el powerup vuelve a salir y ya hay una mina viva → la anterior se destruye sin detonar y se planta una nueva en la posición actual del player. Solo 1 mina activa a la vez (ver Regla 2).

### E7 — Magnetic Mine detona durante Slow Time
La detonación ocurre en `Time.timeScale = 0.4`, así que el VFX expansivo se ve en cámara lenta. El daño y las marcas se aplican igual (no usan deltaTime). Buen highlight visual.

### E8 — Dos misiones se completan en el mismo frame
Solo se activa **un** powerup. El primero en llegar gana; el segundo se ignora (no se encola). Razón: evitar double-cancel feedback confuso.

### E9 — Mission complete durante powerup activo
Se activa el nuevo powerup, cancela el actual (excepto la mina ya plantada — esa persiste si el powerup nuevo no es Magnetic Mine). Feedback claro: SFX de "swap" + flash dorado.

### E10 — Overload Pulse + Wrecking Core en órbita
El pulso no afecta a los objetos en órbita del player (solo enemigos). El Wrecking Core sigue normal en su slot.

### E11 — Enemigo magnetizado por la mina muere antes de ser usado
Sin problema — la marca expira con el enemigo. Otros enemigos magnetizados por la misma detonación quedan elegibles. El jugador tiene una ventana razonable (los magnetizados no se destmagnetizan por tiempo en MVP) para aprovechar la magnetización.

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `mission-system` | **Hard** — única fuente de activación |

### Downstream
| Sistema | Tipo |
|---|---|
| `meta-flow-system` | **Hard** — Slow Time |
| `player-movement` | **Hard** — Slow Time compensation |
| `combat-system` | **Hard** — Overload Pulse repel/damage |
| `attractables-system` | **Hard** — Magnetic Mines prefab |
| `hud-system` | **Soft** — UI display |
| `presentation-system` | **Soft** — VFX/SFX/post-FX |

## Tuning Knobs

| Knob | Default | Rango | Efecto si bajo | Efecto si alto |
|---|---|---|---|---|
| `slowTimeScale` | 0.4 | 0.2–0.7 | freeze | imperceptible |
| `slowTimeDuration` | 8s | 5–15 | nada | trivializa |
| `pulseInterval` | 1.0s | 0.5–2.0 | spam (overlap visual) | sin sensación de tick |
| `pulseRadius` | 6m | 3–10 | sin efecto | screen clear |
| `pulseForce` | 800 | 400–1500 | empuje sutil | enemigos a la pared |
| `pulseDamage` | 1 | 0–3 | sin daño | rompe el rol defensivo |
| `pulseDuration` | 10s | 6–15 | corto | OP |
| `mineAoERadius` | 10m | 6–15 | alcance pobre | medio mapa magnetizado |
| `mineDamage` | 4 | 2–8 | sin punch | one-shot horda |
| `mineMagnetizeMarks` | +2 | 1–3 | requiere strikes adicionales | autom. magnetizado siempre |
| `mineTriggerRadius` | 0.6m | 0.3–1.5 | difícil detonar | detonación accidental al rozar |
| `mineArmTime` | 0.5s | 0.2–1.5 | enemigos cercanos detonan instantáneo | enemigos pasan sin detonar |
| `mineTimeout` | 30s | 15–60 | mina expira sin uso | mina queda decoración eterna |

## Visual/Audio Requirements

- **Slow Time**: post-process desaturado + chromatic aberration leve. Audio pitch-shifted down. Trail de afterimages en el player. SFX de "drop" al activar.
- **Overload Pulse**: aura magnética azul-eléctrica permanente alrededor del player. Cada pulso = ring particle expansivo (0→6m en 0.3s) + SFX "thump" grave. Screen shake leve por pulso.
- **Magnetic Mine**: disco metálico plano sobre el suelo con LED **azul-eléctrico** parpadeante (más rápido cerca del timeout o cuando un enemigo se acerca). Línea magnética sutil player→mine durante el spawn. **Worldspace marker** flotando sobre la mina para que el jugador la ubique a distancia. Detonación = pulso azul expansivo 0→10m + grid de líneas magnéticas + screen shake medio + SFX "thunk + crackle eléctrico". Enemigos magnetizados muestran el VFX standard de magnetización (chispas + outline).
- **Activation general**: flash dorado en el player + SFX "powerup ascendente" + banner UI 1s.
- **Deactivation**: flash descendente + SFX "power down".

## UI Requirements

### MVP
- **Active powerup icon**: esquina inferior izquierda, ícono grande + timer circular.
- **Warning**: parpadeo del ícono en los últimos 2s.
- **Magnetic Mine**: badge "MINE READY" junto al ícono mientras la mina está activa. Cuando detona, badge desaparece y se muestra brevemente "MINE TRIGGERED". Worldspace marker sobre la posición de la mina para localización rápida.
- **Mission → powerup banner**: texto centrado breve `"MISSION! → ¡{POWERUP_NAME}!"` por 1s.

## Acceptance Criteria

1. **AC-1**: Solo 1 powerup activo a la vez. Activar uno nuevo cancela el anterior (excepto la mina ya plantada, que persiste hasta detonar/expirar — salvo si el powerup nuevo es Magnetic Mine y reemplaza la mina).
2. **AC-2**: Powerups se activan **únicamente** por `OnMissionComplete`. **No existen** pickups en arena ni drops de boss en MVP.
3. **AC-3**: Slow Time setea `Time.timeScale = 0.4` durante 8s y compensa el player para que se mueva a velocidad real percibida.
4. **AC-4**: Overload Pulse emite 10 pulsos a intervalos de 1s, cada uno repele y daña 1 a enemigos dentro de 6m del player.
5. **AC-5**: Magnetic Mine planta **una sola** mina en `player.position` capturada en el frame del `OnMissionComplete`. La mina arma tras 0.5s y queda inerte hasta que un enemigo la pise (trigger 0.6m).
6. **AC-6**: Al detonar (por trigger o timeout 30s), Magnetic Mine aplica **+2 magnetic marks** y **4 dmg** a TODOS los enemigos dentro de 10m, sin falloff por distancia.
7. **AC-7**: Solo 1 Magnetic Mine activa a la vez. Si se vuelve a sortear y ya hay una, la anterior se destruye sin detonar y se planta una nueva.
8. **AC-8**: Timer del powerup se muestra en HUD y se pausa durante upgrade choice screen.
9. **AC-9**: Al expirar/cancelar, todos los modifiers se revierten correctamente (timeScale, etc).
10. **AC-10**: Activación inmediata sin UI de selección — el powerup se elige random uniforme del pool de 3 y se aplica al recibir `OnMissionComplete`.
11. **AC-11**: El catálogo total son 3 powerups (Slow Time, Overload Pulse, Magnetic Mine). Magnet Fever **no existe** como powerup (sus stats están en upgrades vía Magnetic Reach + Quick Coil).
12. **AC-12**: La selección de powerup usa pesos temáticos provistos por la misión completada (no uniforme). Verificable con log de 100 activaciones.

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿El jugador debería poder elegir entre 2 powerups al completar misión, o random uniforme es mejor para mantener el ritmo? | cris | Post-playtest |
| Q2 | ¿Overload Pulse debería escalar con upgrades (ej. +1 daño por stack de Scrap Cannon)? Riesgo: rompe el rol defensivo. | cris | Post-MVP |
| Q3 | ¿Magnetic Mine debería tener un cap de distancia mínima entre detonación y player (ej. si el player está dentro del AoE, no recibe la magnetización pero ve el efecto)? Por ahora el AoE es solo para enemigos, pero quizá un buff de "self-magnet boost" sería interesante. | cris | Post-playtest |
| Q4 | ¿Cooldown global entre powerups (ej. 8s) para evitar encadenado de misiones rápidas? | cris | Post-playtest |
| Q5 | ¿Conviene un cuarto powerup ofensivo de respaldo si los 3 defensivos resultan monótonos en playtests?
