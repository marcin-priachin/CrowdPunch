using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum BossPartKind : byte { Head, LeftHand, RightHand }
    public enum BossHandPhase : byte { Returning, Ready, Shielding, Anticipation, Active, Recovery, Staggered }
    public enum BossAttack : byte { Slam, Lunge, Sweep }

    public struct BossPart : IComponentData
    {
        public Entity Encounter;
        public BossPartKind Kind;
        public float Radius;
        public float3 InitialPosition;
        public quaternion InitialRotation;
    }

    public struct BossHand : IComponentData
    {
        public BossHandPhase Phase;
        public BossAttack Attack;
        public float Remaining, Duration;
        public float3 Start, Target, Direction, PreviousPosition;
        public double StaggerProtectedUntil;
        public uint AttackSequence;
        public byte PlayerHit;
    }

    public struct BossMotionTarget : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
    }

    public struct BossImpactFeedback : IComponentData
    {
        public float FlashRemaining;
        public byte Pending;
        public float3 Position, Direction;
    }

    public struct BossVisualOwner : IComponentData { public Entity Value; }
    public struct BossScatterHistory : IBufferElementData { public Entity Body; public uint AttackSequence; }
    public struct BossCrowdMember : IComponentData { public Entity Encounter; public float ReplenishDelay; }
    public struct BossCrowdSequence : IComponentData { public Entity Encounter; }
}
