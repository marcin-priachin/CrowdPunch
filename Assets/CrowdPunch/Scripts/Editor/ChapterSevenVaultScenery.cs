using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CrowdPunch.Editor.ChapterSevenVaultAssets;

namespace CrowdPunch.Editor
{
    // VISION-004..006: frozen construction surrounds, but never changes, the combat envelope.
    internal static class ChapterSevenVaultScenery
    {
        internal static void Build(Transform root, Bounds[] floors, int stage)
        {
            var bounds = floors[0];
            foreach (var floor in floors.Skip(1)) bounds.Encapsulate(floor);
            float y = floors.Min(f => f.max.y) - .12f;
            Landscape(root, bounds, y, stage);
            int bays = Mathf.Max(3, Mathf.CeilToInt(bounds.size.z / 22));
            foreach (int side in new[] {-1, 1})
            {
                Shape(root, "Connected vault side gallery", new Vector3(bounds.center.x + side * (bounds.extents.x + 14), y, bounds.center.z),
                    new Vector3(4, 4, bounds.size.z + 18), Surface("Limestone", true));
                Shape(root, "Continuous gallery frost cap", new Vector3(bounds.center.x + side * (bounds.extents.x + 14), y + 4, bounds.center.z),
                    new Vector3(4.5f, .24f, bounds.size.z + 18.5f), Surface("Chalk", true));
                for (int i = 0; i < bays; i++)
                {
                    float z = Mathf.Lerp(bounds.min.z - 3, bounds.max.z + 3, (i + .5f) / bays);
                    var p = new Vector3(bounds.center.x + side * (bounds.extents.x + 14), y, z);
                    Buttress(root, p + new Vector3(-side * 4, 0, 0), 5.6f, -side * 90);
                    Crystal(root, p + new Vector3(side * 8, 0, 6), 3.3f + i % 3, i * 71 + stage);
                    if (stage == 62 || i == 0) Vault(root, p + new Vector3(side * 9, 0, -4), 8 + i % 3, -side * 90);
                }
            }
            // Low near-side buildings and a closed return gallery leave the entry view clear.
            foreach (int side in new[] {-1, 1})
            {
                int count = Mathf.Max(3, Mathf.CeilToInt(bounds.size.x / 22));
                Shape(root, "Vault return wall", new Vector3(bounds.center.x, y, bounds.center.z + side * (bounds.extents.z + 18)),
                    new Vector3(bounds.size.x + 32, 3.2f, 3), Surface("Limestone", true));
                Shape(root, "Return wall frost cap", new Vector3(bounds.center.x, y + 3.2f, bounds.center.z + side * (bounds.extents.z + 18)),
                    new Vector3(bounds.size.x + 32.4f, .22f, 3.4f), Surface("Chalk", true));
                for (int i = 0; i < count; i++)
                {
                    float x = Mathf.Lerp(bounds.min.x - 8, bounds.max.x + 8, (i + .5f) / count);
                    Buttress(root, new Vector3(x, y, bounds.center.z + side * (bounds.extents.z + 15)), side < 0 ? 3.4f : 6, side < 0 ? 180 : 0);
                    Crystal(root, new Vector3(x + 4, y, bounds.center.z + side * (bounds.extents.z + 26)), 3 + i % 3, stage + i * 41);
                }
            }
            Vector3 North(float x = 0, float z = 30) => new Vector3(bounds.center.x + x, y, bounds.max.z + z);
            switch (stage)
            {
                case 61: Vault(root, North(), 14, 0); Crystal(root, North(-15, 25), 6, 61); break;
                case 62:
                    // Six scenery landmarks follow the actual six authored room floors.
                    var rooms = floors.OrderBy(f => f.center.z).ToArray();
                    for (int i = 0; i < rooms.Length; i++)
                    {
                        var p = new Vector3(bounds.min.x - 25, y, rooms[i].center.z);
                        if (i % 3 == 0) Vault(root, p, 8 + i, 90);
                        else if (i % 3 == 1) Crystal(root, p, 5 + i, 62 + i);
                        else Tower(root, p, 11 + i, 90);
                    }
                    Tower(root, North(12), 19, 0); break;
                case 63:
                    foreach (int side in new[] {-1, 1})
                    {
                        var p = new Vector3(bounds.center.x + side * (bounds.extents.x + 26), y, bounds.center.z);
                        Shape(root, "Recessed meltwater channel", p, new Vector3(4, .06f, bounds.size.z + 10), Surface("Slate"));
                        foreach (int bank in new[] {-1, 1}) Shape(root, "Meltwater stone bank", p + Vector3.right * bank * 2.4f, new Vector3(.7f, .6f, bounds.size.z + 10), Surface("Limestone", true));
                    }
                    Vault(root, North(), 16, 0); break;
                case 64:
                    Vault(root, North(), 18, 0);
                    foreach (int side in new[] {-1, 1}) Tower(root, North(side * 18, 35), 17, side * 12);
                    break;
                case 65:
                    Tower(root, North(), 20, 0);
                    foreach (int side in new[] {-1, 1}) Tower(root, new Vector3(bounds.center.x + side * (bounds.extents.x + 27), y, bounds.center.z), 15, -side * 90);
                    break;
                case 66:
                    Vault(root, North(), 16, 0);
                    var wheel = Shape(root, "Inert distant regulator wheel", North(0, 27) + Vector3.up * 8, new Vector3(6, .5f, 6), Surface("Ochre", true), true);
                    wheel.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    foreach (int side in new[] {-1, 1}) Tower(root, North(side * 17, 34), 15, side * 10);
                    break;
                case 67:
                    foreach (int side in new[] {-1, 1}) for (int i = 0; i < 4; i++)
                    {
                        var p = new Vector3(bounds.center.x + side * (bounds.extents.x + 7), y, bounds.min.z + 5 + i * bounds.size.z / 4);
                        Shape(root, "Low rime garden bed", p, new Vector3(3.4f, .35f, 4.5f), Surface("Limestone", true));
                        Crystal(root, p + Vector3.up * .35f, 1.3f, i + stage);
                        Model(root, "kenney_nature-kit", "plant_bush", p + new Vector3(0, .35f, 1.1f), new Vector3(2.5f, 1.2f, 2), Surface("Sage", true), i * 47);
                    }
                    Vault(root, North(), 12, 0); break;
                case 68:
                    foreach (int side in new[] {-1, 1}) {Tower(root, North(side * 32, 30), 25, side * 8); Crystal(root, North(side * 18, 46), 18, side + 68);}
                    Vault(root, North(0, 40), 21, 0); break;
                case 69:
                    Crystal(root, North(-17, 29), 12, 69); Crystal(root, North(18, 40), 15, 70);
                    Vault(root, North(0, 27), 15, 0); break;
                case 70:
                    foreach (int side in new[] {-1, 1}) {Tower(root, North(side * 20, 31), 23, side * 8); Vault(root, North(side * 9, 25), 12, side * 5);}
                    Vault(root, North(0, 47), 25, 0); break;
            }
        }

        private static void Buttress(Transform parent, Vector3 p, float height, float yaw)
        {
            var root = new GameObject("Frozen vault buttress").transform; root.SetParent(parent, false); root.localPosition = p; root.localRotation = Quaternion.Euler(0, yaw, 0);
            Shape(root, "Buttress broad footing", Vector3.zero, new Vector3(2, .5f, 4), Surface("Rock", true));
            Shape(root, "Buttress pier", Vector3.up * .5f, new Vector3(1.3f, height - .5f, 2.6f), Surface("Limestone", true));
            Shape(root, "Buttress frost crown", Vector3.up * height, new Vector3(1.6f, .22f, 2.9f), Surface("Chalk", true));
            Shape(root, "Recessed brass seal", new Vector3(0, height * .65f, -1.32f), new Vector3(.45f, .6f, .06f), Surface("Ochre", true));
        }

        private static void Vault(Transform parent, Vector3 p, float size, float yaw)
        {
            var root = new GameObject("Complete frozen vault entrance").transform; root.SetParent(parent, false); root.localPosition = p; root.localRotation = Quaternion.Euler(0, yaw, 0);
            float radius = size * .30f, spring = size * .37f, depth = size * .5f;
            // Full side/rear walls and a fitted cap remain complete when foreground cutaways open.
            Shape(root, "Closed vault rear", new Vector3(0, 0, depth * .5f), new Vector3(size, size * .76f, .6f), Surface("Rock", true));
            foreach (int side in new[] {-1, 1})
            {
                Shape(root, "Vault returning wall", new Vector3(side * (size * .5f - .3f), 0, .15f), new Vector3(.6f, size * .76f, depth - .3f), Surface("Limestone", true));
                Shape(root, "Vault doorway pier", new Vector3(side * (radius + size * .10f), 0, -depth * .5f), new Vector3(size * .20f, spring, .8f), Surface("Limestone", true));
            }
            Shape(root, "Vault complete frost roof", new Vector3(0, size * .76f, .15f), new Vector3(size + .35f, .28f, depth + .6f), Surface("Chalk", true));
            Shape(root, "Dark inset vault doors", new Vector3(0, 0, -depth * .5f + .35f), new Vector3(radius * 2, spring + radius, .18f), Surface("Slate", true));
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI / 9 + .018f, b = (i + 1) * Mathf.PI / 9 - .018f;
                var v = new List<Vector3>(); var t = new List<int>();
                Vector3 Point(float angle, float r, float z) => new Vector3(Mathf.Cos(angle) * r, spring + Mathf.Sin(angle) * r, z);
                float outer = radius + size * .20f;
                var aa = Point(a, radius, -.5f); var ab = Point(b, radius, -.5f); var ba = Point(a, outer, -.5f); var bb = Point(b, outer, -.5f);
                Quad(v, t, aa, ab, bb, ba); Quad(v, t, aa + Vector3.forward, ba + Vector3.forward, bb + Vector3.forward, ab + Vector3.forward);
                Quad(v, t, aa, aa + Vector3.forward, ab + Vector3.forward, ab); Quad(v, t, ba, bb, bb + Vector3.forward, ba + Vector3.forward);
                Quad(v, t, aa, ba, ba + Vector3.forward, aa + Vector3.forward); Quad(v, t, ab, ab + Vector3.forward, bb + Vector3.forward, bb);
                var wedge = MeshObject(root, "Drawn vault arch stone", SaveMesh($"Arch {size} stone {i}", v, t), Surface(i == 4 ? "Ochre" : "Limestone", true));
                wedge.transform.localPosition = new Vector3(0, 0, -depth * .5f - .12f);
            }
            Shape(root, "Vault door brass centre", new Vector3(0, 0, -depth * .5f + .23f), new Vector3(.12f, spring, .06f), Surface("Ochre", true));
            ChapterSevenVaultJoints.Apply(root);
        }

        private static void Tower(Transform root, Vector3 p, float height, float yaw)
        {
            Model(root, "kenney_castle-kit/Models/FBX format", "tower-square", p, new Vector3(height * .43f, height, height * .43f), Surface("Limestone", true), yaw);
        }

        private static void Crystal(Transform root, Vector3 p, float height, int seed)
        {
            for (int i = 0; i < 3; i++)
            {
                float h = height * (i == 0 ? 1 : .62f);
                var crystal = MeshObject(root, "Margin ice crystal", CrystalMesh(), Surface("Crystal", true));
                crystal.transform.localPosition = p + new Vector3((i - 1) * height * .28f, h * .5f, (i % 2) * height * .16f);
                crystal.transform.localScale = new Vector3(h * .38f, h, h * .38f); crystal.transform.localRotation = Quaternion.Euler(0, seed * 31 + i * 37, 0);
            }
        }

        private static void Landscape(Transform root, Bounds b, float y, int stage)
        {
            var v = new List<Vector3>(); var t = new List<int>(); const int cells = 64;
            for (int z = 0; z <= cells; z++) for (int x = 0; x <= cells; x++) v.Add(new Vector3(x / (float)cells - .5f, 0, z / (float)cells - .5f));
            for (int z = 0; z < cells; z++) for (int x = 0; x < cells; x++) {int i = z * (cells + 1) + x; t.AddRange(new[] {i, i + cells + 1, i + 1, i + 1, i + cells + 1, i + cells + 2});}
            var earth = MeshObject(root, "Continuous frost earth", SaveMesh("Subdivided frost earth", v, t), Surface("Earth"));
            earth.transform.localPosition = new Vector3(b.center.x, y, b.center.z); earth.transform.localScale = new Vector3(b.size.x + 500, 1, b.size.z + 500);
            for (int layer = 0; layer < 2; layer++)
            {
                v = new List<Vector3>(); t = new List<int>(); const int count = 64;
                Vector3 P(int i, float distance, float h) {float a = i * Mathf.PI * 2 / count, s = Mathf.Sin(a), c = Mathf.Cos(a); return new Vector3(b.center.x + Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), .5f) * (b.extents.x + distance), y + h, b.center.z + Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), .5f) * (b.extents.z + distance));}
                float offset = layer * 47;
                for (int i = 0; i < count; i++)
                {
                    float h = 20 + layer * 12 + Mathf.Floor((Mathf.Sin(i * 1.7f + stage) + 1) * 4) * 2;
                    float next = 20 + layer * 12 + Mathf.Floor((Mathf.Sin((i + 1) * 1.7f + stage) + 1) * 4) * 2;
                    Quad(v, t, P(i, 44 + offset, 0), P(i, 59 + offset, h), P(i + 1, 59 + offset, next), P(i + 1, 44 + offset, 0));
                    Quad(v, t, P(i, 59 + offset, h), P(i, 74 + offset, h - 2), P(i + 1, 74 + offset, next - 2), P(i + 1, 59 + offset, next));
                    Quad(v, t, P(i, 74 + offset, h - 2), P(i, 115 + offset, 0), P(i + 1, 115 + offset, 0), P(i + 1, 74 + offset, next - 2));
                }
                MeshObject(root, "Closed glacial skyline " + layer, SaveMesh($"Glacial skyline {stage} layer {layer}", v, t), Surface("Cliff", true, layer == 1));
            }
        }
    }
}
