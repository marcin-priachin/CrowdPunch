# Pillar dust and boss impacts

2026-11-05. Requirements: PILLAR-003/005/006, VISION-005/006.

All baked Dino pillars, including Campaign 40/80, now emit five short cream dust
puffs along the fallen column at ground contact. Successful boss contacts emit
one larger cream/amber comic impact and a supporting puff at the closest point
on the rotating column. The existing GatekeeperImpact resource and ComicImpactPool
provide the art; source effects, scenes and boss animation assets are unchanged.

PillarFeedbackState acknowledges each event per FallSequence without changing
damage, physics or collision history. The 18-entry dust pool fits three simultaneous
five-puff landings plus three boss hits. CombatFeedback owns cleanup and honours
the existing pause/transition gate. Comic effects retain ReduceFlashes behaviour.

Validation: Unity compilation and dotnet build passed (0 errors; 318 existing
warnings). All three new PillarImpactEffectTests pass for Falling, Waiting and
Consumed snapshots, duplicate suppression and retry with a reused sequence.
All 12 DinoGoblinPresentationTests pass, including saved prefab reference integrity.
Controlled Editor Play in Campaign 40 confirmed three baked feedback states,
18 pooled dust systems, six live bursts/114 particles for a combined landing and
boss-contact event, and the existing animated boss body/accessories. This is a
controlled effects review, not a human encounter/balance playthrough.
An additional controlled physical fall placed the boss in the column's path and
advanced the normal simulation: the column reached pi/2, registered HitBoss=1,
and acknowledged both effects exactly once. The fallen KayKit column, cream dust
and animated/equipped boss were visually checked. The temporary health guard was
removed and Editor Play stopped; no scene was saved during review.
