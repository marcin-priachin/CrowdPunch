# Alien Sporella verification

Open **Window > Crowd Punch > Level Play**, switch to **Legacy**, and select
**34 Spore Weave**. The arena has three pieces of solid cover and two waves:
1 Sporella with 6 Baselines, then 2 Sporellas with 12 Baselines. Defeating the
ordinary enemies advances and completes the encounter. Gauntlet 34 is registered
in Bootstrap's Legacy list and is absent from the campaign catalog and build
scene list (SPORELLA-004, LOOP-006).

Tune the encounter's `SporellaSpawnSettings.asset` in **Alien Sporella**.
The stored profile uses both ownership interactions and both impact directions.
Each burst has two passes of three straight shots spaced 0.18 s apart. The
default order in each pass is left, center, right. At the player's sampled
distance, the outside shots aim 1.5 m to either side. Impact bursts use the
launch direction as their centerline and a 10 m reference distance. The asset
exposes the pass count, shots per pass, and width. The normal windup lasts
0.65 s, and each projectile travels at 14 m/s. The starting projectile tuning
is 26 m range, 0.94 m above the floor, a 0.38 m radius, and 10 damage to the player
or eligible enemies. Player invulnerability lasts 0.5 s. The cooldown is
2–2.85 s after the burst. The release uses frame
10 of the authored Front Projectile Attack clip, where the mouth moves forward.
The FBX texture is retained in the Full Cel material.

The focused EditMode cases cover straight flight, the locked sweep order and
cadence, open side lanes, cover during windup, launch cancellation, ownership
modes, damage ordering and gates, redirect protection, and completing an
impact burst after its emitter is removed (SPORELLA-001..003). All 13 passed.
Unity 6.3 LTS and
`dotnet build Assembly-CSharp.csproj --no-restore` compiled without errors.

A controlled Editor run entered Gauntlet 34 through the Level Play selection
keys. Wave 1 spawned the authored roster, baked the Sporella skin, and emitted a
normal burst with a visible projectile core. An injected player-owned launch
produced a real lethal Unity Physics collision. All six impact shots used the
same origin and left one history entry. Simultaneous normal and impact projectile
renderers used different ownership colors. Wave 2 spawned the authored roster after
clearance. Launching a Sporella with no punch sequence into a Baseline triggered
an outgoing burst with the fallback source key. Restart cleared the pending burst,
shots, and history. Clearing both waves after restart reached Run Complete. The
wave clearances used controlled defeats. Input timing and difficulty still need
a human playtest.

The projectile defeat check hit a Sporella with an impact shot and defeated the
final Sporella with a punch return. The live model sampled the authored Die clip
through 98% and 99% of its playback before pooling. The Legacy clear screen
followed the final animation. All 15 Sporella tests and four landing-lifetime
tests passed.
