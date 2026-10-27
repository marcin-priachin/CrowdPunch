using Unity.Entities;

namespace CrowdPunch.Components
{
    public struct EnemyLandingAnimation : IComponentData
    {
        public float Duration;
        // Optional authored death clip, including defeats that did not follow a launch.
        public float OrdinaryDefeatDuration;
        public int DefeatFramesToSkip;
    }
}
