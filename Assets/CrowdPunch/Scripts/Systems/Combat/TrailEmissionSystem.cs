using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Scenes;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile, UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(ExplosionResolutionSystem))]
    [UpdateAfter(typeof(EnemyGroundReconciliationSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct TrailEmissionSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<TrailSettings>();
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<PhysicsWorldSingleton>()) return;
            var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            double now = SystemAPI.Time.ElapsedTime;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (settings, emitter, launch, pose, velocity, life, entity) in
                SystemAPI.Query<RefRO<TrailSettings>, RefRW<TrailEmitter>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>,
                    RefRO<PhysicsVelocity>, RefRO<EnemyLifetime>>().WithEntityAccess())
            {
                ref var e = ref emitter.ValueRW;
                var s = settings.ValueRO;
                float3 position = pose.ValueRO.Position;
                bool launched = launch.ValueRO.Phase == EnemyLaunchPhase.Launched;
                bool eligible = (launched || launch.ValueRO.Phase == EnemyLaunchPhase.Active) &&
                    (!SystemAPI.HasComponent<RespawnRequest>(entity) || !SystemAPI.IsComponentEnabled<RespawnRequest>(entity));
                float3 previous = e.Initialized != 0 ? e.PreviousPosition : position;
                e.PreviousPosition = position; e.Initialized = 1;
                float distance = math.distance(previous.xz, position.xz);
                bool moving = eligible && distance > math.max(.00001f, s.MinimumSpeed * SystemAPI.Time.DeltaTime);
                if (!moving) { e.Emitting = 0; e.Anchor = position; continue; }
                bool fresh = e.Emitting == 0 || (e.Launched != 0) != launched;
                if (fresh) e.Anchor = previous;
                e.Emitting = 1; e.Launched = launched ? (byte)1 : (byte)0;
                float width = math.max(.01f, launched ? s.LaunchedWidth : s.NormalWidth);
                // Spacing is bounded by radius, keeping the moving endpoint inside the last capsule.
                if (!fresh && math.distance(e.Anchor.xz, position.xz) < math.max(.01f, math.min(s.SectionSpacing, width * .45f))) continue;
                float lifetime = math.max(.01f, launched ? s.LaunchedLifetime : s.NormalLifetime);
                TrailSource source;
                if (state.EntityManager.HasComponent<TrailSource>(e.Record)) source = state.EntityManager.GetComponentData<TrailSource>(e.Record);
                else
                {
                    source = new TrailSource { Enemy = entity, Lifetime = life.ValueRO.Generation };
                    if (SystemAPI.HasComponent<EnemyWaveOwnership>(entity))
                    {
                        var owner = SystemAPI.GetComponent<EnemyWaveOwnership>(entity);
                        source.Sequence = source.SceneOwner = owner.Sequence;
                        source.RunGeneration = owner.RunGeneration; source.WaveIndex = owner.WaveIndex;
                    }
                    else if (state.EntityManager.HasComponent<SceneTag>(entity))
                        source.SceneOwner = state.EntityManager.GetSharedComponent<SceneTag>(entity).SceneEntity;
                    e.Record = commands.CreateEntity();
                    commands.AddComponent(e.Record, source);
                    commands.AddBuffer<TrailDamageTarget>(e.Record);
                }
                source.ExpiresAt = math.max(source.ExpiresAt, now + lifetime);
                commands.SetComponent(e.Record, source);
                var section = new TrailSection
                {
                    Record = e.Record, Source = entity, SourceLifetime = life.ValueRO.Generation,
                    Start = Project(physics, e.Anchor), End = Project(physics, position),
                    CreatedAt = now, ExpiresAt = now + lifetime, Width = width,
                    Damage = math.max(0, launched ? s.LaunchedDamage : s.NormalDamage),
                    TickInterval = math.max(.01f, s.TickInterval), PlayerProtection = math.max(0, s.PlayerProtection),
                    Immunity = s.Immunity, Avoidance = s.Avoidance,
                    DamagesEnemies = launched || s.EnemyDamage == TrailEnemyDamageMode.Both ? (byte)1 : (byte)0,
                    Launched = launched ? (byte)1 : (byte)0,
                    Owner = launched ? launch.ValueRO.Owner : EnemyLaunchOwner.None,
                    ChainDepth = launched && launch.ValueRO.Owner == EnemyLaunchOwner.Player ? launch.ValueRO.FeedbackChainDepth : 0,
                    Color = launched ? s.LaunchedColor : s.NormalColor
                };
                commands.AddComponent(commands.CreateEntity(), section);
                e.Anchor = position;
                // Entity references in ECB component payloads are remapped on playback.
                commands.SetComponent(entity, e);
            }
            commands.Playback(state.EntityManager);
        }

        private static float3 Project(PhysicsWorldSingleton physics, float3 position)
        {
            var hits = new NativeList<Unity.Physics.RaycastHit>(Allocator.Temp);
            physics.CastRay(new RaycastInput { Start = position + new float3(0, 30, 0),
                End = position - new float3(0, 100, 0), Filter = CollisionFilter.Default }, ref hits);
            float height = float.MinValue;
            foreach (var hit in hits)
                if (hit.RigidBodyIndex >= physics.NumDynamicBodies && hit.SurfaceNormal.y > .5f && hit.Position.y <= position.y)
                    height = math.max(height, hit.Position.y);
            position.y = (height == float.MinValue ? -1 : height) + .035f;
            hits.Dispose();
            return position;
        }
    }
}
