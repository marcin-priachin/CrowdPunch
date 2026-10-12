using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Initialization
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemySpawnWarningSystem)), UpdateAfter(typeof(EnemyWaveSpawnSystem))]
    [UpdateBefore(typeof(AI.EnemyChaseSystem))]
    public partial struct SnakeTailSpawnSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            var grid = SystemAPI.HasSingleton<NavigationGrid>() ? SystemAPI.GetSingleton<NavigationGrid>() : default;
            foreach (var (head, tuning, pose, lifetime, entity) in
                SystemAPI.Query<RefRW<SnakeHead>, RefRO<SnakeTuning>, RefRO<LocalTransform>, RefRO<EnemyLifetime>>()
                    .WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (head.ValueRO.Initialized != 0 && head.ValueRO.Generation == lifetime.ValueRO.Generation) continue;
                var t = tuning.ValueRO;
                var mass = SystemAPI.GetComponent<PhysicsMass>(entity);
                if (head.ValueRO.Initialized == 0) head.ValueRW.DynamicMass = mass;
                head.ValueRW.Initialized = 1;
                head.ValueRW.Generation = lifetime.ValueRO.Generation;
                head.ValueRW.AttachedCount = t.SegmentCount;
                // SNAKE-004/006: head health is already initialized/reset by the spawn profile.
                // Constructing its tail must not overwrite that profile or restore combat damage.
                var contact = SystemAPI.GetComponent<EnemyContactDamageSettings>(entity);
                contact.DamagePercent = t.ContactDamagePercent;
                SystemAPI.SetComponent(entity, contact);
                SystemAPI.SetComponent(entity, new EnemyGroundConstraint { SnapRequested = 1 });
                var movement = SystemAPI.GetComponent<EnemyMovementSettings>(entity);
                movement.MoveSpeed = t.PursuitSpeed; movement.ChargeSpeedMultiplier = 1;
                SystemAPI.SetComponent(entity, movement);
                var links = commands.SetBuffer<SnakeTailLink>(entity);
                var path = SystemAPI.GetBuffer<SnakePathPoint>(entity); path.Clear();
                float3 start = pose.ValueRO.Position;
                // Seed a clear recent path, choosing a rear direction with room for the authored tail.
                float2 back = new float2(0, 1); float length = 0;
                for (int d = 0; d < 16; d++)
                {
                    float2 direction = new float2(math.cos(d * math.PI / 8), math.sin(d * math.PI / 8));
                    float available = 0;
                    for (int i = 1; i <= t.SegmentCount * 6; i++)
                    {
                        float distance = i * t.Spacing / 6;
                        if (grid.Data.IsCreated && !NavigationGeometry.Segment(ref grid.Data.Value, start.xz,
                            start.xz + direction * distance, t.OrbRadius)) break;
                        available = distance;
                    }
                    if (available > length) { length = available; back = direction; }
                }
                int samples = math.max(1, t.SegmentCount * 6);
                for (int i = samples; i >= 0; i--)
                    path.Add(new SnakePathPoint { Position = start + new float3(back.x, 0, back.y) * length * i / samples });
                var encounter = SystemAPI.HasComponent<EnemyWaveOwnership>(entity) ? SystemAPI.GetComponent<EnemyWaveOwnership>(entity) : default;
                for (int i = 0; i < t.SegmentCount; i++)
                {
                    var orb = commands.Instantiate(t.OrbPrefab);
                    float3 p = start + new float3(back.x, 0, back.y) * math.min(length, (i + 1) * t.Spacing);
                    commands.SetComponent(orb, LocalTransform.FromPositionRotationScale(p, quaternion.identity, t.OrbRadius));
                    commands.AddComponent(orb, new SnakeSegment { Head = entity, Generation = lifetime.ValueRO.Generation,
                        Index = i, Radius = t.OrbRadius, Encounter = encounter });
                    commands.AddComponent(orb, new EnemyTier { Value = EnemyCombatTier.Normal });
                    commands.AddComponent(orb, new EnemyArchetype { Value = EnemyArchetypeKind.Baseline });
                    commands.AddComponent(orb, new EnemyRespawnSettings { Enabled = 0 });
                    commands.AddComponent(orb, new EnemyHealthBarPolicy { Value = EnemyHealthBarPolicyKind.TemporaryAfterDamage });
                    commands.AddComponent<ElitePunchReservation>(orb);
                    commands.SetComponent(orb, new Health { Current = 30, Max = 30 });
                    contact.ContactRadius = t.OrbRadius; commands.SetComponent(orb, contact);
                    commands.SetComponent(orb, movement);
                    commands.SetComponent(orb, new EnemyGroundConstraint { SnapRequested = 1 });
                    commands.SetComponent(orb, new NavigationAgent { Radius = t.OrbRadius });
                    links.Add(new SnakeTailLink { Orb = orb });
                }
            }
            commands.Playback(state.EntityManager);
        }
    }
}
