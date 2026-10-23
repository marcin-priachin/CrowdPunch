# Goblin Minion Dasher presentation

Verified 2026-10-23 in the Agent B Unity 6000.3.10f1 Editor.
Requirements: ENEMY-005..008, INFO-004, VISION-006.

The shared EnemyDasher prefab now instantiates the requested Goblin Minion prefab.
The physics root, prefab GUID and spawn-settings reference are preserved. Authored
model scale remains (1, 1, 1); the builder aligns the feet using translation only.
Dedicated materials retain Goblin Minion.psd under CrowdPunch/Cel/Enemy, with
LOD0 and moving-mesh shadow casting disabled to match the established style.
Root and descendant transform scales are checked against the supplied prefab.
EnemyDasherSpawnSettings enables Spawn From Ground. Motion 7 references the supplied
Goblin Minion@Spawn.FBX and its exported duration matches the 0.8333334-second clip.
Dasher playback interpolates bone rotations separately from sampled scale and
translation; direct matrix blending had reduced limb scale between rotated poses.

Roll Forward In Place supplies preparation and dash animation. Preparation follows the
telegraph timer; committed dash playback spans travelled/max distance once and
holds its last sample. Launch loops Roll Forward In Place and repunch restarts it. Idle,
walk/run, Take Damage, Look Around, Spawn and Die/reverse-Die cover other states.
Only authored Spawn may extend below the floor; other exported poses receive a
baked vertical correction without changing proportions or physics.

Dasher visuals are marked AnimationDriven, retaining a neutral material multiplier
and bypassing procedural deformation, flashes, bobbing and shake. The old
DasherPresentationSystem was removed. Existing telegraph particles remain.

Verification:

- Unity compilation succeeded with no reported compile errors or warnings.
- 66/66 focused EditMode tests passed: EnemyAnimationTests, EnemyFacingTests,
  DasherObstacleRedirectTests and EnemyPrefabCollisionContactTests.
  Two new cases verify preserved bone lengths/orthogonality between 170-degree
  roll poses during preparation and dash. The prefab check verifies all descendant
  scales, all exported bone axes and the Roll Forward In Place clip references.
- After enabling Dasher Spawn From Ground, all 11 EnemyGroundSpawnTests passed.
  The saved setting, configured motion 7, source Spawn clip and exported duration
  were checked directly in the assigned Editor. Model scales remain unchanged.
- Sample hash matched controller, mesh and avatar. All 11 motions contain
  32 frames for the 44-bone skeleton. Exported geometry clears the unchanged
  capsule floor outside the intentional Spawn emergence.
- Inspected a rendered contact sheet generated directly from the exported skin
  matrices, including Roll Forward In Place, locomotion, recovery and defeat.
- Controlled live campaign level 14 preview baked both the new prefab and a live
  Dasher skin with 44 bones and owner scale 1. The live rendered entity had
  AnimationDriven=1 and material multiplier (1,1,1,1) during dash recovery and
  positioning. Frames advanced from 602 to 1981; live bone-axis lengths remained
  0.999989..1.000007. The game view was inspected. Play Mode was stopped and
  the original scene restored. This was an automated Editor check, not human
  playtesting, a player-build check or target-device performance validation.
- A controlled 256/512-Dasher pose workload (44 bones each) retained bone-axis
  lengths 0.999990..1.000007. Editor job update/completion medians were about
  10ms in this run; these include Editor scheduling and are not device frame times.
- dotnet build Assembly-CSharp.csproj --no-restore failed during generated
  _GetProjectReferenceTargetFrameworkProperties resolution without C# diagnostics.
  Unity compilation and the focused Unity checks succeeded independently.

Local artifacts are under Temp/DasherVerification: tests.xml, tests.txt, poses.png
and roll-forward-game.png. Temporary verification tooling is retained there under Tools.
