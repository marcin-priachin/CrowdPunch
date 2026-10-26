# Combat VFX validation - 2026-10-26

Scope: VISION-005/006, ENEMY-003, CHICKEN-003/004/007, WIZARD-006 and the existing
Dragon/Snake/hazard presentation rules. Visual changes only; no combat tuning or encounter edits.

## Performed checks

- Unity 6000.3.10f1 / URP 17.3: runtime and Editor scripts compile without errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:q` passed with zero errors.
  The build reported 431 existing/package warnings; Unity compilation remains authoritative.
- Focused EditMode suites: CombatFeedbackTests 11/11, CombatVfxTests 2/2,
  DragonFlameTests 39/39, WizardTests 29/29, ChickenBossTests 41/41,
  GroundHazardParticleTests 6/6: **128 passed**.
- New regression cases saturate ranged flight slots while retaining boss capacity, despawn and
  reuse slots, disable/re-enable presentation, saturate finite bursts and clear/reuse them.
- Existing tests cover trail reuse, impact budget/freeze expiry, prefab lifetime constraints,
  boss reflection/player danger, zone rules, flame cover and ground-hazard sample bounds.
- Rendered the owned particle prefabs in a Unity preview scene and inspected gameplay-camera
  captures. A controlled runtime stage exercised the actual spore, flame and projectile
  presentation systems with warning/active zones, both magical projectile families and a
  returned boss shot. These are synthetic visual checks, not full encounter playthroughs.
- Fixed issues found during validation: particle rock mesh had two submeshes; absent particle
  TEXCOORD1 could incorrectly activate circular clipping; faint flame erosion; occluded halo
  sizing/texture selection; imported asset reimports invalidating temporary authoring references.
- Temporary runtime entities, preview objects and camera changes were discarded on exiting
  Play mode. No scene files were saved. Unity was left stopped.

## Bounded-load measurement

Editor, 20 warmup and 120 measured updates with simulation frozen; measurements include
presentation callbacks and mesh submission, not full-frame simulation or GPU cost:

| Workload | Mean CPU per update | Calling-thread allocation over 120 updates |
| --- | ---: | ---: |
| 544 synthetic projectile samples plus staged samples, 64 occupied flight slots | 1.4540 ms | 0 bytes |
| 34 spore zones, 32 decorated (12 clouds + 24 pollen each) | 5.3662 ms | 0 bytes |

The active flight count and unsuspended presentation were checked after profiling.
This is an overload/budget check on the current Editor machine, not a standalone frame-rate
or target-hardware acceptance result. Maximum simultaneous spore fields are comparatively
expensive; lower CloudsPerZone, SporesPerZone or MaximumCloudZones for a tighter visual budget.
All ground boundaries remain visible when decoration reaches its cap. Projectile cores also
remain visible when flight decoration is dropped. No target hardware was specified.

## Remaining unrelated failures / limitations

The broader `Combat` test-name filter initially ran 130 cases. Two VFX failures were fixed
and their suites rerun successfully. Two BossAppearanceTests floor-clearance assertions
remain on untouched authored animation samples: campaign 10 minimum -3.61194 versus
required >= -1.925, and campaign 20 minimum -1.69485 versus required >= -1.625.
These require a separate boss animation/grounding pass; this change does not alter those samples.

The Editor also logs an existing Synty Sidekick ToolDownloader window error when Play mode
changes. No new VFX runtime exception remained in the final preview. A standalone player build,
GPU capture and complete encounter playthroughs were not performed.

Local, ignored inspection artifacts: `Temp/CombatVfx-preview.png`,
`Temp/VfxValidation/Vfx-final-stage.png` and `Temp/CombatVfx-performance.txt`.
The captured stage uses a synthetic floor/camera, so lighting differs from authored levels.

## Launched trail correction - 2026-10-26

The launch ribbon is now approximately one visible model width and 2.5 model lengths,
centred at the current animated pose's world altitude. Its filled, soft-edged mask and 0.95
head opacity replace the narrow eroded streak. Width is measured across horizontal flight;
length uses the largest posed model dimension. Speed determines the trail lifetime, so
spatial length is approximate during acceleration and deceleration.

Unity compilation and all 12 CombatFeedbackTests pass, including the new integration check
for raised model centres, speed-independent target length, and current skin pose overriding
inflated culling bounds. Inspected an actual launched Goblin model moving through the gameplay
camera in campaign 03, with simulation temporarily frozen and an Editor-only orbit driver.
The visible ribbon followed the flying body at model width; example measurement was 1.41 m
wide and approximately 5.12 m long. Temporary objects/drivers were discarded on leaving Play mode.

An initial parallel-job stress measurement exposed excessive worker wait overhead. The final
job runs synchronously under Burst and measures only the existing trail budget. Synthetic
250 launched roots / 750 mesh pieces, 20 warmups and 120 completed updates: pose measurement
0.5518 ms/update, bridge aggregation 1.7063 ms/update, zero calling-thread managed allocations.
Exactly 144 mesh pieces belonging to 48 roots were measured. The bridge timing excludes Mono
renderer callbacks; these are Editor CPU measurements, not standalone frame-rate acceptance.
Raw local report: `Temp/LaunchTrail-performance.txt`.

### Parallel cartoon line revision

The supplied Asterix references supersede the filled ribbon styling. Launch trails now draw
five softly blurred white speed lines without outlines, with constant stroke thickness and spacing along their full
length. A constant TrailRenderer width curve and equal head/tail opacity remove taper and fade.
Model-relative dimensions, posed altitude, lifetime calculation and pool budgets are retained.

Unity compilation and all 12 CombatFeedbackTests pass. The integration test bakes actual trail
geometry and checks equal width at head, middle and tail, in addition to the existing altitude
and length assertions. Rendered and inspected a curved trail made by the production pool,
using its baked mesh in a Unity preview scene. Shader compilation reports no messages.
The ignored local inspection image is `Temp/ParallelLaunchLines.png`. No gameplay scene was saved.

### Exposed speed line controls

Select `Resources/CombatFeedbackSettings.asset`, then use **Launch speed lines** and
**Speed line profiles (0 = oldest tail, 1 = enemy)**. Overall model width/length remain under
Launched trails. Opacity 0 is transparent and 1 is opaque. Stroke width is a fraction of the
full model-width envelope; blur runs from 0 (crisp) to 1 (Gaussian). Spacing is the span
between outer line centres. Colour alpha, line opacity, opacity curve and gradient alpha
multiply together. Width, blur and spacing curves multiply their corresponding controls.
For a fading tail, set the opacity curve to 0 at time 0 and 1 at time 1; leave it flat at 1
for the current uniform appearance. Inspector edits update pooled trails in Play mode.
Scripts editing fields/curves must call settings.RefreshTrailStyle().

All 13 CombatFeedbackTests pass, including a live-edit regression for scalar controls,
tail/head profile samples, colour gradient and reuse of the same GPU texture/material.
Unity compilation passes. Rendered default and nonuniform profiles through the production
pool's property block; checked fade/thickness/blur/spacing and the tail-to-enemy direction.
Temporary alternate settings were cloned and discarded; saved defaults retain the current look.
Ignored inspection images: `Temp/TrailProfiles-default.png` and `Temp/TrailProfiles-varied.png`.

## Tuning and reproduction

- Shared assets: `Assets/CrowdPunch/Resources/CombatVfxLibrary.asset`.
- Impact/time/camera budgets: `Assets/CrowdPunch/Resources/CombatFeedbackSettings.asset`.
- Owned particles/materials: `Assets/CrowdPunch/Prefabs/Feedback` and `Materials/Feedback`.
- Regenerate defaults with `Crowd Punch > Art Direction > Apply Combat VFX` in Edit mode.
  This intentionally reapplies authored visual defaults; tune the Resources assets afterward.
- Imported source packs and their license/readme files remain in their original locations.
