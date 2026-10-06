---
name: implement-crowd-punch-design
description: Implement agreed Crowd Punch gameplay from an attached design conversation or summary in the current Unity repository, including code, settings, assets, level integration, and verification. Use for implementation of designed enemies, bosses, puzzles, hazards, objectives, power-ups, or variations. Exclude brainstorming and planning-only requests.
---

# Implement Crowd Punch Design

## Extract the agreed design

Read the supplied conversation and attachments. Start with the final summary and reconcile it with explicit answers and later corrections. Later explicit user decisions take precedence. Flag material contradictions; never treat assistant suggestions or unanswered options as agreement.

Build a compact checklist of behavior, interactions, settings and defaults, assets, introductory level, spawning, completion conditions, acceptance criteria, and delegated choices. Resolve numbered answers against their original questions. Never interpret a bare answer such as "2" without its option mapping. Distinguish option numbers from numeric values; preserve float defaults and all requested configurable alternatives.

Separate requirements, rejected alternatives, delegated details, and open questions. Do not reopen settled decisions. Choose minor timings, placeholder colors, and implementation details when delegated or immaterial to gameplay. Ask a focused question when ambiguity materially changes gameplay, while continuing independent work. Obtain unavailable source conversations instead of reconstructing decisions from memory.

## Inspect and map the implementation

Confirm the intended repository, read applicable AGENTS.md instructions, check working-tree changes, and preserve unrelated user work. Inspect current Unity/package versions, related code, configuration assets, prefabs, scenes, authoring/baking, wave replenishment, and level progression. Verify paths and architecture mentioned in the design against current files.

Map requirements to existing systems and changes. Reuse established ECS, Jobs, Burst, combat, spawning, UI, and asset patterns. Identify already-satisfied requirements. Avoid broad refactors, speculative frameworks, dependency upgrades, and unrelated mechanic changes.

Give a brief implementation outline and proceed within authorized scope. Do not require approval of routine reversible edits. Do not push, merge, publish, or deploy unless separately authorized.

## Complete the feature

Implement runtime behavior plus necessary authoring, baking, settings, assets, prefab/scene wiring, visuals, UI, and level integration. Do not stop at scripts when the design requires playable content. Expose all requested alternatives with exact defaults, including actual configuration asset values rather than only field initializers.

Follow existing Unity asset workflows. Preserve GUIDs and metadata; prefer available Editor tools over fragile manual scene edits. Verify supplied model paths. Use placeholders only when permitted; report missing required assets and continue independent work. Never invent an asset path or claim unchecked integration.

Review relevant interactions against the agreed rules:

- Direct punches, dash-punches, launched bodies, explosions, and combined impact/explosion events.
- Duplicate collision counting, per-launch identity, cooldowns, and invulnerability.
- Launch ownership by player, elites, or bosses; armor and unlaunchable enemies.
- Aim assist, trajectory preview, collision/pass-through, obstacles, and navigation.
- Replenishment, baseline ammunition, completion, cleanup, and reset.
- Controller behavior, visual feedback, readability, and crowd performance.

Do not impose old feature-specific rules on a new design. Integrate the specified introductory gauntlet and verify its numbering/placement against current progression. Do not add extra levels or mechanics without authorization.

## Verify the agreed behavior

Discover Unity CLI/Editor capabilities through current help or advertised tools; do not invent commands. Confirm the Editor project before mutations. When available, refresh/import, check compilation and console errors, run relevant tests, and inspect scene/prefab/settings references. Use meaningful behavioral checks for interaction risks rather than tests that merely mirror implementation.

Check acceptance criteria and relevant configuration alternatives, repeat hits, combined events, replenishment, completion, restart/cleanup, and level entry. Review the diff for unintended changes and asset omissions. Fix attributable failures and rerun affected checks.

Distinguish compilation from runtime verification and automated checks from human playtesting. If Unity or a required tool is unavailable, perform useful static/repository checks and state the remaining verification gap. Never claim an unperformed playtest or build.

## Report the result

State what was implemented, where it is playable, key settings/defaults, actual verification, and remaining blockers or human playtest checks. Mention material deviations and delegated choices. Keep the report concise and understandable without the design chat. Describe blocked or partial requirements accurately rather than claiming completion.
