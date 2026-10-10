# Chapter 2 implementation and verification

2026-10-10. Requirements: LOOP-002..008, ENEMY-004..008, SHELL-001..006,
TRACK-001..005, BARRICADE-001..005, COVER-001..005, CHICKEN-001..008.

## Play and tune

Press Play in `Assets/CrowdPunch/Scenes/Bootstrap.unity`. Existing Chapter 1 saves can
Continue into level 11. For direct save-isolated previews, use **Window > Crowd Punch >
Level Play > Campaign** and select levels 11-20. Chapter 3 unlocks after Chicken but
remains unavailable until authored. All 23 legacy levels remain in the Legacy tab.

Editable scenes: `Assets/CrowdPunch/Scenes/Campaign/Campaign_11.unity` through `_20.unity`,
each with its own ECS SubScene. Sixteen new waves and separate objective/boss settings
live under `Assets/CrowdPunch/Data/Campaign/`. The creation recipe refuses to overwrite
the batch; tune saved assets directly. No shared ordinary enemy stats were changed.

| Level | Encounter | Objective / authored population |
|---|---|---|
| 11 | First Fuse | Clear 6B+1X, 10B+2X |
| 12 | Crack the Shell | 3 explosion shell, then fresh core damage; hold 8B and paired 2X |
| 13 | Blast the Gate | 3-hit barricade then exit; hold 10B+2X |
| 14 | Committed Charge | Clear 8B+1D, 12B+2D |
| 15 | Crossed Threats | Clear 12B+2R+1D, 16B+2R+2X+2D |
| 16 | Push the Block | 5 net forward track hits; hold 8B |
| 17 | Moving Window | 3-hit target; 90-degree gap, 25 degrees/s; hold 10B+2X |
| 18 | Crowd Reaction | 100 x 100m; 48B+4X, 64B+4R+2D, 80B+6X+2D, 96B+4R+4X+4D |
| 19 | Push from Behind | Off-centre track, 5 net forward hits; hold 8B+2X |
| 20 | Chicken Run | Existing Chicken arena and boss tune; hold 6B |

B = Baseline, R = Ranged, X = Exploder, D = Dasher. Bounded objective replacements
use four seconds; shell Exploders replenish as a pair only after both are gone,
and stop replacing after shell break. Finite waves have three-second entry delays.
Level 18 contains 318 total enemies, 108 in its largest initial simultaneous wave.
Its 3.5-4.5 minute target is a tuning goal, with no forced timer or idle waiting.

Chicken starts with its existing 1800 health, 0.8-second wind-up, 1.5-second shot
spacing and existing single/paired stages. Geometry, model and projectile assets
are reused; campaign boss tuning is independent of the legacy settings.

## Verification

- Unity compilation passed. `dotnet build Assembly-CSharp.csproj --no-restore -m:1`
  passed with zero errors and the 344 existing package/generated/legacy warnings.
- 105 tests passed: 7 campaign persistence/integrity, 4 Chapter 2 integration,
  28 gauntlet regression, 5 shell, 17 track, 22 Chicken, 12 barricade, 10 rotating cover.
- Scene checks validate all ten new levels, campaign-owned settings, wave references,
  copied shell visuals, track/socket references, navigation settings and large-arena
  population/footprint. Chicken arena transforms and collision mesh references match
  its original scene. Progress tests exercise continuation of a Chapter 1 save,
  sequential unlocks, replay preservation and unavailable Chapter 3.
- Rapid save tests exposed transient Windows replacement failures. Save replacement
  now retries for at most 70ms and still reports persistent failures. A test holds
  the backup file locked for 25ms and confirms the completion is saved after release.
  Replays of saved levels do not rewrite the save or rotate its recovery backup.
- The Editor lifecycle probe injects defeats/objective completion using an isolated
  temporary save. It is not a natural combat or duration playtest.
- The completed lifecycle run covered Gatekeeper's Continue boundary and all ten new
  encounters (17 observed waves including Gatekeeper, 16 belonging to Chapter 2).
  Death/retry, full-health entry, encounter ownership cleanup, both physical track
  arrivals, Chicken completion, saved Chapter 3 unlock/availability and replay/retry
  passed. The normal campaign save remained unchanged. No new console errors occurred
  during the completed run and subsequent repaired-objective previews.
- All four large-arena waves spawned their authored populations. Median Editor frame
  samples were about 16.7ms, 17.3ms for the 108-enemy wave, over 15-second samples.
- Scene clearance tests exposed and corrected an entry-derived navigation anchor too
  near its grid boundary. All Chapter 2 navigation anchors now pass all radius classes.
  Visual inspection also caught copied shell/rail local positions losing parent height;
  copying now preserves world pose explicitly. Tests require floor-aligned solids and
  above-floor track markings. The repaired shell and both tracks were inspected in Play.
- A separate repaired-shell runtime check parked the crowd and injected real Exploder
  detonation requests: one survivor prevents top-up, zero survivors replaces exactly
  the two existing slots, the third blast exposes an untouched core and disables further
  Exploder replacement. A fresh punch request killed the core and advanced to level 13
  with survivors present. These are controlled interaction checks, not player clear times.
- Unity was left in Edit Mode with Bootstrap open; build registration has 21 scenes
  (Bootstrap plus levels 1-20). Chapter 1 scenes, shared ordinary profiles and all legacy
  scenes/settings were checked against the pre-Chapter-2 commit and remain unchanged.

## Remaining playtesting

Human clear times, level 18's greater-than-three-minute target, difficulty and
readability need real playtesting. OQ-001 still leaves target hardware/FPS acceptance
open. Editor timings include tooling and frame caps; they do not establish player-build
performance. No standalone player build was produced for this batch.
