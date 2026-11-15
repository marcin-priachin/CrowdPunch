using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum SporellaShotKind : byte { EnemyShot, ImpactShot, PunchReturn }

    public struct SporellaProjectile : IComponentData
    {
        public Entity Arena;
        public Entity Emitter;
        public uint EmitterLifetime;
        public float3 Direction;
        public SporellaShotKind Kind;
        public float Distance, Range, Speed, Radius;
        public float PlayerDamage, PlayerInvincibility, EnemyDamage;
        public uint PlayerLayers, PunchSequence;
        public int ChainDepth;
        public bool CanRedirect, ExitingRedirectContact;
    }
}
