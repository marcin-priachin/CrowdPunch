using Unity.Entities;

namespace CrowdPunch.Components
{
    public struct EnemyArmorSettings : IComponentData
    {
        public float StaggerDuration, KnockbackSpeed, InvulnerabilityDuration;
    }

    public struct EnemyArmor : IComponentData
    {
        public byte Stages;
        public uint HitSequence;
        public double ProtectedUntil, StaggerUntil;
        public static EnemyArmor Fresh => new EnemyArmor { Stages = 2 };
    }

    [InternalBufferCapacity(4)]
    public struct ArmorHitHistory : IBufferElementData
    {
        public Entity Source;
        public uint LaunchSequence;
    }
}
