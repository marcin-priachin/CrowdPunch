using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct ShellPresentationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (part, visual, transform, color) in SystemAPI.Query<RefRO<ShellVisual>,
                RefRW<BarricadeVisual>, RefRW<LocalTransform>, RefRW<URPMaterialPropertyBaseColor>>())
            {
                if (!SystemAPI.HasComponent<ShellTarget>(visual.ValueRO.Barricade)) continue;
                var shell = SystemAPI.GetComponent<ShellTarget>(visual.ValueRO.Barricade);
                var solid = SystemAPI.GetComponent<Barricade>(visual.ValueRO.Barricade);
                if (visual.ValueRO.Scale < 0)
                {
                    visual.ValueRW.Scale = transform.ValueRO.Scale;
                    visual.ValueRW.Position = transform.ValueRO.Position;
                }
                bool core = part.ValueRO.IsCore != 0;
                float damage = core ? 1 - shell.CoreHealth / shell.CoreMaxHealth
                    : 1 - (float)shell.ExplosionsRemaining / shell.RequiredExplosions;
                bool visible = visual.ValueRO.CrackStage == 0 || damage >= visual.ValueRO.CrackStage / 3f;
                float debris = core ? shell.CoreHealth <= 0 ? (float)(SystemAPI.Time.ElapsedTime - solid.LastHitTime) : 0
                    : shell.ExplosionsRemaining == 0 ? (float)(SystemAPI.Time.ElapsedTime - shell.ShellBrokenAt) : 0;
                float progress = math.saturate(debris / solid.DebrisDuration);
                transform.ValueRW.Scale = visible ? visual.ValueRO.Scale * (1 - progress) : 0;
                transform.ValueRW.Position = visual.ValueRO.Position + visual.ValueRO.DebrisDirection * progress;
                float flash = solid.HitSequence == 0 ? 0 : math.saturate(1 - (float)(SystemAPI.Time.ElapsedTime - solid.LastHitTime) / solid.FlashDuration);
                float4 tint = solid.HitSequence != 0 && shell.LastHitBlocked != 0
                    ? new float4(.35f, .5f, .75f, 1) : new float4(1, .8f, .3f, 1);
                color.ValueRW.Value = math.lerp(visual.ValueRO.Color * new float4(new float3(1 - damage * .35f), 1), tint, flash);
            }
        }
    }
}
