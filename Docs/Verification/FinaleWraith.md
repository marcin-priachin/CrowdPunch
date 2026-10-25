# Finale Wraith presentation

Verified 2026-10-25 in the Agent A worktree, Unity 6000.3.10f1.

## Implemented

- FINALE-001: Campaign 81 now instances `Assets/Wraith Cute Series/Prefabs/Wraith.prefab`, preserving its `(1, 1, 1)` root scale. Removed the obsolete `FinaleSettings.modelScale` field and saved value.
- FINALE-003: the dedicated Wraith controller references original FBX clips. Flight/takeoff use Fly Forward In Place, grounded rest uses Idle, falling/accepted damage use Take Damage, and defeat uses the non-looping Die clip. Strike/Sweep/Slam/Shockwave use Projectile Attack/Spin Wing Slash Attack/Wing Stab Attack/Cast Spell respectively. Authored windup reaches 65% at warning resolution and continues into follow-through; phase changes cancel pending actions. Root motion is disabled.
- FINALE-007: removed scripted body flashes, health/vulnerability tint and squash/stretch. Preserved the health bar, ground warnings, anchor status, tether and vulnerability ring. A dedicated Full Cel material keeps the Wraith body texture. Original particle auras remain; Unity validated their transparent material blend settings and the source body's texture alias.
- A fixed sampled translation clears the complete clip envelope, including death. No runtime scale fitting, bone shaking or color-property feedback is applied. Collider geometry, settings other than the removed visual scale, encounter timings, damage rules, waves, scenery and baked lighting remain unchanged.
- `Crowd Punch/Bosses/Apply Wraith Finale` reapplies the presentation. Existing finale construction and boss appearance authoring delegate to the same builder.

## Verification

- Unity compiled the final code without C# errors.
- All three new Wraith presentation tests passed. They verify source prefab/controller/material wiring, warning-to-follow-through continuity and phase cancellation, unchanged scale/material colors, and floor clearance over 41 samples of each of eight clips.
- Full EditMode run completed after the tool's five-minute response timeout: **1,162 passed / 88 failed / 1,250 total**. All 11 existing `FinaleTests` and all three Wraith tests passed.
- The broad failures concern unchanged assets/expectations: armored/Dasher scale, Gatekeeper/Chicken pose or palette, old barricade hit counts, Chapter 2 firing-frame comparison, Dino supply, player dash checks, a Chapter 6 chest intersection, and production presentation expectations. The latter still expect the shared post profile in Chapters 3-9 despite those scenes' chapter profiles. One additional failure was caused by a Pipeline status/eval timeout log during the broad run. No clean-baseline full-suite comparison was performed; these results are not an assertion that the whole repository is green.
- Isolated Level Play at Campaign 81 confirmed advancing simulation (frames 193 to 1,005), flight, controlled falling/landing, grounded Wing Stab Attack, the cyan vulnerability ring, and defeat. Scale remained `(1, 1, 1)`. Die continued holding its final authored pose past normalized time 25; its minimum world-space body height was approximately -0.914m above the arena floor at -1m. Runtime verification used temporary increased player health and controlled boss phases, which were discarded on leaving Play mode. The original clean `New Scene 1` Editor scene was restored.
- The standard `dotnet build Assembly-CSharp.csproj --no-restore -m:1` was interrupted after a lengthy package dependency rebuild without a verdict. A direct-project check with `BuildProjectReferences=false` exited with 36 CS0006 missing generated metadata references under `Temp/Bin`; this does not constitute a successful .NET build. Logs are `Temp/FinaleWraith-dotnet-build.log` and `Temp/FinaleWraith-dotnet-direct.log`.

No full fight balance playthrough or target-device performance run was performed. OQ-001 hardware/FPS targets remain unresolved.
