using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    // BARRICADE-001/003: cues reflect the open route; completion remains owned by the objective.
    [BurstCompile]
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct VillageExitPresentationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach(var (cue,transform,color) in SystemAPI.Query<RefRW<VillageExitCue>,
                RefRW<LocalTransform>,RefRW<URPMaterialPropertyBaseColor>>())
            {
                if(!SystemAPI.HasComponent<Barricade>(cue.ValueRO.Gate)) continue;
                if(cue.ValueRO.Initialized==0)
                {
                    cue.ValueRW.Position=transform.ValueRO.Position;
                    cue.ValueRW.Scale=transform.ValueRO.Scale;
                    cue.ValueRW.Initialized=1;
                }
                var gate=SystemAPI.GetComponent<Barricade>(cue.ValueRO.Gate);
                bool open=gate.HitsRemaining==0;
                float age=math.max(0,(float)(SystemAPI.Time.ElapsedTime-gate.LastHitTime));
                float reveal=open?math.smoothstep(0,.4f,age):0;
                float pulse=.5f+.5f*math.sin(age*3.5f-cue.ValueRO.Phase);
                color.ValueRW.Value=math.lerp(cue.ValueRO.LockedColor,
                    cue.ValueRO.OpenColor*new float4(.85f+.15f*pulse,.85f+.15f*pulse,.85f+.15f*pulse,1),reveal);
                // Keep the radius fixed: it represents the actual destination, not a growing trigger.
                transform.ValueRW.Scale=cue.ValueRO.Scale*(cue.ValueRO.Arrow!=0?reveal:1);
                transform.ValueRW.Position=cue.ValueRO.Position;
                if(cue.ValueRO.Arrow!=0) transform.ValueRW.Position.y+=reveal*(.025f+.025f*pulse);
            }
        }
    }
}
