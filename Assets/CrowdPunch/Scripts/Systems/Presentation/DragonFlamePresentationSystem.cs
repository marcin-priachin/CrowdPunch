using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
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
        private Material material, streamMaterial;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private const int Steps = DragonFlameStreamMesh.AngularSteps * 2;
        private NativeArray<float> reaches;
        private NativeArray<float2> directions;
        private DragonFlameStreamMesh stream;
        private CombatSpriteMesh embers;
        private CombatVfxLibrary library;
        protected override void OnCreate()
        {
            mesh = new Mesh { name = "Attached dragon flame cones", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            reaches = new NativeArray<float>(Steps + 1, Allocator.Persistent);
            directions = new NativeArray<float2>(Steps + 1, Allocator.Persistent);
            stream = new DragonFlameStreamMesh();
            embers = new CombatSpriteMesh("Dragon mouth and embers");
            RequireForUpdate<DragonTuning>(); RequireForUpdate<PhysicsWorldSingleton>();
        }
        protected override void OnUpdate()
        {
            if (material == null) material = Resources.Load<Material>("DragonFlame");
            if (material == null) return;
            if (streamMaterial == null) streamMaterial = Resources.Load<Material>("DragonFlameStream");
            if (library == null) library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            stream.Clear(); embers.Clear();
            var camera = Camera.main;
            float now = (float)SystemAPI.Time.ElapsedTime;
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
                Color tint = active ? new Color(1, .3f, .035f, .13f) : new Color(1, .68f, .1f, .2f);
                float half = math.radians(cone.Profile.AngleDegrees * .5f);
                new DragonFlameCoverSamplingJob {
                    World = world, Enemies = enemies, Cone = cone, Reaches = reaches, Directions = directions
                }.Run();
                int center = vertices.Count; Add(new float3(cone.Mouth.x, floor + .035f, cone.Mouth.z), tint);
                for (int i = 0; i < Steps; i += 2)
                {
                    float reach = math.min(reaches[i], math.min(reaches[i + 1], reaches[i + 2]));
                    float2 left = cone.Mouth.xz + directions[i] * reach;
                    float2 rightPoint = cone.Mouth.xz + directions[i + 2] * reach;
                    int boundary = vertices.Count;
                    Add(new float3(left.x, floor + .035f, left.y), tint);
                    Add(new float3(rightPoint.x, floor + .035f, rightPoint.y), tint);
                    Triangle(center, boundary, boundary + 1);
                    if (active && i % 20 == 0 && reach > .3f)
                    {
                        float phase = math.frac(now * 1.6f + i * .13f);
                        float distance = reach * (.15f + phase * .75f);
                        float2 point = cone.Mouth.xz + directions[i + 1] * distance;
                        float size = math.min(.035f, distance * math.sin(half / Steps) * .7f);
                        var across = new Vector3(directions[i + 1].y, 0, -directions[i + 1].x);
                        embers.Billboard(new Vector3(point.x, math.lerp(cone.Mouth.y, floor + 1.5f, phase), point.y),
                            across, Vector3.up, size, new Color(1, .78f, .19f, .65f * (1 - phase)));
                    }
                }
                if (active) stream.Append(cone, reaches, directions, now);
                if (camera != null)
                {
                    float size = active ? .19f : .05f+.11f*math.saturate(1-flame.ValueRO.Remaining/math.max(.01f,tuning.ValueRO.WarningDuration));
                    for (int i = 0; i <= Steps; i++) size = math.min(size, reaches[i] * .45f);
                    embers.Billboard(cone.Mouth,camera.transform.right,camera.transform.up,size,new Color(1,.84f,.22f,.85f));
                }
            }
            mesh.Clear(); if (vertices.Count == 0) return;
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, null, 0, null, ShadowCastingMode.Off, false);
            stream.Draw(streamMaterial, library);
            if (library != null) embers.Draw(library.Pollen);
        }
        private void Add(float3 p, Color c) { vertices.Add(p); colors.Add(c); }
        private void Triangle(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
        protected override void OnDestroy()
        {
            if (mesh != null)
            {
                if (Application.isPlaying) Object.Destroy(mesh);
                else Object.DestroyImmediate(mesh);
            }
            stream.Dispose(); embers.Dispose();
            reaches.Dispose(); directions.Dispose();
        }
    }
}
