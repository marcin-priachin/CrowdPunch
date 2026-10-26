using CrowdPunch.Components;
using CrowdPunch.Configuration;
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
        private CombatVfxLibrary library;
        private CombatSpriteMesh clouds, pollen;
        protected override void OnCreate()
        {
            properties = new MaterialPropertyBlock();
            clouds = new CombatSpriteMesh("Plant spore clouds");
            pollen = new CombatSpriteMesh("Plant airborne pollen");
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
            if (library == null) library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            clouds.Clear(); pollen.Clear();
            var camera = Camera.main;
            float now = (float)SystemAPI.Time.ElapsedTime;
            int decorated = 0;
            foreach (var zone in SystemAPI.Query<RefRO<WizardZone>>())
            {
                float pulse = .85f + .15f * math.sin((float)SystemAPI.Time.ElapsedTime * 9);
                bool active = zone.ValueRO.Active != 0;
                properties.SetColor("_Color", active ? new Color(.58f,.7f,.12f,.32f * pulse) : new Color(.64f,.76f,.24f,.16f * pulse));
                var position = (Vector3)zone.ValueRO.Position;
                properties.SetFloat("_Ring", active ? .85f : .5f);
                Graphics.DrawMesh(disc, Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * zone.ValueRO.Radius),
                    material, 0, null, 0, properties, ShadowCastingMode.Off, false);
                // All zones retain the full boundary; only decorative cloud density is capped.
                if (library == null || camera == null || decorated++ >= library.MaximumCloudZones) continue;
                float radius = zone.ValueRO.Radius;
                Vector4 clip = new Vector4(position.x,position.z,radius,1);
                float seed = position.x * .31f + position.z * .71f;
                float opacity = active ? 1 : .22f;
                for (int i=0;i<library.CloudsPerZone;i++)
                {
                    float angle = i * 2.399963f + now * .12f + seed;
                    float distance = radius * .72f * math.sqrt((i+.5f)/library.CloudsPerZone);
                    float cycle = math.frac(now * .45f + i * .618f);
                    Vector3 centre = position + new Vector3(math.cos(angle)*distance,.35f+cycle*.8f,math.sin(angle)*distance);
                    float size = math.min(.95f,radius*.3f) * (.75f + cycle*.35f);
                    float fade = math.sin(cycle*math.PI);
                    clouds.Billboard(centre,camera.transform.right,camera.transform.up,size,
                        new Color(.53f,.64f,.18f,.42f*opacity*fade),clip);
                }
                for (int i=0;i<library.SporesPerZone;i++)
                {
                    float cycle = math.frac(now*.38f + i*.381966f);
                    float angle = i*2.399963f + seed + now*.2f;
                    float distance = radius*.88f*math.sqrt((i+.5f)/library.SporesPerZone);
                    Vector3 centre = position + new Vector3(math.cos(angle)*distance,.15f+cycle*1.25f,math.sin(angle)*distance);
                    pollen.Billboard(centre,camera.transform.right,camera.transform.up,.04f+.02f*math.sin(i),
                        new Color(.94f,.88f,.35f,opacity*math.sin(cycle*math.PI)*.8f),clip);
                }
            }
            if (library != null) { clouds.Draw(library.SporeCloud); pollen.Draw(library.Pollen); }
        }
        protected override void OnDestroy() { if (disc != null) Object.Destroy(disc); clouds.Dispose(); pollen.Dispose(); }
    }
}
