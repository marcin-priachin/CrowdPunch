# Squid Baby Trail presentation

2026-10-25. Requirements: TRAIL-001/002/006, VISION-005/006, INFO-004.

- `EnemyTrail.prefab` now uses `Assets/Squid Baby Cute Series/Prefabs/Squid Baby White.prefab`.
- Retains authored proportions and the user's current uniform Squid model scale of 3.
  The collider and gameplay tuning are unchanged.
- Owned `CrowdPunch/Cel/Enemy` material retains the white pack texture. No atlas
  substitution, body tint, flash, shake, squash, launch pitch or recovery flip is applied.
- Slots 0/1/2: Idle, Move Forward In Place, Move Forward Fast In Place. Slots 5/6/9:
  Take Damage for hits, recovery and launches. Slot 7: Spawn. Slot 10: Die.
  Other slots duplicate these clips to retain the shared 11-motion sample format.
- TrailEnemySpawnSettings has Spawn From Ground enabled. The baked Spawn duration is
  0.8333334 seconds; the shared spawn/recovery clock drives it and launching interrupts it.
- Sampled matrices use the existing ECS skinning path, with quaternion interpolation
  preserving authored bone scale. `EnemyTrail.prefab > Squid Baby > Squid Baby` has
  `Enemy Animation Authoring > Ground Clearance`, saved as 0.1 metres. After blending,
  exact weighted mesh vertices determine a world-up translation that anchors grounded
  poses (including Spawn) at the arena floor plus the setting. Launched bodies retain
  flight height with that minimum clearance. Changing model scale or
  clearance needs no sample rebuild. Matching culling bounds shift by the same amount.
- Spawn still uses the requested authored clip and shared clock; its underground
  portion is now lifted to satisfy the later minimum-clearance request.
- Rebuild through `Crowd Punch > Enemies > Rebuild Trail Prefab`; sample-only rebuild
  preserves the existing prefab transforms. Original pack models, clips and importers
  are unchanged. Existing Trail ground-section colors, widths, lifetimes and damage remain.

## Verification

- Unity recompilation passed with no C# errors or warnings.
- Four new Trail animation tests passed: movement/hit/launch/recovery/defeat and physics
  ownership; spawn interruption/pooling; neutral body feedback; source scales, texture,
  sample hashes, bounds and floor clearance.
- EnemyGroundSpawnTests: 13/13 passed. TrailInteractionTests: 16/16 passed.
- EnemyAnimationTests: 50/51 passed, including all four new Trail checks. The sole
  failure is the unrelated Dasher prefab-scale check: expected 1, actual 1.3.
  No Dasher asset was changed.
- Live isolated Editor preview of Campaign 33 advanced normally. The baked Trail had
  eight finite skin matrices, owner scale 1, SpawnMotion 7, UseAnimation 1 and an
  animation-driven renderer with neutral `(1,1,1,1)` color. Active movement emitted
  ground sections. Restart observation caught the actual ground Spawn in Recovering
  with 0.8166667 seconds remaining of 0.8333334 seconds. Inspected a close camera render
  near the end of Spawn for the supplied silhouette, face texture and cel shading.
- Stopped Play mode and restored the original editor scene and play-start/background
  settings. Campaign progress used the existing isolated Editor preview path.
- `dotnet build Assembly-CSharp.csproj --no-restore` was blocked by access-denied errors
  in generated Temp/Bin and obj output paths. Unity compilation is the checked source.
- No target-device performance run or campaign balance playtest was performed.

## Minimum-clearance follow-up

- Unity compilation passed. Four grounding checks passed, including configurable
  0/0.1/0.35-metre clearances, scale 1/3, nonuniform scale, tilted roots, weighted
  vertices, unchanged physics/bone axes, airborne height and culling-bound drift.
- Checked all 11 clips at 32 frames and two intermediate quaternion-blended poses
  per interval: 1,056 poses at the current prefab scale, including Spawn and loop
  seams. Every lowest vertex cleared the configured floor, with a minimum at 0.1 m.
- Grounded poses are also lowered when needed to remove pre-existing sample hover;
  the minimum-clearance clamp alone is used while Launched. Sample-only rebuilds
  explicitly restore the user's model-root position, rotation and scale after FBX
  root curves have been sampled.
- Existing EnemyAnimationTests: 50/51 passed; the sole failure remains the unrelated
  Dasher 1.3-versus-1 scale expectation.
- Final baked Campaign 33 check: Active Squid at model scale 3 measured
  0.100000143 metres above the arena floor, with clearance 0.1 and one correctly
  linked grounding renderer. The player loop advanced during the check.
- Synthetic Editor stress measurement: 100 Trail skins, 838 vertices each, ten warm-up
  updates and 100 measured updates averaged 2.51 ms per grounding update, including
  scheduling, completion and reflection overhead. This is a CPU sample, not device FPS.
