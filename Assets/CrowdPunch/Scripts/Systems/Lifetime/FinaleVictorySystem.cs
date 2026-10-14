using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup), OrderLast=true)]
    public partial struct FinaleVictorySystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            bool clear=false;
            foreach(var boss in SystemAPI.Query<RefRW<FinaleBoss>>()) {
                if(boss.ValueRO.Phase!=FinalePhase.Defeated || boss.ValueRO.Neutralized!=0) continue;
                boss.ValueRW.Neutralized=1; clear=true;
            }
            if(!clear) return;
            using var commands=new EntityCommandBuffer(Allocator.Temp);
            using var enemies=SystemAPI.QueryBuilder().WithAll<Enemy>().WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build().ToEntityArray(Allocator.Temp);
            foreach(var e in enemies) commands.DestroyEntity(e);
            Destroy<LinkedPair>(ref state,commands); Destroy<RangedProjectile>(ref state,commands);
            Destroy<WizardZone>(ref state,commands); Destroy<TrailSection>(ref state,commands); Destroy<TrailSource>(ref state,commands);
            commands.Playback(state.EntityManager);
        }
        private static void Destroy<T>(ref SystemState state,EntityCommandBuffer commands) where T:unmanaged,IComponentData
        {
            using var q=state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            using var entities=q.ToEntityArray(Allocator.Temp);
            foreach(var e in entities) commands.DestroyEntity(e);
        }
    }
}
