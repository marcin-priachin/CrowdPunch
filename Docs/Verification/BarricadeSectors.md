# Ascending barricade sectors

Date: 2026-10-12. Requirements: BARRICADE-007..009.

The user chose to preserve each final sector's layout, hazards and gate difficulty
while allowing its enemy mix to change. Levels 22/34/42/56/62/74 now have different
earlier obstacle arrangements and rising bounded crowds. Their original final obstacle
positions/footprints, hazard settings, spawn banks, gate tuning and exit are retained.
Single-sector introductions and legacy gauntlets are unchanged.

| Level | Sector populations | Final mix | Static patches across level |
| --- | --- | --- | --- |
| 22 | 8, 14 | 9 Baselines, 2 Exploders, 1 Ranged, 1 Dasher, 1 Armored | 0 |
| 34 | 8, 11, 14 | 9 Baselines, 2 Exploders, 1 Ranged, 1 Dasher, 1 Armored | 1 |
| 42 | 8, 10, 13, 16 | 11 Baselines, 2 Exploders, 1 Ranged, 1 Dasher, 1 Armored | 3 |
| 56 | 8, 9, 11, 12, 14 | 9 Baselines, 2 Exploders, 1 Ranged, 1 Dasher, 1 Armored | 0 |
| 62 | 8, 9, 11, 12, 14, 16 | 11 Baselines, 2 Exploders, 1 Ranged, 1 Dasher, 1 Armored | 4 |
| 74 | 8, 9, 11, 13, 14, 16, 18 | 13 Baselines, 2 Exploders, 1 Ranged, 1 Dasher, 1 Armored | 5 |

Earlier layouts alternate small side pockets and paired offset islands, with obstacle
footprint area strictly increasing toward the preserved final arrangement. First sectors
contain seven Baselines and one Exploder. Ranged enemies appear next; Dashers enter from
halfway through, Armored enemies from the last quarter. Every room retains at least six
renewable Baselines. Earlier hazard patches use smaller periodic footprints with 5s
inactive / 1.5s warning / 2s active cycles. Final hazard settings remain original.

Ranged positioning/attack and Dasher decisions/movement now respect spawn-room ownership.
Off-room specialists retain local coverage/reentry intent. New off-room shots cancel;
voluntary dashes stop before the inset edge. Launched bodies remain physics-driven.
Existing gate-owned replenishment restores specialists in the same slots and resets
their states/armor. The finite-wave armor ammunition safeguard is disabled here because
the bounded replenishing Baseline crowd already supplies ammunition.

## Verification

- Unity compilation succeeded with Burst enabled; no runtime Console errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1`: zero errors, 344 existing warnings.
- Campaign checks: 42 tests exercised, with three outdated assumptions corrected and the
  affected Chapter 5/7/8 suites passing on rerun. Crowd selection is explicitly by gate
  order; reduced earlier hazards are checked separately from exact final hazards.
- Barricade filter: 43 passed, including seven new specialist movement/attack cases and
  one new sector-layout test. Dasher obstacle redirect: 2 passed; deliberate shots:
  8 passed; Armored enemies: 20 passed; protected point: 17 passed.
- All six final sectors' serialized obstacle/hazard/gate signatures were compared with
  a snapshot taken before authoring, ignoring transient Editor instance IDs: unchanged.
- Reapplied saved-sector authoring, then reran both sector-layout and room-chain tests:
  passed. Final footprints, ascending obstructions/populations, exact mixes, safe spawn
  banks, connected navigation, gate seals and exit placement were checked.

## Live baked-world checks

Controlled Bootstrap run through all six levels and all 27 sectors. Used an isolated
temporary completed save, restored player/living NPC health, placed the player for entry,
and injected launched Baseline bodies with extra health into gates set to their last hit.
Crowd movement, specialist behavior, hazards, collision and navigation ran normally.
This verifies implementation and integration; it does not establish human difficulty
ranking, combat balance or clear-time targets.

- Every sector spawned its exact authored population and profile counts. Future sectors
  stayed dormant until the previous gate broke and the player entered.
- Each sector provided at least 147 sampled updates of normal room-local AI; level 74's
  final sector provided 598. Off-room shooters did not wind up, off-room Dashers remained
  in positioning, and travel targets remained in their original spawn rooms.
- Ranged, Dasher and Armored enemies were independently pooled and replenished in level
  22's final sector. Generation advanced, ownership and root count stayed fixed, and
  Armored shields reset to two stages.
- Each of the 27 physical last-hit shots broke its gate and continued through the opening.
  Breaking each gate stopped only that owner's Baseline/specialist replenishment.
- All six final exits completed without clearing survivors. Normal campaign progress was
  not modified, and the originally open level-42 SubScene was restored after Play mode.

Some dense banks temporarily left one requested body pending for safe placement; the
existing retry filled every tested crowd to its exact requested total without stalling.

Level-74 Editor profile, normal moving crowds/hazards, 89 owned roots and 66 nonpooled
at the final snapshot (601 samples):

| Marker | Median | Maximum |
| --- | --- | --- |
| Main Thread | 16.638ms | 24.382ms |
| Navigation | 0.047ms | 0.147ms |
| Ground hazard navigation | 0.073ms | 3.414ms |

Raw build, preservation and runtime logs remain under ignored
`Temp/BarricadeSectorValidation/`. OQ-001 hardware/performance targets remain unresolved;
human pacing/difficulty playtesting remains appropriate for the authored ramp.
