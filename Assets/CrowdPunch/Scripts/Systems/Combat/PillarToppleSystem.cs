using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup),OrderLast=true)]
    [UpdateAfter(typeof(Physics.EnemyLaunchHomingSystem)),UpdateAfter(typeof(Physics.EnemyGroundConstraintSystem))]
    public partial struct PillarToppleSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<FallingPillar>(); state.RequireForUpdate<PhysicsWorldSingleton>(); }
        public void OnUpdate(ref SystemState state)
        {
            var em=state.EntityManager; var world=SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var hits=new NativeList<ColliderCastHit>(Allocator.Temp);
            foreach(var (pose,collider,launch,velocity,source) in
                SystemAPI.Query<RefRO<LocalTransform>,RefRO<PhysicsCollider>,RefRO<EnemyLaunchState>,RefRO<PhysicsVelocity>>()
                    .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if(!Eligible(launch.ValueRO) || math.lengthsq(velocity.ValueRO.Linear.xz)<.0001f) continue;
                var input=new ColliderCastInput(collider.ValueRO.Value,pose.ValueRO.Position,
                    pose.ValueRO.Position+velocity.ValueRO.Linear*SystemAPI.Time.DeltaTime,pose.ValueRO.Rotation,pose.ValueRO.Scale);
                hits.Clear(); world.CastCollider(input,ref hits);
                float blocker=2;
                foreach(var h in hits)
                    if(h.Entity!=source && !em.HasComponent<FallingPillar>(h.Entity) && math.abs(h.SurfaceNormal.y)<.7f
                        && math.dot(velocity.ValueRO.Linear,h.SurfaceNormal)<-.001f) blocker=math.min(blocker,h.Fraction);
                foreach(var h in hits)
                    if(h.Fraction<=blocker && em.HasComponent<FallingPillar>(h.Entity)
                        && math.abs(h.SurfaceNormal.y)<.7f && math.dot(velocity.ValueRO.Linear,h.SurfaceNormal)<-.001f)
                        TryTopple(em,h.Entity,launch.ValueRO,velocity.ValueRO.Linear,SystemAPI.Time.ElapsedTime,source);
            }
            hits.Dispose();
        }
        internal static bool Eligible(in EnemyLaunchState launch) => launch.Phase==EnemyLaunchPhase.Launched && launch.Owner==EnemyLaunchOwner.Player;
        internal static bool TryTopple(EntityManager em,Entity entity,in EnemyLaunchState launch,float3 incoming,double now,Entity source=default)
        {
            var p=em.GetComponentData<FallingPillar>(entity); var t=em.GetComponentData<PillarTuning>(entity);
            if(p.Phase!=PillarPhase.Upright || !Eligible(launch) || !em.HasComponent<DinoBoss>(p.Boss)
                || em.GetComponentData<DinoBoss>(p.Boss).Phase==DinoPhase.Defeated) return false;
            float3 offset=t.FallDirection==PillarFallDirection.TowardBoss
                ? em.GetComponentData<LocalTransform>(p.Boss).Position-t.InitialPosition : incoming;
            p.Direction=math.normalizesafe(new float3(offset.x,0,offset.z),math.normalizesafe(new float3(incoming.x,0,incoming.z),new float3(0,0,1)));
            p.Phase=PillarPhase.Falling; p.Elapsed=p.Angle=p.PreviousAngle=0; p.HitBoss=p.HitPlayer=0;
            p.FallSequence++; p.LastEvent=now;
            em.SetComponentData(entity,p);
            var history=em.GetBuffer<PillarFallHit>(entity); history.Clear();
            // The toppling contact is the source's pass-through contact for this fall.
            // Alternate launch mode must not immediately replace its Player-owned launch.
            if(source!=Entity.Null) history.Add(new PillarFallHit { Target=source });
            em.SetComponentData(entity,new PhysicsCollider { Value=t.UnavailableCollider });
            // The source is deliberately untouched: velocity, ownership and launch history survive.
            return true;
        }
    }
}
