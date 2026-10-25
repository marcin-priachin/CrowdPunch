# Goblin Trooper Armored verification

Date: 2026-10-24. Requirements: ENEMY-014/015, INFO-001/004/006, VISION-006.

## Implemented

- `ArmoredEnemy.prefab` retains its GUID, physics capsule and mass. Its visual source is
  `Prefabs/Enemies/Goblin Trooper Armored Variant.prefab`; all source transform scales,
  including the mirrored shoulder, are retained. Alignment uses translation only.
- Dedicated textured `GoblinArmored` Full Cel materials are protected from atlas refresh.
  Animation-driven rendering bypasses procedural tint, flash, shake and squash.
- The Trooper Humanoid clips provide Spawn, Block, Take Damage, Jump In Place, Die and
  reverse-Die rise. Giant Idle/Walk/Run supply ordinary upper-body locomotion; eight
  directional jogs supply legs using resolved velocity and measured stride. Block takes
  priority while Shield 01 remains. Spawn/recovery/flight/defeat retain full-body poses.
- Spawn From Ground is enabled. The baked Spawn duration is 0.8333334 seconds.
- First armor reduction hides Helmet 05; second hides Shield 01. Rendering is toggled
  through enableable MaterialMeshInfo, preserving pooled entities and scale. Fresh armor
  restores both pieces. The canvas no longer creates or publishes shield icons.
- Signed-scale pose interpolation preserves authored mirrored equipment. Armor gameplay,
  encounter composition, collision rules and ammunition replacement are unchanged.

## Checks performed

Unity 6000.3.10f1 compiled the final code with zero compile errors or warnings.
`dotnet build Assembly-CSharp.csproj --no-restore` also passed on the final source
with zero errors and 385 existing package/API warnings.
The standard Test Runner encountered a save-modified-scene dialog; it was canceled.
The unsaved New Scene was retained. Focused NUnit methods and their TestCase variants
were then invoked in place with their SetUp/TearDown methods, without opening test scenes:
113 passed, one unrelated failure. The failing existing
`Info004DasherActiveAnimationPreservesAuthoredScaleAndGroundClearance` expects scale 1
but the existing Dasher prefab is scaled to 1.3. No Dasher asset was changed.

Covered armor impacts/explosion identity, launch protection, pooling, UI exclusion,
prefab scales/materials, upper-body/jog masking, Block through both shield stages,
final-break damage animation, mirrored equipment interpolation and shared animation feedback.
Evidence: `Temp/ArmoredValidation/in-place-results.txt` and `in-place-summary.txt`.

An isolated preview-world bake used the actual prefab and production bakers. It produced
six sampled skins and two equipment render entities; every EnemyVisualOwner was
animation-driven. Evaluated skin matrices were finite through armor 2 -> 1 -> 0 -> 2,
and the visible equipment count followed the same sequence. The original scene and
its unsaved edits were not replaced. Evidence: `Temp/ArmoredValidation/bake-results.txt`.

A 250-Armored isolated Editor probe evaluated 1500 sampled skins and 500 equipment render
entities. After 20 warmups, 120 completed animation-plus-visibility updates averaged
0.252 ms/update. This excludes gameplay, GPU, physics and total frame time; it is not
a target-device performance result.

No full campaign playthrough, real-time encounter timing/readability review, target-device
run or full project test-suite pass is claimed. The controlled Gauntlet_12 check was
updated to inspect equipment rather than shield UI, but was not run in this session.
