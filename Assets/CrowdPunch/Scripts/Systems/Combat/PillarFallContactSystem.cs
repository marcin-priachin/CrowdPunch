using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(Physics.EnemyRecoverySystem)),UpdateBefore(typeof(Lifetime.BossCrowdReplenishmentSystem))]
    public partial struct PillarFallContactSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<FallingPillar>(); }
        public void OnUpdate(ref SystemState state)
        {
            var em=state.EntityManager; float dt=SystemAPI.Time.DeltaTime; double now=SystemAPI.Time.ElapsedTime;
            var player=SystemAPI.HasSingleton<PlayerSnapshot>()?SystemAPI.GetSingleton<PlayerSnapshot>():default;
            foreach(var (pillar,tuning,history,entity) in
                SystemAPI.Query<RefRW<FallingPillar>,RefRO<PillarTuning>,DynamicBuffer<PillarFallHit>>().WithEntityAccess())
            {
                var p=pillar.ValueRO; var t=tuning.ValueRO;
                if(p.Phase!=PillarPhase.Falling || p.Elapsed>t.FallDuration+dt) continue;
                if(p.HitBoss==0 && em.HasComponent<DinoBoss>(p.Boss))
                {
                    var b=em.GetComponentData<DinoBoss>(p.Boss); var bt=em.GetComponentData<DinoTuning>(p.Boss);
                    var bossPosition=em.GetComponentData<LocalTransform>(p.Boss).Position;
                    if(PillarFallGeometry.SweptContact(t,p,b.PreviousPosition,bossPosition,bt.BodyRadius,bt.BodyHeight*.5f))
                    {
                        var health=em.GetComponentData<Health>(p.Boss);
                        if(PillarDamageResolution.HitBoss(ref p,ref b,ref health,bt))
                        {
                            p.LastEvent=now; em.SetComponentData(p.Boss,b); em.SetComponentData(p.Boss,health);
                            em.SetComponentData(p.Boss,new PhysicsVelocity());
                        }
                    }
                }
                float3 playerCenter=player.Position+new float3(0,player.Radius+.02f,0);
                if(player.IsAvailable && p.HitPlayer==0 && PillarFallGeometry.SweptContact(t,p,
                    playerCenter-player.Velocity*dt,playerCenter,player.Radius,player.Radius))
                {
                    p.HitPlayer=1; p.LastEvent=now;
                    if(PlayerBridgeRegistry.TryGetBridge(out var bridge)) bridge.ReceiveEnemyHit(t.PlayerDamage,t.PlayerProtection,p.Direction*t.PushSpeed);
                }
                using var contacts=new NativeList<Entity>(Allocator.TempJob);
                state.Dependency=new CollectContacts { Pillar=p,Tuning=t,History=history.AsNativeArray(),Contacts=contacts,Delta=dt }.Schedule(state.Dependency);
                state.Dependency.Complete();
                foreach(var target in contacts)
                {
                    history.Add(new PillarFallHit { Target=target }); p.LastEvent=now;
                    PillarDamageResolution.HitEnemy(em,entity,target,t,p.Direction,now);
                }
                pillar.ValueRW=p;
            }
        }
        [BurstCompile,WithAll(typeof(Enemy)),WithNone(typeof(RespawnRequest))]
        private partial struct CollectContacts : IJobEntity
        {
            public FallingPillar Pillar;
            public PillarTuning Tuning;
            [ReadOnly] public NativeArray<PillarFallHit> History;
            public NativeList<Entity> Contacts;
            public float Delta;
            public void Execute(in LocalTransform pose,in PhysicsVelocity velocity,in EnemyLaunchState launch,in PhysicsCollider collider,Entity entity)
            {
                if(launch.Phase==EnemyLaunchPhase.Defeated) return;
                foreach(var hit in History) if(hit.Target==entity) return;
                float3 position=pose.Position;
                if(math.distance(position.xz,Tuning.InitialPosition.xz)>Tuning.Height+math.length(velocity.Linear.xz)*Delta+2) return;
                var aabb=collider.Value.Value.CalculateAabb();
                float radius=math.max(aabb.Extents.x,aabb.Extents.z)*pose.Scale;
                float halfHeight=aabb.Extents.y*pose.Scale;
                float3 center=position+math.rotate(pose.Rotation,aabb.Center*pose.Scale);
                if(PillarFallGeometry.SweptContact(Tuning,Pillar,center-velocity.Linear*Delta,center,radius,halfHeight)) Contacts.Add(entity);
            }
        }
    }
}
