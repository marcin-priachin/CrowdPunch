using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.InputBridge;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    // The baked grid stays immutable. Only this system owns and disposes its replacement blobs.
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PlayerObstacleCollisionSystem))]
    public partial struct TrackNavigationSystem : ISystem
    {
        private BlobAssetReference<NavigationGridBlob> original, owned;
        private Entity gridEntity;
        private float3 lastPosition;
        private byte lastMoving;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("CrowdPunch.TrackNavigation");

        public void OnDestroy(ref SystemState state) { if (owned.IsCreated) owned.Dispose(); }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = Marker.Auto();
            if (!SystemAPI.HasSingleton<NavigationGrid>() || !SystemAPI.HasSingleton<TrackObject>())
            {
                if (owned.IsCreated)
                {
                    if (state.EntityManager.Exists(gridEntity) && state.EntityManager.HasComponent<NavigationGrid>(gridEntity))
                    {
                        var previous = state.EntityManager.GetComponentData<NavigationGrid>(gridEntity);
                        if (previous.Data == owned) { previous.Data = original; state.EntityManager.SetComponentData(gridEntity, previous); }
                    }
                    owned.Dispose(); owned = default;
                }
                gridEntity = Entity.Null; original = default;
                return;
            }
            var entity = SystemAPI.GetSingletonEntity<NavigationGrid>();
            var grid = SystemAPI.GetSingleton<NavigationGrid>();
            if (entity != gridEntity || grid.Data != owned)
            {
                if (owned.IsCreated) owned.Dispose();
                owned = default; original = grid.Data; gridEntity = entity;
            }
            var target = SystemAPI.GetSingletonEntity<TrackObject>();
            var pose = SystemAPI.GetComponent<LocalTransform>(target);
            var motion = SystemAPI.GetComponent<TrackObjectState>(target);
            ref var source = ref original.Value;
            float threshold = source.CellSize * .25f;
            if (owned.IsCreated && math.distance(lastPosition, pose.Position) < threshold && lastMoving == motion.Moving) return;
            state.Dependency.Complete();
            var wall = SystemAPI.GetComponent<Barricade>(target);
            var bounds = wall.IntactCollider.Value.CalculateAabb(new RigidTransform(pose.Rotation, pose.Position), pose.Scale);
            var rectangles = new NativeArray<NavigationRectangle>(source.Obstacles.Length + 1, Allocator.Temp);
            for (int i = 0; i < source.Obstacles.Length; i++) rectangles[i] = source.Obstacles[i];
            // Cover the displacement allowed before the next rebuild; stationary footprints are exact.
            float padding = motion.Moving != 0 ? threshold : 0;
            rectangles[rectangles.Length - 1] = new NavigationRectangle { Minimum = bounds.Min.xz - padding, Maximum = bounds.Max.xz + padding };
            var next = NavigationGridConstruction.Build(source.Minimum, source.Maximum, source.CellSize,
                source.Radii, rectangles, Allocator.Persistent);
            rectangles.Dispose();
            next.Value.Margin = source.Margin;
            grid.Data = next; SystemAPI.SetSingleton(grid);
            if (owned.IsCreated) owned.Dispose();
            owned = next; lastPosition = pose.Position; lastMoving = motion.Moving;
            // EnemyNavigationSystem detects the new blob, cancels searches and revalidates paths.
        }
    }
}
