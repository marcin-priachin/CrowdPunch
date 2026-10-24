# Chicken stationary player tracking and turn clips

Verified 2026-10-24 in Agent A, Unity 6000.3.10f1.
Requirements: CHICKEN-001/002/007, INFO-004, VISION-006.

- Pause and Cast track the current player's horizontal position. Rush tracking remains
  independent of the committed route. Defeat and stagger retain their action presentation.
- `ChickenMotionSystem` publishes signed idle world yaw rate in `ChickenTurnState` while
  retaining the existing physics angular-velocity rotation and zero stationary linear velocity.
- During Pause, `ChickenAnimationSystem` selects Giant-pack Turn Left (motion 6) or Turn Right
  (motion 7) from yaw direction. Rates below 0.02 radians/second are ignored to suppress noise.
  Each 20-frame, 0.666667s step finishes before returning to Idle; ongoing turning repeats the
  clip, and reversing direction selects the opposite clip. Cast and Sweep retain priority.
  Only the leg bones use the turn pose. The pelvis, upper body and equipment keep a continuous
  Idle clock; changing turn direction does not restart Idle. Turn legs are composed relative
  to the Idle pelvis using the existing directional-jog leg mask. The jog movement layer is
  disabled during turns. Reset clears turn state and all boss skin playback.
- The source clips author approximately 90 degrees of body yaw. Export removes this yaw
  relative to frame zero on every sampled body/equipment matrix, retaining the step animation
  while the physics root applies the facing rotation exactly once. Source prefab transforms,
  authored scale and imported clips are preserved.
- Existing turn samples and saved wiring in Campaign 20, Campaign 60 and Gauntlet 20 are reused
  by the leg-only layer. Source asset transforms and gameplay timings are unchanged.

## Checks

- Unity compilation succeeded; all 54 Chicken-filtered Editor checks passed with Burst enabled.
  New checks cover left/right idle tracking, stationary velocity, cast priority, turn completion,
  return to idle, continuing/reversed turns, reset and normalized root yaw in all saved scenes.
  Two new layered-pose cases verify that only leg matrices use turning, hips remain Idle,
  legs attach under the Idle pelvis, and upper-body/equipment Idle time continues when turns reverse.
  Previous contact, teleport, route, projectile, model scale and sample dependency checks pass.
- Controlled live Campaign 20 review moved the actual GameObject player around the stationary
  boss, reversed direction and stopped. Across 240 advancing frames and 217 facing samples,
  both turn clips appeared, the boss remained stationary, the jog mask stayed disabled during
  turns, and Idle resumed after the player stopped. The rendered review confirms Idle upper-body
  and equipment poses during both turns. Minimum root-forward/player-direction dot
  product was 1.000000 at recorded precision.
- Rendered close-up review: [Turn Left](Images/GoblinChicken/TurnLeft.png),
  [Turn Right](Images/GoblinChicken/TurnRight.png). These use a temporary review camera.
- Source/test/document whitespace checks passed. The standalone
  `dotnet build Assembly-CSharp.csproj --no-restore -v quiet` still returned `Build FAILED`
  with zero warnings/errors and no diagnostic, as in the preceding changes. Unity compilation,
  automated gameplay checks and the controlled live review succeeded.
- Clip inspection marked the original scene dirty without changing its content. A saved copy
  was compared byte-for-byte with the original before clearing that flag; the original contents
  remained identical. The Editor was stopped and its original `New Scene 1` restored afterward.

This is a controlled Editor review, not a human playthrough or target-device performance result.
The new component belongs to the single boss; no crowd query or simulation rule was expanded.
