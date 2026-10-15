# Boss appearance verification - 2026-10-15

Scope: COMBAT-008, BOSS-002/008, CHICKEN-007, ROLL-007, PILLAR-006 and FINALE-003/007. Existing models, the Ultimate Monsters atlas, supplied clips and the existing ECS skinning shader are reused. No asset purchase, package install, imported FBX edit or gameplay tuning change is required.

## Saved content

- Campaign boss levels 10, 20, 30, 40, 50, 60, 70 and 80; legacy boss levels 11, 20, 21 and 22; finale level 81.
- Animated atlas-backed Gatekeeper head, gold/purple fist materials, anticipation squeeze, recovery recoil and stagger wobble.
- White/red/gold Chicken with release-timed wind-up, faster rush stride and shot recoil.
- Green/pale-spiked Rolling Blob with charge compression, a quiet spinning pose, angular recovery and a baked 64-angle floor support curve.
- Purple/gold Dino with an anchored warning crouch, faster burst gait and blended hit/death poses.
- Royal-blue/gold finale palette, preserved face and wing details, cyan vulnerability emission/ring, landing compression, per-clip grounded support heights and strikes triggered when warnings resolve.

The appearance migration replaces model children only. Existing boss root transforms, colliders, arena geometry, settings assets, wave assets and progression references are retained. Unity's unrelated serialization of default arena/wave fields was removed from the diff.

## Automated checks

- `dotnet build Assembly-CSharp.csproj --no-restore -m:1`: passed, zero errors, 369 existing package/deprecation warnings. Log: `Temp/BossAppearanceBuildFinal.log`.
- Unity compilation: no outstanding compilation failure.
- Boss-filtered EditMode suite: 114/114 passed, including the nine new appearance cases.
- Ordinary enemy animation suite: 26/26 passed.
- Finale suite: 11/11 passed.
- Dino pillar suite: 29/31 passed. The two existing `Pillar006_SupplyHonorsCountAndReusesLaunchedOrPooledSlots(1)` and `(4)` failures still return zero slots. These fixtures and their supply implementation were not changed; the existing architecture verification notes already record the two Dino supply failures.

The new appearance checks validate sampled-source hashes, atlas references, model floor clearance, 128 rolling orientations including interpolation between support samples, all boss animation phase transitions, unchanged physical transforms/velocities, and 300 repeated hand updates without accumulating deformation.

## Editor runtime checks

Entered each introductory boss scene and the finale in Play mode with temporary review cameras. Confirmed advancing frames and successful runtime baking. Inspected actual rendered Gatekeeper, Chicken, Rolling Blob, Dino and finale models. Controlled rolling and downed-finale states were used to inspect their presentation; this was not a full combat playthrough.

The Gatekeeper had its sampled skin in the runtime world. Both Rolling Blob renderers had valid floor-support blobs and synchronized changing roll angles. The finale's blue body palette was previewed on its actual skinned mesh before saving the UV-only copy.

A controlled runtime world containing 133 ordinary enemies and the Gatekeeper was used for a bounded CPU check. Across 250 warmed iterations, `GatekeeperAnimationSystem`, `BossHandPoseSystem` and `BossPresentationSystem` together averaged 0.067 ms per completed update and allocated zero managed bytes on the calling thread. This is an Editor system microbenchmark, not a standalone frame-rate or GPU claim.

All temporary cameras, crowd clones and forced states were discarded on leaving Play mode. The Editor was restored to the clean, stopped `Campaign_81` scene. Human playtesting should still judge attack readability from the normal gameplay camera, animation feel and encounter pacing.

## Rolling eye and error follow-up

The reported misplaced eye in Campaign 30 coincided with repeated Burst `IndexOutOfRangeException` errors in `RollingAnimationSystem`. A recovery angle close to negative zero made floating-point `frac` round to exactly one, producing index 64 for a 64-entry floor curve. Job interruption left the two renderers at different playback phases and one without its floor correction.

The lookup now wraps the first index at the curve seam as well as its neighbour. Four new regressions run two renderers with a real floor blob through 180 updates, covering the captured negative angles, exact zero and positive recovery. All 13 appearance tests pass. The C# build passes with zero errors and the existing 369 warnings (`Temp/RollingEyeFixBuild.log`).

In the real Campaign 30 world, 240 controlled updates at the captured failing angle completed for both renderers with matching phase/angle and finite skin matrices. Subsequent rendered frames show the eye attached, and the console reports no new runtime errors.
