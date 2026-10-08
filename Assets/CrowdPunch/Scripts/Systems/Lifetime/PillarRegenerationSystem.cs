using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Lifetime
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)),UpdateAfter(typeof(PillarFallContactSystem))]
    public partial struct PillarRegenerationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em=state.EntityManager; double now=SystemAPI.Time.ElapsedTime;
            var player=SystemAPI.HasSingleton<PlayerSnapshot>()?SystemAPI.GetSingleton<PlayerSnapshot>():default;
            foreach(var (pillar,tuning,pose,collider,history) in
                SystemAPI.Query<RefRW<FallingPillar>,RefRO<PillarTuning>,RefRW<LocalTransform>,RefRW<PhysicsCollider>,DynamicBuffer<PillarFallHit>>())
            {
                ref var p=ref pillar.ValueRW; var t=tuning.ValueRO;
                if(p.Phase==PillarPhase.Falling && p.Elapsed>=t.FallDuration+t.FallenDuration)
                {
                    p.Phase=p.HitBoss!=0?PillarPhase.Consumed:PillarPhase.Waiting;
                    p.RegenerateAt=now+t.RegenerationDelay; pose.ValueRW.Scale=0;
                }
                if(p.Phase!=PillarPhase.Waiting || now<p.RegenerateAt || !em.HasComponent<DinoBoss>(p.Boss)
                    || em.GetComponentData<DinoBoss>(p.Boss).Phase==DinoPhase.Defeated) continue;
                var bossTuning=em.GetComponentData<DinoTuning>(p.Boss);
                if(player.IsAvailable && Occupies(t,player.Position,player.Radius)
                    || Occupies(t,em.GetComponentData<LocalTransform>(p.Boss).Position,bossTuning.BodyRadius)) continue;
                // Push ordinary bodies clear before restoring collision; never start a launch.
                foreach(var (enemyPose,velocity,launch,enemyCollider) in
                    SystemAPI.Query<RefRO<LocalTransform>,RefRW<PhysicsVelocity>,RefRO<EnemyLaunchState>,RefRO<PhysicsCollider>>()
                        .WithAll<Enemy>().WithNone<RespawnRequest>())
                {
                    if(launch.ValueRO.Phase==EnemyLaunchPhase.Defeated) continue;
                    var aabb=enemyCollider.ValueRO.Value.Value.CalculateAabb();
                    float radius=math.max(aabb.Extents.x,aabb.Extents.z)*enemyPose.ValueRO.Scale;
                    if(!Occupies(t,enemyPose.ValueRO.Position,radius+.12f)) continue;
                    float3 offset=enemyPose.ValueRO.Position-t.InitialPosition; offset.y=0;
                    velocity.ValueRW.Linear.xz=math.normalizesafe(offset,new float3(1,0,0)).xz*math.max(2,t.PushSpeed);
                }
                // Delay restoration until pushes have actually cleared the space, avoiding overlap traps.
                bool occupied=false;
                foreach(var (ep,ec) in SystemAPI.Query<RefRO<LocalTransform>,RefRO<PhysicsCollider>>().WithAll<Enemy>().WithNone<RespawnRequest>())
                {
                    var aabb=ec.ValueRO.Value.Value.CalculateAabb();
                    occupied|=Occupies(t,ep.ValueRO.Position,math.max(aabb.Extents.x,aabb.Extents.z)*ep.ValueRO.Scale+.08f);
                }
                if(occupied) continue;
                p.Phase=PillarPhase.Upright; p.Elapsed=p.Angle=p.PreviousAngle=0; p.HitBoss=p.HitPlayer=0; p.LastEvent=now;
                pose.ValueRW=LocalTransform.FromPosition(t.InitialPosition); collider.ValueRW.Value=t.UprightCollider; history.Clear();
            }
        }
        internal static bool Occupies(in PillarTuning t,float3 position,float radius)
        {
            float2 outside=math.max(math.abs(position.xz-t.InitialPosition.xz)-t.Width*.5f,0);
            return math.lengthsq(outside)<=radius*radius && position.y>=t.InitialPosition.y-radius && position.y<=t.InitialPosition.y+t.Height+radius;
        }
    }
}
