using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup)), UpdateAfter(typeof(BossPresentationSystem))]
    public partial struct BossHandPoseSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            foreach (var (owner, pose, world) in SystemAPI.Query<RefRO<BossVisualOwner>, RefRW<BossHandVisualPose>, RefRW<LocalToWorld>>())
            {
                if (!SystemAPI.HasComponent<BossHand>(owner.ValueRO.Value)) continue;
                ref var visual = ref pose.ValueRW;
                if (visual.Initialized == 0 || !world.ValueRO.Value.Equals(visual.LastOutput))
                {
                    visual.AuthoredWorld = world.ValueRO.Value;
                    visual.Initialized = 1;
                }
                var hand = SystemAPI.GetComponent<BossHand>(owner.ValueRO.Value);
                var root = SystemAPI.GetComponent<LocalToWorld>(owner.ValueRO.Value).Value;
                float progress = 1 - math.saturate(hand.Remaining / math.max(.01f, hand.Duration));
                float squash = hand.Phase == BossHandPhase.Anticipation ? .13f * progress
                    : hand.Phase == BossHandPhase.Recovery ? -.09f * math.sin(progress * math.PI * 2) * (1 - progress)
                    : hand.Phase == BossHandPhase.Staggered ? .1f * (1 - progress) : 0;
                float tilt = hand.Phase == BossHandPhase.Anticipation ? -.16f * progress
                    : hand.Phase == BossHandPhase.Staggered ? .2f * math.sin(progress * math.PI * 3) * (1 - progress) : 0;
                var accent = float4x4.TRS(float3.zero, quaternion.RotateX(tilt), new float3(1 + squash * .4f, 1 - squash, 1 + squash * .4f));
                // Recompose from the authored transform, so idle frames cannot accumulate scale.
                world.ValueRW.Value = math.mul(root, math.mul(accent, math.mul(math.inverse(root), visual.AuthoredWorld)));
                visual.LastOutput = world.ValueRO.Value;
            }
        }
    }
}
