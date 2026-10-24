# Goblin Chicken boss presentation

Verified 2026-10-24 in the Agent A Unity 6000.3.10f1 Editor.
Requirements: CHICKEN-001/002/006/007, INFO-004, VISION-006.

The later proximity behavior change and its 43-check/live verification are recorded in
[Chicken Cast Spell and teleport](ChickenCastTeleport.md).

- Saved Campaign 20, Campaign 60 and Gauntlet 20 use the exact supplied
  `Prefabs/Enemies/Goblin Giant Wizard Variant.prefab`, as corrected by the user.
  The source prefab and user-modified Giant materials were preserved.
- Body, both shoulders, hat and wand use Giant-pack Idle, Run Forward In Place, Sweep Attack,
  Take Damage and Die, retargeted through the prefab's Humanoid avatar.
- Source root and descendant scales match the saved models. Alignment uses translation
  only; animation performs no procedural squash, recoil, shaking or phase tint.
- Eight directional jogs from the existing `Animation/EnemyMovement.controller` are
  sampled below the pelvis; upper-body Sweep and equipment retain their own pose.
- Dedicated textured `CrowdPunch/Cel/Enemy` materials match the established Full Cel style.
- Each of the three boss settings assets serializes `projectileLaunchFrame: 18` and
  `firingAnimationLastFrame: 30`. Frames are zero-based. `windUp: 0.8` now represents
  the full Sweep duration, so release is at 0.48 seconds, followed by Sweep recovery
  during the existing immediate rush. Paired spacing remains at least 1.5 seconds.

## Checks

- Unity compilation passed without errors.
- The earlier .NET build passed with 498 package/reference/deprecation warnings. The latest
  `dotnet build Assembly-CSharp.csproj --no-restore -v quiet` and focused
  `-p:BuildProjectReferences=false` attempt both returned `Build FAILED` with zero errors,
  zero warnings and no diagnostic. Unity compilation, the 33 tests and live runtime checks
  succeeded for the final Giant replacement; the standalone .NET build remains unresolved.
- All 33 Chicken-filtered Editor checks passed with Burst enabled. Coverage includes
  actual release at source frames 0, 15 and 30 without duplicate shots; paired spacing;
  interrupt/defeat behavior; projectile ownership, reflection and return; scene geometry;
  body/equipment sample hashes; scales; leg-only masking; neutral color; rotation blending including mirrored shoulder scale;
  and preservation of physics transforms and velocity.
- The earlier broader `BossAppearanceTests` run passed 11/13. Its Chicken checks passed.
  Remaining failures concern Gatekeeper floor clearance (level 10, minimum -3.61194491
  versus required -1.92499995) and a Dino sample dependency hash mismatch (level 40).
  Neither model nor its samples were changed in this task.
- Live Giant Campaign 20 advanced 824 frames during a 14-second controlled review. It had five
  animated skins, changing poses, observed wind-up/rush, jog movement, upper-body Sweep
  follow-through and neutral boss colors. The shot sequence reached four during observation.
- Live transition to Campaign 60 loaded one Giant boss with five skins; at frame 3058 its
  state was WindUp with shot sequence four.
- Rendered review images: [wind-up](Images/GoblinChicken/WindUp.png) and
  [rush with projectile](Images/GoblinChicken/Rush.png). These use a temporary close-up camera.
- Source/test/document whitespace checks passed. Unity-generated YAML contains its usual
  blank-value trailing spaces. The Editor was stopped and its original `New Scene 1` restored.

## Tooling and limits

The Unity CLI update check could not reach its CDN. The initial model migration exceeded the
eval wrapper's five-second default but completed; a later call with an explicit 60-second
Pipeline timeout completed normally. Native Burst code initially retained old component layouts;
the managed run passed 32/32, and restarting the assigned Editor resolved the native mismatch.
The first restricted restart failed during Package Manager/licensing startup; reopening outside
the restricted launcher succeeded. The final corrected Giant model passed 33/33 with Burst on.

Giant accessories use their own generated mesh identities, avoiding stale GPU deformation data
when replacing the previous generated meshes. Mirrored left-shoulder scale is retained through
rotation/scale interpolation. Obsolete unreferenced generated meshes/material were removed.

This was an automated and controlled Editor review, not a full human combat playthrough or
target-device performance measurement. Authored prefab size is intentionally independent of the
unchanged boss collider. No crowd simulation or spawning algorithm was added or expanded.
