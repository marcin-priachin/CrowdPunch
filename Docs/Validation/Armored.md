# Armored and Gauntlet_12 validation

Validated in Unity 6000.3.10f1 on 2026-09-25. Requirements: ENEMY-014/015,
PLAYER-004/009, COMBAT-014/017, INFO-001/004, LOOP-002 and BOSS-007.

## Implemented content and defaults

- Armored is serialized archetype 5, Normal tier, with two shield stages and no health bar.
  A small overhead row displays one shield icon per remaining stage and hides at zero.
- Protected punches confirm cooldown without gameplay effects or an originating trajectory preview.
  Launched bodies of any ownership and explosions strip stages; source-launch identity and a shared
  protection deadline deduplicate body/blast events and multi-contact bursts.
- Both shield hits: no health loss or launch, 0.30 s stagger and 3 m/s planar recoil.
  Protection lasts 0.25 s, including after the last shield disappears. A subsequent eligible
  hit uses ordinary damage/launch.
- The model keeps one slate tint (0.55, 0.60, 0.66) at every armor stage. Transient hit flashes
  still play. The shield count uses a shared Sprite on the existing pooled screen-space Canvas.
- Authored profile: 30 health; 8 m/s movement times 1.25 charge multiplier = 10 m/s pursuit.
  Navigation/separation remain active; armor stagger preserves recoil velocity.
- Orc_Skull uses embedded Idle, Run, HitReact, Jump_Land and Death clips in sampled ECS animation.
  Two skinned renderers bake CPA3 samples (32 phases/motion), with no runtime crowd Animator.
- Gauntlet_12, "Crack the Shell": 1 Armored + 6 Baselines, then 3 Armored + 12 Baselines.
  Nature-kit arena, baked navigation, open shot lanes, opening hint, twelve-entry selection and progression.
  The boss advances here; this level currently ends the authored sequence.
- Opted-in waves supply one safe Baseline when protected armor remains without usable ammunition.
  The check runs at most 4 Hz after pending initial spawns drain, uses shared spawn profiles, and
  includes supplemental bodies in defeat accounting. Repeated replacement keeps one spare root per wave.

## Automated regression and compilation

[Unity EditMode results](Armored/editmode-results.xml): **148 passed, 0 failed, 0 skipped**.
Coverage includes shield stages, damage after shield removal, surviving recovery, blocked punch confirmation,
unarmored punches/re-punches, assist targets, source-launch and invulnerability deduplication,
explosive event orders (including last-shield hits), launched Dasher ownership and gentle contacts,
elite selection/stale reservations/area resolution, recoil preservation, pooling, ammunition
eligibility, shield-count UI across break/reuse/pooling, prefab physics, sampled animation,
navigation, boss rules and twelve-level scene wiring.

`dotnet build Assembly-CSharp.csproj --no-restore -v:q`: 0 errors, 140 existing warnings.
Unity imported/compiled the new code, generated prefab/samples/materials, and baked the actual scenes.

## Live Editor verification

[Controlled Play Mode log](Armored/playcheck.txt) passes the full sequence:

- Real Unity Physics solver impacts with player-, elite- and boss-origin launch causes remove one
  shield each for the first two hits, preserving health/Active phase. The third hit launches and
  later applies damage without a shield indicator.
- A surviving unshielded enemy recovers without armor. Restart restores two shields.
- A geometrically connected protected punch reports a hit, writes no impulse and produces no preview.
- Three consecutive ammunition shortages each produce exactly one owned replacement; cumulative
  undefeated count stays correct. The next wave contains fifteen enemies and final completion succeeds.
- Boss defeat triggers the real progression signal, loads Gauntlet_12 and removes boss/crowd ownership.
- Baked Armored render entities have valid sample blobs and finite skin matrices.
- A [Game View capture](Armored/shield-gameview.png) shows three otherwise equally tinted
  Armored enemies with two, one and zero shield icons directly above their models. These
  states were arranged for visual inspection after live hit-count verification.
- The latest live check also verifies the shield count at spawn, after each real solver hit,
  on break, and after restart.

The probe positions bodies, injects launches/defeats and restores player health. These are real
scene/system/physics checks, not evidence of a hands-off or human-played balanced run.

## Representative crowd cost

[250-body capture](Armored/performance.txt): 250 active enemies, including 49 Armored, after five
seconds of live simulation. Twenty warmups and 120 completed updates per system, with job completion:

| System | ms/update |
| --- | ---: |
| Chase and separation | 0.047 |
| Navigation | 0.024 |
| Movement | 0.073 |
| Collision, settled contacts | 0.064 |
| Hit-history cleanup | 0.004 |
| Aim assistance | 0.451 |
| Readability | 0.068 |
| Sampled animation | 0.056 |
| Health/shield canvas bridge | 0.292 |

No managed allocations on the measuring thread across each 120-update sample. Injected enemies
were removed afterward; root count returned to the original seven. Histories retain only current
source launches/explosions and are cleared when those lifetimes expire, rather than accumulating
permanent pairs. The encounter safeguard adds no scan to encounters that do not opt in.

These measurements freeze physics/time during individual system measurement. They do not establish
whole-frame FPS, standalone performance, burst-collision throughput, encounter difficulty or duration.
Those remain follow-up profiling/balance work; unrelated design questions remain unresolved.

## Reproduction

Run `CrowdPunch.Tests.ArmoredTestRunner.Run()` in the Editor for the regression suite.
Run `CrowdPunch.Editor.ArmoredEncounterPlayCheck.Start()` outside Play Mode for the controlled
scene check. It stops paused after the boss-to-12 transition. Let the new scene finish spawning,
then `CrowdPunch.Editor.ArmoredCrowdPerformanceCapture.Start()` captures the mixed-crowd probe.
Transient logs/images are under `Temp/ArmoredValidation`; retained evidence is linked above.
Rebuild content using "Crowd Punch/Enemies/Rebuild Armored Prefab",
"Crowd Punch/Enemies/Rebuild Armor Shield Icon", and
"Crowd Punch/Levels/Build Armored Gauntlet 12".
