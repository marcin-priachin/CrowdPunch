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
        private readonly List<Vector3> vertices = new List<Vector3>(8192);
        private readonly List<Color> colors = new List<Color>(8192);
        private readonly List<int> indices = new List<int>(8192);
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
            bool lane = hitAttack == 2 || hitAttack == 3;
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
                    Chevron(center + axis * width * .5f, attack == 4 ? axis : -axis, Mathf.Min(.8f, width * .16f), Cream);
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
            if (hitAttack == 2 || hitAttack == 3)
            {
                Vector3 start = hitCenter - (hitAttack == 2 ? hitDirection * hitLength * .5f : Vector3.zero);
                Vector3 side = Vector3.Cross(Vector3.up, hitDirection);
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
                // The outer ring confirms the entire instantaneous resolved footprint.
                Ring(hitCenter, hitWidth - .2f, .4f * fade, purple, .06f);
                float radius = Mathf.Lerp(hitWidth * .22f, hitWidth - .25f, pop);
                Ring(hitCenter, radius, Mathf.Lerp(.5f, .1f, t), ink, .07f);
                Ring(hitCenter, radius, Mathf.Lerp(.28f, .05f, t), cream, .08f);
                if (hitAttack == 1)
                {
                    Vector3 top = hitOrigin;
                    Vector3 bottom = hitCenter + Vector3.up * .15f;
                    Vector3 side = Vector3.Cross((bottom - top).normalized, UnityEngine.Camera.main != null
                        ? UnityEngine.Camera.main.transform.forward : Vector3.forward).normalized;
                    Vector3 bend = Vector3.Lerp(top, bottom, .52f) + side * 1.2f;
                    Ribbon(top, bend, side, .6f * fade, purple);
                    Ribbon(bend, bottom, side, .45f * fade, cream);
                }
                Star(hitCenter, Mathf.Min(2.4f, hitWidth * .45f) * (1 - t), ink, .1f);
                Star(hitCenter, Mathf.Min(1.9f, hitWidth * .36f) * (1 - t), cream, .11f);
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
            => Quad(a - side * width, b - side * width * .3f, b + side * width * .3f, a + side * width, color);
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
