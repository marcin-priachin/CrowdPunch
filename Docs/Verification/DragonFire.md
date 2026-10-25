# Dragon Fire presentation - 2026-10-25

Requirements: DRAGON-001/002/006, VISION-005/006, INFO-004.

- EnemyDragon.prefab now nests Assets/Dragon Fire Cute Series/Prefabs/Dragon Fire.prefab.
  Every source transform retains its exact authored local scale. The unchanged 2 m capsule
  has its floor at root-local y=-1; visual placement is translation only.
- Fly Idle and Fly Forward In Place provide locomotion. Cast Spell spans warning/active
  flame and loops during launched continuous flame. The sampled RigJaw attachment uses
  the same phase as the skin matrices. Take Damage provides hit/recovery; Die provides defeat.
- DragonEnemySpawnSettings has Spawn From Ground enabled. SpawnMotion 7 samples the full
  one-second Cast Spell through the shared spawn/recovery clock.
- AnimationDriven excludes body tint, impact shake/squash and scripted launch/recovery
  rotations. Pose interpolation preserves authored bone scale. Only death poses crossing
  the floor receive an upward translation in the exported samples.
- The game-owned CrowdPunch/Cel/Enemy material retains Dragon Fire.psd. The shared atlas
  application skips this material folder. Source model/material/animation assets are unchanged.
- All campaign/legacy uses inherit the existing EnemyDragon prefab reference; combat tuning,
  physics dimensions, ownership, cover tests, waves and saves are preserved.

Verification in Unity 6000.3.10f1:

- Unity compilation passed. Dragon-filter Editor tests: 61/61, including source scale,
  material/clip dependencies, death floor clearance, spawn interruption and jaw/cast clocks.
- Shared EnemyAnimationTests: 55/56. Existing Dasher prefab scale 1.3 differs from source 1;
  that unrelated asset was not changed.
- Real Gauntlet_26 controlled baked-world checks passed: moving warning/burst, punched
  continuous flame, collision propagation, recovery/cooldown, zero-health deferred flight,
  exact 6+1 / 12+2 finite waves, retry and scene cleanup. The old checker now accepts
  progression into Gauntlet_27 as well as an end-of-sequence completion.
- Visual inspection confirmed the orange textured cel Dragon renders in the live encounter;
  runtime body has scale 1, 55 sampled bones, AnimationDriven=1 and spawn duration 1 second.
- Representative capture: 250 enemies including 10 Dragons, three seconds advancing
  solver/animation. EnemyAnimationSystem: 0.1142 ms/completed update, zero calling-thread
  allocations over 120 updates after 20 warmups. Editor CPU snapshot with frozen physics
  during measurement; this is not standalone FPS/GPU or target-hardware acceptance.
- An initial dotnet build Assembly-CSharp.csproj --no-restore passed (498 existing warnings).
  The final repeat failed with zero errors/warnings and no diagnostics; Unity compilation
  and the focused tests remained successful.

Temporary live logs, crowd measurements and screenshots are under Temp/DragonValidation.
The original New Scene 1.unity edit was preserved. Editor preview settings were restored.
