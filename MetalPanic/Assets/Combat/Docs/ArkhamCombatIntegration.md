# Arkham Combat Integration

## Quick Start

In Unity, run:

`Tools > Magnet Panic > Arkham Combat > Create Demo In Current Scene`

To add only the pullable metal enemy to an existing scene, run:

`Tools > Magnet Panic > Arkham Combat > Spawn Metal Enemy In Current Scene`

That creates:

- a primitive player using `ArkhamPlayerMotor`, `ArkhamCombatController`, `ArkhamTargetScanner`, `MagnetismController`, and `PlayerInput`;
- an `ArkhamEnemyManager`;
- five primitive `ArkhamEnemy` combat targets;
- one primitive `Metal Enemy` that can be pulled at any time;
- prototype magnetic scrap objects for Pull/Orbit/Repel;
- a simple follow camera;
- prototype animator controllers and URP-safe materials.

Controls use the existing `InputSystem_Actions` asset:

- WASD: move
- Left mouse / Pull action: first click pulls, second click repels
- Right mouse / Attack action: strike
- E / Interact action: alternate Pull/Repel fallback for quick testing
- Space / Jump action: counter pulse

The scripts listen for `OnPull`, legacy `OnPullRelease`, `OnStrike`, `OnAttack`, `OnCounter`, and `OnDodge`, so renaming actions later should be painless.

## Implemented Combat Loop

- Strike applies one magnetic mark through `IMarkable.ApplyMark(1)`.
- Two Strike marks set an enemy to `MagneticMarkState.Magnetized`.
- Counter magnetizes the attacker immediately and pushes it away as a defensive pulse.
- Pull attracts `MagneticObject` props, enemies whose state is `Magnetized`, and enemies with `Always Pullable By Magnet` enabled.
- Orbit keeps pulled payloads in deterministic slots around the player and applies the GDD movement penalty up to -20% at full charge.
- Magnetized enemies are pulled to a held point in front of the player instead of joining the scrap orbit.
- Repel launches orbiting objects and held magnetized enemies in a cone toward mouse aim.
- Repelled scrap damages enemies by object type: light scrap, piercing plates, mines with AOE, and heavy scrap.
- Repelled magnetized enemies become short-lived body projectiles and damage enemies they touch.
- `MagnetismController` can auto-create a world-space aim indicator; `ArkhamEnemy` can auto-create a magnetized cue above the enemy.
- 2D HUD work should use UI Toolkit; spatial combat cues should stay in Unity world-space UI, meshes, particles, or VFX.

## Character Swap

The primitive visuals are disposable. To use a real character:

1. Keep the `ArkhamPlayerMotor`, `ArkhamCombatController`, `ArkhamTargetScanner`, `MagnetismController`, `CharacterController`, and `PlayerInput` components.
2. Replace the capsule renderer with your character prefab.
3. Assign your character `Animator`.
4. Either keep the generated prototype controller, or assign the imported Mix and Jam controller from `Assets/ThirdParty/BatmanArkhamCombat/Character/Animations`.

## Player Prefab Checklist

- Root GameObject: `CharacterController`.
- Root GameObject: `PlayerInput` using `Assets/InputSystem_Actions.inputactions`, action map `Player`, notification `Send Messages`.
- Root GameObject: `ArkhamPlayerMotor`.
- Root GameObject: `ArkhamCombatController`.
- Root GameObject: `ArkhamTargetScanner`.
- Root GameObject: `MagnetismController`.
- Child: visual model with an `Animator`.
- Child: `Hit Point` transform around chest/hand height, assigned to `ArkhamCombatController`.
- Assign the scene camera to `ArkhamPlayerMotor` and `MagnetismController` if `Camera.main` is not reliable.
- Optional: assign custom `Aim Line` / `Aim Tip` visuals on `MagnetismController`, or leave `Auto Create Aim Indicator` enabled for prototypes.

## Enemy Prefab Checklist

- Root GameObject: `CharacterController`.
- Root GameObject: `ArkhamEnemy`.
- Child or root: visual model with an `Animator`.
- Child: `Counter Cue` visual above the head, assigned to `ArkhamEnemy`.
- Optional: assign a custom `Magnetized Indicator`, or leave `Auto Create Magnetized Indicator` enabled for prototypes.
- Parent enemies under one `ArkhamEnemyManager`, or call `Register` at spawn.
- Tune `Magnetic Mass`: small enemies around 2, normal enemies 3, brute enemies 5+.
- For metallic enemies, enable `Always Pullable By Magnet`; they can be pulled even while `Mark State` is `Normal`.
- Keep attack telegraph timings visible: `prepareAttackTime`, `attackHitDelay`, and `attackRecovery`.
- `Destroy On Death` is enabled by default; tune `Death Despawn Delay` if you want the death animation to linger longer.

## Magnetic Prop Checklist

- Root GameObject: visible mesh.
- Collider on the root or child.
- `Rigidbody`, with gravity off for the prototype loop.
- `MagneticObject`.
- Pick `Object Type`: `LightScrap`, `Plate`, `Mine`, or `Heavy`.
- Optional: assign `TrailRenderer`, orbit particles, and impact particles to the feedback fields.
- Repelled props are destroyed after impact or after their projectile lifetime, so spawned scrap should be disposable.
