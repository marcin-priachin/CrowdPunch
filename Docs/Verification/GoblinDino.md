# Armored Goblin Giant Dino (2026-10-25)

## 2026-10-30 - Moving-leg crossing correction

The reported crossing was reproduced with a moving Elite Sweep: composing jog legs below
the action's twisted pelvis reversed ankle ordering (minimum separation -0.1641 model units).
Dino's Block pose narrowed the stance to 0.0732. Human jog retargeting also pulled the wide
Giant's thighs inward. CPJ2 now stores locomotion pelvis poses; moving pelvis orientation and
the complete leg chain follow that reference while action pelvis position/scale and upper-body
matrices stay unchanged. Generated Giant jog samples cap inward thigh adduction at neutral,
retaining forward/back stride, knee motion and bone lengths. Source animations are unchanged.

- Actual prefab/Campaign 40/80 ECS scans each covered 4,096 moving poses across directions,
  stride interpolation and Sweep/Block/Warning poses. Minimum ankle separation is now 0.4065
  model units in all three rigs, with unchanged upper body and zero measured pelvis-origin error.
- All source stance samples and their direction/time blends pass the new wide-stance regression.
- 134 direct Editor checks passed; the two previously documented pillar-supply fixtures still fail.
  Unity compilation and the required C# build pass (318 existing warnings, zero errors).
- Both standing-turn clips, entry/exit, saved bounds and physics-preservation checks still pass.
  A CPU-skinned before/after render was inspected for the reproduced Elite pose.
- The completed enemy-animation update with 2 Elite bodies and 1,000 unrelated skin entities
  measured 0.0536 ms after 60 warmups over 200 updates. This excludes physics/rendering and FPS.
- Only the two shared Giant jog sample assets were regenerated; no scene, prefab, gameplay
  setting, collider or source animation was rewritten for this correction. Existing asset GUIDs remain.

## 2026-10-30 - Shared lower-body standing turns

The Elite prefab and Campaign 40/80 Dino bodies now use the player's exact Left Turn/Right Turn
clips through a separate pelvis-relative leg sample asset. Small yaw is filtered at 10 degrees
and 15 degrees/second. Entry is 0.2 seconds; after a 0.06-second stop delay, exit is 0.08 seconds.
Existing movement-relative jogs, source upper body/equipment, authored scale and combat clocks remain.

- Unity compilation and the required C# build passed, with zero errors and 318 existing warnings.
- Direct Editor execution passed 127 standing-turn, jog, Elite geometry, Dino presentation and
  pillar checks. The same two documented pillar-supply fixtures (counts 1/4) still fail; they run
  without presentation systems and were not changed by this work.
- The saved Elite prefab and both campaign boss roots baked one opted-in body each. Controlled
  ECS playback verified both complete turn cycles, repeats, prompt exit, unchanged upper-body and
  equipment matrices, unchanged owner transform/velocity, and all saved renderer bounds.
- A completed-update microbenchmark with 2 Elite bodies and 1,000 unrelated crowd skin entities,
  after 60 warmups over 200 updates, measured 0.0328 ms per standing-turn update. This isolates
  the new presentation job and completion; it excludes full simulation, rendering and FPS.
- Target scene changes only serialize the new body sample reference/tuning and current default
  authoring fields. Model/scene identities, placement, scale, colliders, waves and legacy Dino are preserved.
- Verification used preview scenes and temporary ECS worlds; full human gameplay review remains useful.

## Original model migration

Implements the requested presentation revision for PILLAR-001/006 and VISION-005/006.
Campaign 40, Campaign 80 and legacy Gauntlet 22 now use
`Assets/CrowdPunch/Prefabs/Enemies/Goblin Giant Armored Variant.prefab`.
All authored transform scales, including the root's 1.5 scale, are preserved.
Translation grounds the exported poses; no collider fitting or runtime squash/stretch remains.

The Giant animation pack supplies Block for chase/burst, Cast Spell for the moving warning,
Take Damage for stagger, and Die for defeat. Warning/stagger playback follows the existing
combat timers. Eight blended directional jogs from the existing CrowdPunch animation
controller replace only leg bones during chase/warning/burst. Physics velocity selects
direction and stride cadence; the default leg playback cap is 2x. The pelvis, upper body,
shield, mace, helmet and shoulders retain the authored action pose. Death holds its final pose.

Dedicated Full Cel materials preserve the source textures and palette. LOD switching is
removed, mesh LOD0 is selected, and accessories are sampled as rigid skinned meshes.
Boss phase color flashes are removed; existing pillar colors remain. Gameplay tuning,
colliders, pillars, waves, arena geometry and boss health UI are unchanged.

## Verification

- Unity compilation passed with no compile errors.
- All 10 Dino presentation tests passed: every saved encounter, complete authored-scale
  comparison, six animated renderers, valid sample hashes, cel materials, all-pose floor
  clearance, phase selection, leg masking, constant palette and unchanged physics.
- The live run exposed a near-zero negative jog angle rounding to sector 8 in Burst.
  Sector wrapping was fixed in the shared sampler; both sides of zero now have regression tests.
- All 11 Chicken presentation regressions passed after that shared change.
- Broader boss appearance tests: 8 passed, 2 failed in unchanged Gatekeeper/Chicken floor
  assertions. Dino's source/floor and finite-transition checks passed.
- Dino pillar gameplay tests: 29 passed, 2 failed in the unchanged
  `Pillar006_SupplyHonorsCountAndReusesLaunchedOrPooledSlots` fixtures (counts 1 and 4).
  Those fixtures exercise supply without presentation systems; they are outside this revision.
- Live Campaign 40: frame advancement confirmed, all six animation entities baked with
  matching skin buffers, one body jog mask, and chase/warning/burst observed (phase mask 7).
  Rendered capture reviewed at `Temp/DinoGoblinValidation/campaign40-rerun.png`.
- Live Campaign 80: level index 79, six animation entities and one body jog mask confirmed.
  No new runtime errors followed the direction-wrap fix in either campaign. An unrelated
  Synty Sidekick editor-window error occurred while entering Play mode.
- Restored the original preview scene and play-mode start/background settings. Unity's
  test-runner save prompt was resolved by saving the user's preview edits.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1` was attempted but spent several
  minutes rebuilding package projects and was stopped. The focused build with
  `-p:BuildProjectReferences=false` failed with 97 missing Unity-generated DLL metadata
  errors and four assembly-conflict warnings. Unity compilation and live baking passed.

No target-device performance run or full human boss-clear playtest was performed.
