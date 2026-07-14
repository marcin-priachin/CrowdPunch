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
}
