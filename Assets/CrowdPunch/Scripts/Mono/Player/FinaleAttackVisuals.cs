using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Player
{
    // One dynamic mesh and a fixed dust pool for the finale. Snapshots contain no entity identities.
    internal sealed class FinaleAttackVisuals : System.IDisposable
    {
        private static readonly Color Ink = new Color(.12f, .045f, .19f, .94f);
        private static readonly Color Violet = new Color(.64f, .22f, 1, .2f);
        private static readonly Color Cream = new Color(1, .94f, .67f);
        private readonly Transform root;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Material material;
        private readonly Configuration.CombatFeedbackSettings feedback;
        private readonly List<Vector3> vertices = new List<Vector3>(16384);
        private readonly List<Color> colors = new List<Color>(16384);
        private readonly List<int> indices = new List<int>(16384);
        private readonly ParticleSystem[] dust = new ParticleSystem[12];
        private Vector3 center, direction, origin, hitCenter, hitDirection, hitOrigin;
        private float length, width, progress, hitLength, hitWidth, hitTime = -10;
        private int attack, hitAttack;
        private const float Floor = -.845f, Duration = .55f;

        public FinaleAttackVisuals(Transform parent)
        {
            root = new GameObject("Finale cartoon attack effects").transform;
            root.SetParent(parent, false);
            mesh = new Mesh { name = "Finale warning and impact shapes" };
            mesh.MarkDynamic();
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Resources.Load<Shader>("Shaders/CombatFeedback"));
            feedback = Resources.Load<Configuration.CombatFeedbackSettings>("CombatFeedbackSettings");
            renderer = root.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            var prefab = Resources.Load<ParticleSystem>("GatekeeperImpact");
            if (prefab != null)
                for (int i = 0; i < dust.Length; i++)
                {
                    dust[i] = Object.Instantiate(prefab, root);
                    dust[i].name = "Finale cream impact puff " + i;
                    dust[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
        }

        public void Present(int attack, Vector3 center, Vector3 direction, float length, float width, float progress, Vector3 origin)
        {
            this.attack = attack; this.center = center; this.center.y = Floor;
            direction.y = 0;
            this.direction = direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward;
            this.length = length; this.width = width; this.progress = Mathf.Clamp01(progress); this.origin = origin;
        }

        public void Resolve(int resolvedAttack)
        {
            // Retain the committed warning dimensions; the current frame may already be in recovery.
            hitAttack = resolvedAttack; hitCenter = center; hitDirection = direction;
            hitLength = length; hitWidth = width; hitOrigin = origin; hitTime = Time.time;
            bool lane = hitAttack == 2;
            int count = lane ? 7 : hitAttack == 1 ? 5 : 10;
            for (int i = 0; i < count; i++)
            {
                Vector3 position;
                if (lane)
                {
                    Vector3 start = hitCenter - (hitAttack == 2 ? hitDirection * hitLength * .5f : Vector3.zero);
                    position = start + hitDirection * (hitLength * (i + .5f) / count);
                }
                else
                {
                    float angle = i * Mathf.PI * 2 / count;
                    position = hitCenter + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * hitWidth * .72f;
                }
                Puff(position + Vector3.up * .12f, hitAttack == 5 ? 1.8f : 1.1f);
            }
        }

        public void Tick()
        {
            vertices.Clear(); colors.Clear(); indices.Clear();
            if (attack != 0) Warning();
            float age = Time.time - hitTime;
            if (age < Duration) Impact(age / Duration);
            renderer.enabled = vertices.Count > 0;
            if (!renderer.enabled) return;
            mesh.Clear(false); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
        }

        private void Warning()
        {
            Color warm = Color.Lerp(new Color(1, .72f, .13f), new Color(1, .25f, .14f), progress);
            Color fill = Violet; fill.a = Mathf.Lerp(.12f, .27f, progress);
            if (length > 0)
            {
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                Vector3 start = center - (attack == 2 ? direction * length * .5f : Vector3.zero);
                Vector3 end = start + direction * length;
                float half = width * .5f;
                Quad(start - side * half, end - side * half, end + side * half, start + side * half, fill);
                // Borders lie inside the true damage rectangle, including both end caps.
                LaneBorder(start, end, side, half, .32f, Ink, .01f);
                LaneBorder(start + direction * .09f, end - direction * .09f, side, half - .09f, .14f, warm, .02f);
                int count = Mathf.Max(2, Mathf.FloorToInt(length / 3));
                for (int i = 0; i < count; i++)
                {
                    float p = (i + .5f) / count;
                    Color mark = Cream; mark.a = p <= progress ? .95f : .24f;
                    Chevron(start + direction * (p * length), direction, Mathf.Min(.8f, width * .18f), mark);
                }
            }
            else
            {
                Disc(center, width, fill);
                Ring(center, width - .18f, .36f, Ink, .01f);
                Ring(center, width - .17f, .15f, warm, .02f);
                // A segmented countdown sits just inside the constant danger boundary.
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI * 2 / 16;
                    Vector3 axis = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    Vector3 side = Vector3.Cross(Vector3.up, axis) * Mathf.Min(.13f, width * .025f);
                    Color tick = Cream; tick.a = (i + 1) / 16f <= progress ? 1 : .18f;
                    Vector3 p = center + axis * (width - .58f);
                    Quad(p - side, p + side, p + side - axis * .32f, p - side - axis * .32f, tick, .03f);
                }
                for (int i = 0; i < 4; i++)
                {
                    Vector3 axis = Quaternion.Euler(0, i * 90, 0) * Vector3.forward;
                    Chevron(center + axis * width * .5f, attack == 4 || attack == 3 ? axis : -axis, Mathf.Min(.8f, width * .16f), Cream);
                }
                // A chunky four-point spell/impact mark, consistent with the comic contact bursts.
                Star(center, Mathf.Min(1.05f, width * .23f), Ink, .035f);
                Star(center, Mathf.Min(.77f, width * .17f), warm, .04f);
            }
            // Energy gathers at the body, visually connecting the authored attack pose to its destination.
            if (attack == 1 || attack == 4)
            {
                float size = Mathf.Lerp(.18f, .8f, progress * progress);
                Vector3 charge = origin + direction * 2;
                StarFacingCamera(charge, size, Ink);
                StarFacingCamera(charge, size * .72f, warm);
            }
        }

        private void Impact(float t)
        {
            float fade = 1 - Mathf.SmoothStep(0, 1, t);
            Color purple = new Color(.68f, .3f, 1, fade * .85f);
            Color cream = Cream; cream.a = fade;
            Color ink = Ink; ink.a *= fade;
            float pop = 1 - Mathf.Pow(1 - t, 3);
            float flash = Mathf.Clamp01(1 - t / .32f);
            bool reduced = feedback != null && feedback.ReduceFlashes;
            Color flashColor = reduced ? new Color(.25f, .48f, 1, flash * .06f)
                : new Color(.65f, .86f, 1, flash * .23f);
            if (hitAttack == 2)
            {
                Vector3 start = hitCenter - (hitAttack == 2 ? hitDirection * hitLength * .5f : Vector3.zero);
                Vector3 side = Vector3.Cross(Vector3.up, hitDirection);
                Quad(start - side * hitWidth * .5f, start + hitDirection * hitLength - side * hitWidth * .5f,
                    start + hitDirection * hitLength + side * hitWidth * .5f, start + side * hitWidth * .5f, flashColor, .035f);
                // Both strokes cover the resolved lane immediately; no travelling damage is implied.
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 a = start + side * (i * hitWidth * .22f);
                    Vector3 b = a + hitDirection * hitLength;
                    float stroke = Mathf.Lerp(.6f, .06f, t);
                    Quad(a - side * stroke, b - hitDirection * 1.2f, b, a + side * stroke, i == 0 ? cream : purple, .08f);
                }
                for (int i = 0; i < 5; i++)
                    Star(start + hitDirection * (hitLength * (i + .5f) / 5), (1 - t) * .9f, cream, .1f);
            }
            else
            {
                // The flash covers the exact committed circle; it is a single fading impact, not a strobe.
                Disc(hitCenter + Vector3.up * .035f, hitWidth, flashColor);
                // The outer ring confirms the entire instantaneous resolved footprint.
                Ring(hitCenter, hitWidth - .2f, .4f * fade, purple, .06f);
                float radius = Mathf.Lerp(hitWidth * .22f, hitWidth - .25f, pop);
                Ring(hitCenter, radius, Mathf.Lerp(.5f, .1f, t), ink, .07f);
                Ring(hitCenter, radius, Mathf.Lerp(.28f, .05f, t), cream, .08f);
                if (hitAttack == 3)
                {
                    // A complete circular wing cut, with rotating tapered crescents rather than a lane stroke.
                    for (int arc = 0; arc < 3; arc++)
                        for (int i = 0; i < 20; i++)
                        {
                            float a = arc * Mathf.PI * 2 / 3 + i * .065f + pop * 1.8f;
                            float b = a + .065f;
                            float outer = hitWidth * Mathf.Lerp(.65f, .96f, pop);
                            float stroke = Mathf.Sin((i + .5f) / 20 * Mathf.PI) * .85f * fade;
                            Vector3 v = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                            Vector3 w = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                            Quad(hitCenter + v * outer, hitCenter + w * outer,
                                hitCenter + w * (outer - stroke), hitCenter + v * (outer - stroke), cream, .14f);
                        }
                }
                if (hitAttack == 1 || hitAttack == 4)
                {
                    Lightning(t, reduced);
                }
                Star(hitCenter, hitWidth * .28f * (1 - t), ink, .1f);
                Star(hitCenter, hitWidth * .22f * (1 - t), reduced ? purple : cream, .11f);
            }
        }

        private void Lightning(float t, bool reduced)
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null) return;
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .82f, t));
            if (fade <= 0) return;
            // Larger circles get a bounded ring of strikes as well as the central discharge.
            int satellites = Mathf.Clamp(Mathf.CeilToInt(hitWidth * .5f), 3, 8);
            for (int bolt = 0; bolt <= satellites; bolt++)
            {
                float angle = bolt * Mathf.PI * 2 / satellites + .3f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 bottom = hitCenter + Vector3.up * .12f;
                if (bolt > 0) bottom += radial * hitWidth * .78f;
                float height = Mathf.Max(5, hitWidth * .8f) * (bolt == 0 ? 1.15f : .75f);
                Vector3 top = bolt == 0 && hitAttack == 1 ? hitOrigin : bottom + Vector3.up * height + radial * .6f;
                float seed = bolt * 3.17f + hitAttack * 1.91f;
                float stroke = Mathf.Clamp(hitWidth * .01f, .045f, .12f) * (bolt == 0 ? 1 : .65f);
                Bolt(top, bottom, stroke, seed, fade, t, reduced, camera);
                ElectricSplash(bottom, hitWidth * (bolt == 0 ? .25f : .14f), fade, t, reduced, camera);
            }
        }

        private void Bolt(Vector3 top, Vector3 bottom, float width, float seed, float fade, float age,
            bool reduced, UnityEngine.Camera camera)
        {
            Vector3 right = camera.transform.right;
            float spread = Mathf.Min(1.2f, Vector3.Distance(top, bottom) * .13f);
            for (int i = 0; i < 16; i++)
            {
                Vector3 a = BoltPoint(top, bottom, right, i / 16f, spread, seed, age);
                Vector3 b = BoltPoint(top, bottom, right, (i + 1) / 16f, spread, seed, age);
                ElectricStroke(a, b, width * Mathf.Lerp(.7f, 1, (i + .5f) / 16), fade, reduced, camera);
            }
            for (int branch = 0; branch < 3; branch++)
            {
                float p = .22f + branch * .24f;
                Vector3 start = BoltPoint(top, bottom, right, p, spread, seed, age);
                Vector3 end = start + right * ((branch % 2 == 0 ? 1 : -1) * spread * 2.2f)
                    + (bottom - top) * .22f;
                for (int i = 0; i < 5; i++)
                {
                    Vector3 a = BoltPoint(start, end, right, i / 5f, spread * .4f, seed + branch + 5, age);
                    Vector3 b = BoltPoint(start, end, right, (i + 1) / 5f, spread * .4f, seed + branch + 5, age);
                    ElectricStroke(a, b, width * .46f * (1 - i / 6f), fade * .75f, reduced, camera);
                }
            }
        }

        private static Vector3 BoltPoint(Vector3 top, Vector3 bottom, Vector3 right, float p, float spread, float seed, float age)
        {
            // Fixed knots keep the silhouette readable. A small continuous drift adds life without frame-random flicker.
            float kink = Mathf.Sin(p * 39 + seed) * .62f + Mathf.Sin(p * 73 + seed * 2) * .38f;
            float drift = Mathf.Sin(p * 18 + seed + age * 6) * .12f;
            return Vector3.Lerp(top, bottom, p) + right * ((kink + drift) * Mathf.Sin(p * Mathf.PI) * spread);
        }

        private void ElectricStroke(Vector3 a, Vector3 b, float width, float fade, bool reduced, UnityEngine.Camera camera)
        {
            Vector3 side = Vector3.Cross(b - a, camera.transform.forward).normalized;
            // Soft blue silhouette, cyan inner glow and a fine white-hot core, following the reference.
            Ribbon(a, b, side, width * 4.5f, new Color(.06f, .3f, 1, fade * .09f));
            Ribbon(a, b, side, width * 2.8f, new Color(.12f, .52f, 1, fade * .18f));
            Ribbon(a, b, side, width * 1.35f, new Color(.28f, .85f, 1, fade * .75f));
            Ribbon(a, b, side, width * .48f, reduced ? new Color(.4f, .8f, 1, fade) : new Color(.92f, 1, 1, fade));
        }

        private void ElectricSplash(Vector3 p, float radius, float fade, float age, bool reduced, UnityEngine.Camera camera)
        {
            // Low flared splinters, like the drawn blue crown at the base of the supplied lightning.
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2 / 9;
                Vector3 axis = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                float length = radius * Mathf.Lerp(.5f, 1, (Mathf.Sin(i * 7.1f) + 1) * .5f);
                Vector3 tip = p + axis * length * Mathf.Lerp(.65f, 1, age)
                    + Vector3.up * length * Mathf.Lerp(.3f, .8f, (Mathf.Sin(i * 4.7f) + 1) * .5f);
                Vector3 side = Vector3.Cross(tip - p, camera.transform.forward).normalized;
                Quad(p - side * radius * .08f, tip, p + axis * length * .4f, p + side * radius * .08f,
                    new Color(.15f, .55f, 1, fade * .65f));
                Triangle(p, tip * .72f + p * .28f, p + axis * length * .35f,
                    reduced ? new Color(.4f, .8f, 1, fade) : new Color(1, .94f, .69f, fade));
            }
        }

        private void LaneBorder(Vector3 start, Vector3 end, Vector3 side, float half, float stroke, Color color, float lift)
        {
            Quad(start - side * half, end - side * half, end - side * (half - stroke), start - side * (half - stroke), color, lift);
            Quad(start + side * half, end + side * half, end + side * (half - stroke), start + side * (half - stroke), color, lift);
            Quad(start - side * half, start + side * half, start + side * half + direction * stroke, start - side * half + direction * stroke, color, lift);
            Quad(end - side * half, end + side * half, end + side * half - direction * stroke, end - side * half - direction * stroke, color, lift);
        }
        private void Chevron(Vector3 p, Vector3 axis, float size, Color color)
        {
            Vector3 side = Vector3.Cross(Vector3.up, axis);
            Vector3 tip = p + axis * size * .55f, notch = p - axis * size * .1f;
            Triangle(tip, p - axis * size * .55f + side * size * .65f, notch, color, .04f);
            Triangle(tip, notch, p - axis * size * .55f - side * size * .65f, color, .04f);
        }
        private void Disc(Vector3 p, float radius, Color color)
        {
            for (int i = 0; i < 64; i++) Triangle(p, p + Radial(i) * radius, p + Radial(i + 1) * radius, color);
        }
        private void Ring(Vector3 p, float radius, float stroke, Color color, float lift)
        {
            for (int i = 0; i < 64; i++)
            {
                Vector3 a = Radial(i), b = Radial(i + 1);
                Quad(p + a * (radius - stroke * .5f), p + b * (radius - stroke * .5f),
                    p + b * (radius + stroke * .5f), p + a * (radius + stroke * .5f), color, lift);
            }
        }
        private static Vector3 Radial(int i) => new Vector3(Mathf.Cos(i * Mathf.PI / 32), 0, Mathf.Sin(i * Mathf.PI / 32));
        private void Star(Vector3 p, float size, Color color, float lift)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, b = (i + 1) * Mathf.PI / 4;
                Vector3 v = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * size * (i % 2 == 0 ? 1 : .28f);
                Vector3 w = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * size * (i % 2 == 0 ? .28f : 1);
                Triangle(p, p + v, p + w, color, lift);
            }
        }
        private void StarFacingCamera(Vector3 p, float size, Color color)
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null) return;
            Vector3 right = camera.transform.right, up = camera.transform.up;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, b = (i + 1) * Mathf.PI / 4;
                Triangle(p, p + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * size * (i % 2 == 0 ? 1 : .28f),
                    p + (right * Mathf.Cos(b) + up * Mathf.Sin(b)) * size * (i % 2 == 0 ? .28f : 1), color);
            }
        }
        private void Ribbon(Vector3 a, Vector3 b, Vector3 side, float width, Color color)
            => Quad(a - side * width, b - side * width, b + side * width, a + side * width, color);
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float lift = 0)
        { Triangle(a, b, c, color, lift); Triangle(a, c, d, color, lift); }
        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, float lift = 0)
        {
            Vector3 offset = Vector3.up * lift;
            vertices.Add(root.InverseTransformPoint(a + offset)); vertices.Add(root.InverseTransformPoint(b + offset));
            vertices.Add(root.InverseTransformPoint(c + offset));
            for (int i = 0; i < 3; i++) { colors.Add(color); indices.Add(indices.Count); }
        }
        private void Puff(Vector3 p, float scale)
        {
            foreach (var effect in dust)
            {
                if (effect == null || effect.IsAlive(true)) continue;
                effect.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.up));
                effect.transform.localScale = Vector3.one * scale; effect.Play(true); return;
            }
        }
        public void Clear()
        {
            attack = 0; hitTime = -10; renderer.enabled = false;
            foreach (var effect in dust) if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
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
