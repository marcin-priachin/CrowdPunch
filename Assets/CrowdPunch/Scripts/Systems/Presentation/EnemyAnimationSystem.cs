using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct EnemyAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new PlaybackJob
            {
                Movement = SystemAPI.GetComponentLookup<DesiredMovement>(true),
                Settings = SystemAPI.GetComponentLookup<EnemyMovementSettings>(true),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct PlaybackJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<DesiredMovement> Movement;
            [ReadOnly] public ComponentLookup<EnemyMovementSettings> Settings;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<RespawnRequest> Respawns;
            public float DeltaTime;

            private void Execute(in EnemyAnimation animation, ref EnemyAnimationPlayback playback, ref DynamicBuffer<SkinMatrix> skin)
            {
                Entity owner = animation.Owner;
                if (!Movement.HasComponent(owner) || !Settings.HasComponent(owner) || !Transforms.HasComponent(owner)
                    || !Launches.HasComponent(owner) || !animation.Samples.IsCreated) return;
                ref var samples = ref animation.Samples.Value;
                if (skin.Length != samples.BoneCount) return;
                if (Respawns.HasComponent(owner) && Respawns.IsComponentEnabled(owner) && Respawns[owner].IsPooled != 0)
                {
                    playback = default;
                    return;
                }

                if (playback.Initialized == 0)
                {
                    playback.Phase = (math.hash(new uint2((uint)owner.Index, (uint)owner.Version)) & 65535) / 65536f;
                    playback.Initialized = 1;
                }
                // COMBAT-010/011: impulses and deferred defeat never become running input.
                // Without authored reaction clips, retain the last pose during non-active phases.
                if (Launches[owner].Phase != EnemyLaunchPhase.Active) return;

                DesiredMovement intent = Movement[owner];
                float3 local = math.rotate(math.inverse(Transforms[owner].Rotation),
                    math.normalizesafe(intent.Direction) * math.max(0f, intent.Speed));
                float2 target = local.xz / math.max(0.01f, Settings[owner].MoveSpeed);
                target /= math.max(1f, math.length(target));
                float blend = animation.BlendResponse <= 0f ? 1f : 1f - math.exp(-animation.BlendResponse * DeltaTime);
                playback.Movement = math.lerp(playback.Movement, target, blend);
                float amount = math.saturate(math.length(playback.Movement));
                float sector = math.frac(math.atan2(playback.Movement.x, playback.Movement.y) / (2f * math.PI)) * 8f;
                // Float rounding can produce exactly 8 just left of forward.
                int first = (int)math.floor(sector) % 8;
                int second = (first + 1) % 8;
                float turn = math.frac(sector);
                float duration = math.lerp(samples.Durations[0],
                    math.lerp(samples.Durations[first + 1], samples.Durations[second + 1], turn), amount);
                playback.Phase = math.frac(playback.Phase + DeltaTime / math.max(0.01f, duration));
                float frame = playback.Phase * samples.FrameCount;
                int a = (int)frame;
                int b = (a + 1) % samples.FrameCount;
                float between = math.frac(frame);
                for (int bone = 0; bone < skin.Length; bone++)
                {
                    float3x4 idle = Sample(ref samples, 0, a, b, bone, between);
                    float3x4 left = Sample(ref samples, first + 1, a, b, bone, between);
                    float3x4 right = Sample(ref samples, second + 1, a, b, bone, between);
                    skin[bone] = new SkinMatrix { Value = idle * (1f - amount) + (left * (1f - turn) + right * turn) * amount };
                }
            }

            private static float3x4 Sample(ref EnemyAnimationSamples data, int motion, int a, int b, int bone, float t)
            {
                int start = motion * data.FrameCount * data.BoneCount;
                return data.Matrices[start + a * data.BoneCount + bone] * (1f - t)
                    + data.Matrices[start + b * data.BoneCount + bone] * t;
            }
        }
    }
}
