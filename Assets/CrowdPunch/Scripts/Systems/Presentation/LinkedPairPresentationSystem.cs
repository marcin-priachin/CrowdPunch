using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
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
        private CombatVfxLibrary library;
        private readonly List<Vector3> vertices = new(); private readonly List<Color> colors = new(); private readonly List<int> indices = new();
        private readonly Dictionary<Entity, (float3 Left, float3 Right)> hands = new();
        protected override void OnCreate()
        {
            mesh = new Mesh { name = "Linked pair tethers", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            RequireForUpdate<LinkedPair>();
        }
        protected override void OnUpdate() {
            Dependency.Complete();
            if (material == null) material = Resources.Load<Material>("LinkedPairTether");
            if (material == null) return;
            if (library == null) library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
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
            double time = SystemAPI.Time.ElapsedTime;
            var camera = Camera.main;
            float3 view = camera != null ? (float3)camera.transform.forward : -math.up();
            var appearances = SystemAPI.GetComponentLookup<LinkedPairAppearance>(true);
            foreach (var (pair, tuning) in SystemAPI.Query<RefRO<LinkedPair>, RefRO<LinkedPairTuning>>()) {
                var p = pair.ValueRO;
                if (!LinkedPairLaunchResolution.TryGet(EntityManager, p.A, out _, out _) || !EntityManager.IsEnabled(p.A) || !EntityManager.IsEnabled(p.B)) continue;
                float3 a = SystemAPI.GetComponent<LocalTransform>(p.A).Position, b = SystemAPI.GetComponent<LocalTransform>(p.B).Position;
                if (!hands.TryGetValue(p.A, out var ah) || !hands.TryGetValue(p.B, out var bh)) continue;
                if (!appearances.TryGetComponent(p.A, out var appearance)) continue;
                float3 centerA = a, centerB = b;
                a = FacingHand(ah.Left, ah.Right, centerB);
                b = FacingHand(bh.Left, bh.Right, centerA);
                bool launched = p.Phase == EnemyLaunchPhase.Launched;
                var accent = launched
                    ? CombatOwnershipTint.Accent(SystemAPI.GetComponent<EnemyLaunchState>(p.Leader).Owner, library)
                    : Color.clear;
                DrawStreams(a, b, view, tuning.ValueRO.MaximumLength, appearance, launched, accent, time, p.A.Index);
            }
            mesh.Clear(); if (vertices.Count == 0) return;
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, null, 0, null, ShadowCastingMode.Off, false);
        }
        protected override void OnDestroy() { if (mesh != null) Object.Destroy(mesh); }

        private void DrawStreams(float3 start, float3 end, float3 view, float maximumLength,
            in LinkedPairAppearance appearance, bool launched, Color accent, double time, int seed)
        {
            float3 span = end - start;
            float length = math.length(span);
            if (length < .01f) return;

            float3 side = math.normalizesafe(math.cross(span, math.up()), new float3(1, 0, 0));
            float3 across = math.normalizesafe(math.cross(span, view), side);
            // The streams converge at the animated hands and sag slightly when slack.
            float sag = math.saturate(1 - length / maximumLength) * math.min(.22f, length * .12f);
            float spread = math.min(appearance.StreamSpread, length * .16f);
            int segments = math.clamp((int)math.ceil(length * 4), 12, 72);
            const float fullTurn = 2 * math.PI;
            float flowSpeed = launched ? appearance.FlightFlowSpeed : appearance.GroundFlowSpeed;
            // Five primary cycles are eight secondary cycles, so wrapping keeps both waves continuous.
            float travel = (float)((time * flowSpeed) % 5) * fullTurn;
            float pairPhase = math.frac(seed * .618f) * fullTurn;
            float waveNumber = fullTurn / appearance.StreamWavelength;
            Color baseColor = CombatOwnershipTint.Blend(ToColor(launched ? appearance.FlightCore : appearance.GroundCore), accent, .85f);
            Color highlight = CombatOwnershipTint.Blend(ToColor(launched ? appearance.FlightHighlight : appearance.GroundHighlight), accent, .5f);
            Color glow = CombatOwnershipTint.Blend(ToColor(launched ? appearance.FlightGlow : appearance.GroundGlow), accent, .9f);
            Color glowEdge = new(glow.r, glow.g, glow.b, 0);
            float glowShoulder = math.lerp(1, appearance.GlowWidth, .22f);

            const int verticesPerSample = 6;
            for (int strand = 0; strand < appearance.StreamCount; strand++)
            {
                bool main = strand == appearance.StreamCount - 1;
                float offset = strand * (fullTurn / appearance.StreamCount) + pairPhase;
                float strandSpread = spread * (main ? .46f : 1);
                float width = math.min(appearance.StreamWidth * (main ? .5f : .325f), length * .12f);
                int first = vertices.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float distance = t * length;
                    float taper = math.smoothstep(0, .85f, math.min(distance, length - distance));
                    float wavePhase = distance * waveNumber;
                    float wave = math.sin(wavePhase - travel + offset)
                        + .32f * math.sin(wavePhase * 2.07f - travel * 1.6f + offset * 1.5f);
                    float3 center = TetherPoint(start, span, sag, t)
                        + across * (wave * strandSpread * taper);

                    float pulse = .5f + .5f * math.sin(distance * 1.15f - travel * 2 + offset);
                    Color core = Color.Lerp(baseColor, highlight, (main ? .62f : .28f) + .36f * pulse);
                    float3 halfWidth = across * (width * math.lerp(.55f, 1, taper));
                    AddVertex(center - halfWidth * appearance.GlowWidth, glowEdge);
                    AddVertex(center - halfWidth * glowShoulder, glow);
                    AddVertex(center - halfWidth, core);
                    AddVertex(center + halfWidth, core);
                    AddVertex(center + halfWidth * glowShoulder, glow);
                    AddVertex(center + halfWidth * appearance.GlowWidth, glowEdge);

                    if (i == 0) continue;
                    int row = first + i * verticesPerSample;
                    for (int edge = 0; edge < verticesPerSample - 1; edge++)
                        AddQuad(row - verticesPerSample + edge, row + edge);
                }
            }

            if (appearance.SparkRate > 0)
                DrawSparks(start, span, across, sag, time, seed, appearance);
        }

        private void DrawSparks(float3 start, float3 span, float3 across, float sag, double time, int seed,
            in LinkedPairAppearance appearance)
        {
            double emission = time * appearance.SparkRate + math.frac(seed * .618f);
            int tick = (int)math.floor(emission);
            float ageStep = 1 / (appearance.SparkRate * appearance.SparkLifetime);
            float3 forward = math.normalize(span);
            Color sparkColor = ToColor(appearance.SparkColor);
            Color transparent = new(sparkColor.r, sparkColor.g, sparkColor.b, 0);

            // Rebuild the sparks that are still alive from their emission ticks.
            for (float age = (float)(emission - tick) * ageStep; age < 1; age += ageStep, tick--)
            {
                var random = new Unity.Mathematics.Random(math.hash(new int2(seed, tick)) | 1u);
                float t = random.NextFloat(.08f, .92f);
                float side = random.NextBool() ? -1 : 1;
                float drift = random.NextFloat(-.7f, .7f);
                float3 direction = math.normalize(across * side + forward * drift);
                float3 normal = math.normalize(across * drift - forward * side);
                float distance = appearance.SparkTravel * random.NextFloat(.65f, 1.35f) * age * (2 - age);
                float3 center = TetherPoint(start, span, sag, t) + direction * distance;

                float size = appearance.SparkLength * random.NextFloat(.65f, 1.3f) * math.lerp(1, .4f, age);
                float3 halfLength = direction * (size * .5f);
                float3 halfWidth = normal * (size * .12f);
                Color color = sparkColor;
                color.a *= 1 - math.smoothstep(.12f, 1, age);

                int first = vertices.Count;
                AddVertex(center - halfLength, transparent);
                AddVertex(center - halfWidth, color);
                AddVertex(center + halfWidth, color);
                AddVertex(center + halfLength, color);
                AddQuad(first, first + 2);
            }
        }

        private static float3 TetherPoint(float3 start, float3 span, float sag, float t) =>
            start + span * t - math.up() * (sag * math.sin(t * math.PI));

        private static Color ToColor(float4 rgba) => new(rgba.x, rgba.y, rgba.z, rgba.w);

        private void AddVertex(float3 position, Color color)
        {
            vertices.Add(position);
            colors.Add(color);
        }

        private void AddQuad(int first, int second)
        {
            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(second);
            indices.Add(second);
            indices.Add(first + 1);
            indices.Add(second + 1);
        }

        public static float3 FacingHand(float3 left, float3 right, float3 partner) =>
            math.distancesq(left.xz, partner.xz) <= math.distancesq(right.xz, partner.xz) ? left : right;
    }
}
