# Final encounter verification

Date: 2026-10-14. Unity: 6000.3.10f1. Requirements: FINALE-001 through FINALE-007; LOOP-002/006.

## Playable content

Use the existing Level Play window to select level 81, **Bring Down the Sky**. It runs through Bootstrap in editor preview without writing normal campaign progress. Normal campaign progression reaches it after level 80; Chapter 9 contains this single level. Existing completion IDs remain unchanged, including saves that had completed all 80 earlier levels.

The saved main scene is `Assets/CrowdPunch/Scenes/Campaign/Campaign_81.unity`, with its ECS SubScene in the adjacent `Campaign_81` directory. The supplied Goleling model, four installations, navigation, obstacles, eight bounded supply sequences, animation controller and warning presentation are wired into these assets. Earlier campaign scenes and shared ordinary enemy profiles were not changed.

## Implemented rules and checks

| Requirements | Evidence |
| --- | --- |
| FINALE-001 / LOOP-002/006: campaign append and save compatibility | EditMode tests cover 81 entries, nine chapter boundaries, the old 80-completion save, build registration and previous chapter access. Live preview loads index 80 with all four baked anchors. |
| FINALE-002/004: anchor lifecycle and ownership | Tests cover immediate cancellation/downing, player versus boss/enemy ownership, paused shell/core punch access, rail motion and physical docking. Live probe verifies spent-anchor retention, fourth-use reset and full retry. Existing cover, shell, rail and barricade regression fixtures pass. |
| FINALE-003: boss cycle and attacks | Live baked-world probe observes both airborne warnings, falling/landing, fixed initial stun, alternating grounded slam/shockwave, harmless takeoff and resumed flight. |
| FINALE-005: damage and rebound | Tests cover vulnerability, separate impact/blast identities, duplicate rejection and safe rebound preserving the launch. An injected moving ECS body physically hits the stationary grounded boss, reducing health from 6000 to 5700. |
| FINALE-006: supply | Initial baked population is 57 physical bodies. Stage-three sample contains 65; configured upper bound is 60 roots plus six Snake segments. Existing normal profiles are reused. Retry restores supply; victory clears reservations and survivors. |
| FINALE-007: victory and cleanup | Live probe verifies immediate damage/supply suppression and final campaign completion without clearing anchors. A separate simultaneous lethal player/boss probe reaches CampaignComplete with player health zero and RunFailed false. Scene exit leaves zero finale bosses, anchors or supply components, camera offset zero and hostile damage suppression false. |

## Automated and Editor results

- Focused Unity EditMode run: **69 passed, 0 failed, 0 skipped**, including 11 finale cases and related campaign/objective regressions.
- Unity script compilation: no compilation failure.
- `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -m:1 -v:q`: successful with existing project/package warnings; no errors. Building referenced Unity-generated package projects is disabled for this check.
- Controlled live probe passed through the entire cycle, fourth-anchor reset, player death/retry, final victory and scene exit. The probe injects anchor hits, launches and lethal damage and restores player health outside its death test. It is behavioral verification, not evidence of natural combat balance.
- Final Game-view inspection confirms the airborne model and player are visible after tuning the camera look height and moving the entrance rock clear of the player spawn.
- `git diff --check` reports no whitespace errors.

Local diagnostic outputs are in ignored `Temp/FinaleValidation`: `editmode-results.xml`, `editmode-summary.txt`, `playcheck.txt`, `dotnet-build.txt` and `flight-final.png`. `FinalePlayCheck.Start()` reruns the controlled play probe; `FinaleTestRunner.Run()` reruns the focused EditMode suite.

An initial broader Chapter 8 run also exposed an unrelated existing level-74 barricade assertion: its test expects four required hits while the saved asset has three. That older scene and tuning were left unchanged. The focused suite includes the Chapter 8 save/progression test affected by this implementation, rather than claiming the entire repository suite passes.

## Tuning and performance limits

`Assets/CrowdPunch/Data/Campaign/Finale/FinaleSettings.asset` owns boss tuning: 6000 health, stages at 67%/34%, 2.5-second fall, 24-second vulnerability with a 7-second initial stun, 2.5-second takeoff and a 3-second victory. The rebound safety cone is 35 degrees. These are delegated starting values, not additional design requirements. Wave assets are in the same directory; shell/core assets are `Data/Campaign/Settings/L81_Shell.asset` and `L81_Solid.asset`. Supply bank/stage/recall settings are on scene authoring.

A stage-three Editor sample recorded 719 frames at 65 bodies: mean 16.69 ms, p95 19.39 ms. Samples exclude debugger/tool stalls of 0.5 seconds or more. This is a bounded local Editor observation, not a target-platform performance guarantee or a standalone Player build benchmark; target hardware remains OQ-001.

Remaining human verification: play naturally with keyboard/mouse and controller; tune the 6-8 minute and 6-8 downing targets; assess camera framing throughout movement, warning overlap, landing escape routes, anchor readability, supply usefulness and stage difficulty. No full manual combat playthrough or standalone build is claimed.

## Boss feedback update (2026-10-14)

The boss health bar is saved on the finale scene's presentation object. Following the user's appearance correction, it is a red fill only, centered at the top, with no panel, health track or text. Its 900-by-14 reference-pixel rectangle uses the player's 1920-by-1080 Canvas scale, fill thickness and 60-pixel top margin. The cyan model tint and pulsing ground ring indicate vulnerability. The opening hint sits below the bar; its graphics do not intercept input.

The initial feedback update passed Unity compilation and live checks of health updates, vulnerable model tint/ring, and the ring turning off at takeoff. Game-view screenshots including overlay UI were inspected using ScreenCapture. The optional dotnet check could not resolve 122 Unity-generated package references under Temp/Bin after the Editor session reset; these are CS0006 missing assembly errors rather than Unity script compilation errors.

Live lifecycle checks of the initial feedback update verified that retry restores one boss HUD at full health and selecting level 1 leaves zero boss HUD components. No encounter simulation or balance changes were made for either feedback update.

The simplified red-bar revision passes Unity compilation. Its running Game-view screenshot was inspected: one red Image, zero text components, no background, centered top placement and the intended thin shape. Setting health to 50% produces a 0.5 fill fraction in the 900-by-14 reference-pixel bar.
