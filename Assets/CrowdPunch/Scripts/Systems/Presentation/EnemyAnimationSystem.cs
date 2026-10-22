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
                Dragons = SystemAPI.GetComponentLookup<DragonFlameState>(true),
                Wizards = SystemAPI.GetComponentLookup<WizardCastState>(true),
                Armors = SystemAPI.GetComponentLookup<EnemyArmor>(true),
                Movement = SystemAPI.GetComponentLookup<DesiredMovement>(true),
                Settings = SystemAPI.GetComponentLookup<EnemyMovementSettings>(true),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                Dashers = SystemAPI.GetComponentLookup<DasherState>(true),
                Ninjas = SystemAPI.GetComponentLookup<NinjaState>(true),
                NinjaTuning = SystemAPI.GetComponentLookup<NinjaTuning>(true),
                RangedAttacks = SystemAPI.GetComponentLookup<RangedAttackState>(true),
                ElitePunches = SystemAPI.GetComponentLookup<ElitePunchState>(true),
                Health = SystemAPI.GetComponentLookup<Health>(true),
                Contacts = SystemAPI.GetComponentLookup<EnemyContactAttemptState>(true),
                ContactSettings = SystemAPI.GetComponentLookup<EnemyContactDamageSettings>(true),
                SpawnAnimations = SystemAPI.GetComponentLookup<EnemySpawnAnimation>(true),
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct PlaybackJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<DragonFlameState> Dragons;
            [ReadOnly] public ComponentLookup<WizardCastState> Wizards;
            [ReadOnly] public ComponentLookup<EnemyArmor> Armors;
            [ReadOnly] public ComponentLookup<DesiredMovement> Movement;
            [ReadOnly] public ComponentLookup<EnemyMovementSettings> Settings;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<RespawnRequest> Respawns;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<DasherState> Dashers;
            [ReadOnly] public ComponentLookup<NinjaState> Ninjas;
            [ReadOnly] public ComponentLookup<NinjaTuning> NinjaTuning;
            [ReadOnly] public ComponentLookup<RangedAttackState> RangedAttacks;
            [ReadOnly] public ComponentLookup<ElitePunchState> ElitePunches;
            [ReadOnly] public ComponentLookup<Health> Health;
            [ReadOnly] public ComponentLookup<EnemyContactAttemptState> Contacts;
            [ReadOnly] public ComponentLookup<EnemyContactDamageSettings> ContactSettings;
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
                    ApplyFrame(ref skin, ref samples, animation.SpawnMotion, from,
                        math.min(from + 1, samples.FrameCount - 1), math.frac(spawnFrame));
                    playback.WasRecovering = 0;
                    playback.WasLaunched = 0;
                    playback.Landing = 0;
                    playback.HitActive = 0;
                    return;
                }
                if (animation.Profile == (byte)EnemyAnimationProfile.Goblin)
                {
                    AnimateGoblin(owner, ref playback, ref skin, ref samples, launch);
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
                if (animation.Profile == (byte)EnemyAnimationProfile.Wizard)
                {
                    bool casting = launch.Phase == EnemyLaunchPhase.Active && Wizards.HasComponent(owner) && Wizards[owner].IsCasting;
                    if (casting)
                    {
                        playback.AttackPhase = playback.WasAttacking == 0 ? 0 : math.frac(playback.AttackPhase + DeltaTime / math.max(.01f, samples.Durations[2]));
                        playback.WasAttacking = 1;
                        float danceFrame = playback.AttackPhase * samples.FrameCount;
                        int from = (int)danceFrame;
                        ApplyFrame(ref skin, ref samples, 2, from, (from + 1) % samples.FrameCount, math.frac(danceFrame));
                    }
                    else { playback.WasAttacking = 0; AnimateBaseline(owner, ref playback, ref skin, ref samples, launch, animation.LaunchPivot); }
                    return;
                }
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
                if (launch.Phase == EnemyLaunchPhase.Launched)
                {
                    AnimateCreatureFlight(owner, ref playback, ref skin, ref samples, launch, 1, false);
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

            // Goblin slots: idle, walk, run, anticipation, strike, hit, rise, idle, idle, flight, fall.
            // All motion comes from authored poses; the physics owner keeps its transform and velocity.
            private void AnimateGoblin(Entity owner, ref EnemyAnimationPlayback playback,
                ref DynamicBuffer<SkinMatrix> skin, ref EnemyAnimationSamples samples, EnemyLaunchState launch)
            {
                bool hit = false;
                if (Health.HasComponent(owner))
                {
                    float health = Health[owner].Current;
                    hit = playback.HealthInitialized != 0 && health < playback.PreviousHealth;
                    playback.HealthInitialized = 1;
                    playback.PreviousHealth = health;
                }
                int motion;
                float progress = -1f;
                bool restart = false;
                if (launch.Phase == EnemyLaunchPhase.Launched)
                {
                    motion = EnemyAnimationSamples.FlyingMotion;
                    restart = playback.WasLaunched == 0 || playback.LaunchSequence != launch.LaunchSequence;
                    playback.WasLaunched = 1;
                    playback.LaunchSequence = launch.LaunchSequence;
                    playback.HitActive = 0;
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
                        var tuning = ContactSettings.HasComponent(owner) ? ContactSettings[owner] : default;
                        if (contact.IsWindingUp != 0)
                        {
                            motion = 3;
                            progress = math.saturate(1f - contact.SecondsRemaining / math.max(.01f, tuning.AttemptWindUpDuration));
                        }
                        else if (contact.IsAttempting != 0)
                        {
                            motion = 4;
                            progress = math.saturate(1f - contact.SecondsRemaining / math.max(.01f, tuning.AttemptDuration));
                        }
                        else if (playback.HitActive != 0) { motion = 5; progress = playback.HitPhase; }
                        else motion = Movement[owner].Speed <= .01f ? 0
                            : Movement[owner].Speed > Settings[owner].MoveSpeed * .65f ? 2 : 1;
                    }
                }
                playback.WasRecovering = (byte)(launch.Phase == EnemyLaunchPhase.Recovering ? 1 : 0);
                bool oneShot = motion == EnemyAnimationSamples.ImpactMotion;
                if (motion != playback.GoblinMotion || restart) playback.Phase = 0;
                else playback.Phase += DeltaTime / math.max(.01f, samples.Durations[motion]);
                playback.Phase = oneShot ? math.saturate(playback.Phase) : math.frac(playback.Phase);
                playback.GoblinMotion = (byte)motion;
                if (progress < 0) progress = oneShot ? math.saturate(playback.Phase) : math.frac(playback.Phase);
                float frame = progress * (oneShot || motion == 3 || motion == 4 || motion == 5 || motion == 6
                    ? samples.FrameCount - 1 : samples.FrameCount);
                int from = math.min((int)frame, samples.FrameCount - 1);
                int to = motion == 0 || motion == 1 || motion == 2 || motion == EnemyAnimationSamples.FlyingMotion
                    ? (from + 1) % samples.FrameCount : math.min(from + 1, samples.FrameCount - 1);
                ApplyFrame(ref skin, ref samples, motion, from, to, math.frac(frame));
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
