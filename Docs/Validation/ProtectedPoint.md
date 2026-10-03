# Protected point validation

Date: 2026-10-03. Unity 6000.3.10f1. Requirements: PROTECT-001..004, LOOP-006.

## Implemented and authored

Gauntlet_18 (Hold the Line) is selectable from Bootstrap after Violet Court. Its saved
scene/SubScene, floor, marker, dedicated settings and three wave assets are included.
Earlier level content and shared enemy profiles were not rebuilt. The builder command is
**Crowd Punch > Levels > Build Protected Point Gauntlet 18**; it reapplies this level's
layout/wave recipe while preserving an existing ProtectedPointSettings asset.

Tune breach threshold and maximum new player attackers in
`Assets/CrowdPunch/Data/Settings/ProtectedPointSettings.asset` (defaults 1 and 3).
Tune compositions, batch size/interval and delays in
`Assets/CrowdPunch/Data/Settings/Waves/Progression/CP18_*.asset`.
These baked settings require leaving Play Mode and reloading/rebaking the level.
Zone dimensions/centre are on ProtectedPointAuthoring in the SubScene; its matching
noncolliding ground mesh is scene geometry, not runtime UI.

## Verification

- Unity script compilation succeeds. `dotnet build Assembly-CSharp.csproj --no-restore`
  succeeds with zero errors and 271 existing package/deprecated-aspect warnings.
- Final Unity Editor suite: **281 passed, zero failed**, with Burst enabled.
- Editor regressions cover closest selection across archetypes, changing slots, range
  gates, zero cap, pooled/launched exclusion, objective intent despite the ordinary
  Explosive exception, committed Dasher/Wizard/Ranged attacks, all four breach phases,
  inclusive boundary/outside checks, stale ownership, linked-visual removal, exact-once
  wave accounting, threshold semantics, failure-before-win, restart and saved wave recipes.
  Existing progression tests also verify all 18 scene entries and level-18 spawn clearance.
- Live default-tuning run: an Active enemy naturally reached the zone. Breaches=1 at
  threshold=1 produced RunFailed, time scale zero, visible cursor and the existing menu's
  "PROTECTED ZONE BREACHED" / "Retry Level" presentation. [Screenshot](ProtectedPoint-failure.png).
- Retry was invoked through the actual menu button. A subsequent diagnostic run used
  runtime-only threshold=100 and attack cap=0, leaving saved assets unchanged. Every
  enemy advanced and breached naturally; no damage, deaths or enemy positions were injected.
  The three waves spawned exactly 16/24/32 enemies in four-enemy batches separated by
  three simulation seconds. Wave 2's first batch arrived five seconds after wave 1 cleared;
  the same held for wave 3. After 72 total breaches, no owned enemy roots remained and the
  existing RUN COMPLETE result paused the game. [Recorded cadence](ProtectedPoint-live.txt).
- Selecting First Line through the result menu left zero ProtectedPoint entities,
  cleared RunFailed/RunComplete and resumed time scale 1. Bootstrap was restored in Edit Mode.
- Isolated 32-enemy ECS timing, cap 3, every candidate in range, 100 warmups and 1,000
  measured calls: selection mean 0.0106ms, no-hit breach scan mean 0.0464ms on this Editor.
  [Timing details](ProtectedPoint-profile.txt). This excludes physics/rendering and is not
  a standalone-player or target-hardware frame-rate claim.

The scene peak assertion permits this level's agreed 32 enemies while preserving earlier
levels' 30-enemy ceiling. Enemy facing uses explicit job dependency scheduling. A pre-existing
Sidekick Tool Downloader editor-window error appeared when entering Play Mode; no
new gameplay/baking/system-order error was observed during the live defense run.

## Human playtests still needed

1. Use normal saved defaults and clear all three waves without assistance. Check whether
   the 96m approach, entry point and camera allow timely interception and readable chains.
2. Stand among more than three enemies. Check closest-slot changes across melee, Ranged,
   Wizard and Dasher, and confirm already-started attacks finish while other enemies advance.
3. Launch a body into the zone, move it back out during recovery, and let another recover
   inside. Only the enemy that becomes Active inside should breach.
4. Set the threshold to two, reload, and allow one breach: the enemy should disappear while
   play continues. The second should fail. Restore the default after tuning.
5. Exercise punches, propagated collisions, Wizard impact zones, and Armored shield breaking
   near the boundary. Wave 3 has finite ammunition; preserve bodies for the Armored enemy.
6. Retry after defeat and after player death, and switch between Gauntlet_17 and Gauntlet_18.
   Check controller navigation and that old enemies/zones do not survive the transition.

The diagnostic all-breach run verifies lifecycle and pacing, not normal difficulty or combat
balance. An unassisted victory, whole-run duration, standalone build and OQ-001 target-hardware
performance remain unverified. Ground graphics are a simple marker; final art/audio are future work.
