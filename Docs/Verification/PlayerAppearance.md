# Player appearance update

2026-10-21, Agent B workspace, branch `player-character-update`.

## Scope

VISION-005/006, PLAYER-002/004/005/008/009. Replaced the Sidekick visual hierarchy inside
`PlayerModel.prefab` with `Models/Player/rig.fbx` and its supplied base/normal textures.
The existing prefab root, Animator, movement and punch component identities are retained.
Its root visual scale is 2.35, upper chest 1.12/1.04/1.08, head 1.16 and both hands 1.4.
The production full-cel material uses low-strength normals, low smoothness and no metallic
shine. The single skin replaces the previous modular renderers.

Project-owned Heavy Fighter clips supply idle, forward/backward walking and punch.
Existing directional jogging fills the pack's missing strafe/diagonal directions; the
existing hit reaction remains. The upper-body mask preserves leg locomotion while punching.
Punch clip poses use ready frame 4 and contact/rotation-end frame 8 at 30 fps, with playback
multiplier 1.35, strike multiplier 1.85 and follow-through multiplier 0.85. These only control
presentation. Gameplay input, hit resolution, cooldown, movement, dash, collisions, campaign
settings, saves and ECS systems were not edited. Source asset binaries and Heavy Fighter
importers were preserved; only the new normal texture's importer was corrected to Normal Map.

Rebuild with **Crowd Punch > Presentation > Rebuild Player Appearance**.

## Verification

- Unity 6000.3.10f1 compilation: success, zero reported compiler errors/warnings.
- `dotnet build Assembly-CSharp.csproj --no-restore`: success, 0 errors, 498 warnings from
  package/reference version conflicts and existing obsolete APIs/unused fields.
- 15 focused Editor checks passed on the final assets: valid humanoid, disabled root motion,
  Always Animate, humanoid right-hand resolution, no visual colliders, one textured skin,
  production material, and 61 samples for each of the four Heavy Fighter clips. All sampled
  vertices were finite and inside the authored culling bounds; roots did not translate or
  rotate and retargeted hands moved. This was a temporary verification script, not an added
  NUnit suite.
- Final supplied clip floor minima in unit model space: Idle -0.0125, forward -0.0369,
  backward -0.0202, punch -0.0197. Slight source sole penetration remains during the stride.
- Campaign 01 ran with `EditorPreview=True`; frame advance was verified server-side
  (210 to 217). This established a live player loop before controlled pose inspection.
- In a subsequent isolated preview paused on level entry, an accepted `RequestPunch`
  advanced the bridge sequence immediately; ready/contact parameters were 0.2/0.4. A
  controlled positive-delta Animator evaluation moved the right hand 2.389737 world units
  from ready to contact while the player root remained (0, 0.5, 0).
- 300 explicit cooldown hand restore/apply updates retained scale (1.61, 1.61, 1.61).
  Hiding restored the authored (1.4, 1.4, 1.4); punch reset restored parameter 0.2.
- Inspected textured model and punch samples and the actual village gameplay camera, plus
  controlled close views. The temporary camera/player placement changes were Play-mode-only;
  Play mode was stopped afterward. No scene files changed.

This is automated rig validation and controlled Editor visual review. Human combat playtesting,
other chapter lighting reviews, standalone builds and target-device performance were not run.
Unity's previously imported Heavy Fighter pack reports source avatar bone-length warnings.
Tooling also logged a command timeout during an Editor reload; subsequent targeted commands
and the rebuild completed successfully.

## Upper-body movement correction

The follow-up request keeps the upper body's idle rotation during movement. The humanoid
upper-body mask retained locomotion's hip rotation, turning the torso despite the punch
override. The builder now samples 41 model-relative spine rotations with idle locomotion;
the existing punch presentation interpolates these before applying its yaw offset. This
anchors torso orientation without modifying hips, legs, player facing or gameplay. The
existing hit-reaction weight releases the correction during damage reactions.

Unity compilation passed with zero reported errors/warnings. The .NET build also passed
with 0 errors and 385 existing package/obsolete-API warnings using disabled build servers
and a single MSBuild worker after the default invocation exited without diagnostics.
A temporary Editor verification
compared 576 poses: nine movement directions, four gait phases, eight punch/cooldown times
and two root facings. Maximum spine, chest and head rotation differences against idle were
0.0000 degrees at the same punch time. Hip rotation still varied by 76.96 degrees; the
ready-to-contact torso turn remained 20.11 degrees. Another 300 presentation updates showed
no accumulated rotation, player-root position stayed unchanged, and disable restored the
uncorrected animated spine rotation. These are controlled Animator checks, not human playtesting.

## Contact-frame recovery and separate miss cooldown

The next user decision revises PLAYER-008/009: cooldown animation begins at Contact Frame
and plays forward to the clip's end, wraps to frame zero, then plays forward to Ready Frame
over the acknowledged outcome's cooldown. Both segments use one continuous clip playback
rate, so the full recovery fits that duration. It takes
priority over remaining strike playback. `PlayerPunch.CooldownStarted` starts the contact
pose immediately; `ConfirmContactPose` no longer restarts follow-through during an active
cooldown. The upper-body orientation correction continues to sample the same clip timeline.

`PlayerPunchSettings.asset` retains `cooldown: 1.25` for eligible enemy/intact barricade
connections and adds `missCooldown: 0.25` for misses. Both durations are independent, with
zero allowing immediate retry after acknowledgement. Hand feedback and animation normalize
against the selected duration. Hit-stop still only follows a confirmed connection; misses
do not receive impact feedback. Existing scene-specific ready-frame/playback settings were
preserved, including Bootstrap's Ready Frame 2 and rotation-end frame 6. The scene file was
already modified before this follow-up and was not edited by this implementation.

Validation: Unity compilation passed with zero reported errors/warnings; the .NET build
passed with 0 errors and 385 existing warnings. All 29 focused Editor tests passed:
8 `PlayerPunchCooldownTests`, 12 `BarricadeTests`, and 9 `CombatFeedbackTests`. New coverage
uses real bridge result events and checks independent timing/progress, pending/cooldown
request rejection, zero-duration retry, duplicate/stale acknowledgements, reset/disable,
and contact/midpoint/ready animation parameters for both outcomes. This follow-up used
automated Editor tests; it did not run another human or standalone playtest.

After the forward-wrap clarification, the 8 cooldown tests were rerun with explicit checks
for forward post-contact playback, the clip-end boundary, wrapped opening frames and the
final ready pose for both hit and miss cooldowns.

The final input-flow correction makes `PunchStarted` immediately skip Ready Frame to
Contact Frame, before any bridge acknowledgement and regardless of the optional
hit-confirmation snap setting. It no longer plays the intervening wind-up on input.
Recovery remains Contact -> clip end -> frame zero -> Ready on the selected outcome clock.
All 10 cooldown tests passed, including both snap-setting values, contact posing before
acknowledgement and no animation restart for cooldown-rejected input. Unity compilation
also passed; the .NET build passed with existing warnings.

## Planted idle stance

The reported air-stepping was reproduced with the current Idle clip and controller. Before
correction, the left foot moved up to 0.0977 model units along Z and 0.0175 vertically;
the right moved 0.0776 along Z and 0.0142 vertically. At the authored 3.525 world scale,
this is roughly 27-34 cm horizontal travel and 5-6 cm vertical travel. Root Y offsets
move this entire animated trajectory without stopping the foot motion. Foot IK alone
removed vertical movement but left roughly 25 cm of shared horizontal sliding.

`PlayerIdleFootPlanting` now keeps the idle body's RootT/RootQ reference and both foot
T/Q targets at their initial stance. Unity's locomotion foot IK solves stable boots while
non-target muscle curves remain authored. Walk/strafe animation data is unchanged and the
existing blend tree releases the stance when moving. The current Idle name and user-selected
feet-based height configuration are retained. The menu command updates this saved clip and
controller only; it does not rebuild the prefab or move any player/scene roots. The full
appearance builder also calls the same treatment so it survives a later rebuild.

Three `PlayerIdleFootPlantTests` passed: 81 samples of the complete idle loop keep both
feet within 0.0005 model units of their initial position, soles remain near model floor
zero, movement still produces strides and replants when idle, and stationary punch/cooldown
poses preserve the stance. Direct sampling measured the lowest visible sole at 0.0002
model units throughout the loop (about 0.7 mm at authored scale). An isolated Campaign 01
preview was paused after level entry and inspected from a close village camera at idle
phases 0 and 0.5. Both world-space foot positions were unchanged; visual origin Y stayed
-1, matching the authored floor. Temporary Play-mode camera/placement changes were discarded
on stopping Play mode. No scene or player prefab file changed.

The existing cooldown test fixture was also corrected to use the current trimmed clip's
actual duration instead of assuming the old 20-frame clip. All 10 cooldown tests then passed,
for 13 passing focused tests in this correction. No punch clip was changed.
Unity compilation passed without reported compiler errors/warnings, and the .NET Editor
build passed with 0 errors and 357 existing package/reference warnings. This is controlled
Editor validation and visual review, not human combat playtesting or a device benchmark.
