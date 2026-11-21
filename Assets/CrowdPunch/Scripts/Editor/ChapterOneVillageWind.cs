using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    internal static class ChapterOneVillageWind
    {
        private const string MeshFolder = ChapterOneVillageAssets.Output + "/Landscape/Wind";
        private const string WindMeshSuffix = " village wind";
        // Matches the UV8 data tags in PaintedFoundry.shader.
        private const int WindChannel = 7;
        private const float FoliageTag = 67;
        private const float RotorTag = 68;

        internal static void Apply(Transform scenery)
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder(ChapterOneVillageAssets.Output + "/Landscape", "Wind");

            foreach (var plant in scenery.GetComponentsInChildren<Transform>(true))
            {
                bool tree = plant.name.StartsWith("CommonTree_", StringComparison.Ordinal) &&
                    plant.name.Length == "CommonTree_1".Length;
                if (!tree && plant.name != "Bush_Common" && plant.name != "Bush_Common_Flowers") continue;
                foreach (var filter in plant.GetComponentsInChildren<MeshFilter>(true))
                    ApplyFoliage(filter, tree);
            }

            var axle = scenery.Find("Windmill axle");
            if (axle == null) return;
            foreach (Transform child in scenery)
            {
                if (child.name != "Windmill timber sail" && child.name != "Cream windmill sail") continue;
                var renderer = child.GetComponent<MeshRenderer>();
                ApplySail(child.GetComponent<MeshFilter>(), axle.position, child.name == "Cream windmill sail");
                // The baked-only lighting policy cannot follow the rotor. Keep the sail's
                // receiver chart and leave its old fixed silhouette out of the new bake.
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void ApplyFoliage(MeshFilter filter, bool tree)
        {
            var source = filter.sharedMesh;
            if (IsWindMesh(source)) return;
            var path = MeshPath(source, tree ? "Tree" : "Bush");
            var windMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (windMesh == null)
            {
                var bounds = source.bounds;
                float amplitude = tree ? .13f : .055f;
                var data = new List<Vector4>(source.vertexCount);
                foreach (var vertex in source.vertices)
                {
                    var height = Mathf.InverseLerp(bounds.min.y, bounds.max.y, vertex.y);
                    var weight = Mathf.InverseLerp(.15f, 1f, height);
                    data.Add(new Vector4(amplitude * weight * weight, 0, 0, FoliageTag));
                }
                bounds.Expand(new Vector3(.6f, 0, .6f));
                windMesh = CreateMesh(source, path, data, bounds);
            }
            filter.sharedMesh = windMesh;
        }

        private static void ApplySail(MeshFilter filter, Vector3 axle, bool cloth)
        {
            var source = filter.sharedMesh;
            if (IsWindMesh(source)) return;
            var sail = filter.transform;
            int restAngle = Mathf.RoundToInt(sail.eulerAngles.z) % 360;
            var path = MeshPath(source, (cloth ? "Cloth" : "Timber") + restAngle);
            var windMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (windMesh == null)
            {
                var data = new List<Vector4>(source.vertexCount);
                float radius = 0;
                foreach (var vertex in source.vertices)
                {
                    Vector2 offset = sail.TransformPoint(vertex) - axle;
                    data.Add(new Vector4(offset.x, offset.y, 0, RotorTag));
                    radius = Mathf.Max(radius, offset.magnitude);
                }
                // A full turn occupies a disk around the axle in the plane of the sail.
                var center = sail.InverseTransformPoint(new Vector3(axle.x, axle.y, sail.position.z));
                var scale = sail.lossyScale;
                var bounds = new Bounds(center, new Vector3(2 * radius / scale.x,
                    2 * radius / scale.y, source.bounds.size.z));
                windMesh = CreateMesh(source, path, data, bounds);
            }
            filter.sharedMesh = windMesh;
        }

        private static bool IsWindMesh(Mesh mesh) => mesh.name.Contains(WindMeshSuffix) &&
            mesh.HasVertexAttribute(VertexAttribute.TexCoord7);

        private static string MeshPath(Mesh mesh, string part)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId);
            return $"{MeshFolder}/{part}_{Hash128.Compute(guid + "/" + localId)}.asset";
        }

        private static Mesh CreateMesh(Mesh source, string path, List<Vector4> data, Bounds bounds)
        {
            if (source.HasVertexAttribute(VertexAttribute.TexCoord7))
                throw new InvalidOperationException("Village wind meshes need a free UV8 channel: " + source.name);
            var mesh = Object.Instantiate(source);
            mesh.name = source.name + WindMeshSuffix;
            mesh.SetUVs(WindChannel, data);
            mesh.bounds = bounds;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
