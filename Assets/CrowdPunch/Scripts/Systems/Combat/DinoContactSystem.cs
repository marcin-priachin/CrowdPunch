using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)),UpdateAfter(typeof(PillarFallContactSystem))]
    [UpdateBefore(typeof(Physics.EnemyRecoverySystem))]
    public partial struct DinoContactSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var player=SystemAPI.HasSingleton<PlayerSnapshot>()?SystemAPI.GetSingleton<PlayerSnapshot>():default;
            double now=SystemAPI.Time.ElapsedTime;
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
    }
}
