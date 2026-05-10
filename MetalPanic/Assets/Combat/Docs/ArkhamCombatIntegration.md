# Arkham Combat Integration

## Quick Start

In Unity, run:

`Tools > Magnet Panic > Arkham Combat > Create Demo In Current Scene`

That creates:

- a primitive player using `ArkhamPlayerMotor`, `ArkhamCombatController`, `ArkhamTargetScanner`, and `PlayerInput`;
- an `ArkhamEnemyManager`;
- five primitive `ArkhamEnemy` combat targets;
- a simple follow camera;
- prototype animator controllers and URP-safe materials.

Controls use the existing `InputSystem_Actions` asset:

- WASD: move
- Left mouse / Attack action: strike
- Space / Jump action: counter

## Character Swap

The primitive visuals are disposable. To use a real character:

1. Keep the `ArkhamPlayerMotor`, `ArkhamCombatController`, `ArkhamTargetScanner`, `CharacterController`, and `PlayerInput` components.
2. Replace the capsule renderer with your character prefab.
3. Assign your character `Animator`.
4. Either keep the generated prototype controller, or assign the imported Mix and Jam controller from `Assets/ThirdParty/BatmanArkhamCombat/Character/Animations`.

Enemies expose magnetic mark state through `ArkhamEnemy.MagneticMarks` and `ArkhamEnemy.IsMagnetized`, so Pull/Repel can hook into this later.
