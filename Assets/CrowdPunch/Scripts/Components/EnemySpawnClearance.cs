using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>Conservative prefab-collider radius used only for spawn occupancy checks.</summary>
    public struct EnemySpawnClearance : IComponentData
    {
        public float Value;
    }
}
