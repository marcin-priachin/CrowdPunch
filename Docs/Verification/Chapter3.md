# Chapter 3 implementation and verification

## 2026-11-17 encounter revision

Chapter 3 now implements LOOP-011 across levels 21–30. Level 22's final room
uses 7 Baselines, 2 Exploders and one each of Ranged, Dasher, Armored, Mushroom
Shooter and Alien Spike. Both new specialists follow the room boundary when
starting attacks and aiming pursuit, as required by BARRICADE-008.

Level 25 and 27 follow-ups arrive eight seconds after the previous last batch.
The first teaches permanent ground and the second teaches a periodic crossing.
Level 28 uses 264 enemies across 48/56/72/88 allocations. Phases 1 and 3 use
timed advancement. Phase 2 clears the remaining crowd after its Elite, and the
final phase counts all survivors. Its three ground patches retain their geometry,
damage and offsets. Initial batches are 4/5s in other compact encounters and
8/6s at 28. Level 24 keeps its three clear-gated 4/4s defense waves under
PROTECT-004. Levels 21 and 23 keep the clear checkpoints for Armor and Elites.

Preview the levels through Level Play > Campaign. The explicit **Crowd Punch >
Campaign > Apply Chapter Three Encounter Revision** command rewrites the saved
encounter recipes using the existing GUIDs and spawn banks.

### Revision verification

- Unity compiled successfully. `dotnet build Assembly-CSharp.csproj --no-restore`
  completed with zero errors and 309 existing warnings.
- 84 tests passed across Alien Spike, Mushroom Shooter, defense, hazards,
  campaign progression, scene wiring and barricade sector coverage.
  The two new room cases exercise attack eligibility on each side of a room
  boundary. The older Rolling Blob geometry test still compares asset paths
  and fails against the existing Quarry baked mesh copies.
- Compared all 17 revised wave assets with the prior commit. Spawn rectangles,
  GUIDs and exact-count totals checked out. The level 24 defense assets and
  level 28 hazard SubScene have no changes.
- The isolated Play-mode probe observed both specialists in level 22's final
  room, completed both gates, and exercised the level 24 first-breach failure
  and retry. A late activation stalled the initial level 21 defeat injection.
  The probe resumed after applying damage to the remaining enemy and monitored
  subsequent late activations. Its milestone tally counts the retried first
  defense wave once. The probe completed levels 21–30, unlocked Chapter 4,
  and verified replay and full-health retry.
- The hazard epochs synchronized at every phase boundary in 25, 27 and 28.
  Level 28's first two batches arrived 6.00s apart. The observer shortened later
  intervals within phases for the stress check. Levels 28.2 and 28.4 began with
  surviving attackers. The checkpoint before 28.3 reached zero undefeated.
  The final phase's 15-second Editor sample had a 78.4ms median frame with
  probe and recording overhead. Natural overlap balance, player clear time
  and target hardware performance still need playtesting.

## Original implementation evidence

2026-10-10. Requirements: LOOP-002..008, ENEMY-009..015, PROTECT-001..004,
GROUND-001..007, BARRICADE-006, TRACK-001..005, COVER-001..005, ROLL-001..007.

## Play and tune

Open `Assets/CrowdPunch/Scenes/Bootstrap.unity` and press Play. Existing Chapter 2
saves Continue into level 21. Direct previews are available through Window > Crowd
Punch > Level Play > Campaign, without writing the normal campaign save. Completing
level 30 unlocks the future Chapter 4. Legacy gauntlets retain their own tab.

Editable scenes are `Assets/CrowdPunch/Scenes/Campaign/Campaign_21.unity` through
`Campaign_30.unity`, each with its own ECS SubScene. Nineteen new wave assets and
independent objective/boss/hazard settings live in `Assets/CrowdPunch/Data/Campaign`.
The creation recipe refuses to overwrite existing content; tune saved assets directly.

| Level | Encounter | Initial composition / objective |
|---|---|---|
| 21 | Peel the Armor | 8B+1A > 12B+2A; armor ammunition supply |
| 22 | Heavy Traffic | Hold 12B+2X; 2m gate, three hits, sealed terrain sides |
| 23 | Borrowed Fist | 10B+1E > 14B+2R+1E; existing Elite support replenishment |
| 24 | Hold the Line | 12B > 16B > 20B; four every four seconds; first breach fails |
| 25 | Hot Ground | 10B > 14B+2R; permanent circle left of centre |
| 26 | Safe Side | Hold 10B+2X; five net track hits; permanent outer pocket |
| 27 | Wait for the Warning | 10B+1D > 14B+2R+2D; periodic west crossing |
| 28 | Crowd Channels | 59 > 77 > 96 > 110 including Elites; 100 x 100m |
| 29 | Window of Safety | Hold 10B; three-hit target; 100-degree opening at 20 degrees/s |
| 30 | Rolling Blob | Hold 6B; preserved legacy arena, boss settings and vulnerability cycle |

B = Baseline, R = Ranged, X = Exploder, D = Dasher, A = Armored, E = Elite.
Elites are additional to normal wave counts. Level 28 has 339 ordinary enemies and
three Elites initially; existing Elite support and armor safeguards can supply more.
Permanent patches have 2.5m radius. Periodic strips are 3 x 8m and use a 4/1.5/2.5s
inactive/warning/active cycle; the pair in level 28 starts four seconds apart.
Damage remains 12 at a shared 0.75s victim interval. Spawn banks are outside all
authored hazard footprints with clearance. Broad safe routes remain around patches.

## Verification

- Unity compilation passed. `dotnet build Assembly-CSharp.csproj --no-restore -m:1`
  passed with zero errors and 344 warnings from existing reference conflicts,
  obsolete Unity APIs, and the existing unused aspect field.
- All 16 campaign tests passed, including four new Chapter 3 tests covering catalog availability,
  population and safeguards, all navigation anchors, defense settings, gate side
  sealing/front exposure, hazard timing/spawn clearance, and identical Rolling Blob
  arena transforms/collision mesh references.
- 123 related tests passed: protected point 15, armor 20, Rolling Blob 31,
  ground hazards 20, hazard navigation 12, and Elite punch geometry 25.
- Scene validation caught inherited boss replenishment flags on defense waves and
  insufficient hazard/spawn clearance. Defense flags were disabled explicitly;
  level 28's periodic strips moved inward to X=-19/+19m, away from side spawn banks.
- The live Chapter 3 lifecycle check passed through Continue from level 20, all 19
  new waves, death/retry, defense breach/failure/retry and timed batches, track arrival,
  hazard phases, scene ownership/cleanup, level 30 completion, save/reload, replay,
  and the unavailable Chapter 4 boundary. It uses an isolated save and controlled
  defeat/objective injections, not a human combat clear. Runtime screenshots were
  inspected for gate access, defense layout, track placement, terrain, and hazards.
  The normal save was unchanged. Log: `Temp/CampaignValidation/chapter3-lifecycle.txt`.

## Large-crowd performance check

The lifecycle probe exposed a severe pause in level 28's fourth wave. Profiling the
110-enemy final wave in isolation identified `EliteCrowdSupportSystem` searching the
entire navigation grid when nearby staging positions were blocked. Its fallback now
retains search progress across frames, revalidates cached candidates, and checks at
most 32 in-bounds cells per Elite per update. Pending searches remain unstaged.
All 25 Elite geometry tests passed, including reaching a staging point around a long
obstacle across multiple updates.

Comparable 20-second Editor probes, with the player kept alive and simulation running:

| Marker | Before median / max | After median / max |
|---|---|---|
| Elite crowd support | 2.69 / 1712.12ms | 2.53 / 10.38ms |
| Main thread | 16.64 / 1774.09ms | 16.70 / 72.52ms |

These are sampled Editor CPU timings, including Editor overhead; they do not establish
target-device FPS. Logs are `Temp/CampaignValidation/chapter3-peak-profile.txt` and
`chapter3-peak-profile-fixed.txt`. No wave assets were changed by the isolated probes.

## Remaining playtesting

Human clear times, level 28's greater-than-three-minute target, encounter difficulty,
and target-device performance still need playtesting. OQ-001 leaves target hardware
and FPS acceptance unresolved. Controlled Editor checks do not establish those results.
