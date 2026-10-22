# Goblin Baseline Verification

Date: 2026-10-22. Workspace: Agent B.
Requirements: ENEMY-012, INFO-004, VISION-005/006; shared COMBAT-011/014 lifecycle.

The baseline prefab retains its GUID, capsule, Rigidbody authoring and shared spawn-setting
reference. Its visual source is the user-authored Goblin Trooper Variant prefab. Supplied
Idle, Walk/Run Forward In Place, Cut Attack, Take Damage, Jump In Place and Die clips are
sampled through the Humanoid avatar. Cut Attack is split at 38% for anticipation/strike;
Jump In Place supplies the flight segment; reverse Die supplies the rise. Contact and
recovery clocks remain gameplay-owned. The mace uses a generated one-bone skinned mesh.

## Checks completed

- Unity compilation completed with no errors after the final source changes.
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 -v:q`
  passed with zero errors and 34 existing package/obsolete-API warnings. The initial full
  generated-project dependency rebuild was stopped after prolonged package compilation;
  the focused source build above completed successfully.
- All 30 cases in EnemyAnimationTests passed, including four new Goblin checks for contact
  timing, walk/run and hit selection, launch/recovery/defeat/pool reuse, neutral colour and
  unchanged render transforms under impact feedback. The assertions, setup and teardown
  were invoked directly inside the assigned Editor because the normal Test Runner requested
  saving the user's dirty New Scene. The save dialog was cancelled; no scene edits were lost.
- Both sample files have current dependency hashes, matching bone order, 32 frames per motion,
  positive durations, finite skinning output and moving poses. All non-spawn body motions stay above
  the source floor within 0.0001 units. The mace shares the body floor correction and retains
  the supplied weapon texture. Body: 2,354 vertices/47 bones; mace: 300 vertices/one bone.
- Campaign 1 was loaded through Bootstrap in Editor preview mode. Frame counts advanced
  (3,970 to 4,851), six playback entities represented three bodies plus their weapons,
  contact strike and locomotion were observed, and all six material multipliers stayed white.
  The camera capture in Temp/AgentB/goblin-campaign01.png confirms the Goblins in the village
  with textured Full Cel shading. An exported-pose sheet was also reviewed.
- A completed-update benchmark on cloned live baked bodies used 20 warmups and 100 samples:
  200 bodies/400 renderers averaged 0.04665 ms; 1,000 bodies/2,000 renderers averaged 0.16965 ms.
  This measures EnemyAnimationSystem only, excluding rendering, physics, gameplay and FPS.
  All clones were removed and the prior world clock/pause state restored.
- Source and documentation diffs pass whitespace checks. Unity-generated YAML retains its
  normal empty-field whitespace. Original dirty New Scene was restored, outside Play Mode;
  the run-in-background setting was restored. Source pack materials and the variant were
  not overwritten. Existing Fantasy Ground and New Scene disk edits were preserved.

## Limits

This is controlled Editor verification, not human combat playtesting, a standalone player
build, target-device performance testing or a complete campaign playthrough. The animation
pack has no dedicated Flying, Falling Flat Impact or get-up clip, so the authored jump/fall
poses are reused as recorded above. Combat attacks, health, physics ownership and spawn population are unchanged. Ground
spawning now holds normal movement for the supplied 0.833-second Spawn animation.

## Ground spawn option follow-up (INFO-006, COMBAT-018)

The Spawn section of EnemySpawnSettings now exposes Spawn From Ground, default off and
explicitly enabled in EnemySpawnSettings.asset for the Goblin Baseline. The builder configures
SpawnMotion 7 on body and mace and preserves the Spawn clip's below-floor movement. Supported
enemies appear at the floor under their advertised XZ point after safe warning clearance;
unsupported/unchecked profiles retain drop spawning. Initial, pooled and restarted entries
share the same path. The existing recovery clock gates movement/attacks for 0.833 seconds;
a normal punch can interrupt emergence and keep the resulting launch momentum.

- 31 animation cases plus seven ground-spawn cases pass in isolated ECS worlds inside Unity.
  They cover floor alignment, exact XZ coordinates, warning obstruction, option off/no clip
  fallback, completion/reuse, the actual EnemyRespawnSystem pool/warning/activation path
  and immediate punch interruption.
- Final Unity compilation and the focused .NET source build pass (zero errors; 34 existing
  package/obsolete-API warnings for .NET).
- A live Campaign 1 restart was observed from frames 1122 through 1169. Body and mace both
  used the spawn state, root Y remained 0 (the floor plus collider clearance) and XZ remained
  (1.482428, -0.2970808) throughout. Progress advanced from 0.02 to 1 over the authored
  0.8333334 seconds; the state then disabled and the enemy returned to Active. Four camera
  captures under Temp/AgentB/ground-spawn-live-*.png show the underground start, emergence
  and completed state. The exported six-phase pose sheet was also reviewed.
- An isolated 500-body activation benchmark (three warmups, ten samples) averaged 17.79 ms
  for drop spawning and 21.07 ms with ground animation. This includes existing warning
  clearance/spacing and activation only, without physics or rendering, and is not an FPS claim.
- Spawn state is cleared by pooling and legacy restart; configuration and clip duration survive.
  The Editor returned to New Scene outside Play Mode with the background setting restored.
