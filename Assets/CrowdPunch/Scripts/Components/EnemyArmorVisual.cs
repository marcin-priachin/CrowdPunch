using Unity.Entities;

namespace CrowdPunch.Components
{
    public struct EnemyArmorVisual : IComponentData
    {
        public Entity Owner;
        public byte StagesRequired;
    }
}
