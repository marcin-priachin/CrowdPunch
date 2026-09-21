using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
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
                Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                Dashers = SystemAPI.GetComponentLookup<DasherState>(true),
                RangedAttacks = SystemAPI.GetComponentLookup<RangedAttackState>(true),
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
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<DasherState> Dashers;
            [ReadOnly] public ComponentLookup<RangedAttackState> RangedAttacks;
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
                EnemyLaunchState launch = Launches[owner];
                if (animation.Profile == (byte)EnemyAnimationProfile.Dasher)
                {
                    AnimateDasher(owner, ref playback, ref skin, ref samples, launch);
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Explosive)
                {
                    AnimateExplosive(owner, ref playback, ref skin, ref samples, launch);
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Ranged)
                {
                    AnimateRanged(owner, ref playback, ref skin, ref samples, launch);
                    return;
                }
                if (launch.Phase == EnemyLaunchPhase.Launched)
                {
                    playback.Landing = 0;
                    bool newLaunch = playback.WasLaunched == 0 || playback.LaunchSequence != launch.LaunchSequence;
                    playback.FlightPhase = newLaunch ? 0f : math.saturate(playback.FlightPhase
                        + DeltaTime / math.max(0.01f, samples.Durations[EnemyAnimationSamples.FlyingMotion]));
                    playback.WasLaunched = 1;
                    playback.LaunchSequence = launch.LaunchSequence;
                    if (Velocities.HasComponent(owner))
                    {
                        float3 velocity = Velocities[owner].Linear;
                        if (math.lengthsq(velocity) > 0.0001f)
                            playback.FlightPitch = -math.atan2(velocity.y, math.length(velocity.xz));
                    }
                    float flyingFrame = playback.FlightPhase * (samples.FrameCount - 1);
                    int from = (int)flyingFrame;
                    int to = math.min(from + 1, samples.FrameCount - 1);
                    var pitch = new float3x3(quaternion.RotateX(playback.FlightPitch));
                    for (int bone = 0; bone < skin.Length; bone++)
                    {
                        float3x4 pose = Sample(ref samples, EnemyAnimationSamples.FlyingMotion,
                            from, to, bone, math.frac(flyingFrame));
                        skin[bone] = new SkinMatrix { Value = new float3x4(math.mul(pitch, pose.c0),
                            math.mul(pitch, pose.c1), math.mul(pitch, pose.c2), math.mul(pitch, pose.c3)) };
                    }
                    return;
                }
                bool endingLaunch = playback.WasLaunched != 0
                    || (launch.LaunchSequence != 0 && playback.LaunchSequence != launch.LaunchSequence);
                if (launch.Phase == EnemyLaunchPhase.Recovering || launch.Phase == EnemyLaunchPhase.Defeated)
                {
                    if (endingLaunch)
                    {
                        playback.Landing = 1;
                        playback.ImpactPhase = 0f;
                        float impactDuration = samples.Durations[EnemyAnimationSamples.ImpactMotion];
                        playback.ImpactDuration = launch.Phase == EnemyLaunchPhase.Recovering
                            ? math.min(impactDuration, math.max(0.01f, launch.RecoverySecondsRemaining)) : impactDuration;
                        playback.LaunchSequence = launch.LaunchSequence;
                    }
                    else if (playback.Landing != 0)
                        playback.ImpactPhase = math.saturate(playback.ImpactPhase + DeltaTime / math.max(0.01f, playback.ImpactDuration));
                    if (playback.Landing != 0)
                    {
                        float impactFrame = playback.ImpactPhase * (samples.FrameCount - 1);
                        int from = (int)impactFrame;
                        int to = math.min(from + 1, samples.FrameCount - 1);
                        for (int bone = 0; bone < skin.Length; bone++)
                            skin[bone] = new SkinMatrix { Value = Sample(ref samples, EnemyAnimationSamples.ImpactMotion,
                                from, to, bone, math.frac(impactFrame)) };
                    }
                }
                else playback.Landing = 0;
                playback.WasLaunched = 0;
                if (launch.Phase != EnemyLaunchPhase.Active) return;

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

            private void AnimateDasher(Entity owner, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples, EnemyLaunchState launch)
            {
                bool dashing = Dashers.HasComponent(owner) && Dashers[owner].Phase == DasherPhase.Dashing;
                if (launch.Phase == EnemyLaunchPhase.Launched || dashing)
                {
                    playback.Landing = 0;
                    playback.WasLaunched = launch.Phase == EnemyLaunchPhase.Launched ? (byte)1 : (byte)0;
                    ApplyFrame(ref skin, ref samples, 1, 0, 0, 0f);
                    return;
                }

                playback.WasLaunched = 0;
                if (launch.Phase == EnemyLaunchPhase.Defeated)
                {
                    if (playback.Landing == 0)
                    {
                        playback.Landing = 1;
                        playback.ImpactPhase = 0f;
                    }
                    else
                    {
                        playback.ImpactPhase = math.saturate(playback.ImpactPhase
                            + DeltaTime / math.max(0.01f, samples.Durations[EnemyAnimationSamples.ImpactMotion]));
                    }
                    float deathFrame = playback.ImpactPhase * (samples.FrameCount - 1);
                    int from = (int)deathFrame;
                    ApplyFrame(ref skin, ref samples, EnemyAnimationSamples.ImpactMotion, from,
                        math.min(from + 1, samples.FrameCount - 1), math.frac(deathFrame));
                    return;
                }

                playback.Landing = 0;
                if (launch.Phase != EnemyLaunchPhase.Active && launch.Phase != EnemyLaunchPhase.Recovering)
                    return;
                int motion = Movement[owner].Speed > 0.01f ? 1 : 0;
                playback.Phase = math.frac(playback.Phase
                    + DeltaTime / math.max(0.01f, samples.Durations[motion]));
                float frame = playback.Phase * samples.FrameCount;
                int a = (int)frame;
                ApplyFrame(ref skin, ref samples, motion, a, (a + 1) % samples.FrameCount, math.frac(frame));
            }

            private void AnimateExplosive(Entity owner, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples, EnemyLaunchState launch)
            {
                if (launch.Phase == EnemyLaunchPhase.Launched)
                {
                    playback.Landing = 0;
                    playback.WasLaunched = 1;
                    ApplyFrame(ref skin, ref samples, 1, 0, 0, 0f);
                    return;
                }

                playback.WasLaunched = 0;
                if (launch.Phase == EnemyLaunchPhase.Defeated)
                {
                    if (playback.Landing == 0)
                    {
                        playback.Landing = 1;
                        playback.ImpactPhase = 0f;
                    }
                    else
                    {
                        playback.ImpactPhase = math.saturate(playback.ImpactPhase
                            + DeltaTime / math.max(0.01f, samples.Durations[EnemyAnimationSamples.ImpactMotion]));
                    }
                    float deathFrame = playback.ImpactPhase * (samples.FrameCount - 1);
                    int from = (int)deathFrame;
                    ApplyFrame(ref skin, ref samples, EnemyAnimationSamples.ImpactMotion, from,
                        math.min(from + 1, samples.FrameCount - 1), math.frac(deathFrame));
                    return;
                }

                playback.Landing = 0;
                if (launch.Phase != EnemyLaunchPhase.Active && launch.Phase != EnemyLaunchPhase.Recovering)
                    return;
                int motion = Movement[owner].Speed > 0.01f ? 1 : 0;
                playback.Phase = math.frac(playback.Phase
                    + DeltaTime / math.max(0.01f, samples.Durations[motion]));
                float frame = playback.Phase * samples.FrameCount;
                int a = (int)frame;
                ApplyFrame(ref skin, ref samples, motion, a, (a + 1) % samples.FrameCount, math.frac(frame));
            }

            private void AnimateRanged(Entity owner, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples, EnemyLaunchState launch)
            {
                const int attackMotion = 2;
                if (launch.Phase == EnemyLaunchPhase.Launched)
                {
                    playback.Landing = 0;
                    playback.WasLaunched = 1;
                    playback.WasAttacking = 0;
                    ApplyRotatedFrame(ref skin, ref samples, EnemyAnimationSamples.FlyingMotion,
                        0, 0, 0f, quaternion.RotateX(-math.PI * 0.5f));
                    return;
                }

                playback.WasLaunched = 0;
                if (launch.Phase == EnemyLaunchPhase.Defeated)
                {
                    playback.WasAttacking = 0;
                    if (playback.Landing == 0)
                    {
                        playback.Landing = 1;
                        playback.ImpactPhase = 0f;
                    }
                    else
                    {
                        playback.ImpactPhase = math.saturate(playback.ImpactPhase
                            + DeltaTime / math.max(0.01f, samples.Durations[EnemyAnimationSamples.ImpactMotion]));
                    }
                    float deathFrame = playback.ImpactPhase * (samples.FrameCount - 1);
                    int from = (int)deathFrame;
                    ApplyFrame(ref skin, ref samples, EnemyAnimationSamples.ImpactMotion, from,
                        math.min(from + 1, samples.FrameCount - 1), math.frac(deathFrame));
                    return;
                }

                playback.Landing = 0;
                if (launch.Phase != EnemyLaunchPhase.Active && launch.Phase != EnemyLaunchPhase.Recovering)
                    return;
                bool attacking = RangedAttacks.HasComponent(owner)
                    && RangedAttacks[owner].Phase == RangedAttackPhase.WindUp;
                if (attacking)
                {
                    if (playback.WasAttacking == 0) playback.AttackPhase = 0f;
                    else playback.AttackPhase = math.saturate(playback.AttackPhase
                        + DeltaTime / math.max(0.01f, samples.Durations[attackMotion]));
                    playback.WasAttacking = 1;
                    float attackFrame = playback.AttackPhase * (samples.FrameCount - 1);
                    int from = (int)attackFrame;
                    ApplyFrame(ref skin, ref samples, attackMotion, from,
                        math.min(from + 1, samples.FrameCount - 1), math.frac(attackFrame));
                    return;
                }

                playback.WasAttacking = 0;
                int motion = Movement[owner].Speed > 0.01f ? 1 : 0;
                playback.Phase = math.frac(playback.Phase
                    + DeltaTime / math.max(0.01f, samples.Durations[motion]));
                float frame = playback.Phase * samples.FrameCount;
                int a = (int)frame;
                ApplyFrame(ref skin, ref samples, motion, a, (a + 1) % samples.FrameCount, math.frac(frame));
            }

            private static void ApplyFrame(ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples,
                int motion, int from, int to, float blend)
            {
                for (int bone = 0; bone < skin.Length; bone++)
                    skin[bone] = new SkinMatrix { Value = Sample(ref samples, motion, from, to, bone, blend) };
            }

            private static void ApplyRotatedFrame(ref DynamicBuffer<SkinMatrix> skin,
                ref EnemyAnimationSamples samples, int motion, int from, int to, float blend, quaternion rotation)
            {
                var matrix = new float3x3(rotation);
                for (int bone = 0; bone < skin.Length; bone++)
                {
                    float3x4 pose = Sample(ref samples, motion, from, to, bone, blend);
                    skin[bone] = new SkinMatrix { Value = new float3x4(math.mul(matrix, pose.c0),
                        math.mul(matrix, pose.c1), math.mul(matrix, pose.c2), math.mul(matrix, pose.c3)) };
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
