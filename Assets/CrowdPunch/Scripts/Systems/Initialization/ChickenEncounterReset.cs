using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Initialization
{
    internal static class ChickenEncounterReset
    {
        public static void Reset(EntityManager em)
        {
            using(var shots=em.CreateEntityQuery(ComponentType.ReadOnly<ChickenProjectile>())) em.DestroyEntity(shots);
            using var query=em.CreateEntityQuery(ComponentType.ReadOnly<ChickenBoss>());
            using var bosses=query.ToEntityArray(Allocator.Temp);
            foreach(var e in bosses)
            {
                var t=em.GetComponentData<ChickenTuning>(e);
                em.SetComponentData(e,new ChickenBoss { Stage=1, Phase=ChickenPhase.Pause,Remaining=t.OpeningPause,
                    Destination=t.InitialPosition,PreviousPosition=t.InitialPosition,Facing=t.InitialRotation });
                em.SetComponentData(e,new Health { Current=t.Health,Max=t.Health });
                em.SetComponentData(e,LocalTransform.FromPositionRotation(t.InitialPosition,t.InitialRotation));
                em.SetComponentData(e,new PhysicsVelocity());
                em.GetBuffer<ChickenHit>(e).Clear(); em.GetBuffer<ChickenHitHistory>(e).Clear();
            }
        }
    }
}
