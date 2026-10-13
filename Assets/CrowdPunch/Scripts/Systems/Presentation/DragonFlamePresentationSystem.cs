using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Material = UnityEngine.Material;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class DragonFlamePresentationSystem : SystemBase
    {
        private Mesh mesh;
        private Material material;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        protected override void OnCreate()
        {
            mesh = new Mesh { name = "Attached dragon flame cones", indexFormat = IndexFormat.UInt32 };
            RequireForUpdate<DragonTuning>(); RequireForUpdate<PhysicsWorldSingleton>();
        }
        protected override void OnUpdate()
        {
            if (material == null) material = Resources.Load<Material>("DragonFlame");
            if (material == null) return;
            Dependency.Complete(); vertices.Clear(); colors.Clear(); triangles.Clear();
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            foreach (var (tuning, flame, launch, pose, life, source) in
                SystemAPI.Query<RefRO<DragonTuning>, RefRO<DragonFlameState>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>, RefRO<EnemyLifetime>>()
                    .WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (!DragonFlameGeometry.TryCone(source, pose.ValueRO, tuning.ValueRO, flame.ValueRO, launch.ValueRO, life.ValueRO.Generation, out var cone)) continue;
                float floor = cone.Tuning.GroundY;
                bool active = cone.Active != 0;
                Color tint = active ? new Color(1, .23f, .015f, .38f) : new Color(1, .68f, .1f, .2f);
                float half = math.radians(cone.Profile.AngleDegrees * .5f);
                float2 right = new float2(cone.Forward.y, -cone.Forward.x);
                const int steps = 80;
                int center = vertices.Count; Add(new float3(cone.Mouth.x, floor + .035f, cone.Mouth.z), tint);
                int previousBoundary = -1;
                for (int i = 0; i <= steps; i++)
                {
                    float angle = math.lerp(-half, half, i / (float)steps);
                    float2 direction = cone.Forward * math.cos(angle) + right * math.sin(angle);
                    float reach = DragonFlameGeometry.Reach(world, enemies, source, cone.Mouth.xz, direction, cone.Profile.Length, floor + .25f);
                    float2 point = cone.Mouth.xz + direction * math.max(0, reach - .01f);
                    int boundary = vertices.Count;
                    Add(new float3(point.x, floor + .035f, point.y), tint);
                    if (previousBoundary >= 0) Triangle(center, previousBoundary, boundary);
                    previousBoundary = boundary;
                    // A raised flame fan remains attached to the mouth; each ray uses the same cover cut as the warning.
                    if (active && i > 0 && i % 4 == 0)
                    {
                        int start = vertices.Count;
                        float previous = math.lerp(-half, half, (i - 3) / (float)steps);
                        float2 otherDirection = cone.Forward * math.cos(previous) + right * math.sin(previous);
                        float otherReach = DragonFlameGeometry.Reach(world, enemies, source, cone.Mouth.xz, otherDirection, cone.Profile.Length, floor + .25f);
                        float2 other = cone.Mouth.xz + otherDirection * math.max(0, otherReach - .01f);
                        float flicker = .3f + .2f * math.sin((float)SystemAPI.Time.ElapsedTime * 17 + i);
                        Add(cone.Mouth, new Color(1, .9f, .2f, .75f));
                        Add(new float3(point.x, floor + flicker, point.y), new Color(1, .2f, .01f, .08f));
                        Add(new float3(other.x, floor + flicker, other.y), new Color(1, .4f, .01f, .18f));
                        Triangle(start, start + 1, start + 2);
                    }
                }
                // The compact mouth flare grows during wind-up and disappears with lifecycle interruption.
                if (!active)
                {
                    float size = .06f + .14f * math.saturate(1 - flame.ValueRO.Remaining / tuning.ValueRO.WarningDuration);
                    int start = vertices.Count;
                    Add(cone.Mouth + new float3(-size, -size, 0), new Color(1, .8f, .2f, .8f));
                    Add(cone.Mouth + new float3(size, -size, 0), new Color(1, .8f, .2f, .8f));
                    Add(cone.Mouth + new float3(0, size, 0), new Color(1, .8f, .2f, .8f));
                    Triangle(start, start + 1, start + 2);
                }
            }
            mesh.Clear(); if (vertices.Count == 0) return;
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, null, 0, null, ShadowCastingMode.Off, false);
        }
        private void Add(float3 p, Color c) { vertices.Add(p); colors.Add(c); }
        private void Triangle(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
        protected override void OnDestroy() { if (mesh != null) Object.Destroy(mesh); }
    }
}
