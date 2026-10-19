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
    public partial struct ShellFragmentPresentationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (motion, part, visual, transform, color) in SystemAPI.Query<RefRW<ShellFragmentMotion>,
                RefRO<ShellVisual>, RefRO<BarricadeVisual>, RefRW<LocalTransform>, RefRW<URPMaterialPropertyBaseColor>>())
            {
                Entity target = visual.ValueRO.Barricade;
                if (!SystemAPI.HasComponent<ShellTarget>(target)) continue;
                var shell = SystemAPI.GetComponent<ShellTarget>(target);
                var solid = SystemAPI.GetComponent<Barricade>(target);
                if (motion.ValueRO.Initialized == 0)
                {
                    motion.ValueRW.Position = transform.ValueRO.Position;
                    motion.ValueRW.Rotation = transform.ValueRO.Rotation;
                    motion.ValueRW.Scale = transform.ValueRO.Scale;
                    motion.ValueRW.Initialized = 1;
                }
                var p = motion.ValueRO;
                bool core = part.ValueRO.IsCore != 0;
                float damage = core ? 1 - shell.CoreHealth / shell.CoreMaxHealth
                    : 1 - (float)shell.ExplosionsRemaining / shell.RequiredExplosions;
                bool broken = core ? shell.CoreHealth <= 0 : shell.ExplosionsRemaining == 0;
                float hitTime = math.max(0, (float)(now - solid.LastHitTime));
                float breakTime = math.max(0, (float)(now - (core ? solid.LastHitTime : shell.ShellBrokenAt)));
                float3 position = p.Position;
                quaternion rotation = p.Rotation;
                float scale = p.Scale;
                if (p.Kind >= 2)
                {
                    float age = p.Kind == 2 ? hitTime : breakTime;
                    bool active = p.Kind == 2 ? solid.HitSequence != 0 && shell.LastHitBlocked == 0 : broken;
                    float u = math.saturate(age / p.Duration);
                    age = active ? math.min(age, p.Duration) : 0;
                    scale *= active ? 1 - math.smoothstep(.55f, 1, u) : 0;
                    if (p.Kind == 3) scale *= math.lerp(.3f, 1.8f, u);
                    position += p.Velocity * age + new float3(0, (p.Kind == 2 ? -4 : .4f) * age * age, 0);
                    rotation = math.mul(p.Rotation, quaternion.EulerXYZ(p.Spin * age));
                }
                else if (broken)
                {
                    float u = math.saturate(breakTime / p.Duration);
                    float flight = math.min(breakTime, p.Duration);
                    position += p.Velocity * flight + new float3(0, -6 * flight * flight, 0);
                    position.y = math.max(p.RestingY, position.y);
                    var tumble = math.mul(p.Rotation, quaternion.EulerXYZ(p.Spin * flight));
                    var landed = quaternion.EulerXYZ(new float3(math.PI * .5f, p.Seed * math.PI * 2, .08f));
                    rotation = math.slerp(tumble, landed, math.smoothstep(.5f, .9f, u));
                    scale *= p.Kind == 1 ? 0 : 1 - math.smoothstep(.72f, 1, u);
                }
                else
                {
                    if (p.Kind == 1 && damage + .001f < visual.ValueRO.CrackStage / 3f) scale = 0;
                    float recoil = solid.HitSequence == 0 ? 0 : math.sin(hitTime * 40) * math.exp(-hitTime * 15);
                    position += math.normalizesafe(p.Velocity) * (damage * .055f + recoil * .065f);
                    rotation = math.mul(p.Rotation, quaternion.EulerXYZ(new float3(recoil * .045f, 0, (p.Seed - .5f) * damage * .09f)));
                }
                transform.ValueRW.Position = position;
                transform.ValueRW.Rotation = rotation;
                transform.ValueRW.Scale = scale;
                float flash = solid.HitSequence == 0 ? 0 : math.saturate(1 - hitTime / .14f);
                float4 tint = shell.LastHitBlocked != 0 ? new float4(.38f, .64f, .83f, 1) : new float4(1, .87f, .53f, 1);
                color.ValueRW.Value = math.lerp(visual.ValueRO.Color * new float4(new float3(1 - damage * .18f), 1), tint, flash * .65f);
            }
        }
    }
}
