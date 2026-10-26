using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
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
        private readonly float[] reaches = new float[81];
        private readonly float2[] directions = new float2[81];
        private CombatSpriteMesh tongues, embers;
        private CombatVfxLibrary library;
        protected override void OnCreate()
        {
            mesh = new Mesh { name = "Attached dragon flame cones", indexFormat = IndexFormat.UInt32 };
            tongues = new CombatSpriteMesh("Cover-clipped dragon flame tongues");
            embers = new CombatSpriteMesh("Dragon mouth and embers");
            RequireForUpdate<DragonTuning>(); RequireForUpdate<PhysicsWorldSingleton>();
        }
        protected override void OnUpdate()
        {
            if (material == null) material = Resources.Load<Material>("DragonFlame");
            if (material == null) return;
            if (library == null) library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            tongues.Clear(); embers.Clear();
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
                float2 right = new float2(cone.Forward.y, -cone.Forward.x);
                const int steps = 80;
                int center = vertices.Count; Add(new float3(cone.Mouth.x, floor + .035f, cone.Mouth.z), tint);
                int previousBoundary = -1;
                for (int i = 0; i <= steps; i++)
                {
                    float angle = math.lerp(-half, half, i / (float)steps);
                    float2 direction = cone.Forward * math.cos(angle) + right * math.sin(angle);
                    float reach = DragonFlameGeometry.Reach(world, enemies, source, cone.Mouth.xz, direction, cone.Profile.Length, floor + .25f);
                    reaches[i] = math.max(0,reach-.025f); directions[i] = direction;
                    float2 point = cone.Mouth.xz + direction * math.max(0, reach - .01f);
                    int boundary = vertices.Count;
                    Add(new float3(point.x, floor + .035f, point.y), tint);
                    if (previousBoundary >= 0) Triangle(center, previousBoundary, boundary);
                    previousBoundary = boundary;
                }
                if (active && library != null)
                {
                    for (int i=8;i<=steps;i+=8)
                    {
                        // Use the shortest of every sampled ray in this angular cell. No free-flying emitter can leak through cover.
                        float reach = reaches[i-8];
                        for (int k=i-7;k<=i;k++) reach=math.min(reach,reaches[k]);
                        if (reach < .15f) continue;
                        for (int layer=0;layer<5;layer++)
                        {
                            float phase = math.frac(now*1.6f + math.frac(math.sin(i*12.9898f)*43758.5453f) + layer/5f);
                            float distance = reach * (.08f + phase*.88f);
                            float2 a = cone.Mouth.xz + directions[i-8]*distance;
                            float2 b = cone.Mouth.xz + directions[i]*distance;
                            float bottom = math.lerp(cone.Mouth.y-.15f,floor+.04f,phase);
                            float height = (1.1f + math.sin(i*.9f+now*5)*.18f) * (1-phase*.45f);
                            Vector3 left = new Vector3(a.x,bottom,a.y), r = new Vector3(b.x,bottom,b.y);
                            float tipDistance=math.min(reach,distance+1.2f);
                            float2 tipLeft=cone.Mouth.xz+directions[i-8]*tipDistance;
                            float2 tipRight=cone.Mouth.xz+directions[i]*tipDistance;
                            tongues.Quad(left,r,new Vector3(tipRight.x,bottom+height,tipRight.y),new Vector3(tipLeft.x,bottom+height,tipLeft.y),
                                Color.Lerp(new Color(1,.76f,.16f,.95f),new Color(1,.23f,.025f,.4f),phase));
                            if (layer == 0 && camera != null && i%8==0)
                            {
                                // Tiny vertical diamonds stay inside the same clipped cell, including near walls.
                                var centre = (left+r)*.5f + Vector3.up*(height+.08f);
                                float size = math.min(.035f,Vector3.Distance(left,r)*.2f);
                                embers.Billboard(centre,(r-left).normalized,Vector3.up,size,
                                    new Color(1,.78f,.19f,.65f*(1-phase)));
                            }
                        }
                    }
                }
                if (camera != null)
                {
                    float size = active ? .19f : .05f+.11f*math.saturate(1-flame.ValueRO.Remaining/math.max(.01f,tuning.ValueRO.WarningDuration));
                    embers.Billboard(cone.Mouth,camera.transform.right,camera.transform.up,size,new Color(1,.84f,.22f,.85f));
                }
            }
            mesh.Clear(); if (vertices.Count == 0) return;
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, null, 0, null, ShadowCastingMode.Off, false);
            if (library != null) { tongues.Draw(library.DragonFlame); embers.Draw(library.Pollen); }
        }
        private void Add(float3 p, Color c) { vertices.Add(p); colors.Add(c); }
        private void Triangle(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
        protected override void OnDestroy() { if (mesh != null) Object.Destroy(mesh); tongues.Dispose(); embers.Dispose(); }
    }
}
