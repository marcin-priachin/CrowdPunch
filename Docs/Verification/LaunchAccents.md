# Launch accent verification - 2026-10-28

Scope: VISION-004/005/006. Presentation of existing launches only; no launch force,
collision, damage, recovery, encounter or player-control changes.

## Settings

`Assets/CrowdPunch/Resources/CombatFeedbackSettings.asset`, Inspector **Launch accents**:

| Independent toggle | Default | Associated tuning |
| --- | --- | --- |
| Launch Departure Puff | On | 0.18 seconds, scale 0.8 |
| Launch Speed Line Boost | On | 0.13 seconds, opacity x2 (clamped), brightness x1.5 |
| Launch Body Snap | On | 0.18 seconds, compression 0.22, stretch 0.38 |

Existing speed-line values and profiles remain unchanged. Each toggle can be changed in
Play mode. Snap changes apply through the bridge on the next presentation update. Disabling
puffs clears active departures; disabling the boost restores baseline line shading. Launch
identity is consumed while presentation is unavailable, preventing an old departure on resume.

## Performed checks

- Unity 6000.3.10f1 compilation completed without errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:q` passed: zero errors,
  318 existing package-conflict, obsolete-API and legacy warnings.
- CombatFeedbackTests: **18/18 passed** after the final runtime edits. Cases cover direct
  and collision launch identity, re-punch, ordinary contacts, no trail capacity, presentation
  disable/resume, independent toggles, line boost expiry, shared materials/profiles, constant
  trail dimensions, animated-body compression/stretch/restoration, no accumulated scale,
  preserved physics transform/velocity, departure saturation, expiry during timeScale zero,
  cleanup and the existing impact/camera/time-ownership behaviour.
- CombatVfxTests: **2/2 passed**.
- EnemyAnimationTests: **116/117 passed**. The remaining assertion is unrelated to launch
  feedback: `Info004DasherActiveAnimationPreservesAuthoredScaleAndGroundClearance` compares
  the unchanged EnemyDasher animator root scale (1.3) with its imported model scale (1.0),
  at EnemyAnimationTests.cs:1308, before exercising runtime presentation. No prefab/model
  asset or authored animation sample was changed for this feature.
- Controlled Play Mode validation in the existing Campaign_09 scene used ten existing
  baked Baseline enemies, a temporary player bridge/camera and a line of seven bodies.
  A bridge punch entered the real punch/physics/propagation systems. Paused inspection
  confirmed one PlayerPunch launch, one EnemyCollision launch and chain depth two; both
  emitted their own departure feedback. Rendered captures of departure and chain moments
  were inspected with the authored village environment and skinned Goblin models.
  A live trail property block read opacity 0.6 and brightness 1.5 against the saved 0.3/1.0
  baseline during the launch boost. No new gameplay runtime exception was observed.
- Temporary runtime changes were discarded by stopping Play mode. Synthetic profiling
  objects were destroyed, the original clean Campaign_09 scene was reopened without saving,
  and the Editor was left stopped. The known Sidekick ToolDownloader window error remains
  unrelated; initial Play-mode loading also caused tooling request timeouts.

## Bounded-load measurement

Synthetic Editor worlds, one render entity per crowd body, 10% newly launched each update,
20 warmup and 100 measured updates per setting. The measured region includes the existing
combat-feedback bridge/bounds aggregation, the impact/snap render job and completion, 48
trail slots and departure mesh submission. Fixture creation and launch-state writes are
outside the timed region; animation sampling, physics, skin-probe measurement and GPU cost
are excluded. The latter paths retain their existing implementation and budgets.

| Crowd / launches per update | Three accents | Mean CPU per update | Maximum | Calling-thread allocation over 100 updates |
| --- | --- | --- | --- | --- |
| 1,000 / 100 | Off | 3.84 ms | 5.75 ms | 0 bytes |
| 1,000 / 100 | On | 5.40 ms | 7.91 ms | 0 bytes |
| 3,000 / 300 | Off | 10.70 ms | 16.96 ms | 0 bytes |
| 3,000 / 300 | On | 10.38 ms | 16.53 ms | 0 bytes |

These are noisy Editor snapshots of the combined presentation path, not an isolated effect
benchmark or a full-frame/target-hardware result; the 3,000-body reversal illustrates that
noise. The departure pool stays at 24 slots / one renderer / at most 8,640 submitted vertices
with saved defaults, accepts at most eight new departures per update and drops excess
decoration. Body snaps remain independent of trail/departure slot availability. Authored
animated models outside the snap window take an early-out after any required restoration.

No standalone player build, GPU profile or full encounter playthrough was performed.
OQ-001's target hardware/performance acceptance remains open. Audio language remains
unchanged under OQ-019.

Ignored local inspection artifacts: `Temp/LaunchAccents/departure.png`,
`Temp/LaunchAccents/chain.png`, `Temp/LaunchAccents/build.log` and
`Temp/LaunchAccentsProfile.cs`.
