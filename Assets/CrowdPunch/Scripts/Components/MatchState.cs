using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Singleton state for high-level game flow.
    /// </summary>
    public struct MatchState : IComponentData
    {
        public bool IsRunning;
        public float ElapsedSeconds;
    }

    /// <summary>Global limit on ordinary melee enemies that actively close on the player.</summary>
    public struct EnemyCrowdPressureSettings : IComponentData
    {
        public int MaximumApproachingEnemies;
    }
}
