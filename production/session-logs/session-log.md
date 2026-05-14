## Session End: 20260510_142506
### Commits
b7c551d setting project
499ce21 init
### Uncommitted Changes
MetalPanic/Assets/Editor/WebGLBuildScript.cs
MetalPanic/Assets/Scenes/SampleScene.unity
MetalPanic/Assets/Scenes/SampleScene.unity.meta
MetalPanic/ProjectSettings/ProjectSettings.asset
design/magnet_panic_scrapstorm_gdd_gamejam.md
---

## Archived Session State: 20260511_155208
# Active Session State

> **Última actualización:** 2026-05-10

## Tarea actual

Diseñando `combat-system` GDD (sistema #11 del index, Day 3).
**Anterior completo:** `magnetism-system` (Day 1, núcleo).

## Estado

- `design/gdd/game-concept.md` ✅
- `design/gdd/systems-index.md` ✅ (1/21 designed)
- `design/gdd/magnetism-system.md` ✅ — 11 secciones (8 requeridas + 3
  opcionales), 844 líneas. Define contratos `IAttractable` e `IMarkable`,
  el recurso `currentCharge`, y los eventos consumidos por 10 sistemas
  downstream.

## Cambios al index aplicados

- `magnetism-system` status: Not Started → Designed
- Progress: Not Started 21 → 20, Designed 0 → 1
- Dependencia upstream agregada: `damage-health-system` (surgió durante
  el diseño como Hard dep para resolver impactos)

## Open Questions registradas

7 preguntas abiertas en `magnetism-system.md` § Open Questions, con
owner sugerido y target de resolución (Q1-Q7). Las críticas para Día 1
son Q2 (intangibilidad orbital) y Q4 (cue visual de succión).

## Próximo

Opciones (en orden de impacto):
1. `/design-review design/gdd/magnetism-system.md` — validar el GDD antes
   de codear.
2. `/prototype magnetism` — código throwaway para validar el feel del
   Pull/Repel (apunta al Criterio #2 de éxito).
3. `/design-system damage-health-system` — siguiente bottleneck. Now
   listed as upstream dep de magnetism.
4. `/design-system combat-system` — escribe a `IMarkable`, completa el
   loop de combate junto con el GDD ya hecho.
---

## Session End: 20260511_155208
### Commits
d17a8eb chore: fix collider on character
972c30a chore: movement + camera
cae265b chore: wip pool objects system
08e2a5c chore: wip arena system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
---

## Session End: 20260511_161629
### Commits
d17a8eb chore: fix collider on character
972c30a chore: movement + camera
cae265b chore: wip pool objects system
08e2a5c chore: wip arena system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Magnetism/MagneticObject.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
MetalPanic/Assets/Prefabs/MainCharacter.prefab
MetalPanic/Assets/Scenes/GameScene.unity
production/session-logs/session-log.md
production/session-state/active.md
---

## Session End: 20260511_164550
### Commits
00d771f chore: overload system wip
d17a8eb chore: fix collider on character
972c30a chore: movement + camera
cae265b chore: wip pool objects system
08e2a5c chore: wip arena system
### Uncommitted Changes
MetalPanic/Assets/Combat/Editor/ArkhamCombatSetup.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagneticObject.cs
---

## Session End: 20260511_190250
### Commits
00d771f chore: overload system wip
d17a8eb chore: fix collider on character
972c30a chore: movement + camera
cae265b chore: wip pool objects system
08e2a5c chore: wip arena system
### Uncommitted Changes
MetalPanic/Assets/Combat/Editor/ArkhamCombatSetup.cs
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagneticObject.cs
MetalPanic/Assets/Prefabs/MainCharacter.prefab
MetalPanic/Assets/Scenes/GameScene.unity
production/session-logs/session-log.md
---

## Session End: 20260511_200900
### Commits
48e3f1d chore: enemies and attractable object prefabs wip
00d771f chore: overload system wip
d17a8eb chore: fix collider on character
972c30a chore: movement + camera
cae265b chore: wip pool objects system
08e2a5c chore: wip arena system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Magnetism/MagneticObject.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
MetalPanic/Assets/Editor/WebGLBuildScript.cs
MetalPanic/Assets/Settings/Mobile_RPAsset.asset
MetalPanic/Assets/Settings/PC_RPAsset.asset
MetalPanic/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset
MetalPanic/ProjectSettings/EditorBuildSettings.asset
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260511_201725
### Commits
ef59914 fix: attractable heavy object clamp
48e3f1d chore: enemies and attractable object prefabs wip
00d771f chore: overload system wip
d17a8eb chore: fix collider on character
972c30a chore: movement + camera
cae265b chore: wip pool objects system
08e2a5c chore: wip arena system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_051046
### Commits
77b200c chore: adjust animations and input from the player
a927fe5 chore: adjust animations WIP
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_052146
### Commits
e471b9c fix: overloard scrap limit
77b200c chore: adjust animations and input from the player
a927fe5 chore: adjust animations WIP
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Magnetism/MagneticObject.cs
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_052718
### Commits
0666dd6 fix: magnetic objects
e471b9c fix: overloard scrap limit
77b200c chore: adjust animations and input from the player
### Uncommitted Changes
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_053454
### Commits
0666dd6 fix: magnetic objects
e471b9c fix: overloard scrap limit
77b200c chore: adjust animations and input from the player
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Health/CombatHealth.cs
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_054735
### Commits
3e8b904 chore: damage popup
0666dd6 fix: magnetic objects
e471b9c fix: overloard scrap limit
77b200c chore: adjust animations and input from the player
### Uncommitted Changes
design/gdd/systems-index.md
design/gdd/wave-director.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_055144
### Commits
3e8b904 chore: damage popup
0666dd6 fix: magnetic objects
e471b9c fix: overloard scrap limit
77b200c chore: adjust animations and input from the player
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Health/CombatHealth.cs
MetalPanic/Assets/Combat/Scripts/UI/DamageNumberSpawner.cs
MetalPanic/Assets/Combat/Scripts/UI/DamagePopup.cs
design/gdd/systems-index.md
design/gdd/wave-director.md
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_092559
### Commits
973dccd chore: health popup
3e8b904 chore: damage popup
0666dd6 fix: magnetic objects
e471b9c fix: overloard scrap limit
77b200c chore: adjust animations and input from the player
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Arena/ArenaSystem.cs
MetalPanic/Assets/Combat/Tests/EditMode/ArenaSystemTests.cs
design/gdd/arena-system.md
design/gdd/systems-index.md
design/gdd/wave-director.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_093733
### Commits
897ab57 chore: adjust arena system
973dccd chore: health popup
3e8b904 chore: damage popup
0666dd6 fix: magnetic objects
e471b9c fix: overloard scrap limit
77b200c chore: adjust animations and input from the player
### Uncommitted Changes
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_142526
### Commits
897ab57 chore: adjust arena system
### Uncommitted Changes
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_203102
### Commits
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Generated/EnemyDefinitions/RunnerBot.asset
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_205705
### Commits
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Generated/EnemyDefinitions/RunnerBot.asset
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_211454
### Commits
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Generated/EnemyDefinitions/RunnerBot.asset
MetalPanic/Assets/Combat/Scripts/ArkhamCombatController.cs
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
MetalPanic/Assets/Combat/Scripts/Enemies/EnemyDefinition.cs
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_213814
### Commits
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_214954
### Commits
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
MetalPanic/Assets/Scenes/GameScene.unity
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_215951
### Commits
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
MetalPanic/Assets/Scenes/GameScene.unity
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_224322
### Commits
0e62bcf chore: adjust magnetism repel enemies
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_230156
### Commits
0e62bcf chore: adjust magnetism repel enemies
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_230807
### Commits
0e62bcf chore: adjust magnetism repel enemies
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260512_233016
### Commits
3086da1 chore: adjust magnetic pull with enemies
0e62bcf chore: adjust magnetism repel enemies
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Arena/ArenaSystem.cs
MetalPanic/Assets/Prefabs/Arena.prefab
MetalPanic/Assets/Scenes/GameScene.unity
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260513_010401
### Commits
26cdd56 chore: adjut arene with 3 exits
3086da1 chore: adjust magnetic pull with enemies
0e62bcf chore: adjust magnetism repel enemies
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamCombatController.cs
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260513_013518
### Commits
04980fd chore: adjust counter
26cdd56 chore: adjut arene with 3 exits
3086da1 chore: adjust magnetic pull with enemies
0e62bcf chore: adjust magnetism repel enemies
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260513_014459
### Commits
04980fd chore: adjust counter
26cdd56 chore: adjut arene with 3 exits
3086da1 chore: adjust magnetic pull with enemies
0e62bcf chore: adjust magnetism repel enemies
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamCombatController.cs
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260513_015900
### Commits
04980fd chore: adjust counter
26cdd56 chore: adjut arene with 3 exits
3086da1 chore: adjust magnetic pull with enemies
0e62bcf chore: adjust magnetism repel enemies
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamCombatController.cs
MetalPanic/Assets/Combat/Scripts/ArkhamSimpleCameraFollow.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260513_034240
### Commits
04980fd chore: adjust counter
26cdd56 chore: adjut arene with 3 exits
3086da1 chore: adjust magnetic pull with enemies
0e62bcf chore: adjust magnetism repel enemies
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamCombatController.cs
MetalPanic/Assets/Combat/Scripts/ArkhamSimpleCameraFollow.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs
MetalPanic/Assets/Combat/Scripts/Pooling/Pool.cs
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260513_041001
### Commits
24a5e28 chore: Adjust combat system + camera + target strike indicators
04980fd chore: adjust counter
26cdd56 chore: adjut arene with 3 exits
3086da1 chore: adjust magnetic pull with enemies
0e62bcf chore: adjust magnetism repel enemies
e0a888d chore: adjust runnerBot
a44fc69 chore: adjust enemy attack
e5fc468 chore: wip vertical slice combat system
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260513_194400
### Commits
7f1d294 chore: adjust react app
c373b59 chore: adjust combo
911bcf6 chore: adjust health
3064d32 chore: combat enemies balance
30f3dc9 chore: adjust enemies
b5c3059 chore: new enemies + adjust enemies pressure
b77e03d chore: combo logs
b9e1876 chore: adjust character sticky attack
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamSimpleCameraFollow.cs
MetalPanic/Packages/manifest.json
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260513_201633
### Commits
7f1d294 chore: adjust react app
c373b59 chore: adjust combo
911bcf6 chore: adjust health
3064d32 chore: combat enemies balance
30f3dc9 chore: adjust enemies
b5c3059 chore: new enemies + adjust enemies pressure
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamSimpleCameraFollow.cs
MetalPanic/Assets/Scenes/GameScene.unity
MetalPanic/Packages/manifest.json
MetalPanic/Packages/packages-lock.json
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260513_214538
### Commits
516b477 chore: kenny environment + vfx
f4bebb2 Merge pull request #1 from cristianFleita/feat-combat
16e5cd1 chore: adjust camera
7f1d294 chore: adjust react app
c373b59 chore: adjust combo
911bcf6 chore: adjust health
3064d32 chore: combat enemies balance
30f3dc9 chore: adjust enemies
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Arena/ArenaSystem.cs
MetalPanic/Assets/Scenes/GameScene.unity
MetalPanic/Assets/Scenes/Map.unity
---

## Session End: 20260513_225924
### Commits
516b477 chore: kenny environment + vfx
f4bebb2 Merge pull request #1 from cristianFleita/feat-combat
16e5cd1 chore: adjust camera
7f1d294 chore: adjust react app
c373b59 chore: adjust combo
911bcf6 chore: adjust health
3064d32 chore: combat enemies balance
30f3dc9 chore: adjust enemies
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Arena/ArenaSystem.cs
MetalPanic/Assets/Combat/Scripts/Magnetism/MagneticObject.cs
MetalPanic/Assets/Prefabs/Arena.prefab
MetalPanic/Assets/Prefabs/Attractables/Heavy_Attractable.prefab
MetalPanic/Assets/Prefabs/Attractables/LightScrap_Attractable.prefab
MetalPanic/Assets/Prefabs/Attractables/Mine_Attractable.prefab
MetalPanic/Assets/Prefabs/Attractables/Plate_Attractable.prefab
MetalPanic/Assets/Prefabs/Map/Gate/gate.prefab
MetalPanic/Assets/Scenes/GameScene.unity
MetalPanic/Assets/Scenes/GameScene.unity.meta
MetalPanic/Assets/Scenes/Map.unity.meta
production/session-logs/session-log.md
---

## Session End: 20260513_231730
### Commits
150a886 chore: add projectile trigger ignore logic and update arena system configuration with playable area colliders
516b477 chore: kenny environment + vfx
f4bebb2 Merge pull request #1 from cristianFleita/feat-combat
16e5cd1 chore: adjust camera
7f1d294 chore: adjust react app
c373b59 chore: adjust combo
911bcf6 chore: adjust health
3064d32 chore: combat enemies balance
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/Arena/ArenaSystem.cs
MetalPanic/Assets/Prefabs/Arena.prefab
MetalPanic/Assets/Scenes/GameScene.unity
---

## Session End: 20260513_234058
### Commits
d8cece4 chore: add obstacle in arena
150a886 chore: add projectile trigger ignore logic and update arena system configuration with playable area colliders
516b477 chore: kenny environment + vfx
f4bebb2 Merge pull request #1 from cristianFleita/feat-combat
16e5cd1 chore: adjust camera
7f1d294 chore: adjust react app
c373b59 chore: adjust combo
911bcf6 chore: adjust health
3064d32 chore: combat enemies balance
---

## Session End: 20260514_003143
### Commits
a8d3f2f chore: adjust health system config
d8cece4 chore: add obstacle in arena
150a886 chore: add projectile trigger ignore logic and update arena system configuration with playable area colliders
516b477 chore: kenny environment + vfx
f4bebb2 Merge pull request #1 from cristianFleita/feat-combat
16e5cd1 chore: adjust camera
7f1d294 chore: adjust react app
### Uncommitted Changes
MetalPanic/Assets/Combat/Generated/EnemyDefinitions/HeavyBot.asset
MetalPanic/Assets/Combat/Generated/EnemyDefinitions/MetalEnemy.asset
MetalPanic/Assets/Combat/Generated/EnemyDefinitions/RunnerBot.asset
MetalPanic/Assets/Combat/Generated/EnemyDefinitions/Scrapling.asset
MetalPanic/Assets/Combat/Generated/EnemyDefinitions/SpitterDrone.asset
MetalPanic/Assets/Combat/Scripts/ArkhamPlayerMotor.cs
MetalPanic/Assets/InputSystem_Actions.inputactions
MetalPanic/Assets/Prefabs/MainCharacter.prefab
MetalPanic/Assets/Scenes/SampleScene.unity
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260514_022820
### Commits
1904bd6 chore: adjust speed + optional controllers
a8d3f2f chore: adjust health system config
d8cece4 chore: add obstacle in arena
150a886 chore: add projectile trigger ignore logic and update arena system configuration with playable area colliders
516b477 chore: kenny environment + vfx
f4bebb2 Merge pull request #1 from cristianFleita/feat-combat
16e5cd1 chore: adjust camera
7f1d294 chore: adjust react app
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
MetalPanic/Assets/Prefabs/Map/Gate/gate-door-window.prefab
MetalPanic/Assets/Prefabs/Map/Gate/gate.prefab
MetalPanic/Assets/Prefabs/Map/Wall/structure-window-wide.prefab
MetalPanic/Assets/Prefabs/Map/Wall/template-corner.prefab
MetalPanic/Assets/Prefabs/Map/Wall/template-wall-corner.prefab
MetalPanic/Assets/Prefabs/Map/Wall/template-wall-detail-a.prefab
MetalPanic/Assets/Prefabs/Map/Wall/template-wall.prefab
MetalPanic/Assets/Scenes/GameScene.unity
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

## Session End: 20260514_023507
### Commits
1904bd6 chore: adjust speed + optional controllers
a8d3f2f chore: adjust health system config
d8cece4 chore: add obstacle in arena
150a886 chore: add projectile trigger ignore logic and update arena system configuration with playable area colliders
516b477 chore: kenny environment + vfx
f4bebb2 Merge pull request #1 from cristianFleita/feat-combat
16e5cd1 chore: adjust camera
7f1d294 chore: adjust react app
### Uncommitted Changes
MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs
MetalPanic/Assets/Prefabs/Enemies/Combat/MetalEnemy.prefab
MetalPanic/Assets/Prefabs/Enemies/Combat/Scrapling.prefab
MetalPanic/Assets/Prefabs/Map/Gate/gate-door-window.prefab
MetalPanic/Assets/Prefabs/Map/Gate/gate.prefab
MetalPanic/Assets/Prefabs/Map/Wall/structure-window-wide.prefab
MetalPanic/Assets/Prefabs/Map/Wall/template-corner.prefab
MetalPanic/Assets/Prefabs/Map/Wall/template-wall-corner.prefab
MetalPanic/Assets/Prefabs/Map/Wall/template-wall-detail-a.prefab
MetalPanic/Assets/Prefabs/Map/Wall/template-wall.prefab
MetalPanic/Assets/Scenes/GameScene.unity
production/session-logs/session-log.md
react-app/public/unity-build/Build/unity-build.data.unityweb
react-app/public/unity-build/Build/unity-build.framework.js.unityweb
react-app/public/unity-build/Build/unity-build.loader.js
react-app/public/unity-build/Build/unity-build.wasm.unityweb
---

