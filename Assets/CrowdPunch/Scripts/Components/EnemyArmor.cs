using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct EnemyArmorSettings : IComponentData
    {
        public float StaggerDuration, KnockbackSpeed, InvulnerabilityDuration;
        public float4 FullColor, ChippedColor, CrackedColor, BrokenColor;
    }

    public struct EnemyArmor : IComponentData
    {
        public byte Stages;
        public uint HitSequence;
        public double ProtectedUntil, StaggerUntil;
        // Only the event that breaks armor may pass the damage gate during protection.
        public float PendingBreakDamage;
        public static EnemyArmor Fresh => new EnemyArmor { Stages = 3 };
    }

    [InternalBufferCapacity(4)]
    public struct ArmorHitHistory : IBufferElementData
    {
        public Entity Source;
        public uint LaunchSequence;
    }
}
