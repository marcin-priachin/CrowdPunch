# Sticky auto aim - 2026-11-06

Implements the user's selected approaches 1 + 3 under PLAYER-003/004: wider
retention with score hysteresis, and fast switching after deliberate look input
toward a centred challenger. Input-specific balance remains open under OQ-017.

## Saved settings

`Assets/CrowdPunch/Data/Settings/PlayerPunchSettings.asset`:

| Setting | Value |
| --- | --- |
| Use Player Direction Only | Disabled, enabling initial assistance |
| Assist range | 22m (unchanged) |
| Acquisition / release cone | 24 / 50 degrees |
| Passive challenger score improvement | 0.2 |
| Ordinary challenger delay | 0.25s |
| Deliberate switch centre cone | 6 degrees |
| Minimum alignment-improving look turn | 3 degrees |
| Recent input memory | 0.2s |
| Deliberate challenger delay | 0.04s |
| Distance weight | 0.35 (unchanged) |

These are provisional tuning values. All are exposed in Player Punch Settings.
Margin zero removes passive score hysteresis. Centre cone zero disables fast
switching. Fast delay is capped by ordinary delay; ordinary delay zero permits
immediate replacement once the applicable score/intent gates pass. The facing-only
toggle and range zero still disable assistance without erasing authored tuning.

## Behavior and ownership

Initial acquisition retains existing ray/fallback and Gatekeeper head priorities.
For a retained ordinary lock, marginal challengers cannot replace it. A clearly
better passive challenger must persist through the ordinary delay. A challenger
inside the centre cone can use the fast delay if recent actual look input improves
alignment enough. This explicit override can bypass the passive margin but still
requires a better score, allowing fast switches between closely spaced targets.
Moving the player, punched body or target, camera follow and shake do not themselves
create look intent. Retained targets continue to update their aim solution.

CameraFollow publishes input-commanded signed orbit degrees through PlayerEcsBridge.
The same angular criteria apply to mouse and gamepad. ECS keeps input history,
starting facing, turn timing, current lock and challenger per punchable source.
Direction reversal restarts the turn; idle input expires; a target change consumes
the turn. A bridge reset sequence discards intent when previews clear even when no
fixed step runs while paused. Leaving the punch volume/protection clears source
state through the existing path. No shared player-level target memory or
outside-limits grace period is introduced.

Hard range/release limits, obstruction, protection, target validity and Gatekeeper
ordinary-delay priority remain authoritative. No hit-volume expansion, physics
velocity change, launch ownership change or preview/click commitment change is
introduced. Enemy bodies and returned Chicken projectiles share UpdateAim.
No additional crowd scan or physics query is added; challenger comparison checks
only the two already-selected entities. Metadata, scene and prefab bindings remain.

## Verification

Unity 6000.3.10f1 compiled the updated runtime and Editor tests without compilation
errors. `dotnet build Assembly-CSharp.csproj --no-restore -v quiet` passed with zero
errors and the same 318 existing package/version/deprecated-API warnings. Build log:
`Temp/StickyAimRetention.build.log` (local, untracked).

All 257 checks passed:

| Editor suite | Passed |
| --- | ---: |
| StableLaunchAimTests | 42 |
| PlayerPunchCooldownTests | 16 |
| AuthoritativeLaunchTests | 79 |
| CommittedLaunchTests | 20 |
| ChickenBossTests | 48 |
| SnakeEnemyTests | 52 |

New coverage includes moving-target retention and hard release, marginal facing-ray
challengers and source movement, deliberate left/right switches at 4 and 12 degree
separations, passive/wrong-way/tiny input, centre-cone gating, input expiry/reversal,
pause-clear invalidation, immediate invalid-target replacement, source-volume reset,
configured fast-switch disable, settings-to-bridge wiring and displayed-direction
commitment following a fast switch. Existing tests preserve physical propagation,
Gatekeeper shielding/priority, protection and returned-projectile behavior.

The existing representative 100/1,000-body aim-plus-preview timing checks both ran
and passed with eight punchable sources over 100 measured updates after warmup.
These are Editor microbenchmarks without full simulation/rendering, not evidence
of a player-build frame-rate target. Bootstrap scene inspection confirmed the shared
settings asset and a camera target carrying PlayerEcsBridge; live asset inspection
confirmed assistance enabled and the saved cone/delay values.

No human balance playtest was performed. In play, compare strafing with no look
input against rotating toward another body, including adjacent targets, both input
devices, dash, dense moving crowds and scene/pause resets. Confirm 50-degree retention
feels helpful and 0.04-second deliberate switching remains readable in the existing
trajectory preview. No OQ-001 performance target or OQ-017 input preference is inferred.
