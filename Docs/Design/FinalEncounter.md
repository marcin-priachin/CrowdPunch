# Crowd Punch — Level 81 Final Encounter
Agreed design, 14 October 2026. Planning handoff; no repository changes performed.

## Purpose and scope
Add level 81 as the sole encounter in its own ninth chapter, after the existing 80-level campaign. A giant flying boss continuously pressures the player in a 100 × 100 metre arena containing every available enemy type. The player chooses among four familiar objective challenges to bring the boss down, then damages him using the crowd.

Boss model: `Assets/CrowdPunch/Models/UltimateMonsters/Flying/Goleling_Evolved.fbx`.
Make him properly large; exact scale, collider dimensions, flight height and animation choices are delegated to Codex. Keep the player, launch direction, ground warnings and objectives readable with the existing TPP camera.

Target approximately 6–8 minutes and 6–8 successful downings in typical play. These are playtest balance targets, not a timer, mandatory cycle count or damage cap. Strong play may defeat him sooner.

## Agreed lifecycle
1. **Airborne:** boss is immune to damage, follows above and slightly ahead of the player with smooth limited turning, and attacks continuously at a readable cadence. Four unfinished anchors are available simultaneously.
2. **Anchor activation:** completing any one anchor immediately cancels pending airborne attacks and clears their warnings. That anchor becomes spent. A visible energy tether drags the boss toward its nearby landing point.
3. **Falling:** all installations pause, preserving progress and refusing further objective hits or activations.
4. **Landing:** clearly telegraphed area damage and push affect player and enemies, but not anchors. Use a valid landing space near the completed anchor.
5. **Downed:** vulnerability starts only after landing. A configurable fixed timer begins. Boss starts stunned and stops attacking; after the initial stun he alternates telegraphed directional slams and radial shockwaves while remaining vulnerable. Crowd combat continues.
6. **Takeoff:** timer expiry ends vulnerability. Takeoff is harmless. Installations remain paused through this transition.
7. **Resumed flight:** unused anchors reactivate with their prior progress. Spent anchors stay unavailable. After the fourth anchor has been used and the boss resumes flight, reset and reactivate the entire set together.
8. **Death:** immediate victory takes precedence over other transitions. Cancel attacks and replenishment, neutralize surviving enemies, play a short victory sequence, then report campaign completion. No survivor-clearing requirement.

Boss health damage persists across downings. There are three health-based difficulty stages. Increase airborne attack demands and specialist enemy pressure as health falls; preserve installation mechanics and ordinary enemy stats. Thresholds and exact stage recipes are delegated.

## Four ground anchors
Present the challenges as four ground anchors linked to the boss's flight shield. Completing one overloads it and creates the tether/downing. Spent anchors remain visibly dark until the set resets.

Exactly four installations, one of each type, arranged one in each quarter around an open central area. Provide clear connecting routes, useful nearby cover and sufficient space for boss landings, crowd positioning and shooting from multiple angles.

| Anchor | Challenge and trigger |
| --- | --- |
| Rotating cover | Land the required launched hits on a target through a rotating opening. Completing its durability requirement overloads the anchor. Reuse established covered-target aiming, reflection and blast shielding behavior. |
| Shell/core | Crack an explosion-only shell with the required eligible Exploder blasts, then destroy the exposed core. Direct player punches **can damage this exposed core**. Eligible player-owned launched bodies and their explosions also damage it. Breaking the shell alone does not down the boss. |
| Rail | Use directional launched impacts to move a block along a short rail into its socket. Physical docking triggers the anchor, not merely selecting its final destination. Reuse bidirectional progress and directional stepping. |
| Narrow target | Hit a narrow target several times with launched enemies. Final required hit triggers the anchor. This is an aiming target, not a break-and-exit gate or a separate exit objective. |

All four are available simultaneously during flight, permitting switching whenever useful. Partial target damage, shell/core damage and rail position persist across downings. No reset for abandoning an unfinished challenge.

A used anchor stays spent until all four have been used. There is no additional recharge delay or compulsory waiting phase. During falling, downed and takeoff phases, installations cannot accept objective hits, activate, queue another downing or extend the damage window. Preserve unfinished logical progress; exact pause handling for rotation and any rail slide already in motion is delegated, with no hidden completion or lost progress.

### Ownership and special core exception
- Body hits on installations must come from **player-owned launches**, including chain descendants.
- Counted explosions must come from a **player-owned launched source**. Unlaunched proximity explosions and enemy/boss-owned launches cannot advance these objectives.
- Direct punches do not advance covered/narrow targets, rail movement or intact shell damage.
- **Exception:** direct player punches damage the exposed shell core.
- Reuse applicable existing once-per-launch histories and impact/blast deduplication for hit-count targets and rail steps.
- Preserve the shell's separate phase resolution: its breaking explosion cannot also damage the newly exposed core; exposed-core impact and blast are separate damage events.

## Boss attacks
### Airborne
Use both:
- Regular telegraphed ground strikes near the player's position.
- Occasional telegraphed sweeping attack lanes. Boss briefly commits to a flight path during the sweep.

Both damage and push player and enemies. They leave anchors unaffected. Threat cadence must allow deliberate positioning and punches; continuous threat does not mean continuous unavoidable damage. Sampling/locking times, warning shapes, area sizes, damage and push are configurable delegated tuning.

Activating an anchor cancels pending airborne attacks immediately and removes their warnings. Resolve cancellation consistently so no cancelled delayed hit lands during the fall.

### Grounded
After the initial stun, alternate directional slams and radial shockwaves. Both have clear wind-ups/ground warnings, damage and push player and enemies, and leave anchors unaffected. Boss remains vulnerable until the downed timer expires.

## Damage and physical interaction
- Boss damage is restricted to the downed vulnerability window.
- Eligible damage: player-owned launched-body impacts, player-owned chain descendants, and associated explosions.
- Direct punches never damage the boss.
- A qualifying launched Exploder can deal both body-impact and explosion damage.
- Each body deals impact damage once per continuous launch. Re-punching creates a new eligible player-owned launch under existing rules.
- The grounded boss remains stationary and unlaunchable. Hits produce visible reactions without translating him.
- Ordinary launched bodies rebound from his solid grounded collider and continue ordinary launch behavior. Exploders detonate on impact.
- **Do not rebound bodies directly toward the player.** Use impact geometry for ordinary reflection and a safeguard that deflects any outgoing direction aimed directly at the player sideways. Safety angle, chosen side and collision-aware implementation are delegated. This does not make launched bodies or their subsequent chains harmless.
- Flight/transition colliders must not inadvertently create a ground obstruction or allow boss damage outside the vulnerability phase. Exact collision arrangement is delegated.

## Crowd and ammunition
Continuous bounded replenishment throughout the active fight; no finite wave-clear or survivor-clear objective. All available enemy types appear over time:
Baseline, Ranged, Exploder/Explosive, Dasher, Elite, Armored, Wizard, Trail, LinkedPair, Dragon, Snake and Ninja.

Keep reliable launchable Baselines and Exploders near unfinished installations. Do not allow elimination, boss area attacks, spatial distribution or specialist composition to make an anchor impossible to complete. Continue supplying suitable bodies during downed windows so boss damage remains achievable.

Total population, per-type caps, stage mixes, timing and safe spawn banks are editable delegated tuning. Respect existing special lifecycle/counting semantics: LinkedPair has two endpoints per pair; Snake tail bodies add physical population beyond its head. Keep shared ordinary enemy stats intact. Profile representative finale populations; target hardware/FPS remains a wider project open question.

Survivors remain hostile during normal cycles. No new supply after victory; cancel already pending allocations/respawns and neutralize survivors and remaining hostile effects.

## Campaign and reset
- Add stable level 81 in a separate ninth chapter. Unlock after level 80.
- Preserve all existing 80 level IDs, scenes, unlocks and completed progress. Existing saves with level 80 completed must gain access to the new unfinished finale.
- Move final campaign completion to victory in level 81; completing level 80 should lead to access to the finale.
- The sole-level ninth chapter must work in chapter/level selection, Continue, replay and the existing Level Play Editor workflow. Avoid assuming every chapter has exactly ten playable levels.
- Preserve current-level failure/retry behavior: player death retries this encounter with full player health and all boss/anchor/crowd state restored.
- Retry restores boss health/stage/flight state, all four anchors, shell/core health, cover pose/clocks, rail state, hit histories, spawn supplies and clean attack/warning/tether state.
- Scene exit cleans up boss, crowd, persistent enemy effects, warnings, pending attacks and supply.
- Short opening instruction and in-world active/paused/spent/damaged feedback should explain the goal without stopping combat. Exact wording, finale/chapter name, particles, colors and victory presentation are delegated.

## Configurable settings
Use dedicated finale-owned settings; do not overwrite legacy objectives, other bosses or shared ordinary enemy profiles. The chosen behaviors below are requirements, not a request to expose every unselected alternative as a mode.

| Setting/group | Agreed value or initial-value owner |
| --- | --- |
| Arena dimensions | 100 × 100 metres |
| Chapter/level | Ninth chapter; level 81 only |
| Anchor count/types | Exactly four; one covered target, shell/core, short rail, narrow target |
| Downing condition | Any one completed anchor |
| Anchor lifecycle | Spent until all four used; full reset on resumed flight after fourth |
| Partial progress | Persistent across downings |
| Transition/downed anchor interaction | Paused; no queued downings or window extension |
| Boss stages | Exactly three, health-based |
| Vulnerability | Fixed duration starting after landing; initial stun then grounded attacks |
| Boss damage ownership | Player-owned bodies/chains and associated explosions |
| Installation ownership | Player-owned launched bodies/explosions; direct-punch exposed-core exception |
| Crowd supply | Continuous bounded replenishment |
| Duration/downings target | 6–8 minutes; approximately 6–8 downings in typical play |
| Boss scale, collider, health, flight height/offset/turning | Codex chooses editable initial values |
| Stage health thresholds/attack and crowd recipes | Codex chooses editable initial values |
| Fall/takeoff durations, downed duration, initial stun | Codex chooses editable initial values |
| Airborne/ground attacks: warnings, cadence, areas, damage/push | Codex chooses editable initial values |
| Anchor durability, core health, opening width/rotation | Codex chooses editable initial values using existing mechanics |
| Short rail route, step count/distance, slide duration | Codex chooses editable initial values |
| Crowd total/per-type limits, placement and replenishment | Codex chooses editable initial values with ammunition safeguards |
| Rebound strength and player-avoidance angle | Codex chooses editable initial values |

## Verified integration evidence
Inspected repository: [marcin-priachin/CrowdPunch](https://github.com/marcin-priachin/CrowdPunch), default branch `main`, commit `a254df1f5e8da032375670a6cb35fb6ed5ef42af` (14 October 2026). Root `AGENTS.md` is the only AGENTS.md found in the retrieved recursive tree. This is a planning inspection, not runtime validation.

Read design/architecture sources:
- `Docs/Design/GDD.md`: VISION-001..005, LOOP-002..009, BARRICADE-001..009, COVER-001..005, SHELL-001..006, TRACK-001..006 and relevant enemy rules.
- `Docs/Design/OpenQuestions.md`.
- `Docs/Architecture/CurrentArchitecture.md`: campaign, enemy integration and existing objective descriptions.

Verified source and serialized data:
- `Assets/CrowdPunch/Data/Campaign/Campaign.asset`: eight chapter names and 80 available level entries ending at cp-080.
- `Scripts/Configuration/CampaignCatalog.cs` (under `Assets/CrowdPunch`): arrays of chapter names and level records; no per-chapter level range is encoded here.
- `Scripts/Mono/Levels/CampaignProgress.cs`: completion saved by stable string IDs; NextUnfinished and unlock checks operate on catalog count. This supports retaining IDs when appending content; menu/chapter indexing still needs inspection during implementation.
- `Scripts/Systems/Combat/BarricadeHitResolution.cs`: per-source/launch hit deduplication, covered-target explosion eligibility and hit-count destruction.
- `Scripts/Systems/Combat/ShellHitResolution.cs`: separate shell/core blast resolution, punches damage exposed core, once-per-launch core body damage.
- `Scripts/Systems/Combat/TrackObjectHitResolution.cs`: player-only filtering options, directional steps, clamping and history.
- `Scripts/Systems/Physics/TrackObjectArrivalSystem.cs`: locks after physical arrival and uses shared barricade completion state.
- `Scripts/Systems/Presentation/GauntletCompletionSystem.cs`: boss states and objective flags report encounter completion. Its barricade loop can override the earlier boss-completion decision. Finale anchor success must therefore be routed to downing without premature encounter completion.
- `Scripts/Components/EnemyArchetype.cs`: ordinary enum identities include the established archetypes; Snake has separate component-based implementation described in architecture docs.
- Supplied Goleling FBX path exists as a Git LFS pointer (820,204 bytes). Binary model, animations, and suitability of exact clips were **not** inspected.

### Design changes and integration limits
The request deliberately supersedes LOOP-002's exact 80-level/eight-chapter campaign cap by appending a one-level ninth chapter. Final-completion and chapter-length assumptions need updating while preserving saves.

Objective systems are reusable mechanics, not drop-in finale behavior: their current completion, replenishment, ownership and reset coupling require explicit finale integration. In particular the shell anchor's player-owned-explosion eligibility differs from the existing ordinary shell accepting any explosion. Existing rotating-cover return shots target the player; the new **boss-body rebound safeguard** applies to the grounded boss and does not silently revise rotating-cover reflection.

Respect the hybrid architecture: GameObject player/camera/UI; ECS crowd/combat/lifecycle and narrow bridges. Refresh relevant source before implementation if main has advanced. No new implementation was authored or tested during this design conversation.

## Established exclusions and corrections
- No direct-punch boss damage.
- No mandatory surviving-crowd cleanup after boss death.
- No automatic queued downing or damage-window extension from completing another anchor while grounded.
- No extra anchor recharge wait.
- No boss movement from launched impacts.
- Shell cracking alone does not activate its anchor; destroy the exposed core, which explicitly accepts player punches.
- Boss rebounds must not send enemies directly toward the player.
Other offered but unselected options were not individually established as rejected with a reason.

## Delegated choices and open questions
Codex chooses all remaining numerical tuning, exact geometry/landing points, phase/collision implementation, eligible event attribution details, safeguards, animation clips, minor presentation, chapter/encounter names and short victory sequence. Keep these consistent with the rules above and expose relevant numerical values in dedicated finale settings.

**No consequential feature-scope design question remains.** Balance, readability, model animation fit, actual encounter duration and representative performance require playtesting; global hardware/FPS targets remain under OQ-001.

## Observable acceptance criteria
1. Level 81 is accessible after level 80 in its own one-level chapter; existing saves and all prior completions remain intact.
2. During flight, the large boss follows above/ahead without obscuring critical shot or warning information, using both attack families.
3. All four anchor types can be pursued or switched freely; completing any one pulls the boss down near that anchor and cancels pending airborne attacks.
4. Partial progress persists; used anchors stay spent, and the full set resets only on resumed flight following use of all four.
5. Anchors do not accept progress, queue activations or extend vulnerability during falling/downed/takeoff phases.
6. Player-owned launched impacts/blasts advance eligible installations; other ownership does not. Exposed shell core accepts direct punches; intact shell and boss do not.
7. Landing has a clear damaging/pushing warning; takeoff is harmless. Vulnerability timer begins after landing.
8. Downed boss first stuns, then alternates telegraphed slams/shockwaves without moving from impacts, remaining vulnerable until timer expiry.
9. Player-owned bodies/chains/blasts damage the vulnerable boss. Bodies damage once per continuous launch; Exploders can deal impact plus blast. Normal bodies rebound without being sent directly toward the player.
10. Every available enemy type appears over time within a bounded supply; reliable nearby Baselines/Exploders prevent ammunition softlocks.
11. Three health-based stages increase pressure without changing ordinary enemy stats or installation rules. Boss health persists between cycles.
12. Boss death immediately stops hostile combat and supply, neutralizes survivors and leads to campaign completion without clearing requirements.
13. Retry and scene exit leave no retained damage, spent anchors, pending attacks, orphan crowd/effects or false completion.
14. Editable initial values support tuning toward 6–8 minutes and roughly 6–8 downings without enforcing either.

