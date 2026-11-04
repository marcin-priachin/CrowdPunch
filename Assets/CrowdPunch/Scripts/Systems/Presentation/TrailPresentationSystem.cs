using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class TrailPresentationSystem : SystemBase
    {
        private Mesh mesh;
        private Material material;
        private CombatVfxLibrary library;
        private MaterialPropertyBlock properties;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<Vector4> capsules = new List<Vector4>();
        private readonly List<Vector2> ownership = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        protected override void OnCreate()
        {
            mesh = new Mesh { name = "Ground trail capsules", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            properties = new MaterialPropertyBlock();
            properties.SetFloat("_UseOwnershipTint", 1);
        }

        protected override void OnUpdate()
        {
            if (material == null) material = Resources.Load<Material>("EnemyTrail");
            if (material == null) return;
            vertices.Clear();
            colors.Clear();
            capsules.Clear();
            ownership.Clear();
            triangles.Clear();
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var sectionRef in SystemAPI.Query<RefRO<TrailSection>>())
            {
                var section = sectionRef.ValueRO;
                if (section.Width <= 0f) continue;

                // Fixed-step expiry owns removal. The frame clock only controls shading.
                float freshness = math.saturate((float)((section.ExpiresAt - now) /
                    math.max(.01, (section.ExpiresAt - section.CreatedAt) * .35)));
                var tint = section.Color;
                float freshOpacity = math.lerp(.78f, .98f, math.saturate(tint.w));
                var color = new Color(tint.x, tint.y, tint.z, math.lerp(.55f, freshOpacity, freshness));
                var visualOwner = section.Launched != 0 ? section.Owner : EnemyLaunchOwner.Enemy;
                float accent = CombatOwnershipTint.ShaderIndex(visualOwner);
                AddCapsule(section, color, new Vector2(accent, 0));
            }
            mesh.Clear();
            if (vertices.Count == 0) return;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, capsules);
            mesh.SetUVs(1, ownership);
            mesh.SetTriangles(triangles, 0);
            if (library == null) library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            CombatOwnershipTint.SetShaderPalette(properties, library);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, null, 0, properties, ShadowCastingMode.Off, false);
        }

        private void AddCapsule(in TrailSection section, Color color, Vector2 accent)
        {
            float2 delta = section.End.xz - section.Start.xz;
            float length = math.length(delta);
            float2 direction = math.normalizesafe(delta, new float2(1, 0));
            float radius = section.Width * .5f;
            float3 forward = new float3(direction.x, 0f, direction.y) * radius;
            float3 side = new float3(-forward.z, 0f, forward.x);

            int first = vertices.Count;
            AddRow(section.Start - forward, side, color, new Vector4(-radius, 0f, length, radius), accent);
            AddRow(section.Start, side, color, new Vector4(0f, 0f, length, radius), accent);
            AddRow(section.End, side, color, new Vector4(length, 0f, length, radius), accent);
            AddRow(section.End + forward, side, color, new Vector4(length + radius, 0f, length, radius), accent);
            for (int i = first; i < first + 6; i += 2)
            {
                triangles.Add(i);
                triangles.Add(i + 1);
                triangles.Add(i + 3);
                triangles.Add(i);
                triangles.Add(i + 3);
                triangles.Add(i + 2);
            }
        }

        private void AddRow(float3 center, float3 side, Color color, Vector4 capsule, Vector2 accent)
        {
            vertices.Add(center - side);
            vertices.Add(center + side);
            colors.Add(color);
            colors.Add(color);
            ownership.Add(accent);
            ownership.Add(accent);
            capsule.y = -capsule.w;
            capsules.Add(capsule);
            capsule.y = capsule.w;
            capsules.Add(capsule);
        }

        protected override void OnDestroy() { if (mesh != null) Object.Destroy(mesh); }
    }
}
