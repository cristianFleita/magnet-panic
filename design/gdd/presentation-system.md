# Presentation System

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Game feel — cada acción tiene peso, cada impacto se siente, cada momento grande es épico

## Overview

El Presentation System es la capa de "juiciness" — VFX, SFX, hitstop, slow-mo, screen effects, y partículas que transforman acciones mecánicas en momentos satisfactorios. No afecta el gameplay (es cosmético), pero define cómo se SIENTE el juego. Sin presentation, el combate funciona pero se siente vacío. Con presentation, cada hit tiene peso.

Hoy existe parcialmente: `ArkhamSimpleCameraFollow.Shake()`, partículas en `MagneticObject`, y trail renderers. El GDD consolida todo el feedback en un sistema unificado.

## Player Fantasy

**"Cada golpe PESA."**

La fantasía es sensorial: el jugador no solo ve que hizo daño — lo siente. El screen shake, el hitstop de 2 frames, el flash blanco del enemigo, el SFX metálico del impacto — todo junto crea la ilusión de peso y poder.

Referencia: **Hades** (hitstop perfecto, partículas de cada hit), **Celeste** (screen shake proporcional, freeze frames), **Vlambeer** (los inventores del "screen shake = game feel").

## Detailed Design

### Core Rules

#### Regla 1 — 9 Eventos obligatorios de feedback

Definidos en el GDD original (§15). Cada uno necesita VFX + SFX:

| # | Evento | VFX | SFX | Shake | Hitstop |
|---|---|---|---|---|---|
| 1 | **Strike hit** | Flash blanco en enemigo (0.1s) | Metal clang | 0.11-0.36, 0.14s | 2 frames |
| 2 | **Counter** | Pulse circular cyan | Shield bash + whoosh | 0.15, 0.16s | 3 frames |
| 3 | **Pull start** | Líneas convergentes al player | Magnetic hum start | — | — |
| 4 | **Repel fire** | Cone flash + speed lines | Electromagnetic pulse | 0.10, 0.10s | 1 frame |
| 5 | **Repel impact** | Sparks + debris en punto de impacto | Metal crash | 0.12, 0.12s | 2 frames |
| 6 | **Wall slam** | Impact decal + crack particles | Concrete slam + bass | 0.18, 0.15s | 3 frames |
| 7 | **Player damage** | Screen edge red flash + player flash | Pain grunt + impact | 0.20, 0.20s | 4 frames |
| 8 | **Enemy death** | Explosion particles + debris | Destruction crunch | 0.08, 0.10s | 1 frame |
| 9 | **Overload explosion** | Shockwave ring + flash + debris | Deep boom + silence 0.3s | 0.30, 0.30s | 6 frames |

#### Regla 2 — Hitstop (time freeze)

Hitstop congela el gameplay por N frames para enfatizar impactos:

```csharp
public void TriggerHitstop(int frames)
{
    if (hitstopCoroutine != null) StopCoroutine(hitstopCoroutine);
    hitstopCoroutine = StartCoroutine(HitstopRoutine(frames));
}

IEnumerator HitstopRoutine(int frames)
{
    float originalScale = Time.timeScale;
    Time.timeScale = 0f;
    for (int i = 0; i < frames; i++)
        yield return null; // WaitForEndOfFrame ignores timeScale
    Time.timeScale = originalScale;
}
```

El hitstop es corto (1-6 frames = 16-100ms a 60 FPS). Es imperceptible conscientemente pero crea una sensación de "peso" enormemente satisfactoria.

#### Regla 3 — Slow-mo para momentos grandes

Eventos épicos (boss kill, multi-kill ×5+, last enemy of wave) triggean un slow-mo breve:

```csharp
public void TriggerSlowMo(float timeScale, float duration)
{
    // timeScale = 0.3, duration = 0.5s (real time)
    StartCoroutine(SlowMoRoutine(timeScale, duration));
}
```

| Evento | TimeScale | Duration | Descripción |
|---|---|---|---|
| Boss kill | 0.2 | 0.8s | Casi freeze, dramático |
| Multi-kill ×5+ | 0.3 | 0.4s | Breve highlight |
| Last enemy of wave | 0.4 | 0.3s | Sutil, cierre satisfactorio |

#### Regla 4 — VFX pool

Todas las partículas se manejan vía `object-pooling` para evitar GC en WebGL. Cada efecto es un prefab pooleable con auto-despawn.

#### Regla 5 — SFX priorities

Si hay muchos sonidos simultáneos (ej: overload + 5 enemy deaths), el audio manager prioriza:
1. Player damage (siempre audible).
2. Overload/Boss events.
3. Impacts (últimos 3 más recientes).
4. Ambient (pull hum, orbit sounds).

Máximo 8 simultaneous AudioSources para WebGL performance.

#### Regla 6 — Post-processing

Efectos de cámara para estados especiales:

| Estado | Efecto |
|---|---|
| Overload Critical | Vignette roja pulsante |
| Slow Time powerup | Desaturación + chromatic aberration |
| Player low HP (≤1) | Vignette roja constante, heartbeat desaturación |
| Death | Desaturación progresiva + blur |

### Interactions with Other Systems

| Sistema | Datos consumidos | Feedback producido |
|---|---|---|
| `combat-system` | Strike, Counter, Damage events | VFX + SFX + Shake + Hitstop |
| `magnetism-system` | Pull, Repel, Orbit events | VFX + SFX |
| `overload-system` | Critical, Overload, Recovery events | VFX + SFX + Post-processing |
| `arena-system` | Wall slam events | VFX + SFX + Shake |
| `enemy-system` | Death events | VFX + SFX |
| `boss-system` | Phase transitions, boss death | VFX + SFX + Slow-mo |
| `scoring-xp-system` | Multi-kill, high combo | Slow-mo |
| `powerup-system` | Activation, deactivation | VFX + SFX + Post-processing |
| `camera-system` | Shake API | `cameraRig.Shake()` calls |

## Dependencies

### Upstream (all soft — presentation is cosmetic)
Todos los sistemas de gameplay son upstream — presentation reacciona a sus eventos.

### Downstream
| Sistema | Tipo |
|---|---|
| `camera-system` | **Hard** — shake API |
| `object-pooling` | **Hard** — VFX pool |

## Tuning Knobs

| Knob | Default | Rango |
|---|---|---|
| `hitstopFrames` (per event) | 1-6 | 0-10 |
| `shakeAmplitude` (per event) | 0.08-0.30 | 0-0.5 |
| `slowMoTimeScale` | 0.2-0.4 | 0.1-0.6 |
| `slowMoDuration` | 0.3-0.8s | 0.1-1.5s |
| `maxSimultaneousAudio` | 8 | 4-16 |
| `masterShakeIntensity` | 1.0 | 0-2.0 (accessibility: 0 = no shake) |

## Acceptance Criteria

1. **AC-1**: Los 9 eventos obligatorios tienen VFX + SFX implementados.
2. **AC-2**: Hitstop funciona correctamente (freeze N frames sin afectar input).
3. **AC-3**: Slow-mo en boss kill (0.2 timeScale, 0.8s).
4. **AC-4**: VFX usan object pooling (no Instantiate/Destroy).
5. **AC-5**: Máximo 8 AudioSources simultáneas en WebGL.
6. **AC-6**: Post-processing de overload critical (vignette roja).
7. **AC-7**: `masterShakeIntensity = 0` desactiva todos los shakes (accessibility).

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Música dinámica que escala con el acto? Acto 1 = chill, Acto 4 = intense. | cris | Post-MVP |
| Q2 | ¿Damage numbers flotantes? Tipo RPG. Agrega info pero puede clutterear la pantalla. | cris | Post-playtest |
