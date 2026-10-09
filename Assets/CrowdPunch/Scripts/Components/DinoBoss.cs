using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum DinoPhase : byte { Chase, Warning, Burst, Stagger, Defeated }
    public struct DinoBoss : IComponentData
    {
        public DinoPhase Phase;
        public int Stage, SuccessfulHits;
        public float Remaining;
        public float3 Direction, PreviousPosition;
        public double NextPlayerContact;
    }
    public struct DinoTuning : IComponentData
    {
        public int RequiredHits;
        public int EnemiesPerPillar;
        public float PillarCrowdRadius;
        public float3 ChaseSpeeds, ChaseDurations;
        public float BurstMultiplier, ChaseTurnDegrees, BurstTurnDegrees, Acceleration;
        public float WarningDuration, BurstDuration, StaggerDuration;
        public float BodyRadius, BodyHeight, Mass, CrowdPush;
        public float PlayerDamage, PlayerPush, PlayerProtection, ContactInterval;
        public float3 ChaseColor, WarningColor, BurstColor, StaggerColor;
        public float3 InitialPosition;
        public quaternion InitialRotation;
    }
    public struct DinoVisualOwner : IComponentData { public Entity Value; }
}
