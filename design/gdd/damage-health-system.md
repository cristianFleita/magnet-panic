# damage-health-system

> **Status:** Prototype
> **Última actualización:** 2026-05-10
> **Capa:** Foundation · **Tier:** MVP · **Roadmap:** Día 1-2
> **Implements Pillars:** P2 (combate legible), P4 (riesgo/recompensa)

## Overview

El sistema de vida define cuánto aguanta el jugador, cuánto aguantan los
enemigos y cómo entran las curaciones al loop de run infinita. Es foundation:
combat, magnetism, pickups, HUD, scoring, wave-director y meta-flow leen sus
eventos.

## Run Infinita

La run principal es **endless score attack**. No hay duración fija ni victoria
por timer. El jugador juega hasta morir; el score final se compara en
leaderboard. Los 4-5 minutos son el primer hito fuerte de dificultad, no el
final.

### Mecánicas de la run

- **HP del jugador:** recurso principal de supervivencia. Al llegar a 0 termina la run.
- **HP de enemigos:** cada enemigo expone vida actual/máxima para barras y scoring.
- **Curación:** pickups restauran HP si el jugador no está full. No reviven.
- **Escalada:** el wave director sube presión por actos de 60-90 s.
- **Bosses:** aparecen como hitos de dificultad; derrotarlos da score/XP y la run sigue.
- **Leaderboard:** score final; tiempo sobrevivido como desempate.

## Implementación Actual

- `HealthPool`: lógica pura de vida, daño, curación y clamp.
- `CombatHealth`: componente Unity reusable con eventos de daño, curación,
  cambio de vida y muerte.
- `ArkhamCombatController`: el player usa `CombatHealth` y se bloquea al morir.
- `ArkhamEnemy`: los enemigos usan `CombatHealth` y exponen barra sobre la cabeza.
- `HealingPickup`: pickup de curación consumible.
- `PlayerHealthHud`: HUD 2D hecho con UI Toolkit.
- `WorldSpaceHealthBar`: barra espacial de enemigos con Canvas world-space.

## Reglas

1. El daño siempre clampa la vida entre 0 y máximo.
2. La curación siempre clampa al máximo.
3. Los pickups sólo curan si el target está vivo y no está full.
4. La muerte se emite una sola vez por entidad.
5. Las barras de enemigo se ocultan cuando el enemigo muere.
6. El HUD 2D pertenece a UI Toolkit; las barras sobre enemigos son UI world-space.

## Próximo Diseño

Cuando se diseñe `scoring-xp-system`, este sistema debe emitir o exponer:

- daño recibido por el jugador,
- muertes de enemigos,
- muerte del jugador,
- curaciones usadas,
- HP restante al final de una jugada grande.
