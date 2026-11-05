using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Player
{
    // Presentation-only binding, objective activation and stun accents in one bounded mesh.
    internal sealed class FinaleDowningVisuals : System.IDisposable
    {
        private static readonly Color Ink = new Color(.08f, .12f, .23f);
        private static readonly Color Cyan = new Color(.12f, 1, .91f);
        private static readonly Color Cream = new Color(1, .94f, .65f);
        private readonly Transform root;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Material material;
        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Color> colors = new List<Color>(4096);
        private readonly List<int> indices = new List<int>(4096);
        private int phase;
        private bool stunned;
        private float progress, radius;
        private Vector3 source, chest, head, floor;

        public FinaleDowningVisuals(Transform parent)
        {
            root = new GameObject("Finale cartoon downing").transform;
            root.SetParent(parent, false);
            mesh = new Mesh { name = "Finale binding and stun accents" };
            mesh.MarkDynamic();
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Resources.Load<Shader>("Shaders/CombatFeedback"));
            renderer = root.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
        }

        public void Present(int phase, Vector3 source, Vector3 chest, Vector3 head, Vector3 position,
            float progress, float radius, bool stunned)
        {
            this.phase = phase; this.source = source + Vector3.up * 1.1f;
            this.chest = chest; this.head = head; floor = position; floor.y = -.80f;
            this.progress = Mathf.Clamp01(progress); this.radius = radius; this.stunned = stunned;
        }

        public void Tick()
        {
            vertices.Clear(); colors.Clear(); indices.Clear();
            var camera = UnityEngine.Camera.main;
            if (camera != null && (phase == 1 || phase == 2))
            {
                if (phase == 1) Pull(camera);
                else Grounded(camera);
            }
            renderer.enabled = vertices.Count > 0;
            if (!renderer.enabled) return;
            mesh.Clear(false); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
        }

        public void Body(Vector3 chest, Vector3 head) { this.chest = chest; this.head = head; }

        private void Pull(UnityEngine.Camera camera)
        {
            Vector3 axis = chest - source;
            Vector3 side = Vector3.Cross(axis.normalized, camera.transform.forward).normalized;
            // Tightening curved rope, outlined so it remains legible against the marble and body.
            for (int i = 0; i < 24; i++)
            {
                Vector3 a = Rope(i / 24f), b = Rope((i + 1) / 24f);
                Stroke(a, b, side, .24f, Ink);
                Stroke(a, b, side, .13f, Cyan);
            }
            // Packets move from the boss to the completed installation: a visible downward pull.
            for (int i = 0; i < 7; i++)
            {
                float p = Mathf.Repeat(i / 7f - Time.time * .75f, 1);
                Vector3 at = Rope(p), along = axis.normalized;
                Triangle(at - along * .4f, at + along * .3f + side * .32f, at + along * .3f - side * .32f, Cream);
            }
            Ring(chest, 1.5f, .18f, Cyan);
            // The source gets a short celebratory pop before settling to an energized socket.
            float pop = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .32f));
            Vector3 socket = source; socket.y = -.78f;
            Ring(socket, Mathf.Lerp(1.4f, 3.8f, 1 - pop), .24f * pop + .08f, Alpha(Cyan, .5f + pop * .5f));
            Star(source + Vector3.up * .7f, camera.transform.right, camera.transform.up, 1.2f + pop, Ink);
            Star(source + Vector3.up * .7f, camera.transform.right, camera.transform.up, .85f + pop * .8f, Cream);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3;
                Vector3 radial = camera.transform.right * Mathf.Cos(angle) + camera.transform.up * Mathf.Sin(angle);
                Vector3 at = source + Vector3.up * .7f + radial * (2 + (1 - pop) * 2);
                Stroke(at, at + radial * pop * .8f, Vector3.Cross(radial, camera.transform.forward), .09f * pop, Cyan);
            }
        }

        private Vector3 Rope(float p) => Vector3.Lerp(source, chest, p)
            + Vector3.up * (Mathf.Sin(p * Mathf.PI) * Mathf.Lerp(2, .2f, progress));

        private void Grounded(UnityEngine.Camera camera)
        {
            // Four broad brackets read as a captured giant, leaving the crowd and cyan ring visible.
            float pulse = .5f + .5f * Mathf.Sin(Time.time * 4);
            // Visible leashes connect the floating Wraith's body to the floor capture marks.
            Vector3 cross = camera.transform.right; cross.y = 0; cross.Normalize();
            Vector3 cuff = chest - Vector3.up;
            Ring(cuff, 1.5f, .13f, Cyan);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 top = cuff + cross * (sign * 1.4f), bottom = floor + cross * (sign * radius * .7f);
                Vector3 side = Vector3.Cross((bottom - top).normalized, camera.transform.forward).normalized;
                Stroke(top, bottom, side, .15f, Ink);
                Stroke(top, bottom, side, .075f, Cyan);
                for (int link = 1; link < 8; link++)
                {
                    Vector3 p = Vector3.Lerp(top, bottom, link / 8f);
                    Stroke(p - side * .17f, p + side * .17f, (bottom - top).normalized, .055f, Cream);
                }
            }
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                Vector3 axis = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 side = Vector3.Cross(Vector3.up, axis);
                Vector3 a = floor + axis * radius * .28f;
                Vector3 b = floor + axis * radius * .52f + side * .35f;
                Vector3 c = floor + axis * radius * .72f;
                Stroke(a, b, side, .10f, Alpha(Ink, .65f));
                Stroke(b, c, side, .07f, Alpha(Ink, .65f));
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * .5f;
                Vector3 axis = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 side = Vector3.Cross(Vector3.up, axis);
                Vector3 at = floor + axis * (radius - .7f);
                Stroke(at - side * .9f, at + side * .9f, axis, .22f, Ink);
                Stroke(at - side * .8f, at + side * .8f, axis, .11f, Cyan);
                Stroke(at - side * .9f, at - side * .9f - axis * .8f, side, .16f, Cyan);
                Stroke(at + side * .9f, at + side * .9f - axis * .8f, side, .16f, Cyan);
                Vector3 tip = at - axis * (1 + pulse * .12f);
                Triangle(tip - axis * .55f, tip + side * .45f, tip - side * .45f, Alpha(Cyan, .65f + pulse * .3f));
            }
            if (!stunned) return;
            // Classic cartoon dizziness belongs only to the real initial stun, never active ground attacks.
            Vector3 orbit = head + Vector3.up * 1.7f;
            Ring(orbit, 1.65f, .055f, Alpha(Cream, .65f));
            for (int i = 0; i < 3; i++)
            {
                float angle = Time.time * 2.4f + i * Mathf.PI * 2 / 3;
                Vector3 p = orbit + new Vector3(Mathf.Cos(angle) * 1.65f, Mathf.Sin(angle * 2) * .18f, Mathf.Sin(angle) * 1.65f);
                Star(p, camera.transform.right, camera.transform.up, .52f, Ink);
                Star(p, camera.transform.right, camera.transform.up, .37f, Cream);
            }
        }

        private void Star(Vector3 p, Vector3 right, Vector3 up, float size, Color color)
        {
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5, b = (i + 1) * Mathf.PI / 5;
                Triangle(p, p + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * size * (i % 2 == 0 ? 1 : .42f),
                    p + (right * Mathf.Cos(b) + up * Mathf.Sin(b)) * size * (i % 2 == 0 ? .42f : 1), color);
            }
        }
        private void Ring(Vector3 p, float radius, float width, Color color)
        {
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI / 24, b = (i + 1) * Mathf.PI / 24;
                Vector3 v = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), w = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                Quad(p + v * (radius - width), p + w * (radius - width), p + w * radius, p + v * radius, color);
            }
        }
        private void Stroke(Vector3 a, Vector3 b, Vector3 side, float width, Color color)
            => Quad(a - side * width, b - side * width, b + side * width, a + side * width, color);
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        { Triangle(a, b, c, color); Triangle(a, c, d, color); }
        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            vertices.Add(root.InverseTransformPoint(a)); vertices.Add(root.InverseTransformPoint(b)); vertices.Add(root.InverseTransformPoint(c));
            for (int i = 0; i < 3; i++) { colors.Add(color); indices.Add(indices.Count); }
        }
        private static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }
        public void Clear() { phase = 0; renderer.enabled = false; }
        public void Dispose()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(mesh); Object.DestroyImmediate(material);
                if (root != null) Object.DestroyImmediate(root.gameObject);
                return;
            }
#endif
            Object.Destroy(mesh); Object.Destroy(material);
            if (root != null) Object.Destroy(root.gameObject);
        }
    }
}
