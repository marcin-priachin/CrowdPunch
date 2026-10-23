using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct BossPresentationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            float dt=SystemAPI.Time.DeltaTime;
            BossAttackTelegraphs.BeginFrame();
            foreach(var (part,feedback,entity) in SystemAPI.Query<RefRO<BossPart>,RefRW<BossImpactFeedback>>().WithEntityAccess())
            {
                feedback.ValueRW.FlashRemaining=math.max(0,feedback.ValueRO.FlashRemaining-dt);
                if(feedback.ValueRO.Pending!=0)
                {
                    feedback.ValueRW.Pending=0;
                    if(PlayerBridgeRegistry.TryGetBridge(out var bridge) && !FeedbackTimeController.IsSuspended)
                        bridge.ReceiveImpact(new CombatFeedbackMessage { Kind=CombatImpactKind.EnemyCollision,
                            Position=feedback.ValueRO.Position,Direction=feedback.ValueRO.Direction,
                            Intensity=part.ValueRO.Kind==BossPartKind.Head?.45f:.25f,PlayerOwned=true,ChainDepth=1 });
                }
                var boss=SystemAPI.GetComponent<BossEncounter>(part.ValueRO.Encounter);
                if(boss.Cycle==BossCycle.Defeated || boss.Cycle==BossCycle.Transition || !SystemAPI.HasComponent<BossHand>(entity)) continue;
                var h=SystemAPI.GetComponent<BossHand>(entity);
                if(h.Phase!=BossHandPhase.Anticipation) continue;
                var t=SystemAPI.GetComponent<BossTuning>(part.ValueRO.Encounter);
                var a=BossHandCoordinationSystem.AttackTuning(h.Attack,t);
                BossAttackTelegraphs.Publish(part.ValueRO.Kind==BossPartKind.LeftHand?0:1,h.Target,h.Start,h.Direction,a.Width,(int)h.Attack,
                    1-math.saturate(h.Remaining/math.max(.01f,h.Duration)));
            }
        }
    }
}
