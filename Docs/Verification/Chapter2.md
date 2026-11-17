# Chapter 2 implementation and verification

## 2026-11-17 encounter revision

The Chapter 2 implementation follows LOOP-011 and SPIKE-001..005.

- Level 15 opens with 6 Baselines and 1 Alien Spike. A clear checkpoint leads to
  6 Baselines, 2 Exploders and 2 Spikes. The opening hint explains the volley,
  the launch burst and shot redirection.
- Level 17 supports the rotating-cover target with 9 Baselines, 2 Exploders
  and 1 Spike. The support cap remains 12.
- Level 18 alternates Spike/Exploder and Mushroom Shooter/Dasher groups.
  The four phases allocate 48, 56, 72 and 88 enemies, down from 318 to
  264 total. Baselines account for 160 of 264. Batches of up to 8 arrive every 6s,
  and reinforcement phases begin 6s after the previous last batch. All surviving
  attackers count toward final clearance. Spike shots survive timed overlap
  within the current run.
- The other levels use batches of 4 every 5s. Levels 11, 14 and 15 keep their
  introductory clear checkpoints, and objective and boss support keep their
  existing counts or caps and replacement rules. Chapter 2 has no static hazards.

Use Level Play's Campaign tab to preview levels 11–20. The Editor command
**Crowd Punch > Campaign > Apply Chapter Two Encounter Revision** reapplies
the recipes to the existing assets and keeps their spawn rectangles and GUIDs.

### Revision verification

- Unity compiled successfully. `dotnet build Assembly-CSharp.csproj --no-restore`
  completed with 0 errors and 309 existing warnings.
- All 28 Alien Spike tests passed. The new projectile cases cover timed overlap,
  a clear checkpoint, stale run generation and encounter completion.
- The revised level 18 asset test and campaign save test passed. In the same
  four-test Chapter 2 suite, two assertions still fail against pre-existing
  Chicken assets. The campaign fires on frame 12 and the legacy boss on frame
  18. The Foundry's campaign arena references baked shadow mesh copies, while
  the comparison expects identical mesh paths. Both differences predate this
  revision.
- Compared the 16 wave assets against the prior commit. Every serialized spawn
  region and wave GUID was retained, and each total equals the sum of its
  guaranteed enemy counts.
- The controlled Editor lifecycle probe completed levels 11–20, observing
  all 16 Chapter 2 wave definitions plus Gatekeeper at entry. It verified
  retry, objective completion, both track arrivals, Chapter 3 unlock and
  replay using an isolated save and injected defeats.
- The level 18 observer measured 6.01s between the first two batches, then
  shortened the remaining intra-phase waits for the probe. All three natural
  phase timers advanced with earlier enemies remaining. The final gate reached
  zero cumulative survivors before completion. Level 15 produced an active
  baked Alien Spike.
- The accelerated level 18 stress sample reached 264 roots, including 16
  Spikes and 16 Mushroom Shooters. The last phase's 15-second Editor sample
  had a 32.4ms median frame, including rendering and probe overhead. Natural
  clear time, overlap balance and target-hardware performance remain open.

The rest of this file records the original October implementation and its checks.

## Original implementation evidence

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

Gate-width amendment (2026-10-10, BARRICADE-006): level 13's plate and collider
now measure 2m across following the user's revised width. Both damage stages fit
inside the plate. Two 19 x 3m, 4m-high solid rock barriers join the gate edges and
extend beyond the arena perimeter. The shared campaign builder applies this layout
from level 13 onward. Unity compilation and all four Chapter 2 editor tests passed;
the scene test checks sealed side approaches, exit clearance behind the barrier and
navigation participation clearance. Difficulty still needs human playtesting.

Gate collision repair (2026-10-10): the initial rocks protruded ahead of the gate
and intercepted edge shots. Live casts with the baked enemy collider confirmed
terrain was nearer at X=0.7m and 1m. The rocks now sit 1.2m farther toward the exit,
overlapping the gate's rear half while exposing its front face. Live casts at
X=0/0.4/0.7/1m hit the gate first. Controlled launched-body impacts reduced durability
3 -> 2 -> 1 -> 0 in Play Mode. After destruction, a 0.5m-radius player-sized sphere
cast crossed the central opening; equivalent casts at X=-8m/+8m remained blocked
by terrain. The probe moved bodies and injected launch state/velocity, used inflated
player health to isolate combat, and ran in Editor preview without campaign saves.
All four Chapter 2 editor tests passed, including terrain overlap/front-face checks.

Human clear times, level 18's greater-than-three-minute target, difficulty and
readability need real playtesting. OQ-001 still leaves target hardware/FPS acceptance
open. Editor timings include tooling and frame caps; they do not establish player-build
performance. No standalone player build was produced for this batch.
