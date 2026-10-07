using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(ChickenDamageSystem))]
    public partial struct ChickenPlayerImpactSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if(!SystemAPI.HasSingleton<PlayerSnapshot>()) return;
            var p=SystemAPI.GetSingleton<PlayerSnapshot>();
            if(!p.IsAvailable || !PlayerBridgeRegistry.TryGetBridge(out var bridge)) return;
            foreach(var (boss,tuning,pose) in SystemAPI.Query<RefRW<ChickenBoss>,RefRO<ChickenTuning>,RefRO<LocalTransform>>())
            {
                if(boss.ValueRO.Phase!=ChickenPhase.Rush || boss.ValueRO.RushHitPlayer!=0) continue;
                var t=tuning.ValueRO;
                float3 player=p.Position; player.y=pose.ValueRO.Position.y;
                if(!LaunchedEnemyPlayerImpactSystem.SegmentIntersectsSphere(boss.ValueRO.PreviousPosition,pose.ValueRO.Position,player,t.BodyRadius+p.Radius)) continue;
                boss.ValueRW.RushHitPlayer=1;
                bridge.ReceiveEnemyHit(t.PlayerDamage,t.PlayerProtection,
                    math.normalizesafe(new float3(p.Position.x-pose.ValueRO.Position.x,0,p.Position.z-pose.ValueRO.Position.z))*t.PlayerPush);
            }
        }
    }
}
