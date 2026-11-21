using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Editor
{
    internal static class ChapterOneVillageGateGable
    {
        private const string GableName = "Limestone gatehouse gables";

        internal static void Apply(Transform scenery, int stage)
        {
            var gate = scenery.Find("gate");
            if (gate == null || gate.Find(GableName) != null) return;
            var facade = gate.GetComponentInChildren<MeshRenderer>();
            // The roof sits on the middle of the gate mesh, with its eaves above the lower facades.
            var roof = scenery.Cast<Transform>().Where(t => t.name == "Roof_RoundTiles_8x8")
                .SelectMany(t => t.GetComponentsInChildren<MeshRenderer>())
                .Where(r => r.shadowCastingMode != ShadowCastingMode.ShadowsOnly)
                .Single(r => Mathf.Abs(r.bounds.center.x - facade.bounds.center.x) < .05f &&
                    Mathf.Abs(r.bounds.center.z - facade.bounds.center.z) < .05f &&
                    Mathf.Abs(r.bounds.min.y - facade.bounds.max.y) < .05f);

            string path = ChapterOneVillageAssets.Output + $"/Landscape/GatehouseGables_{stage:00}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = Build(facade.bounds, roof.bounds);
                AssetDatabase.CreateAsset(mesh, path);
            }

            var gable = new GameObject(GableName, typeof(MeshFilter), typeof(MeshRenderer));
            gable.transform.position = new Vector3(roof.bounds.center.x, roof.bounds.min.y, roof.bounds.center.z);
            gable.transform.SetParent(gate, true);
            gable.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gable.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = facade.sharedMaterial;
            renderer.forceMeshLod = 0;
            gable.isStatic = true;
        }

        private static Mesh Build(Bounds facade, Bounds roof)
        {
            float halfWidth = facade.extents.x - .015f;
            float halfDepth = facade.extents.z - .02f;
            // The imported gate's front and rear walls end below its central crenellation.
            float bottom = facade.min.y + facade.size.y * .76f - roof.min.y;
            // The intermediate points follow the curved tile underside. Each joins inside
            // the ceramic thickness at the gate wall, leaving the eaves proud of the stone.
            var outline = new List<Vector2> { new(-halfWidth, bottom), new(halfWidth, bottom) };
            var profile = new[] { .045f, .14f, .282f, .435f, .592f, .758f, .927f };
            for (int i = 0; i < profile.Length; i++)
            {
                float x = i == 0 ? halfWidth : roof.extents.x * (6 - i) / 6f;
                outline.Add(new Vector2(x, roof.size.y * profile[i]));
            }
            for (int i = profile.Length - 2; i >= 0; i--)
            {
                float x = i == 0 ? halfWidth : roof.extents.x * (6 - i) / 6f;
                outline.Add(new Vector2(-x, roof.size.y * profile[i]));
            }

            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * halfDepth;
                int start = vertices.Count;
                vertices.Add(new Vector3(0, bottom * .5f, z));
                foreach (var point in outline) vertices.Add(new Vector3(point.x, point.y, z));
                for (int i = 0; i < outline.Count; i++)
                {
                    int a = start + 1 + i;
                    int b = start + 1 + (i + 1) % outline.Count;
                    indices.AddRange(side < 0 ? new[] { start, b, a } : new[] { start, a, b });
                }
            }
            for (int i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                int start = vertices.Count;
                vertices.Add(new Vector3(a.x, a.y, -halfDepth));
                vertices.Add(new Vector3(b.x, b.y, -halfDepth));
                vertices.Add(new Vector3(b.x, b.y, halfDepth));
                vertices.Add(new Vector3(a.x, a.y, halfDepth));
                indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var mesh = new Mesh { name = GableName };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
