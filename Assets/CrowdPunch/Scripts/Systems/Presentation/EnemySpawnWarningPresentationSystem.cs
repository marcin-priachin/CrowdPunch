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
    public partial class EnemySpawnWarningPresentationSystem : SystemBase
    {
        private const int Segments = 32;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<float4> locations = new List<float4>();
        private Mesh rings;
        private Material material;
        private MaterialPropertyBlock properties;

        protected override void OnCreate()
        {
            rings = new Mesh { name = "Enemy spawn warning rings", indexFormat = IndexFormat.UInt32 };
            rings.MarkDynamic();
            properties = new MaterialPropertyBlock();
        }

        protected override void OnUpdate()
        {
            if (material == null) material = Resources.Load<Material>("WizardZone");
            if (material == null) return;
            float height = SystemAPI.HasSingleton<ArenaGroundPlane>()
                ? SystemAPI.GetSingleton<ArenaGroundPlane>().SurfaceHeight : 0f;
            bool changed = false;
            int count = 0;
            foreach (var warning in SystemAPI.Query<RefRO<EnemySpawnWarning>>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities))
            {
                var w = warning.ValueRO;
                var location = new float4(w.Position.x, height + .04f, w.Position.z, w.Radius);
                if (count >= locations.Count) { locations.Add(location); changed = true; }
                else if (math.any(locations[count] != location)) { locations[count] = location; changed = true; }
                count++;
            }
            if (count < locations.Count) { locations.RemoveRange(count, locations.Count - count); changed = true; }
            if (count == 0) return;
            if (changed) RebuildRings();
            properties.SetColor("_Color", new Color(1f, .65f, .12f,
                .65f + .25f * math.sin((float)SystemAPI.Time.ElapsedTime * 12f)));
            properties.SetFloat("_Ring", 0f);
            // One shared mesh/draw for the whole crowd; no object or material per reservation.
            Graphics.DrawMesh(rings, Matrix4x4.identity, material, 0, null, 0, properties,
                ShadowCastingMode.Off, false);
        }

        private void RebuildRings()
        {
            vertices.Clear(); uv.Clear(); triangles.Clear();
            foreach (var location in locations)
            {
                int start = vertices.Count;
                for (int i = 0; i < Segments; i++)
                {
                    float angle = i * math.PI * 2 / Segments;
                    var direction = new Vector3(math.cos(angle), 0, math.sin(angle));
                    var center = new Vector3(location.x, location.y, location.z);
                    vertices.Add(center + direction * location.w);
                    vertices.Add(center + direction * (location.w - .12f));
                    uv.Add(Vector2.one * .5f); uv.Add(Vector2.one * .5f);
                    int outer = start + i * 2, next = start + (i + 1) % Segments * 2;
                    triangles.Add(outer); triangles.Add(next); triangles.Add(outer + 1);
                    triangles.Add(next); triangles.Add(next + 1); triangles.Add(outer + 1);
                }
            }
            rings.Clear();
            if (vertices.Count == 0) return;
            rings.SetVertices(vertices); rings.SetUVs(0, uv); rings.SetTriangles(triangles, 0);
            rings.RecalculateBounds();
        }

        protected override void OnDestroy()
        {
            if (rings == null) return;
            if (Application.isPlaying) Object.Destroy(rings);
            else Object.DestroyImmediate(rings);
        }
    }
}
