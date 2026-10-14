using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class LinkedPairPresentationSystem : SystemBase
    {
        private Mesh mesh; private Material material;
        private readonly List<Vector3> vertices = new(); private readonly List<Color> colors = new(); private readonly List<int> indices = new();
        protected override void OnCreate() { mesh = new Mesh { name = "Linked pair tethers", indexFormat = IndexFormat.UInt32 }; RequireForUpdate<LinkedPair>(); }
        protected override void OnUpdate() {
            Dependency.Complete();
            if (material == null) material = Resources.Load<Material>("LinkedPairTether");
            if (material == null) return;
            vertices.Clear(); colors.Clear(); indices.Clear();
            foreach (var (pair, tuning) in SystemAPI.Query<RefRO<LinkedPair>, RefRO<LinkedPairTuning>>()) {
                var p = pair.ValueRO;
                if (!LinkedPairLaunchResolution.TryGet(EntityManager, p.A, out _, out _) || !EntityManager.IsEnabled(p.A) || !EntityManager.IsEnabled(p.B)) continue;
                float3 a = SystemAPI.GetComponent<LocalTransform>(p.A).Position, b = SystemAPI.GetComponent<LocalTransform>(p.B).Position;
                float3 side = math.normalizesafe(math.cross(b - a, math.up()), new float3(1, 0, 0)) * tuning.ValueRO.Width * .5f;
                Color color = p.Phase == EnemyLaunchPhase.Launched ? new Color(1, .35f, .12f, .95f) : new Color(.35f, 1, .85f, .9f);
                int start = vertices.Count;
                // A small visual sag communicates slack. Damage uses the straight planar span.
                const int segments = 12;
                for (int i = 0; i <= segments; i++) {
                    float t = i / (float)segments; float3 point = math.lerp(a, b, t);
                    point.y -= math.sin(t * math.PI) * math.saturate(1 - math.distance(a, b) / tuning.ValueRO.MaximumLength) * .25f;
                    vertices.Add(point - side); vertices.Add(point + side); colors.Add(color); colors.Add(color);
                    if (i > 0) { int k = start + i * 2; indices.Add(k - 2); indices.Add(k - 1); indices.Add(k); indices.Add(k); indices.Add(k - 1); indices.Add(k + 1); }
                }
            }
            mesh.Clear(); if (vertices.Count == 0) return;
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, null, 0, null, ShadowCastingMode.Off, false);
        }
        protected override void OnDestroy() { if (mesh != null) Object.Destroy(mesh); }
    }
}
