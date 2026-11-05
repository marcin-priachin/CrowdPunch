using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct FinalePresentationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            bool defeated=false;
            foreach(var (boss,tuning,pose) in SystemAPI.Query<RefRO<FinaleBoss>,RefRO<FinaleTuning>,RefRO<LocalTransform>>()) {
                var b=boss.ValueRO; var t=tuning.ValueRO; defeated=b.Phase==FinalePhase.Defeated;
                var shape=b.Attack!=FinaleAttack.None?b.Attack:b.ResolvedAttack;
                float length=shape==FinaleAttack.Sweep?t.SweepLength:shape==FinaleAttack.Slam?t.SlamLength:0;
                float width=shape==FinaleAttack.Sweep?t.SweepWidth:shape==FinaleAttack.Slam?t.SlamWidth
                    :shape==FinaleAttack.Landing?t.LandingRadius:shape==FinaleAttack.Shockwave?t.ShockwaveRadius:t.StrikeRadius;
                float duration=b.Attack==FinaleAttack.Landing?t.FallSeconds:t.WarningSeconds[math.clamp(b.Stage-1,0,2)];
                float groundedWeight=b.Phase==FinalePhase.Downed || b.Phase==FinalePhase.Defeated?1:
                    b.Phase==FinalePhase.Falling?math.smoothstep(0,1,1-b.Remaining/math.max(.01f,t.FallSeconds)):
                    b.Phase==FinalePhase.TakingOff?1-math.smoothstep(0,1,1-b.Remaining/math.max(.01f,t.TakeoffSeconds)):0;
                FinalePresentation.Publish(pose.ValueRO.Position,pose.ValueRO.Rotation,(int)b.Phase,(int)b.Attack,b.WarningCenter,
                    b.WarningDirection,length,width,1-math.saturate(b.AttackRemaining/math.max(.01f,duration)),b.Health/t.Health,b.TetherOrigin,groundedWeight,
                    b.AttackSequence,b.ResolvedAttackSequence,(int)b.ResolvedAttack);
            }
            if(PlayerBridgeRegistry.TryGetBridge(out var bridge)) bridge.EncounterVictory=defeated;
            int i=0;
            foreach(var (anchor,pose) in SystemAPI.Query<RefRO<FinaleAnchor>,RefRO<LocalTransform>>()) {
                var a=anchor.ValueRO;
                FinalePresentation.Anchor(i++,pose.ValueRO.Position,a.Spent!=0?2:SystemAPI.GetComponent<FinaleBoss>(a.Boss).Phase==FinalePhase.Flying?0:1);
            }
        }
    }
}
