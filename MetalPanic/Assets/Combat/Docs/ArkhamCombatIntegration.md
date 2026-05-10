# Arkham Combat Integration

## Quick Start

In Unity, run:

`Tools > Magnet Panic > Arkham Combat > Create Demo In Current Scene`

That creates:

- a primitive player using `ArkhamPlayerMotor`, `ArkhamCombatController`, `ArkhamTargetScanner`, `MagnetismController`, and `PlayerInput`;
- an `ArkhamEnemyManager`;
- five primitive `ArkhamEnemy` combat targets;
- prototype magnetic scrap objects for Pull/Orbit/Repel;
- a simple follow camera;
- prototype animator controllers and URP-safe materials.

Controls use the existing `InputSystem_Actions` asset:

- WASD: move
- Hold and release left mouse / Pull action: pull, orbit, then repel
- Right mouse / Attack action: strike
- E / Interact action: alternate Pull/Repel fallback for quick testing
- Space / Jump action: counter pulse

The scripts listen for `OnPull`, `OnPullRelease`, `OnStrike`, `OnAttack`, `OnCounter`, and `OnDodge`, so renaming actions later should be painless.

## Implemented Combat Loop

- Strike applies one magnetic mark through `IMarkable.ApplyMark(1)`.
- Two Strike marks set an enemy to `MagneticMarkState.Magnetized`.
- Counter magnetizes the attacker immediately and pushes it away as a defensive pulse.
- Pull attracts `MagneticObject` props and enemies whose state is `Magnetized`.
- Orbit keeps pulled payloads in deterministic slots around the player and applies the GDD movement penalty up to -20% at full charge.
- Repel launches orbiting objects and magnetized enemies in a cone toward mouse aim.
- Repelled scrap damages enemies by object type: light scrap, piercing plates, mines with AOE, and heavy scrap.
- Repelled magnetized enemies become short-lived body projectiles and damage enemies they touch.

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

## Enemy Prefab Checklist

- Root GameObject: `CharacterController`.
- Root GameObject: `ArkhamEnemy`.
- Child or root: visual model with an `Animator`.
- Child: `Counter Cue` visual above the head, assigned to `ArkhamEnemy`.
- Parent enemies under one `ArkhamEnemyManager`, or call `Register` at spawn.
- Tune `Magnetic Mass`: small enemies around 2, normal enemies 3, brute enemies 5+.
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
