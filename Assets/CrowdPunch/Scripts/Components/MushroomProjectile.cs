using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct MushroomProjectile : IComponentData
    {
        public Entity Arena;
        public float3 Direction;
        public float Speed, Radius, Range, RangeRemaining;
        public float PlayerDamage, PlayerInvincibility, EnemyDamage;
        public uint PlayerLayers, PunchSequence;
        public EnemyLaunchOwner Owner;
        public bool ExitingRedirectContact;
    }

    [InternalBufferCapacity(4)]
    public struct MushroomProjectileHit : IBufferElementData
    {
        public Entity Enemy;
        public uint Lifetime;
    }
}
