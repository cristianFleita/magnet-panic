# Systems Index — Magnet Panic: Scrapstorm

> **Última actualización:** 2026-05-10
> **Fuente:** `design/gdd/game-concept.md` (extracto del GDD jam `design/gdd-gamejam.md`)
> **Total de sistemas:** 21 · **MVP:** 21 · **Diseñados:** 0

Este índice es la fuente de verdad para qué sistemas existen, en qué orden se
diseñan, y qué dependencias tienen. Se actualiza cada vez que un sistema
termina su GDD individual o cambia su estado.

---

## Progreso

| Estado | Cantidad |
|---|---:|
| Not Started | 20 |
| In Design | 0 |
| Designed | 1 |
| In Review | 0 |
| Approved | 0 |

---

## Sistemas (tabla principal)

| # | Sistema | Capa | Tier | Roadmap | Estado | GDD |
|---:|---|---|---|---|---|---|
| 1 | `damage-health-system` | Foundation | MVP ⚠ | Día 1-2 | Not Started | — |
| 2 | `input-system` | Foundation | MVP | Día 1 | Not Started | — |
| 3 | `arena-system` | Foundation | MVP | Día 1 | Not Started | — |
| 4 | `object-pooling` | Foundation | MVP | Día 1-2 | Not Started | — |
| 5 | `player-movement` | Core | MVP | Día 1 | Not Started | — |
| 6 | `camera-system` | Core | MVP | Día 1 | Not Started | — |
| 7 | `magnetism-system` | Core | MVP ⚠ | Día 1 | Designed | [magnetism-system.md](magnetism-system.md) |
| 8 | `overload-system` | Core | MVP | Día 1-3 | Not Started | — |
| 9 | `attractables-system` | Content | MVP | Día 2 | Not Started | — |
| 10 | `enemy-system` | Content | MVP ⚠ | Día 4 | Not Started | — |
| 11 | `combat-system` | Content | MVP | Día 3 | Not Started | — |
| 12 | `wave-director` | Encuentros | MVP | Día 4-5 | Not Started | — |
| 13 | `scoring-xp-system` | Meta | MVP | Día 5 | Not Started | — |
| 14 | `upgrade-system` | Meta | MVP | Día 5 | Not Started | — |
| 15 | `mission-system` | Meta | MVP | Día 6 | Not Started | — |
| 16 | `powerup-system` | Meta | MVP | Día 6 | Not Started | — |
| 17 | `boss-system` | Meta | MVP* | Día 7 | Not Started | — |
| 18 | `hud-system` | Presentation | MVP | Día 8 | Not Started | — |
| 19 | `presentation-system` | Presentation | MVP | Día 8 | Not Started | — |
| 20 | `host-bridge` | Boundary | MVP | Día 10 | Not Started | — |
| 21 | `meta-flow-system` | Boundary | MVP | Día 10 | Not Started | — |

**Leyenda:**
- ⚠ = Bottleneck (muchos dependientes, alto riesgo de cambio)
- \* = Candidato a recorte si el día 7 va apretado (`boss-system`); fallback es
  victoria por extracción/score

---

## Descripción breve por sistema

### Foundation

- **`damage-health-system`** — HP por entidad, aplicación de daño, knockback,
  invulnerabilidad temporal, evento de muerte. Base usada por player, enemies,
  attractables (impactos) y boss.
- **`input-system`** — Wrapper sobre Unity New Input System. Expone intentos
  abstractos (`Move`, `Aim`, `PullHold`, `PullRelease`, `Strike`, `Counter`,
  `UpgradeChoice`). Aísla rebinding y diferencias de plataforma.
- **`arena-system`** — Geometría del nivel, paredes (críticas para "wall slam"),
  límites de cámara, colisiones estáticas, spawn points.
- **`object-pooling`** — Pool genérico para chatarra, proyectiles, enemigos
  y partículas. Crítico para WebGL (evitar GC en runs intensas).

### Core

- **`player-movement`** — 8 direcciones, velocidad base 5, penalización -20%
  con carga completa, invulnerabilidad 0.5s tras daño.
- **`camera-system`** — Top-down/iso con seguimiento suave. Soporta hooks de
  shake del `presentation-system`.
- **`magnetism-system`** — Núcleo del juego. Pull (campo de atracción),
  órbita (retención de objetos), Repel (disparo cónico). Define el contrato
  de "marca magnética" que escriben combat y leen pull. Owner del recurso
  "carga actual" (capacidad).
- **`overload-system`** — Estados Normal / Crítico / Overload. Sube con
  retención excesiva y atracción de enemigos. Al llenarse: explosión radial,
  empuja enemigos, vacía carga, vulnerabilidad 0.5s.

### Content

- **`attractables-system`** — Chatarra liviana, placa, mina, objeto pesado.
  Cada uno con capacidad, daño, velocidad, comportamiento al impactar
  (mina explota, placa bloquea proyectiles).
- **`enemy-system`** — Scrapling (horda), Runner Bot (carga lineal con aviso),
  Shield Bot (bloquea frontal), Spitter Drone (opcional, proyectiles
  reatrayables). Hospeda el estado de marca magnética por instancia.
- **`combat-system`** — Strike (cono frontal 70°, daño bajo, aplica marca).
  Counter / pulso (ventana 0.35-0.5s, espacio, repele atacante). Combo
  tracking. Magnetiza enemigos a 2 stacks de marca.

### Encuentros

- **`boss-system`** — Scrap Brute. 4 fases: persigue, absorbe chatarra,
  sobrecarga, ventana vulnerable. Invoca Scraplings.
- **`wave-director`** — Timeline scripted del run (§13 GDD). Eventos a
  tiempo fijo: 0:20 Scraplings, 1:00 Runner, 2:00 Shield, 3:30 Brute, etc.

### Meta

- **`scoring-xp-system`** — Scrap XP por kill base, kill con repel (medio),
  combo multi-kill (alto), counter perfecto, misión, reciclaje.
- **`upgrade-system`** — Choice de 3 al level up. Pools: Pull / Repel /
  Combo / Capacidad / Especiales. 9-12 upgrades MVP.
- **`mission-system`** — Misión activa cada 45-60s. Combo Hunter, Counterstorm,
  Scrap Collector, Wall Slam, No Hands. Recompensa XP/curación/powerup/reroll.
- **`powerup-system`** — Repeler 360, Slow Time, Magnet Fever, Enemy Pull.
  Temporales, modifican magnetism/enemy en runtime.

### Presentation

- **`hud-system`** — HP, barra sobrecarga, capacidad, XP, combo, misión
  activa, cooldown counter, indicador magnetizado, cono de repulsión.
- **`presentation-system`** — Game feel + audio + VFX. Hitstop breve,
  screen shake moderado, slow-mo en jugadas grandes, combo popups,
  partículas de pull/repel, alerta de sobrecarga, SFX de los 9 eventos
  obligatorios del GDD §15.

### Boundary

- **`host-bridge`** — Contrato Unity ↔ React vía `react-unity-webgl`.
  Eventos out: `RunStarted`, `RunEnded(score, stats)`, `ScoreSubmitted`.
  Eventos in: `StartRun`, `RestartRun`, `ApplySettings`.
- **`meta-flow-system`** — State machine del shell: Menu → Tutorial (visual,
  0:00-0:20) → Run → Score Screen. Pause. Restart. Condiciones de
  victoria/derrota.

---

## Dependencias (capas)

```
Layer 0 — Foundation
  damage-health-system    (sin deps)
  input-system            (sin deps)
  arena-system            (sin deps)
  object-pooling          (sin deps)

Layer 1 — Core
  player-movement         ← input, arena
  camera-system           ← player-movement
  magnetism-system        ← input, player-movement, object-pooling, damage-health
  overload-system         ← magnetism-system

Layer 2 — Content base
  enemy-system            ← damage-health, arena, pool, player-movement, magnetism
  attractables-system     ← magnetism-system, damage-health, pool
  combat-system           ← input, player-movement, damage-health, enemy-system

Layer 3 — Encuentros / scoring
  boss-system             ← enemy-system, attractables-system, damage-health
  wave-director           ← enemy-system, pool
  scoring-xp-system       ← combat-system, magnetism-system

Layer 4 — Customización en run
  upgrade-system          ← scoring-xp, magnetism, combat, damage-health, overload
  mission-system          ← combat, magnetism, scoring-xp
  powerup-system          ← magnetism, enemy-system, wave-director

Layer 5 — Presentation
  hud-system              ← damage-health, magnetism, overload, scoring-xp,
                            mission, combat
  presentation-system     ← damage-health, combat, magnetism, overload,
                            scoring-xp (consume eventos)

Layer 6 — Boundary
  host-bridge             ← scoring-xp (final score), meta-flow
  meta-flow-system        ← scoring-xp, wave-director, boss-system,
                            damage-health, host-bridge
```

**Sin dependencias circulares.**

Nota sobre la marca magnética (acoplamiento sutil):
- `magnetism-system` define el contrato (estados Normal/Marcado/Magnetizado/Aturdido + reglas).
- `enemy-system` hospeda el estado por instancia.
- `combat-system` lo escribe (Strike aplica marca; 2 stacks → Magnetizado).
- `magnetism-system` lo lee (Pull arrastra Magnetizado).

Esto evita el ciclo: el contrato es upstream, el estado es runtime.

---

## Bottlenecks (alto riesgo)

Estos sistemas tienen muchos dependientes. Cambiarlos después de que sus
consumidores estén implementados es caro. Diseñar y congelar pronto.

| Sistema | # dependientes directos | Por qué importa |
|---|---:|---|
| `damage-health-system` | ~10 | Toda fuente/receptor de daño lo usa |
| `magnetism-system` | ~8 | Núcleo del juego, define el contrato de marca |
| `enemy-system` | ~6 | Hospeda mark state, target de wave-director, boss extiende |

Recomendación: prototipar `magnetism-system` el día 1 con ` /prototype` antes
de cerrar el GDD final, porque su game feel decide el éxito del juego (criterio
#2 de éxito en `game-concept.md`).

---

## Orden de diseño recomendado

Diseñar GDDs en este orden. Cada uno usa `/design-system <nombre>`.

### Bloque 1 — Foundation (día 1, antes de codear)

1. `damage-health-system` (bottleneck #1, sin deps)
2. `input-system`
3. `arena-system`
4. `object-pooling`

### Bloque 2 — Core gameplay (día 1, núcleo magnético)

5. `player-movement`
6. `magnetism-system` ⚠ (bottleneck, núcleo, prototipar antes de cerrar)
7. `camera-system`
8. `overload-system`

### Bloque 3 — Contenido base (días 2-4)

9. `attractables-system`
10. `enemy-system` ⚠ (bottleneck, hospeda mark state)
11. `combat-system`

### Bloque 4 — Encuentros y meta (días 4-6)

12. `wave-director`
13. `scoring-xp-system`
14. `upgrade-system`
15. `mission-system`
16. `powerup-system`

### Bloque 5 — Boss y presentación (días 7-8)

17. `boss-system` (candidato a recorte)
18. `hud-system`
19. `presentation-system`

### Bloque 6 — Cierre (día 10)

20. `host-bridge`
21. `meta-flow-system`

---

## Sistemas fuera del MVP (Full Vision)

Listados en `game-concept.md` como descartados explícitamente para la jam:

- Co-op multiplayer
- Polaridad avanzada (más allá de magnetizado básico)
- Meta-progresión entre runs (unlocks persistentes)
- Múltiples biomas / arenas
- Leaderboards online
- Boss complejo (más allá de Scrap Brute simple)
- Animaciones elaboradas
- Narrativa scripted

Si la jam se extiende o se hace post-jam, estos vuelven a entrar al index.

---

## Próximos pasos

1. **`/design-system damage-health-system`** — bottleneck #1, foundation pura.
2. **`/design-system magnetism-system`** — núcleo del juego, alto riesgo de
   diseño. Considerar `/prototype magnetism` antes para validar game feel.
3. Seguir el orden de diseño bloque por bloque.

Después de cada `/design-system`, este índice se actualiza automáticamente
con el nuevo estado y enlace al GDD individual.
