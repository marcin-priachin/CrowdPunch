using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Initialization
{
    internal static class FinaleEncounterReset
    {
        public static void Reset(EntityManager em)
        {
            if (Mono.Player.PlayerBridgeRegistry.TryGetBridge(out var bridge)) bridge.EncounterVictory=false;
            using var q=em.CreateEntityQuery(typeof(FinaleBoss));
            using var bosses=q.ToEntityArray(Allocator.Temp);
            foreach(var e in bosses) {
                var t=em.GetComponentData<FinaleTuning>(e);
                em.SetComponentData(e,new FinaleBoss { Health=t.Health,Stage=1,AttackRemaining=4 });
                em.SetComponentData(e,LocalTransform.FromPosition(0,t.FlightHeight,0));
                em.GetBuffer<FinaleHit>(e).Clear();
                ResetAnchors(em,e);
            }
        }

        public static void ResetAnchors(EntityManager em,Entity boss)
        {
            using var q=em.CreateEntityQuery(typeof(FinaleAnchor));
            using var anchors=q.ToEntityArray(Allocator.Temp);
            foreach(var e in anchors) {
                var a=em.GetComponentData<FinaleAnchor>(e); if(a.Boss!=boss) continue;
                a.Spent=0; em.SetComponentData(e,a);
                var wall=em.GetComponentData<Barricade>(e); wall.HitsRemaining=wall.RequiredHits; wall.HitSequence=0; wall.LastHitTime=0;
                em.SetComponentData(e,wall); em.SetComponentData(e,new PhysicsCollider {Value=wall.IntactCollider});
                em.GetBuffer<BarricadeHitHistory>(e).Clear(); em.GetBuffer<BarricadeRebound>(e).Clear();
                if(em.HasComponent<ShellTarget>(e)) {
                    var shell=em.GetComponentData<ShellTarget>(e); shell.ExplosionsRemaining=shell.RequiredExplosions;
                    shell.CoreHealth=shell.CoreMaxHealth; shell.LastHitBlocked=0; shell.ShellBrokenAt=0;
                    em.SetComponentData(e,shell);
                }
                if(em.HasComponent<TrackObject>(e)) {
                    var track=em.GetComponentData<TrackObject>(e); var pose=em.GetComponentData<LocalTransform>(e); pose.Position=track.Start;
                    em.SetComponentData(e,pose); em.SetComponentData(e,new TrackObjectState()); em.SetComponentData(e,new PhysicsVelocity());
                    em.GetBuffer<TrackPushHistory>(e).Clear(); em.GetBuffer<TrackPushContact>(e).Clear();
                }
                if(wall.Cover!=Entity.Null) {
                    var cover=em.GetComponentData<RotatingCover>(wall.Cover);
                    em.SetComponentData(wall.Cover,new RotatingCoverState {Angle=cover.InitialAngle});
                    var pose=em.GetComponentData<LocalTransform>(wall.Cover); pose.Rotation=quaternion.RotateY(cover.InitialAngle);
                    em.SetComponentData(wall.Cover,pose); em.GetBuffer<CoverReflection>(wall.Cover).Clear();
                }
            }
        }
    }
}
