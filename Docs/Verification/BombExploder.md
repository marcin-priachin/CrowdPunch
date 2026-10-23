# Bomb Exploder presentation

Implemented 2026-10-23 in the Agent B workspace and its assigned Unity 6000.3.10f1
Editor. Requirements: INFO-004, ENEMY-010, ENEMY-013.

## Saved integration

- `Assets/CrowdPunch/Prefabs/EnemyExplosive.prefab` retains its GUID, physics capsule,
  mass, constraints and campaign spawn references. Its visual is the supplied
  `Assets/Bomb Cute Series/Prefabs/Bomb.prefab`.
- The six-bone, 708-vertex mesh retains the supplied Bomb texture and uses the
  established `CrowdPunch/Cel/Enemy` shader. Emission pulsing and tint masks are off.
  Generic readability tinting, impact flashes and scripted squash bypass its
  animation-driven render children. The pack's companion spark particles are omitted.
- The 32-frame samples and controller use clips from `Assets/Bomb Cute Series/FBX`.
  Idle and Forward In Place loop; contact pursuit loops Going To Explode. Launches
  play Take Damage once, restarting on repunch. Recovery follows the gameplay clock
  through Spawn; ordinary defeat plays Die; detonation plays Explode.
- Anticipation adds no fuse. Collision, damage, blast radius and chain detonation
  stay owned by the existing gameplay systems. The defeated body remains visible
  for its appropriate clip before pooling, including contact explosions without a
  prior launch. Ground-spawn configuration is unchanged; its authored slot remains
  available. Sampled Idle geometry, rather than cached imported renderer bounds,
  aligns the model to the existing capsule floor by translation only. The source
  prefab's authored scale and proportions are preserved; no builder resizing is applied.
  Explosive spawning no longer adds the old `(1.25, 0.8, 1.25)` role scale.
- Die and Explode samples receive a uniform upward translation only where needed
  to keep all skinned vertices above the capsule floor. Bone rotations and scales
  remain authored; spawn emergence is unaffected. Culling bounds include the
  corrected poses.

## Verification

- Death-floor correction: the new regression check reproduced floor penetration in
  Die at frame 8, interpolation 0.25 (minimum owner-local Y -1.072234 versus floor -1).
  Regenerated death samples clear the floor at all 32 exported frames and quarter-frame
  interpolation points in both Die and Explode. The test also compares every bone's
  rotation/scale matrix with the source clips to verify translation-only correction.
  The prefab's configured uniform 1.5 scale and visual offset are retained. Rendered
  midpoint/terminal poses are in `Temp/BombVerification/DeathFloorPoses.png`.
- Authored-scale correction: Unity recompiled, 11/11 `EnemyGroundSpawnTests` passed
  (including Explosive spawning with and without an authored transform matrix), and
  the focused Exploder prefab/sample test passed. The latter checks source-prefab
  source proportions, floor placement and current animation sample dependencies.
- Unity compilation completed without errors.
- 43/43 `EnemyAnimationTests` passed after the death-floor correction. These cover
  actual animated sample frames, source dependency hashes, upright saved Idle,
  floor alignment, pursuit/recovery/defeat selection, launch interruption, physics
  ownership, pooling and bypass of tint/deformation.
- 3/3 `EnemyLandingLifetimeTests` and 7/7 `EnemyPrefabCollisionContactTests` passed.
  Contact explosions retain the 0.166667-second Explode visual; ordinary defeat
  retains the 1.333333-second Die visual. Deadlines do not extend each frame.
- Campaign 11 was loaded through the Editor preview path. Live baked data confirmed
  six skin matrices per Bomb, `AnimationDriven = 1`, and neutral `(1,1,1,1)` material
  multipliers. Controlled runtime poses and camera captures confirmed the model,
  texture, shading and corrected floor alignment before the authored-scale correction.
  Frame and world-time snapshots
  advanced after dismissing the campaign failure pause.
- A controlled Editor presentation check with 250 Bombs measured 0.024106 ms mean
  per animation-system schedule-and-complete over 200 iterations after warmup.
  Gameplay/physics groups were disabled for this measurement; it is not a full-game
  or target-device performance result.
- Evidence is in local `Temp/BombVerification/`, including authored pose renders,
  live captures, test results and runtime snapshots. Temporary runtime clones were
  discarded by exiting Play Mode; the original Editor scene remains open.

## Limits

The ordinary `dotnet build Assembly-CSharp.csproj --no-restore -m:1` attempt was
stopped while rebuilding package projects. A follow-up with
`-p:BuildProjectReferences=false` failed on 90 missing generated package metadata
references, with four package warnings. Unity compilation and tests are the
verified C# result. No standalone build, human combat playtest, target-device
performance run or full campaign regression suite was performed. The Editor also
reported an unrelated invalid Synty Sidekick Tool Downloader window.
