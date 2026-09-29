using Unity.Entities;

namespace CrowdPunch.Components
{
    // SHELL-001..005: Barricade supplies solid geometry/aiming; this owns durability.
    public struct ShellTarget : IComponentData
    {
        public int RequiredExplosions, ExplosionsRemaining;
        public float CoreHealth, CoreMaxHealth, ExploderReplacementDelay;
        public double ReplaceExplodersAt, ShellBrokenAt;
        public byte ReplacementBatchActive, LastHitBlocked;
    }

    public struct ShellVisual : IComponentData
    {
        public byte IsCore;
    }
}
