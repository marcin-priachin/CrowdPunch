# Boss encounter validation

Validation resumed on 2026-09-23 in Unity 6000.3.10f1, from the boss implementation in commit `e31547c`.

## Scope and reproduction

The encounter implements BOSS-001 through BOSS-008 in gauntlet 11, The Gatekeeper.
Open `Assets/CrowdPunch/Scenes/Bootstrap.unity`, enter Play mode, and select
`11 The Gatekeeper` in the existing pause menu to play it directly.
The ordinary progression reaches it after gauntlet 10.

`CrowdPunch.Tests.BossTestRunner.Run()` runs the focused Unity EditMode suite and writes
NUnit XML and a summary to `Temp/BossValidation`. The suite includes boss encounter,
gauntlet progression, launched-body/player impact, and Dasher obstacle regressions.

**Crowd Punch > Levels > Run Sequence Lifecycle Smoke Check** starts the full live
progression probe from Edit mode. It hands off to `BossEncounterPlayCheck` after the ten
ordinary gauntlets. `BossEncounterPlayCheck.Start()` runs just the boss probe with
Bootstrap open. Evidence is written under `Temp/GauntletValidation` and
`Temp/BossValidation`. These directories are temporary; retained results belong here.

The live probes restore player health, inject damage to clear ordinary waves and cross
boss thresholds, and place existing bodies for isolated physics checks. They verify
runtime wiring and lifecycle behavior. They do not measure player skill, encounter
difficulty, or the BOSS-009 duration target.

## Automated results

- Focused Unity EditMode run: **49 passed, 0 failed, 0 skipped**.
- `dotnet build Assembly-CSharp.csproj --no-restore`: **0 errors**, 141 warnings.
  These match the previously recorded package-reference conflicts and legacy
  ColliderAspect/IAspect warnings; the successful build alone does not validate baking.

Coverage includes player ownership and propagated ownership, fresh punch reclamation,
duplicate-contact suppression, invulnerability, direct-punch/explosion/projectile
exclusion, Dasher eligibility, hand stagger protection, threshold clamping and attack
cancellation, replenishment gating, completion signaling, soft reset, and rounded-route
continuity (BOSS-001 through BOSS-007).

Retained evidence: [NUnit results](BossEncounter/editmode-results.xml) and
[summary](BossEncounter/editmode-summary.txt).

## Live results

The full [progression probe](BossEncounter/progression-smoke.txt) passed all ten ordinary
gauntlets and 39 waves, then advanced to gauntlet 11 (BOSS-007, LOOP-006). It checked exact
authored compositions, generation ownership, court containment and a peak population of
30 active ordinary enemies. Elite-wave normals pooled and returned as the same entities;
replenishment stayed disabled after the elite died.

The [boss probe](BossEncounter/playcheck.txt) then completed:

- Baked head and two hands remained inside bounds, without ordinary Enemy or
  EnemyLaunchState components. The supporting population was exactly 12 Baseline and
  one Ranged, with no duplicate living special (BOSS-005/006).
- Slam, lunge and sweep were observed in every stage, followed by recovery and the
  shared opening. The perimeter route advanced to 21, 42 and 56 metres at the three
  stage checkpoints (BOSS-002/004/005).
- An existing grounded body, placed and given player-owned launch velocity by the
  probe, physically collided with and damaged the head. The corresponding boss-owned
  body did not damage it. A hand placed across the shot physically blocked head damage
  and entered stagger (BOSS-001/002/003). These use the solver, not a direct resolver call.
- The Ranged support enemy was defeated, pooled and safely returned as the same entity;
  its living population never exceeded one (BOSS-006).
- Head defeat signaled completion exactly once while supporting enemies remained.
  Restart removed the old head and reset health, stage, hit count and completion.
  Selecting another gauntlet removed boss and supporting crowd entities. Returning,
  dying and retrying restored both player and boss (BOSS-007).

The probe sampled 5,490 Editor frame intervals at the authored 13-body boss population:
median **15.55 ms**, p95 **20.70 ms**. This includes Editor overhead and is not a standalone
build benchmark or an isolated per-system CPU measurement.

A [gameplay-camera capture](BossEncounter/live.png) was visually inspected: the separate
head/hands, supporting crowd and world-space slam telegraph render. Camera renders omit
the screen-space overlay canvas, so this image does not verify the health-bar UI.

Unity reported **no errors or exceptions** during the live run. The death/retry check
produced the existing `Game object with animator is inactive` warning from
`PlayerHitAnimation.CancelReaction()` during player deactivation. All lifecycle
assertions still passed. Play mode was stopped after verification; temporary placements,
injected damage and system overrides were discarded with the runtime world.

## Remaining playtesting

BOSS-009 targets approximately 2-3 minutes without a forced timer. This requires normal
input-driven playtesting; injected threshold hits cannot establish it. Keep the current
tuning provisional. OQ-001 still leaves representative target hardware, crowd size and
frame-rate acceptance unresolved; Editor timing is descriptive evidence only.

## Head rebound direction - 2026-09-23

After a launched body contacts the head, `BossHeadBounceSystem` changes only a
player-bound horizontal rebound into a sideways direction. It runs after the existing
launched-body/player impact check, preserves the solver's horizontal speed and vertical
motion, and clears a homing lock on the head. Already safe rebounds are unchanged
(BOSS-001, COMBAT-004/015). This is an implementation of the player's requested
head-rebound behavior, not a new general collision rule.

The focused [EditMode run](BossEncounter/head-bounce-editmode-results.xml) passed **52/52**
tests, including direct player-bound, already safe and off-center rebound cases.
`dotnet build` of runtime and Editor assemblies passed with zero errors. The
[live boss probe](BossEncounter/head-bounce-playcheck.txt) completed all stages and the
existing ownership, shielding, replenishment and lifecycle checks. On the physical
player-owned head hit, the rebound retained **17.35 m/s** horizontal speed and had
**0.000** normalized velocity component toward the player. The already edited boss
movement speed of 6 was preserved in the asset. The probe restores player health and
injects threshold hits, so this remains behavior verification rather than balance evidence.
