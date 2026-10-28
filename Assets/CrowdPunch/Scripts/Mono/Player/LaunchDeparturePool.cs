using CrowdPunch.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Player
{
    // Finite, world-anchored launch punctuation. One mesh and fixed storage for the crowd.
    internal sealed class LaunchDeparturePool : System.IDisposable
    {
        private struct Departure
        {
            public bool Active;
            public Vector3 Position, Direction;
            public float Age, Size;
        }
        private const int VerticesPerDeparture = 384;
        private readonly Departure[] departures;
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
        internal int Capacity => departures.Length;

        public LaunchDeparturePool(Transform parent, CombatFeedbackSettings settings, Material material)
        {
            this.settings = settings;
            departures = new Departure[Mathf.Clamp(settings.ParticlePoolSizePerEffect * 3, 3, 48)];
            vertices = new Vector3[departures.Length * VerticesPerDeparture];
            colors = new Color[vertices.Length];
            indices = new int[vertices.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            var go = new GameObject("Launch departures (batched)");
            root = go.transform;
            root.SetParent(parent, false);
            mesh = new Mesh { name = "Cartoon launch departures" };
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

        public void Show(Vector3 position, Vector3 direction, Vector3 modelSize)
        {
            if (!settings.LaunchDeparturePuff) return;
            for (int i = 0; i < departures.Length; i++)
            {
                if (departures[i].Active) continue;
                direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.forward;
                departures[i] = new Departure
                {
                    Active = true, Direction = direction,
                    Position = position - direction * Mathf.Clamp(modelSize.z * .25f, .1f, .7f),
                    Size = Mathf.Clamp(Mathf.Max(modelSize.x, modelSize.y * .5f), .35f, 2.5f)
                };
                ActiveCount++;
                return;
            }
        }

        public void Tick(float unscaledDeltaTime, UnityEngine.Camera camera)
        {
            if (!settings.LaunchDeparturePuff) { Clear(); return; }
            count = ActiveCount = 0;
            for (int i = 0; i < departures.Length; i++)
            {
                ref var departure = ref departures[i];
                if (!departure.Active) continue;
                departure.Age += Mathf.Max(0, unscaledDeltaTime);
                if (departure.Age >= Mathf.Max(.01f, settings.LaunchDepartureDuration))
                { departure.Active = false; continue; }
                ActiveCount++;
                if (camera != null) Draw(departure, camera);
            }
            renderer.enabled = count > 0;
            if (count == 0) return;
            mesh.Clear(false);
            mesh.SetVertices(vertices, 0, count);
            mesh.SetColors(colors, 0, count);
            mesh.SetTriangles(indices, 0, count, 0, true);
        }

        private void Draw(Departure departure, UnityEngine.Camera camera)
        {
            float t = Mathf.Clamp01(departure.Age / Mathf.Max(.01f, settings.LaunchDepartureDuration));
            float ease = 1f - Mathf.Pow(1f - t, 3);
            float fade = (1f - t) * (1f - t);
            float size = departure.Size * settings.LaunchDepartureScale;
            var screenRight = camera.transform.right;
            var screenUp = camera.transform.up;
            float angle = Mathf.Atan2(Vector3.Dot(departure.Direction, screenUp), Vector3.Dot(departure.Direction, screenRight));
            right = root.InverseTransformVector((screenRight * Mathf.Cos(angle) + screenUp * Mathf.Sin(angle)) * size);
            up = root.InverseTransformVector((-screenRight * Mathf.Sin(angle) + screenUp * Mathf.Cos(angle)) * size);
            centre = root.InverseTransformPoint(departure.Position - camera.transform.forward * .03f);
            for (int i = 0; i < 5; i++)
            {
                float a = (i - 2) * .58f;
                var point = new Vector2(-.12f - Mathf.Cos(a) * ease * .35f, Mathf.Sin(a) * ease * .46f);
                Puff(point, Mathf.Lerp(.1f, .24f, ease) * (1f - Mathf.Abs(i - 2) * .09f), fade * .82f);
            }
            for (int stroke = -1; stroke <= 1; stroke++)
            {
                Vector2 previous = StrokePoint(0, stroke, ease);
                for (int segment = 1; segment <= 10; segment++)
                {
                    Vector2 next = StrokePoint(segment / 10f, stroke, ease);
                    var side = new Vector2(-(next - previous).y, (next - previous).x).normalized * .023f * (1f - t * .6f);
                    var color = new Color(1f, .98f, .88f, fade);
                    Triangle(previous - side, previous + side, next + side, color, color, color);
                    Triangle(previous - side, next + side, next - side, color, color, color);
                    previous = next;
                }
            }
        }

        private static Vector2 StrokePoint(float t, int stroke, float expansion)
            => new Vector2(-.08f - t * (.4f + expansion * .42f),
                stroke * (.18f + t * t * (.22f + expansion * .25f)));

        private void Puff(Vector2 point, float radius, float alpha)
        {
            var core = new Color(1f, .97f, .87f, alpha);
            var edge = new Color(1f, .97f, .87f, 0);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f, b = (i + 1) * Mathf.PI / 6f;
                Triangle(point, point + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    point + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, core, edge, edge);
            }
        }
        private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            vertices[count] = centre + right * a.x + up * a.y; colors[count++] = ca;
            vertices[count] = centre + right * b.x + up * b.y; colors[count++] = cb;
            vertices[count] = centre + right * c.x + up * c.y; colors[count++] = cc;
        }
        public void Clear()
        {
            System.Array.Clear(departures, 0, departures.Length);
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
