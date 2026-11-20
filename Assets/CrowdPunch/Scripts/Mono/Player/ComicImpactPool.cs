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
            public bool Active, IsBlast, BrightFlash, HasDrawn;
            public CombatImpactKind Kind;
            public Vector3 Position, Direction, FlashOffset;
            public float Age, Intensity;
            public float BlastRadius;
        }

        private const float Duration = .22f;
        private const float PunchFlashDuration = .145f;
        private const float PunchFlashLift = .20f;
        private const float BlastFlashDuration = .155f;
        private const float CampaignImpactScale = .65f;
        private const int VerticesPerImpact = 768;
        private static readonly Color Ink = new Color(.095f, .055f, .16f);
        private static readonly Vector2[] BurstOutline = MakeOutline(10, .34f);
        private static readonly Vector2[] CoreOutline = MakeOutline(10, .38f);
        private static readonly Vector2[] StarOutline = MakeOutline(4, .3f);
        private static readonly Vector2[] RingOutline = MakeOutline(12, 1f);
        private static readonly Vector2[] PunchFlashOutline = MakeOutline(8, .34f);
        private static readonly Vector2[] BlastFlashOutline = MakeOutline(14, .82f, .10f);
        private static readonly Color FlashCentre = new Color(6.8f, 6.4f, 4.6f);
        private static readonly Color FlashInner = new Color(5.6f, 4.8f, 3.1f);
        private static readonly Color PunchFlashEdge = new Color(6f, 2.5f, .40f);
        private static readonly Color BlastFlashEdge = new Color(7f, 2.36f, .38f);
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

        public void Show(CombatImpactKind kind, Vector3 position, Vector3 direction, float intensity,
            bool brightFlash = false, Vector3 flashOffset = default)
        {
            if (settings.ComicImpactScale <= 0f || kind != CombatImpactKind.Punch
                && kind != CombatImpactKind.EnemyCollision && kind != CombatImpactKind.PlayerDamage) return;
            Queue(new Impact { Active = true, Kind = kind, Position = position,
                Direction = direction, Intensity = Mathf.Clamp01(intensity), BrightFlash = brightFlash, FlashOffset = flashOffset });
        }

        public void ShowBlast(Vector3 position, float radius)
        {
            if (settings.ComicImpactScale <= 0f || settings.ReduceFlashes) return;
            Queue(new Impact { Active = true, IsBlast = true, Position = position + Vector3.up * .60f,
                BlastRadius = radius, BrightFlash = true });
        }

        private void Queue(Impact impact)
        {
            int slot = -1;
            for (int i = 0; i < impacts.Length; i++)
            {
                if (!impacts[i].Active) { slot = i; break; }
            }
            // Reserve the player's readability under saturation by replacing a background collision.
            if (slot < 0 && impact.Kind != CombatImpactKind.EnemyCollision)
                for (int i = 0; i < impacts.Length; i++)
                    if (impacts[i].Kind == CombatImpactKind.EnemyCollision
                        && (slot < 0 || impacts[i].Age > impacts[slot].Age)) slot = i;
            if (slot < 0) return;
            if (!impacts[slot].Active) ActiveCount++;
            impacts[slot] = impact;
        }

        public void Tick(float unscaledDeltaTime, UnityEngine.Camera camera)
        {
            count = 0;
            ActiveCount = 0;
            bool canDraw = camera != null && settings.ComicImpactScale > 0f;
            float delta = Mathf.Max(0f, unscaledDeltaTime);
            for (int i = 0; i < impacts.Length; i++)
            {
                ref var impact = ref impacts[i];
                if (!impact.Active) continue;
                bool firstFrame = !impact.HasDrawn;
                // A confirmed punch starts its flash on a rendered frame, even after a hitch.
                if (!firstFrame || !canDraw || !IsDirectPunch(impact)) impact.Age += delta;
                if (impact.Age >= (impact.IsBlast ? BlastFlashDuration : Duration)) { impact.Active = false; continue; }
                ActiveCount++;
                if (!canDraw) continue;
                if (!impact.IsBlast) Draw(impact, camera);
                if (impact.BrightFlash && !settings.ReduceFlashes)
                    DrawFlash(impact, camera, firstFrame ? 0f : impact.Age);
                impact.HasDrawn = true;
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
            SetBillboard(impact.Position, impact.Direction, camera.transform, .08f);

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
            if (damage)
            {
                Ring(ringRadius, ringWidth * 1.8f, WithAlpha(Ink, fade * .8f));
                Ring(ringRadius, ringWidth, WithAlpha(accent, fade));
            }

            int stars = collision ? 0 : 2;
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

        private void DrawFlash(Impact impact, UnityEngine.Camera camera, float age)
        {
            bool collision = impact.Kind == CombatImpactKind.EnemyCollision;
            if (impact.Kind == CombatImpactKind.PlayerDamage || collision && impact.Intensity < .38f) return;
            bool directPunch = IsDirectPunch(impact);
            float duration = impact.IsBlast ? BlastFlashDuration : collision ? .072f : PunchFlashDuration;
            if (age >= duration) return;

            float peak = impact.IsBlast ? .072f : collision ? .025f : .070f;
            float fade = 1f - Smooth(Mathf.InverseLerp(peak * .77f, duration, age));
            float width = impact.IsBlast ? Mathf.Clamp(impact.BlastRadius * .32f, .78f, 1.7f)
                : collision ? Mathf.Lerp(.23f, .42f, impact.Intensity)
                : .76f * Mathf.Lerp(.8f, 1.12f, impact.Intensity);
            // The approved flash widths use the saved campaign scale as their reference.
            width *= (settings.ComicImpactScale / CampaignImpactScale) * Mathf.Lerp(.86f, 1.16f, age / duration);
            float gain = collision ? (impact.Intensity >= .70f ? .48f : .29f) : 1f;
            Vector3 position = impact.Position + impact.FlashOffset;
            if (!impact.IsBlast) position += Vector3.up * (collision ? .18f : PunchFlashLift);
            if (directPunch)
            {
                // Move along the viewing ray so the flash stays over the contact on screen.
                Ray ray = camera.ViewportPointToRay(camera.WorldToViewportPoint(position));
                float room = Vector3.Dot(position - ray.origin, ray.direction);
                position -= ray.direction * Mathf.Clamp(room - .10f, 0f, .85f);
            }
            SetBillboard(position, impact.Direction, camera.transform, directPunch ? 0f : .18f,
                impact.IsBlast ? 12f * Mathf.Deg2Rad : 0f);

            Color centerColor = WithAlpha(FlashCentre * gain, fade);
            Color inner = WithAlpha(FlashInner * gain, fade);
            Color edge = WithAlpha((impact.IsBlast ? BlastFlashEdge : PunchFlashEdge) * gain, fade);
            Color fringe = WithAlpha(edge, 0f);
            var outline = impact.IsBlast ? BlastFlashOutline : PunchFlashOutline;
            Vector2 size = new Vector2(width, width * (impact.IsBlast ? .82f : .62f));
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = Vector2.Scale(outline[i], size), b = Vector2.Scale(outline[(i + 1) % outline.Length], size);
                Triangle(Vector2.zero, a * .48f, b * .48f, centerColor, inner, inner);
                Triangle(a * .48f, a * .95f, b * .48f, inner, edge, inner);
                Triangle(b * .48f, a * .95f, b * .95f, inner, edge, edge);
                Triangle(a * .95f, a * 1.1f, b * .95f, edge, fringe, edge);
                Triangle(b * .95f, a * 1.1f, b * 1.1f, edge, fringe, fringe);
            }
        }

        private static bool IsDirectPunch(Impact impact) =>
            impact.BrightFlash && !impact.IsBlast && impact.Kind == CombatImpactKind.Punch;

        internal static Vector3 PunchFlashOffset(UnityEngine.Camera camera, Vector3 position, Vector3 head, Vector3 hips)
        {
            Vector3 flash = position + Vector3.up * PunchFlashLift;
            Vector3 point = camera.WorldToViewportPoint(flash);
            Vector3 topOfTorso = camera.WorldToViewportPoint(head);
            Vector3 bottomOfTorso = camera.WorldToViewportPoint(hips);
            float torsoDepth = Mathf.Min(topOfTorso.z, bottomOfTorso.z);
            if (torsoDepth <= camera.nearClipPlane || point.z < torsoDepth - .3f) return Vector3.zero;

            // A compact envelope around the live pose avoids the oversized skin-culling bounds.
            Vector3 torso = (head + hips) * .5f;
            float shoulderRadius = Vector3.Distance(head, hips) * .6f;
            float centerX = camera.WorldToViewportPoint(torso).x;
            float shoulderX = camera.WorldToViewportPoint(torso + camera.transform.right * shoulderRadius).x;
            float halfWidth = Mathf.Abs(shoulderX - centerX) + .025f / camera.aspect;
            float left = Mathf.Min(topOfTorso.x, bottomOfTorso.x) - halfWidth;
            float right = Mathf.Max(topOfTorso.x, bottomOfTorso.x) + halfWidth;
            float top = Mathf.Max(topOfTorso.y, bottomOfTorso.y) + .045f;
            float bottom = Mathf.Min(topOfTorso.y, bottomOfTorso.y) - .045f;
            if (point.x < left || point.x > right || point.y < bottom || point.y > top) return Vector3.zero;

            float side = point.x < centerX ? left : right;
            if (Mathf.Abs(side - point.x) * camera.aspect < top - point.y) point.x = side;
            else point.y = top;
            return camera.ViewportToWorldPoint(point) - flash;
        }

        private void SetBillboard(Vector3 position, Vector3 direction, Transform camera, float depthOffset, float rotation = 0f)
        {
            Vector3 screenRight = camera.right, screenUp = camera.up;
            float angle = rotation + Mathf.Atan2(Vector3.Dot(direction, screenUp), Vector3.Dot(direction, screenRight));
            float sine = Mathf.Sin(angle), cosine = Mathf.Cos(angle);
            right = root.InverseTransformVector(screenRight * cosine + screenUp * sine);
            up = root.InverseTransformVector(-screenRight * sine + screenUp * cosine);
            centre = root.InverseTransformPoint(position - camera.forward * depthOffset);
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

        private static Vector2[] MakeOutline(int tips, float inset, float variation = 0f)
        {
            var points = new Vector2[tips * 2];
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * 2f * Mathf.PI / points.Length;
                float r = (i & 1) == 0 ? 1f : inset;
                if (tips == 10) r *= 1f + .12f * Mathf.Sin(i * 2.7f);
                r *= 1f + variation * Mathf.Cos(i * 2.41f);
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
            Triangle(a, b, c, color, color, color);
        }

        private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            Vertex(a, ca); Vertex(b, cb); Vertex(c, cc);
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
