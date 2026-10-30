# Gatekeeper Magma presentation verification

Date: 2026-10-23. Requirements: BOSS-001/002/004/008, VISION-006.

The supplied Polygonal Magma Orange prefab replaces the Orc head and primitive
fists in Campaign 10, Campaign 50 and legacy Gauntlet 11. Its supplied FBX clips
provide idle, movement, attacks, damage/stagger, protection, transition and defeat.
The sampled ECS rig overrides RigLArm1/RigRArm1 translations independently so
hands follow existing physical damage areas. Source assets are preserved. The owned cel
material uses the source orange texture with a fixed brighter albedo; no phase
color multipliers or procedural squash remain on Gatekeeper.

- Unity compilation completed without errors or new warnings.
- 5/5 GatekeeperMagmaTests passed, including every encounter's source wiring,
  independent moving palm attachment over 420 updates, stable scales, unchanged
  physics, and complete/retriggered damage playback without flash timers.
- 27/27 BossEncounterTests passed.
- The four existing boss animation transition/physics tests passed. The broader
  appearance suite passed its Gatekeeper sample hash/floor check, but its Chicken
  and Dino sample hash checks failed on unchanged assets. Those were not rebuilt.
- Serialized scene comparison against HEAD found only the head name, head visual
  child reference, and removed hand visual child references changed among retained
  objects. All retained authoring components and authored transforms are unchanged.
- Controlled Campaign 10 Editor review advanced frames 1 to 1089 across 1081
  observations, covering all three attacks, recovery, relocation and transition.
  Maximum palm-to-physical-hand error was 0.00000438m. A temporary accepted hit
  exercised damage playback. The review used temporary player health and a front
  camera for captures; both disappeared on exiting Play mode. No runtime errors
  were returned by the final console read. This is not human combat playtesting.
- An initial capture helper timed out because player defeat paused game time. It
  was removed; the completed review used a temporary protected player.
- dotnet build Assembly-CSharp.csproj --no-restore exited with Build FAILED and
  no reported errors or warnings. Unity compilation and focused tests above are
  the verified compilation/runtime results. No standalone or target-device
  performance claim is made.

## Hand fit after authored model rescaling

Campaign 10 and 50 retain the user's model scale of (6, 6, 6) and model position.
Hand sizing now comes from the existing physical 2.6m radius, the current model
world scale, and a fixed reference radius measured from the animated mesh.
Horizontal hand mesh centers follow the damage areas. Interpolated mesh bounds
provide a conservative bottom for a presentation-only ground lift, with 0.02m
clearance exposed on GatekeeperRigAuthoring. Colliders, attack targets, timings,
and damage remain unchanged. The legacy Gatekeeper uses the same correction.

- Final Unity compilation passed. All 11 focused rig checks and 27 encounter
  regression checks passed. Actual mesh vertices were checked across attack,
  stagger, transition and defeat poses, interpolated frames, additional model
  rescaling, and a raised arena ground plane.
- Final live review recorded 720 advancing frames in each campaign encounter.
  Their minimum hand vertex heights were -0.980002m against the -1m arena floor,
  providing the configured 0.02m clearance through observed slam and lunge states.
  Every recorded hand vertex stayed within the original horizontal damage radius.
  The automated pose sweep additionally covers the sweep attack and later poses.
- Serialized comparison against the start of this follow-up confirmed that only
  rig-arm position/scale overrides and rig configuration changed in the scenes.
  Model transforms and all physical-part settings were preserved.
- The temporary live recorder logged empty ground-query errors during loading;
  their stack traces point to the temporary recorder, not gameplay code. It
  subsequently completed both captures and was removed on leaving Play mode.
  The existing Sidekick Tool Downloader window error also remains unrelated.
- The final dotnet build again exited 1 with zero reported warnings/errors;
  Unity compilation and the 38 focused checks above are the verified results.
  Editor launch configuration and background ticking were restored after review.

Evidence: [rig tests](Evidence/GatekeeperMagma/hand-fit-tests.json),
[encounter tests](Evidence/GatekeeperMagma/hand-fit-encounter-tests.json),
[live mesh measurements](Evidence/GatekeeperMagma/hand-fit-live.json),
[scene preservation audit](Evidence/GatekeeperMagma/hand-fit-serialized-audit.json).

## Constant fist size and attack barrier

The attack growth, projected marker-coverage fitting and telegraph changes from
the last follow-up were reverted at the user's request. Hand size is constant.
Only round-barrier containment and manual size controls remain from that slice.
The existing ground clearance and the user's model scales of 6 are preserved.

To adjust size in either campaign SubScene, select the skinned-mesh child under
`Gatekeeper Model`, then edit **Gatekeeper Rig Authoring > Left Fist Scale /
Right Fist Scale**. Both default to 1 (previous size); 1.5 makes that fist 50%
larger. Save the scene and restart Play to rebake. These are visual multipliers;
colliders, damage areas and attack markers are unchanged. Boundary envelopes
automatically account for the selected sizes.

- Unity compilation passed. All 14 rig checks and 30 encounter checks passed,
  including independent manual sizes, model rescaling, ground clearance, and
  attack/recovery movement at 30/60/120 Hz without crossing the circular barrier.
- Live Campaign 10 and 50 covered all three active attacks over 1,099 and 967
  recorded frames. Maximum vertex distances from the court center were
  18.170757m and 18.176730m (barrier radius 19m). No animated vertex exceeded the
  configured constant fist-size envelope. Minimum height stayed at -0.980002m
  against the -1m ground. No live bound assertions failed.
- Model scales remain 6 in campaign 10/50. The original telegraph and presentation
  files match their pre-growth versions. Both size multipliers are explicitly 1
  in all three scenes. Temporary review configuration was restored, and the
  Campaign 50 rig was selected in the Inspector for tuning.
- The separate dotnet build still exits 1 with no reported errors/warnings;
  Unity compilation and the 44 focused checks are the verified build results.

Evidence: [rig tests](Evidence/GatekeeperMagma/constant-size-rig-tests.json),
[encounter tests](Evidence/GatekeeperMagma/constant-size-encounter-tests.json),
[live bounds](Evidence/GatekeeperMagma/constant-size-live.json).

## Play startup controls

The user reproduced unresponsive startup input with the Agent A Game view set to
Play Maximized. Changing input routing alone did not resolve it. With the Game
view set to Play Focused, the user confirmed startup movement works. The runtime
trace shows movement beginning on frame 22 and stopping on the release frame 47;
keyboard events arrive about 10 ms after their native timestamps in that sample.
See [startup control evidence](Evidence/GatekeeperMagma/startup-controls.json).

Agent A's Game view remains set to Play Focused as a local Editor workaround.
Default input routing was restored and temporary diagnostic scripts and runtime
objects were removed. No player movement code was changed for this workaround.
The engine-level cause of the maximized-window behavior and standalone behavior
were not established.

![Magma Gatekeeper idle](Images/GatekeeperMagma/Opening.png)

![Authored transition](Images/GatekeeperMagma/Transition.png)

## Cartoon attack effects (2026-10-30)

Requirements: BOSS-002/008, VISION-005/006. Campaign 10/50 and the shared legacy
Gatekeeper now use filled amber-to-coral attack areas, dark ink borders and cream
direction cues. The original circle, straight lane and curved sweep footprints
are retained. Dedicated cream Cartoon FX smoke, golden strokes and existing comic
impact accents replace generic boss contact particles and accent completed strikes.
Moving lunges/sweeps leave short dust puffs. This pass changes presentation only.

- Unity compilation passed; no new compiler warnings were reported.
- Final `dotnet build Assembly-CSharp.csproj --no-restore -v:q` passed with zero
  errors and 318 existing package/reference/deprecation warnings.
- Controlled Editor preview observed real slam, lunge and sweep anticipation in
  both campaign encounters, and live pooled particles on slam recovery. Campaign
  10 screenshots were visually reviewed for all three shapes and the impact.
- Campaign 50 loaded six dedicated smoke/stroke instances with the existing
  Cartoon FX smoke material. Pausing cleared all markers and live dust. Switching
  to campaign level 1 left zero boss entities, visible markers or live boss dust.
- Enemy health, boss settings, colliders, source models, authored scenes and shared
  ordinary-enemy feedback assets were not edited. The Editor preview restored
  player health during inspection and did not write campaign progress. Play mode
  was stopped and the original Campaign 09 scene restored afterward.
- No gameplay errors appeared during the completed probes. Editor startup still
  logged the existing Sidekick Tool Downloader window error, and the automation
  connection logged timeouts while entering Play mode. These are distinct from
  gameplay/compilation results. This was presentation inspection, not a balance,
  standalone-build or target-device performance test.

The follow-up also replaces the model's original red `FX Square` ambient particles.
Scene-instance overrides give both palms small cream/gold Cartoon FX puffs and the
head emitter warm star sparks, fading and drifting upward with a 12-particle cap
per emitter. Original emitter activation is preserved (the head emitter is inactive
in the current encounters). The model rebuild recipe reapplies these overrides.
Both campaign 10 and 50 were inspected in Play mode: their two live companion
emitters used the smoke atlas and emitted textured puffs; no `FX Square` material
remained on any of the three model emitters. The head replacement and legacy 11
were checked in the serialized authoring setup. A scene comparison confirmed only
particle prefab overrides changed, preserving model scale and combat components.
Unity compilation and the follow-up C# build passed (zero errors; existing package
warnings). No gameplay errors appeared during the completed runtime checks.
