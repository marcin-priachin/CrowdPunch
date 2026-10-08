using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Initialization
{
    internal static class DinoEncounterReset
    {
        internal static void Reset(EntityManager em)
        {
            using var bosses=em.CreateEntityQuery(typeof(DinoBoss));
            using var entities=bosses.ToEntityArray(Allocator.Temp);
            foreach(var e in entities)
            {
                var t=em.GetComponentData<DinoTuning>(e);
                em.SetComponentData(e,new DinoBoss { Stage=1,Phase=DinoPhase.Chase,Remaining=t.ChaseDurations.x,
                    Direction=math.forward(t.InitialRotation),PreviousPosition=t.InitialPosition });
                em.SetComponentData(e,new Health { Current=t.RequiredHits,Max=t.RequiredHits });
                em.SetComponentData(e,LocalTransform.FromPositionRotation(t.InitialPosition,t.InitialRotation)); em.SetComponentData(e,new PhysicsVelocity());
            }
            using var pillars=em.CreateEntityQuery(typeof(FallingPillar));
            using var pillarEntities=pillars.ToEntityArray(Allocator.Temp);
            foreach(var e in pillarEntities)
            {
                var p=em.GetComponentData<FallingPillar>(e); var t=em.GetComponentData<PillarTuning>(e);
                em.SetComponentData(e,new FallingPillar { Boss=p.Boss });
                em.SetComponentData(e,LocalTransform.FromPosition(t.InitialPosition));
                em.SetComponentData(e,new PhysicsCollider { Value=t.UprightCollider }); em.GetBuffer<PillarFallHit>(e).Clear();
            }
            using var animations=em.CreateEntityQuery(typeof(EnemyAnimation),typeof(EnemyAnimationPlayback));
            using var renderers=animations.ToEntityArray(Allocator.Temp);
            foreach(var e in renderers)
                if(em.HasComponent<DinoBoss>(em.GetComponentData<EnemyAnimation>(e).Owner)) em.SetComponentData(e,new EnemyAnimationPlayback());
        }
    }
}
