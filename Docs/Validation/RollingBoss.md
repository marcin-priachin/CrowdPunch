# Rolling Blob / Gauntlet_21 validation

Scope: ROLL-001..007 and shared COMBAT-007/012/014/015/018, PLAYER-003/004/005, LOOP-006.

## Automated checks

- Unity 6000.3.10f1 script compilation passes. `dotnet build Assembly-CSharp.csproj --no-restore`
  passes with existing package assembly-conflict/deprecation warnings and zero errors.
- `RollingTestRunner.Run()` runs the Blob, Chicken, Gatekeeper, player body-impact, homing
  and authored progression suites. Result: **119 passed, 0 failed, 0 skipped**.
- Blob cases cover pause/wind-up vulnerability and roll rejection; direct-punch immunity;
  immutable surviving timers; threshold overflow and next-cycle tuning; tracking/commit;
  both ending/aim/ownership/direction/target/contact modes; maximum-duration safeguard;
  actual homing; combined impact/blast, temporary protection, re-punch and pooled lifetimes;
  any-source explosion integration; armored/elite resistance; deferred lethal launch damage;
  sustained-contact history pruning; defeat completion once with survivors; reset and replenishment.
- Authored progression checks now cover twenty-one ordered levels and Gauntlet_21's scene,
  bounds, spawn regions, build registration and selector entry. Earlier boss suites remain green.

## Controlled live Editor check

`RollingPlayCheck.Start()` restores player health, places physical bodies, commits test rolls
and injects the final damage. It is a physics/lifecycle probe, not a human balance playtest.
The saved log is `Temp/RollingValidation/playcheck.txt`.

- The actual baked encounter advances pause/wind-up/roll, with more than 700 observed frames,
  real static redirects, three bounded Baselines and two sampled skinned meshes.
- Perimeter reflection at final-stage speed counts one entering collision; interior obstacle
  re-aim acquires player direction and does not count outgoing sustained contact repeatedly.
- A real solver crowd contact damages/relaunches a Player-owned body as a fresh Boss-owned body.
- A real launched body damages a vulnerable pause without resetting its remaining timer;
  shared rebound clears boss homing.
- Defeat completes the final gauntlet immediately with survivors and stops replenishment.
- Retry restores full health, first stage, empty history and a bounded crowd; switching to
  Chicken removes the Blob, and returning creates a fresh encounter.
- Top-down live capture confirms broad lanes, three solid obstacles, enclosed rails and both
  Blob body/spikes rendering with state tint. Capture uses a temporary Play Mode camera change.

The live pass exposed and fixed an index-shift defect in expired contact-history pruning.
A dedicated regression now protects continuing contacts while earlier contacts expire.
Both source skinned meshes now use compute-deformation materials and a shared spin pivot;
the initial unsupported-material warning has been resolved.

The final live rerun reports no runtime errors. A pre-existing player-death presentation
warning can still occur after the probe releases its restored player: `PlayerHitAnimation`
tries to play on the deactivated player Animator. It does not come from boss baking/rendering.

## Representative encounter profile

At one boss plus the authored three Baselines, 240 live Editor frames of ProfilerRecorder
samples measured average per-frame markers: crowd collision collection/resolution .0358ms,
movement/capsule sweeps .0112ms, player impact .0131ms, damage .0219ms, cycle .0049ms,
presentation .0032ms and animation scheduling .0033ms. Scripts and Burst categories are
reported separately in `Temp/RollingValidation/profile.txt`; zero entries in the unused
category are excluded from the numbers above. This excludes asynchronous animation job
execution, GPU rendering and other gameplay/physics costs. It is a local encounter-cost
sample, not an end-to-end large-crowd or target-hardware benchmark; OQ-001 remains open.

## Human playtests remaining

- Validate 2-3 minute duration, health/damage and stage pressure with keyboard/mouse and gamepad.
- Check color readability and rotating model silhouette from the normal gameplay camera.
- Try alternate settings against corner approaches, a player behind interior obstacles, Armor,
  Elites and Exploders in custom wave compositions; the authored encounter intentionally has Baselines.
- Validate larger custom crowds on target hardware. The authored encounter's intended population is three.
