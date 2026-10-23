# Goblin Jog Leg Layer

Date: 2026-10-23. Workspace: Agent A. Unity: 6000.3.10f1.
Requirements: INFO-004, ENEMY-002/012, shared COMBAT-011/014 and INFO-006 lifecycle.

Both Baseline and Ranged Goblins use the existing directional jog clips in
`Assets/CrowdPunch/Animation/` below the pelvis. The original upper-body animation keeps
ownership of torso, arms, head and equipment, including contact and ranged attacks.
The existing movement blend tree supplies eight directions, including the mirrored
right-hand diagonals and their half-cycle offsets. Movement is blended relative to facing,
with a separate gait clock and a smooth transition to the original pose when stopping.

`GoblinJogSampling` retargets the clips through the supplied Humanoid avatar. Separate CPJ1
assets store 32 frames per direction for eight thigh/calf/foot/toe bones, relative to the
pelvis. Runtime composition restores the original pelvis transform before applying the leg
mask. Original upper-body matrices remain bit-identical. Spawn, launch, recovery and defeat
retain their full-body animations; physics, movement rules and projectile timing remain owned
by existing systems. New render bounds cover the leg poses. Equipment has no jog layer.

The existing prefabs were updated in place using **Crowd Punch > Animation > Apply Goblin
Jog Legs**. Original CPA3 samples and controllers were retained. Both current authored 1.3
visual scale overrides were preserved. The older ranged asset assertion that required
matching the nested source's unit scale was replaced by sampling-preservation checks;
the separate runtime spawn tests still prohibit scripted proportion stretching.

## Verification

- Unity compilation completed with no reported errors.
- All 15 `EnemyJogAnimationTests`, 37 `EnemyAnimationTests` and nine
  `EnemyGroundSpawnTests` passed. Coverage includes directional/intermediate blends,
  facing, firing while moving, stopping, lifecycle bypasses, leg masks, equipment exclusion,
  and preservation of authored scale, controllers and original sample bytes.
- Actual generated rigs: 16 directional cases (eight per prefab) over 32 advancing updates
  retained every unmasked upper-body matrix exactly while the leg poses changed. Both
  generated jog dependency stamps were current. Report: `Temp/GoblinJogValidation/check.txt`.
- Reviewed a rendered pose sheet at `Temp/GoblinJogValidation/poses.png` showing Baseline
  and Ranged forward/right/back/left leg poses with their original upper-body animations.
- A controlled Campaign 5 preview verified the real baked eight-bone leg layer on both
  enemy types, original-only equipment, Baseline jogging, Ranged backward/strafe movement,
  and Cut Attack follow-through while the legs continued jogging. The probe observed 309
  advancing frames. For this bounded visual check, player health was maintained and ranged
  retreat distances/wind-up were adjusted in runtime ECS data only. Saved tuning was not
  changed. Report and capture: `Temp/GoblinJogValidation/live.txt` and `live.png`.
  The assigned Editor returned to its original stopped Bootstrap scene with background
  and auto-tick settings restored.
- Isolated Editor CPU profiling with actual generated body/leg samples: 500 owners / 1,508
  sampled parts, 20 warmups and 120 measured updates averaged 0.5488 ms/update for animation
  scheduling and completion. This excludes physics, rendering and GPU; it is not standalone
  FPS, a before/after performance comparison or target-device acceptance.
- The focused .NET build remains blocked by 132 missing generated package DLL references
  and four assembly-conflict warnings. Log: `Temp/GoblinJogValidation/dotnet.txt`. Unity
  compilation and focused tests passed separately.

Local reports/images under `Temp/` are ignored verification artifacts. No human combat
playtest, standalone build or target-device performance validation was performed.
