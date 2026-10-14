using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Initialization
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateBefore(typeof(EnemyWaveSpawnSystem))]
    public partial struct FinaleSupplySystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if(!SystemAPI.HasSingleton<FinaleBoss>() || SystemAPI.GetSingleton<FinaleBoss>().Phase==FinalePhase.Defeated) return;
            var player=SystemAPI.HasSingleton<PlayerSnapshot>()?SystemAPI.GetSingleton<PlayerSnapshot>():default;
            using var commands=new EntityCommandBuffer(Allocator.Temp);
            foreach(var (supply,sequence,e) in SystemAPI.Query<RefRO<FinaleSupply>,RefRW<EnemyWaveSequence>>().WithEntityAccess()) {
                var s=supply.ValueRO;
                if(s.PairOnly!=0 && sequence.ValueRO.Phase==EnemyWaveRuntimePhase.AwaitingActivation) {
                    bool live=false;
                    foreach(var (owner,launch,enemy) in SystemAPI.Query<RefRO<EnemyWaveOwnership>,RefRO<EnemyLaunchState>>()
                        .WithOptions(EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess()) {
                        if(owner.ValueRO.Sequence!=e) continue;
                        live |= !SystemAPI.IsComponentEnabled<RespawnRequest>(enemy) && launch.ValueRO.Phase!=EnemyLaunchPhase.Defeated;
                    }
                    if(live) continue;
                    foreach(var (owner,enemy) in SystemAPI.Query<RefRO<EnemyWaveOwnership>>().WithOptions(EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess())
                        if(owner.ValueRO.Sequence==e) commands.DestroyEntity(enemy);
                    foreach(var (pair,pairEntity) in SystemAPI.Query<RefRO<LinkedPair>>().WithEntityAccess())
                        if(pair.ValueRO.Sequence==e) commands.DestroyEntity(pairEntity);
                    var old=sequence.ValueRO;
                    sequence.ValueRW=new EnemyWaveSequence {InitialSeed=old.InitialSeed,RandomState=old.RandomState,
                        RunGeneration=old.RunGeneration+1,MinimumPlayerDistance=old.MinimumPlayerDistance,
                        PlacementAttemptsPerEnemy=old.PlacementAttemptsPerEnemy};
                }
                if(s.LocalAmmunition==0 || !player.IsAvailable || math.distance(player.Position.xz,s.Bank.xz)>s.PlayerBankDistance) continue;
                foreach(var (owner,pose,launch,enemy) in SystemAPI.Query<RefRO<EnemyWaveOwnership>,RefRO<LocalTransform>,RefRO<EnemyLaunchState>>()
                    .WithNone<RespawnRequest>().WithEntityAccess()) {
                    if(owner.ValueRO.Sequence!=e || launch.ValueRO.Phase!=EnemyLaunchPhase.Active
                        || math.distance(pose.ValueRO.Position.xz,s.Bank.xz)<s.RecallDistance
                        || math.distance(pose.ValueRO.Position.xz,player.Position.xz)<20) continue;
                    // Reuse the fixed slots and safe warning/placement pipeline; no extra ammunition allocations.
                    SystemAPI.SetComponent(enemy,new RespawnRequest());
                    SystemAPI.SetComponentEnabled<RespawnRequest>(enemy,true);
                }
            }
            commands.Playback(state.EntityManager);
        }
    }
}
