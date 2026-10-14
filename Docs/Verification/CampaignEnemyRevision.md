# Four-enemy campaign revision

Date: 2026-10-14. Requirements: LOOP-002..009, LINK-001..007, DRAGON-001..007,
SNAKE-001..007, NINJA-001..006, PROTECT-001 and shared combat/lifecycle rules.

The accepted Campaign80Proposal.md revision is authored in 15 existing campaign
encounters and 48 wave assets. Introductions are LinkedPair 41, Dragon 52, Snake 61
and Ninja 71. Each earlier introduction recurs in every later chapter. Chapter
builders, exact profile guarantees, separate pair counts and opening hints match
the saved encounters. The revised large waves at 48/58/68/78 spawn together.

No shared enemy settings, prefabs, legacy content, catalog identifiers, saves,
build registration, objective support, static hazards or arena geometry changed.
Existing authored spawn rectangles are retained. Initial physical-body budgets
include two endpoints per pair and six orbs per Snake; clearance counts omit orbs.

## Checks

- Unity compilation passes. A serial `dotnet build Assembly-CSharp.csproj
  --no-restore -m:1` passes with zero errors; existing package warnings remain.
- All 20 `CampaignEnemyRevisionTests` cases pass: exact composition of every
  revised wave against the accepted table, no early appearances, every planned
  recurrence, finite simultaneous cadence, profile/prefab references, ammunition
  safeguards, Wizard-zone gates, six-segment tails and the two-slot Ninja cap.
- Editor application reuses saved wave GUIDs. Profiles are resolved after each
  scene switch to avoid Unity unloading assets held only by local variables.
  A post-application audit finds 48 revised waves and no invalid profile refs.
- Enemy regressions pass: LinkedPair 23/23, Dragon 38/38, Snake 32/32 and Ninja
  21/21. Together with the new campaign cases, 134 focused checks pass. Two
  pre-existing fixtures were corrected: the pair stretch uses the shared asset's
  actual maximum length (currently 14m), and the thin static Ninja obstacle is
  placed in a static PhysicsWorld body instead of an uninitialized dynamic body.
  Shared settings and runtime mechanics were not changed to satisfy these tests.
- The broader 65-case campaign run passed 59. Five failures were the existing
  barricade required-hit assertions (expected 4, authored 3); the sixth was the
  former level-61 armor assertion, now removed for the Snake introduction.
  Rerunning Chapter Seven then passed 4/5, with its remaining failure also the
  unchanged barricade assertion. Thus six broader tests remain blocked on that
  existing barricade mismatch, not enemy integration. Full spawn-bank connectivity,
  the four revised large-population tests and new cadence checks passed.
- The initial broad run caught the Editor profile-loading defect; it was corrected
  before the successful focused reruns and baked-world validation.

## Runtime verification

`CampaignEnemyEncounterCheck` drives the 15 revised encounters in the actual
baked/Unity Physics world using an isolated temporary campaign save. It checks
baked definitions and special components, finite spawning/clearance, introductory
launches and retry health/state, Ninja concurrency, wave advancement and completion.
It injects launches/defeats and shortens later pre-wave idle waits; the first
introductory delay is natural and saved delays remain three seconds. It clears
active occupants while pending warnings become safe, rather than treating warning
reservations as already activated enemies. It does not measure normal combat clear
times. The large arena samples include rendering, probe and Editor overhead and
are not an FPS acceptance claim.

The controlled run passed all 15 encounters and all 48 waves with 9,634 observed
advancing frames. All four introductions passed eligible special launches and
retry/full-health/clean-state checks. Snake lost exactly one tip on the injected
punch and restored six segments on retry. Pair endpoints launched together. Baked
definitions and special prefab components matched saved assets. Ninja ambush slots
never exceeded two. Loading the original first level after the final replay left
zero pair records, Snake heads/orbs, Dragons or Ninjas from the previous encounter.

For the four large arenas, completed-wave observation frame medians ranged from
16.55-19.54ms in this Editor run. The final wave at 78 had 136 authored clearance
roots plus 12 initial Snake orbs (148 physical bodies before defeats); 122 live roots
remained at the observation point. These are local observations with frame limiting,
pending safe activations and natural defeats, not a full-population CPU/GPU benchmark.
Evidence is in `Temp/CampaignEnemyValidation/live-check.txt`.

Separate live previews confirmed the mint LinkedPair endpoints and visible tether,
and the protected cyan Snake head/six-orb tail with a highlighted gold tip, together
with the new opening hints. Captures are `linked-pair-live.png` and `snake-live.png`
in that same Temp directory. Unity was returned to Edit mode and the original
Gauntlet_27 Sub Scene after verification.

## Remaining human validation

Play all four introductions and their mixed follow-ups on keyboard/mouse and
controller for readability, flanking, tail access, rear-arrival timing and recovery.
Tune counts after measuring real clear times, especially Snake-containing waves.
The 4-5 minute large-arena targets remain provisional. OQ-001 target hardware/FPS
is unresolved; controlled Editor observations do not settle it.
