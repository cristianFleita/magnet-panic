# Tutorial System — Magnet Panic: Scrapstorm

**Versión:** 0.1
**Estado:** diseño (pre-implementación)
**Motor:** Unity 6 (6000.3.6f1) — WebGL
**Scope:** experiencia obligatoria al iniciar por primera vez; opcional vía menú "How to Play".
**Tiempo objetivo del jugador:** 90–120 s para completarlo de punta a punta.

---

## 1. Objetivo del tutorial

Enseñar, en orden de criticidad, los seis bloques que componen una run real:

1. **Movimiento y cámara** — WASD + mouse.
2. **Combate magnético — Pull / Strike / Repel** — los tres verbos centrales del GDD §3.2.
3. **Counter** — la ventana defensiva (Espacio) que también construye combo.
4. **Misiones de estilo** — qué son, cómo aparecen, cómo se completan.
5. **Upgrades** — qué pasa al subir de nivel, cómo se elige.
6. **Powerups y puntaje** — qué desbloquean las misiones y cómo se traduce todo en score.

> **Regla 0:** ningún paso del tutorial introduce más de **un verbo o sistema nuevo**.
> Si el jugador necesita aprender dos cosas a la vez, **divídelo en dos pasos**.

Criterio de éxito (alineado con GDD §19):

- Se entiende en menos de **20 segundos por paso**.
- Cada paso termina con una jugada **espectacular pequeña**, no solo "presionaste la tecla".
- Al salir del tutorial el jugador puede empezar una run "real" sin tooltips.

---

## 2. Por qué un tutorial dedicado (y no in-run)

La curva de Magnet Panic es **acoplada**: Repel sin Pull no hace nada, Strike sin Magnetizar es solo daño chico, el Counter requiere un enemigo telegrafiando ataque. Enseñarlo "orgánicamente" dentro de una oleada normal expone al jugador a estímulos que aún no entiende.

Decisión: **escena separada** (`TutorialScene.unity`) con:

- arena reducida (~60% del tamaño normal),
- spawns guionados (no `WaveDirector`),
- enemigos placeholder con HP elevado y daño nulo o muy bajo,
- HUD con overlay de instrucción + highlight visual del input requerido,
- skip explícito ("Skip Tutorial" → carga `GameScene` directamente).

El tutorial reutiliza los sistemas reales (`MagnetismController`, `ArkhamCombatController`, `MissionSystem`, `UpgradeSystem`, `PowerupController`, `ScoringRuntime`), no fakes. El nuevo componente clave es **`TutorialDirector`**, que reemplaza al `WaveDirector` y a parte de `RunController` para esta escena.

---

## 3. Pasos del tutorial (guion completo)

Cada paso lista: **objetivo**, **setup de escena**, **condición de éxito** y **feedback obligatorio**.

> Todos los textos del HUD están en inglés porque el código y el juego shippean en inglés. El GDD permanece en español.

### Paso 1 — Movement

- **Objetivo:** que el jugador descubra WASD y la cámara top-down.
- **Setup:** arena vacía, jugador en el centro, cámara `ArkhamSimpleCameraFollow` ya configurada.
- **HUD:**
  - Top center: `"Move with W A S D"`
  - Highlight de las teclas (sprite del HUD).
- **Condición de éxito:** el jugador recorre **≥ 4 m** de distancia acumulada.
- **Feedback:** al cumplir, ✅ check verde, sonido suave de "step complete", la siguiente prompt aparece.
- **Falla suave:** si pasan 8 s sin moverse, la prompt parpadea y se reproduce voz/SFX recordatorio.

### Paso 2 — Aim

- **Objetivo:** comprender que el mouse rota la dirección de mira (no la cámara).
- **Setup:** aparece un **reticle indicator** (cursor del juego) y un marcador fantasma del cono frontal (`StrikeTargetIndicator` o un mesh world-space).
- **HUD:** `"Move your mouse to aim"`.
- **Condición de éxito:** la dirección de mira del jugador cambia **≥ 90°** acumulados.
- **Feedback:** cono frontal del jugador se ilumina cuando se mueve la mira; el reticle se "engancha" visualmente al primer enemigo cuando lo haya.

### Paso 3 — Pull (atraer chatarra)

- **Objetivo:** primer contacto con magnetismo, sin presión.
- **Setup:** spawnea **5 piezas de chatarra liviana** alrededor del jugador a ~4 m. No hay enemigos.
- **HUD:**
  - `"Hold Left Click to PULL scrap into orbit"`
  - Indicador circular del **radio de pull** (5 m) dibujado en el piso usando `LineRenderer` o decal world-space.
- **Condición de éxito:** **≥ 3 objetos** orbitando simultáneamente (lectura: `MagnetismController.OrbitingCount`).
- **Feedback:**
  - Sonido continuo de atracción mientras se mantiene LMB (ya existe en `MagnetismVfxBinder`).
  - VFX al entrar en órbita.
  - Counter en HUD: `Capacity 3 / 8`.
- **Edge case:** si el jugador suelta LMB con todo en órbita, no pasa nada, sigue el paso esperando el conteo.

### Paso 4 — Repel (descarga)

- **Objetivo:** descubrir que la chatarra orbital es ammo.
- **Setup:** sigue la chatarra del paso 3. Aparece un **muñeco objetivo (training dummy)** estático a 8 m, con HP visible (barra `WorldSpaceHealthBar`).
- **HUD:** `"Release / second click to REPEL toward the cursor"`.
  - Cono de repulsión dibujado world-space (50°, ya está en `MagnetismController`).
- **Condición de éxito:** el dummy queda destruido por el impacto de chatarra repelida.
- **Feedback:** hitstop (~80 ms), shake moderado, número de daño con `DamageNumberSpawner`, sonido potente de repulsión.

### Paso 5 — Strike

- **Objetivo:** introducir el ataque cuerpo a cuerpo en cono.
- **Setup:** spawnea **1 Scrapling pasivo** (no agrede, HP elevado para sobrevivir al primer hit) a 3 m frente al jugador.
- **HUD:** `"Right Click to STRIKE"`.
- **Condición de éxito:** 1 strike conectado al Scrapling.
- **Feedback:** el enemigo recibe **1 marca magnética**; aparece un icono "1/2" sobre su cabeza (usar el indicador de `MagneticObject`/`StrikeTargetPresenter`).

### Paso 6 — Magnetize (2× strike)

- **Objetivo:** que el jugador entienda que **2 strikes = enemigo magnetizado**.
- **Setup:** mismo Scrapling del paso 5, sigue pasivo.
- **HUD:** `"Strike again to MAGNETIZE the enemy"`.
- **Condición de éxito:** el Scrapling alcanza el estado **Magnetized**.
- **Feedback:** brillo/aura magnético en el enemigo (`MagnetismVfxBinder`), tick de sonido distintivo, el HUD muestra ícono "Magnetized".

### Paso 7 — Pull → Repel (el combo central)

- **Objetivo:** revelar que el enemigo magnetizado es proyectil.
- **Setup:** misma escena. Aparece un **segundo enemigo objetivo** (Scrapling pasivo) a 7 m, marcado con flecha "Target".
- **HUD:**
  - `"Hold Left Click to PULL the magnetized enemy"`
  - Al pegarse al jugador: `"Now REPEL toward the marked target"`.
- **Condición de éxito:** el segundo Scrapling muere por **impacto del Scrapling lanzado** (`KillMethod.RepelEnemy` en `KillContext`).
- **Feedback:** slow-mo breve (200 ms) al impacto, popup `+COMBO`, primer combo del jugador.
- **Pedagogía clave:** este es el "momento eureka". El paso debe permitir reintentos sin reiniciar — si el primer Repel falla, respawnea el target.

### Paso 8 — Counter / pulso (Espacio)

- **Objetivo:** introducir defensa activa y ventana de timing.
- **Setup:** spawnea **1 Runner Bot** scripteado para hacer **un solo charge telegrafiado** hacia el jugador, sin moverse antes. Telegrafía clara: línea roja y SFX wind-up de 0.8 s (más largo que en juego real para tutorial).
- **HUD:**
  - Antes del telegraph: `"Watch for the red line — that's an attack telegraph"`
  - Durante telegraph: `"Press SPACE to COUNTER"` con un anillo que se cierra alrededor del Runner.
- **Condición de éxito:** counter correcto (`ArkhamCombatController.OnCounterPerformed`).
- **Feedback obligatorio:**
  - Slow-mo de **1 s** (alineado con upgrade `Counter perfecto` del GDD §11.3 para anticipar la sensación).
  - Repel automático del atacante.
  - Popup `COUNTER!` grande.
- **Falla suave:** si el jugador no llega a tiempo, **no recibe daño** (el daño se anula en tutorial), el Runner se reposiciona y reintenta hasta 3 veces. Tras 3 fallos: prompt extra `"Press SPACE just as the line flashes"` y un cuarto intento más lento (1.2 s).

### Paso 9 — Overload (sobrecarga)

- **Objetivo:** mostrar la consecuencia de retener mucho metal.
- **Setup:** spawnea una **nube grande de chatarra** (12+ piezas) y un grupo de **4 enemigos pasivos** alrededor.
- **HUD:**
  - `"Hold Pull until your Overload bar fills"` + flecha al medidor de sobrecarga.
  - Cuando la barra llega al 90%: `"Release for an OVERLOAD PULSE"`.
- **Condición de éxito:** se dispara la explosión radial de `OverloadController` y golpea al menos 1 enemigo.
- **Feedback:** flash blanco, shake fuerte (curva moderada por WebGL), sonido "thump" grave, los enemigos vuelan.
- **Importante:** en este paso **se desactiva el daño al jugador**; el GDD §6.9 dice que en jam no daña, igual.

### Paso 10 — Mission (misión de estilo)

- **Objetivo:** enseñar que las misiones aparecen y dan recompensa.
- **Setup:** se activa programáticamente la misión `ComboHunter` (variante tutorial: target `2` en vez de `4`, duración 30 s, gate `Always`). Spawnean **6 Scraplings agrupados** a media distancia.
- **HUD:**
  - Mission card slide-in: `"Combo Hunter — kill 2 enemies in one Repel"`.
  - Timer visible.
- **Condición de éxito:** misión completada (no expirada).
- **Feedback:** banner `MISSION COMPLETE` con efecto dorado, sonido de logro, XP popup, **recompensa de powerup desbloqueada (paso 12)**.
- **Falla suave:** si expira sin éxito, la misión se reinicia con un toast `"Try again — pull them together first"` (máx 2 reintentos antes de avanzar igual).

### Paso 11 — Level Up & Upgrade

- **Objetivo:** que el jugador entienda la elección entre 3 upgrades.
- **Setup:** al completar el paso 10 (o al fallar 2 veces) se fuerza un **level up** vía `UpgradeSystem.ForceLevelUp(tutorialPool)`. La pool del tutorial expone solo **3 upgrades pedagógicamente claros**:
  - `MagneticReach` ("+20% pull radius")
  - `ScrapCannon` ("+25% repel velocity")
  - `DeepPockets` ("+3 capacity")
- **HUD:** `UpgradeChoiceHud` aparece, fondo se oscurece, tiempo de juego en pausa.
- **Texto:** `"Pick an UPGRADE — it stays for the whole run"`.
- **Condición de éxito:** el jugador elige una opción (mouse o teclas 1-3).
- **Feedback:** flash del stat afectado en el HUD (radio que se expande visible si eligió `MagneticReach`), sonido power-up.

### Paso 12 — Powerup

- **Objetivo:** enseñar que misiones drop powerups temporales y son distintos a los upgrades.
- **Setup:** se otorga `SlowTime` automáticamente al completar el paso 10 (vía `PowerupController.Grant`). Aparecen **3 enemigos cargando hacia el jugador**.
- **HUD:**
  - Icono de powerup parpadeando con duración.
  - Texto: `"POWERUPS are temporary. Survive the wave!"`.
- **Condición de éxito:** sobrevivir 4 s con `SlowTime` activo.
- **Feedback:** distorsión visual de slow-mo, audio low-pass.
- **Diferencia explícita vs upgrade:** un toast pedagógico al final: `"Powerups expire. Upgrades stay."`.

### Paso 13 — Score & Combo

- **Objetivo:** explicar el HUD de combo y el contador de score.
- **Setup:** spawnea **8 Scraplings pasivos** en formación cerrada.
- **HUD:**
  - `"Chain kills to raise your COMBO multiplier"`.
  - Contador de combo visible en grande.
- **Condición de éxito:** combo ≥ x5.
- **Feedback:** la barra/cifra de combo crece con cada kill; el score acumulado aparece grande al final con `"Run Score: 1,240"` para que se asocie a la idea de leaderboard.

### Paso 14 — Outro

- **HUD:**
  - `"You've got it. Now survive."`
  - Botón: `[ Start Run ]` y `[ Replay Tutorial ]`.
- **Acción:** `[Start Run]` carga `GameScene`, marca `PlayerPrefs.HasCompletedTutorial = 1`. `[Replay]` recarga `TutorialScene`.

---

## 4. Contenido de la escena `TutorialScene.unity`

Estructura jerárquica propuesta (alineada con `RunBootstrap`):

```
TutorialScene
├── --- Bootstrap ---
│   └── TutorialBootstrap (GameObject)
│       ├── TutorialBootstrap.cs        # análogo a RunBootstrap pero usa TutorialDirector
│       └── TutorialDirector.cs         # secuenciador de pasos
├── --- Arena ---
│   └── Arena (instancia de Arena.prefab, escalada ~0.6)
│       └── Spawn anchors (children):
│           ├── ScrapClusterAnchor (paso 3)
│           ├── DummyAnchor       (paso 4)
│           ├── ScraplingAnchor   (pasos 5–7)
│           ├── RunnerAnchor      (paso 8)
│           ├── OverloadCluster   (paso 9)
│           ├── MissionCluster    (paso 10)
│           └── ComboCluster      (paso 13)
├── --- Player ---
│   └── (instancia de MainCharacter.prefab — sin cambios)
├── --- Camera ---
│   └── Main Camera (ArkhamSimpleCameraFollow, configurada a player)
├── --- Systems ---
│   ├── EnemyManager (instancia de prefab existente)
│   ├── MissionSystem (configurada con MissionCatalog_Tutorial)
│   ├── UpgradeSystem (configurada con UpgradeCatalog_Tutorial — 3 entries)
│   ├── PowerupController
│   ├── ScoringRuntime (modo "tutorial": no escribe leaderboard)
│   └── HealingDirector (apagado)
├── --- UI ---
│   ├── HUD (UIDocument GameSceneUiController)
│   └── TutorialOverlay (UIDocument nuevo, descrito abajo)
└── --- VFX prewarm ---
    └── PoolPrewarmer (instancia warm de DamagePopup, OverloadPulseEffect, etc.)
```

### 4.1 `TutorialOverlay` (UI Toolkit)

Un `UIDocument` separado, encima del HUD normal, con:

- **Top banner** — título del paso actual.
- **Center prompt** — instrucción principal grande.
- **Key highlight** — slots para 1–2 sprites de teclas/botones de mouse, animados (pulse).
- **Progress chip** — `2/3 in orbit`, `Combo x4`, etc.
- **Step complete toast** — banner verde de 1.2 s al cumplir.
- **Skip button** — esquina superior derecha, `"Skip Tutorial"`.

USS y UXML viven en `MetalPanic/Assets/Combat/UI/Tutorial/`.

### 4.2 Assets de datos nuevos

- `Assets/Combat/Data/Missions/Mission_Tutorial_ComboHunter.asset` — variante de `ComboHunter` con `targetCount=2`, `durationSeconds=30`, `tier=1`, `grantsPowerup=true`, weights cargados hacia `SlowTime`.
- `Assets/Combat/Data/Missions/MissionCatalog_Tutorial.asset` — contiene solo la misión anterior.
- `Assets/Combat/Data/Upgrades/UpgradeCatalog_Tutorial.asset` — 3 entries: MagneticReach, ScrapCannon, DeepPockets.
- `Assets/Combat/Data/Tutorial/TutorialStep_*.asset` × 14 — uno por paso.
- `Assets/Combat/Data/Tutorial/TutorialSequence.asset` — lista ordenada de los 14 pasos.

---

## 5. Arquitectura de código

### 5.1 Componentes nuevos

```
Assets/Combat/Scripts/Tutorial/
├── TutorialBootstrap.cs          # entry point — análogo a RunBootstrap
├── TutorialDirector.cs           # avanza pasos, escucha eventos
├── TutorialStepDefinition.cs     # ScriptableObject (datos por paso)
├── TutorialSequence.cs           # ScriptableObject (lista ordenada)
├── TutorialOverlayController.cs  # bind del UIDocument
├── TutorialSpawnPoint.cs         # MonoBehaviour para anchors en la arena
└── Triggers/
    ├── ITutorialStepTrigger.cs   # interfaz: bool IsSatisfied(ctx)
    ├── MoveDistanceTrigger.cs
    ├── AimRotationTrigger.cs
    ├── OrbitCountTrigger.cs
    ├── KillByRepelTrigger.cs
    ├── StrikeHitTrigger.cs
    ├── MagnetizedStateTrigger.cs
    ├── CounterPerformedTrigger.cs
    ├── OverloadPulseTrigger.cs
    ├── MissionCompletedTrigger.cs
    ├── UpgradeSelectedTrigger.cs
    ├── PowerupSurvivedTrigger.cs
    └── ComboReachedTrigger.cs
```

### 5.2 `TutorialStepDefinition` (ScriptableObject)

Campos serializados:

- `id` (enum `TutorialStepId`)
- `displayName` (string)
- `instruction` (TextArea)
- `keyHighlights` (array de sprites)
- `triggers` (lista polimórfica de `ITutorialStepTrigger` — usar `[SerializeReference]`)
- `triggerLogic` (`AnyOf` / `AllOf`)
- `setupActions` (lista polimórfica — spawn dummy, activar misión, otorgar powerup, etc.)
- `teardownActions`
- `failSoftAfterSeconds` (float, opcional)
- `nextStepOverride` (TutorialStepId, opcional)

Esto permite definir todo el guion del tutorial **en datos**, sin recompilar.

### 5.3 `TutorialDirector` — flujo

```csharp
// pseudo-código
void StartSequence() {
  currentIndex = 0;
  ApplyStep(sequence.steps[0]);
}

void ApplyStep(TutorialStepDefinition step) {
  overlay.Show(step);
  foreach (var action in step.setupActions) action.Execute(ctx);
  foreach (var trigger in step.triggers) trigger.Arm(ctx, OnTriggerSatisfied);
}

void OnTriggerSatisfied(ITutorialStepTrigger t) {
  if (StepLogicMet()) AdvanceStep();
}

void AdvanceStep() {
  foreach (var action in current.teardownActions) action.Execute(ctx);
  overlay.PlayStepComplete();
  currentIndex++;
  if (currentIndex >= sequence.Count) FinishTutorial();
  else ApplyStep(sequence[currentIndex]);
}
```

### 5.4 Acoplamiento con sistemas existentes

Sin cambios destructivos. El director **suscribe** a los `UnityEvent` ya públicos:

| Sistema                 | Evento usado                                          | Trigger que lo consume         |
|-------------------------|-------------------------------------------------------|--------------------------------|
| `ArkhamPlayerMotor`     | derivar posición cada frame                            | `MoveDistanceTrigger`           |
| `MagnetismController`   | `OnObjectOrbited` / `OnEnemyOrbited` / `OnChargeChanged` | `OrbitCountTrigger`, `OverloadPulseTrigger` |
| `ArkhamCombatController`| `OnStrikeHit`, `OnCounterPerformed` (existen)         | `StrikeHitTrigger`, `CounterPerformedTrigger` |
| `ArkhamEnemy`           | `OnMagnetizedStateChanged`                            | `MagnetizedStateTrigger`        |
| `KillEventRouter`       | `OnKillEvent(KillContext ctx)` con `ctx.method`       | `KillByRepelTrigger`            |
| `MissionSystem`         | `OnMissionCompleted(MissionDefinition)`               | `MissionCompletedTrigger`       |
| `UpgradeSystem`         | `OnUpgradePicked(UpgradeId)`                          | `UpgradeSelectedTrigger`        |
| `PowerupController`     | `OnPowerupStarted/Ended`                              | `PowerupSurvivedTrigger`        |
| `ScoringRuntime`        | `OnComboChanged(int)`                                 | `ComboReachedTrigger`           |

Eventos que **aún no existen** y hay que agregar (mínimos, no rompen API):

- `ArkhamCombatController.OnStrikeHit(ArkhamEnemy)` — si no está, exponer.
- `ArkhamEnemy.OnMagnetizedStateChanged(MagnetState)` — si no está.
- `UpgradeSystem.ForceLevelUp(UpgradeCatalog overrideCatalog)` — overload temporal del tutorial.
- `PowerupController.Grant(PowerupId)` — concesión directa (probablemente ya existe vía mission reward, verificar).

### 5.5 Pausas y damage-off

`TutorialDirector` mantiene dos flags globales:

- `tutorialInvincible` → `CombatHealth.AcceptDamage` retorna 0 si está activo (gateable por `bool` público).
- `tutorialTimeFreeze` → durante upgrade pick y mission card, `Time.timeScale = 0f`.

Implementación recomendada: una bandera estática `TutorialMode.IsActive` en un namespace utilitario, leída por `CombatHealth` y `EnemyAggression`.

---

## 6. Reglas pedagógicas que el equipo debe respetar

1. **Un input nuevo por paso.** Si un paso requiere combinar dos teclas nuevas, partirlo.
2. **Highlight visual fuerte.** Cada input nuevo se enseña con sprite + pulse animation, no solo con texto.
3. **Sin fail-states duros.** En tutorial nunca se pierde HP. Solo prompts más explícitos al fallar.
4. **Reintentos infinitos** dentro de un paso. Spawn de targets se renueva si fueron destruidos sin éxito.
5. **El paso termina con feedback positivo.** Slow-mo, popup, sonido — el jugador siempre siente que "lo logró".
6. **Texto corto, ≤ 8 palabras** en el prompt principal. La explicación larga vive en tooltips secundarios.
7. **Skip siempre visible.** Sin penalización. Marca `HasCompletedTutorial` igual si se completa explícitamente o si la run real supera 60 s.
8. **Idioma:** UI en inglés (consistencia con build). Diseño en español.

---

## 7. Orden de implementación recomendado

Pensado para encajar dentro del Día 10 del roadmap (GDD §18 — "Tutorial, menú, build, deploy"). Cuatro tareas, ~6–8 horas reales con assets ya existentes.

### Tarea 1 — Scaffolding (1.5 h)

- Crear escena `TutorialScene.unity` duplicando `GameScene.unity` y limpiando spawners de `WaveDirector`.
- Crear carpetas `Assets/Combat/Scripts/Tutorial/` y `Assets/Combat/UI/Tutorial/`.
- Implementar `TutorialBootstrap.cs` reusando lo que sirve de `RunBootstrap` (player + arena + cámara + EnemyManager).
- Implementar `TutorialStepDefinition`, `TutorialSequence`, `TutorialDirector` (versión mínima que solo muestra prompt y completa con un trigger placeholder `KeyPressTrigger`).
- Verificar que la escena corre y avanza pasos vacíos.

### Tarea 2 — Triggers y acciones (2 h)

- Implementar los 12 triggers listados en §5.1 contra eventos existentes.
- Implementar `SpawnAction`, `GrantPowerupAction`, `StartMissionAction`, `ForceLevelUpAction`, `EnableInputAction`, `DespawnAction` para `setupActions` / `teardownActions`.
- Agregar los eventos faltantes listados en §5.4.
- Smoke-test cada trigger en una escena de prueba.

### Tarea 3 — Contenido (2 h)

- Crear los 14 `TutorialStep_*.asset` con prompts y triggers.
- Crear `TutorialSequence.asset` con el orden.
- Crear `MissionCatalog_Tutorial.asset` y `UpgradeCatalog_Tutorial.asset`.
- Colocar `TutorialSpawnPoint`s en la arena reducida.
- Pase de balance: cantidades, distancias, timeouts.

### Tarea 4 — Overlay UI + polish (1.5–2 h)

- Maquetar `TutorialOverlay.uxml` / `.uss`.
- Implementar `TutorialOverlayController.cs` con animaciones de pulse y toast.
- Hookear `MainMenuController` para que el primer arranque cargue `TutorialScene`; agregar entry `"How to Play"`.
- Persistencia con `PlayerPrefs("HasCompletedTutorial")`.
- Pase de SFX: complete-tick, mission-pop, upgrade-pop, counter-stinger.
- QA exhaustivo en build WebGL (no en editor — el comportamiento de input puede diferir).

### Riesgos a vigilar

- **Input bridging** — `GameInputProvider` está atado a `PlayerInput` con el camera reference; al instanciar todo desde `TutorialBootstrap` hay que reconfigurarlo igual que en `RunBootstrap.ConfigurePlayerForRun`.
- **Pooling** — la primera vez que dispara un VFX el `Pool` hace allocations. Prewarm en `Awake` antes del primer paso.
- **WebGL audio** — algunos SFX simultáneos pueden saturar el mixer; testear el paso 9 (Overload) y paso 13 (multi-kill) en build, no en editor.
- **Skip mid-step** — si el jugador skipea durante un step que pausó el tiempo (paso 11), restaurar `Time.timeScale = 1f` explícitamente antes de cambiar escena.

---

## 8. Checklist de "listo para implementar"

- [ ] `TutorialScene.unity` creada y abre sin errores.
- [ ] `TutorialBootstrap` instancia player + arena + EnemyManager + sistemas de combate.
- [ ] `TutorialDirector` carga `TutorialSequence` y avanza al menos 1 paso con trigger real.
- [ ] Los 12 triggers compilan y se suscriben sin warnings.
- [ ] Los 14 `TutorialStepDefinition` assets existen con prompts y configuración.
- [ ] `MissionCatalog_Tutorial` y `UpgradeCatalog_Tutorial` existen y están enchufados al director.
- [ ] `TutorialOverlay` renderiza encima del HUD sin tapar el cono de mira.
- [ ] `PlayerPrefs` marca el tutorial como completado.
- [ ] Skip funciona desde cualquier paso y restaura `Time.timeScale`.
- [ ] Build WebGL probado de punta a punta < 2 min.
- [ ] Ninguna referencia a sistemas de gameplay vive en el código del overlay (UI no posee estado de juego — CLAUDE.md).

---

## 9. Referencias cruzadas

- GDD principal: `design/gdd-gamejam.md` §3.2 (verbos), §6 (mecánicas), §10 (misiones), §11 (upgrades), §12 (powerups), §14 (UI mínima), §15 (feedback), §19 (criterios de éxito).
- Sistemas: `design/gdd/combat-system.md`, `design/gdd/magnetism-system.md`, `design/gdd/mission-system.md`, `design/gdd/upgrade-system.md`, `design/gdd/powerup-system.md`, `design/gdd/scoring-xp-system.md`, `design/gdd/hud-system.md`.
- Código clave: `MetalPanic/Assets/Combat/Scripts/WaveDirector/RunBootstrap.cs`, `Combat/Scripts/Missions/MissionSystem.cs`, `Combat/Scripts/Upgrades/UpgradeSystem.cs`, `Combat/Scripts/Powerups/PowerupController.cs`, `Combat/Scripts/Magnetism/MagnetismController.cs`, `Combat/Scripts/ArkhamCombatController.cs`, `Combat/Scripts/Scoring/ScoringRuntime.cs`.
