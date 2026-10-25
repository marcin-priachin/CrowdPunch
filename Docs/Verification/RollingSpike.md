# Rolling boss Spike presentation (2026-10-24)

Requirements: ROLL-001/006/007, INFO-004/006, VISION-006.

Campaign 30/70 and legacy Gauntlet 21 use `Assets/Spike Cute Series/Prefabs/Spike.prefab`.
The follow-up size correction uses a fixed uniform scene scale of 4.95441 on all axes.
Its sampled idle/spin silhouette reaches the existing 3.6m collision radius, with 0.08m
floor clearance in all three scenes. Uniform fitting preserves Spike's wide proportions;
the existing 7.2m collision body and all encounter tuning remain authored. The original
pack prefab is unchanged. Runtime never changes the model's scale.

`RollingSpikeBuilder` exports 32 frames for each motion in the existing CPA3 format:
Idle, Hide and Spin Attack In Place, reversed Hide to Idle (wind-up), Take Damage,
Die, and forward Hide to Idle (post-spin recovery). Authored hit reactions cover the
configured protection window without resetting the committed action or timer.
Floor corrections translate skin samples; only the Editor builder authors the constant model scale.
Quaternion interpolation avoids scale loss between rotated samples. The material-color
system and the procedural rolling/squash/floor-angle path are removed. Spike textures
use dedicated `CrowdPunch/Cel/Enemy` materials and are excluded from monster-atlas replacement.

Verification:

- Unity 6000.3.10f1 compiled runtime and Editor assemblies successfully.
- `dotnet build Assembly-CSharp.csproj --no-restore` did not succeed. After refreshing
  the generated project files, MSBuild reported `Build FAILED` with zero warnings
  and zero errors, without a useful diagnostic. Unity compilation is the verified
  compiler result for this change.
- RollingBossTests: 31/31 passed, including damage gates, cycle timing, collisions,
  ownership, resistance, reset and completion.
- Final focused `Roll` filter: 42/42 passed, including campaign introduction/rematch
  arena preservation and the new scale/hit/death animation regression.
- Follow-up scale correction: Unity compilation passed. The focused campaign-30 source,
  grounding and hitbox-footprint regression passed (1/1). Independent preview-scene
  measurements for legacy 21 and campaign 30/70 found radius 3.60000014m and floor
  clearance 0.08000016m at scale (4.95441,4.95441,4.95441). The original collision
  settings assets were not modified. The .NET build still reports failure without diagnostics.
- BossAppearanceTests: 7/10 passed on the initial migration. Rolling source/grounding,
  animation transitions, scale-preserving interpolation, hit reaction and death passed.
  Other-boss failures: Gatekeeper and Chicken floor assertions, Dino sample-source hash.
- Final sample file: 26 bones, 32 frames, 9,152 finite matrices. Matrix determinants
  range from 0.999997 to 1.000002, with axis lengths from 0.999971 to 1.000030.
  Reversed wind-up endpoints match the forward emergence endpoints.
- Serialized scene comparison: shared-object changes are limited to the boss child
  reference and removal of obsolete visual pivot/floor-offset fields. Arena objects,
  gameplay settings references, boss transforms and wave data remain unchanged.
- Campaign 30 Play Mode during the initial native-size migration: frame count advanced from 9 to 17 during the server-side
  wait, then to 495 and 1,570. One boss and one 26-bone sampled animation owner
  were present. Natural play reached rolling and post-spin recovery. A controlled
  paused spin sample selected motion 1 and retained runtime bone axis lengths
  0.999993..1.000011; a single frame step uploaded the pose for a close-up render.
  The purple/red source palette rendered under the cel shader. No new gameplay
  errors were reported during the probe; the console retained Pipeline timeouts
  and an unrelated invalid Synty Tool Downloader window error from Play Mode entry.
- Full Editor suite: attempted, but the connector response timed out after 300s.
  The broad campaign-scene audits continued for over ten minutes; the run was
  cancelled through Unity's TestRunnerApi before the focused final run. No full-suite
  pass is claimed. The Pipeline cancel command did not stop the underlying NUnit run.
- Legacy opening hint now describes the spin rather than a red roll. Play Mode was
  stopped and the original campaign-20 SubScene restored after verification.

No target-device performance or human balance playtest is claimed.
