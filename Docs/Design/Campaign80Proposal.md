# Crowd Punch: 80-level campaign proposal

Date: 2026-10-10. Status: **Approved design; Chapters 1-4 implementation authorized.** Includes the user's amendment requiring one large wave-clear level per chapter. Encounter numbers are starting tuning and remain subject to playtesting.

Source: `C:/Users/pc/Downloads/Crowd_Punch_80_Level_Campaign_Plan.md` plus current repository design, configuration and authoring inspection. The user explicitly selected full-table review before Chapter 1 implementation, then requested Chapters 2, 3 and 4 in sequence. Levels 1-40 and their campaign infrastructure are authorized; Chapters 5-8 remain future implementation batches.

## Reading the table

- B = Baseline, R = Ranged, X = Exploder, D = Dasher, E = Elite, A = Armored, W = Wizard, T = Trail. Numbers are exact proposed spawn counts, not weights. Elite counts are additional to normal-enemy totals.
- `>` separates finite waves. Unless noted: first wave after 3 seconds, later waves 3 seconds after all current/previous enemies are defeated; spawn each wave together. Directions refer to safely inset spawn rectangles, not guaranteed exact enemy positions. Ordinary wave-clear encounters end after the final wave is defeated.
- `Hold` means one bounded replenishing support population until the objective completes, with a proposed 4-second replacement delay. Counts describe the initial/target population, not an unlimited simultaneous crowd. Existing specialized replenishment rules take precedence; in particular Shell replaces its two Exploders only after both are gone, and stops replacing them once the shell breaks.
- `Batch n/s` means batches of n enemies every s seconds within each finite wave. Defense waves use a 5-second cleared-wave pause. No timed overlap is required in this proposal; difficulty comes from geometry, directions, composition and within-wave cadence.
- Dimensions are approximate playable width x length in metres, except the eight large wave arenas, which each require a 100 x 100m playable footprint. North is the objective/far edge; the player generally enters from the south. All non-boss arenas receive distinct authored floor/boundary/obstacle arrangements using the existing nature kit. Defense arenas retain the established 32 x 96m size and 8 x 4m end zone, with distinct approach geometry.
- `G1` = one always-active circular patch, radius 2.5m. `G2` = two always-active 3 x 6m rectangles. `P1` = one periodic 3 x 8m strip. `P2` = two such strips with staggered offsets; `P3` = three. Periodic starting tune: inactive 4s, warning 1.5s, active 2.5s, with evenly staggered offsets. Damage/intervals retain the existing hazard defaults. Every layout retains safe routes and safe spawning throughout the cycle; strips never seal all routes.
- Hazard column lists added static patches. Enemy-created Wizard zones/Trail sections and boss attacks remain present whenever those enemies/bosses appear, even when static hazards are `None`.
- Durations are playtest targets, never time limits or minimum enforced times. Counts, dimensions and timings are reviewable starting values, not measured results.

## Campaign shape

Accepted gate-width amendment (2026-10-10, BARRICADE-006, revised by the user): keep Chapter 1's introductory barricades wide; every break-and-exit barricade from level 13 onward is 2m wide, with solid terrain sealing both sides to the perimeter so the exit is accessible only through the gate. Arena dimensions in the table remain unchanged.

There are exactly 80 proposed levels: eight bosses, 36 wave-clear levels and 36 specific-objective levels. Chapters 1-4 introduce every existing archetype, objective, static hazard activation mode and boss. Chapters 5-8 remix familiar rules. Each chapter has exactly one 100 x 100m arena with a substantially larger enemy crowd and wave clearing as its only objective, at levels 8, 18, 28, 38, 48, 58, 68 and 78. All eight target more than three minutes: 3.5-4.5 minutes in Chapters 1-4 and 4-5 minutes in Chapters 5-8. This replaces the earlier proposal for only three long non-boss encounters. Other non-boss levels mostly target 1-2 minutes, with shorter introductions and a breather immediately before each boss. Boss introductions target 2-3 minutes; rematches 3-5 minutes.

Accepted amendment (2026-10-10): every chapter must contain one 100 x 100m level with a larger crowd where the player only clears waves, targeting longer than three minutes. Exact placement, compositions, wave counts and target ranges below are proposed tuning. These arenas are explicit exceptions to the compact-level default in LOOP-002. Their duration must come from sustained crowd combat, not forced survival timers, long spawn delays or walking across empty space. Spawn concentrated groups from safely inset banks around broad connected combat areas; preserve room for chains and repositioning. Finite waves clear before the next begins. Large waves spawn together unless the table says otherwise, so these are larger simultaneous crowds as well as larger total encounters. Ordinary enemy stats remain unchanged. Profile the highest concurrent population and navigation over the full footprint; OQ-001 hardware/FPS targets remain unresolved.

The boss order moves from choosing exposed body-shot angles, to returning mobile projectiles, to reading protected movement cycles, to baiting a pursuing boss into falling pillars. Each introduction and rematch preserves its corresponding legacy arena geometry: Gatekeeper from Gauntlet_11, Chicken from _20, Rolling Blob from _21, Dino from _22. Rematches change existing settings/support/hazard placement only.

Introduction order: fundamentals (1-2), Ranged (3), barricade (4), rotating cover (7), Gatekeeper (10); Exploder (11), shell (12), Dasher (14), track (16), Chicken (20); Armored (21), Elite (23), defense (24), always-active ground (25), periodic ground (27), Rolling Blob (30); Wizard (31), Trail (33), Dino/pillars (40). After each boss, the following introduction lowers simultaneous pressure. Wave-clear peaks alternate with smaller precision objectives and recovery encounters.

## Chapter 1: First Impact

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 01 | First Line / clear waves | 24 x 30 open rounded court; far-centre then two far banks | 3B > 6B > 8B | None | Low-pressure aiming and body chains; fundamentals available immediately | 45-75s |
| 02 | Choose the Angle / clear waves | 28 x 32 shallow trapezoid; one offset rock island; alternating north-west/north-east banks | 8B > 12B | None | Move around cover to line up a crowd; practice dash repositioning | 60-90s |
| 03 | Across the Court / clear waves | 30 x 36 elongated hexagon; two small side rocks; north then east bank | 6B+1R > 10B+2R | None | Introduce ranged pressure with nearby launch ammunition | 75-105s |
| 04 | Break Through / break barricade and reach exit | 24 x 38 funnel with broad rear court; north exit; lateral support rectangles | Hold 8B; 3 barricade hits | None | Low-pressure deliberate shots at a solid target; cross the exposed exit | 60-90s |
| 05 | Crossfire / clear waves | 34 x 32 offset diamond; two separated cover islands; west then east bank | 12B+2R > 16B+3R | None | First pressure peak: leave a firing lane and launch across it | 90-120s |
| 06 | Open the Lane / break barricade and reach exit | 28 x 40 bent perimeter, short central divider with broad side access; side-bank support | Hold 10B; 3 hits | None | Easier precision encounter; find a useful shot around an obstacle | 60-90s |
| 07 | Through the Opening / destroy rotating-cover target | 34 x 34 octagon; open ring around enclosure; south/west support | Hold 8B; 3 hits; 100-degree opening, continuous 20 degrees/s | None | Introduce opening timing and dangerous returned bodies | 75-105s |
| 08 | Changing Sides / clear waves | 100 x 100 broad court; two offset rock islands, connected open combat areas; north, west, east, north-east banks | 40B > 56B+4R > 72B+4R > 88B+6R | None | Chapter's large crowd encounter: sustain chains and reposition between directions | 3.5-4.5min |
| 09 | One Good Shot / clear waves | 30 x 30 fan-shaped court, one side alcove; two broad far rectangles | 10B > 12B+1R | None | Breather and boss preparation: use bodies to reach a distant threat | 60-90s |
| 10 | The Gatekeeper / defeat boss | Preserve Gauntlet_11 round court and boss route; safe interior crowd rectangle | Hold 10B; three existing boss stages | None | Body shots into exposed head, dodge committed hands; campaign warning tune below | 2-3min |

Chapter 1 opening hints, shown briefly without stopping play:

1. "Line up an enemy with the crowd, then punch. The short line shows the launch direction."
2. "Move around the rocks to find a clear angle. Dash to reposition."
3. "Use nearby enemies to reach the shooters. Keep moving between shots."
4. "Launch bodies into the barricade, then reach the exit."
5. "Step out of the firing lane and send a body back across it."
6. "Find an angle around the divider and break through."
7. "Send bodies through the rotating opening. Watch for returned shots."
8. "Clear each large wave. Find a line through the crowd and reposition as the next wave arrives."
9. "Set up a clear line through the crowd to the distant threat."
10. "Launch enemies into the head. Dodge the hands and shoot through the openings."

Chapter 1 proposals specific to campaign-owned assets: Gatekeeper retains 300 health, existing three stages and damage/ownership rules. Change slam/lunge/sweep anticipation from current 0.1s to 0.8/0.8/1.0s; retain current recovery 2.8/2.8/3.0s initially. Use 1.5s exposed opening and 4s crowd replacement delay. Durations require playtesting; do not pad fights with waits or modify shared enemy stats. Level 7 cover keeps reflection multiplier 1.0, no hit response, and existing enclosure solidity/target eligibility. Barricades retain three hits and existing launch-source eligibility. Later boss/settings proposals below are starting tunes, not changes to legacy assets.

## Chapter 2: Moving Ammunition

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 11 | First Fuse / clear waves | 28 x 30 wide oval, no interior blockers; north banks | 6B+1X > 10B+2X | None | Gentle post-boss Exploder introduction; deliberate detonation position | 60-90s |
| 12 | Crack the Shell / destroy shell and core target | 32 x 32 clipped square, open central target; west/east support | Hold 8B plus specialized pair of 2X; 3 shell explosions; ordinary core health | None | Explosion-only shell, then a fresh attack on the exposed core | 75-120s |
| 13 | Blast the Gate / break barricade and reach exit | 30 x 42 split approach merging before north exit; side support | Hold 10B+2X; 3 hits | None | Reuse explosions against a familiar objective | 75-105s |
| 14 | Committed Charge / clear waves | 30 x 40 runway with wide side pockets; far end waves | 8B+1D > 12B+2D | None | Introduce committed Dasher direction in open space | 75-105s |
| 15 | Crossed Threats / clear waves | 36 x 34 kite shape, diagonal island; alternating side banks | 12B+2R+1D > 16B+2R+2X+2D | None | Mid-chapter peak combining distant threats and launch ammunition | 90-120s |
| 16 | Push the Block / knock into place | 32 x 38 open court, longitudinal rail, ample end access; side support | Hold 8B; 5 net forward hits | None | Breather: direction determines forward or backward progress | 75-105s |
| 17 | Moving Window / destroy rotating-cover target | 36 x 32 broad ring with two outside rock shelves; north/south support | Hold 10B+2X; 3 hits; 90-degree opening, continuous 25 degrees/s | None | Send Exploders through the opening; outside blasts cannot bypass cover | 90-120s |
| 18 | Crowd Reaction / clear waves | 100 x 100 court with three widely separated rock clusters and long diagonal lanes; north, west, east, split north banks | 48B+4X > 64B+4R+2D > 80B+6X+2D > 96B+4R+4X+4D | None | Large crowd encounter: explosive chains and committed charges through dense groups | 3.5-4.5min |
| 19 | Push from Behind / knock into place | 30 x 42 off-centre rail, broad outer return lane; west/east support | Hold 8B+2X; 5 net forward hits | None | Pre-boss breather: place explosions on the useful side of the block | 60-90s |
| 20 | Chicken Run / defeat boss | Preserve Gauntlet_20 open arena and solid perimeter | Hold 6B; existing single/paired shot stages | None | Return punchable shots while respecting dangerous rebounds | 2-3min |

## Chapter 3: Pressure and Space

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 21 | Peel the Armor / clear waves | 32 x 34 broad shooting court, one rear rock shelf; north banks | 8B+1A > 12B+2A; armor ammunition safeguard | None | Gentle post-boss armor introduction; body hits before direct launch | 75-105s |
| 22 | Heavy Traffic / break barricade and reach exit | 30 x 40 asymmetric funnel, two separated boulders; lateral support | Hold 12B+2X; 3 hits | None | Familiar objective and relief before a new enemy | 75-105s |
| 23 | Borrowed Fist / clear waves | 36 x 36 wide court with unobstructed diagonal; far-bank support | 10B+1E > 14B+2R+1E | None | Elite introduction: read and disrupt an enemy-owned body shot | 90-120s |
| 24 | Hold the Line / protect point through finite waves | 32 x 96 lane, broad open middle, zone at south end; north spawns | 12B > 16B > 20B; batch 4/4s; first breach fails | None | Defense introduction; hit approaching bodies away from the zone | 90-120s |
| 25 | Hot Ground / clear waves | 34 x 34 rounded rectangle, off-centre hot island; north/east banks | 10B > 14B+2R | G1 centre-left | Introduce permanent ground danger with a wide safe bypass | 75-105s |
| 26 | Safe Side / knock into place | 34 x 40 rail on east half, wide west approach; north/south side rectangles | Hold 10B+2X; 5 net forward hits | G1 west outer pocket | Use a hazard as an optional crowd opportunity without blocking the rail | 90-120s |
| 27 | Wait for the Warning / clear waves | 36 x 38 two broad lanes around short island; north then side banks | 10B+1D > 14B+2R+2D | P1 west lane | Introduce periodic warning/active cycle; keep an always-safe route | 75-105s |
| 28 | Crowd Channels / clear waves | 100 x 100 court, two staggered long islands with broad end gaps; north, west, east, split side banks | 56B+3A > 72B+4R+1E > 88B+4D+4X > 104B+4A+2E; armor safeguard | G1 outer pocket and P2 side crossings; continuous safe routes | Large crowd encounter: line up armor and elite threats through the crowd | 3.5-4.5min |
| 29 | Window of Safety / destroy rotating-cover target | 38 x 36 ring court, asymmetric outer rocks; south/east support | Hold 10B; 3 hits; 100-degree opening, continuous 20 degrees/s | None | Pre-boss breather: a familiar target with generous shot windows | 60-90s |
| 30 | Rolling Blob / defeat boss | Preserve Gauntlet_21 arena, obstacles and broad lanes | Hold 6B; existing protected roll/vulnerable pause cycle | None | Read committed rolls, reclaim launched bodies and use pauses | 2-3min |

## Chapter 4: Living Hazards

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 31 | Purple Warning / clear waves | 34 x 34 open clipped court; far bank | 6B+1W > 12B+2W; Wizard safeguards | None | Gentle Wizard introduction; move out of zones, launch into casters | 75-105s |
| 32 | Quiet Delivery / knock into place | 34 x 38 diagonal rail, open end pockets; east/west support | Hold 10B; 5 net forward hits | None | Familiar precision breather before next introduction | 60-90s |
| 33 | Leave a Trail / clear waves | 36 x 36 open rounded diamond; far-bank waves | 6B+1T > 12B+2T | None | Trail introduction; redirect its path, avoid lingering sections | 75-105s |
| 34 | Break the Detour / break barricade and reach exit | 32 x 44 dogleg with two broad bends; lateral support | Hold 12B+2X; 4 hits | P1 optional inner shortcut | Remix gate and ground timing without new enemy rules | 90-120s |
| 35 | Moving Perimeter / destroy rotating-cover target | 40 x 38 open ring, three outer pockets; alternating safe support regions | Hold 12B+1T; 4 hits; 100-degree opening, continuous 25 degrees/s | None | Aim while a moving Trail changes safe positioning | 90-120s |
| 36 | Distant Defense / protect point through finite waves | 32 x 96 lane with two widely offset rock islands; north spawns | 16B+2R > 20B+1D > 20B+2R+1W; batch 4/3s | None | First mixed defense; Wizard behavior follows defense targeting rules | 90-120s |
| 37 | Blast Shelter / destroy shell and core target | 36 x 34 side-notched court, two rear rocks, open central target | Hold 12B plus specialized pair of 2X; 3 shell explosions | G1 far outer corner | Recovery encounter: known explosion objective and ample safe ground | 75-105s |
| 38 | The Crowd Moves / clear waves | 100 x 100 court with four shallow perimeter bays and open diagonal lanes; west, east, north, split side banks | 64B+4R+2W > 80B+4D+2T > 96B+4A+2E > 112B+4X+2W+2T; armor/Wizard safeguards | None | Large crowd encounter: redirect living hazards through groups and separate specialist threats | 3.5-4.5min |
| 39 | Last Alignment / knock into place | 34 x 42 rail along clear centre, twin side bays; lateral support | Hold 10B; 5 net forward hits | None | Pre-boss breather: place body shots into an environmental target | 60-90s |
| 40 | Falling Giants / defeat Dino with pillars | Preserve Gauntlet_22 arena and all three pillar positions | Hold 8B general support plus 2B per unconsumed pillar | None | Bait pursuit and topple pillars with player-owned bodies; 3 successful hits | 2-3min |

## Chapter 5: Better Angles

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 41 | Back to Bodies / clear waves | 34 x 32 open pentagon, one side rock crescent; far banks | 12B+2R > 16B+2X | None | Post-boss recovery with familiar chains | 75-105s |
| 42 | Outer Route / break barricade and reach exit | 34 x 44 curved funnel around large rock island; side support | Hold 14B+2X; 4 hits | G2 on unused inner pockets | Choose long safe alignment or a pressured shorter shot | 90-120s |
| 43 | Turn the Shot / clear waves | 38 x 36 court with three widely spaced islands; west/east banks | 16B+2R+1E > 18B+2D+1E | None | Fight Elite shot setups from useful angles | 90-120s |
| 44 | Stop and Go / destroy rotating-cover target | 40 x 36 unequal outer bays around enclosure; side support | Hold 12B+2R+1X; 4 hits; 85-degree opening, rotate 3s at 30 degrees/s / pause 1s | None | Existing cover pause mode rewards planning a shot | 90-120s |
| 45 | Armor in Motion / clear waves | 38 x 40 clipped corridor with cross lanes; north/east/west banks | 14B+2A > 16B+2D+2A > 18B+2R+1E; armor safeguard | P1 side crossing | Pressure peak through composition and route choice | 90-120s |
| 46 | Measured Push / knock into place | 36 x 40 wide oblique rail with open rear apron; two side rectangles | Hold 10B+2X; 5 net forward hits | None | Mid-chapter relief; control direction and rebound space | 75-105s |
| 47 | Divided Approach / protect point through finite waves | 32 x 96 lane, long island splitting north approach, merged south half | 20B+2R > 24B+2D > 24B+2X+1E; batch 4/3s | None | Reposition between separated defense approaches | 90-120s |
| 48 | Firebreak / clear waves | 100 x 100 court with two broad side basins linked through a wide centre; north, west, east, split north, side banks | 64B+6X > 80B+4R+2T > 88B+4A+2E > 104B+4D+2W > 120B+6X+2T; safeguards | G2 in outer pockets | Large crowd encounter: choose chain-reaction angles while moving between open basins | 4-5min |
| 49 | Make Room / clear waves | 34 x 34 open fan with two boundary outcrops; far bank | 14B+1W > 18B+2R; Wizard safeguards | None | Breather before Gatekeeper rematch | 75-105s |
| 50 | Gatekeeper Returns / defeat boss | Exact Gatekeeper arena geometry; safe interior support | Hold 12B+1R; at most one live non-Baseline; three stages | P2 in side sectors, clear central shooting apron | Harder positioning; 0.65/0.65/0.85s warnings, retain recoveries; 360 health | 3-4min |

## Chapter 6: Deliberate Reactions

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 51 | Fresh Fuse / destroy shell and core target | 34 x 36 broad oval with one empty rear bay; side support | Hold 10B plus specialized pair of 2X; 3 shell explosions | None | Low-pressure return to deliberate explosives after boss | 75-105s |
| 52 | Crossing Paths / clear waves | 38 x 40 S-edged court, two offset islands; west/east waves | 16B+2D+1T > 20B+2R+2T | None | Launch a Trail across a useful lane while dodging charges | 90-120s |
| 53 | Reverse the Window / destroy rotating-cover target | 40 x 40 unequal octagon, north rock shelf; east/west support | Hold 14B+2X; 4 hits; 90-degree opening, 30 degrees/s, reversal every 5s | None | Existing reversal mode; read motion instead of memorizing a rhythm | 90-120s |
| 54 | Push Under Pressure / knock into place | 38 x 44 off-axis rail between two broad bays; side support | Hold 14B+2X; 6 net forward hits | P2 away from rail endpoints | Precision objective under alternating spatial pressure | 90-120s |
| 55 | Disrupt the Casters / clear waves | 40 x 40 triangular-island court; north/east/west waves | 14B+2W > 18B+2R+2W > 18B+2A+1W; safeguards | None | Chapter peak: approach casters through crowd and armor | 90-120s |
| 56 | Clean Break / break barricade and reach exit | 32 x 42 broad funnel, one short offset screen; side support | Hold 12B+2X; 4 hits | None | Recovery objective with straightforward shot access | 75-105s |
| 57 | Long Watch / protect point through finite waves | 32 x 96 three staggered approach islands, broad recovery area before zone | 20B+2R > 24B+2D > 24B+2X+1E; batch 4/3s | None | Familiar defense under mixed approach pressure; first breach fails | 90-120s |
| 58 | Moving Batteries / clear waves | 100 x 100 court, three offset islands and wide cross-court shooting lanes; north, west, east, split sides, north banks | 72B+4R+1E > 88B+6X+2D > 96B+4A+2E > 112B+4R+2W > 128B+6X+4D; safeguards | P2 optional outer crossings | Large crowd encounter: disrupt elite setups and exploit explosive groups | 4-5min |
| 59 | Open Air / clear waves | 36 x 32 wide shallow court with boundary alcoves; far banks | 12B+2X > 16B+2R | None | Deliberate relief after endurance and before boss | 60-90s |
| 60 | Chicken Crossfire / defeat boss | Exact Chicken arena geometry, no new interior blockers | Hold 8B+2X; existing single/paired patterns | P2 narrow edge sectors, broad central safe space | Movement-leading shot aim; retain readable 0.8s wind-up, 1.5s shot spacing; 2100 health | 3-4min |

## Chapter 7: Control the Ground

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 61 | Stable Footing / clear waves | 36 x 34 open stepped rectangle; north banks | 12B+1A > 16B+2R; armor safeguard | None | Post-boss relief using familiar body-shot targets | 75-105s |
| 62 | Side Door / break barricade and reach exit | 36 x 44 offset funnel, exit in north-east corner; side support | Hold 14B+2X; 4 hits | P2 on optional central route | Read a safe approach without forcing hazard crossings | 90-120s |
| 63 | Draw a Line / clear waves | 42 x 38 three broad lanes around low rock islands; west/east waves | 16B+2T+1W > 20B+2D+2T; Wizard safeguards | None | Layer living hazards while preserving retreat space | 90-120s |
| 64 | Shell Island / destroy shell and core target | 38 x 38 central target, wide safe ring, four shallow boundary notches | Hold 14B plus specialized pair of 2X; 4 shell explosions | G2 in opposite outer pockets | Keep Exploders alive until they can reach the shell | 90-120s |
| 65 | Three Approaches / clear waves | 42 x 42 three-sided court with wide linking bays; north/west/east waves | 18B+2A+1E > 20B+2R+2W > 22B+2D+2T; safeguards | P1 far side lane | Peak through changing composition and usable lanes | 90-120s |
| 66 | Set the Pace / destroy rotating-cover target | 38 x 40 offset ring with broad southern bay; lateral support | Hold 12B+2X; 4 hits; 90-degree opening, 25 degrees/s, successful-hit pause 1s | None | Relief: exploit the existing hit-pause option | 75-105s |
| 67 | Keep It Moving / clear waves | 42 x 40 staggered islands, continuous outside lane; north/east waves | 20B+2R+1E > 22B+2A+2D; armor safeguard | P2 between islands, bypasses open | Retain good body-shot angles as safe lanes change | 90-120s |
| 68 | Shifting Front / clear waves | 100 x 100 four-bay court with widely separated islands and a broad central cross; spawn bank changes each wave | 64B+4R > 80B+6X > 88B+4A+2E > 96B+4D+2T > 112B+4R+2W > 128B+4A+6X; safeguards | P3 around outer islands, broad bypasses retained | Large crowd encounter: shift shot angles and use safe ground during sustained waves | 4-5min |
| 69 | Straighten Up / knock into place | 36 x 40 straight rail with wide clear backing area and two side notches | Hold 10B+2X; 5 net forward hits | None | Pre-boss recovery with deliberate directional hits | 60-90s |
| 70 | Rolling Pressure / defeat boss | Exact Rolling Blob arena and obstacle geometry | Hold 8B+2X; existing three stages | P2 in outer pockets, all broad boss lanes retained | Existing physical-reflection mode; 600 health; pause 2.5/1.8/1.2s, same roll speeds | 3-4min |

## Chapter 8: Crowd Mastery

| Level | Name / one main objective | Layout and spawn directions | Composition and waves | Static hazards | Teaching purpose / pacing | Target |
|---|---|---|---|---|---|---|
| 71 | Find Your Feet / clear waves | 36 x 36 open tapered octagon; one far island; north then west bank | 14B+2R > 18B+2X | None | Post-boss reset before final combinations | 75-105s |
| 72 | Thread the Crowd / destroy rotating-cover target | 42 x 40 asymmetric ring with clear south apron; side support | Hold 14B+1W+1T; 4 hits; 100-degree opening, 30 degrees/s, reversals every 6s | None | Time a body shot while relocating around living hazards | 90-120s |
| 73 | Committed Lines / clear waves | 44 x 40 slanted two-island court; east/west/north waves | 18B+2D+1E > 20B+2A+2R > 22B+2X+2T; armor safeguard | P2 across optional side routes | Peak in launch direction, enemy commitment and escape planning | 90-120s |
| 74 | Open the Last Gate / break barricade and reach exit | 38 x 46 double-bend funnel, broad shot pockets at both bends | Hold 16B+2X; 4 hits | G2 in inner bend pockets | Find the angle; use explosions without losing needed ammunition | 90-120s |
| 75 | Guard the Approach / protect point through finite waves | 32 x 96 alternating island pairs, three broad north approaches | 20B+2R > 24B+2A+1E > 24B+2D+1W; batch 4/3s; armor safeguard | None | Defense peak with physical crowd control; no hazard-dependent victory | 90-120s |
| 76 | A Clear Purpose / destroy shell and core target | 36 x 38 open target court, low boundary rock fans; side support | Hold 10B plus specialized pair of 2X; 3 shell explosions | None | Breather using a familiar readable target | 75-105s |
| 77 | Final Stand / protect point through finite waves | 32 x 96 broad zigzag approaches, two long islands with generous end gaps | 24B+2A > 26B+2X+1E > 26B+2R+2W; batch 4/3s; armor safeguard | None | Final short defense; use the approaching crowd to prevent breaches | 90-120s |
| 78 | Claim the Ground / clear waves | 100 x 100 court, staggered rock crescents forming broad linked combat areas; north, west, east, split north, split sides, far banks | 72B+4R > 88B+6X+2D > 96B+4A+2E > 104B+2W+2T > 120B+4R+4D > 136B+4A+6X+2E; safeguards | P2 outer lanes, safe central routes | Final large crowd encounter: sustained chain reactions using all learned positioning skills | 4-5min |
| 79 | Room to Breathe / clear waves | 38 x 34 open rounded trapezoid, isolated far rock; far banks | 14B+2X > 18B+2R+1A; armor safeguard | None | Short release before final boss; deliberate clean launches | 60-90s |
| 80 | Giants Fall Again / defeat Dino with pillars | Exact Dino arena and three pillar positions | Hold 10B+1R+1D general support plus 2B per unconsumed pillar | P2 between outer approaches; pillar setup areas remain safe | Three successful pillars; existing chase intervals 6/4.5/3.5s, 1s warning; toward-boss fall | 3-5min |

## Rules carried into implementation

- Preserve VISION-001..005, PLAYER-001/003/004/005/010, COMBAT ownership/damage rules, INFO-001..005 and all mechanic-specific requirements. All fundamentals remain available from level 1. No weapons, new combo system, stat progression, new enemy/boss mechanics or extra persistent HUD.
- The table repositions existing mechanics and deliberately repeats objectives. Legacy-only placement restrictions, including BARRICADE-005's original single-introduction restriction and old numbered gauntlet instructions, become historical constraints on the preserved legacy content. Gameplay damage/eligibility/lifecycle requirements remain authoritative. Record this scope explicitly in the GDD upon acceptance.
- Map barricades to BARRICADE-001..005; cover to COVER-001..005; shell to SHELL-001..006; track to TRACK-001..005; defense to PROTECT-001..004; armor to ENEMY-014/015; Wizards to WIZARD-001..007; Trails to TRAIL-001..007; ground to GROUND-001..007; bosses to BOSS, CHICKEN, ROLL and PILLAR groups.
- Shell remains a single objective with its existing two phases, not two campaign objectives. Barricade destruction plus reaching its exit remains its existing single objective. No added survivor cleanup for target or boss victories.
- Keep ordinary enemy health, damage, speed and behavior settings shared and unchanged. Campaign wave assets and objective/boss settings are independent of legacy settings. Reuse meshes/materials/prefabs where safe. No recipe may silently restore legacy scenes to the player build.
- Armored finite waves enable the existing ammunition safeguard. Ordinary Wizard waves enable Baseline ammunition and wait for persistent Wizard zones. Trail-only waves never wait on remaining Trail sections. Defense uses finite waves with existing defeat precedence and no Wizard hazard completion gate. Its armor safeguard is enabled only where required; any interaction with breaches/last surviving armor needs a focused runtime check.
- Cover/track support mixtures with non-Baseline specialists need validation against the existing replenishment implementation before their future chapter batch. Preserve bounded populations and safe ammunition. Do not silently replace an unsupported combination with a new mechanic. None of these future mixed-objective combinations are required by Chapter 1.
- Spawn regions must reject obstacles, occupied positions and warning/active ground. Launches use normal physics; no manually positioned frozen enemies. Validate broad path access using real navigation, including objective-solid movement and periodic phases.
- Boss geometry remains exactly the corresponding existing arena; do not use rematches to change floor, perimeter, static obstacles or pillar positions. Static hazard placement is permitted separately. Dino remains pillar-only, with three pillars and at most three required successes. Gatekeeper remains player-owned body damage only; rematch support stays at most one live non-Baseline.
- Later first bosses start from their existing settings except listed support counts; balance in their own chapter batch. Actual durations may require campaign-owned boss tuning. No forced pacing waits, altered damage eligibility or shared ordinary-enemy stat escalation to achieve them.

## Chapter 1 implementation boundary after review

1. Add a campaign catalog with stable level identifiers and an 80-slot design mapping; create/load only the ten implemented scenes. Later levels are unavailable content, never dangling scene loads. Keep saved unlock progress distinct from installed/playable content.
2. Add local saved completion/unlocks, Continue, chapter/level selection and confirmed New Campaign. Continue selects the next unfinished playable campaign level. An unfinished future chapter must not be presented as full campaign completion.
3. Ordinary completion advances; retries reload only the current level at full health. A replay cannot reduce progression and returns to selection with Retry and Next Level where available. Completing level 10 saves the Chapter 2 unlock and shows Chapter 1 Complete with selection/replay access; until Chapter 2 exists, show its unavailable state and disable its Continue action. This is a proposed temporary batch boundary, not a change to the complete 80-level lifecycle.
4. Extend the existing Level Play Editor window with Campaign/Legacy choices. All 23 legacy encounters remain editable/playable through that window without touching campaign saves. The player build includes Bootstrap and implemented campaign scenes only; validation and legacy scenes stay development-only.
5. Author Chapter 1 scenes, ECS SubScenes, wave assets, campaign-owned objective/boss settings, entry points, hints and distinct kit layouts. Preserve the exact Gatekeeper arena. Reuse the current additive scene lifecycle and Mono/ECS bridge ownership.
6. Update LOOP-002..006 and resolve the campaign-specific scope of OQ-009/013/018. Document architecture after actual implementation. Leave unrelated weapons, effect grammar, art/input and hardware-performance questions open.
7. Verify save/reopen, sequential unlocks, replay, New Campaign confirmation, retry/full-health, completion/transition cleanup, boss authority, Chapter 1 boundary, keyboard/controller menus, legacy save isolation and build registration. Compile through dotnet when practical and Unity after import; inspect baking and play every Chapter 1 encounter. Check aiming, replenishment, geometry/navigation, timing and representative crowd performance separately. In level 8, verify the 100 x 100m footprint, 270 authored enemies across four waves (peak initial wave population 94, before defeats), navigation/collision/presentation cost and a playtest duration above three minutes. Larger-wave duration remains tuning-dependent; do not claim it from spawn counts alone. Report any unavailable runtime verification explicitly.

## Review status

The user approved the revised 80-row table and instructed implementation of Chapter 1. Chapters 2-8 remain future implementation batches. See CurrentArchitecture.md for implemented ownership and the Chapter 1 verification record for actual checks and remaining playtest work.
