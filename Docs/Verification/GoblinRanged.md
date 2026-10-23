# Goblin Ranged Enemy Verification

Date: 2026-10-23. Workspace: Agent A. Unity: 6000.3.10f1.
Requirements: ENEMY-002/004, INFO-004/006, VISION-005/006; shared combat lifecycle.

`EnemyRanged.prefab` retains its GUID, physics capsule and shared settings reference.
Its visual source is the supplied `Prefabs/Enemies/Goblin Trooper Wizard Variant.prefab`.
The Goblin pack's Humanoid motions are sampled into the existing 11-motion, 32-frame
GPU-skinning format. Body, hat and wand use matching sampled poses; the rigid accessories
are converted to one-bone skinned meshes. Materials match the Baseline Full Cel shader
and preserve source textures. The Monster Atlas refresh excludes both Goblin material folders.

Cut Attack is sampled in full for anticipation and follow-through. Projectile Launch Frame
selects a zero-based authored frame (0-25, default 10) in the ranged spawn settings.
Anticipation is retimed to reach that frame at the end of Wind Up Duration, and the emitted
projectile sequence starts the remaining clip once. The frame setting changes playback
without resampling. The builder retains the source model's scale; ranged initialization
adds no scripted proportion scaling. Cancellation and
launch discard firing follow-through. Existing projectile, navigation and physics rules
remain authoritative. The Goblin profile supplies authored locomotion, damage, jump flight,
Die landing/defeat and reverse-Die recovery. `AnimationDriven` render ownership bypasses
runtime tint pulses, hit flashes, bobbing and procedural deformation. The ranged settings
enable Spawn From Ground; all three parts use the pack's Spawn in slot 7.

## Initial integration checks

- Unity recompilation passed with zero errors and zero reported warnings.
- Unity Edit Mode Test Runner: all 34 `EnemyAnimationTests` and all seven
  `EnemyGroundSpawnTests` passed. New checks cover ranged anticipation timing, actual-shot
  follow-through, cancellation, launch interruption, source prefab/controller/material wiring,
  current dependency hashes and the ground-spawn setting. Existing tests cover pooled reuse,
  recovery, neutral color/deformation bypass, safe warning placement and punch interruption.
- Every generated part has finite sampled matrices, positive motion durations and 32 frames.
  Body uses 47 bones; hat and wand each use one bone. Applying the shared material
  configuration retained each Goblin texture.
- A controlled Campaign 5 Editor preview observed natural warning/ground spawn and firing
  after restart. Player health was maintained during this check. Emergence lasted the authored
  0.8333334 seconds, with no sampled XZ drift above 0.002 m while emergence was active.
  Body, hat and wand all selected anticipation during wind-up and follow-through when shot 1
  fired, then completed the one-shot. All three render entities carried `AnimationDriven`
  and neutral color multipliers. The probe observed 427 advancing frames / 7.76 game seconds.
- Live captures were reviewed at `Temp/RangedWizardValidation/spawn.png`, `windup.png`
  and `firing.png`; the report is `live-check.txt` in the same folder. These are local,
  ignored verification artifacts. The assigned Editor returned to the original New Scene
  outside Play Mode, with its original background and auto-tick settings.
- Isolated Editor CPU measurement with actual generated samples: 500 ranged owners / 1,500
  sampled parts, 20 warmups and 120 animation updates averaged 0.4431 ms/update for scheduling
  plus job completion. This excludes physics, rendering and GPU work and is not standalone
  FPS or target-device acceptance. Report: `Temp/RangedWizardValidation/profile.txt`.
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 -v:q` failed
  with 132 CS0006 errors for absent generated package DLL references under `Temp/Bin/Debug`
  and four assembly-conflict warnings. Unity's own compilation and tests passed separately.

The first asset rebuild request exceeded Pipeline's main-thread response timeout, but
completed successfully. Its resulting assets and current dependency hashes were verified;
the rebuild was not repeated blindly. The Unity CLI update check could not reach its CDN.
No source-import changes, campaign scene edits, standalone build, human combat playtest
or target-device performance validation were performed.

## Prefab scale and projectile frame follow-up

- Removed the builder's scale reset/height fitting and the ranged spawn system's
  `(0.85, 1.3, 0.85)` proportion stretch. Generated visual scale now equals the supplied
  prefab scale. Root capsule authoring is retained.
- Added **Projectile Launch Frame** to the ranged section of Enemy Spawn Settings.
  `RangedEnemySpawnSettings.asset` defaults to authored frame 10 of 0-25. Wind Up Duration
  still sets the delay to firing; anticipation reaches the selected frame at that time.
  Complete Cut Attack samples allow changing the frame without rebuilding the samples.
- Clean Unity compilation passed with zero errors. After an initial stale compiled spawn
  path failed the new scale regression checks, a clean script compilation resolved it;
  all 37 animation tests and all nine ground-spawn tests passed. Frame tests cover 0, 10
  and 25, while spawn tests cover both absent and explicitly authored proportion matrices.
- Controlled Campaign 5 preview verified no runtime root scale matrix, baked frame 10/25,
  and a temporary runtime-only switch to frame 20/25. The next natural shot played that
  release pose on body, hat and wand, then completed once. Ground emergence and neutral
  materials remained correct. The probe observed 406 advancing frames / 7.67 game seconds.
  Player health was maintained; no human combat playtest was performed.
- Report and captures are under `Temp/RangedWizardValidation/frame-setting/`; the saved
  settings remain at frame 10. The original scene, stopped state and background settings
  were restored, and auto-tick was disabled again.
- The focused .NET build still fails with the same 132 missing generated package-DLL
  errors and four assembly-conflict warnings. Log: `Temp/RangedWizardValidation/dotnet-frame-setting.txt`.
- The updated samples passed finite-pose/texture checks. An isolated CPU repeat with 500
  ranged owners / 1,500 parts averaged 0.1480 ms per animation update (20 warmups, 120
  measured updates, schedule plus completion). This excludes physics/rendering/GPU and
  is not an FPS claim. Report: `Temp/RangedWizardValidation/frame-setting/profile.txt`.
