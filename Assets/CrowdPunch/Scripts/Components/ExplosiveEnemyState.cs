using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>Persistent idempotency state reset by pooling or a game restart.</summary>
    public struct ExplosiveEnemyState : IComponentData
    {
        public byte HasExploded;
    }

    public struct ExplosiveDetonationRequest : IComponentData, IEnableableComponent
    {
    }
}
