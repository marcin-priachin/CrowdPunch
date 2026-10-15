using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup)), UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial struct GatekeeperAnimationSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new Animate {
                Bosses = SystemAPI.GetComponentLookup<BossEncounter>(true),
                Tuning = SystemAPI.GetComponentLookup<BossTuning>(true),
                Feedback = SystemAPI.GetComponentLookup<BossImpactFeedback>(true),
                Delta = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile] private partial struct Animate : IJobEntity
        {
            [ReadOnly] public ComponentLookup<BossEncounter> Bosses;
            [ReadOnly] public ComponentLookup<BossTuning> Tuning;
            [ReadOnly] public ComponentLookup<BossImpactFeedback> Feedback;
            public float Delta;
            public void Execute(in EnemyAnimation animation, in BossAnimationAnchor anchor,
                ref EnemyAnimationPlayback playback, ref DynamicBuffer<SkinMatrix> skin)
            {
                if (!Bosses.HasComponent(animation.Owner) || !animation.Samples.IsCreated) return;
                var boss = Bosses[animation.Owner]; var tuning = Tuning[animation.Owner];
                ref var samples = ref animation.Samples.Value;
                if (skin.Length != samples.BoneCount) return;
                bool hit = Feedback.HasComponent(animation.Owner) && Feedback[animation.Owner].FlashRemaining > 0;
                int motion = boss.Cycle == BossCycle.Defeated ? 4 : boss.Cycle == BossCycle.Transition ? 2 : hit ? 3 : boss.Cycle == BossCycle.Relocating ? 1 : 0;
                bool initialized = playback.Initialized != 0;
                if (playback.Initialized != motion + 1) { playback.Phase = 0; playback.Initialized = (byte)(motion + 1); }
                bool loop = motion < 2;
                float duration = motion == 2 ? tuning.TransitionDuration : samples.Durations[motion];
                playback.Phase = loop ? math.frac(playback.Phase + Delta / math.max(.01f, duration)) : math.saturate(playback.Phase + Delta / math.max(.01f, duration));
                float squash = motion == 2 ? .12f * math.sin(playback.Phase * math.PI) : 0;
                BossSkinPose.Sample(ref samples, ref skin, motion, playback.Phase, loop, anchor.Floor,
                    new float3(1 + squash * .5f, 1 - squash, 1 + squash * .5f), quaternion.identity,
                    initialized ? 1 - math.exp(-18 * Delta) : 1);
            }
        }
    }
}
