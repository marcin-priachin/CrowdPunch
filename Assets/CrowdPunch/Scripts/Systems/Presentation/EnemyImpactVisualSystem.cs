using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    // Final render-only composition after the role/telegraph colors. LocalTransform,
    // PostTransformMatrix, skin samples and physics geometry retain their owners.
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(DasherPresentationSystem))]
    [UpdateAfter(typeof(EnemyReadabilitySystem))]
    [UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial struct EnemyImpactVisualSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            new VisualJob { Feedback = SystemAPI.GetComponentLookup<EnemyImpactFeedback>(true) }.ScheduleParallel();
        }
        [BurstCompile]
        private partial struct VisualJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyImpactFeedback> Feedback;
            private void Execute(in EnemyVisualOwner owner, ref EnemyVisualDeformation visual,
                ref LocalToWorld world, ref URPMaterialPropertyBaseColor color)
            {
                if (!Feedback.HasComponent(owner.Value)) return;
                if (visual.Initialized == 0 || !world.Value.Equals(visual.LastOutput))
                {
                    visual.AuthoredWorld = world.Value;
                    visual.Initialized = 1;
                }
                var f = Feedback[owner.Value];
                float amount = f.Squash * f.Intensity * math.saturate(f.VisualRemaining / math.max(.001f, f.VisualDuration));
                float3 direction = math.normalizesafe(f.VisualDirection, math.up());
                var d = float3x3.identity * (1f + amount * .5f)
                    - new float3x3(direction * direction.x, direction * direction.y, direction * direction.z) * (amount * 1.5f);
                var original = visual.AuthoredWorld;
                world.Value = new float4x4(new float4(math.mul(d, original.c0.xyz), original.c0.w),
                    new float4(math.mul(d, original.c1.xyz), original.c1.w),
                    new float4(math.mul(d, original.c2.xyz), original.c2.w), original.c3);
                visual.LastOutput = world.Value;
                float flash = f.Flash * f.Intensity * math.saturate(f.FlashRemaining / math.max(.001f, f.FlashDuration));
                color.Value.xyz = math.lerp(color.Value.xyz, new float3(1f), flash);
            }
        }
    }
}
