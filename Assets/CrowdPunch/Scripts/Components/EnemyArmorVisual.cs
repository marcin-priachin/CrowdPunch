using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct EnemyArmorVisual : IComponentData
    {
        public Entity Owner;
        public Entity Animation;
        public byte StagesRequired;
        public AABB AuthoredBounds;
    }
}
