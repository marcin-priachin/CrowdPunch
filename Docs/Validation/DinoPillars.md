# Dino Pillars / Gauntlet_22 validation

Scope: PILLAR-001..006, COMBAT-007/014/015/018, PLAYER-003/004 and LOOP-006.

## Original content and defaults (2026-10-08)

Select **22 Dino Pillars** from the existing level menu or Level Play window.
The authored sequence appends Gauntlet_22 immediately after Rolling Blob.
The enclosed 40m arena has three 9m pillars at (-8,-3), (8,-3), and (0,7) in XZ,
plus eight replenishing Baselines. The wave asset owns composition and its 3s respawn delay.

`Data/Settings/DinoBossSettings.asset` defaults to three successful pillar hits,
toward-boss falling, damage-and-push, 1.35s falls, .65s fallen visibility and a 4s
regeneration delay after disappearance. Chase speeds are 3.8/4.5/5.2m/s and chase
intervals 7/5.5/4s. Warning lasts 1s while chasing, burst lasts 2.2s at 1.9x speed,
turning changes from 180 to 38 degrees/s, and stagger lasts 1.3s.
One/two-hit victory, incoming-direction falls and Environment-owned ordinary launches
are available in the same asset. Rebuild the encounter after changing authored body/pillar
geometry so the fitted visual model/shaft matches the baked collision shape.

## Automated checks

- Unity 6000.3.10f1 script compilation succeeds.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1` succeeds with zero errors
  and 344 existing package assembly-conflict/deprecation warnings. Single-node MSBuild
  avoids the environment's intermittent project-reference discovery failure.
- `DinoTestRunner.Run()` runs Dino/pillar, Rolling, Chicken, Gatekeeper, launched-player
  impact, homing and authored progression regressions: **140 passed, 0 failed, 0 skipped**.
- Twenty pillar cases cover ownership gates, source pass-through under the alternate
  launch response, locked fall directions, rotating-shape/fast-crossing contacts,
  once-per-fall damage, Environment ownership, push/launch alternatives, stage/cycle
  behavior, 1/2/3-hit defeat, stagger refresh, consumption, delayed safe regeneration,
  ordinary crowd push without damage/launch, targeting, reset, completion and replenishment.
- Progression tests validate all twenty-two scene entries, bounds, regions and build order.

## Controlled live Editor check

`DinoPlayCheck.Start()` runs physical shots and lifecycle checks in the actual baked encounter.
It restores player health, places test bodies at their cached grounded height, holds the boss
for geometry checks and repositions the player through the normal entry-point API.
This is a controlled verification probe; it does not measure player difficulty or fight duration.

The probe verifies:

- One baked Dino, exactly three pillars, eight bounded Baselines and generated sampled animation.
- Live chase, moving warning and burst transitions, and pursuit around an upright pillar.
- A real swept Player-owned launched body topples a pillar and continues with its launch intact.
- Solid collision disables before the fall; the rotating shape damages Dino exactly once.
- A successful pillar remains consumed; an incoming-direction miss disappears and returns.
- Regeneration waits while the player occupies the space and clears ordinary crowd bodies
  without starting launches before restoring collision.
- Three physical pillar hits complete immediately with surviving support and stop replenishment.
- Retry restores full boss health, all upright pillars, empty histories and eight support bodies.
- Selecting Rolling removes all Dino/pillar state; returning creates a fresh encounter.

The test setup initially placed a source below its floor and bypassed the player's pending
movement handshake. Correcting the probe to use cached ground height and SetLevelEntryPoint
resolved those probe failures. The final live pass reports no gameplay runtime errors.
During Editor reloads, the existing Level Play preview window can emit the URP
VolumeComponent.OnDisable null-reference exception; it does not occur during the live encounter.
The existing player death presentation warning may occur after controlled health restoration stops.

Saved evidence: `DinoPillars/playcheck.txt`, `DinoPillars/editmode-summary.txt`,
`DinoPillars/contact-benchmark.txt`, and `DinoPillars/Overview.png`.
The overview uses a temporary Play Mode camera change; authored camera settings are unchanged.

## Crowd cost sample

An isolated ECS benchmark uses three simultaneous late-fall pillars with fresh contact
histories each update, 40 warmups and 240 measured updates. The final Burst collector path
measures .3391ms mean (.6698ms maximum) at eight enemies and 6.1555ms mean (11.3104ms maximum)
at 200 enemies. This deliberately stresses repeated fresh damage resolution, including
history and shared damage writes. It excludes setup/reset, Unity Physics, animation, rendering
and target-hardware performance; it is not a complete game-frame benchmark.
The authored encounter remains a small crowd. Large custom compositions need profiling and
further tuning; OQ-001's performance targets remain unresolved.

## Local pillar crowd update (2026-10-09)

`Enemies Per Pillar` in `DinoBossSettings.asset` defaults to **2**, adding six local
Baselines alongside the existing eight wave bodies. `Pillar Crowd Radius` defaults to
4m. Local bodies use ordinary chase, contact and separation within that radius;
ordinary launch/recovery physics remains intact and displaced Active survivors return
through movement intent. The same bounded slots safely respawn nearby using the wave's
three-second delay. Temporary launch, recovery, death and blocked placement can leave a
slot unavailable until it returns; they do not generate unlimited replacement bodies.
Missed pillars retain their local crowd. Successful pillars release surviving members
to ordinary AI and disable their local respawning; boss defeat stops all replenishment.
Zero enemies per pillar disables the additional crowd.

The current edited arena, pillar placement, model size and boss movement tuning are
preserved. A separate live check uses those actual authored positions to verify six
local bodies, movement back after displacement, local respawn after ordinary lethal
damage, consumption shutdown and exact slot recreation on retry. Evidence is saved in
`DinoPillars/pillar-crowd-live.txt`.

Current verification: **151 related boss/progression tests pass**, including all 31
pillar cases; five additional crowd-distribution tests pass. Unity compilation and the
single-node C# build pass with no new warnings. Scene validation now transforms floor
vertices into world space before checking bounds, so scaled authored floors are checked
correctly. The original `DinoPlayCheck` physical-shot recipe assumes its original pillar
coordinates; the local-crowd live check uses the current edited layout.

Repeated live-world system measurements (20 warmups + 120 samples) give mean supply/
positioning costs of .0500/.0030ms at fourteen enemy roots and .0754/.0028ms at 200 roots.
The synthetic extra roots are removed immediately afterwards. These measurements exclude
physics, rendering and other AI; supply is measured with existing filled local slots.
See `DinoPillars/pillar-crowd-profile.txt`.

The movement correction removes fixed standing destinations. Ordinary movement goals and
speeds remain unchanged inside the circle, while outside goals are clipped and outward
motor velocity is limited at its boundary. Launched/Recovering bodies remain unconstrained.
The live check with the current configured 8m radius observed two local bodies moving up
to 12.022m from their initial positions over 482 updates; their maximum measured radius
was 8.034m (solver tolerance .4m). Player movement was scripted inside the area and Dino
was held in stagger, while enemy positions and velocities were not injected. Evidence:
`DinoPillars/pillar-roaming-live.txt`.
Repeated system timings at 14/200 enemy roots measured mean positioning .0028/.0028ms
and shared motor .0055/.0851ms, excluding physics/rendering and other AI. Evidence:
`DinoPillars/pillar-roaming-profile.txt`.
The movement suite covers unchanged ordinary/committed intent inside the area, clipped
outside goals, shaft detours, tangential/inward boundary motion and unrestricted launched
and recovering velocity. Results: `DinoPillars/pillar-roaming-tests.txt`.

## Human playtesting remaining

- Tune pursuit, pillar timing and spacing together from the normal gameplay camera.
- Check burst/stagger colors, fall readability and keyboard/mouse/gamepad positioning.
- Try the incoming-direction and launch alternatives with custom Armor, Elite and Exploder
  compositions, preserving their established resistance and damage rules.
- Profile larger custom crowds on the intended target hardware.
