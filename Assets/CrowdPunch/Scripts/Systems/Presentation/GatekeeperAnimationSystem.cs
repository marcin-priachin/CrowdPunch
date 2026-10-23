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
                Bosses = state.GetComponentLookup<BossEncounter>(true),
                Tuning = state.GetComponentLookup<BossTuning>(true),
                Delta = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile] private partial struct Animate : IJobEntity
        {
            [ReadOnly] public ComponentLookup<BossEncounter> Bosses;
            [ReadOnly] public ComponentLookup<BossTuning> Tuning;
            public float Delta;
            public void Execute(in EnemyAnimation animation, in BossAnimationAnchor anchor,
                ref EnemyAnimationPlayback playback, ref DynamicBuffer<SkinMatrix> skin)
            {
                if (!Bosses.HasComponent(animation.Owner) || !animation.Samples.IsCreated) return;
                var boss = Bosses[animation.Owner]; var tuning = Tuning[animation.Owner];
                ref var samples = ref animation.Samples.Value;
                if (skin.Length != samples.BoneCount) return;
                if (boss.AcceptedHits != playback.ArmorHitSequence)
                {
                    playback.ArmorHitSequence = boss.AcceptedHits;
                    playback.HitActive = 1;
                    if (playback.Initialized == 4) playback.Phase = 0;
                }
                if (boss.Cycle == BossCycle.Transition || boss.Cycle == BossCycle.Defeated) playback.HitActive = 0;
                bool hit = playback.HitActive != 0;
                int motion = boss.Cycle == BossCycle.Defeated ? 4 : boss.Cycle == BossCycle.Transition ? 2 : hit ? 3 : boss.Cycle == BossCycle.Relocating ? 1 : 0;
                bool initialized = playback.Initialized != 0;
                if (playback.Initialized != motion + 1) { playback.Phase = 0; playback.Initialized = (byte)(motion + 1); }
                bool loop = motion < 2;
                float duration = motion == 2 ? tuning.TransitionDuration : samples.Durations[motion];
                playback.Phase = loop ? math.frac(playback.Phase + Delta / math.max(.01f, duration)) : math.saturate(playback.Phase + Delta / math.max(.01f, duration));
                if (motion == 3 && playback.Phase >= 1) playback.HitActive = 0;
                BossSkinPose.Sample(ref samples, ref skin, motion, playback.Phase, loop, anchor.Floor,
                    new float3(1), quaternion.identity,
                    initialized ? 1 - math.exp(-18 * Delta) : 1);
            }
        }
    }
}
