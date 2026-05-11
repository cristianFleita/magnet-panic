# Click Magnetism Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert magnetism from hold/release into click-to-pull, click-to-repel with clear world-space aiming and magnetized enemy cues.

**Architecture:** `MagnetismController` owns the toggle state, scrap orbit, repulsion preview, and pulled enemy list. `ArkhamEnemy` owns its magnetized cue and magnetic control movement. GDD docs record the UI policy: UI Toolkit for 2D HUD/menus, classic Unity world-space UI/VFX for spatial gameplay cues.

**Tech Stack:** Unity 6000.3, C# 9, New Input System `PlayerInput.SendMessages`, URP-safe runtime primitives/materials.

---

### Task 1: Toggle Pull/Repel Input

**Files:**
- Modify: `MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs`

- [ ] Replace hold/release handling so `OnPull(InputValue)` only acts on press.
- [ ] First press starts Pull; second press calls Repel.
- [ ] Keep `ReleasePull()` public for forced release/debug, but do not call it from mouse release.

### Task 2: World-Space Aim Indicator

**Files:**
- Modify: `MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs`

- [ ] Add a `LineRenderer` and simple runtime tip object when no indicator is assigned.
- [ ] Show it while Pull is active or payload exists.
- [ ] Point it from the player toward mouse aim using the existing `AimDirection()`.

### Task 3: Magnetized Enemy Cue

**Files:**
- Modify: `MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs`

- [ ] Add an optional `magnetizedIndicator` field.
- [ ] Auto-create a small world-space halo when none is assigned.
- [ ] Show it only while `MarkState == MagneticMarkState.Magnetized`.

### Task 4: Pulled Enemies Instead Of Orbiting Enemies

**Files:**
- Modify: `MetalPanic/Assets/Combat/Scripts/Magnetism/MagnetismController.cs`
- Modify: `MetalPanic/Assets/Combat/Scripts/ArkhamEnemy.cs`

- [ ] Replace enemy orbit storage with a pulled enemy list.
- [ ] Pull magnetized enemies toward a hold point in front of the player/aim direction.
- [ ] Repel pulled enemies on the second click without adding them to orbit capacity.

### Task 5: Documentation And Compile Verification

**Files:**
- Modify: `design/gdd-gamejam.md`
- Modify: `design/gdd/magnetism-system.md`
- Modify: `MetalPanic/Assets/Combat/Docs/ArkhamCombatIntegration.md`

- [ ] Document click-to-pull/click-to-repel.
- [ ] Document UI Toolkit for 2D UI and Unity world-space UI/VFX for spatial cues.
- [ ] Run runtime and editor C# compile checks.
