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
                bool alive=SystemAPI.HasComponent<FinaleBoss>(owner)
                    && SystemAPI.GetComponent<FinaleBoss>(owner).Phase!=FinalePhase.Defeated
                    || SystemAPI.HasComponent<BossEncounter>(owner)
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
                // FINALE-006: a Snake slot includes its tail. Wait for old detached projectiles
                // before replacing the head, so repeated head kills cannot grow physical population.
                if (alive && SystemAPI.HasComponent<FinaleBoss>(owner) && SystemAPI.HasComponent<SnakeHead>(entity))
                    foreach (var segment in SystemAPI.Query<RefRO<SnakeSegment>>())
                        if (segment.ValueRO.Head==entity && segment.ValueRO.Detached!=0)
                            respawn.ValueRW.Enabled=0;
            }
        }
    }
}
