using System;
using System.Collections.Generic;
using System.Linq;
using CrowdPunch.Mono.Levels;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // VISION-005/006: retain each convex court separately, including room/track layouts.
    public static class SceneryHazeFloorOutline
    {
        public static Vector4[] Build(IEnumerable<MeshRenderer> floors)
        {
            var edges = new List<Vector4>();
            foreach (var floor in floors)
            {
                var points = floor.GetComponent<MeshFilter>().sharedMesh.vertices
                    .Select(floor.transform.TransformPoint).Select(p => new Vector2(p.x, p.z))
                    .Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToArray();
                var hull = new List<Vector2>();
                foreach (var point in points) Append(hull, point, 0);
                int lower = hull.Count;
                for (int i = points.Length - 2; i >= 0; i--) Append(hull, points[i], lower - 1);
                if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
                if (hull.Count < 3) throw new InvalidOperationException("Invalid haze floor: " + floor.name);
                for (int i = 0; i < hull.Count; i++)
                {
                    var a = hull[i]; var b = hull[(i + 1) % hull.Count];
                    edges.Add(new Vector4(a.x, a.y, b.x, b.y));
                }
            }
            if (edges.Count > CelLevelLighting.MaxHazeEdges)
                throw new InvalidOperationException("Scenery haze floor outlines exceed shader capacity.");
            return edges.ToArray();
        }

        private static void Append(List<Vector2> hull, Vector2 point, int start)
        {
            while (hull.Count >= start + 2)
            {
                var a = hull[hull.Count - 1] - hull[hull.Count - 2];
                var b = point - hull[hull.Count - 1];
                if (a.x * b.y - a.y * b.x > .00001f) break;
                hull.RemoveAt(hull.Count - 1);
            }
            hull.Add(point);
        }
    }
}
