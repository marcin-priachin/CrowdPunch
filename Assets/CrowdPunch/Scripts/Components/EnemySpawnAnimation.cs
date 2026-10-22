using Unity.Entities;

namespace CrowdPunch.Components
{
    // Configuration survives pooling; enabled only while the authored emergence is playing.
    public struct EnemySpawnAnimation : IComponentData, IEnableableComponent
    {
        public float Duration;
        public byte UseAnimation;
    }
}
