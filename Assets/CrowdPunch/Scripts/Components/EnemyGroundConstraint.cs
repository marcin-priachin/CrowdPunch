using Unity.Entities;

namespace CrowdPunch.Components
{
    public struct EnemyGroundConstraint : IComponentData
    {
        public float Height;
        public byte HasGround;
        public byte IsLocked;
        public byte SnapRequested;
    }
}
