using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(BossCollisionSystem))]
    public partial struct BossPlayerImpactSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if(!SystemAPI.HasSingleton<PlayerSnapshot>()) return;
            var player=SystemAPI.GetSingleton<PlayerSnapshot>();
            foreach(var (hand,part,transform) in SystemAPI.Query<RefRW<BossHand>,RefRO<BossPart>,RefRO<LocalTransform>>())
            {
                var boss=SystemAPI.GetComponent<BossEncounter>(part.ValueRO.Encounter);
                var t=SystemAPI.GetComponent<BossTuning>(part.ValueRO.Encounter);
                float3 current=transform.ValueRO.Position;
                if(hand.ValueRO.Phase==BossHandPhase.Active && hand.ValueRO.PlayerHit==0
                    && boss.Cycle==BossCycle.Attacking && player.IsAvailable)
                {
                    float radius=part.ValueRO.Radius+player.Radius;
                    bool hit;
                    if(hand.ValueRO.Attack==BossAttack.Slam)
                        hit=current.y<=t.HandHeight+.5f && math.distance(current.xz,player.Position.xz)<=t.Slam.Width+player.Radius;
                    else
                        hit=LaunchedEnemyPlayerImpactSystem.SegmentIntersectsSphere(
                            new float3(hand.ValueRO.PreviousPosition.x,0,hand.ValueRO.PreviousPosition.z),new float3(current.x,0,current.z),
                            new float3(player.Position.x,0,player.Position.z),radius);
                    if(hit && PlayerBridgeRegistry.TryGetBridge(out var bridge))
                    {
                        hand.ValueRW.PlayerHit=1;
                        float3 direction=math.normalizesafe(new float3(player.Position.x-current.x,0,player.Position.z-current.z),hand.ValueRO.Direction);
                        bridge.ReceiveEnemyHit(t.PlayerDamage,t.PlayerInvulnerability,direction*t.PlayerKnockback);
                    }
                }
                hand.ValueRW.PreviousPosition=current;
            }
        }
    }
}
