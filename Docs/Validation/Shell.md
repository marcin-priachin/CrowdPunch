# Shell / Gauntlet_15 validation

Date: 2026-09-29. Requirements: SHELL-001..006, PLAYER-003/004/009, LOOP-006.

## Implemented

- Saved Gauntlet_15, "Crack the Shell", appended to Bootstrap selection and Build Settings.
- Three configurable exploder blasts permanently expose a five-health core. Its default matches
  the current Baseline health. The breaking blast leaves that health untouched.
- Direct punches confirm normal cooldown in both phases: blocked shell feedback, ordinary core damage.
- Shared swept solid impacts, non-exploder rebound, explosive detonation, ordinary core collision
  damage, once-per-launch core impact history, and separate explosion damage.
- Same aim locks, homing, collision bridge and initial-direction preview; no new bounce prediction.
- Twelve Baselines replenish until core destruction. Two exploder slots replenish only after none
  remain, with a two-second delay plus any unfinished pooling/safe-placement time. Shell exposure
  cancels future guarantees while existing exploders remain.
- Navigation excludes the target footprint; both phases block movement. Core death completes
  immediately. Restart restores target/crowd/completion. No health bars or additional HUD.
- Shell cracks, shell debris, core tint/scars, distinct blocked/successful flashes, impact particles
  and a brief opening hint. Placeholder assets are intentional.

## Evidence

- Unity 6000.3.10f1 compiled the runtime and editor code without compilation errors.
- `dotnet build Assembly-CSharp.csproj --no-restore` passed with zero errors. Existing package
  assembly-version conflicts, deprecated aspect APIs and the learning-aspect unused field remain.
- `ShellTestRunner.Run()` runs 47 edit-mode cases: shell durability, phase isolation, impact/blast
  separation, per-launch deduplication, completion, replacement timing/slot reservation, baseline
  continuation, barricade/cover regression tests and all fifteen levels' scene/sequence validation.
  Result: 47 passed, zero failed/skipped. XML and summary: `Temp/ShellValidation/editmode-*`.
- `ShellPlayCheck.Start()` drove the actual baked Gauntlet_15 through the standard update groups.
  It checked its 14-root composition, blocked punch confirmation, aim validity, shell rebound,
  hybrid player blocking, unlaunched blast damage, no top-up with one survivor, pooled pair
  replacement, clean core exposure, continued Baseline replenishment eligibility, core rebound,
  separate impact/blast damage, no further exploder replacements, immediate win and fresh restart.
  Result: COMPLETE, all checks passed. Evidence: `Temp/ShellValidation/playcheck.txt`.
- A separate live sequence check selected Gauntlet_14, injected target destruction, then verified
  automatic entry into Gauntlet_15 with one fresh shell and exactly 14 crowd roots. The earlier
  objective and synthetic stress crowd were unloaded. Unity was left paused on this fresh level.
- Game-view images of intact shell, two-hit shell damage and exposed damaged core were inspected.
  Images remain under `Temp/ShellValidation`. Damage snapshots use injected state; the latter
  images also contain the synthetic stress crowd and are not representative encounter screenshots.
- Unity's Console reported no new gameplay/baking errors during the successful live check.
  Early tool calls were interrupted by editor domain reload; the first harness run also reset its
  timeout timestamp across Play Mode entry. The harness was corrected and rerun successfully.

The live probe injects actions/positions and temporarily raises core health to observe both exploder
damage events. It validates integration, not difficulty or normal input feel. It restores a fresh
level at completion. `ShellCrowdPerformanceCapture.Capture()` requires that paused level and adds
temporary stress entities; exit Play Mode after capture to discard them.

## Performance sample

Final Burst-enabled Editor sample: 20 warmups and 200 repeated updates in the baked collision
world, with a fixed synthetic launched crowd. Values are mean / p95 milliseconds.

| Roots | Shared target sweep | Exploder replenishment | Shell/core visuals |
| --- | --- | --- | --- |
| 14 (authored bound) | 0.0932 / 0.0963 | 0.0028 / 0.0028 | 0.0050 / 0.0050 |
| 128 (synthetic stress) | 0.5835 / 0.6048 | 0.0028 / 0.0028 | 0.0049 / 0.0051 |

These isolated system timings exclude whole-frame rendering/physics, editor overhead outside
the measured calls and real player input. They are not a frame-rate guarantee or a resolution
of OQ-001. Raw results: `Temp/ShellValidation/performance.txt`.

## Remaining playtests

1. Select **15 Crack the Shell**. Confirm the opening hint and that a punch feels blocked while
   still spending cooldown. Read one and two shell cracks from the normal camera.
2. Launch an exploder into the target and also lure an unlaunched explosion close to it. Both
   should count. The third blast exposes a full-health turquoise core; a new attack finishes it.
3. Keep one exploder alive and confirm its partner stays absent. Kill both away from the target;
   confirm the delayed pair returns at safe locations. Check continued Baseline pressure after exposure.
4. Shoot Baselines at both phases: rebound, subsequent crowd collisions and normal recovery should
   remain readable. Confirm aim-lock selection and preview agree around the target from all sides.
5. Test ordinary keyboard/controller input, death/retry and level selection. Automated checks
   cover reset, registration and the live Gauntlet_14 to 15 transition, but do not replace manual input testing.

No intentional design deviations. Baseline count (12), replacement delay (2 seconds), dimensions
(3.2 x 4 x 3.2 m), rebound (0.85), core health (5) and visual treatment are delegated tuning defaults.
Core damage visuals may be brief with default health/damage, since one strong attack can kill it.
Final art/audio, encounter balance, full player-build profiling and OQ-001 hardware/frame-rate targets
remain unverified future work. No new progression or weapon decisions were made.
