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
            {
                var owner=member.ValueRO.Encounter;
                bool alive=SystemAPI.HasComponent<BossEncounter>(owner)
                    && SystemAPI.GetComponent<BossEncounter>(owner).Cycle!=BossCycle.Defeated
                    || SystemAPI.HasComponent<ChickenBoss>(owner)
                    && SystemAPI.GetComponent<ChickenBoss>(owner).Phase!=ChickenPhase.Defeated;
                respawn.ValueRW.Enabled=(byte)(member.ValueRO.ReplenishDelay>=0 && alive?1:0);
            }
        }
    }
}
