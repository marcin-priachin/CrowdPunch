using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Initialization
{
    internal static class BossEncounterReset
    {
        public static void Reset(EntityManager em)
        {
            using(var query=em.CreateEntityQuery(ComponentType.ReadOnly<BossPart>()))
            using(var parts=query.ToEntityArray(Allocator.Temp))
            foreach(var e in parts)
            {
                var p=em.GetComponentData<BossPart>(e);
                em.SetComponentData(e,LocalTransform.FromPositionRotation(p.InitialPosition,p.InitialRotation));
                em.SetComponentData(e,new BossMotionTarget { Position=p.InitialPosition,Rotation=p.InitialRotation });
                em.SetComponentData(e,new PhysicsVelocity()); em.SetComponentData(e,new BossImpactFeedback());
                em.GetBuffer<CollisionDamageHistory>(e).Clear();
                if(em.HasComponent<BossHand>(e))
                {
                    em.SetComponentData(e,new BossHand { Phase=BossHandPhase.Returning,PreviousPosition=p.InitialPosition });
                    em.GetBuffer<BossScatterHistory>(e).Clear();
                }
                else
                {
                    var old=em.GetComponentData<BossEncounter>(e); var t=em.GetComponentData<BossTuning>(e);
                    em.SetComponentData(e,new BossEncounter { LeftHand=old.LeftHand,RightHand=old.RightHand,Stage=1,Cycle=BossCycle.Opening,Remaining=t.OpeningDuration });
                    em.SetComponentData(e,new Health { Current=t.Health,Max=t.Health });
                }
            }
        }
    }
}
