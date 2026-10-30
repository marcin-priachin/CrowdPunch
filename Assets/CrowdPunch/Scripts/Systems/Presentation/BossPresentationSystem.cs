using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct BossPresentationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            float dt=SystemAPI.Time.DeltaTime;
            float groundHeight=SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var ground)?ground.SurfaceHeight:-1f;
            BossAttackTelegraphs.BeginFrame();
            foreach(var (part,feedback,entity) in SystemAPI.Query<RefRO<BossPart>,RefRW<BossImpactFeedback>>().WithEntityAccess())
            {
                feedback.ValueRW.FlashRemaining=math.max(0,feedback.ValueRO.FlashRemaining-dt);
                if(feedback.ValueRO.Pending!=0)
                {
                    feedback.ValueRW.Pending=0;
                    BossAttackTelegraphs.Impact(feedback.ValueRO.Position,feedback.ValueRO.Direction,
                        part.ValueRO.Kind==BossPartKind.Head?.45f:.25f);
                }
                var boss=SystemAPI.GetComponent<BossEncounter>(part.ValueRO.Encounter);
                if(boss.Cycle==BossCycle.Defeated || boss.Cycle==BossCycle.Transition || !SystemAPI.HasComponent<BossHand>(entity)) continue;
                var h=SystemAPI.GetComponent<BossHand>(entity);
                var t=SystemAPI.GetComponent<BossTuning>(part.ValueRO.Encounter);
                var a=BossHandCoordinationSystem.AttackTuning(h.Attack,t);
                BossAttackTelegraphs.Publish(part.ValueRO.Kind==BossPartKind.LeftHand?0:1,h.Target,h.Start,h.Direction,
                    SystemAPI.GetComponent<LocalTransform>(entity).Position,a.Width,h.Attack,h.Phase,h.AttackSequence,
                    1-math.saturate(h.Remaining/math.max(.01f,h.Duration)),groundHeight);
            }
        }
    }
}
