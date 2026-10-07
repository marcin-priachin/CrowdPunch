using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct RollingPresentationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double now=SystemAPI.Time.ElapsedTime;
            foreach(var (owner,color) in SystemAPI.Query<RefRO<RollingVisualOwner>,RefRW<URPMaterialPropertyBaseColor>>())
            {
                if(!SystemAPI.HasComponent<RollingBoss>(owner.ValueRO.Value)) continue;
                var b=SystemAPI.GetComponent<RollingBoss>(owner.ValueRO.Value);
                var t=SystemAPI.GetComponent<RollingTuning>(owner.ValueRO.Value);
                float3 tint=b.Phase==RollingPhase.Roll?t.RollColor:b.Phase==RollingPhase.WindUp?t.WindUpColor:t.PauseColor;
                if(b.Phase==RollingPhase.Defeated) tint=new float3(.2f);
                else if(now<b.InvulnerableUntil) tint=t.ProtectionColor*(1+.25f*math.sin((float)now*t.ProtectionPulse));
                color.ValueRW.Value=new float4(tint,1);
            }
        }
    }
}
