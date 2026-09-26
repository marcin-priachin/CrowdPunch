using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(ExplosionResolutionSystem))]
    [UpdateBefore(typeof(OutOfBoundsSystem)), UpdateBefore(typeof(EnemyRespawnSystem))]
    public partial struct BarricadeCrowdReplenishmentSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (member, respawn) in SystemAPI.Query<RefRO<BarricadeCrowdMember>, RefRW<EnemyRespawnSettings>>())
                respawn.ValueRW.Enabled = (byte)(SystemAPI.HasComponent<Barricade>(member.ValueRO.Barricade)
                    && SystemAPI.GetComponent<Barricade>(member.ValueRO.Barricade).HitsRemaining > 0 ? 1 : 0);
        }
    }
}
