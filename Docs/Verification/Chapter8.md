# Chapter 8 implementation and verification

2026-10-10. Requirements: LOOP-002..008, COVER-001..005, BARRICADE-006,
PROTECT-001..004, SHELL-001..006, PILLAR-001..006, GROUND-001..007,
ENEMY-009/011/014/015, WIZARD-001..007, TRAIL-001..007.

## Playing and tuning

Open `Assets/CrowdPunch/Scenes/Bootstrap.unity` and press Play. Continue after Chapter 7
loads level 71. Window > Crowd Punch > Level Play > Campaign provides previews without
changing normal progress. Level 80 ends the campaign; completed levels remain replayable.
All 80 campaign encounters are installed. Ten new main scenes and ECS SubScenes, 23 wave
assets and independent objective settings live under `Assets/CrowdPunch/Scenes/Campaign`
and `Assets/CrowdPunch/Data/Campaign`. Existing enemy profiles and runtime mechanics
remain unchanged.

| Level | Encounter | Initial population / objective |
|---|---|---|
| 71 | Find Your Feet | 14B+2R > 18B+2X |
| 72 | Thread the Crowd | Hold 14B+1W+1T; four-hit cover, 100-degree opening, 30 degrees/s, six-second reversals |
| 73 | Committed Lines | 18B+2D+1E > 20B+2A+2R > 22B+2X+2T; two periodic side strips |
| 74 | Open the Last Gate | Hold 16B+2X; four-hit 2m gate with sealed, recessed sides; two permanent pockets |
| 75 | Guard the Approach | Finite defense: 20B+2R > 24B+2A > 24B+2D+1W |
| 76 | A Clear Purpose | Hold 10B plus specialized pair of 2X; three shell explosions then core |
| 77 | Final Stand | Finite defense: 24B+2A > 26B+2X > 26B+2R+2W |
| 78 | Claim the Ground | 76 > 96 > 102 > 108 > 128 > 148; 100 x 100m wave arena; two periodic outer strips |
| 79 | Room to Breathe | 14B+2X > 18B+2R+1A |
| 80 | Giants Fall Again | Hold 10B+1R+1D plus 2B per unconsumed pillar; three successful pillar hits; two outer periodic strips |

B = Baseline, R = Ranged, X = Exploder, D = Dasher, A = Armored, E = Elite,
W = Wizard, T = Trail. Level 78's six compositions are 72B+4R, 88B+6X+2D,
96B+4A+2E, 104B+2W+2T, 120B+4R+4D and 136B+4A+6X+2E. The 658 initial enemies
include four Elites; existing ammunition safeguards can add more. Three-second delays
separate cleared waves; no duration timer forces the 4-5 minute target.

Both defenses exclude Elites, use batches of four every three seconds, five-second
cleared-wave pauses and first-breach failure. Neither enables Wizard supply or
persistent-zone completion gates. Approved armor safeguards remain in 75 wave 2 and
77 wave 1; their finite replacement bodies count toward remaining enemies and breaches.
Objective support replenishes after four seconds; the shell uses its existing paired
Exploder rule. Permanent pockets measure 3 x 6m; periodic strips measure 3 x 8m and use
4/1.5/2.5s inactive/warning/active cycles, staggered by four seconds. The Dino retains
its exact original arena and pillar positions, with 6/4.5/3.5s chase stages, one-second
warning and toward-boss pillar falls.

## Verification

- Unity compilation and single-worker `dotnet build Assembly-CSharp.csproj --no-restore -m:1`
  passed with zero errors and 331 existing reference warnings.
- All 39 campaign tests passed. Coverage includes exact populations, navigation clearance,
  safe spawn/hazard separation, sealed gate frontage, finite defense policies, cover/shell
  tuning, final save sentinel and replay unlocks, original Dino geometry, pillar-area
  clearance, and correct upward floor triangulation with exact polygon areas.
- Shared suites passed: protected point 17, Dino 31, Pillar-filter 33 (overlaps Dino and
  campaign geometry), rotating cover 10, shell 5 and ground hazards 32.
- Ten-second stationary, healed-player samples in level 78 measured wave 3 (102 initial,
  92 surviving) at 16.660ms median / 35.146ms maximum main-thread time across 597 samples.
  Wave 6 (148 initial, 120 surviving) measured 18.200ms / 57.582ms across 473 samples.
  Both advanced approximately ten game seconds at time scale 1. The first snapshot found all 180 hidden pooled roots outside active physics. Wave 6
  initially caught one of 534 newly pooled roots in the previous physics-world snapshot;
  the second capture about 18ms later found zero of those 534 roots in active physics,
  consistent with the next physics rebuild. The 2/4 visible landing bodies were counted
  separately. Wave 6 median Elite punch/support costs were .371/4.737ms, with
  support peaking at 31.134ms; Wizard collision contacts measured .202ms median. Physics
  group scheduling measured .220ms median, which does not measure total worker cost.
  Evidence: `Temp/CampaignValidation/chapter8-profile.txt` and
  `chapter8-systems-profile.txt`. These include Editor overhead and natural casualties;
  they are not target-device or worst-case combat benchmarks.
- The live lifecycle passed all 23 new waves plus the preceding Rolling Blob boundary,
  player death/retry, both defenses' first-breach failure and retry, exact batch counts,
  hazard phase cycles, shell settings, final CampaignComplete handling, save/reload,
  replay/retry and return to menu. It uses an isolated temporary save and injected
  defeats/objective completion; the normal save is untouched. Evidence:
  `Temp/CampaignValidation/chapter8-lifecycle.txt`.
- A focused level 72 replay defeated its Wizard and Trail: both returned as the same
  bounded entities, with 16 total roots and the cover objective still active.
- In level 77, a controlled setup held one armored enemy away from the zone and defeated
  its other wave members. The existing safeguard supplied a finite Baseline replacement,
  kept the wave incomplete, retained the same wave/generation ownership and did not
  enable infinite respawn. Moving that Active replacement into the protected zone caused
  first-breach failure and destroyed the body. Retry cleared breach/supply state and
  restarted wave 1 without changing progress. No runtime changes were necessary.
- Final Dino replay confirmed baked three-hit/two-local-body tuning, 6/4.5/3.5s chase
  stages and one-second warning, then returned ReplayComplete while preserving the
  completed-save sentinel and blocking index 80. Evidence for these focused checks:
  `Temp/CampaignValidation/chapter8-interactions.txt`.
- Game views were inspected for the mixed cover, sealed gate, long defense, large arena
  and Dino's outer hazard strips. No gameplay runtime errors occurred; the injected
  player death produced the existing inactive Animator warning.

- All 81 enabled build scenes are registered; new assets have Unity metadata. The diff
  passes whitespace checks. Unity is stopped on Bootstrap with no dirty scene.

## Remaining playtesting

Human clear times, level 78's 4-5 minute target (over three minutes), the Dino rematch's
3-5 minute target, overall difficulty and target-device performance require playtesting.
OQ-001 hardware/FPS acceptance remains unresolved. Controlled injections and Editor
profiling do not establish these balance or release-performance targets.
