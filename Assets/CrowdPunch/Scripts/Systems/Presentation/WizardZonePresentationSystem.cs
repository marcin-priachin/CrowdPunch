using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.Groups;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    // Rendering only. Geometry is shared; no GameObject, Animator or collider is created per zone.
    [UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial class WizardZonePresentationSystem : SystemBase
    {
        private const float PollenReferenceRadius = 6f;
        // The default 32 decorated zones can each reach this limit within the shared quad batch.
        private const int MaximumSporesPerZone = 128;

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
            double now = SystemAPI.Time.ElapsedTime;
            var warningStyle = library != null ? library.WizardWarningIndicator : WizardIndicatorStyle.Warning;
            var activeStyle = library != null ? library.WizardActiveIndicator : WizardIndicatorStyle.Active;
            float ringThickness = library != null ? library.WizardRingThickness : CombatVfxLibrary.DefaultWizardRingThickness;
            properties.SetFloat("_RingThickness", math.saturate(ringThickness));
            int decorated = 0;
            foreach (var zone in SystemAPI.Query<RefRO<WizardZone>>())
            {
                var hazard = zone.ValueRO;
                float pulse = .85f + .15f * math.sin((float)now * 9);
                bool active = hazard.Active != 0;
                var style = active ? activeStyle : warningStyle;
                var fill = style.FillColor;
                fill.a *= pulse;
                properties.SetColor("_Color", fill);
                var accent = CombatOwnershipTint.Accent(hazard.TriggerOwner, library);
                properties.SetColor("_RingColor", CombatOwnershipTint.Blend(style.RingColor, accent, .9f));
                properties.SetFloat("_Ring", math.saturate(style.RingOpacity));
                var position = (Vector3)hazard.Position;
                Graphics.DrawMesh(disc, Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * hazard.Radius),
                    material, 0, null, 0, properties, ShadowCastingMode.Off, false);
                // All zones retain the full boundary; only decorative cloud density is capped.
                if (library == null || camera == null || decorated++ >= library.MaximumCloudZones) continue;
                float radius = hazard.Radius;
                if (radius <= .001f) continue;
                Vector4 clip = new Vector4(position.x,position.z,radius,1);
                float seed = position.x * .31f + position.z * .71f;
                float opacity = active ? 1 : .22f;
                for (int i=0;i<library.CloudsPerZone;i++)
                {
                    float angle = i * 2.399963f + (float)now * .12f + seed;
                    float distance = radius * .72f * math.sqrt((i+.5f)/library.CloudsPerZone);
                    float cycle = math.frac((float)now * .45f + i * .618f);
                    Vector3 centre = position + new Vector3(math.cos(angle)*distance,.35f+cycle*.8f,math.sin(angle)*distance);
                    float size = math.min(.95f,radius*.3f) * (.75f + cycle*.35f);
                    float fade = math.sin(cycle*math.PI);
                    clouds.Billboard(centre,camera.transform.right,camera.transform.up,size,
                        new Color(.53f,.64f,.18f,.42f*opacity*fade),clip);
                }
                DrawPollen(hazard, camera.transform, clip, now, opacity);
            }
            if (library != null) { clouds.Draw(library.SporeCloud); pollen.Draw(library.Pollen); }
        }

        private void DrawPollen(in WizardZone zone, Transform camera, Vector4 clip, double now, float opacity)
        {
            var ground = (Vector3)zone.Position;
            Vector3 crown = default;
            bool emitsFromCrown = zone.Follow != 0 && TryGetSporeOrigin(zone.Source, out crown);
            float telegraphDuration = math.max(0f, zone.Settings.TelegraphDuration);
            // Continue the spray through activation, when the zone's expiry is extended.
            float castDuration = zone.Active != 0
                ? telegraphDuration + math.max(0f, zone.Settings.ActiveDuration) : telegraphDuration;
            float castAge = castDuration + (float)(now - zone.ExpiresAt);
            float flightSeconds = math.max(.05f, library.SporeFlightSeconds);
            float spreadPhase = math.clamp(library.SporeSpreadPhase, .01f, 1f);
            float spreadRadius = zone.Radius * math.saturate(library.SporeSpreadFraction);
            float seed = emitsFromCrown
                ? (math.hash(new int2(zone.Source.Index, zone.Source.Version)) & 1023u) * (2 * math.PI / 1024)
                : ground.x * .31f + ground.z * .71f + (float)now * .2f;
            Vector3 right = camera.right;
            Vector3 up = camera.up;
            float relativeRadius = zone.Radius / PollenReferenceRadius;
            int sporeCount = (int)math.round(math.clamp(
                library.SporesPerZone * relativeRadius * relativeRadius, 1f, MaximumSporesPerZone));

            for (int i = 0; i < sporeCount; i++)
            {
                float angle = i * 2.399963f + seed;
                float distance = spreadRadius * math.sqrt((i + .5f) / sporeCount);
                Vector3 drift = new Vector3(math.cos(angle) * distance, 0f, math.sin(angle) * distance);
                // Keep emission order independent of angle so the outer spray covers the whole circle.
                float stagger = math.frac(i * .7548777f);
                float cycle;
                Vector3 centre;
                if (emitsFromCrown)
                {
                    float age = castAge - flightSeconds * stagger;
                    if (age < 0f) continue;
                    cycle = math.frac(age / flightSeconds);
                    // Reach the outer cloud while the spores are still bright, then settle into it.
                    float spread = math.smoothstep(0f, spreadPhase, cycle);
                    centre = Vector3.LerpUnclamped(crown, ground + drift, spread);
                    centre.y = math.lerp(crown.y, ground.y + .65f, cycle * cycle)
                        + .95f * math.sin(cycle * math.PI);
                }
                else
                {
                    cycle = math.frac((float)now * .38f + stagger);
                    centre = ground + drift + Vector3.up * (.15f + cycle * 1.25f);
                }

                float size = math.min(zone.Radius * .22f, (.105f + .035f * math.sin(i)) * (1f - .35f * cycle));
                float fade = math.smoothstep(0f, .06f, cycle) * (1f - math.smoothstep(.78f, 1f, cycle));
                pollen.Billboard(centre, right, up, size, new Color(1f, .93f, .43f, opacity * fade), clip);
            }
        }

        private bool TryGetSporeOrigin(Entity source, out Vector3 position)
        {
            position = default;
            if (!SystemAPI.HasComponent<WizardSporeOrigin>(source)) return false;
            var origin = SystemAPI.GetComponent<WizardSporeOrigin>(source);
            if (!SystemAPI.HasBuffer<SkinMatrix>(origin.Animation) || !SystemAPI.HasComponent<LocalToWorld>(origin.Root))
                return false;
            var skin = EntityManager.GetBuffer<SkinMatrix>(origin.Animation, true);
            if (origin.Bone >= skin.Length) return false;
            float3 crown = math.mul(skin[origin.Bone].Value, new float4(origin.BindPosition, 1f));
            position = math.transform(SystemAPI.GetComponent<LocalToWorld>(origin.Root).Value, crown);
            return true;
        }

        protected override void OnDestroy() { if (disc != null) Object.Destroy(disc); clouds.Dispose(); pollen.Dispose(); }
    }
}
