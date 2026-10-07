using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(RollingDamageSystem))]
    public partial struct RollingPlayerImpactSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var p=SystemAPI.HasSingleton<PlayerSnapshot>()?SystemAPI.GetSingleton<PlayerSnapshot>():default;
            double now=SystemAPI.Time.ElapsedTime;
            foreach(var (boss,tuning,pose,velocity) in
                SystemAPI.Query<RefRW<RollingBoss>,RefRO<RollingTuning>,RefRW<LocalTransform>,RefRW<PhysicsVelocity>>())
            {
                // Explicit ground-plane constraint, independent of solver crowd contact.
                pose.ValueRW.Position.y=tuning.ValueRO.InitialPosition.y;
                velocity.ValueRW.Linear.y=0; velocity.ValueRW.Angular=float3.zero;
                var b=boss.ValueRO; var t=tuning.ValueRO;
                if(!p.IsAvailable || b.Phase==RollingPhase.Defeated || now<b.NextPlayerContact
                    || b.Phase!=RollingPhase.Roll && t.ContactDanger==RollingContactDanger.RollingOnly) continue;
                float3 player=p.Position; player.y=pose.ValueRO.Position.y;
                if(!LaunchedEnemyPlayerImpactSystem.SegmentIntersectsSphere(b.PreviousPosition,pose.ValueRO.Position,player,t.BodyRadius+p.Radius)) continue;
                boss.ValueRW.NextPlayerContact=now+t.ContactInterval;
                if(PlayerBridgeRegistry.TryGetBridge(out var bridge)) bridge.ReceiveEnemyHit(t.PlayerDamage,t.PlayerProtection,
                    math.normalizesafe(new float3(p.Position.x-pose.ValueRO.Position.x,0,p.Position.z-pose.ValueRO.Position.z),b.Direction)*t.PlayerPush);
            }
        }
    }
}
