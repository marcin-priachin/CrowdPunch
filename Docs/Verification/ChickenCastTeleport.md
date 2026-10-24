# Chicken proximity Cast Spell and teleport

Verified 2026-10-24 in the assigned Agent A Unity 6000.3.10f1 Editor.
Requirements: CHICKEN-002/006/007, INFO-004, VISION-006.

## Behavior

- During Pause, the default close-player response now enters stationary Cast rather than
  committing a proximity rush. The supplied Giant-pack Cast Spell occupies sampled motion 5
  for the body, shoulders, hat and wand. Directional jog masking is bypassed for the full cast.
- The model builder derives `castDuration: 1` from the supplied clip and saves it in the
  Campaign 20, Campaign 60 and legacy Chicken settings. `ChickenCastTuning` bakes this clock.
  `proximityDistance`, contact damage, protection and push retain their existing settings.
- Contact during Cast uses the normal player health/protection/push bridge. With defaults,
  contact deals 14 HP and applies the existing outward push. Repeated contact respects player
  damage invulnerability. Pause, Stagger and Defeated have no boss contact damage.
- At cast completion, `ChickenTeleportSystem` runs between AI and movement, before physics
  world construction. It selects the farthest corner from the current player position within
  `arenaHalfSize - bodyRadius - 0.15m`. This maximizes distance on the authored open rectangle
  while keeping the body clear of perimeter walls. It preserves height and scale, clears
  velocity, resets both swept-contact endpoints and resumes Pause. There is no path damage.
- The optional Finish Pause response, committed wind-ups, shot-triggered rushes, paired shot
  spacing and Sweep release frame remain intact. Accepted hits interrupt casting or a pending
  teleport through the existing stagger/defeat transition. Restart restores ordinary initial state.

## Verification

- Unity compilation passed without errors.
- All 43 Chicken-filtered Editor checks passed with Burst enabled. New coverage includes
  stationary cast timing, current-player destination selection, collider clearance and global
  distance maximization, scale/height preservation, interruption and reset, real player health
  and push delivery with repeated-contact protection, and full-body cast/jog exclusion.
  Existing projectile, paired cycle, collision ownership, saved sample hashes, authored scales
  and campaign arena preservation checks also passed.
- Controlled live Campaign 20 check: 62 advancing frames, 61 observed cast frames, 1.018s
  from first observed Cast to teleport. The boss stayed stationary, five parts played Cast,
  playback reached phase 1, leg masking remained off, and player health changed 100 -> 86
  with a nonzero accepted push. The destination was the farthest inset corner and scale stayed
  unchanged: `(2.1, 0.7, -12)` -> `(16.15, 0.7, 16.15)`.
- The same controlled Campaign 60 check passed: 62 advancing frames, 61 cast frames, 1.013s,
  five animated parts, stationary cast, 100 -> 86 health, push, farthest inset corner and
  unchanged scale. Small observed timing differences reflect frame/fixed-step observation.
- [Cast Spell rendered review](Images/GoblinChicken/CastSpell.png) uses a temporary close-up
  camera. Temporary probe scripts and controlled placements were outside production assets.
- `dotnet build Assembly-CSharp.csproj --no-restore -v quiet` still returned `Build FAILED`
  with zero warnings, zero errors and no diagnostic, matching the unresolved standalone build
  condition from the preceding model update. Unity compilation and runtime checks succeeded.
- Source/test/document whitespace checks passed. The Editor was stopped and restored to its
  original `New Scene 1`; the supplied prefab and source materials were preserved.

The subsequent stationary turn-animation change and its 52-check/live review are recorded in
[Chicken standing turns](ChickenStandingTurns.md).

The first live probe arranged the boss before the reloaded player snapshot stabilized and timed
out without observing the cast. The successful probes waited for level loading and 0.5s of normal
simulation before arranging contact. These are controlled Editor checks, not a full human combat
playthrough or target-device performance measurement. The new query and corner search concern
one boss and four candidate points; no crowd scan or spawn behavior was added.

## Player-facing movement follow-up (2026-10-24, CHICKEN-001)

Rush commitment and each subsequent AI tick now set facing toward the current horizontal player
position independently of the fixed destination. The existing physics angular velocity applies
the orientation. The directional jog layer already works relative to the actual owner rotation,
so backward and sideways steps accompany player-facing travel. Cast, shot and teleport timing
and authored model scale are unchanged.

All 46 Chicken-filtered Editor tests passed, including three new facing cases with changed player
position, vertical separation and coincident XZ fallback. Destination, rush identity, physics velocity
and transforms remain unchanged by the AI-facing update. A controlled live Campaign 20 rush advanced
43 frames with 42 observed movement samples: the minimum dot product between actual owner forward
and current player direction was 0.999967, the destination stayed fixed, and backward jog blending
was observed. Unity compilation passed. The standalone .NET build again returned `Build FAILED`
with zero warnings/errors and no diagnostic. The original Editor scene was restored afterward.
