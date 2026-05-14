# Arkham Combat Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Architecture:** Runtime scripts live under `Assets/MagnetPanic/Combat/Scripts` and are self-contained. The system exposes magnetic mark/magnetized enemy state so future Pull/Repel systems can build on Strike and Counter. An editor installer creates animator controllers, primitive URP-safe demo objects, and scene wiring.

**Tech Stack:** Unity 6000.3, C#, CharacterController, Input System message callbacks, UnityEditor setup utilities, optional imported Mix and Jam animation assets.

---

### Task 1: Runtime Combat Core

**Files:**
- Create: `MetalPanic/Assets/MagnetPanic/Combat/Scripts/ArkhamPlayerMotor.cs`
- Create: `MetalPanic/Assets/MagnetPanic/Combat/Scripts/ArkhamCombatController.cs`
- Create: `MetalPanic/Assets/MagnetPanic/Combat/Scripts/ArkhamEnemy.cs`
- Create: `MetalPanic/Assets/MagnetPanic/Combat/Scripts/ArkhamEnemyManager.cs`
- Create: `MetalPanic/Assets/MagnetPanic/Combat/Scripts/ArkhamTargetScanner.cs`
- Create: `MetalPanic/Assets/MagnetPanic/Combat/Scripts/ArkhamSimpleCameraFollow.cs`

- [x] Write self-contained runtime scripts with no HDRP or DOTween dependency.
- [x] Support movement, lock-on attacks, target lunges, enemy hit/death, enemy attack turns, counter windows, player damage, and magnetic mark stacks.
- [x] Keep public inspector fields conservative so prefabs can swap meshes, animators, VFX, and characters later.

### Task 2: Animation and Demo Setup

**Files:**
- Create: `MetalPanic/Assets/MagnetPanic/Combat/Editor/ArkhamCombatSetup.cs`

