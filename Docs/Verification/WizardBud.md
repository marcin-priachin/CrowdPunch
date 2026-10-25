# Bud Wizard verification (2026-10-25)

Requirements: WIZARD-001/006 and INFO-004/006. The latest user decision restores
original unsmoothed fly motion and supersedes the earlier per-frame grounding,
planted-leaf, sway-remapping and movement-damping adjustments.

- Model: `Assets/Bud Cute Series/Prefabs/Bud.prefab`, with the existing 3x visual-root
  Inspector scale and original child scales. Textured `CrowdPunch/Cel/Enemy` shading
  and animation-driven readability remain in use.
- Each non-spawn flight clip retains its original sampled motion, including vertical bob
  and leaf articulation. Export adds one constant vertical translation for the whole
  clip, placing its cycle minimum at zero; playback interpolates the raw poses.
  Death instead aligns each sampled pose to the shared movement clearance, preventing
  early death frames from hovering due to the lower final fallen pose.
- In `Assets/CrowdPunch/Data/Settings/Enemies/WizardEnemySpawnSettings.asset`, expand
  Wizard > Wizard Settings > Animation Ground Clearance. Default: **0.1 world metres**.
  This controls clearance at the lowest point of each full animation cycle, rather
  than holding every frame at the same height. It affects visuals only.
- `WizardSettings.AnimationGroundClearance` follows the existing spawn-profile data
  flow into ECS. The animation baker records the world-to-animation height conversion
  (1/3 at the current scale). Render bounds cover both the configurable lift and spawn.
- Spawn From Ground remains enabled, using Spawn to Ground Idle with underground
  emergence. Export limits its vertical rise to the fly-idle destination height and blends
  the final quarter to that exact pose. Clearance eases in during the rise; a 0.12 s
  handoff into active flight replaces the extra post-spawn takeoff. Fly Projectile Upward
  loops during casting; Fly Spin Attack supplies launched motion; Fly Take Damage and
  Fly Die supply hit and defeat presentation. Root motion stays disabled.

Validation:

- Unity 6000.3.10f1 compilation succeeded; all **48 Wizard-filtered tests passed**.
- Exported movement matrices match the original Fly Forward In Place clip up to a
  single constant height translation, with no smoothing or leaf modifications.
- Runtime geometry checks verify cycle minima at 0, 0.1 and 0.4 m, including 1x and
  3x scale conversion, preserved physics transforms and expanded culling bounds.
- Prior clearance-integration check: live Campaign 31 advanced from frame 1/time 0 to frame 1939/time 17.304 s. Its Wizard
  reached Active casting with baked clearance 0.1, height conversion 0.333333343 and
  one correctly linked animation-bounds entity. This was an automated runtime check,
  not a full human playthrough or target-device test.
- Isolated workload: 250 moving Buds, 15 bones, 30 warmups and 200 completed animation
  updates, including configurable clearance and bounds: 0.0472 ms/update. Excludes
  rendering, physics, GPU and full-frame cost; this is not an FPS measurement.
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:q` exited 1 with zero
  reported warnings/errors and no actionable diagnostics. Unity compilation and test
  execution provide the successful compilation evidence.
Spawn-height follow-up validation:

- Runtime skinning tests cover the complete spawn at 0.1 and 0.4 m clearance and 1x/3x
  scale. Every pose stays below the destination fly height and starts underground.
- The last spawn pose and first active pose match, including when movement begins
  immediately. The handoff completes without another takeoff, and physics transforms
  remain unchanged. Original non-spawn flight motion still passes its preservation checks.
- Corrected emergence is included in exported culling bounds.
- Isolated animation workload: 250 spawning Buds, 15 bones each, 30 warmups and
  200 completed updates: 0.1618 ms/update. Excludes physics, rendering, GPU and whole-frame
  cost. No new full campaign playthrough was performed for this follow-up.

Death-altitude follow-up:

- The Fly Die pose articulation and duration are retained. Each exported death pose is
  vertically aligned to zero before the existing configurable movement clearance is
  applied. This removes the elevated start while keeping the corpse above the floor.
- The change is in sample export only; no runtime transform writes, scale changes or
  new per-frame work were added. Tests cover the complete death clip at 1x/3x scale
  and 0.1/0.4 m clearance, culling bounds and the held terminal pose.

- Final death-altitude validation: all 48 Wizard-filtered tests passed. The saved
  visual scale remains 3x and clearance remains 0.1 m. No new campaign playthrough
  was performed for this sample-only correction.
- Isolated death-animation workload: 250 defeated Buds, 15 bones, 30 warmups and
  200 completed updates measured 0.2127 ms/update, excluding rendering, physics, GPU
  and full-frame cost. This is not an FPS measurement.
