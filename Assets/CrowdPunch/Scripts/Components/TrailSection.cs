using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct TrailSection : IComponentData
    {
        public Entity Record, Source;
        public uint SourceLifetime;
        public float3 Start, End;
        public double CreatedAt, ExpiresAt;
        public float Width, Damage, TickInterval, PlayerProtection;
        public TrailImmunityMode Immunity;
        public TrailAvoidanceMode Avoidance;
        public byte DamagesEnemies, Launched;
        public EnemyLaunchOwner Owner;
        public int ChainDepth;
        public float4 Color;
    }
}
