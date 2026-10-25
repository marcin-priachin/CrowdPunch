# Ninja humanoid presentation

Implemented 2026-10-25 for NINJA-001/005 and VISION-006.

`EnemyNinja.prefab` uses `Prefabs/Enemies/Fair Black Male Ninja 03 Variant.prefab`.
The supplied root scale is 1.2. The builder retains all source hierarchy scales,
aligning the feet to the existing capsule floor by translation only. Physics,
ambush settings, campaign placement and legacy recipes are unchanged.

The Ninja avatar retargets existing humanoid clips:

| State | Animation |
| --- | --- |
| Idle / positioning | Ninja pack Idle / Walk Forward In Place / Run Forward W Weapons In Place |
| Preparation | Goblin Trooper Cut Attack, normalized 0-.38, over the preparation clock |
| Arrival | Hold Cut Attack at .38 while direction stays locked |
| Dash | Cut Attack .38-.68, driven by committed dash distance |
| Ambush recovery | Cut Attack .68-1 over the Ninja recovery clock |
| Hit | Goblin Trooper Take Damage |
| Launched | Project Flying humanoid clip |
| Ordinary recovery / defeat | Reverse Die / Die |

The hood, mask and sword become sampled single-bone skins and follow their
authored attachments. Every piece uses the body's translation-only floor
correction. Rotation interpolation preserves bone scale. No procedural launch
pitch, recovery flip, squash, shake or body-color feedback is applied. Full Cel
materials keep the original Ninja textures; the atlas update excludes them.
Departure/arrival smoke remains.

Verification:

- Unity compilation passed with no C# errors.
- Ninja-filtered Editor checks: 43/43 passed, including interruption, scheduling,
  campaign recipes, authored scale, all exported bounds/floor clearance,
  humanoid dependencies and animation timing.
- Shared `EnemyAnimationTests`: 55/56 passed. The untouched Dasher prefab has
  authored scale 1.3 while `Info004DasherActiveAnimationPreservesAuthoredScaleAndGroundClearance`
  still expects 1.0. No Dasher assets were changed.
- Inspected a rendered six-pose preview from the exported sample matrices,
  including equipment, in `Temp/NinjaValidation/humanoid-samples.png`.
- Live Gauntlet_25: preparation, safe teleport, stationary warning, locked dash,
  natural player damage, bounded travel, recovery, second wave, two-slot
  arbitration, punch interruption and actual solver chain ownership passed.
  The older `NinjaEncounterPlayCheck` then fails its level-25 assertion when the
  completed encounter advances to level 26; it assumes level 25 ends the sequence.
- Live inspection found four Ninja animation entities, 39 total sampled bones,
  finite skin matrices and four renderers with `AnimationDriven = 1`. Frame
  count advanced from 4 to 1180 during the run.
- CPU animation microbenchmark: 3 Ninjas + 250 Baselines, 512 sampled renderer
  entities, 20 warmups and 120 completed updates: 0.0970 ms/update on this machine.
  This excludes physics, GPU rendering and FPS; it is not a balance playtest.
- `dotnet build Assembly-CSharp.csproj --no-restore` could not complete because
  generated `obj/Debug` build files denied writes. Unity compilation is the
  successful compilation result for this change.
