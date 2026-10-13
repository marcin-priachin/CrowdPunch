using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Movement
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyMovementSystem))]
    public partial struct SnakeTailMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = math.max(.001f, SystemAPI.Time.DeltaTime);
            var grid = SystemAPI.HasSingleton<NavigationGrid>() ? SystemAPI.GetSingleton<NavigationGrid>() : default;
            foreach (var (head, tuning, pose, path, links, entity) in
                SystemAPI.Query<RefRO<SnakeHead>, RefRO<SnakeTuning>, RefRO<LocalTransform>,
                    DynamicBuffer<SnakePathPoint>, DynamicBuffer<SnakeTailLink>>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (head.ValueRO.Initialized == 0) continue;
                var t = tuning.ValueRO;
                var mass = head.ValueRO.DynamicMass;
                mass.InverseInertia.x = 0; mass.InverseInertia.z = 0;
                if (SnakeHitRules.Protected(head.ValueRO)) { mass.InverseMass = 0; mass.InverseInertia = float3.zero; }
                SystemAPI.SetComponent(entity, mass);
                if (SystemAPI.GetComponent<EnemyLaunchState>(entity).Phase != EnemyLaunchPhase.Active) continue;
                if (grid.Data.IsCreated && SystemAPI.HasComponent<NavigationAgent>(entity))
                {
                    var headVelocity = SystemAPI.GetComponent<PhysicsVelocity>(entity);
                    headVelocity.Linear.xz = SnakeHeadMovement.LimitStep(ref grid.Data.Value, pose.ValueRO.Position.xz,
                        headVelocity.Linear.xz, SystemAPI.GetComponent<NavigationAgent>(entity).Radius, dt);
                    SystemAPI.SetComponent(entity, headVelocity);
                }
                if (path.Length == 0 || math.distancesq(path[path.Length - 1].Position, pose.ValueRO.Position) > .0025f)
                    path.Add(new SnakePathPoint { Position = pose.ValueRO.Position });
                // Bound history by travelled distance; keep the oldest corner needed by the last orb.
                float retained = 0; int first = path.Length - 1;
                while (first > 0 && retained < (t.SegmentCount + 2) * t.Spacing)
                { retained += math.distance(path[first].Position, path[first - 1].Position); first--; }
                if (first > 0) path.RemoveRange(0, first);
                if (path.Length > 2048) path.RemoveRange(0, path.Length - 2048);
                for (int i = 0; i < links.Length; i++)
                {
                    Entity orb = links[i].Orb;
                    if (!SystemAPI.HasComponent<SnakeSegment>(orb) || SystemAPI.GetComponent<SnakeSegment>(orb).Detached != 0) continue;
                    var p = SystemAPI.GetComponent<LocalTransform>(orb);
                    float3 tangent;
                    var center = Sample(path, pose.ValueRO.Position, (i + 1) * t.Spacing, out tangent);
                    float3 side = new float3(-tangent.z, 0, tangent.x);
                    float amplitude = t.WaveAmplitude * math.sin((float)SystemAPI.Time.ElapsedTime * math.PI * 2 * t.WaveFrequencyHz - i * .8f);
                    float3 desired = center + side * amplitude;
                    // Sweep from the actual body, so reducing the wave cannot cut across an obstacle corner.
                    bool clear = !grid.Data.IsCreated || NavigationGeometry.Segment(ref grid.Data.Value, p.Position.xz, desired.xz, t.OrbRadius);
                    for (int attempt = 0; !clear && attempt < 8; attempt++)
                    {
                        amplitude *= .5f; desired = center + side * amplitude;
                        clear = NavigationGeometry.Segment(ref grid.Data.Value, p.Position.xz, desired.xz, t.OrbRadius);
                    }
                    var velocity = SystemAPI.GetComponent<PhysicsVelocity>(orb);
                    float2 target = clear ? (desired.xz - p.Position.xz) / dt : float2.zero;
                    float maximum = math.max(8, t.PursuitSpeed * 3);
                    target *= math.min(1, maximum / math.max(.001f, math.length(target)));
                    velocity.Linear.xz = target;
                    velocity.Angular = float3.zero;
                    SystemAPI.SetComponent(orb, velocity);
                }
            }
        }

        internal static float3 Sample(DynamicBuffer<SnakePathPoint> path, float3 head, float distance, out float3 tangent)
        {
            float3 newer = head; tangent = new float3(0, 0, 1);
            for (int i = path.Length - 1; i >= 0; i--)
            {
                float3 older = path[i].Position;
                float length = math.distance(newer, older);
                if (length > .0001f)
                {
                    tangent = (newer - older) / length;
                    if (distance <= length) return math.lerp(newer, older, distance / length);
                    distance -= length;
                }
                newer = older;
            }
            return newer;
        }
    }
}
