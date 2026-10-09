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
            foreach(var (member,respawn,entity) in SystemAPI.Query<RefRO<BossCrowdMember>,RefRW<EnemyRespawnSettings>>().WithEntityAccess())
            {
                var owner=member.ValueRO.Encounter;
                bool alive=SystemAPI.HasComponent<BossEncounter>(owner)
                    && SystemAPI.GetComponent<BossEncounter>(owner).Cycle!=BossCycle.Defeated
                    || SystemAPI.HasComponent<ChickenBoss>(owner)
                    && SystemAPI.GetComponent<ChickenBoss>(owner).Phase!=ChickenPhase.Defeated
                    || SystemAPI.HasComponent<RollingBoss>(owner)
                    && SystemAPI.GetComponent<RollingBoss>(owner).Phase!=RollingPhase.Defeated
                    || SystemAPI.HasComponent<DinoBoss>(owner)
                    && SystemAPI.GetComponent<DinoBoss>(owner).Phase!=DinoPhase.Defeated;
                if(SystemAPI.HasComponent<PillarCrowdMember>(entity))
                {
                    var local=SystemAPI.GetComponent<PillarCrowdMember>(entity);
                    alive=alive && SystemAPI.HasComponent<FallingPillar>(local.Pillar) && SystemAPI.HasComponent<DinoTuning>(owner)
                        && Initialization.PillarCrowdPlacement.Needed(SystemAPI.GetComponent<FallingPillar>(local.Pillar),
                            SystemAPI.GetComponent<DinoBoss>(owner),SystemAPI.GetComponent<DinoTuning>(owner));
                }
                respawn.ValueRW.Enabled=(byte)(member.ValueRO.ReplenishDelay>=0 && alive?1:0);
            }
        }
    }
}
