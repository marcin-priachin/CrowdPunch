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
    [UpdateAfter(typeof(EnemyAnimationSystem)), UpdateAfter(typeof(DinoAnimationSystem))]
    public partial struct StandingTurnAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new Animate
            {
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                Elites = SystemAPI.GetComponentLookup<ElitePunchState>(true),
                Dinos = SystemAPI.GetComponentLookup<DinoBoss>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                Delta = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Animate : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<ElitePunchState> Elites;
            [ReadOnly] public ComponentLookup<DinoBoss> Dinos;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<RespawnRequest> Respawns;
            public float Delta;

            private void Execute(in EnemyAnimation animation, in EnemyAnimationPlayback basePlayback,
                in StandingTurnAnimation tuning, ref StandingTurnPlayback playback, ref DynamicBuffer<SkinMatrix> skin)
            {
                Entity owner = animation.Owner;
                if (!Transforms.HasComponent(owner) || !tuning.Samples.IsCreated || !animation.Samples.IsCreated) return;
                float3 forward = math.normalizesafe(math.forward(Transforms[owner].Rotation) * new float3(1, 0, 1), math.forward());
                bool elite = Elites.HasComponent(owner);
                bool dino = Dinos.HasComponent(owner);
                bool available = elite && Launches.HasComponent(owner) && Launches[owner].Phase == EnemyLaunchPhase.Active
                    && Elites[owner].Phase != ElitePunchPhase.CommittedSweep && basePlayback.HitActive == 0;
                available |= dino && Dinos[owner].Phase != DinoPhase.Stagger && Dinos[owner].Phase != DinoPhase.Defeated;
                available &= !Respawns.HasComponent(owner) || !Respawns.IsComponentEnabled(owner) || Respawns[owner].IsPooled == 0;
                float speed = Velocities.HasComponent(owner) ? math.length(Velocities[owner].Linear.xz) : 0;
                if (playback.Initialized == 0 || !available || speed > .03f)
                {
                    // Sample current facing while moving/disabled so a stop or respawn cannot
                    // replay accumulated travel yaw. Locomotion and combat retain their pose.
                    playback = new StandingTurnPlayback { PreviousForward = forward, Initialized = 1 };
                    return;
                }
                if (Delta <= 0) return;
                float yaw = math.atan2(math.cross(playback.PreviousForward, forward).y, math.dot(playback.PreviousForward, forward));
                playback.PreviousForward = forward;
                bool rotating = math.abs(yaw) / Delta >= tuning.MinimumSpeed && math.abs(yaw) > .000001f;
                if (rotating)
                {
                    if (math.sign(yaw) != math.sign(playback.AccumulatedYaw)) playback.AccumulatedYaw = 0;
                    playback.AccumulatedYaw += yaw;
                    playback.StoppedTime = 0;
                }
                else
                {
                    playback.AccumulatedYaw = 0;
                    playback.StoppedTime += Delta;
                }
                int requested = rotating && math.abs(playback.AccumulatedYaw) >= tuning.MinimumAngle ? (yaw < 0 ? -1 : 1) : 0;
                if (requested != 0 && requested != playback.Direction)
                {
                    playback.Direction = requested;
                    playback.Phase = 0;
                    playback.AccumulatedYaw = 0;
                }
                if (playback.Direction == 0) return;
                bool active = rotating || playback.StoppedTime < tuning.StopDelay;
                float duration = active ? tuning.BlendInDuration : tuning.BlendOutDuration;
                float step = duration > 0 ? Delta / duration : 1;
                playback.Weight = active ? math.min(1, playback.Weight + step) : math.max(0, playback.Weight - step);
                ref var samples = ref tuning.Samples.Value;
                int clip = playback.Direction < 0 ? 0 : 1;
                playback.Phase += Delta / math.max(.01f, samples.Durations[clip]);
                playback.Phase = active ? math.frac(playback.Phase) : math.saturate(playback.Phase);
                int motion = dino ? math.clamp(basePlayback.Initialized - 1, 0, 4) : 0;
                Apply(ref samples, ref animation.Samples.Value, ref skin, motion, basePlayback.Phase,
                    playback.Phase, clip, playback.Weight);
                if (playback.Weight == 0)
                {
                    playback.Direction = 0;
                    playback.Phase = 0;
                }
            }

            private static void Apply(ref StandingTurnSamples turns, ref EnemyAnimationSamples original,
                ref DynamicBuffer<SkinMatrix> skin, int motion, float basePhase, float phase, int clip, float weight)
            {
                if (turns.BoneCount != skin.Length) return;
                var hip = math.mul(Matrix(skin[turns.HipBoneIndex].Value), turns.HipBindInverse);
                var sourceHip = math.mul(Matrix(ChickenSkinPose.Bone(ref original, motion, basePhase, motion < 2, turns.HipBoneIndex)), turns.HipBindInverse);
                var toCurrentHip = math.mul(hip, math.inverse(sourceHip));
                float frame = phase * (turns.FrameCount - 1);
                int from = math.min((int)frame, turns.FrameCount - 1), to = math.min(from + 1, turns.FrameCount - 1);
                int count = turns.LegBones.Length;
                for (int leg = 0; leg < count; leg++)
                {
                    int bone = turns.LegBones[leg];
                    var relative = ChickenSkinPose.Blend(turns.Matrices[(clip * turns.FrameCount + from) * count + leg],
                        turns.Matrices[(clip * turns.FrameCount + to) * count + leg], math.frac(frame));
                    var target = math.mul(hip, Matrix(relative));
                    // Resample the base leg pose instead of feeding last frame's overlay
                    // through Dino's smoothing again. The configured exit reaches exact idle.
                    var basis = math.mul(toCurrentHip, Matrix(ChickenSkinPose.Bone(ref original, motion, basePhase, motion < 2, bone)));
                    skin[bone] = new SkinMatrix { Value = ChickenSkinPose.Blend(Pose(basis), Pose(target), weight) };
                }
            }

            private static float4x4 Matrix(float3x4 p) => new float4x4(new float4(p.c0, 0), new float4(p.c1, 0), new float4(p.c2, 0), new float4(p.c3, 1));
            private static float3x4 Pose(float4x4 p) => new float3x4(p.c0.xyz, p.c1.xyz, p.c2.xyz, p.c3.xyz);
        }
    }
}
