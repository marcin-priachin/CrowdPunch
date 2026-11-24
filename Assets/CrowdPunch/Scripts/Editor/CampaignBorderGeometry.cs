using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    internal static class CampaignBorderGeometry
    {
        internal const float Depth = 1.5f;
        internal const float Height = 1.7985f;
        private const float JointOverlap = .12f;

        internal static Vector2[] FloorOutline(MeshCollider floor)
        {
            var vertices = floor.sharedMesh.vertices.Select(floor.transform.TransformPoint).ToArray();
            float top = vertices.Max(v => v.y);
            var points = vertices.Select(v => new Vector2Int(Mathf.RoundToInt(v.x * 10000), Mathf.RoundToInt(v.z * 10000))).ToArray();
            var positions = new Dictionary<Vector2Int, Vector2>();
            var counts = new Dictionary<(Vector2Int, Vector2Int), int>();
            var indices = floor.sharedMesh.triangles;
            void Count(int a, int b)
            {
                var first = points[a]; var second = points[b];
                if (first == second) return;
                positions[first] = new Vector2(vertices[a].x, vertices[a].z);
                positions[second] = new Vector2(vertices[b].x, vertices[b].z);
                var edge = first.x < second.x || first.x == second.x && first.y < second.y
                    ? (first, second) : (second, first);
                counts.TryGetValue(edge, out int count);
                counts[edge] = count + 1;
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (top - vertices[a].y > .001f || top - vertices[b].y > .001f || top - vertices[c].y > .001f) continue;
                Count(a, b); Count(b, c); Count(c, a);
            }

            var neighbors = new Dictionary<Vector2Int, List<Vector2Int>>();
            void Link(Vector2Int a, Vector2Int b)
            {
                if (!neighbors.TryGetValue(a, out var links)) neighbors[a] = links = new List<Vector2Int>(2);
                links.Add(b);
            }
            foreach (var edge in counts.Where(pair => pair.Value == 1).Select(pair => pair.Key))
            {
                Link(edge.Item1, edge.Item2);
                Link(edge.Item2, edge.Item1);
            }
            if (neighbors.Count < 3 || neighbors.Values.Any(links => links.Count != 2))
                throw new InvalidOperationException("The arena floor must have a closed top outline.");
            var start = neighbors.Keys.OrderBy(p => p.y).ThenBy(p => p.x).First();
            var previous = start;
            var current = neighbors[start][0];
            var outline = new List<Vector2> { positions[start] };
            while (current != start && outline.Count < neighbors.Count)
            {
                outline.Add(positions[current]);
                var next = neighbors[current][0] == previous ? neighbors[current][1] : neighbors[current][0];
                previous = current;
                current = next;
            }
            if (current != start || outline.Count != neighbors.Count)
                throw new InvalidOperationException("The arena floor must have one enclosed boundary.");
            float area = 0;
            for (int i = 0; i < outline.Count; i++) area += Cross(outline[i], outline[(i + 1) % outline.Count]);
            if (area < 0) outline.Reverse(1, outline.Count - 1);
            return outline.ToArray();
        }

        internal static Vector2[] RoundCourtOutline(IEnumerable<Transform> rim, Vector2 center)
        {
            // The round floor continues under the boss route. The original inner rim defines the player court.
            var planes = rim.Select(t =>
            {
                var position = new Vector2(t.position.x, t.position.z);
                var normal = new Vector2(t.right.x, t.right.z).normalized;
                if (Vector2.Dot(normal, position - center) < 0) normal = -normal;
                return (normal, distance: Vector2.Dot(normal, position) - t.lossyScale.x * .5f);
            }).OrderBy(p => Mathf.Atan2(p.normal.y, p.normal.x)).ToArray();
            var outline = new Vector2[planes.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                var a = planes[(i + outline.Length - 1) % outline.Length];
                var b = planes[i];
                float denominator = Cross(a.normal, b.normal);
                if (denominator < .001f) throw new InvalidOperationException("The round court has invalid perimeter planes.");
                outline[i] = new Vector2(a.distance * b.normal.y - a.normal.y * b.distance,
                    a.normal.x * b.distance - a.distance * b.normal.x) / denominator;
            }
            return outline;
        }

        internal static Vector2 Normal(Vector2 a, Vector2 b)
        {
            var edge = (b - a).normalized;
            return new Vector2(edge.y, -edge.x);
        }

        internal static Vector2[] SplitLongEdges(Vector2[] outline, Transform[] rim)
        {
            var split = new List<Vector2>(rim.Length);
            for (int i = 0; i < outline.Length; i++)
            {
                var start = outline[i]; var end = outline[(i + 1) % outline.Length];
                var direction = (end - start).normalized;
                var normal = Normal(start, end);
                float length = (end - start).magnitude;
                var centers = rim.Where(t => Mathf.Abs(Vector2.Dot(new Vector2(t.forward.x, t.forward.z), direction)) > .999f)
                    .Select(t => new Vector2(t.position.x, t.position.z) - start)
                    .Where(p => Mathf.Abs(Vector2.Dot(p, normal)) < Depth)
                    .Select(p => Vector2.Dot(p, direction)).Where(distance => distance > .05f && distance < length - .05f)
                    .OrderBy(distance => distance).ToArray();
                split.Add(start);
                // The large Dino courts already divide their long walls into shorter authored sections.
                for (int segment = 1; segment < centers.Length; segment++)
                    split.Add(start + direction * ((centers[segment - 1] + centers[segment]) * .5f));
            }
            return split.ToArray();
        }

        internal static Vector2[] Outside(Vector2[] outline)
        {
            var outside = new Vector2[outline.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                var previous = Normal(outline[(i + outline.Length - 1) % outline.Length], outline[i]);
                var next = Normal(outline[i], outline[(i + 1) % outline.Length]);
                outside[i] = outline[i] + (previous + next) * (Depth / (1 + Vector2.Dot(previous, next)));
            }
            return outside;
        }

        internal static Mesh Wall(string name, Vector2[] outline, Vector2[] outside, Vector2 origin, Quaternion rotation, int edge, bool collision)
        {
            int next = (edge + 1) % outline.Length;
            var start = outline[edge]; var end = outline[next];
            var outerStart = outside[edge]; var outerEnd = outside[next];
            if (collision)
            {
                var direction = (end - start).normalized;
                float overlap = Mathf.Min(JointOverlap, (end - start).magnitude * .2f);
                if (Cross(start - outline[(edge + outline.Length - 1) % outline.Length], direction) >= 0)
                    start -= direction * overlap;
                if (Cross(direction, outline[(next + 1) % outline.Length] - end) >= 0)
                    end += direction * overlap;
                outerStart -= direction * overlap;
                outerEnd += direction * overlap;
            }
            var inverse = Quaternion.Inverse(rotation);
            Vector2 Local(Vector2 point)
            {
                var offset = point - origin;
                var local = inverse * new Vector3(offset.x, 0, offset.y);
                return new Vector2(local.x, local.z);
            }
            return Prism(name, new[] { Local(start), Local(end), Local(outerEnd), Local(outerStart) }, !collision);
        }

        internal static Mesh GateWall() => Prism("Gate wall unit prism", new[] { new Vector2(-.5f, -.5f),
            new Vector2(.5f, -.5f), new Vector2(.5f, .5f), new Vector2(-.5f, .5f) }, true, 1f);

        internal static Mesh GateJamb(string name, int direction, float frontSpan, float rearSpan)
        {
            float halfDepth = Depth * .5f;
            return Prism(name, new[] { new Vector2(0, -halfDepth), new Vector2(direction * frontSpan, -halfDepth),
                new Vector2(direction * rearSpan, halfDepth), new Vector2(0, halfDepth) }, true);
        }

        private static Mesh Prism(string name, Vector2[] corners, bool unwrap, float height = Height)
        {
            var vertices = new List<Vector3>(24);
            var indices = new List<int>(36);
            var uv = new List<Vector2>(24);
            var centroid = corners.Aggregate(Vector2.zero, (sum, p) => sum + p) / corners.Length;
            var center = new Vector3(centroid.x, height * .5f, centroid.y);
            Vector3 Point(int corner, float y) => new Vector3(corners[corner].x, y, corners[corner].y);
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c + d) * .25f - center) < 0) (b, d) = (d, b);
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                indices.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            }
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                Quad(Point(i, 0), Point(next, 0), Point(next, height), Point(i, height));
            }
            Quad(Point(0, height), Point(1, height), Point(2, height), Point(3, height));
            Quad(Point(0, 0), Point(1, 0), Point(2, 0), Point(3, 0));
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.SetUVs(0, uv);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (unwrap && !Unwrapping.GenerateSecondaryUVSet(mesh))
                throw new InvalidOperationException("Could not generate lightmap UVs for " + name);
            return mesh;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
