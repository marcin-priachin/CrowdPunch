using CrowdPunch.Components;
using CrowdPunch.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Player
{
    // One bounded mesh for the whole crowd. Slots carry copied contact data, never entities.
    internal sealed class ComicImpactPool
    {
        private struct Impact
        {
            public bool Active;
            public CombatImpactKind Kind;
            public Vector3 Position, Direction;
            public float Age, Intensity;
        }

        private const float Duration = .32f;
        private const int VerticesPerImpact = 768;
        private static readonly Color Ink = new Color(.095f, .055f, .16f);
        private static readonly Vector2[] BurstOutline = MakeOutline(10, .34f);
        private static readonly Vector2[] CoreOutline = MakeOutline(10, .38f);
        private static readonly Vector2[] StarOutline = MakeOutline(4, .3f);
        private static readonly Vector2[] RingOutline = MakeOutline(12, 1f);
        private readonly Impact[] impacts;
        private readonly Vector3[] vertices;
        private readonly Color[] colors;
        private readonly int[] indices;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Transform root;
        private readonly CombatFeedbackSettings settings;
        private int count;
        private Vector3 centre, right, up;

        internal int ActiveCount { get; private set; }
        internal int Capacity => impacts.Length;

        public ComicImpactPool(Transform parent, CombatFeedbackSettings settings, Material material)
        {
            this.settings = settings;
            impacts = new Impact[Mathf.Clamp(settings.ParticlePoolSizePerEffect * 3, 3, 48)];
            vertices = new Vector3[impacts.Length * VerticesPerImpact];
            colors = new Color[vertices.Length];
            indices = new int[vertices.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            var go = new GameObject("Comic impacts (batched)");
            root = go.transform;
            root.SetParent(parent, false);
            mesh = new Mesh { name = "Comic impact accents" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
        }

        public void Show(CombatImpactKind kind, Vector3 position, Vector3 direction, float intensity)
        {
            if (settings.ComicImpactScale <= 0f || kind != CombatImpactKind.Punch
                && kind != CombatImpactKind.EnemyCollision && kind != CombatImpactKind.PlayerDamage) return;
            int slot = -1;
            for (int i = 0; i < impacts.Length; i++)
            {
                if (!impacts[i].Active) { slot = i; break; }
            }
            // Reserve the player's readability under saturation by replacing a background collision.
            if (slot < 0 && kind != CombatImpactKind.EnemyCollision)
                for (int i = 0; i < impacts.Length; i++)
                    if (impacts[i].Kind == CombatImpactKind.EnemyCollision
                        && (slot < 0 || impacts[i].Age > impacts[slot].Age)) slot = i;
            if (slot < 0) return;
            if (!impacts[slot].Active) ActiveCount++;
            impacts[slot] = new Impact { Active = true, Kind = kind, Position = position,
                Direction = direction, Intensity = Mathf.Clamp01(intensity) };
        }

        public void Tick(float unscaledDeltaTime, UnityEngine.Camera camera)
        {
            count = 0;
            ActiveCount = 0;
            for (int i = 0; i < impacts.Length; i++)
            {
                ref var impact = ref impacts[i];
                if (!impact.Active) continue;
                impact.Age += Mathf.Max(0f, unscaledDeltaTime);
                if (impact.Age >= Duration) { impact.Active = false; continue; }
                ActiveCount++;
                if (camera != null && settings.ComicImpactScale > 0f) Draw(impact, camera);
            }
            renderer.enabled = count > 0;
            if (count == 0) return;
            mesh.Clear(false);
            mesh.SetVertices(vertices, 0, count);
            mesh.SetColors(colors, 0, count);
            mesh.SetTriangles(indices, 0, count, 0, true);
        }

        private void Draw(Impact impact, UnityEngine.Camera camera)
        {
            float t = impact.Age / Duration;
            bool damage = impact.Kind == CombatImpactKind.PlayerDamage;
            bool collision = impact.Kind == CombatImpactKind.EnemyCollision;
            Color accent = damage ? new Color(1f, .22f, .19f)
                : collision ? new Color(.18f, .88f, 1f) : new Color(1f, .68f, .045f);
            Color core = settings.ReduceFlashes ? accent : new Color(1f, .95f, .69f);
            float radius = (damage ? 1.05f : collision ? .58f : .9f)
                * Mathf.Lerp(.65f, 1.15f, impact.Intensity) * settings.ComicImpactScale;
            float fade = 1f - Smooth(Mathf.InverseLerp(.45f, 1f, t));
            Vector3 screenRight = camera.transform.right, screenUp = camera.transform.up;
            float angle = Mathf.Atan2(Vector3.Dot(impact.Direction, screenUp), Vector3.Dot(impact.Direction, screenRight));
            right = root.InverseTransformVector(screenRight * Mathf.Cos(angle) + screenUp * Mathf.Sin(angle));
            up = root.InverseTransformVector(-screenRight * Mathf.Sin(angle) + screenUp * Mathf.Cos(angle));
            centre = root.InverseTransformPoint(impact.Position - camera.transform.forward * .08f);

            // The burst snaps out, overshoots once, then contracts; the ring carries energy outward.
            float pop = 1f + .23f * Mathf.Sin(Mathf.Min(1f, t / .32f) * Mathf.PI);
            float burst = radius * pop * (1f - Smooth(Mathf.InverseLerp(.15f, .62f, t)));
            if (burst > .001f)
            {
                Burst(Vector2.zero, burst, WithAlpha(Ink, fade), BurstOutline, 0f);
                Burst(Vector2.zero, burst * .83f, WithAlpha(accent, fade), BurstOutline, 0f);
                Burst(Vector2.zero, burst * .44f, WithAlpha(core, fade), CoreOutline, .07f);
            }
            float travel = 1f - Mathf.Pow(1f - t, 3f);
            float ringRadius = radius * Mathf.Lerp(.45f, 1.7f, travel);
            float ringWidth = radius * .065f * (1f - t);
            Ring(ringRadius, ringWidth * 1.8f, WithAlpha(Ink, fade * .8f));
            Ring(ringRadius, ringWidth, WithAlpha(accent, fade));

            int stars = collision ? 2 : 4;
            for (int i = 0; i < stars; i++)
            {
                float a = .4f + i * (2f * Mathf.PI / stars);
                Vector2 point = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * Mathf.Lerp(.55f, 1.85f, travel);
                point.x += radius * travel * .22f;
                point.y -= radius * t * t * .5f;
                float size = radius * .17f * fade * (1f + .2f * Mathf.Sin(t * Mathf.PI));
                Burst(point, size, WithAlpha(Ink, fade), StarOutline, a + t * 3f);
                Burst(point, size * .7f, WithAlpha(accent, fade), StarOutline, a + t * 3f);
            }
        }

        private void Burst(Vector2 origin, float radius, Color color, Vector2[] outline, float angle)
        {
            int edges = outline.Length;
            float sine = Mathf.Sin(angle) * radius, cosine = Mathf.Cos(angle) * radius;
            for (int i = 0; i < edges; i++)
            {
                Vector2 v = outline[i], w = outline[(i + 1) % edges];
                Vector2 a = new Vector2(v.x * cosine - v.y * sine, v.x * sine + v.y * cosine);
                Vector2 b = new Vector2(w.x * cosine - w.y * sine, w.x * sine + w.y * cosine);
                Triangle(origin, origin + a, origin + b, color);
            }
        }

        private static Vector2[] MakeOutline(int tips, float inset)
        {
            var points = new Vector2[tips * 2];
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * 2f * Mathf.PI / points.Length;
                float r = (i & 1) == 0 ? 1f : inset;
                if (tips == 10) r *= 1f + .12f * Mathf.Sin(i * 2.7f);
                points[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return points;
        }

        private void Ring(float radius, float width, Color color)
        {
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                Vector2 v = RingOutline[i], w = RingOutline[(i + 1) % segments];
                Triangle(v * (radius - width), v * radius, w * radius, color);
                Triangle(v * (radius - width), w * radius, w * (radius - width), color);
            }
        }

        private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            Vertex(a, color); Vertex(b, color); Vertex(c, color);
        }
        private void Vertex(Vector2 point, Color color)
        {
            vertices[count] = centre + right * point.x + up * point.y;
            colors[count++] = color;
        }
        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }

        public void Clear()
        {
            for (int i = 0; i < impacts.Length; i++) impacts[i] = default;
            ActiveCount = 0;
            if (renderer != null) renderer.enabled = false;
            if (mesh != null) mesh.Clear(false);
        }
        public void Dispose()
        {
            Clear();
            if (Application.isPlaying)
            {
                Object.Destroy(mesh);
                if (root != null) Object.Destroy(root.gameObject);
            }
            else
            {
                Object.DestroyImmediate(mesh);
                if (root != null) Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}
