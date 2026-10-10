# Chapter 5 implementation and verification

2026-10-10. Requirements: LOOP-002..008, BOSS-001..009, PROTECT-001..004,
ENEMY-009/011, BARRICADE-006, TRACK-001..005, COVER-001..005, GROUND-001..007.

## Playing and tuning

Open `Assets/CrowdPunch/Scenes/Bootstrap.unity` and press Play. Continue after Chapter 4
loads level 41. Window > Crowd Punch > Level Play > Campaign offers previews without
changing normal progress. Finishing level 50 unlocks the future, unavailable Chapter 6.
Ten main scenes and ECS SubScenes, 21 wave assets, and independent objective settings
live under `Assets/CrowdPunch/Scenes/Campaign` and `Assets/CrowdPunch/Data/Campaign`.
The creation recipe refuses to overwrite saved content.

| Level | Encounter | Initial population / objective |
|---|---|---|
| 41 | Back to Bodies | 12B+2R > 16B+2X |
| 42 | Outer Route | Hold 14B+2X; four-hit 2m gate; sealed, recessed sides; two hot pockets |
| 43 | Turn the Shot | 16B+2R+1E > 18B+2D+1E |
| 44 | Stop and Go | Hold 12B+2R+1X; four-hit cover target; 85-degree opening |
| 45 | Armor in Motion | 14B+2A > 16B+2D+2A > 18B+2R+1E; periodic side crossing |
| 46 | Measured Push | Hold 10B+2X; five net hits along a diagonal rail |
| 47 | Divided Approach | 20B+2R > 24B+2D > 24B+2X; finite defense |
| 48 | Firebreak | 70 > 86 > 94 > 110 > 128; 100 x 100m wave-clear arena |
| 49 | Make Room | 14B+1W > 18B+2R |
| 50 | Gatekeeper Returns | Hold 12B+1R; three-stage boss, 360 health; two periodic side sectors |

B = Baseline, R = Ranged, X = Exploder, D = Dasher, A = Armored, E = Elite,
W = Wizard, T = Trail. Level 48's exact compositions are 64B+6X, 80B+4R+2T,
88B+4A+2E, 104B+4D+2W, and 120B+6X+2T. Its 488 initial enemies include the two
Elites; existing Elite, armor and Wizard supplies can add more. There is no timer.
Level 44 rotates at 30 degrees/s for three seconds and pauses for one second.
Defense batches up to four every three seconds, pauses five seconds after cleared
waves, has no replenishment, and fails at the first breach. Permanent patches are
3 x 6m; periodic strips are 3 x 8m with 4/1.5/2.5s inactive/warning/active timing.
Gatekeeper's side sectors are staggered by four seconds, leaving the central shooting
apron clear. Warning timings are .65/.65/.85s; recoveries remain 2.8/2.8/3s.
A configurable warning floor preserves legacy encounters' .8s clamp.

## Verification

- Unity compilation passed. The .NET build passed with zero errors and 344 existing
  reference-conflict, obsolete-API and unused-field warnings.
- All 25 campaign tests passed, including exact populations, safe navigation anchors,
  hazard/spawn separation, gate front clearance, rail/socket wiring, cover cycles,
  save/unlock boundaries, actual baked boss timings and legacy boss geometry identity.
- Related tests passed: protected-point 16, boss 27, cover 10, track 17, ground 32.
  The new defense regression verifies ordinary Elite replenishment remains enabled
  outside defense and switches off when the last Elite is defeated.
- The live lifecycle passed all 21 new waves plus the preceding Dino boundary, player
  death/retry, first-breach failure/retry, partial defense batches, hazard phase cycles,
  physical track arrival, chapter completion, save/reload, replay/retry and unavailable
  Chapter 6 handling. It uses an isolated temporary save and injected defeats/objective
  completion; the normal save is untouched. Evidence: `Temp/CampaignValidation/chapter5-lifecycle.txt`.
- Runtime screenshots were inspected for narrow gate/terrain clearance, the rotating
  opening, rail/socket alignment, Firebreak and the rematch's side hazards. No runtime
  errors occurred during the lifecycle run. The initial asset-generation API timed out,
  but scene generation completed; all resulting scenes subsequently passed the tests.
- Live ten-second Editor captures kept the player alive without combat input. Firebreak
  wave 3 (94 initial enemies) recorded 601 samples, 16.63ms median main-thread frame,
  28.89ms maximum; Elite punch median .90ms and support median 1.98ms. Wave 5 (128 initial
  enemies) recorded 600 samples, 16.60ms median, 43.44ms maximum; Wizard collision job
  median .15ms. Both advanced 10.03 game seconds with time scale 1. At the final snapshot,
  all 394 respawn-request roots were outside the actual physics world. An earlier sample
  included visible landing bodies; a follow-up confirmed all 250 hidden pooled bodies
  then present were excluded. Evidence: `Temp/CampaignValidation/chapter5-profile.txt`.
  These captures include Editor overhead and are not target-device or worst-case combat
  benchmarks. The recorded Physics.Simulate marker is the GameObject physics marker,
  not the ECS simulation group's total worker cost.

## Remaining playtesting

Human clear times, level 48's 4-5 minute target, the boss rematch's 3-4 minute target,
encounter difficulty, and target-device performance require playtesting. OQ-001 hardware
and FPS acceptance remain unresolved. Controlled Editor probes do not establish these.

## Protected-zone clarification

The user excluded Elites from all protected-zone levels. Level 47 now has 22/26/26
normal enemies, with its final wave reduced to 24 Baselines and two Exploders.
The asset and authoring recipe agree; future plan entries in levels 57, 75 and 77
also omit their former Elite. OQ-020 is resolved, and ordinary Elite AI is unchanged.
Follow-up validation passed: five Chapter 5 tests and 17 protected-point tests,
including a scan of every authored protected-zone scene (legacy and campaign) for
fixed or weighted Elite entries. Unity compilation passed; the single-worker .NET
build passed with zero errors and 331 existing reference warnings. An initial parallel
.NET invocation returned failure without diagnostics; retrying with one worker succeeded.
A live level 47 Editor preview confirmed valid baked wave definitions, 22/26/26 actual
normal spawns, no Elite entities, no enabled replenishment and successful completion
after injected defeats. No runtime Console errors or warnings occurred. The normal
campaign save was untouched; Unity was left stopped on Bootstrap. Evidence:
`Temp/CampaignValidation/chapter5-defense-no-elites.txt`.
Chapter 5 implementation is complete; the human playtest targets above remain.
