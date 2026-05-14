# Host Bridge

> **Status**: In Design
> **Author**: cris + agents
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Integración web — el juego Unity vive dentro de una app React y se comunican bidirección

## Overview

El Host Bridge es la interfaz de comunicación entre el runtime de Unity (WebGL build) y la app React que lo hostea vía `react-unity-webgl`. Define los eventos que Unity envía a React (run ended, score submit) y los comandos que React envía a Unity (start run, restart, apply settings). Es un sistema Boundary que no afecta gameplay pero es crítico para la experiencia web completa.

No hay código existente — diseño nuevo basado en la API de `react-unity-webgl`.

## Player Fantasy

Ninguna — este sistema es invisible para el jugador. Si funciona, el jugador nunca lo nota. Si falla, el juego no arranca.

## Detailed Design

### Core Rules

#### Regla 1 — Unity → React (Outbound events)

Implementados via `.jslib` que llaman a `window.dispatchReactUnityEvent()`:

| Evento | Payload | Cuándo |
|---|---|---|
| `GameReady` | `{}` | Unity terminó de cargar, listo para recibir comandos |
| `RunStarted` | `{ timestamp }` | El gameplay comienza (post-tutorial/countdown) |
| `RunEnded` | `{ score, wave, survivalTime, maxCombo, kills, deathCause }` | Player muere |
| `ScoreSubmitted` | `{ score, playerName }` | Score enviado al leaderboard |
| `StateChanged` | `{ state: "menu"|"playing"|"dead"|"paused" }` | Transición de estado del meta-flow |

#### Regla 2 — React → Unity (Inbound commands)

Implementados via `SendMessage()` de react-unity-webgl a un GameObject `HostBridge`:

| Comando | Payload | Efecto |
|---|---|---|
| `StartRun` | `{}` | Inicia una nueva run (menu → gameplay) |
| `RestartRun` | `{}` | Restart desde death screen |
| `ApplySettings` | `{ sfxVolume, musicVolume, shakeIntensity }` | Aplica settings de la UI React |
| `PauseGame` | `{}` | Pausa el gameplay |
| `ResumeGame` | `{}` | Resume el gameplay |

#### Regla 3 — Singleton MonoBehaviour

```csharp
public sealed class HostBridge : MonoBehaviour
{
    static HostBridge instance;
    public static HostBridge Instance => instance;
    
    void Awake() { instance = this; }
    
    // Outbound
    public void NotifyRunEnded(RunStats stats) => SendToReact("RunEnded", JsonUtility.ToJson(stats));
    
    // Inbound (called by SendMessage from React)
    public void StartRun() => MetaFlowSystem.Instance.StartRun();
    public void ApplySettings(string json) => SettingsManager.Apply(JsonUtility.FromJson<Settings>(json));
    
    void SendToReact(string eventName, string payload)
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        SendReactEvent(eventName, payload); // jslib extern
        #endif
    }
}
```

#### Regla 4 — Editor fallback

En el Editor (no WebGL), los comandos inbound se simulan con teclas:
- F5 = StartRun
- F9 = RestartRun
- Los settings usan defaults.

Los eventos outbound se logean a Console.

### Interactions with Other Systems

| Sistema | Dirección | Datos |
|---|---|---|
| `meta-flow-system` | **bidirectional** | Commands trigger state changes; state changes trigger events |
| `scoring-xp-system` | **upstream** | Final score + stats para RunEnded payload |
| `presentation-system` | **downstream** | Volume settings para SFX/music |
| `camera-system` | **downstream** | Shake intensity setting |

## Dependencies

### Upstream
| Sistema | Tipo |
|---|---|
| `meta-flow-system` | **Hard** — state machine que responde a commands |
| `scoring-xp-system` | **Hard** — stats para RunEnded |

### Downstream
Ninguno — es terminal hacia React.

## Acceptance Criteria

1. **AC-1**: `GameReady` se emite al completar la carga de Unity.
2. **AC-2**: `StartRun` desde React inicia el gameplay.
3. **AC-3**: `RunEnded` envía score, wave, survivalTime, kills a React.
4. **AC-4**: `ApplySettings` modifica volumen y shake intensity en runtime.
5. **AC-5**: En Editor, todo funciona con fallbacks (teclas + console logs).
6. **AC-6**: No hay crashes si React no está escuchando (null-safe).

## Open Questions

| # | Pregunta | Owner | Target |
|---|---|---|---|
| Q1 | ¿Leaderboard server-side o local storage? Server = compartido, pero requiere backend. Local = simple para jam. | cris | Pre-implementation |
| Q2 | ¿Analytics events? Tracking de session length, upgrades elegidos, causa de muerte. Útil para balanceo post-jam. | cris | Post-MVP |
