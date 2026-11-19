using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyRecoverySystem))]
    public partial struct SporellaImpactBurstSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SporellaImpactBurst>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool hasArena = SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var floor);
            Entity arena = hasArena ? SystemAPI.GetSingletonEntity<ArenaGroundPlane>() : Entity.Null;
            var lifetimes = SystemAPI.GetComponentLookup<EnemyLifetime>(true);
            SystemAPI.TryGetSingletonBuffer<EnemyAttackAudioEvent>(out var audio);
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var (burstRef, launchRef, settings, ranged, pose, velocity, history, emitter) in
                SystemAPI.Query<RefRW<SporellaImpactBurst>, RefRO<EnemyLaunchState>, RefRO<SporellaSettings>,
                    RefRO<RangedEnemySettings>, RefRO<LocalTransform>, RefRO<PhysicsVelocity>,
                    DynamicBuffer<SporellaImpactHistory>>()
                    .WithNone<RespawnRequest>().WithEntityAccess())
            {
                ref var burst = ref burstRef.ValueRW;
                ref readonly var launch = ref launchRef.ValueRO;
                var tuning = settings.ValueRO;
                if (!hasArena || launch.Phase != EnemyLaunchPhase.Launched || launch.Owner != EnemyLaunchOwner.Player
                    || !tuning.CanEmitOnLaunch(launch.LastCause))
                {
                    burst = default;
                    continue;
                }

                if (burst.LaunchSequence != launch.LaunchSequence || burst.Shot.Arena != arena)
                {
                    burst = new SporellaImpactBurst { LaunchSequence = launch.LaunchSequence };
                    uint generation = lifetimes.TryGetComponent(emitter, out var life) ? life.Generation : 0;
                    burst.Shot.Arena = arena;
                    if (!SporellaImpactRecording.TryRecord(history, emitter, generation, launch)) continue;

                    float3 facing = MushroomShotGeometry.Horizontal(math.forward(pose.ValueRO.Rotation), math.forward());
                    float3 direction = MushroomShotGeometry.Horizontal(velocity.ValueRO.Linear, facing);
                    float height = SporellaShotGeometry.Height(floor.SurfaceHeight, ranged.ValueRO.ProjectileRadius, tuning);
                    burst.Sweep = SporellaShotGeometry.AimAcrossLaunch(pose.ValueRO.Position, direction, height, tuning);
                    burst.ShotsRemaining = burst.Sweep.ShotCount;
                    burst.Shot = SporellaShotEmission.Create(arena, emitter, generation,
                        SporellaShotKind.ImpactShot, tuning, ranged.ValueRO);
                    burst.Shot.PunchSequence = launch.OriginatingPunchSequence;
                    burst.Shot.ChainDepth = launch.FeedbackChainDepth;
                }
                else if (burst.ShotsRemaining > 0)
                {
                    burst.Wait -= dt;
                }

                if (burst.ShotsRemaining == 0 || burst.Wait > .00001f) continue;
                burst.Sweep.Origin.xz = pose.ValueRO.Position.xz;
                float interval = SporellaShotEmission.Interval(tuning);
                while (burst.ShotsRemaining > 0 && burst.Wait <= .00001f)
                {
                    SporellaShotEmission.FireSweepShot(commands, burst.Shot, burst.Sweep, burst.ShotsRemaining, audio);
                    burst.Sweep.Forward = -burst.Sweep.Forward;
                    burst.ShotsRemaining--;
                    burst.Wait += interval;
                }
            }
            commands.Playback(state.EntityManager);
        }
    }
}
