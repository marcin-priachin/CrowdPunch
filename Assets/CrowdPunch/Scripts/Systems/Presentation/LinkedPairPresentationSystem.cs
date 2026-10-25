using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Deformations;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial class LinkedPairPresentationSystem : SystemBase
    {
        private Mesh mesh; private Material material;
        private readonly List<Vector3> vertices = new(); private readonly List<Color> colors = new(); private readonly List<int> indices = new();
        private readonly Dictionary<Entity, (float3 Left, float3 Right)> hands = new();
        protected override void OnCreate() { mesh = new Mesh { name = "Linked pair tethers", indexFormat = IndexFormat.UInt32 }; RequireForUpdate<LinkedPair>(); }
        protected override void OnUpdate() {
            Dependency.Complete();
            if (material == null) material = Resources.Load<Material>("LinkedPairTether");
            if (material == null) return;
            vertices.Clear(); colors.Clear(); indices.Clear();
            hands.Clear();
            foreach (var (anchors, skin) in SystemAPI.Query<RefRO<LinkedPairHandAnchors>, DynamicBuffer<SkinMatrix>>())
            {
                var h = anchors.ValueRO;
                if (!SystemAPI.HasComponent<LocalToWorld>(h.Root) || h.LeftBone >= skin.Length || h.RightBone >= skin.Length) continue;
                var root = SystemAPI.GetComponent<LocalToWorld>(h.Root).Value;
                hands[h.Owner] = (
                    math.transform(root, math.mul(skin[h.LeftBone].Value, new float4(h.LeftBindPosition, 1))),
                    math.transform(root, math.mul(skin[h.RightBone].Value, new float4(h.RightBindPosition, 1))));
            }
            foreach (var (pair, tuning) in SystemAPI.Query<RefRO<LinkedPair>, RefRO<LinkedPairTuning>>()) {
                var p = pair.ValueRO;
                if (!LinkedPairLaunchResolution.TryGet(EntityManager, p.A, out _, out _) || !EntityManager.IsEnabled(p.A) || !EntityManager.IsEnabled(p.B)) continue;
                float3 a = SystemAPI.GetComponent<LocalTransform>(p.A).Position, b = SystemAPI.GetComponent<LocalTransform>(p.B).Position;
                if (!hands.TryGetValue(p.A, out var ah) || !hands.TryGetValue(p.B, out var bh)) continue;
                float3 centerA = a, centerB = b;
                a = FacingHand(ah.Left, ah.Right, centerB);
                b = FacingHand(bh.Left, bh.Right, centerA);
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

        public static float3 FacingHand(float3 left, float3 right, float3 partner) =>
            math.distancesq(left.xz, partner.xz) <= math.distancesq(right.xz, partner.xz) ? left : right;
    }
}
