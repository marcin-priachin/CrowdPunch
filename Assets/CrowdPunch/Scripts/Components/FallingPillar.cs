using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Components
{
    public enum PillarPhase : byte { Upright, Falling, Waiting, Consumed }
    public enum PillarFallDirection : byte { IncomingTravel, TowardBoss }
    public enum PillarImpactMode : byte { DamageAndPush, DamageAndLaunch }
    public struct FallingPillar : IComponentData
    {
        public Entity Boss;
        public PillarPhase Phase;
        public float Elapsed, PreviousAngle, Angle;
        public float3 Direction;
        public byte HitBoss, HitPlayer;
        public double RegenerateAt, LastEvent;
        public uint FallSequence;
    }
    public struct PillarTuning : IComponentData
    {
        public float Height, Width, FallDuration, FallenDuration, RegenerationDelay;
        public float EnemyDamage, PlayerDamage, PushSpeed, LaunchSpeed, PlayerProtection;
        public PillarFallDirection FallDirection;
        public PillarImpactMode ImpactMode;
        public float3 InitialPosition;
        public BlobAssetReference<Collider> UprightCollider, UnavailableCollider;
    }
    public struct PillarFallHit : IBufferElementData { public Entity Target; }
    public struct PillarVisualOwner : IComponentData { public Entity Value; }
}
