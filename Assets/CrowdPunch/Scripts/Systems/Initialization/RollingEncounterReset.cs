using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Initialization
{
    internal static class RollingEncounterReset
    {
        public static void Reset(EntityManager em)
        {
            using var query=em.CreateEntityQuery(ComponentType.ReadOnly<RollingBoss>());
            using var bosses=query.ToEntityArray(Allocator.Temp);
            foreach(var e in bosses)
            {
                var t=em.GetComponentData<RollingTuning>(e);
                em.SetComponentData(e,new RollingBoss { Stage=1,CycleStage=1,Phase=RollingPhase.Pause,
                    Remaining=t.OpeningPause,PreviousPosition=t.InitialPosition,Direction=math.forward(t.InitialRotation) });
                em.SetComponentData(e,new Health { Current=t.Health,Max=t.Health });
                em.SetComponentData(e,LocalTransform.FromPositionRotation(t.InitialPosition,t.InitialRotation));
                em.SetComponentData(e,new PhysicsVelocity());
                em.GetBuffer<RollingHit>(e).Clear(); em.GetBuffer<RollingHitHistory>(e).Clear(); em.GetBuffer<RollingCrowdContact>(e).Clear();
            }
            using var animations=em.CreateEntityQuery(ComponentType.ReadWrite<RollingAnimationPivot>(),ComponentType.ReadWrite<EnemyAnimationPlayback>());
            using var renderers=animations.ToEntityArray(Allocator.Temp);
            foreach(var renderer in renderers)
            {
                var pivot=em.GetComponentData<RollingAnimationPivot>(renderer); pivot.Angle=0;
                em.SetComponentData(renderer,pivot); em.SetComponentData(renderer,new EnemyAnimationPlayback());
            }
        }
    }
}
