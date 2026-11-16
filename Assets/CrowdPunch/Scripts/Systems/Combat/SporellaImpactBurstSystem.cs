using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyLaunchCollisionSystem))]
    [UpdateAfter(typeof(SporellaProjectileSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct SporellaImpactBurstSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireAnyForUpdate(state.GetEntityQuery(ComponentType.ReadOnly<SporellaImpactContact>()),
                state.GetEntityQuery(ComponentType.ReadOnly<SporellaImpactBurst>()));
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
            foreach (var (burstRef, entity) in SystemAPI.Query<RefRW<SporellaImpactBurst>>().WithEntityAccess())
            {
                ref var burst = ref burstRef.ValueRW;
                if (!hasArena || burst.Shot.Arena != arena)
                {
                    commands.DestroyEntity(entity);
                    continue;
                }
                burst.Wait -= dt;
                while (burst.ShotsRemaining > 0 && burst.Wait <= .00001f)
                {
                    SporellaShotEmission.FireSweepShot(commands, burst.Shot, burst.Sweep, burst.ShotsRemaining, audio);
                    burst.ShotsRemaining--;
                    burst.Wait += burst.Interval;
                }
                if (burst.ShotsRemaining == 0) commands.DestroyEntity(entity);
            }

            // Deliberately include pending-defeat emitters. The collision job has already
            // accepted the contact, and the remaining timers never depend on the emitter.
            foreach (var (contacts, settings, ranged, emitter) in
                SystemAPI.Query<DynamicBuffer<SporellaImpactContact>, RefRO<SporellaSettings>, RefRO<RangedEnemySettings>>()
                    .WithEntityAccess())
            {
                if (hasArena)
                {
                    uint generation = lifetimes.TryGetComponent(emitter, out var life) ? life.Generation : 0;
                    var tuning = settings.ValueRO;
                    float height = SporellaShotGeometry.Height(floor.SurfaceHeight, ranged.ValueRO.ProjectileRadius, tuning);
                    foreach (var contact in contacts)
                    {
                        var sweep = SporellaShotGeometry.AimAlongImpact(contact.Position, contact.Direction, height, tuning);
                        var shot = SporellaShotEmission.Create(arena, emitter, generation,
                            SporellaShotKind.ImpactShot, tuning, ranged.ValueRO);
                        shot.PunchSequence = contact.PunchSequence;
                        shot.ChainDepth = contact.ChainDepth;
                        SporellaShotEmission.FireSweepShot(commands, shot, sweep, sweep.ShotCount, audio);
                        if (sweep.ShotCount <= 1) continue;
                        var entity = commands.CreateEntity();
                        float interval = SporellaShotEmission.Interval(tuning);
                        commands.AddComponent(entity, new SporellaImpactBurst
                        {
                            Shot = shot, Sweep = sweep, ShotsRemaining = sweep.ShotCount - 1, Interval = interval, Wait = interval
                        });
                    }
                }
                contacts.Clear();
            }
            commands.Playback(state.EntityManager);
        }
    }
}
