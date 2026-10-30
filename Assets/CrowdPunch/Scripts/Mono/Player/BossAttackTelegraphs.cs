using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Player
{
    // Presentation-only endpoint: two copied hand slots, never ECS entities.
    public sealed class BossAttackTelegraphs : MonoBehaviour
    {
        private static BossAttackTelegraphs active;
        private readonly Mesh[] meshes = new Mesh[2];
        private readonly MeshRenderer[] renderers = new MeshRenderer[2];
        private readonly bool[] seen = new bool[2];
        private readonly Vector3[] outline = new Vector3[48];
        private readonly List<Vector3> vertices = new List<Vector3>(1536);
        private readonly List<Color> colors = new List<Color>(1536);
        private readonly List<int> indices = new List<int>(1536);
        private Material material;
        private GatekeeperAttackParticles particles;
        private uint restart;

        private void OnEnable() { active = this; restart = GameRestartRegistry.Sequence; }
        private void OnDisable()
        {
            if (active == this) active = null;
            Clear();
        }
        private void OnDestroy()
        {
            particles?.Dispose();
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
        private void Clear()
        {
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
            particles?.Clear();
        }
        private void LateUpdate()
        {
            if (FeedbackTimeController.IsSuspended || restart != GameRestartRegistry.Sequence)
            {
                restart = GameRestartRegistry.Sequence;
                Clear();
                return;
            }
            for (int i = 0; i < seen.Length; i++) if (!seen[i]) particles?.Cancel(i);
            if (!seen[0] && !seen[1]) { particles?.Clear(); return; }
            particles?.Tick(Time.unscaledDeltaTime);
        }
        public static void BeginFrame()
        {
            if (active == null) return;
            for (int i = 0; i < 2; i++)
            {
                active.seen[i] = false;
                if (active.renderers[i] != null) active.renderers[i].enabled = false;
            }
        }
        public static void Publish(int slot, Vector3 center, Vector3 start, Vector3 direction,
            Vector3 position, float width, BossAttack attack, BossHandPhase phase, uint sequence,
            float progress, float groundHeight)
        {
            if (active == null || FeedbackTimeController.IsSuspended || slot < 0 || slot >= 2) return;
            active.EnsureResources();
            active.seen[slot] = true;
            active.particles.ShowHand(slot, center, position, direction, attack, phase, sequence, groundHeight);
            if (phase == BossHandPhase.Anticipation)
                active.Draw(slot, center, start, direction, width, attack, progress, groundHeight);
        }
        public static void Impact(Vector3 position, Vector3 direction, float intensity)
        {
            if (active == null || FeedbackTimeController.IsSuspended) return;
            active.EnsureResources();
            active.particles.Impact(position, direction, intensity);
        }
        private void EnsureResources()
        {
            if (material == null) material = new Material(Resources.Load<Shader>("Shaders/CombatFeedback"));
            if (particles == null) particles = new GatekeeperAttackParticles(transform, material);
        }
        private void Draw(int slot, Vector3 center, Vector3 start, Vector3 direction, float width,
            BossAttack attack, float progress, float groundHeight)
        {
            if (meshes[slot] == null)
            {
                var go = new GameObject("Gatekeeper cartoon attack area " + slot);
                go.transform.SetParent(transform, false);
                meshes[slot] = new Mesh { name = "Gatekeeper attack footprint " + slot };
                meshes[slot].MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = meshes[slot];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderers[slot] = renderer;
            }
            vertices.Clear(); colors.Clear(); indices.Clear();
            center.y = start.y = groundHeight + .045f;
            direction.y = 0;
            direction = direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            progress = Mathf.Clamp01(progress);
            Color warm = Color.Lerp(new Color(1, .72f, .12f), new Color(1, .26f, .09f), progress);
            Color fill = warm; fill.a = Mathf.Lerp(.16f, .32f, progress);
            int count = attack == BossAttack.Lunge ? 4 : 48;
            if (attack == BossAttack.Slam)
            {
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2 / count;
                    outline[i] = center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * width;
                }
                for (int i = 0; i < count; i++) Triangle(center, outline[i], outline[(i + 1) % count], fill);
            }
            else if (attack == BossAttack.Lunge)
            {
                outline[0] = start - side * width; outline[1] = center - side * width;
                outline[2] = center + side * width; outline[3] = start + side * width;
                Quad(outline[0], outline[1], outline[2], outline[3], fill);
            }
            else
            {
                // Preserve the committed curved sweep footprint; triangulate as a strip.
                for (int i = 0; i < 24; i++)
                {
                    float p = i / 23f;
                    Vector3 c = center + side * Mathf.Lerp(-width, width, p) - direction * (Mathf.Sin(p * Mathf.PI) * 2);
                    outline[i] = c - direction * 1.5f;
                    outline[47 - i] = c + direction * 1.5f;
                    if (i > 0) Quad(outline[i - 1], outline[i], outline[47 - i], outline[48 - i], fill);
                }
            }
            Color ink = new Color(.095f, .055f, .16f, .9f);
            float winding = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = outline[i], b = outline[(i + 1) % count];
                winding += a.x * b.z - b.x * a.z;
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 a = outline[i], b = outline[(i + 1) % count];
                // Both border bands sit inside the original footprint, so urgency never grows its area.
                Vector3 inward = Vector3.Cross(Vector3.up, b - a).normalized;
                if (winding > 0) inward = -inward;
                Quad(a, b, b + inward * .28f, a + inward * .28f, ink, .004f);
                Quad(a + inward * .075f, b + inward * .075f, b + inward * .21f, a + inward * .21f, warm, .008f);
            }
            // Filled cream chevrons share the chunky initial-trajectory arrow language.
            Vector3 arrowDirection = attack == BossAttack.Sweep ? side : direction;
            Vector3 arrowSide = Vector3.Cross(Vector3.up, arrowDirection);
            Vector3 marker = attack == BossAttack.Lunge ? Vector3.Lerp(start, center, .65f)
                : attack == BossAttack.Sweep ? center - direction * 2 : center;
            float size = Mathf.Min(.85f, width * .32f);
            Color cream = new Color(1, .95f, .72f, .9f);
            if (attack == BossAttack.Slam)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 axis = Quaternion.Euler(0, i * 90, 0) * Vector3.forward;
                    Vector3 cross = Vector3.Cross(Vector3.up, axis);
                    Vector3 tip = center + axis * width * .35f;
                    Triangle(tip, tip + axis * size + cross * size * .55f, tip + axis * size - cross * size * .55f, cream, .012f);
                }
            }
            else
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 tip = marker + arrowDirection * (i * size * 1.5f);
                    Triangle(tip + arrowDirection * size * .5f, tip - arrowDirection * size * .5f + arrowSide * size * .55f,
                        tip - arrowDirection * size * .5f - arrowSide * size * .55f, cream, .012f);
                }
            var mesh = meshes[slot];
            mesh.Clear(false); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
            renderers[slot].enabled = true;
        }
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float lift = 0)
        { Triangle(a, b, c, color, lift); Triangle(a, c, d, color, lift); }
        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, float lift = 0)
        {
            Vector3 offset = Vector3.up * lift;
            vertices.Add(transform.InverseTransformPoint(a + offset));
            vertices.Add(transform.InverseTransformPoint(b + offset));
            vertices.Add(transform.InverseTransformPoint(c + offset));
            for (int i = 0; i < 3; i++) { colors.Add(color); indices.Add(indices.Count); }
        }
    }
}
