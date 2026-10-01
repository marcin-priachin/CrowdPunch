using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    // Rendering only. Geometry is shared; no GameObject, Animator or collider is created per zone.
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class WizardZonePresentationSystem : SystemBase
    {
        private Mesh disc;
        private Material material;
        private MaterialPropertyBlock properties;
        protected override void OnCreate()
        {
            properties = new MaterialPropertyBlock();
            const int segments = 96;
            var vertices = new Vector3[segments + 1]; var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 3]; uv[0] = new Vector2(.5f, .5f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                uv[i + 1] = new Vector2(vertices[i + 1].x, vertices[i + 1].z) * .5f + Vector2.one * .5f;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = (i + 1) % segments + 1; triangles[i * 3 + 2] = i + 1;
            }
            disc = new Mesh { name = "Wizard radius disc", vertices = vertices, uv = uv, triangles = triangles };
            disc.RecalculateBounds();
        }
        protected override void OnUpdate()
        {
            if (material == null) material = Resources.Load<Material>("WizardZone");
            if (material == null) return;
            foreach (var zone in SystemAPI.Query<RefRO<WizardZone>>())
            {
                float pulse = .85f + .15f * math.sin((float)SystemAPI.Time.ElapsedTime * 9);
                bool active = zone.ValueRO.Active != 0;
                properties.SetColor("_Color", new Color(.55f, .08f, 1f, (active ? .42f : .16f) * pulse));
                var position = (Vector3)zone.ValueRO.Position;
                properties.SetFloat("_Ring", active ? .85f : .5f);
                Graphics.DrawMesh(disc, Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * zone.ValueRO.Settings.Radius),
                    material, 0, null, 0, properties, ShadowCastingMode.Off, false);
            }
        }
        protected override void OnDestroy() { if (disc != null) Object.Destroy(disc); }
    }
}
