using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct BossPresentationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            double now=SystemAPI.Time.ElapsedTime;
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
            foreach(var (owner,color) in SystemAPI.Query<RefRO<BossVisualOwner>,RefRW<URPMaterialPropertyBaseColor>>())
            {
                var part=SystemAPI.GetComponent<BossPart>(owner.ValueRO.Value);
                var boss=SystemAPI.GetComponent<BossEncounter>(part.Encounter);
                float3 c=new float3(1);
                if(boss.Cycle==BossCycle.Defeated) c=new float3(.25f);
                else if(boss.Cycle==BossCycle.Transition) c=new float3(1.7f,1.35f,.35f)*(1+.2f*math.sin((float)now*8));
                else if(part.Kind==BossPartKind.Head && now<boss.InvulnerableUntil)
                    c=new float3(.35f,1.7f,2)*(1+.2f*math.sin((float)now*22));
                else if(SystemAPI.HasComponent<BossHand>(owner.ValueRO.Value))
                {
                    var h=SystemAPI.GetComponent<BossHand>(owner.ValueRO.Value);
                    if(h.Phase==BossHandPhase.Anticipation) c=new float3(1.7f,1.2f,.3f);
                    else if(h.Phase==BossHandPhase.Active) c=new float3(1.8f,.4f,.15f);
                    else if(h.Phase==BossHandPhase.Staggered) c=new float3(.3f,1.5f,1.7f);
                    else if(h.Phase==BossHandPhase.Recovery) c=new float3(.55f);
                    else if(h.Phase==BossHandPhase.Shielding) c=new float3(.65f,.8f,1.4f);
                }
                if(SystemAPI.GetComponent<BossImpactFeedback>(owner.ValueRO.Value).FlashRemaining>0) c=math.lerp(c,new float3(3),.7f);
                color.ValueRW.Value=new float4(c*owner.ValueRO.Color.xyz,1);
            }
        }
    }
}
