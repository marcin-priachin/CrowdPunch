# Chapter 4 implementation and verification

2026-10-10. Requirements: LOOP-002..008, WIZARD-001..007, TRAIL-001..007,
PROTECT-001..004, BARRICADE-006, TRACK-001..005, COVER-001..005,
SHELL-001..006, GROUND-001..007, PILLAR-001..006.

## Playing and tuning

Open `Assets/CrowdPunch/Scenes/Bootstrap.unity` and press Play. Continue after Chapter 3
loads level 31. Window > Crowd Punch > Level Play > Campaign offers previews without
changing normal progress. Finishing level 40 unlocks the future, unavailable Chapter 5.
Each scene from `Campaign_31.unity` through `Campaign_40.unity` has its own ECS SubScene.
Seventeen new wave assets and independent objective settings are editable under
`Assets/CrowdPunch/Data/Campaign`. The creation recipe refuses to overwrite saved content.

| Level | Encounter | Initial population / objective |
|---|---|---|
| 31 | Purple Warning | 6B+1W > 12B+2W; Wizard supply and persistent-zone gate |
| 32 | Quiet Delivery | Hold 10B; diagonal rail, five net hits |
| 33 | Leave a Trail | 6B+1T > 12B+2T; trails do not delay completion |
| 34 | Break the Detour | Hold 12B+2X; four-hit 2m gate with sealed sides; periodic shortcut |
| 35 | Moving Perimeter | Hold 12B+1T; four-hit target; 100-degree opening at 25 degrees/s |
| 36 | Distant Defense | 16B+2R > 20B+1D > 20B+2R+1W; batches up to four every 3s |
| 37 | Blast Shelter | Hold 12B and specialized pair of 2X; three shell explosions; hot corner |
| 38 | The Crowd Moves | 70 > 86 > 102 > 120; 100 x 100m wave-clear arena |
| 39 | Last Alignment | Hold 10B; five net rail hits |
| 40 | Falling Giants | Hold 8B plus 2B per unconsumed pillar; three pillar hits |

B = Baseline, R = Ranged, X = Exploder, D = Dasher, A = Armored, E = Elite,
W = Wizard, T = Trail. Level 38's exact compositions are 64B+4R+2W, 80B+4D+2T,
96B+4A+2E, and 112B+4X+2W+2T. Its 378 initial enemies include the two Elites;
existing Elite, armor and Wizard supplies can add more. There is no survival timer.
The defense has no replenishment or persistent-zone completion gate, fails on the first
breach, and retains five-second cleared-wave pauses. Ground tuning remains 12 damage
every .75s, with 4/1.5/2.5s inactive/warning/active timing for level 34.
Dino's original arena, model and all three pillar transforms are preserved.

## Verification

- Unity compilation passed. The .NET build passed with zero errors and 344 existing
  reference-conflict, obsolete-API and unused-field warnings.
- Four Chapter 4 tests passed after moving level 34's navigation anchor clear of its
  bend. The other 16 campaign tests passed unchanged apart from availability/count updates.
- 135 related tests passed: Wizard 29, Trail 16, Dino/pillars 31, defense 15, tracks 17,
  shell 5, cover 10, barricade 12.
- The landing/pooling regression also passes: visible corpse physics remains active,
  pooled bodies leave the simulated world, and a real safe respawn restores world 0.
  All 20 campaign tests and the Wizard, Trail and Dino suites passed after this fix.
- The clean live lifecycle rerun passed all 17 new waves plus the preceding Rolling
  boss boundary, player death/retry, defense breach/failure/retry, all partial batches,
  periodic hazard phases, both tracks' physical arrival, Dino's six local ammunition
  bodies, chapter completion, save/reload, replay/retry and the unavailable Chapter 5
  boundary. It uses an isolated save and injected defeats/objective completion, not
  a human combat clear. The normal campaign save remains untouched. Evidence:
  `Temp/CampaignValidation/chapter4-lifecycle.txt`.
- An additional live Dino check defeated one general support body, observed pooling,
  then confirmed its safe respawn and presence in the actual simulated physics world
  at index 0. Evidence: `Temp/CampaignValidation/chapter4-live-respawn.txt`.
- Runtime screenshots were inspected for Wizard/Trail readability, the diamond court,
  diagonal rail, narrow gate, defense lane, shell/hazard placement, crowd banks and Dino.
  No runtime errors occurred in the clean rerun. Unity emitted transient JobTempAlloc
  lifetime warnings during the initial rebake; they did not recur through the later
  defense, large waves and boss checks. Editor was left stopped on Bootstrap.

## Multi-wave performance diagnosis

The first live run found severe later-wave slowdown, despite normal early waves.
The final 120-enemy wave recorded a 1034.94ms median main-thread frame, with only ten
rendered frames and 3.33 game seconds in a ten-second wall-clock capture. A separate
third-wave reproduction identified Wizard collision processing at 269.67ms median.
Pooled enemies from previous waves still had mutually colliding capsules at one
underground point. Moving 167 pooled bodies out of the simulated physics world in
that running scene reduced the main-thread median to 16.67ms over 544 samples.

The permanent fix changes physics-world membership only when entering the hidden
pool and on safe respawn, preserving collider data and visible landing response.
Logs: `Temp/CampaignValidation/chapter4-peak-profile.txt`, `chapter4-slow-systems.txt`,
and `chapter4-pool-diagnostic.txt`. The initial lifecycle probe also exposed its own
timeout clock spanning a long encounter and the next load; that clock now resets when
the new level is selected. The lifecycle passed a rerun from the beginning after both fixes.

With the permanent fix, the same final-wave capture recorded 600 frames and 10.03 game
seconds in ten wall-clock seconds: main-thread median 16.73ms, maximum 26.31ms; physics
median .38ms, maximum 1.03ms; Wizard impact processing median .22ms. All 258 pooled
bodies from the preceding waves were absent from the actual physics world. Every large
wave's lifecycle frame median was 16.7ms. The player was kept alive without combat input;
these Editor measurements include Editor overhead and are not a target-device or
worst-case combat benchmark. Evidence: `chapter4-peak-profile-fixed.txt` in the same folder.

## Remaining playtesting

Human clear times, level 38's greater-than-three-minute target, encounter difficulty,
and target-device performance require playtesting. OQ-001 hardware and FPS acceptance
remain unresolved. Controlled Editor probes do not establish these results.
