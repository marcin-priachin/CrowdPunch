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
    [UpdateInGroup(typeof(GamePostPhysicsGroup)),UpdateAfter(typeof(PillarFallContactSystem))]
    [UpdateBefore(typeof(EnemyLaunchCollisionSystem)), UpdateBefore(typeof(Physics.AuthoritativeLaunchFinishSystem))]
    [UpdateBefore(typeof(Physics.EnemyRecoverySystem))]
    public partial struct DinoContactSystem : ISystem
    {
        private const float MinimumArmorContactImpulse = .25f;

        public void OnCreate(ref SystemState state) => state.RequireForUpdate<DinoBoss>();

        public void OnUpdate(ref SystemState state)
        {
            var player=SystemAPI.HasSingleton<PlayerSnapshot>()?SystemAPI.GetSingleton<PlayerSnapshot>():default;
            double now=SystemAPI.Time.ElapsedTime;
            RecordArmorContacts(ref state, now);
            foreach(var (boss,tuning,pose,velocity) in SystemAPI.Query<RefRW<DinoBoss>,RefRO<DinoTuning>,RefRW<LocalTransform>,RefRW<PhysicsVelocity>>())
            {
                ref var b=ref boss.ValueRW; var t=tuning.ValueRO;
                pose.ValueRW.Position.y=t.InitialPosition.y; velocity.ValueRW.Linear.y=0; velocity.ValueRW.Angular=float3.zero;
                if(b.Phase==DinoPhase.Defeated) continue;
                if(player.IsAvailable && b.Phase!=DinoPhase.Stagger && now>=b.NextPlayerContact)
                {
                    float3 p=player.Position; p.y=pose.ValueRO.Position.y;
                    if(LaunchedEnemyPlayerImpactSystem.SegmentIntersectsSphere(b.PreviousPosition,pose.ValueRO.Position,p,t.BodyRadius+player.Radius))
                    {
                        b.NextPlayerContact=now+t.ContactInterval;
                        if(PlayerBridgeRegistry.TryGetBridge(out var bridge)) bridge.ReceiveEnemyHit(t.PlayerDamage,t.PlayerProtection,
                            math.normalizesafe(new float3(p.x-pose.ValueRO.Position.x,0,p.z-pose.ValueRO.Position.z),b.Direction)*t.PlayerPush);
                    }
                }
                foreach(var (ep,ev,launch,ec) in SystemAPI.Query<RefRO<LocalTransform>,RefRW<PhysicsVelocity>,RefRO<EnemyLaunchState>,RefRO<PhysicsCollider>>()
                    .WithAll<Enemy>().WithNone<RespawnRequest>())
                {
                    if(launch.ValueRO.Phase==EnemyLaunchPhase.Defeated) continue;
                    var bounds=ec.ValueRO.Value.Value.CalculateAabb();
                    float radius=math.max(bounds.Extents.x,bounds.Extents.z)*ep.ValueRO.Scale;
                    float3 offset=ep.ValueRO.Position-pose.ValueRO.Position; offset.y=0;
                    if(math.lengthsq(offset)>math.square(t.BodyRadius+radius+.12f)) continue;
                    float2 away=math.normalizesafe(offset,b.Direction).xz;
                    float outward=math.dot(ev.ValueRO.Linear.xz,away);
                    ev.ValueRW.Linear.xz+=away*math.max(0,t.CrowdPush-outward);
                }
            }
        }

        private void RecordArmorContacts(ref SystemState state, double now)
        {
            var bosses = SystemAPI.GetComponentLookup<DinoBoss>();
            // The committed launch sweep can hit before the solver reports a collision.
            foreach (var (path, launch) in SystemAPI.Query<RefRO<AuthoritativeLaunchPath>, RefRO<EnemyLaunchState>>()
                         .WithAll<Enemy>().WithNone<RespawnRequest>())
            {
                var contact = path.ValueRO;
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Launched
                    || launch.ValueRO.LaunchSequence != contact.LaunchSequence
                    || contact.ContactImpulse < MinimumArmorContactImpulse
                    || !bosses.TryGetComponent(contact.Contact, out var boss) || !CanRecordArmorContact(boss, now))
                    continue;
                RecordArmorContact(ref boss, contact.ContactPoint, now);
                bosses[contact.Contact] = boss;
            }

            if (!SystemAPI.TryGetSingleton<SimulationSingleton>(out var simulation)
                || !SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var world)) return;

            state.Dependency = new RecordArmorCollision
            {
                Bosses = bosses,
                Enemies = SystemAPI.GetComponentLookup<Enemy>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                World = world.PhysicsWorld,
                Now = now
            }.Schedule(simulation, state.Dependency);
        }

        private static bool CanRecordArmorContact(in DinoBoss boss, double now) =>
            boss.Phase != DinoPhase.Defeated && now >= boss.NextArmorContact;

        private static void RecordArmorContact(ref DinoBoss boss, float3 point, double now)
        {
            boss.ArmorContactSequence++;
            boss.ArmorContactPosition = point;
            boss.NextArmorContact = now + .17;
        }

        [BurstCompile]
        private struct RecordArmorCollision : ICollisionEventsJob
        {
            public ComponentLookup<DinoBoss> Bosses;
            [ReadOnly] public ComponentLookup<Enemy> Enemies;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<RespawnRequest> Respawns;
            [ReadOnly] public PhysicsWorld World;
            public double Now;

            public void Execute(CollisionEvent collision)
            {
                Entity bossEntity = Bosses.HasComponent(collision.EntityA) ? collision.EntityA : collision.EntityB;
                Entity body = bossEntity == collision.EntityA ? collision.EntityB : collision.EntityA;
                if (!Bosses.TryGetComponent(bossEntity, out var boss) || !CanRecordArmorContact(boss, Now)
                    || !Enemies.HasComponent(body) || !Launches.TryGetComponent(body, out var launch)
                    || launch.Phase != EnemyLaunchPhase.Launched
                    || (Respawns.HasComponent(body) && Respawns.IsComponentEnabled(body))) return;

                var contact = collision.CalculateDetails(ref World);
                if (contact.EstimatedImpulse < MinimumArmorContactImpulse) return;
                RecordArmorContact(ref boss, contact.AverageContactPointPosition, Now);
                Bosses[bossEntity] = boss;
            }
        }
    }
}
