using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct EnemyAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new EnemyPosePlaybackJob
            {
                Dragons = SystemAPI.GetComponentLookup<DragonFlameState>(true),
                Wizards = SystemAPI.GetComponentLookup<WizardCastState>(true),
                WizardTuning = SystemAPI.GetComponentLookup<WizardSettings>(true),
                Armors = SystemAPI.GetComponentLookup<EnemyArmor>(true),
                Movement = SystemAPI.GetComponentLookup<DesiredMovement>(true),
                Settings = SystemAPI.GetComponentLookup<EnemyMovementSettings>(true),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                Dashers = SystemAPI.GetComponentLookup<DasherState>(true),
                DasherTuning = SystemAPI.GetComponentLookup<DasherSettings>(true),
                Ninjas = SystemAPI.GetComponentLookup<NinjaState>(true),
                NinjaTuning = SystemAPI.GetComponentLookup<NinjaTuning>(true),
                RangedAttacks = SystemAPI.GetComponentLookup<RangedAttackState>(true),
                RangedTuning = SystemAPI.GetComponentLookup<RangedEnemySettings>(true),
                ElitePunches = SystemAPI.GetComponentLookup<ElitePunchState>(true),
                Health = SystemAPI.GetComponentLookup<Health>(true),
                Exploders = SystemAPI.GetComponentLookup<ExplosiveEnemyState>(true),
                Contacts = SystemAPI.GetComponentLookup<EnemyContactAttemptState>(true),
                SpawnAnimations = SystemAPI.GetComponentLookup<EnemySpawnAnimation>(true),
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new WizardBoundsJob
            {
                Animations = SystemAPI.GetComponentLookup<EnemyAnimation>(true),
                Tuning = SystemAPI.GetComponentLookup<WizardSettings>(true)
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct WizardBoundsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyAnimation> Animations;
            [ReadOnly] public ComponentLookup<WizardSettings> Tuning;
            private void Execute(in EnemyAnimationBounds original, ref RenderBounds bounds)
            {
                if (!Animations.HasComponent(original.Animation)) return;
                var animation = Animations[original.Animation];
                float clearance = Tuning.HasComponent(animation.Owner)
                    ? math.max(0f, Tuning[animation.Owner].AnimationGroundClearance) * animation.WorldToAnimationHeight : 0f;
                bounds.Value = original.Original;
                // Cover both unshifted emergence and the configurable visual lift.
                bounds.Value.Center.y += clearance * .5f;
                bounds.Value.Extents.y += clearance * .5f;
            }
        }

        [BurstCompile]
        private partial struct EnemyPosePlaybackJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<DragonFlameState> Dragons;
            [ReadOnly] public ComponentLookup<WizardCastState> Wizards;
            [ReadOnly] public ComponentLookup<WizardSettings> WizardTuning;
            [ReadOnly] public ComponentLookup<EnemyArmor> Armors;
            [ReadOnly] public ComponentLookup<DesiredMovement> Movement;
            [ReadOnly] public ComponentLookup<EnemyMovementSettings> Settings;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<RespawnRequest> Respawns;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<DasherState> Dashers;
            [ReadOnly] public ComponentLookup<DasherSettings> DasherTuning;
            [ReadOnly] public ComponentLookup<NinjaState> Ninjas;
            [ReadOnly] public ComponentLookup<NinjaTuning> NinjaTuning;
            [ReadOnly] public ComponentLookup<RangedAttackState> RangedAttacks;
            [ReadOnly] public ComponentLookup<RangedEnemySettings> RangedTuning;
            [ReadOnly] public ComponentLookup<ElitePunchState> ElitePunches;
            [ReadOnly] public ComponentLookup<Health> Health;
            [ReadOnly] public ComponentLookup<ExplosiveEnemyState> Exploders;
            [ReadOnly] public ComponentLookup<EnemyContactAttemptState> Contacts;
            [ReadOnly] public ComponentLookup<EnemySpawnAnimation> SpawnAnimations;
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
                if (animation.SpawnMotion >= 0 && animation.SpawnMotion < EnemyAnimationSamples.MotionCount
                    && SpawnAnimations.HasComponent(owner) && SpawnAnimations.IsComponentEnabled(owner)
                    && launch.Phase == EnemyLaunchPhase.Recovering)
                {
                    float progress = math.saturate(1f - launch.RecoverySecondsRemaining
                        / math.max(.001f, SpawnAnimations[owner].Duration));
                    float spawnFrame = progress * (samples.FrameCount - 1);
                    int from = (int)spawnFrame;
                    if (animation.Profile == (byte)EnemyAnimationProfile.Dasher
                        || animation.Profile == (byte)EnemyAnimationProfile.GoblinArmored || ElitePunches.HasComponent(owner))
                        ApplyDasherFrame(ref skin, ref samples, animation.SpawnMotion, from,
                            math.min(from + 1, samples.FrameCount - 1), math.frac(spawnFrame));
                    else
                        ApplyFrame(ref skin, ref samples, animation.SpawnMotion, from,
                            math.min(from + 1, samples.FrameCount - 1), math.frac(spawnFrame));
                    if (animation.Profile == (byte)EnemyAnimationProfile.Wizard)
                    {
                        float lift = WizardTuning.HasComponent(owner) ? math.max(0f, WizardTuning[owner].AnimationGroundClearance)
                            * animation.WorldToAnimationHeight * math.smoothstep(0f, 1f, progress) : 0f;
                        for (int bone = 0; bone < skin.Length; bone++)
                        {
                            var pose = skin[bone].Value;
                            pose.c3.y += lift;
                            skin[bone] = new SkinMatrix { Value = pose };
                        }
                        playback.Phase = 0f;
                    }
                    playback.WasRecovering = (byte)(animation.Profile == (byte)EnemyAnimationProfile.Wizard ? 2 : 0);
                    playback.RecoveryAge = 0;
                    playback.WasAttacking = 0;
                    playback.WasLaunched = 0;
                    playback.Landing = 0;
                    playback.HitActive = 0;
                    playback.Movement = 0;
                    playback.JogPhase = 0;
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Goblin
                    || animation.Profile == (byte)EnemyAnimationProfile.GoblinArmored)
                {
                    AnimateGoblin(owner, ref playback, ref skin, ref samples, launch, animation.LaunchPivot);
                    bool armored = animation.Profile == (byte)EnemyAnimationProfile.GoblinArmored;
                    bool charging = !armored && !ElitePunches.HasComponent(owner)
                        && Contacts.HasComponent(owner) && Contacts[owner].IsAttempting != 0;
                    if (launch.Phase == EnemyLaunchPhase.Active && !charging && animation.JogSamples.IsCreated)
                        AnimateJogLegs(owner, animation.BlendResponse, ref playback, ref skin, ref animation.JogSamples.Value,
                            armored || ElitePunches.HasComponent(owner), animation.MaxLegAnimationSpeed);
                    else
                    {
                        playback.Movement = 0;
                        playback.JogPhase = 0;
                    }
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Explosive)
                {
                    AnimateExplosive(owner, ref playback, ref skin, ref samples, launch);
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Dasher)
                {
                    AnimateGoblinDasher(owner, ref playback, ref skin, ref samples, launch);
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Wizard)
                {
                    AnimateWizard(owner, ref playback, ref skin, ref samples, launch,
                        WizardTuning.HasComponent(owner) ? math.max(0f, WizardTuning[owner].AnimationGroundClearance)
                            * animation.WorldToAnimationHeight : 0f);
                    return;
                }
                if (animation.Profile != (byte)EnemyAnimationProfile.Standard
                    && animation.Profile != (byte)EnemyAnimationProfile.Elite
                    && launch.Phase == EnemyLaunchPhase.Recovering)
                {
                    AnimateCreatureRecovery(ref playback, ref skin, ref samples, launch,
                        animation.Profile != (byte)EnemyAnimationProfile.Dasher
                        && animation.Profile != (byte)EnemyAnimationProfile.Dragon
                        && animation.Profile != (byte)EnemyAnimationProfile.Explosive, animation.LaunchPivot);
                    return;
                }
                playback.WasRecovering = 0;
                if (animation.Profile == (byte)EnemyAnimationProfile.Dragon)
                {
                    if ((launch.Phase == EnemyLaunchPhase.Active || launch.Phase == EnemyLaunchPhase.Launched) && Dragons.HasComponent(owner))
                    {
                        var flame = Dragons[owner];
                        int motion = flame.Motion;
                        float dragonFrame = flame.AnimationPhase * (motion == 2 ? samples.FrameCount - 1 : samples.FrameCount);
                        int from = (int)dragonFrame;
                        ApplyFrame(ref skin, ref samples, motion, from,
                            motion == 2 ? math.min(from + 1, samples.FrameCount - 1) : (from + 1) % samples.FrameCount, math.frac(dragonFrame));
                    }
                    else AnimateDasher(owner, ref playback, ref skin, ref samples, launch);
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Ninja)
                {
                    if (launch.Phase == EnemyLaunchPhase.Active && Ninjas.HasComponent(owner) && NinjaTuning.HasComponent(owner))
                    {
                        var ninja = Ninjas[owner];
                        int motion = ninja.Phase == NinjaPhase.Preparing ? 2
                            : ninja.Phase == NinjaPhase.ArrivalWarning || ninja.Phase == NinjaPhase.Dashing ? 3 : -1;
                        if (motion >= 0)
                        {
                            float progress = ninja.Phase == NinjaPhase.Preparing
                                ? math.saturate(1 - ninja.SecondsRemaining / math.max(.001f, NinjaTuning[owner].PreparationDuration)) : .35f;
                            float pose = progress * (samples.FrameCount - 1);
                            int from = (int)pose;
                            ApplyFrame(ref skin, ref samples, motion, from, math.min(from + 1, samples.FrameCount - 1), math.frac(pose));
                            return;
                        }
                    }
                    AnimateBaseline(owner, ref playback, ref skin, ref samples, launch, animation.LaunchPivot);
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Ranged)
                {
                    AnimateRanged(owner, ref playback, ref skin, ref samples, launch);
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Armored)
                {
                    if (launch.Phase == EnemyLaunchPhase.Launched)
                    {
                        playback.HitActive = 0;
                        AnimateBaseline(owner, ref playback, ref skin, ref samples, launch, animation.LaunchPivot);
                    }
                    else
                    {
                        if (Armors.HasComponent(owner) && playback.ArmorHitSequence != Armors[owner].HitSequence)
                        {
                            playback.ArmorHitSequence = Armors[owner].HitSequence;
                            playback.HitActive = 1;
                            playback.HitPhase = 0;
                        }
                        AnimateElite(owner, ref playback, ref skin, ref samples, launch);
                    }
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Elite)
                {
                    AnimateElite(owner, ref playback, ref skin, ref samples, launch);
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Baseline)
                {
                    AnimateBaseline(owner, ref playback, ref skin, ref samples, launch, animation.LaunchPivot);
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

            // ENEMY-005..008 / INFO-004: animation follows gameplay clocks and never writes
            // owner transforms, scale, material colour or physics. Roll Forward In Place plays once per
            // charge/dash; a launched Dasher loops the same authored motion language.
            private void AnimateGoblinDasher(Entity owner, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples, EnemyLaunchState launch)
            {
                const int prepareMotion = 3, dashMotion = 4, riseMotion = 6, dashRecoveryMotion = 8;
                var dash = Dashers.HasComponent(owner) ? Dashers[owner] : default;
                var tuning = DasherTuning.HasComponent(owner) ? DasherTuning[owner] : default;
                int motion;
                float progress = -1f;
                bool restart = false;
                bool hit = false;
                if (Health.HasComponent(owner))
                {
                    float health = Health[owner].Current;
                    hit = playback.HealthInitialized != 0 && health < playback.PreviousHealth;
                    playback.HealthInitialized = 1;
                    playback.PreviousHealth = health;
                }
                if (launch.Phase == EnemyLaunchPhase.Launched)
                {
                    motion = EnemyAnimationSamples.FlyingMotion;
                    restart = playback.WasLaunched == 0 || playback.LaunchSequence != launch.LaunchSequence;
                    playback.WasLaunched = 1;
                    playback.LaunchSequence = launch.LaunchSequence;
                    playback.HitActive = 0;
                }
                else if (launch.Phase == EnemyLaunchPhase.Defeated) motion = EnemyAnimationSamples.ImpactMotion;
                else if (launch.Phase == EnemyLaunchPhase.Recovering)
                {
                    if (playback.WasRecovering == 0)
                        playback.ImpactDuration = math.max(.001f, launch.RecoverySecondsRemaining);
                    float recovery = math.saturate(1 - launch.RecoverySecondsRemaining / playback.ImpactDuration);
                    motion = recovery < .45f ? EnemyAnimationSamples.ImpactMotion : riseMotion;
                    progress = recovery < .45f ? recovery / .45f : (recovery - .45f) / .55f;
                }
                else if (dash.Phase == DasherPhase.Preparing)
                {
                    motion = prepareMotion;
                    progress = math.saturate(1 - dash.SecondsRemaining / math.max(.001f, tuning.TelegraphDuration));
                }
                else if (dash.Phase == DasherPhase.Dashing)
                {
                    motion = dashMotion;
                    progress = math.saturate(dash.DashDistanceTravelled / math.max(.001f, tuning.MaximumDistance));
                }
                else if (dash.Phase == DasherPhase.Recovering)
                {
                    motion = dashRecoveryMotion;
                    progress = math.saturate(1 - dash.SecondsRemaining / math.max(.001f, tuning.RecoveryDuration));
                }
                else
                {
                    if (hit) { playback.HitActive = 1; playback.HitPhase = 0; }
                    else if (playback.HitActive != 0)
                    {
                        playback.HitPhase += DeltaTime / math.max(.001f, samples.Durations[5]);
                        if (playback.HitPhase >= 1) playback.HitActive = 0;
                    }
                    motion = playback.HitActive != 0 ? 5 : Movement[owner].Speed <= .01f ? 0
                        : Movement[owner].Speed > Settings[owner].MoveSpeed * .65f ? 2 : 1;
                    if (motion == 5) progress = playback.HitPhase;
                }
                playback.WasRecovering = (byte)(launch.Phase == EnemyLaunchPhase.Recovering ? 1 : 0);
                if (launch.Phase != EnemyLaunchPhase.Launched) playback.WasLaunched = 0;
                bool loop = motion == 0 || motion == 1 || motion == 2 || motion == EnemyAnimationSamples.FlyingMotion;
                if (restart || motion != playback.GoblinMotion) playback.Phase = 0;
                else playback.Phase += DeltaTime / math.max(.001f, samples.Durations[motion]);
                playback.Phase = loop ? math.frac(playback.Phase) : math.saturate(playback.Phase);
                playback.GoblinMotion = (byte)motion;
                if (progress < 0) progress = playback.Phase;
                float frame = progress * (loop ? samples.FrameCount : samples.FrameCount - 1);
                int from = math.min((int)frame, samples.FrameCount - 1);
                ApplyDasherFrame(ref skin, ref samples, motion, from,
                    loop ? (from + 1) % samples.FrameCount : math.min(from + 1, samples.FrameCount - 1), math.frac(frame));
            }

            private void AnimateDasher(Entity owner, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples, EnemyLaunchState launch)
            {
                bool dashing = launch.Phase == EnemyLaunchPhase.Active
                    && Dashers.HasComponent(owner) && Dashers[owner].Phase == DasherPhase.Dashing;
                if (launch.Phase == EnemyLaunchPhase.Launched || dashing)
                {
                    if (dashing)
                    {
                        playback.Landing = 0;
                        playback.WasLaunched = 0;
                        playback.Phase = math.frac(playback.Phase + DeltaTime * 2f / math.max(.01f, samples.Durations[1]));
                        float wingFrame = playback.Phase * samples.FrameCount;
                        int wing = (int)wingFrame;
                        ApplyFrame(ref skin, ref samples, 1, wing, (wing + 1) % samples.FrameCount, math.frac(wingFrame));
                    }
                    else AnimateCreatureFlight(owner, ref playback, ref skin, ref samples, launch, 1, false);
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
                bool damaged = false;
                if (Health.HasComponent(owner))
                {
                    float health = Health[owner].Current;
                    damaged = playback.HealthInitialized != 0 && health < playback.PreviousHealth;
                    playback.PreviousHealth = health;
                    playback.HealthInitialized = 1;
                }
                if (launch.Phase == EnemyLaunchPhase.Launched)
                {
                    playback.HitActive = 0;
                    playback.WasAttacking = 0;
                    playback.WasRecovering = 0;
                    playback.Landing = 0;
                    bool newLaunch = playback.WasLaunched == 0 || playback.LaunchSequence != launch.LaunchSequence;
                    playback.FlightPhase = newLaunch ? 0 : math.saturate(playback.FlightPhase
                        + DeltaTime / math.max(.01f, samples.Durations[EnemyAnimationSamples.FlyingMotion]));
                    playback.WasLaunched = 1;
                    playback.LaunchSequence = launch.LaunchSequence;
                    float flightFrame = playback.FlightPhase * (samples.FrameCount - 1);
                    int from = (int)flightFrame;
                    ApplyFrame(ref skin, ref samples, EnemyAnimationSamples.FlyingMotion, from,
                        math.min(from + 1, samples.FrameCount - 1), math.frac(flightFrame));
                    return;
                }

                playback.WasLaunched = 0;
                if (launch.Phase == EnemyLaunchPhase.Defeated)
                {
                    // ENEMY-013: the gameplay blast has already resolved. Only play its visual here.
                    int deathMotion = Exploders.HasComponent(owner) && Exploders[owner].HasExploded != 0
                        ? EnemyAnimationSamples.ImpactMotion : 8;
                    playback.HitActive = 0;
                    playback.WasAttacking = 0;
                    if (playback.Landing == 0)
                    {
                        playback.Landing = 1;
                        playback.ImpactPhase = 0f;
                    }
                    else
                    {
                        playback.ImpactPhase = math.saturate(playback.ImpactPhase
                            + DeltaTime / math.max(0.01f, samples.Durations[deathMotion]));
                    }
                    float deathFrame = playback.ImpactPhase * (samples.FrameCount - 1);
                    int from = (int)deathFrame;
                    ApplyFrame(ref skin, ref samples, deathMotion, from,
                        math.min(from + 1, samples.FrameCount - 1), math.frac(deathFrame));
                    return;
                }

                playback.Landing = 0;
                if (launch.Phase == EnemyLaunchPhase.Recovering)
                {
                    if (playback.WasRecovering == 0)
                        playback.ImpactDuration = math.max(.01f, launch.RecoverySecondsRemaining);
                    playback.WasRecovering = 1;
                    playback.HitActive = 0;
                    playback.WasAttacking = 0;
                    float recoveryFrame = math.saturate(1 - launch.RecoverySecondsRemaining / playback.ImpactDuration)
                        * (samples.FrameCount - 1);
                    int from = (int)recoveryFrame;
                    ApplyFrame(ref skin, ref samples, 6, from, math.min(from + 1, samples.FrameCount - 1), math.frac(recoveryFrame));
                    return;
                }
                playback.WasRecovering = 0;
                if (launch.Phase != EnemyLaunchPhase.Active) return;
                if (damaged) { playback.HitActive = 1; playback.HitPhase = 0; }
                if (playback.HitActive != 0)
                {
                    float hitFrame = playback.HitPhase * (samples.FrameCount - 1);
                    int from = (int)hitFrame;
                    ApplyFrame(ref skin, ref samples, 5, from, math.min(from + 1, samples.FrameCount - 1), math.frac(hitFrame));
                    playback.HitPhase = math.saturate(playback.HitPhase + DeltaTime / math.max(.01f, samples.Durations[5]));
                    if (playback.HitPhase >= 1) playback.HitActive = 0;
                    return;
                }
                bool pursuing = Contacts.HasComponent(owner)
                    && (Contacts[owner].IsAttempting != 0 || Contacts[owner].IsWindingUp != 0);
                int motion = pursuing ? 2 : Movement[owner].Speed > .01f ? 1 : 0;
                if (pursuing != (playback.WasAttacking != 0)) playback.Phase = 0;
                playback.WasAttacking = (byte)(pursuing ? 1 : 0);
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
                    playback.WasAttacking = 0;
                    AnimateCreatureFlight(owner, ref playback, ref skin, ref samples, launch, EnemyAnimationSamples.FlyingMotion, true);
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

            private void AnimateElite(Entity owner, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples, EnemyLaunchState launch)
            {
                const int punchMotion = 2;
                const int hitMotion = 3;
                bool damaged = false;
                if (Health.HasComponent(owner))
                {
                    float currentHealth = Health[owner].Current;
                    damaged = playback.HealthInitialized != 0 && currentHealth < playback.PreviousHealth;
                    playback.PreviousHealth = currentHealth;
                    playback.HealthInitialized = 1;
                }

                if (launch.Phase == EnemyLaunchPhase.Defeated)
                {
                    playback.HitActive = 0;
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
                if (damaged)
                {
                    playback.HitActive = 1;
                    playback.HitPhase = 0f;
                    playback.WasAttacking = 0;
                }
                if (playback.HitActive != 0)
                {
                    float hitFrame = playback.HitPhase * (samples.FrameCount - 1);
                    int from = (int)hitFrame;
                    ApplyFrame(ref skin, ref samples, hitMotion, from,
                        math.min(from + 1, samples.FrameCount - 1), math.frac(hitFrame));
                    playback.HitPhase = math.saturate(playback.HitPhase
                        + DeltaTime / math.max(0.01f, samples.Durations[hitMotion]));
                    if (playback.HitPhase >= 1f) playback.HitActive = 0;
                    return;
                }

                bool attacking = ElitePunches.HasComponent(owner)
                    && ElitePunches[owner].Phase == ElitePunchPhase.WindUp;
                if (attacking)
                {
                    if (playback.WasAttacking == 0) playback.AttackPhase = 0f;
                    else playback.AttackPhase = math.saturate(playback.AttackPhase
                        + DeltaTime / math.max(0.01f, samples.Durations[punchMotion]));
                    playback.WasAttacking = 1;
                    float punchFrame = playback.AttackPhase * (samples.FrameCount - 1);
                    int from = (int)punchFrame;
                    ApplyFrame(ref skin, ref samples, punchMotion, from,
                        math.min(from + 1, samples.FrameCount - 1), math.frac(punchFrame));
                    return;
                }

                playback.WasAttacking = 0;
                if (launch.Phase != EnemyLaunchPhase.Active && launch.Phase != EnemyLaunchPhase.Recovering)
                    return;
                int motion = Movement[owner].Speed > 0.01f ? 1 : 0;
                playback.Phase = math.frac(playback.Phase
                    + DeltaTime / math.max(0.01f, samples.Durations[motion]));
                float frame = playback.Phase * samples.FrameCount;
                int a = (int)frame;
                ApplyFrame(ref skin, ref samples, motion, a, (a + 1) % samples.FrameCount, math.frac(frame));
            }

            // Goblin slots: idle, walk, run, anticipation, strike, hit, rise, spawn, idle, flight, fall.
            // All motion comes from authored poses; the physics owner keeps its transform and velocity.
            private void AnimateGoblin(Entity owner, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples, EnemyLaunchState launch, float3 pivot)
            {
                var ranged = RangedAttacks.HasComponent(owner) ? RangedAttacks[owner] : default;
                float release = RangedTuning.HasComponent(owner)
                    ? math.saturate(RangedTuning[owner].ProjectileLaunchProgress) : 0f;
                bool fired = ranged.ProjectilesSpawned != playback.ProjectileSequence;
                playback.ProjectileSequence = ranged.ProjectilesSpawned;
                if (launch.Phase != EnemyLaunchPhase.Active) playback.WasAttacking = 0;
                else if (fired)
                {
                    // Continue the full Cut Attack at the configured release pose only after a real shot.
                    playback.WasAttacking = 1;
                    playback.AttackPhase = release;
                }
                else if (playback.WasAttacking != 0)
                {
                    playback.AttackPhase += DeltaTime / math.max(.01f, samples.Durations[4]);
                    if (playback.AttackPhase >= 1f) playback.WasAttacking = 0;
                }
                bool hit = false;
                if (Health.HasComponent(owner))
                {
                    float health = Health[owner].Current;
                    hit = playback.HealthInitialized != 0 && health < playback.PreviousHealth;
                    playback.HealthInitialized = 1;
                    playback.PreviousHealth = health;
                }
                bool armored = Armors.HasComponent(owner);
                bool shielded = armored && Armors[owner].Stages > 0;
                if (armored && playback.ArmorHitSequence != Armors[owner].HitSequence)
                {
                    playback.ArmorHitSequence = Armors[owner].HitSequence;
                    hit = true;
                }
                int motion;
                float progress = -1f;
                if (launch.Phase == EnemyLaunchPhase.Launched)
                {
                    playback.WasLaunched = 1;
                    playback.LaunchSequence = launch.LaunchSequence;
                    playback.HitActive = 0;
                    playback.WasRecovering = 0;
                    playback.Landing = 0;
                    playback.Phase = 0;
                    playback.FlightPhase = 0;
                    playback.GoblinMotion = EnemyAnimationSamples.FlyingMotion;
                    // Slot 9 contains the exact Jump In Place frame 6. Tip the model top-first;
                    // owner yaw continues to follow physics velocity without changing the collider.
                    ApplyRotatedFrame(ref skin, ref samples, EnemyAnimationSamples.FlyingMotion,
                        0, 0, 0, quaternion.RotateX(math.PI * .5f), pivot);
                    return;
                }
                else
                {
                    playback.WasLaunched = 0;
                    if (launch.Phase == EnemyLaunchPhase.Defeated) motion = EnemyAnimationSamples.ImpactMotion;
                    else if (launch.Phase == EnemyLaunchPhase.Recovering)
                    {
                        if (playback.WasRecovering == 0)
                            playback.ImpactDuration = math.max(.01f, launch.RecoverySecondsRemaining);
                        float recovery = math.saturate(1f - launch.RecoverySecondsRemaining / playback.ImpactDuration);
                        motion = recovery < .45f ? EnemyAnimationSamples.ImpactMotion : 6;
                        progress = recovery < .45f ? recovery / .45f : (recovery - .45f) / .55f;
                    }
                    else
                    {
                        if (hit) { playback.HitActive = 1; playback.HitPhase = 0; }
                        else if (playback.HitActive != 0)
                        {
                            playback.HitPhase += DeltaTime / math.max(.01f, samples.Durations[5]);
                            if (playback.HitPhase >= 1f) playback.HitActive = 0;
                        }
                        var contact = Contacts.HasComponent(owner) ? Contacts[owner] : default;
                        if (shielded)
                        {
                            motion = 8; // Block owns the upper body until the shield is removed.
                            if (hit) playback.Phase = 0;
                        }
                        else if (ElitePunches.HasComponent(owner) && ElitePunches[owner].Phase == ElitePunchPhase.CommittedSweep)
                        {
                            motion = 4;
                            progress = ElitePunches[owner].SweepProgress;
                            playback.HitActive = 0;
                        }
                        else if (RangedAttacks.HasComponent(owner) && RangedTuning.HasComponent(owner)
                            && ranged.Phase == RangedAttackPhase.WindUp)
                        {
                            motion = 3;
                            progress = release * math.saturate(1f - ranged.SecondsRemaining
                                / math.max(.01f, RangedTuning[owner].WindUpDuration));
                            playback.WasAttacking = 0;
                        }
                        else if (playback.WasAttacking != 0)
                        {
                            motion = 4;
                            progress = playback.AttackPhase;
                        }
                        else if (contact.IsAttempting != 0 && !armored && !ElitePunches.HasComponent(owner))
                        {
                            motion = 2;
                        }
                        else if (playback.HitActive != 0) { motion = 5; progress = playback.HitPhase; }
                        else
                        {
                            float speed = (armored || ElitePunches.HasComponent(owner)) && Velocities.HasComponent(owner)
                                ? math.length(Velocities[owner].Linear.xz) : Movement[owner].Speed;
                            float idleThreshold = armored || ElitePunches.HasComponent(owner) ? .03f : .01f;
                            motion = speed <= idleThreshold ? 0 : speed > Settings[owner].MoveSpeed * .65f ? 2 : 1;
                        }
                    }
                }
                playback.WasRecovering = (byte)(launch.Phase == EnemyLaunchPhase.Recovering ? 1 : 0);
                bool oneShot = motion == EnemyAnimationSamples.ImpactMotion;
                if (motion != playback.GoblinMotion) playback.Phase = 0;
                else playback.Phase += DeltaTime / math.max(.01f, samples.Durations[motion]);
                playback.Phase = oneShot ? math.saturate(playback.Phase) : math.frac(playback.Phase);
                playback.GoblinMotion = (byte)motion;
                if (progress < 0) progress = oneShot ? math.saturate(playback.Phase) : math.frac(playback.Phase);
                float frame = progress * (oneShot || motion == 3 || motion == 4 || motion == 5 || motion == 6
                    ? samples.FrameCount - 1 : samples.FrameCount);
                int from = math.min((int)frame, samples.FrameCount - 1);
                int to = motion == 0 || motion == 1 || motion == 2 || (armored && motion == 8) || motion == EnemyAnimationSamples.FlyingMotion
                    ? (from + 1) % samples.FrameCount : math.min(from + 1, samples.FrameCount - 1);
                if (armored || ElitePunches.HasComponent(owner)) ApplyDasherFrame(ref skin, ref samples, motion, from, to, math.frac(frame));
                else ApplyFrame(ref skin, ref samples, motion, from, to, math.frac(frame));
            }

            private void AnimateJogLegs(Entity owner, float blendResponse, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyJogSamples samples, bool resolvedMotion, float maxPlaybackSpeed)
            {
                if (samples.BoneCount != skin.Length) return;
                var intent = Movement[owner];
                float3 velocity = resolvedMotion && Velocities.HasComponent(owner)
                    ? new float3(Velocities[owner].Linear.x, 0, Velocities[owner].Linear.z)
                    : math.normalizesafe(intent.Direction) * math.max(0, intent.Speed);
                float3 local = math.rotate(math.inverse(Transforms[owner].Rotation), velocity);
                float speed = math.length(local.xz);
                if (resolvedMotion)
                {
                    // INFO-004 / ENEMY-009: setup intent can stop or reverse before physics
                    // does. Use resolved motion immediately; vector smoothing can cancel
                    // opposite directions and leave a translating Giant with idle legs.
                    playback.Movement = speed > .03f ? local.xz / speed : float2.zero;
                }
                else
                {
                    float2 target = local.xz / math.max(.01f, Settings[owner].MoveSpeed);
                    target /= math.max(1, math.length(target));
                    float blend = blendResponse <= 0 ? 1 : 1 - math.exp(-blendResponse * DeltaTime);
                    playback.Movement = math.lerp(playback.Movement, target, blend);
                }
                float amount = math.saturate(math.length(playback.Movement));
                if (amount < .0001f) return;
                float sector = math.frac(math.atan2(playback.Movement.x, playback.Movement.y) / (2 * math.PI)) * 8;
                int first = (int)math.floor(sector) % EnemyJogSamples.DirectionCount;
                int second = (first + 1) % EnemyJogSamples.DirectionCount;
                float turn = math.frac(sector);
                float duration = math.lerp(samples.Durations[first], samples.Durations[second], turn);
                float cycleDistance = samples.CycleDistances.Length == EnemyJogSamples.DirectionCount
                    ? math.max(.01f, math.lerp(samples.CycleDistances[first], samples.CycleDistances[second], turn)) : 2f;
                float rate = resolvedMotion ? speed / cycleDistance : 1f / math.max(.01f, duration);
                if (maxPlaybackSpeed > 0f) rate = math.min(rate, maxPlaybackSpeed / math.max(.01f, duration));
                float advance = rate * DeltaTime;
                playback.JogPhase = math.frac(playback.JogPhase + advance);
                float frame = playback.JogPhase * samples.FrameCount;
                int a = (int)frame, b = (a + 1) % samples.FrameCount;
                float between = math.frac(frame);
                var originalHip = skin[samples.HipBoneIndex].Value;
                var hip = math.mul(new float4x4(new float4(originalHip.c0, 0), new float4(originalHip.c1, 0),
                    new float4(originalHip.c2, 0), new float4(originalHip.c3, 1)), samples.HipBindInverse);
                int count = samples.LegBones.Length;
                for (int leg = 0; leg < count; leg++)
                {
                    var left = BlendPose(samples.Matrices[(first * samples.FrameCount + a) * count + leg],
                        samples.Matrices[(first * samples.FrameCount + b) * count + leg], between, resolvedMotion);
                    var right = BlendPose(samples.Matrices[(second * samples.FrameCount + a) * count + leg],
                        samples.Matrices[(second * samples.FrameCount + b) * count + leg], between, resolvedMotion);
                    var relative = BlendPose(left, right, turn, resolvedMotion);
                    var pose = math.mul(hip, new float4x4(new float4(relative.c0, 0), new float4(relative.c1, 0),
                        new float4(relative.c2, 0), new float4(relative.c3, 1)));
                    int bone = samples.LegBones[leg];
                    skin[bone] = new SkinMatrix { Value = BlendPose(skin[bone].Value,
                        new float3x4(pose.c0.xyz, pose.c1.xyz, pose.c2.xyz, pose.c3.xyz), amount, resolvedMotion) };
                }
            }

            private static float3x4 BlendPose(float3x4 a, float3x4 b, float blend, bool preserveScale)
            {
                if (!preserveScale) return a * (1f - blend) + b * blend;
                float3 scaleA = math.max(new float3(.000001f), new float3(math.length(a.c0), math.length(a.c1), math.length(a.c2)));
                float3 scaleB = math.max(new float3(.000001f), new float3(math.length(b.c0), math.length(b.c1), math.length(b.c2)));
                // Mirrored equipment has a negative determinant in its authored bind pose.
                // Keep that reflection in scale before extracting a proper rotation.
                if (math.determinant(new float3x3(a.c0, a.c1, a.c2)) < 0) scaleA.x = -scaleA.x;
                if (math.determinant(new float3x3(b.c0, b.c1, b.c2)) < 0) scaleB.x = -scaleB.x;
                var rotationA = math.normalize(new quaternion(new float3x3(a.c0 / scaleA.x, a.c1 / scaleA.y, a.c2 / scaleA.z)));
                var rotationB = math.normalize(new quaternion(new float3x3(b.c0 / scaleB.x, b.c1 / scaleB.y, b.c2 / scaleB.z)));
                var rotation = new float3x3(math.slerp(rotationA, rotationB, blend));
                float3 scale = math.lerp(scaleA, scaleB, blend);
                return new float3x4(rotation.c0 * scale.x, rotation.c1 * scale.y, rotation.c2 * scale.z, math.lerp(a.c3, b.c3, blend));
            }

            // Bud clips provide all body feedback. Never rotate, squash or recolor the model for readability.
            private void AnimateWizard(Entity owner, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples, EnemyLaunchState launch, float clearance)
            {
                bool damaged = false;
                if (Health.HasComponent(owner))
                {
                    float health = Health[owner].Current;
                    damaged = playback.HealthInitialized != 0 && health < playback.PreviousHealth;
                    playback.HealthInitialized = 1;
                    playback.PreviousHealth = health;
                }
                bool leavingSpawn = launch.Phase == EnemyLaunchPhase.Active && playback.WasRecovering == 2;
                int motion;
                float progress = -1f;
                if (launch.Phase == EnemyLaunchPhase.Launched)
                {
                    playback.WasRecovering = 0;
                    playback.WasAttacking = 0;
                    playback.HitActive = 0;
                    bool entering = playback.WasLaunched == 0 || playback.LaunchSequence != launch.LaunchSequence;
                    playback.FlightPhase = entering ? 0f : math.frac(playback.FlightPhase
                        + DeltaTime / math.max(.01f, samples.Durations[EnemyAnimationSamples.FlyingMotion]));
                    playback.WasLaunched = 1;
                    playback.LaunchSequence = launch.LaunchSequence;
                    motion = EnemyAnimationSamples.FlyingMotion;
                    progress = playback.FlightPhase;
                }
                else if (launch.Phase == EnemyLaunchPhase.Defeated)
                {
                    if (playback.Landing == 0) playback.ImpactPhase = 0f;
                    else playback.ImpactPhase = math.saturate(playback.ImpactPhase
                        + DeltaTime / math.max(.01f, samples.Durations[EnemyAnimationSamples.ImpactMotion]));
                    playback.Landing = 1;
                    playback.WasAttacking = 0;
                    playback.HitActive = 0;
                    motion = EnemyAnimationSamples.ImpactMotion;
                    progress = playback.ImpactPhase;
                }
                else if (launch.Phase == EnemyLaunchPhase.Recovering)
                {
                    // Takeoff is authored from ground idle, so hold that pose until recovery ends.
                    playback.WasRecovering = 1;
                    playback.RecoveryAge = 0;
                    playback.WasLaunched = 0;
                    playback.WasAttacking = 0;
                    playback.HitActive = 0;
                    motion = 4;
                }
                else
                {
                    playback.Landing = 0;
                    playback.WasLaunched = 0;
                    bool casting = Wizards.HasComponent(owner) && Wizards[owner].IsCasting;
                    playback.AttackPhase = !casting || playback.WasAttacking == 0 ? 0f
                        : math.frac(playback.AttackPhase + DeltaTime / math.max(.01f, samples.Durations[2]));
                    playback.WasAttacking = (byte)(casting ? 1 : 0);
                    if (damaged) { playback.HitActive = 1; playback.HitPhase = 0; }
                    if (casting)
                    {
                        // Keep a single uninterrupted loop at the Telegraph -> Active boundary.
                        playback.WasRecovering = 0;
                        motion = 2;
                        progress = playback.AttackPhase;
                    }
                    else if (playback.WasRecovering == 1)
                    {
                        motion = 6;
                        progress = math.saturate(playback.RecoveryAge / math.max(.01f, samples.Durations[6]));
                        playback.RecoveryAge += DeltaTime;
                        if (progress >= 1f) playback.WasRecovering = 0;
                    }
                    else motion = Movement[owner].Speed > .01f ? 1 : 0;
                    if (playback.HitActive != 0)
                    {
                        motion = 5;
                        progress = playback.HitPhase;
                        playback.HitPhase = math.saturate(playback.HitPhase
                            + DeltaTime / math.max(.01f, samples.Durations[5]));
                        if (playback.HitPhase >= 1f) playback.HitActive = 0;
                    }
                }
                bool loop = motion == 0 || motion == 1 || motion == 2 || motion == 4
                    || motion == EnemyAnimationSamples.FlyingMotion;
                if (progress < 0f)
                {
                    playback.Phase = math.frac(playback.Phase + DeltaTime / math.max(.01f, samples.Durations[motion]));
                    progress = playback.Phase;
                }
                float frame = progress * (loop ? samples.FrameCount : samples.FrameCount - 1);
                int from = (int)frame;
                ApplyFrame(ref skin, ref samples, motion, from,
                    loop ? (from + 1) % samples.FrameCount : math.min(from + 1, samples.FrameCount - 1), math.frac(frame));
                float spawnBlend = math.smoothstep(0f, 1f, math.saturate(playback.RecoveryAge / .12f));
                for (int bone = 0; bone < skin.Length; bone++)
                {
                    float3x4 pose = skin[bone].Value;
                    pose.c3.y += clearance;
                    if (leavingSpawn)
                    {
                        var origin = samples.Matrices[(7 * samples.FrameCount + samples.FrameCount - 1) * samples.BoneCount + bone];
                        origin.c3.y += clearance;
                        pose = origin * (1f - spawnBlend) + pose * spawnBlend;
                    }
                    skin[bone] = new SkinMatrix { Value = pose };
                }
                if (leavingSpawn)
                {
                    playback.RecoveryAge += DeltaTime;
                    playback.WasRecovering = (byte)(spawnBlend < 1f ? 2 : 0);
                }
            }

            private void AnimateBaseline(Entity owner, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples, EnemyLaunchState launch, float3 pivot)
            {
                if (launch.Phase == EnemyLaunchPhase.Launched)
                {
                    AnimateCreatureFlight(owner, ref playback, ref skin, ref samples, launch, EnemyAnimationSamples.FlyingMotion, true, pivot);
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

            private void AnimateCreatureFlight(Entity owner, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples,
                EnemyLaunchState launch, int motion, bool sideways, float3 pivot = default)
            {
                bool entering = playback.WasLaunched == 0 || playback.LaunchSequence != launch.LaunchSequence;
                playback.FlightPhase = entering ? 0 : math.frac(playback.FlightPhase
                    + DeltaTime * .85f / math.max(.01f, samples.Durations[motion]));
                playback.WasLaunched = 1;
                playback.LaunchSequence = launch.LaunchSequence;
                playback.Landing = 0;
                float pitch = sideways ? -math.PI * .5f : 0;
                if (Velocities.HasComponent(owner))
                {
                    float3 velocity = Velocities[owner].Linear;
                    if (math.lengthsq(velocity) > .01f)
                        pitch -= math.clamp(math.atan2(velocity.y, math.length(velocity.xz)), -.5f, .5f);
                }
                float frame = playback.FlightPhase * samples.FrameCount;
                int from = (int)frame;
                ApplyRotatedFrame(ref skin, ref samples, motion, from, (from + 1) % samples.FrameCount,
                    math.frac(frame), quaternion.RotateX(pitch), pivot);
            }

            private void AnimateCreatureRecovery(ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples,
                EnemyLaunchState launch, bool sideways, float3 pivot)
            {
                if (playback.WasRecovering == 0)
                {
                    playback.RecoveryAge = 0;
                    playback.ImpactDuration = math.max(.01f, launch.RecoverySecondsRemaining);
                }
                else playback.RecoveryAge += DeltaTime;
                playback.WasRecovering = 1;
                playback.WasLaunched = 0;
                playback.Landing = 0;
                playback.WasAttacking = 0;
                playback.Phase = math.frac(playback.Phase + DeltaTime * .45f / math.max(.01f, samples.Durations[0]));
                float frame = playback.Phase * samples.FrameCount;
                int from = (int)frame;
                float rise = math.smoothstep(0, 1, playback.RecoveryAge / math.min(.24f, playback.ImpactDuration));
                ApplyRotatedFrame(ref skin, ref samples, 0, from, (from + 1) % samples.FrameCount,
                    math.frac(frame), quaternion.RotateX(sideways ? -math.PI * .5f * (1 - rise) : 0), pivot);
            }

            private static void ApplyDasherFrame(ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples,
                int motion, int from, int to, float blend)
            {
                int start = motion * samples.FrameCount * samples.BoneCount;
                for (int bone = 0; bone < skin.Length; bone++)
                {
                    float3x4 a = samples.Matrices[start + from * samples.BoneCount + bone];
                    float3x4 b = samples.Matrices[start + to * samples.BoneCount + bone];
                    // Shared rotation interpolation also preserves mirrored equipment bind poses.
                    skin[bone] = new SkinMatrix { Value = BlendPose(a, b, blend, true) };
                }
            }

            private static void ApplyFrame(ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples,
                int motion, int from, int to, float blend)
            {
                for (int bone = 0; bone < skin.Length; bone++)
                    skin[bone] = new SkinMatrix { Value = Sample(ref samples, motion, from, to, bone, blend) };
            }

            private static void ApplyRotatedFrame(ref DynamicBuffer<SkinMatrix> skin,
                ref EnemyAnimationSamples samples, int motion, int from, int to, float blend, quaternion rotation, float3 pivot = default)
            {
                var matrix = new float3x3(rotation);
                for (int bone = 0; bone < skin.Length; bone++)
                {
                    float3x4 pose = Sample(ref samples, motion, from, to, bone, blend);
                    skin[bone] = new SkinMatrix { Value = new float3x4(math.mul(matrix, pose.c0),
                        math.mul(matrix, pose.c1), math.mul(matrix, pose.c2), pivot + math.mul(matrix, pose.c3 - pivot)) };
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
