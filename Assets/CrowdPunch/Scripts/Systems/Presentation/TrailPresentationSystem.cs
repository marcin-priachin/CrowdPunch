using System.Collections.Generic;
using CrowdPunch.Components;
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
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        protected override void OnCreate() => mesh = new Mesh { name = "Ground trail capsules", indexFormat = IndexFormat.UInt32 };

        protected override void OnUpdate()
        {
            if (material == null) material = Resources.Load<Material>("EnemyTrail");
            if (material == null) return;
            vertices.Clear(); colors.Clear(); triangles.Clear();
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var sectionRef in SystemAPI.Query<RefRO<TrailSection>>())
            {
                var section = sectionRef.ValueRO;
                if (now >= section.ExpiresAt) continue;
                var tint = section.Color;
                tint.w *= math.saturate((float)((section.ExpiresAt - now) / math.max(.01, (section.ExpiresAt - section.CreatedAt) * .35)));
                Color color = new Color(tint.x, tint.y, tint.z, tint.w);
                float radius = section.Width * .5f;
                float2 direction = math.normalizesafe(section.End.xz - section.Start.xz, new float2(1, 0));
                float angle = math.atan2(direction.y, direction.x);
                int center = vertices.Count;
                Add((section.Start + section.End) * .5f, color);
                const int capSteps = 12;
                for (int cap = 0; cap < 2; cap++)
                for (int step = 0; step <= capSteps; step++)
                {
                    float a = angle - math.PI * .5f + (cap * capSteps + step) * math.PI / capSteps;
                    float3 point = cap == 0 ? section.End : section.Start;
                    Add(point + new float3(math.cos(a) * radius, 0, math.sin(a) * radius), color);
                }
                int count = (capSteps + 1) * 2;
                for (int i = 0; i < count; i++)
                { triangles.Add(center); triangles.Add(center + 1 + (i + 1) % count); triangles.Add(center + 1 + i); }
            }
            mesh.Clear();
            if (vertices.Count == 0) return;
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, null, 0, null, ShadowCastingMode.Off, false);
        }
        private void Add(float3 point, Color color) { vertices.Add(point); colors.Add(color); }
        protected override void OnDestroy() { if (mesh != null) Object.Destroy(mesh); }
    }
}
