using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(BossCollisionSystem))]
    [UpdateBefore(typeof(OutOfBoundsSystem)), UpdateBefore(typeof(EnemyRespawnSystem))]
    public partial struct BossCrowdReplenishmentSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach(var (member,respawn) in SystemAPI.Query<RefRO<BossCrowdMember>,RefRW<EnemyRespawnSettings>>())
                respawn.ValueRW.Enabled=(byte)(member.ValueRO.ReplenishDelay>=0
                    && SystemAPI.HasComponent<BossEncounter>(member.ValueRO.Encounter)
                    && SystemAPI.GetComponent<BossEncounter>(member.ValueRO.Encounter).Cycle!=BossCycle.Defeated?1:0);
        }
    }
}
