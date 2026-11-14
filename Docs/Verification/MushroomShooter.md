# Mushroom Shooter verification

Open **Window > Crowd Punch > Level Play**, select **Legacy**, and pick **33 Return Fire**.
The level has three solid cover pieces and two wave-clear waves: 1 Shooter with 6
Baselines, then 2 Shooters with 12 Baselines. The campaign catalog and player build
scene list do not include this encounter (MUSHROOM-004, LOOP-006).

The Shooter profile has a 0.30 s tracking warning, a 0.45 s locked warning, a
2–3 s cooldown, a 12–22 m preferred band and a 32 m engagement range.
The projectile travels at 32 m/s for up to 45 m per leg, 0.95 m above the floor,
with a 1.10 m diameter. It does 10 damage to the player with 0.5 s of normal
invulnerability and 5 damage to eligible enemies after redirect.

The No Root Projectile Attack clip has no release event. In Unity, the mouth
bone reaches its first forward extremum at frame 11 of 25 (30 fps).
The baked scene reported the first projectile at 32 m/s with its full 45 m range,
and the animation reported release progress 0.440 on that same rendered frame.
The locked floor line was captured at the beginning of its 0.45 s hold.

Seven focused EditMode cases verify cover and warning timing, cancellation in
both phases, cooldown after recovery, projectile priority, repeated punch
rejection, harmless travel through enemies on the incoming leg, ordered piercing,
armor immunity, and player contact after leaving the redirect overlap.

In a controlled live Editor run through the Level Play launch path, each Shooter
fired in both authored waves. Injected enemy defeats demonstrated standard wave
advancement and final completion. The live check is a wiring and system order
check; natural input timing, difficulty, and larger crowd performance still need
playtesting. The separate `dotnet build` returned unsuccessful without compiler
errors; Unity compilation was clean.
