using Unity.Entities;
namespace CrowdPunch.Components
{
    public struct PillarCrowdMember : IComponentData
    {
        public Entity Pillar;
        public int Slot;
    }
}
