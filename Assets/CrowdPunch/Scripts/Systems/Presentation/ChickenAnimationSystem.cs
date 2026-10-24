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
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup)), UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial struct ChickenAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new Animate {
                Bosses = SystemAPI.GetComponentLookup<ChickenBoss>(true),
                Tuning = SystemAPI.GetComponentLookup<ChickenTuning>(true),
                CastTuning = SystemAPI.GetComponentLookup<ChickenCastTuning>(true),
                Turns = SystemAPI.GetComponentLookup<ChickenTurnState>(true),
                Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Elapsed = SystemAPI.Time.ElapsedTime,
                Delta = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Animate : IJobEntity
        {
            [ReadOnly] public ComponentLookup<ChickenBoss> Bosses;
            [ReadOnly] public ComponentLookup<ChickenTuning> Tuning;
            [ReadOnly] public ComponentLookup<ChickenCastTuning> CastTuning;
            [ReadOnly] public ComponentLookup<ChickenTurnState> Turns;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            public float Delta;
            public double Elapsed;

            public void Execute(in EnemyAnimation animation, ref EnemyAnimationPlayback playback, ref DynamicBuffer<SkinMatrix> skin)
            {
                if (!Bosses.HasComponent(animation.Owner) || !animation.Samples.IsCreated) return;
                var boss = Bosses[animation.Owner]; var tuning = Tuning[animation.Owner];
                ref var samples = ref animation.Samples.Value;
                if (skin.Length != samples.BoneCount) return;
                if (playback.ProjectileSequence != boss.ShotSequence && boss.ShotSequence == 0)
                    playback = default;
                // Release commits the rush immediately; keep the rest of the Sweep on the upper body.
                bool followThrough = boss.ShotSequence != 0 && boss.SinceShot < tuning.WindUp * (1 - tuning.ShotReleasePhase);
                float yawRate=Turns.HasComponent(animation.Owner)?Turns[animation.Owner].YawRate:0;
                int requestedTurn=yawRate<-.02f?6:yawRate>.02f?7:0;
                int previousMotion=playback.Initialized-1;
                bool finishingTurn=(previousMotion==6 || previousMotion==7) && playback.Phase<1;
                int idleMotion=requestedTurn!=0?requestedTurn:finishingTurn?previousMotion:0;
                int motion = boss.Phase == ChickenPhase.Defeated ? 4 : boss.Phase == ChickenPhase.Stagger ? 3
                    : boss.Phase == ChickenPhase.Cast ? 5
                    : boss.Phase == ChickenPhase.WindUp || followThrough ? 2 : boss.Phase == ChickenPhase.Rush ? 1
                    : boss.Phase==ChickenPhase.Pause?idleMotion:0;
                bool initialized = playback.Initialized != 0;
                bool entering = playback.Initialized != motion + 1;
                float phase = entering ? 0 : playback.Phase;
                bool turning=motion==6 || motion==7;
                if(turning && phase>=1) phase=0;
                bool loop = motion < 2;
                float idlePhase=(float)math.frac(Elapsed/math.max(.01f,samples.Durations[0]));
                if (motion == 5)
                    phase = 1 - math.saturate(boss.Remaining / math.max(.01f, CastTuning.HasComponent(animation.Owner)
                        ? CastTuning[animation.Owner].Duration : samples.Durations[5]));
                else if (motion == 2)
                    phase = boss.Phase == ChickenPhase.WindUp
                        ? math.max(0, tuning.ShotReleasePhase - math.max(0, boss.Remaining) / math.max(.01f, tuning.WindUp))
                        : math.saturate(tuning.ShotReleasePhase + boss.SinceShot / math.max(.01f, tuning.WindUp));
                else if(motion==0) phase=idlePhase;
                else
                {
                    float duration = motion == 3 ? tuning.Stagger : samples.Durations[motion];
                    phase += Delta / math.max(.01f, duration);
                    phase = loop ? math.frac(phase) : math.saturate(phase);
                }
                float blend = initialized ? 1 - math.exp(-math.max(0, animation.BlendResponse) * Delta) : 1;
                if (animation.BlendResponse <= 0) blend = 1;
                // Do not ease the firing pose behind the authoritative release frame.
                if (motion == 2 || motion == 5) blend = 1;
                if(turning)
                {
                    if(animation.JogSamples.IsCreated)
                        ChickenSkinPose.IdleWithTurnLegs(ref samples,ref animation.JogSamples.Value,ref skin,motion,phase,idlePhase,blend);
                    else
                        // Head/hand equipment follows the same Idle upper-body clock as the body.
                        ChickenSkinPose.Sample(ref samples,ref skin,0,idlePhase,true,blend);
                }
                else ChickenSkinPose.Sample(ref samples, ref skin, motion, phase, loop, blend);
                playback.Phase = phase; playback.Initialized = (byte)(motion + 1);
                playback.ProjectileSequence = boss.ShotSequence;
                if (animation.JogSamples.IsCreated && boss.Phase != ChickenPhase.Defeated && boss.Phase != ChickenPhase.Stagger
                    && boss.Phase != ChickenPhase.Cast
                    && !turning
                    && Velocities.HasComponent(animation.Owner) && Transforms.HasComponent(animation.Owner))
                {
                    float3 local = math.rotate(math.inverse(Transforms[animation.Owner].Rotation), Velocities[animation.Owner].Linear);
                    float2 movement = local.xz / math.max(.01f, tuning.RushSpeed * (boss.Stage == 3 ? tuning.FinalRushMultiplier : 1));
                    movement /= math.max(1, math.length(movement));
                    playback.Movement = math.lerp(playback.Movement, movement, blend);
                    ChickenSkinPose.Jog(ref animation.JogSamples.Value, ref skin, ref playback, Delta);
                }
                else { playback.Movement = 0; playback.JogPhase = 0; }
            }
        }
    }
}
