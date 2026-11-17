# Crowd Punch: 80-level campaign proposal

Date: 2026-10-10. Revised: 2026-11-17. The roster and reinforcement revision is implemented across all nine chapters. Encounter counts and cadence are starting values for playtesting.

Source: `C:/Users/pc/Downloads/Crowd_Punch_80_Level_Campaign_Plan.md` plus current repository design, configuration and authoring inspection. The user explicitly selected full-table review before Chapter 1 implementation, then requested Chapters 2 through 8 in sequence. All 80 levels and their campaign infrastructure are authorized and implemented.

## Approved variety and reinforcement revision (2026-11-16, LOOP-011)

The user approved the three-shooter plan, then requested Chapters 1 through 9.
This section supersedes conflicting composition and cadence rules in the original
proposal and the 2026-10-14 revision. The Chapter 1–8 tables reflect the revised
assets.
Level 81 follows FINALE-001..007 and the authored roster in [Final Encounter](FinalEncounter.md#crowd-and-ammunition).

Preserve all 81 stable IDs, existing objectives, boss order, save/unlock behavior,
boss arena geometry, track routes and barricade room chains. Ordinary enemy stats
remain shared. Replace Baselines with meaningful combinations, reducing total counts
where specialist pressure warrants it. Baseline percentages below apply to mixed
wave-clear encounters, excluding introductions and ammunition-dependent support.

| Chapter | Enemy integration and encounter emphasis | Baseline target | Static hazard target |
|---|---|---|---|
| 1 / 1-10 | Mushroom Shooter at 5, returning at 8/9. Preserve fundamentals at 1-4, target objectives and Gatekeeper. Timed practice at 2/8/9. | 65-75% | None; cover and firing lanes first. |
| 2 / 11-20 | Preserve Exploder 11 and Dasher 14; introduce Alien Spike at 15. One Spike in 17 support; alternate Spike/Exploder and Shooter/Dasher groups at 18; 19 stays gentle. | 55-65% | None; retain hazard introduction order. |
| 3 / 21-30 | Preserve Armor, Elite, defense and ground introductions. Add familiar Shooters/Spikes to 22/28; keep 25/27 focused on their hazard lessons. | 50-60% | Introduce permanent at 25, periodic at 27; about 3 patches at 28. |
| 4 / 31-40 | Preserve Wizard 31 and Trail 33. Shooter/Trail at 35; varied established normals at defense 36. Rotate Shooter/Armor, Spike/Exploder and Wizard/Trail at 38. | 45-55% | 2-3 in pressure encounters 35/36, 4 at 38; 39 stays quiet. |
| 5 / 41-50 | Preserve LinkedPair 41. Sporella introduction replaces 43's current mix; Sporella/Armor/LinkedPair practice at 45, varied defense at 47, all three shooter roles across groups at 48. Retain relief at 46/49. | 40-50% | 2-3 in pressure encounters, 4-5 at 48. |
| 6 / 51-60 | Preserve Dragon 52. Modest Shooter/Sporella support at 53/54. Dragon/Sporella then Spike/Exploder at 55; alternate combinations at 58; 59 stays light. | 40-50% | 3-4 in pressure encounters, 5-6 at 58; clear rail corners. |
| 7 / 61-70 | Preserve Snake 61. Shooter/Trail at 63; Snake/Sporella then Spike/Exploder at 65; limited Shooter/Dragon at 67. Change dominant threats between sectors at 68. | 35-45% | 3-5 in pressure encounters, 6 at 68; retain tail and rail access. |
| 8 / 71-80 | Preserve Ninja 71. Shooter support at 72; Ninja/Sporella and Armor/Spike at 73; distinct normal mixtures at defenses 75/77. All archetypes across successive groups at 78; keep 76/79 as relief. | 35-45% | 4-5 in pressure encounters, 6-8 across 78; connected safe routes. |
| 9 / 81 | Include the three shooters in staged specialist supply by substitution within existing 37/50/66 physical-body budgets. Preserve reliable Baseline/Exploder anchor and downing ammunition. | Objective-dependent | No additional blanket ground danger around anchors. |

First encounters retain one clear-to-advance checkpoint and no static hazards:

- Mushroom Shooter, 5: **6B+1M > 6B+2R+1M**. Read committed warning, cover clipping and the player-owned piercing launch beam.
- Alien Spike, 15: **6B+1AS > 6B+2X+2AS**. Read radial volleys and exploit impact bursts beside explosive targets.
- Sporella, 43: **6B+1SP > 8B+2SP**. Read fixed sweeps, redirect shots and discover launch emission.

M = Mushroom Shooter, AS = Alien Spike, SP = Sporella. Existing later introductions
41/52/61/71 and their chapter recurrence remain; Sporella at 43 replaces that one
LinkedPair/Elite mixture without removing either archetype from the chapter.

### Reinforcement cadence and completion

Default to a finite reinforcement schedule followed by one cumulative survivor clear.
After Chapter 1, target 75-85% timed intermediate transitions. Retain full-clear
checkpoints for introductions and selected Elite encounters to contain replenishment.

| Encounter | Starting cadence |
|---|---|
| Early mixed combat | 4-6 enemies every 5-7s |
| Middle mixed combat | 5-7 every 4-6s; alternate directions |
| Late mixed combat | 6-8 every 4-5s; distribute specialists |
| Large arenas | 8-12 every 4-6s, distinct composition phases, at most one midpoint clear checkpoint |
| Defense | Keep level 24 clear-gated; later levels use finite timed reinforcement and final resolution of all attackers |
| Objectives/bosses | Bounded objective-owned supply, smaller initial batches and staggered replacement; victory does not require survivor cleanup |

Keep breach rules, no Elites in defense, armor/Wizard ammunition, and shared boss and
objective eligibility. Intermediate Wizard-zone waits must not accidentally turn a
timed reinforcement into a full clear; final gates retain the applicable rules.
Check Elite supply across overlapping survivors. Budget concurrent bodies including
LinkedPair endpoints and Snake tails. No new adaptive spawn director is required.

Increase distinct hazardous areas rather than expanding every patch. Use flanks,
optional shortcuts and launch opportunities, stagger periodic offsets, and preserve
safe spawning, clear objective shots and connected safe routes at every phase.
Wizard zones, Trails and flames consume the same readability budget. Keep barricade
final-sector geometry/hazards under BARRICADE-009, and do not use bosses or breathers
to apply the ordinary-pressure patch targets indiscriminately.

### Chapter 1 exact authored cadence

All 19 existing wave asset GUIDs, spawn regions and scene geometry are retained.
Levels 1/3/5 retain clear checkpoints. Levels 2/8/9 advance on time, even with surviving
earlier enemies; their final phase waits for **all current and previous** survivors.
The initial delay is 3s. Batches are 4 every 6s (8 every 6s at level 8). Between timed
phases, wait 6s after the previous final successful batch, then use zero next-phase
delay. Clear-gated phases retain a 3s next-wave delay. Placement rejection may delay
a batch safely. Objective/boss initial support uses 4/6s and existing per-body 4s
replacement, with unchanged bounded populations and completion ownership.

Level 8 retains 40/60/76/94 enemies per phase, 270 total: **186B+56R+28M** (68.9%
Baseline, formerly 94.8%). Eight-body batches yield 35 batches; with unobstructed
placement the last arrives around 207s plus fixed-step overhead. That is a schedule,
not a measured clear time or survival objective. Playtest fast-clear empty intervals,
slow-clear accumulation, shooter readability and the 3.5-4.5 minute target separately.
Level 9 is reduced from 23 to 17 enemies, with exactly one Mushroom Shooter.

### Chapter 2 authored cadence

Levels 11, 14 and 15 keep a full-clear checkpoint between their introduction and
practice groups. Their first group starts after 3s and the next starts 3s after
clearance. Batches are 4 every 5s. Initial objective and boss support uses the
same 4/5s cadence and the existing 4s replacement rules, including the paired
Exploders at 12. Level 17 replaces one Baseline with one Alien Spike and retains
two Exploders. Level 19 retains its bounded 8B+2X supply.

Level 18 delivers 264 enemies in 48/56/72/88 allocations: 160 Baselines (60.6%),
30 Exploders, 16 Alien Spikes, 16 Ranged, 26 Dashers and 16 Mushroom Shooters.
The first batch starts after 3s, with up to 8 enemies every 6s. The next phase
starts 6s after the previous phase's last successful batch, even if it has
survivors. Final clearance includes every surviving attacker. With clear spawn
regions, the 33rd batch arrives around 195s plus fixed-step overhead. The target
is still 3.5–4.5 minutes, subject to playtesting of overlap and clear times.

### Chapter 3 authored cadence

Levels 21 and 23 retain the Armor and Elite introduction checkpoints. Level 24
retains its three clear-gated defense waves, four enemies every four seconds, and
first-breach failure. Levels 25 and 27 keep familiar rosters and a single patch
each. The second phase begins eight seconds after the last successful batch of
the first phase and final clearance counts all survivors. Other compact levels
use initial batches of four every five seconds. Level 22's two rooms hold
7B+1X and 7B+1R+2X+1D+1A+1M+1AS, respectively. Each begins its own supply on
entry and follows the existing bounded replenishment rules.

Level 28 delivers 264 enemies including three Elites, with 144 Baselines
(54.5%). Spawn up to eight enemies every six seconds. Phases 1 and 3 advance
six seconds after their final successful batch. After phase 2, clear all active
enemies to drain its Elite's replenishing crowd, then wait the existing three
seconds before phase 3. Phase 4 waits for survivors from the current and earlier
phases. Armor ammunition remains available across the first overlap and through
the final clear. One outer hot pocket and two staggered warning crossings remain
in use. The crossings sit in the southwest and north approaches with at least
6.5m between each footprint and the nearest island. Their offsets reset at each
wave boundary under GROUND-001.

### Chapter 4 authored cadence

Levels 31 and 33 teach Wizard zones and Trails in clear-gated groups. The Wizard
introduction retains its ammunition and persistent-zone checks. Level 35 holds
11 Baselines, one Trail and one Mushroom Shooter around the moving target, with
two 1.5m radius periodic pockets. Level 36's 14/18/20 finite groups arrive in
batches of four every four seconds. The second and third groups each start six
seconds after the preceding final batch. A single active breach fails the
attempt and victory resolves all remaining attackers. Two periodic 3 x 6m
patches leave an open route to the protected zone. No Elite or Wizard
ammunition enters this defense encounter.

Level 38 has 264 enemies including two Elites and 132 Baselines (50%). Each
phase arrives in batches of eight every six seconds. Phases 1 and 3 advance
six seconds after their last successful batch. Phase 2 requires a full clear
and a three-second pause before phase 3. Its armor supply covers survivors
from phase 1. Wizards appear in the final phase, with the existing
ammunition and persistent-zone gate. The last clear includes earlier
survivors. Four periodic 3 x 8m strips sit on open floor with a connected
central route and offsets of 0/2/4/6s. Levels 35/36 use offsets of 0/4s.
All new patches have at least four metres of clearance from solid islands
and use the standard 4s inactive, 1.5s warning and 2.5s active timing.

Other encounters start with batches of four every five seconds and retain
their existing objectives and support caps. The chapter keeps the
level 39 breather and the Dino pillar-only victory.

### Chapter 5 authored cadence

Level 43 introduces Sporella with Baselines and existing cover. Both phases
arrive in batches of four every five seconds. Clear the first phase to get a
three-second pause before the second. Level 45 has 58 allocated enemies,
including six LinkedPair endpoints and one Elite, with 27 Baselines (46.6%).
It releases batches of six every five seconds. Each reinforcement phase starts
six seconds after the preceding phase's last successful batch. Every phase
retains the armor ammunition safeguard, and the final Elite's supply must be
cleared. The original west strip and a southeast strip use 0s/4s offsets.

Level 47 has finite groups of 16/20/22 in batches of four every four seconds,
with each reinforcement starting six seconds after the preceding phase's last
successful batch. Any breach fails the attempt. Complete the encounter by
clearing all remaining attackers. Two 3 x 6m warning strips on the southern
approach use 0s/4s offsets and leave a direct route to the protected zone.

Level 48 has 320 allocated enemies (144 Baselines, 45%) across five phases,
including 28 LinkedPair endpoints and two Elites. It releases up to eight
enemies every six seconds. Phases 1, 2 and 4 advance six seconds after their last
successful batch. Phase 3 clears all survivors and the Elite supply, then
pauses three seconds before phase 4. The final clearance includes earlier
survivors and persistent Wizard zones. The two permanent outer pockets and
three periodic 3 x 8m strips leave routes between the side basins. The periodic
strips use offsets of 0s, 2.67s and 5.33s. All new Chapter 5 strips have at least
four metres of clearance from the nearest authored solid island.

Level 49 is a smaller pre-boss breather, with four enemies every five seconds
and one Wizard-zone clear checkpoint. Levels 42, 44, 46 and 50 retain their
bounded objective support. Level 41 retains the original isolated LinkedPair
introduction. The existing level 42 asset uses three hits per gate, while the
BARRICADE-009 design default and the level's opening hint specify four.

### Chapter 6 authored cadence

Level 52 retains the isolated Dragon introduction. Levels 53 and 54 each
substitute one specialist for one Baseline, retaining 16 support enemies,
batches of four every 1.5s and bounded objective replacements. Level 54's
three 3 x 4m strips sit on the approach to turns 2, 3 and 6, lined up with
the next leg. Their near edges are four metres from the rail centreline.
Their 0/2/4s offsets vary which shot setup is under pressure, with room
beside each strip and clearance for the carrier and socket.
Levels 51, 56 and 60 retain their objective and boss support. Level 56's saved
gate asset requires three hits per gate. BARRICADE-009 specifies four as the
design default.

Level 55 sends 66 allocated enemies across three banks, including 30 Baselines
(45.5%) and four LinkedPair endpoints. Batches of six arrive every five seconds.
The first two phases advance six seconds after their last successful batch.
Armor ammunition, Wizard ammunition and persistent Wizard-zone clearance apply
to the final phase. Three 3 x 6m warning strips leave routes on either side of
the central strip.

Level 57 releases finite groups of 16/20/22 in batches of four every four
seconds. A single breach fails the attempt, and victory requires clearing every
remaining attacker. No Elite or replenishing support enters the defense.
Three 3 x 6m strips leave an open route to the protected zone. Levels 55 and
57 stagger their strips at 0/2.67/5.33s.

Moving Batteries (58) has 328 allocated enemies across five phases, including
146 Baselines (44.5%), 28 LinkedPair endpoints and two Elites. Batches are eight
every six seconds. Clear the crowd and Elite supply after phase 3, then wait
three seconds before phase 4. The final phase clears all survivors and persistent
Wizard zones. Five staggered 3 x 8m strips use offsets of 0/1.6/3.2/4.8/6.4s and
leave connected cross-court routes. The new Chapter 6 strips have at least four
metres of clearance from solid islands and stay clear of the rail and its corners.

Level 59 keeps a familiar, hazard-free breather with 20 enemies across two
phases and batches of four every five seconds. At 57, 58 and 59, timed phases
advance six seconds after the last successful batch with zero next-phase delay.

### Chapter 7 authored cadence

Level 61 retains the isolated Snake introduction with one six-segment tail
per clear-gated wave. Levels 63, 65 and 67 introduce timed groups from their
existing banks in batches of up to six every five seconds. The next group
arrives six seconds after the previous group's last successful batch.
Level 63 combines Mushroom Shooters and Trails with LinkedPairs and a later
Dragon. Level 65 opens with Snake and Sporella, switches to Alien Spike and
Exploders, then brings in Armor and Wizards with their existing safeguards.
Level 67 opens with one Mushroom Shooter and one Dragon, then brings in Armor,
another Dragon and one Snake. Each encounter has three 3 x 6m strips at
0/2.67/5.33s offsets and retains bypass space around its islands.

Shifting Front (68) allocates 328 enemies for clearance across six sectors,
including two Elites and 28 LinkedPair endpoints. Three Snakes contribute
18 attached tail segments, for 346 physical bodies across the encounter.
There are 133 Baselines, or 40.5% of clearance enemies and 38.4% of physical
bodies. Up to eight enemies arrive every six seconds. Phases 1, 2, 4 and 5
advance six seconds after their last successful batch. The midpoint waits
for the Elite group and earlier survivors to be cleared, followed by a
three-second pause. The final clear includes all survivors and persistent
Wizard zones. Armor ammunition applies at the Elite checkpoint and at the
finish. Six 3 x 6m warning strips stagger at one-sixth intervals of the
eight-second cycle. They sit in the approaches between banks and the central
cross, at least four metres from rock footprints. Open lanes around the
strips leave space to circle the Snake heads and reach the tails.

Levels 62, 64, 66, 69 and 70 retain their objective layouts and bounded
support. Level 62's shared gate asset requires three hits per gate.
BARRICADE-009 specifies four as the design default. Level 69 keeps the
hazard-free pre-boss track route.

### Chapter 8 authored cadence

Level 71 retains the isolated Ninja introduction. Level 72 replaces one
Baseline with one Mushroom Shooter in its 16-enemy bounded support, maintaining
batches of four every 1.5s. Level 73 opens with Ninja and Sporella, follows with
Armor and Alien Spike, and brings in Snake and Dragon for the final group.
Its 66 enemies and six tail segments include 27 Baselines (37.5% of physical
bodies). It uses batches of six every five seconds and carries the armor
safeguard through the final phase.

Levels 75 and 77 use different finite groups of 18/20/24 and 20/22/24,
respectively, in batches of four every four seconds. Level 75 moves from
Ranged and Exploders to Armor and Dashers, then a Trail group. Level 77
opens with Armor and Exploders and closes with Ranged, Dashers and Wizards.
One breach fails the attempt and the final clear resolves every attacker.
The existing armor fallback stays eligible in subsequent phases while
protected armor survives. These defenses exclude Elites, Wizard ammunition
and persistent-zone completion waits.

Claim the Ground (78) allocates 336 enemies over six phases, including 129
Baselines (38.4%), two Elites and 32 LinkedPair endpoints. Three Snakes add
18 tail segments, for 354 physical bodies across the encounter. All 15 enemy
archetypes appear across the six phases. The shared cap remains two concurrent
Ninja ambushes. It uses batches of eight every six seconds. The midpoint after
phase 3 clears all survivors and Elite support, then pauses three seconds.
The final phase clears all survivors and persistent Wizard zones. Armor
ammunition applies at the midpoint and final gates.

Timed transitions at 73, 75, 77, 78 and 79 start six seconds after the last
successful batch of the previous phase. Level 79 has two light groups of ten,
delivered in batches of four every five seconds. Level 76 retains its familiar
shell objective. Levels 74 and 80 retain their layouts, hazards and objectives.
Level 74's saved gate setting is three hits, with four as the BARRICADE-009
design default.

Level 73 has four 3 x 4m strips. Levels 75 and 77 each have four 3 x 6m strips
placed off the direct route to the zone. All three levels stagger their strips
at 0/2/4/6s. Level 78 has seven 3 x 6m strips staggered evenly across eight
seconds, with one in the central crossing and six in the approaches across
all four sectors, including the outer northeast and southwest. Its central
bypass connects the combat areas and gives room to circle Snake tails. New
and repositioned strips preserve at least four metres from the rock
footprints and stay clear of spawn banks.

### Chapter 9 authored supply

Bring Down the Sky (81) adds one Mushroom Shooter in health stage 1, one Alien
Spike in stage 2 and one Sporella in stage 3. The three substitutions use existing
specialist slots. Earlier slots remain active in each later stage. Including
the LinkedPair and six Snake tail segments, the stage caps are 37/50/66 physical
bodies. Each anchor bank retains its Baseline/Exploder supply of 4/1, 6/1 and
8/2 per successive stage. The same spawn banks and replenishment clocks serve
both flight and downing. Victory ends supply and clears the new shooter effects.

### Implementation and remaining work

Chapter 1 uses existing batching, timed activation, interleaved exact profile allocation,
cumulative defeat accounting and Mushroom beam lifecycle. The chapter recipe and
in-place revision command update saved waves and hints together. No enemy stats,
legacy assets, later chapter assets, catalog IDs, scenery or lighting are changed.
Chapter 2 uses the same timed and cumulative wave modes at 18. Alien Spike shots
remain active across timed transitions during the same run. Clear checkpoints,
completion, retry and unload expire them.
Chapter 3 uses the same clocks for 25/27/28, including a cumulative checkpoint
at 28 between the Elite encounter and the next timed group. Level 22's two
shooters follow the barricade room bounds for pursuit and attack starts.
Verification is recorded for [Chapter 1](../Verification/Chapter1.md),
[Chapter 2](../Verification/Chapter2.md), [Chapter 3](../Verification/Chapter3.md),
[Chapter 4](../Verification/Chapter4.md), [Chapter 5](../Verification/Chapter5.md)
[Chapter 6](../Verification/Chapter6.md), [Chapter 7](../Verification/Chapter7.md)
and [Chapter 8](../Verification/Chapter8.md). Level 81 verification is recorded
in [Final Encounter](../Verification/FinalEncounter.md).

Actual duration, overlap balance and OQ-001 target-hardware acceptance remain
unverified design tuning. The relevant chapter verification notes record the
controlled Editor checks.

## Reading the table

- B = Baseline, R = Ranged, X = Exploder, D = Dasher, E = Elite, A = Armored, W = Wizard, T = Trail, M = Mushroom Shooter. Numbers are exact proposed spawn counts, not weights. Elite counts are additional to normal-enemy totals.
- LP = LinkedPair (counted in pairs, two independently defeated endpoints per pair), F = Dragon (flame; D remains Dasher), S = Snake (counted in heads, not tail segments), N = Ninja. Each S starts with the shared settings asset's current six-segment tail: seven physical bodies initially, but only the head counts toward wave clearance. LP endpoints are additional to the ordinary normal-enemy count through `linkedPairCount`; F/S/N use ordinary exact profile counts. The tether record is not another enemy. See the 2026-10-14 revision below for population accounting.
- `>` separates clear-gated phases and `~>` marks timed phases that can overlap survivors. Chapters 1–8 follow their authored cadence above. Directions refer to safely inset spawn rectangles. Final wave-clear completion includes all earlier survivors.
- `Hold` means one bounded replenishing support population until the objective completes, with a proposed 4-second replacement delay. Counts describe the initial/target population, not an unlimited simultaneous crowd. Existing specialized replenishment rules take precedence; in particular Shell replaces its two Exploders only after both are gone, and stops replacing them once the shell breaks.
- `Batch n/s` means batches of n enemies every s seconds within each finite phase. Unrevised defense assets retain their 5-second cleared-wave pause; LOOP-011 changes later campaign defense when implemented.
- Dimensions are approximate playable width x length in metres, except the eight large wave arenas, which each require a 100 x 100m playable footprint. North is the objective/far edge; the player generally enters from the south. All non-boss arenas receive distinct authored floor/boundary/obstacle arrangements using the existing nature kit. Defense arenas retain the established 32 x 96m size and 8 x 4m end zone, with distinct approach geometry.
- `G1` = one always-active circular patch, radius 2.5m. `G2` = two always-active 3 x 6m rectangles. `P1` = one periodic 3 x 8m strip. `P2` = two such strips with staggered offsets; `P3` = three. Periodic starting tune: inactive 4s, warning 1.5s, active 2.5s, with evenly staggered offsets. Damage/intervals retain the existing hazard defaults. Every layout retains safe routes and safe spawning throughout the cycle; strips never seal all routes.
- Hazard column lists added static patches. Enemy-created Wizard zones/Trail sections, Dragon flames, LinkedPair tethers and boss attacks remain present whenever those enemies/bosses appear, even when static hazards are `None`.
- Durations are playtest targets, never time limits or minimum enforced times. Counts, dimensions and timings are reviewable starting values, not measured results.

## Campaign shape

Accepted gate-width amendment (2026-10-10, BARRICADE-006, revised by the user): keep Chapter 1's introductory barricades wide; every break-and-exit barricade from level 13 onward is 2m wide, with solid terrain sealing both sides to the perimeter so the exit is accessible only through the gate. The later BARRICADE-007 amendment below extends the affected arenas into room chains.

There are exactly 80 proposed levels: eight bosses, 36 wave-clear levels and 36 specific-objective levels. Chapters 1-4 introduce the original eight archetypes, every objective, static hazard activation mode and boss. Chapters 5-8 each introduce one newly implemented enemy while remixing familiar rules and retaining earlier introductions. Each chapter has exactly one 100 x 100m arena with a substantially larger enemy crowd and wave clearing as its only objective, at levels 8, 18, 28, 38, 48, 58, 68 and 78. All eight target more than three minutes: 3.5-4.5 minutes in Chapters 1-4 and 4-5 minutes in Chapters 5-8. This replaces the earlier proposal for only three long non-boss encounters. Other non-boss levels mostly target 1-2 minutes, with shorter introductions and a breather immediately before each boss. Boss introductions target 2-3 minutes; rematches 3-5 minutes.

Accepted amendment (2026-10-10): every chapter must contain one 100 x 100m level with a larger crowd where the player only clears waves, targeting longer than three minutes. Exact placement, compositions, wave counts and target ranges below are proposed tuning. These arenas are explicit exceptions to the compact-level default in LOOP-002. Their duration must come from sustained crowd combat, not forced survival timers, long spawn delays or walking across empty space. Spawn concentrated groups from safely inset banks around broad connected combat areas; preserve room for chains and repositioning. Finite waves clear before the next begins. Large waves spawn together unless the table says otherwise, so these are larger simultaneous crowds as well as larger total encounters. Ordinary enemy stats remain unchanged. Profile the highest concurrent population and navigation over the full footprint; OQ-001 hardware/FPS targets remain unresolved.

Protected-zone levels exclude Elites (PROTECT-001, user clarification 2026-10-10). The former Elite entries in levels 47, 57, 75 and 77 are removed without replacement; other wave counts remain unchanged.

The boss order moves from choosing exposed body-shot angles, to returning mobile projectiles, to reading protected movement cycles, to baiting a pursuing boss into falling pillars. Each introduction and rematch preserves its corresponding legacy arena geometry: Gatekeeper from Gauntlet_11, Chicken from _20, Rolling Blob from _21, Dino from _22. Rematches change existing settings/support/hazard placement only.

Introduction order: fundamentals (1-2), Ranged (3), barricade (4), Mushroom Shooter (5), rotating cover (7), Gatekeeper (10); Exploder (11), shell (12), Dasher (14), Alien Spike (15), track (16), Chicken (20); Armored (21), Elite (23), defense (24), always-active ground (25), periodic ground (27), Rolling Blob (30); Wizard (31), Trail (33), Dino/pillars (40); LinkedPair (41), Sporella (43), Dragon (52, after the familiar shell breather at 51), Snake (61), Ninja (71). New shooter introductions follow the exact recipes above; the original four later introductions retain Baseline-only support. Wave-clear peaks alternate with smaller precision objectives and recovery encounters.

## Four-enemy campaign revision (2026-10-14)

The user specified the introduction chapters and continued appearances in later chapters, reviewed this update before project changes and then authorized implementation. The following exact placements and counts are now authored starting tuning. This revision changes 15 existing wave-clear encounters and 48 wave assets; it adds no levels or objectives. Chapter builders, saved waves and opening hints are updated together. See [verification](../Verification/CampaignEnemyRevision.md) for actual checks and remaining playtests.

| Enemy / requirements | First campaign encounter | Chapter 5 appearances | Chapter 6 appearances | Chapter 7 appearances | Chapter 8 appearances |
|---|---|---|---|---|---|
| LinkedPair / LINK-001..007 | 41: 6B+1LP > 12B+2LP | 41, 45, 48 | 55, 58 | 63, 65, 68 | 73, 78 |
| Dragon / DRAGON-001..007 | 52: 6B+1F > 12B+2F | Not introduced | 52, 55, 58 | 63, 65, 67, 68 | 73, 78 |
| Snake / SNAKE-001..007 | 61: 6B+1S > 10B+1S | Not introduced | Not introduced | 61, 65, 67, 68 | 73, 78 |
| Ninja / NINJA-001..006 | 71: 6B+1N > 12B+2N | Not introduced | Not introduced | Not introduced | 71, 73, 78 |

Each first encounter isolates the new behavior. Level 41 teaches shared launch, tether swings and solid-cover protection. Level 52 retains two solid islands with broad flanking routes so the moving flame warning and cover blocking can be read before Dragon joins Wizard/LinkedPair mixtures at 55. Level 61 provides room to circle the protected head and approach the currently vulnerable tail tip; only one Snake appears at once in either introductory wave. Level 71 teaches preparation, safe rear arrival, committed dash and interruptible recovery with one Ninja, then two. Later level 78 can contain three Ninjas, while NINJA-004's shared maximum of two concurrent ambushes remains unchanged.

The large encounters at 48/58/68/78 consolidate respectively one/two/three/four new enemy types. Their waves vary the combinations rather than requiring every learned enemy in every wave. Other follow-ups build from familiar threats: paired endpoint bodies help create launch chains, Dragons reward cover and redirection, Snakes demand repeated exposed-tip shots, and Ninjas add interruption and repositioning decisions. Tethers and flames retain their existing restrictions against intact armor, protected Snake parts, bosses and puzzle targets; they do not acquire new damage privileges for campaign combinations.

Outside the four introduction levels, replace Baselines at two per LP, one per F/N and seven per S (one head plus the current six orbs). This keeps each revised wave's initial physical-body population equal to its previous population, including fixed Elites and attached Snake orbs. Pair records are excluded. The four large arenas retain initial peak populations of 128, 138, 138 and 148 physical bodies respectively, before defeats or ammunition supplies. Clearance totals can be lower because attached orbs do not count as independent enemies. If the shared Snake segment count changes, recheck these budgets rather than silently assuming seven bodies per head. These are population comparisons, not evidence of equal performance or duration: shared pair constraints, tail physics, flame queries and teleports have different costs.

Keep the existing level names, objective allocation, static hazard schedules, boss support, defense waves, room chains, track routes and pre-boss breathers. The only layout clarifications are paired spawn clearance at 41, solid Dragon cover/flanking at 52, Snake rear-tail space at 61 and connected Ninja arrival space at 71. Existing floors and obstacles should satisfy those requirements where possible; verify actual navigation and physical clearance before changing geometry. For this revision the four new types appear only in finite wave-clear encounters. Their player-focused behaviors can be taught and mixed there without changing objective replenishment or defense targeting. This is an encounter-placement choice, not a permanent prohibition on future objective combinations.

Proposed brief opening hints, using the existing hint system without stopping play or adding HUD:

- 41: "Punch either linked enemy to launch both. Keep clear of the tether, even while they recover."
- 52: "Use rocks to block the flame. Launch a Dragon to sweep its fire through the crowd."
- 61: "Launch the highlighted tail tip. Strip each segment, then attack the exposed head."
- 71: "Watch the Ninja prepare and appear behind you. Move aside or interrupt, then strike during recovery."

The revision updates the chapter builders and their generated campaign-owned waves together, with exact profile guarantees and the separate LinkedPair count. Shared enemy settings and the 80 stable campaign identifiers, saves, unlock order and build registration are preserved (LOOP-002..009). Legacy Gauntlet_24..27 remain unchanged and available only through Level Play; their original legacy-only implementation instructions do not prohibit this separately requested campaign placement. GDD.md records that additional campaign scope under LOOP-009, retaining SNAKE-007, NINJA-006, DRAGON-007 and LINK-007's legacy encounter recipes and all gameplay rules.

Verification for implementation: check no early appearances and every recurrence listed above; exact pair endpoint/head clearance; atomic safe pair placement and Snake tail construction; correct prefab/settings baking; shared launch, collision propagation, cover blocking, tail-tip protection, Ninja interruption/concurrency and recovery; retry/unload with no orphan tethers, tails, flames or ambush reservations; and unchanged objective/legacy/save behavior. Preserve armor ammunition and Wizard-zone gates where already required. Dragon flame, detached Snake orbs and pair records add no new completion wait. Profile the four revised large arenas including tails and paired physics, and playtest all four introductions and mixed follow-ups. The existing duration targets remain provisional, especially where Snake clearance counts fall; tune campaign composition after playtesting rather than changing shared enemy stats or adding forced delays. OQ-001 hardware/FPS targets remain open.

## Chapter 1: First Impact

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 01 | First Line / clear waves | 24 x 30 open rounded court; far-centre then two far banks | 3B > 6B > 8B | None | Low-pressure aiming and body chains; fundamentals available immediately | 45-75s |
| 02 | Choose the Angle / clear waves | 28 x 32 shallow trapezoid; one offset rock island; alternating north-west/north-east banks | 8B ~> 12B; batch 4/6s | None | First timed reinforcement; move around cover and dash to reposition | 60-90s |
| 03 | Across the Court / clear waves | 30 x 36 elongated hexagon; two small side rocks; north then east bank | 6B+1R > 10B+2R | None | Introduce ranged pressure with nearby launch ammunition | 75-105s |
| 04 | Break Through / break barricade and reach exit | 24 x 38 funnel with broad rear court; north exit; lateral support rectangles | Hold 8B; 3 barricade hits | None | Low-pressure deliberate shots at a solid target; cross the exposed exit | 60-90s |
| 05 | Crossfire / clear waves | 34 x 32 offset diamond; two separated cover islands; west then east bank | 6B+1M > 6B+2R+1M; batch 4/6s | None | Mushroom Shooter introduction: dodge the locked beam and launch a piercing counterattack | 75-105s |
| 06 | Open the Lane / break barricade and reach exit | 28 x 40 bent perimeter, short central divider with broad side access; side-bank support | Hold 10B; 3 hits | None | Easier precision encounter; find a useful shot around an obstacle | 60-90s |
| 07 | Through the Opening / destroy rotating-cover target | 34 x 34 octagon; open ring around enclosure; south/west support | Hold 8B; 3 hits; 100-degree opening, continuous 20 degrees/s | None | Introduce opening timing and dangerous returned bodies | 75-105s |
| 08 | Changing Sides / clear waves | 100 x 100 broad court; two offset rock islands, connected open combat areas; north, west, east, north-east banks | 28B+8R+4M ~> 42B+12R+6M ~> 52B+16R+8M ~> 64B+20R+10M; batch 8/6s | None | Sustained mixed reinforcement; launch Shooters through crowds and change firing angles | 3.5-4.5min |
| 09 | One Good Shot / clear waves | 30 x 30 fan-shaped court, one side alcove; two broad far rectangles | 6B+2R ~> 6B+2R+1M; batch 4/6s | None | Short familiar mixed breather before the boss | 60-90s |
| 10 | The Gatekeeper / defeat boss | Preserve Gauntlet_11 round court and boss route; safe interior crowd rectangle | Hold 10B; three existing boss stages | None | Body shots into exposed head, dodge committed hands; campaign warning tune below | 2-3min |

Chapter 1 opening hints, shown briefly without stopping play:

1. "Line up an enemy with the crowd, then punch. The short line shows the launch direction."
2. "Move around the rocks to find a clear angle. Reinforcements arrive from the other side; dash to reposition."
3. "Use nearby enemies to reach the shooters. Keep moving between shots."
4. "Launch bodies into the barricade, then reach the exit."
5. "Step out of the Mushroom Shooter's warning line. Launch it through the crowd to turn its beam against them."
6. "Find an angle around the divider and break through."
7. "Send bodies through the rotating opening. Watch for returned shots."
8. "Mixed reinforcements keep arriving from changing sides. Launch Mushroom Shooters through the crowd, then clear the survivors."
9. "One last small group is coming. Line up the Mushroom Shooter with the crowd."
10. "Launch enemies into the head. Dodge the hands and shoot through the openings."

Chapter 1 proposals specific to campaign-owned assets: Gatekeeper retains 300 health, existing three stages and damage/ownership rules. Change slam/lunge/sweep anticipation from current 0.1s to 0.8/0.8/1.0s; retain current recovery 2.8/2.8/3.0s initially. Use 1.5s exposed opening and 4s crowd replacement delay. Durations require playtesting; do not pad fights with waits or modify shared enemy stats. Level 7 cover keeps reflection multiplier 1.0, no hit response, and existing enclosure solidity/target eligibility. Barricades retain three hits and existing launch-source eligibility. Later boss/settings proposals below are starting tunes, not changes to legacy assets.

## Chapter 2: Moving Ammunition

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 11 | First Fuse / clear waves | 28 x 30 wide oval, no interior blockers; north banks | 6B+1X > 10B+2X | None | Gentle post-boss Exploder introduction; deliberate detonation position | 60-90s |
| 12 | Crack the Shell / destroy shell and core target | 32 x 32 clipped square, open central target; west/east support | Hold 8B plus specialized pair of 2X; 3 shell explosions; ordinary core health | None | Explosion-only shell, then a fresh attack on the exposed core | 75-120s |
| 13 | Blast the Gate / break barricade and reach exit | 30 x 42 split approach merging before north exit; side support | Hold 10B+2X; 3 hits | None | Reuse explosions against a familiar objective | 75-105s |
| 14 | Committed Charge / clear waves | 30 x 40 runway with wide side pockets; far end waves | 8B+1D > 12B+2D | None | Introduce committed Dasher direction in open space | 75-105s |
| 15 | Crossed Threats / clear waves | 36 x 34 kite shape, diagonal island; alternating side banks | 6B+1AS > 6B+2X+2AS, batch 4/5s | None | Read radial volleys, redirect spikes and launch a Spike into an Exploder group | 75-105s |
| 16 | Push the Block / knock into place | 32 x 38 open court, longitudinal rail, ample end access; side support | Hold 8B; 5 net forward hits | None | Breather: direction determines forward or backward progress | 75-105s |
| 17 | Moving Window / destroy rotating-cover target | 36 x 32 broad ring with two outside rock shelves; north/south support | Hold 9B+2X+1AS, 3 hits, 90-degree opening at continuous 25 degrees/s | None | Use Spike bursts on the crowd and send Exploders through the opening | 90-120s |
| 18 | Crowd Reaction / clear waves | 100 x 100 court with three widely separated rock clusters and long diagonal lanes; north, west, east, split north banks | 30B+12X+6AS ~> 34B+4R+12D+6M ~> 44B+18X+10AS ~> 52B+12R+14D+10M, batch 8/6s | None | Alternate explosive reaction groups with Mushroom Shooter lanes and Dasher pressure | 3.5-4.5min |
| 19 | Push from Behind / knock into place | 34 x 40 left-turn court; two 10m legs; clear corners and endpoints; outer side support | Hold 8B+2X; 10 net forward hits | None | Pre-boss breather: place explosions on the useful side of the block | 60-90s |
| 20 | Chicken Run / defeat boss | Preserve Gauntlet_20 open arena and solid perimeter | Hold 6B; existing single/paired shot stages | None | Return punchable shots while respecting dangerous rebounds | 2-3min |

## Chapter 3: Pressure and Space

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 21 | Peel the Armor / clear waves | 32 x 34 broad shooting court, one rear rock shelf; north banks | 8B+1A > 12B+2A; armor ammunition safeguard | None | Gentle post-boss armor introduction; body hits before direct launch | 75-105s |
| 22 | Heavy Traffic / break barricade and reach exit | 2 distinct ascending sectors, 30 x 80m total; final original paired islands; sealed 2m gates | Hold 7B+1X then 7B+1R+2X+1D+1A+1M+1AS on room entry; 2 gates x 3 hits | None | Open first approach builds into a mixed final crowd with both familiar shooters | Fresh pacing playtest |
| 23 | Borrowed Fist / clear waves | 36 x 36 wide court with unobstructed diagonal; far-bank support | 10B+1E > 14B+2R+1E | None | Elite introduction: read and disrupt an enemy-owned body shot | 90-120s |
| 24 | Hold the Line / protect point through finite waves | 32 x 96 lane, broad open middle, zone at south end; north spawns | 12B > 16B > 20B; batch 4/4s; first breach fails | None | Defense introduction; hit approaching bodies away from the zone | 90-120s |
| 25 | Hot Ground / clear waves | 34 x 34 rounded rectangle, off-centre hot island; north/east banks | 10B ~> 14B+2R, batch 4/5s | G1 centre-left | Read permanent ground danger, then change angles on timed ranged support | 75-105s |
| 26 | Safe Side / knock into place | 34 x 42 right-turn stair court; three 10m legs; clear corners and endpoints; outer side support | Hold 10B+2X; 15 net forward hits | G1 lower outer pocket | Use a hazard as an optional crowd opportunity without blocking the rail | 90-120s |
| 27 | Wait for the Warning / clear waves | 36 x 38 two broad lanes around short island; north then side banks | 10B+1D ~> 14B+2R+2D, batch 4/5s | P1 west lane | Read the periodic crossing, then use the safe lane against familiar reinforcements | 75-105s |
| 28 | Crowd Channels / clear waves | 100 x 100 court, two staggered long islands with broad end gaps; north, west, east, split side banks | 26B+10R+6A+6M ~> 30B+15R+4A+6M+1E > 40B+14X+12D+6AS ~> 48B+12R+10D+6A+10M+2E, batch 8/6s | G1 outer pocket and P2 southwest and north crossings at 0s/4s offsets | Break armor with bodies, stop the first Elite's replacements, then use Spike bursts on explosive groups | 3.5-4.5min |
| 29 | Window of Safety / destroy rotating-cover target | 38 x 36 ring court, asymmetric outer rocks; south/east support | Hold 10B; 3 hits; 100-degree opening, continuous 20 degrees/s | None | Pre-boss breather: a familiar target with generous shot windows | 60-90s |
| 30 | Rolling Blob / defeat boss | Preserve Gauntlet_21 arena, obstacles and broad lanes | Hold 6B; existing protected roll/vulnerable pause cycle | None | Read committed rolls, reclaim launched bodies and use pauses | 2-3min |

## Chapter 4: Living Hazards

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 31 | Purple Warning / clear waves | 34 x 34 open clipped court; far bank | 6B+1W > 12B+2W; Wizard safeguards | None | Gentle Wizard introduction; move out of zones, launch into casters | 75-105s |
| 32 | Quiet Delivery / knock into place | 42 x 42 left-turn stair court; four 10m legs; clear corners and endpoints; outer side support | Hold 10B; 20 net forward hits | None | Familiar precision breather before next introduction | 60-90s |
| 33 | Leave a Trail / clear waves | 36 x 36 open rounded diamond; far-bank waves | 6B+1T > 12B+2T | None | Trail introduction; redirect its path, avoid lingering sections | 75-105s |
| 34 | Break the Detour / break barricade and reach exit | 3 distinct ascending sectors, 32 x 132m total; final original detour; sealed 2m gates | Hold 8 > 11 > 14; final 9B+1R+2X+1D+1A; saved gate setting is 3 hits | Final original P1 only | Build pressure before the final warning shortcut | Fresh pacing playtest |
| 35 | Moving Perimeter / destroy rotating-cover target | 40 x 38 open ring, three outer pockets; alternating safe support regions | Hold 11B+1T+1M; 4 hits; 100-degree opening, continuous 25 degrees/s | Two small periodic pockets | Read the opening while the Trail and Mushroom Shooter change safe angles | 90-120s |
| 36 | Distant Defense / protect point through finite waves | 32 x 96 lane with two widely offset rock islands; north spawns | 10B+3R+1X ~> 11B+4D+3X ~> 12B+4R+3D+1W, batch 4/4s | Two 3 x 6m periodic strips along the approach | Intercept timed groups and resolve all remaining attackers | 90-120s |
| 37 | Blast Shelter / destroy shell and core target | 36 x 34 side-notched court, two rear rocks, open central target | Hold 12B plus specialized pair of 2X; 3 shell explosions | G1 far outer corner | Recovery encounter: known explosion objective and ample safe ground | 75-105s |
| 38 | The Crowd Moves / clear waves | 100 x 100 court with four shallow perimeter bays and open diagonal lanes; west, east, north, split side banks | 24B+12R+6A+6M ~> 28B+12D+6A+8M+2E > 36B+18X+12D+6AS ~> 44B+16R+10X+10D+3W+5T, batch 8/6s | Four staggered 3 x 8m periodic strips | Break armor, shut down Elite supply, then redirect Spike bursts and living hazards | 3.5-4.5min |
| 39 | Last Alignment / knock into place | 42 x 52 right-turn stair court; five 10m legs; clear corners and endpoints; outer side support | Hold 10B; 25 net forward hits | None | Pre-boss breather: place body shots into an environmental target | 60-90s |
| 40 | Falling Giants / defeat Dino with pillars | Preserve Gauntlet_22 arena and all three pillar positions | Hold 8B general support plus 2B per unconsumed pillar | None | Bait pursuit and topple pillars with player-owned bodies; 3 successful hits | 2-3min |

## Chapter 5: Better Angles

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 41 | Back to Bodies / clear waves | 34 x 32 open pentagon, one side rock crescent; far banks with room for both endpoints and broad flanking space | 6B+1LP > 12B+2LP | None | LinkedPair introduction: launch either endpoint to swing both; use cover against the dangerous tether | 75-105s |
| 42 | Outer Route / break barricade and reach exit | 4 distinct ascending sectors, 34 x 176m total; final original central island; sealed 2m gates | Hold 8 > 10 > 13 > 16; final 11B+1R+2X+1D+1A; 4 gates x 4 hits | One reduced periodic fore-sector patch; final original G2 | Grow into the final pressured outer route | Fresh pacing playtest |
| 43 | Turn the Shot / clear waves | 38 x 36 court with three widely spaced islands; west/east banks | 6B+1SP > 8B+2SP; batch 4/5s | None | Read Sporella's windup from cover, redirect a shot and launch Sporella to sweep shots through the crowd | 90-120s |
| 44 | Stop and Go / destroy rotating-cover target | 40 x 36 unequal outer bays around enclosure; side support | Hold 12B+2R+1X; 4 hits; 85-degree opening, rotate 3s at 30 degrees/s / pause 1s | None | Existing cover pause mode rewards planning a shot | 90-120s |
| 45 | Armor in Motion / clear waves | 38 x 40 clipped corridor with cross lanes; north/east/west banks | 8B+2A+1LP+2SP+2X ~> 9B+2A+1LP+2SP+3D+2R ~> 10B+2A+1LP+2SP+5R+1E; batch 6/5s, armor safeguard | Two staggered 3m-wide warning strips, 8m west and 6m southeast | Launch bodies to crack armor and turn Sporella's fire against the crowd | 90-120s |
| 46 | Measured Push / knock into place | 52 x 52 left-turn stair court; six 10m legs; clear corners and endpoints; outer side support | Hold 10B+2X; 30 net forward hits | None | Mid-chapter relief; control direction and rebound space | 75-105s |
| 47 | Divided Approach / protect point through finite waves | 32 x 96 lane, long island splitting north approach, merged south half | 10B+4R+2X ~> 12B+5D+3X ~> 13B+4R+3D+2X; batch 4/4s | Two staggered 3 x 6m approach strips | Intercept timed groups from the separated approaches and clear every remaining attacker | 90-120s |
| 48 | Firebreak / clear waves | 100 x 100 court with two broad side basins linked through a wide centre; north, west, east, split north, side banks | 22B+14X+6AS+2D+2LP ~> 25B+12R+8M+5D+3LP ~> 29B+8A+5SP+8X+4D+2R+2E+3LP > 32B+12D+12X+6SP+4AS+3LP ~> 36B+12R+8M+8X+4T+4SP+2W+3LP; batch 8/6s | G2 outer pockets and P3 staggered strips | Alternate Spike, Mushroom Shooter and Sporella pressure by bank, then clear the Elite checkpoint and reposition through the warning gaps | 4-5min |
| 49 | Make Room / clear waves | 34 x 34 open fan with two boundary outcrops; far bank | 8B+1W > 10B+2R; batch 4/5s, Wizard safeguards | None | Breather before Gatekeeper rematch | 75-105s |
| 50 | Gatekeeper Returns / defeat boss | Exact Gatekeeper arena geometry; safe interior support | Hold 12B+1R; at most one live non-Baseline; three stages | P2 in side sectors, clear central shooting apron | Harder positioning; 0.65/0.65/0.85s warnings, retain recoveries; 360 health | 3-4min |

## Chapter 6: Deliberate Reactions

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 51 | Fresh Fuse / destroy shell and core target | 34 x 36 broad oval with one empty rear bay; side support | Hold 10B plus specialized pair of 2X; 3 shell explosions | None | Low-pressure return to deliberate explosives after boss | 75-105s |
| 52 | Crossing Paths / clear waves | 38 x 40 S-edged court, two offset solid cover islands, broad flanking routes; west/east waves | 6B+1F > 12B+2F | None | Dragon introduction: flank the moving warning, use solid cover, then launch continuous flame through the crowd | 90-120s |
| 53 | Reverse the Window / destroy rotating-cover target | 40 x 40 unequal octagon, north rock shelf; east/west support | Hold 13B+2X+1M; 4 hits; 90-degree opening, 30 degrees/s, reversal every 5s | None | Read the cover reversal while avoiding the Mushroom Shooter's firing lane | 90-120s |
| 54 | Push Under Pressure / knock into place | 52 x 62 right-turn stair court; seven 10m legs; clear corners and endpoints; outer side support | Hold 13B+2X+1SP; 35 net forward hits | Three staggered 3 x 4m strips behind turns 2, 3 and 6 | Time body-shot setups through the warnings or approach from the side while reading Sporella sweeps | 90-120s |
| 55 | Disrupt the Casters / clear waves | 40 x 40 triangular-island court; north/east/west waves | 9B+1F+2SP+3R+3D+1LP ~> 10B+3AS+6X+3D ~> 11B+2A+1F+1LP+2M+2W+4R; batch 6/5s | Three staggered 3 x 6m warning strips | Redirect Dragon flames and Sporella shots, then use Spike bursts and Exploders as firing angles change | 90-120s |
| 56 | Clean Break / break barricade and reach exit | 5 distinct ascending sectors, 32 x 210m total; final original offset island; sealed 2m gates | Hold 8 > 9 > 11 > 12 > 14; final 9B+1R+2X+1D+1A; 5 gates x 3 hits in saved asset | None | Straightforward shots build into mixed final pressure | Fresh pacing playtest |
| 57 | Long Watch / protect point through finite waves | 32 x 96 three staggered approach islands, broad recovery area before zone | 10B+4R+2D ~> 12B+5D+3X ~> 12B+4R+4X+2T; batch 4/4s | Three staggered 3 x 6m side strips | Intercept timed groups and clear every attacker. First breach fails | 90-120s |
| 58 | Moving Batteries / clear waves | 100 x 100 court, three offset islands and wide cross-court shooting lanes; north, west, east, split sides, north banks | 21B+2F+6SP+9R+6D+2LP ~> 25B+8AS+14X+3D+3LP ~> 29B+8A+6M+8X+5R+2E+3LP > 32B+3F+5SP+11D+7X+8R+3LP ~> 39B+2F+4AS+8M+12X+10R+5D+2W+3LP; batch 8/6s | Five staggered 3 x 8m strips, including two outer crossings | Alternate flame and projectile pressure by bank. Clear the Elite group midway | 4-5min |
| 59 | Open Air / clear waves | 36 x 32 wide shallow court with boundary alcoves; far banks | 8B+2X ~> 8B+2R; batch 4/5s | None | Deliberate relief after endurance and before boss | 60-90s |
| 60 | Chicken Crossfire / defeat boss | Exact Chicken arena geometry, no new interior blockers | Hold 8B+2X; existing single/paired patterns | P2 narrow edge sectors, broad central safe space | Movement-leading shot aim; retain readable 0.8s wind-up, 1.5s shot spacing; 2100 health | 3-4min |

## Chapter 7: Control the Ground

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 61 | Stable Footing / clear waves | 36 x 34 open stepped rectangle; north banks with clear rear tail space and a wide route around the head | 6B+1S > 10B+1S | None | Snake introduction: launch the highlighted tail tip one segment at a time, then defeat the exposed head | 75-105s |
| 62 | Side Door / break barricade and reach exit | 6 distinct ascending sectors, 36 x 264m total; final original side approach; northeast 2m gates | Hold 8 > 9 > 11 > 12 > 14 > 16; final 11B+1R+2X+1D+1A; 6 gates x 3 hits in saved asset | Two reduced periodic fore-sector patches; final original P2 | Read broader earlier routes before the final side gate | Fresh pacing playtest |
| 63 | Draw a Line / clear waves | 42 x 38 three broad lanes around low rock islands; west/east waves | 7B+2M+3T+2R+1LP ~> 8B+2M+2T+4D+1F+1LP+1R; batch 6/5s | Three staggered 3 x 6m strips with central bypass | Leave Mushroom Shooter firing lanes before Trails close off retreats | 90-120s |
| 64 | Shell Island / destroy shell and core target | 38 x 38 central target, wide safe ring, four shallow boundary notches | Hold 14B plus specialized pair of 2X; 4 shell explosions | G2 in opposite outer pockets | Keep Exploders alive until they can reach the shell | 90-120s |
| 65 | Three Approaches / clear waves | 42 x 42 three-sided court with wide linking bays; north/west/east waves | 8B+1S+2SP+1LP+3R+4D ~> 9B+3AS+6X+4D ~> 10B+2A+1F+1LP+5R+2T+2W; batch 6/5s | Three staggered 3 x 6m approach strips | Reach the Snake tip through Sporella sweeps, then redirect Spike bursts into Exploder groups | 90-120s |
| 66 | Set the Pace / destroy rotating-cover target | 38 x 40 offset ring with broad southern bay; lateral support | Hold 12B+2X; 4 hits; 90-degree opening, 25 degrees/s, successful-hit pause 1s | None | Relief: exploit the existing hit-pause option | 75-105s |
| 67 | Keep It Moving / clear waves | 42 x 40 staggered islands, continuous outside lane; north/east waves | 7B+1M+1F+3R+3D+3X ~> 9B+1F+1S+2A+4D+3X; batch 6/5s, armor safeguard | Three staggered 3 x 6m strips at island approaches | Read the Mushroom Shooter's lane and the Dragon sweep before changing sides to reach the Snake tip | 90-120s |
| 68 | Shifting Front / clear waves | 100 x 100 four-bay court with widely separated islands and a broad central cross; north, west, east, south, split side, north banks | 16B+6M+6R+7D+1F+2LP ~> 19B+6AS+15X+4D+2LP ~> 23B+8A+6R+9X+2F+3LP+2E > 23B+2S+4SP+6T+10D+7X+2LP ~> 26B+1F+5SP+7M+5R+10D+6X+2LP ~> 26B+1S+2F+4AS+6X+5A+8R+4D+2W+3LP; batch 8/6s | Six staggered 3 x 6m approach strips with a broad central crossing | Change angles for each sector, clear the Elite checkpoint, then circle to exposed Snake tips | 4-5min |
| 69 | Straighten Up / knock into place | 62 x 62 left-turn stair court; eight 10m legs; clear corners and endpoints; outer side support | Hold 10B+2X; 40 net forward hits | None | Pre-boss recovery with deliberate directional hits | 60-90s |
| 70 | Rolling Pressure / defeat boss | Exact Rolling Blob arena and obstacle geometry | Hold 8B+2X; existing three stages | P2 in outer pockets, all broad boss lanes retained | Existing physical-reflection mode; 600 health; pause 2.5/1.8/1.2s, same roll speeds | 3-4min |

## Chapter 8: Crowd Mastery

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 71 | Find Your Feet / clear waves | 36 x 36 open tapered octagon; one far island; north then west bank, broad connected floor for safe rear arrivals | 6B+1N > 12B+2N | None | Ninja introduction: read preparation and rear arrival, reposition or interrupt, then punish recovery | 75-105s |
| 72 | Thread the Crowd / destroy rotating-cover target | 42 x 40 asymmetric ring with clear south apron; side support | Hold 13B+1W+1T+1M; 4 hits; 100-degree opening, 30 degrees/s, reversals every 6s | None | Time body shots through the reversing cover while reading the Mushroom Shooter's lane | 90-120s |
| 73 | Committed Lines / clear waves | 44 x 40 slanted two-island court; east/west/north waves | 8B+2N+2SP+1LP+3R+3D ~> 9B+2A+3AS+5X+3D ~> 10B+2A+1S+1F+1N+1LP+4T+3R; batch 6/5s | Four staggered 3 x 4m strips | Interrupt Ninja ambushes, then adapt to armored targets and Spike bursts | 90-120s |
| 74 | Open the Last Gate / break barricade and reach exit | 7 distinct ascending sectors, 38 x 322m total; final original two bends; sealed 2m gates | Hold 8 > 9 > 11 > 13 > 14 > 16 > 18; final 13B+1R+2X+1D+1A; 7 gates x 3 hits in saved asset | Three reduced periodic fore-sector patches; final original G2 | Escalate crowd and angle pressure toward the last gate | Fresh pacing playtest |
| 75 | Guard the Approach / protect point through finite waves | 32 x 96 alternating island pairs, three broad north approaches | 10B+4R+4X ~> 12B+2A+6D ~> 13B+4R+4X+3T; batch 4/4s | Four staggered 3 x 6m switchback and approach strips | Break armor and keep attackers off the zone as fresh groups arrive | 90-120s |
| 76 | A Clear Purpose / destroy shell and core target | 36 x 38 open target court, low boundary rock fans; side support | Hold 10B plus specialized pair of 2X; 3 shell explosions | None | Breather using a familiar readable target | 75-105s |
| 77 | Final Stand / protect point through finite waves | 32 x 96 broad zigzag approaches, two long islands with generous end gaps | 10B+2A+4X+4D ~> 11B+5X+5D+1R ~> 12B+6R+4D+2W; batch 4/4s | Four staggered 3 x 6m side strips | Reposition around the islands to intercept early Exploders and late Wizards | 90-120s |
| 78 | Claim the Ground / clear waves | 100 x 100 court, staggered rock crescents forming broad linked combat areas; north, west, east, split north, split sides, far banks | 15B+2N+6M+7R+6D+2LP ~> 18B+2F+6AS+13X+5D+2LP ~> 22B+8A+2E+9R+9X+3LP > 22B+2S+5SP+6T+1F+8D+6X+3LP ~> 25B+3N+5SP+7M+6R+8D+4X+3LP ~> 27B+1S+2F+4AS+6A+2W+8R+10X+6D+3LP; batch 8/6s | Seven staggered 3 x 6m strips across all four sectors and the central crossing | Shift launch angles with each bank and clear the Elite checkpoint before the final mixed groups | 4-5min |
| 79 | Room to Breathe / clear waves | 38 x 34 open rounded trapezoid, isolated far rock; far banks | 8B+2X ~> 8B+2R; batch 4/5s | None | Short release before Dino. Line up clean launches as the shooters arrive | 60-90s |
| 80 | Giants Fall Again / defeat Dino with pillars | Exact Dino arena and three pillar positions | Hold 10B+1R+1D general support plus 2B per unconsumed pillar | P2 between outer approaches; pillar setup areas remain safe | Three successful pillars; existing chase intervals 6/4.5/3.5s, 1s warning; toward-boss fall | 3-5min |

2026-10-11 update: TRACK-006 replaces the former equal-length straight/diagonal rails with
successive 10m additions and left/right route turns. Level 16 retains its 10m introduction;
later routes are 20-80m with clear corners, adjusted floors/entries and separated spawn banks.
The implementation preserves the original 2m step, giving 5/10/15/20/25/30/35/40 net forward
hits. Earlier short-clear timing targets for these longer objectives require fresh playtesting.

2026-10-11 update: BARRICADE-007 adds one original room-length segment and gate at each
successive break-and-exit level: 13/22/34/42/56/62/74 have 1/2/3/4/5/6/7 gates. Each room
originally reused its bounded support and hazards; its crowd activates after the preceding
gate breaks and the player enters. All gates must break before the final exit completes
the level. Level 13's existing single room remains intact. Short-clear timing targets for
the extended chains require fresh human playtesting.

2026-10-12 update: BARRICADE-009 gives levels 22/34/42/56/62/74 distinct, increasingly
difficult sectors. The final layout, hazards and gate difficulty remain unchanged;
its crowd may change (user choice 1). Earlier populations rise from eight to each
original final cap, while side-pocket/island obstructions grow and periodic patches
remain smaller and less numerous than the final hazards. Baselines and Exploders
are joined by Ranged, Dashers and Armored enemies as sectors progress; the final
mix has all five types. Baselines remain bounded, renewable ammunition. BARRICADE-008
room confinement also applies to specialist movement and newly started attacks.

## Rules carried into implementation

- Preserve VISION-001..005, PLAYER-001/003/004/005/010, COMBAT ownership/damage rules, INFO-001..005 and all mechanic-specific requirements. All fundamentals remain available from level 1. No weapons, new combo system, stat progression, new enemy/boss mechanics or extra persistent HUD.
- The table repositions existing mechanics and deliberately repeats objectives. Legacy-only placement restrictions, including BARRICADE-005's original single-introduction restriction and old numbered gauntlet instructions, become historical constraints on the preserved legacy content. Gameplay damage/eligibility/lifecycle requirements remain authoritative. Record this scope explicitly in the GDD upon acceptance.
- Map barricades to BARRICADE-001..005; cover to COVER-001..005; shell to SHELL-001..006; track to TRACK-001..005; defense to PROTECT-001..004; armor to ENEMY-014/015; Wizards to WIZARD-001..007; Trails to TRAIL-001..007; ground to GROUND-001..007; bosses to BOSS, CHICKEN, ROLL and PILLAR groups.
- The four-enemy revision additionally maps LinkedPair to LINK-001..007, Dragon to DRAGON-001..007, Snake to SNAKE-001..007 and Ninja to NINJA-001..006. Preserve the current gameplay defaults, six-segment Snake tuning and two-slot Ninja ambush cap; this revision changes campaign placement/composition only.
- Shell remains a single objective with its existing two phases, not two campaign objectives. Barricade destruction plus reaching its exit remains its existing single objective. No added survivor cleanup for target or boss victories.
- Keep ordinary enemy health, damage, speed and behavior settings shared and unchanged. Campaign wave assets and objective/boss settings are independent of legacy settings. Reuse meshes/materials/prefabs where safe. No recipe may silently restore legacy scenes to the player build.
- Armored finite waves enable the existing ammunition safeguard. Ordinary Wizard waves enable Baseline ammunition and wait for persistent Wizard zones. Trail-only waves never wait on remaining Trail sections. Defense uses finite waves with existing defeat precedence and no Wizard hazard completion gate. Its armor safeguard is enabled only where required; any interaction with breaches/last surviving armor needs a focused runtime check.
- Cover/track support mixtures with non-Baseline specialists need validation against the existing replenishment implementation before their future chapter batch. Preserve bounded populations and safe ammunition. Do not silently replace an unsupported combination with a new mechanic. None of these future mixed-objective combinations are required by Chapter 1.
- Spawn regions must reject obstacles, occupied positions and warning/active ground. Launches use normal physics; no manually positioned frozen enemies. Validate broad path access using real navigation, including objective-solid movement and periodic phases.
- Boss geometry remains exactly the corresponding existing arena; do not use rematches to change floor, perimeter, static obstacles or pillar positions. Static hazard placement is permitted separately. Dino remains pillar-only, with three pillars and at most three required successes. Gatekeeper remains player-owned body damage only; rematch support stays at most one live non-Baseline.
- Later first bosses start from their existing settings except listed support counts; balance in their own chapter batch. Actual durations may require campaign-owned boss tuning. No forced pacing waits, altered damage eligibility or shared ordinary-enemy stat escalation to achieve them.

## Original Chapter 1 implementation boundary (2026-10-10, historical)

1. Add a campaign catalog with stable level identifiers and an 80-slot design mapping; create/load only the ten implemented scenes. Later levels are unavailable content, never dangling scene loads. Keep saved unlock progress distinct from installed/playable content.
2. Add local saved completion/unlocks, Continue, chapter/level selection and confirmed New Campaign. Continue selects the next unfinished playable campaign level. An unfinished future chapter must not be presented as full campaign completion.
3. Ordinary completion advances; retries reload only the current level at full health. A replay cannot reduce progression and returns to selection with Retry and Next Level where available. Completing level 10 saves the Chapter 2 unlock and shows Chapter 1 Complete with selection/replay access; until Chapter 2 exists, show its unavailable state and disable its Continue action. This is a proposed temporary batch boundary, not a change to the complete 80-level lifecycle.
4. Extend the existing Level Play Editor window with Campaign/Legacy choices. All 23 legacy encounters remain editable/playable through that window without touching campaign saves. The player build includes Bootstrap and implemented campaign scenes only; validation and legacy scenes stay development-only.
5. Author Chapter 1 scenes, ECS SubScenes, wave assets, campaign-owned objective/boss settings, entry points, hints and distinct kit layouts. Preserve the exact Gatekeeper arena. Reuse the current additive scene lifecycle and Mono/ECS bridge ownership.
6. Update LOOP-002..006 and resolve the campaign-specific scope of OQ-009/013/018. Document architecture after actual implementation. Leave unrelated weapons, effect grammar, art/input and hardware-performance questions open.
7. Verify save/reopen, sequential unlocks, replay, New Campaign confirmation, retry/full-health, completion/transition cleanup, boss authority, Chapter 1 boundary, keyboard/controller menus, legacy save isolation and build registration. Compile through dotnet when practical and Unity after import; inspect baking and play every Chapter 1 encounter. Check aiming, replenishment, geometry/navigation, timing and representative crowd performance separately. In level 8, verify the 100 x 100m footprint, 270 authored enemies across four waves (peak initial wave population 94, before defeats), navigation/collision/presentation cost and a playtest duration above three minutes. Larger-wave duration remains tuning-dependent; do not claim it from spawn counts alone. Report any unavailable runtime verification explicitly.

## Review status

The user approved the revised 80-row table and instructed implementation of Chapter 1. Subsequent requests authorize Chapters 2-8, completing the full campaign. See CurrentArchitecture.md and Docs/Verification/Chapter1.md through Chapter8.md for implemented ownership, actual checks and remaining playtest work.

2026-10-14: The user requested the four-enemy campaign integration and asked for Campaign80Proposal.md to be updated and explained before project changes. After the proposal update, the user authorized applying its introductions and later appearances. The revised rows are now authored in the existing campaign, with matching builders and opening hints; counts and pacing remain playtest tuning. Actual verification is recorded in [CampaignEnemyRevision.md](../Verification/CampaignEnemyRevision.md).
