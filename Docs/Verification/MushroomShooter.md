# Mushroom Shooter verification

Open **Window > Crowd Punch > Level Play**, select **Legacy**, and pick **33 Return Fire**.
The level has three solid cover pieces and two wave-clear waves. The first has one
Shooter and six Baselines, and the second has two Shooters and twelve Baselines
(MUSHROOM-004, LOOP-006).

## Authored settings

The Shooter aims at warning onset and displays one translucent lane for 0.75 s. The preferred range is
12–22 m, the engagement range is 32 m, and the cooldown is 2–3 s.
The laser holds for 0.25 s with a maximum range of 45 m and a width of 1.10 m.
The prefab's grounded muzzle is 0.532 m above the floor. The No Root attack clip's
release pose is frame 11 of 25 at 30 fps.

## Verified in Unity

The 11 Mushroom Shooter EditMode cases cover translucent warning geometry, fixed
aim and release timing, cover, once-per-beam damage, punch credit, launch
interruption, and impacts through the Unity Physics solver. The 15 Sporella cases
and four Combat VFX cases also passed.
An isolated Return Fire preview produced a hostile beam and a punch-triggered
beam through the normal bridge. Both reached the active beam mesh.
A second preview sampled 61 warning frames with 24% opacity and confirmed that
the beam followed the warning direction at release.

In a separate Editor preview, `MushroomBeamSystem` averaged 0.49 ms with 7
authored enemies and 0.32 ms with 14, from 12 active hostile-beam frames per
wave. The respective peaks were 1.96 ms and 0.62 ms. The second wave was
reached by injecting first-wave defeats. These samples include Editor overhead.

## Playtest checks

- Sidestep as soon as the translucent lane appears. The violet beam should follow the lane and
  pass through enemies. Crossing it during the hold should deal 10 player damage
  once and grant the standard 0.5 s of invulnerability.
- Punch the Shooter or launch a body into it. The amber beam should start at the
  impact position and point along the launch. Line up several enemies to check that
  each takes 5 damage once, intact armor is protected, and the player is safe.
- Move behind cover and check that both beams end at its near face. Check that a
  launch interrupts the warning or active hostile beam, and that retry clears any
  active beams.
