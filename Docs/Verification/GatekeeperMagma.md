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
