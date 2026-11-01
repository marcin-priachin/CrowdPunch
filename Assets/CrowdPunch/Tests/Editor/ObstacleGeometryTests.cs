using System;
using System.Collections.Generic;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.InputBridge;
using CrowdPunch.Systems.AI;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CrowdPunch.Tests
{
    public sealed class ObstacleGeometryTests
    {
        private static IEnumerable<string> Scenes() => AssetDatabase.FindAssets("t:Scene", new[] { "Assets/CrowdPunch/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p);

        [Test]
        public void Vision004_NavigationCoversLowProtrusionsWithoutBlockingOnOverheadCanopy()
        {
            var low = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                low.transform.position = new Vector3(2, .5f, 0); low.transform.localScale = new Vector3(2, 1, 2);
                canopy.transform.position = new Vector3(0, 6, 0); canopy.transform.localScale = new Vector3(20, 2, 20);
                var footprint = new NavigationRectangle { Minimum = new float2(-1), Maximum = new float2(1) };
                footprint = SolidObstacleGeometry.ExpandNavigationFootprint(footprint, low.GetComponent<MeshFilter>(), 0, 2);
                footprint = SolidObstacleGeometry.ExpandNavigationFootprint(footprint, canopy.GetComponent<MeshFilter>(), 0, 2);
                Assert.That(footprint.Minimum, Is.EqualTo(new float2(-1)));
                Assert.That(footprint.Maximum, Is.EqualTo(new float2(3, 1)));
            }
            finally { Object.DestroyImmediate(low); Object.DestroyImmediate(canopy); }
        }

        [Test]
        public void Vision004_LevelEightRockCutoutHasAPhysicalNavigationExit()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/CrowdPunch/Scenes/Campaign/Campaign_08/Campaign_08 Sub Scene.unity");
            try
            {
                var solid = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SolidObstacleAuthoring>())
                    .Single(o => o.transform.position.x < 0);
                using var rock = SolidObstacleGeometry.CreateCollider(solid.transform, solid.GetComponentsInChildren<MeshFilter>());
                using var body = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
                    { Radius = .5f, Vertex0 = new float3(0, -.5f, 0), Vertex1 = new float3(0, .5f, 0) });
                using var rectangles = new Unity.Collections.NativeArray<NavigationRectangle>(new[] { new NavigationRectangle
                    { Minimum = new float2(-16, 0), Maximum = new float2(-8, 12) } }, Unity.Collections.Allocator.Temp);
                using var grid = NavigationGridConstruction.Build(new float2(-50), new float2(50), 1,
                    new float3(.62f, .97f, 1.62f), rectangles, Unity.Collections.Allocator.Persistent);
                using var physicsOwner = new PhysicsWorld(1, 0, 0); var physics = physicsOwner;
                var bodies = physics.StaticBodies;
                bodies[0] = new RigidBody { Collider = rock,
                    WorldFromBody = new RigidTransform(quaternion.identity, solid.transform.position), Scale = 1 };
                physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
                using var world = new World("Level eight physical cutout");
                var enemy = world.EntityManager.CreateEntity(typeof(Enemy));
                var system = world.GetOrCreateSystem<EnemyNavigationSystem>();
                var enemies = world.Unmanaged.ResolveSystemStateRef(system).GetComponentLookup<Enemy>(true);
                var pose = LocalTransform.FromPosition(new float3(-15, 0, 2));
                Assert.That(NavigationGeometry.Anchor(ref grid.Value, pose.Position.xz, 0), Is.LessThan(0));
                Assert.False(NavigationGeometry.TryEscape(ref grid.Value, pose.Position.xz, .5f, 0, out _));
                Assert.True(NavigationPhysicalEscape.TryFind(ref grid.Value, physics.CollisionWorld, enemy, pose,
                    new PhysicsCollider { Value = body }, enemies, 0, out float2 exit));
                Assert.That(NavigationGeometry.Anchor(ref grid.Value, exit, 0), Is.GreaterThanOrEqualTo(0));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCaseSource(nameof(Scenes))]
        public void Vision004_SavedObstacleSurfacesAndPerimeterContainment(string path)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            var origin = new GameObject("Obstacle validation origin");
            try
            {
                var roots = scene.GetRootGameObjects();
                foreach (var solid in roots.SelectMany(r => r.GetComponentsInChildren<SolidObstacleAuthoring>()))
                {
                    var surfaces = solid.GetComponentsInChildren<MeshFilter>().Where(f => Visible(f)).ToArray();
                    Assert.That(surfaces.Length, Is.GreaterThan(0), solid.name);
                    using var collider = SolidObstacleGeometry.CreateCollider(solid.transform, surfaces);
                    var vertices = new List<float3>(); var triangles = new List<int3>();
                    foreach (var surface in surfaces)
                    {
                        int offset = vertices.Count;
                        vertices.AddRange(surface.sharedMesh.vertices.Select(v => (float3)solid.transform.InverseTransformPoint(surface.transform.TransformPoint(v))));
                        var indices = surface.sharedMesh.triangles;
                        for (int i = 0; i < indices.Length; i += 3) triangles.Add(new int3(indices[i], indices[i + 1], indices[i + 2]) + offset);
                    }
                    float3 min = vertices.Aggregate(new float3(float.PositiveInfinity), math.min);
                    float3 max = vertices.Aggregate(new float3(float.NegativeInfinity), math.max);
                    for (int axis = 0; axis < 2; axis++)
                    for (int height = 0; height < 3; height++)
                    for (int sample = 0; sample < 11; sample++)
                    {
                        int travel = axis == 0 ? 0 : 2, across = axis == 0 ? 2 : 0;
                        float3 start = (min + max) * .5f;
                        start.y = math.lerp(min.y, max.y, (height + .37f) / 3);
                        start[travel] = min[travel] - 1;
                        start[across] = math.lerp(min[across], max[across], (sample + .23f) / 11);
                        float3 end = start; end[travel] = max[travel] + 1;
                        float expected = float.PositiveInfinity;
                        foreach (var triangle in triangles)
                            expected = math.min(expected, TriangleHit(start, end - start, vertices[triangle.x], vertices[triangle.y], vertices[triangle.z]));
                        bool hit = collider.Value.CastRay(new RaycastInput { Start = start, End = end, Filter = CollisionFilter.Default }, out Unity.Physics.RaycastHit actual);
                        Assert.That(hit, Is.EqualTo(!float.IsPositiveInfinity(expected)), $"{solid.name}: ray {start} to {end}");
                        if (hit) Assert.That(actual.Fraction, Is.EqualTo(expected).Within(.0002f), solid.name);
                    }
                }

                var perimeters = roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>())
                    .Where(r => r.name.StartsWith("Perimeter ")).ToArray();
                foreach (var perimeter in perimeters)
                {
                    var collider = perimeter.GetComponent<UnityEngine.MeshCollider>();
                    Assert.That(collider, Is.Not.Null, perimeter.name);
                    Assert.That(collider.sharedMesh, Is.SameAs(perimeter.GetComponent<MeshFilter>().sharedMesh), perimeter.name);
                    Assert.That(collider.convex, Is.False, "Convex hulls fill visible rock cutouts.");
                    Assert.That(perimeter.GetComponents<UnityEngine.Collider>().Length, Is.EqualTo(1));
                }
                if (perimeters.Length == 0) return;
                using var boundary = SolidObstacleGeometry.CreateCollider(origin.transform, perimeters.Select(r => r.GetComponent<MeshFilter>()).ToArray());
                using var sphere = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f }, CollisionFilter.Default);
                var aabb = boundary.Value.CalculateAabb();
                var arena = roots.SelectMany(r => r.GetComponentsInChildren<ArenaAuthoring>()).Single();
                float3 center = aabb.Center; center.y = arena.GroundSurfaceHeight + .52f;
                float length = math.length(aabb.Extents) * 2 + 5;
                for (int i = 0; i < 96; i++)
                {
                    float angle = (i + .31f) * math.PI * 2 / 96;
                    var end = center + new float3(math.cos(angle), 0, math.sin(angle)) * length;
                    Assert.That(boundary.Value.CastCollider(new ColliderCastInput(sphere, center, end), out ColliderCastHit _), Is.True,
                        $"Perimeter must contain the floor-anchored player sweep at angle {angle}.");
                }
            }
            finally { Object.DestroyImmediate(origin); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static bool Visible(MeshFilter f)
        {
            var r = f.GetComponent<MeshRenderer>();
            return f.sharedMesh != null && r != null && r.enabled && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        private static float TriangleHit(float3 start, float3 direction, float3 a, float3 b, float3 c)
        {
            float3 e1 = b - a, e2 = c - a, p = math.cross(direction, e2);
            float determinant = math.dot(e1, p);
            if (math.abs(determinant) < 1e-7f) return float.PositiveInfinity;
            float inverse = 1 / determinant;
            float3 t = start - a;
            float u = math.dot(t, p) * inverse;
            float3 q = math.cross(t, e1);
            float v = math.dot(direction, q) * inverse;
            float fraction = math.dot(e2, q) * inverse;
            return u >= 0 && v >= 0 && u + v <= 1 && fraction >= 0 && fraction <= 1 ? fraction : float.PositiveInfinity;
        }

        [TestCase(1f)] [TestCase(-1f)]
        public void Vision004_SeparatedTransformedMeshesKeepVisibleGap(float mirror)
        {
            var root = new GameObject("Two part obstacle");
            try
            {
                root.transform.position = new Vector3(11, -2, 7);
                foreach (float x in new[] { -2f, 2f })
                {
                    var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    part.transform.SetParent(root.transform, false);
                    part.transform.localPosition = new Vector3(x, 1, 0);
                    part.transform.localRotation = Quaternion.Euler(0, 23, 0);
                    part.transform.localScale = new Vector3(mirror, 2, .6f);
                }
                using var collider = SolidObstacleGeometry.CreateCollider(root.transform, root.GetComponentsInChildren<MeshFilter>());
                var ray = new RaycastInput { Start = new float3(0, 1, -3), End = new float3(0, 1, 3), Filter = CollisionFilter.Default };
                Assert.That(collider.Value.CastRay(ray), Is.False, "No bounding hull across the visible gap.");
                ray.Start = new float3(2, 1, -3); ray.End = new float3(2, 1, 3);
                Assert.That(collider.Value.CastRay(ray), Is.True, "Scaled/rotated/mirrored child remains solid.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(.5f)] [TestCase(7f)]
        public void Vision004_PlayerSweepUsesFloorAndPreservesVisualRootHeight(float rootHeight)
        {
            var go = new GameObject("Player obstacle test");
            var bridge = go.AddComponent<PlayerEcsBridge>();
            PlayerBridgeRegistry.TryGetBridge(out var previous);
            PlayerBridgeRegistry.Register(bridge);
            try
            {
                using var world = new World("Grounded player collision");
                using var wall = Unity.Physics.BoxCollider.Create(new BoxGeometry { Center = new float3(0, -.25f, 0), Size = new float3(1, 1.5f, 10), Orientation = quaternion.identity });
                using var physicsOwner = new PhysicsWorld(1, 0, 0);
                var physics = physicsOwner;
                var bodies = physics.StaticBodies;
                bodies[0] = new RigidBody { Collider = wall, WorldFromBody = RigidTransform.identity, Scale = 1 };
                physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
                var em = world.EntityManager;
                em.AddComponentData(em.CreateEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
                em.AddComponentData(em.CreateEntity(), new ArenaGroundPlane { SurfaceHeight = -1 });
                var system = world.GetOrCreateSystem<PlayerObstacleCollisionSystem>();
                bridge.PublishPlayerSnapshot(new Vector3(-3, rootHeight, 0), Vector3.forward, .5f);
                bridge.PublishMovement(new Vector3(-3, rootHeight, 0), new Vector3(3, rootHeight, 0), .5f, .02f);
                system.Update(world.Unmanaged);
                Assert.That(bridge.ResolvedMovementPosition.x, Is.LessThan(-.98f));
                Assert.That(bridge.ResolvedMovementPosition.y, Is.EqualTo(rootHeight));
            }
            finally
            {
                PlayerBridgeRegistry.Unregister(bridge);
                if (previous != null) PlayerBridgeRegistry.Register(previous);
                Object.DestroyImmediate(go);
            }
        }
    }
}
