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
    public partial struct RollingAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new Animate {
                Bosses = SystemAPI.GetComponentLookup<RollingBoss>(true),
                Tuning = SystemAPI.GetComponentLookup<RollingTuning>(true),
                Delta = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Animate : IJobEntity
        {
            [ReadOnly] public ComponentLookup<RollingBoss> Bosses;
            [ReadOnly] public ComponentLookup<RollingTuning> Tuning;
            public float Delta;

            public void Execute(in EnemyAnimation animation, ref EnemyAnimationPlayback playback,
                ref RollingAnimationState visual, ref DynamicBuffer<SkinMatrix> skin)
            {
                if (!Bosses.HasComponent(animation.Owner) || !animation.Samples.IsCreated) return;
                var boss = Bosses[animation.Owner];
                var tuning = Tuning[animation.Owner];
                ref var data = ref animation.Samples.Value;
                if (skin.Length != data.BoneCount) return;
                if (visual.Initialized != 0 && visual.PreviousPhase == RollingPhase.Roll && boss.Phase == RollingPhase.Pause)
                    visual.Returning = 1;
                visual.Initialized = 1;
                visual.PreviousPhase = boss.Phase;
                if (visual.AcceptedHits != boss.AcceptedHits)
                {
                    visual.AcceptedHits = boss.AcceptedHits;
                    playback.HitActive = 1;
                    playback.HitPhase = 0;
                }
                bool reaction = playback.HitActive != 0 && boss.Phase != RollingPhase.Roll && boss.Phase != RollingPhase.Defeated;
                int motion = boss.Phase == RollingPhase.Defeated ? 4 : boss.Phase == RollingPhase.Roll ? 1
                    : reaction ? 3 : boss.Phase == RollingPhase.WindUp ? 2 : visual.Returning != 0 ? 5 : 0;
                if (playback.Initialized != motion + 1)
                {
                    playback.Initialized = (byte)(motion + 1);
                    playback.Phase = 0;
                }
                bool loop = motion == 0 || motion == 1;
                if (motion == 2)
                    playback.Phase = 1 - math.saturate(boss.Remaining / math.max(.01f, tuning.WindUp));
                else if (motion == 3)
                {
                    // Show the authored reaction across the protection window; gameplay clocks keep running.
                    float duration = tuning.Invulnerability > .01f ? tuning.Invulnerability : data.Durations[3];
                    playback.HitPhase = math.saturate(playback.HitPhase + Delta / math.max(.01f, duration));
                    playback.Phase = playback.HitPhase;
                    if (playback.HitPhase >= 1) playback.HitActive = 0;
                }
                else
                {
                    float advance = Delta / math.max(.01f, data.Durations[motion]);
                    playback.Phase = loop ? math.frac(playback.Phase + advance) : math.saturate(playback.Phase + advance);
                }
                if (motion == 1 || motion == 2 || motion == 4)
                {
                    visual.Returning = 0;
                    if (motion != 2) playback.HitActive = 0;
                }
                if (motion == 5 && playback.Phase >= 1) visual.Returning = 0;
                Sample(ref data, ref skin, motion, playback.Phase, loop);
            }

            private static void Sample(ref EnemyAnimationSamples data, ref DynamicBuffer<SkinMatrix> skin, int motion, float phase, bool loop)
            {
                float frame = phase * (loop ? data.FrameCount : data.FrameCount - 1);
                int a = math.min((int)frame, data.FrameCount - 1);
                int b = loop ? (a + 1) % data.FrameCount : math.min(a + 1, data.FrameCount - 1);
                float fraction = math.frac(frame);
                for (int bone = 0; bone < skin.Length; bone++)
                {
                    var from = data.Matrices[(motion * data.FrameCount + a) * data.BoneCount + bone];
                    var to = data.Matrices[(motion * data.FrameCount + b) * data.BoneCount + bone];
                    // Matrix lerp shrinks rotated bones. Interpolate rotation independently of authored scale.
                    float3 sa = math.max(new float3(.000001f), new float3(math.length(from.c0), math.length(from.c1), math.length(from.c2)));
                    float3 sb = math.max(new float3(.000001f), new float3(math.length(to.c0), math.length(to.c1), math.length(to.c2)));
                    var ra = math.normalize(new quaternion(new float3x3(from.c0 / sa.x, from.c1 / sa.y, from.c2 / sa.z)));
                    var rb = math.normalize(new quaternion(new float3x3(to.c0 / sb.x, to.c1 / sb.y, to.c2 / sb.z)));
                    var rotation = new float3x3(math.slerp(ra, rb, fraction));
                    float3 scale = math.lerp(sa, sb, fraction);
                    skin[bone] = new SkinMatrix { Value = new float3x4(rotation.c0 * scale.x, rotation.c1 * scale.y,
                        rotation.c2 * scale.z, math.lerp(from.c3, to.c3, fraction)) };
                }
            }
        }
    }
}
