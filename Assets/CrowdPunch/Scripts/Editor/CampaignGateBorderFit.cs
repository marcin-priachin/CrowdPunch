using System;
using System.Collections.Generic;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // BARRICADE-003/004/006/007: keep the existing gate entities, room links, art and hit rules.
    internal static class CampaignGateBorderFit
    {
        internal static int Apply(Scene scene, Vector2[] outline, float floor, Material wallMaterial,
            Func<Mesh, Mesh> saveMesh, bool fitWideOpening)
        {
            var roots = scene.GetRootGameObjects();
            var gates = roots.SelectMany(g => g.GetComponentsInChildren<BarricadeAuthoring>(true))
                .Where(g => g.name == "Barricade" || g.name.StartsWith("Room barricade ", StringComparison.Ordinal))
                .OrderBy(g => g.transform.position.z).ToArray();
            if (gates.Length == 0) return 0;

            var arena = roots.SelectMany(g => g.GetComponentsInChildren<ArenaAuthoring>(true)).Single();
            var sides = arena.GetComponentsInChildren<SolidObstacleAuthoring>(true)
                .Where(s => s.name.StartsWith("Gate terrain ", StringComparison.Ordinal)).ToArray();
            Mesh unit = null;
            foreach (var gate in gates)
            {
                if (Quaternion.Angle(gate.transform.rotation, Quaternion.identity) > .01f)
                    throw new InvalidOperationException("Expected a gate aligned across the court: " + gate.name);
                var around = sides.Where(side => gates.OrderBy(other =>
                    Mathf.Abs(other.transform.position.z - side.transform.position.z)).First() == gate).ToArray();
                if (around.Length != 0 && around.Length != 2)
                    throw new InvalidOperationException("Expected two walls flanking " + gate.name);

                var p = gate.transform.position;
                float front = around.Length == 0 ? p.z - CampaignBorderGeometry.Depth * .5f
                    : around[0].transform.position.z - around[0].Size.y * .5f;
                float rear = front + CampaignBorderGeometry.Depth;
                float center = (front + rear) * .5f;
                var frontEdges = CourtCrossSection(outline, front, p.x);
                var rearEdges = CourtCrossSection(outline, rear, p.x);
                float width = gate.size.x;
                if (fitWideOpening)
                {
                    // Leave a solid jamb at the tight end of the tapered court on both sides.
                    const float minimumJamb = .1f;
                    float clearHalfWidth = Mathf.Min(p.x - Mathf.Max(frontEdges.x, rearEdges.x),
                        Mathf.Min(frontEdges.y, rearEdges.y) - p.x);
                    width = Mathf.Min(width, 2 * (clearHalfWidth - minimumJamb));
                }
                FitGate(gate, new Vector3(p.x, floor + CampaignBorderGeometry.Height * .5f, center), width);
                foreach (int direction in new[] { -1, 1 })
                {
                    float gateEdge = p.x + direction * gate.size.x * .5f;
                    float frontEdge = direction < 0 ? frontEdges.x : frontEdges.y;
                    float rearEdge = direction < 0 ? rearEdges.x : rearEdges.y;
                    float frontSpan = (frontEdge - gateEdge) * direction;
                    float rearSpan = (rearEdge - gateEdge) * direction;
                    if (Mathf.Max(frontSpan, rearSpan) < .001f) continue;
                    if (Mathf.Min(frontSpan, rearSpan) < .001f)
                        throw new InvalidOperationException("Gate terrain cannot seal the court edge at " + gate.name);

                    var side = around.SingleOrDefault(s => Mathf.Sign(s.transform.position.x - p.x) == direction);
                    if (side == null)
                    {
                        string name = direction < 0 ? "Gate terrain left" : "Gate terrain right";
                        side = new GameObject(name, typeof(SolidObstacleAuthoring)).GetComponent<SolidObstacleAuthoring>();
                        SceneManager.MoveGameObjectToScene(side.gameObject, scene);
                        side.transform.SetParent(arena.transform, true);
                        var child = new GameObject(direction < 0 ? "Gate wall left" : "Gate wall right", typeof(MeshFilter), typeof(MeshRenderer));
                        SceneManager.MoveGameObjectToScene(child, scene);
                        child.transform.SetParent(side.transform, false);
                    }
                    float span = Mathf.Max(frontSpan, rearSpan);
                    // Extend the navigation footprint outward by at least half a metre. The visible solid ends at the inner rim.
                    side.placementUnit = .5f;
                    side.footprint = new Vector2Int(Mathf.CeilToInt(span * 2f) + 1, 3);
                    side.height = CampaignBorderGeometry.Height;
                    side.transform.SetPositionAndRotation(new Vector3(gateEdge + direction * side.Size.x * .5f, floor, center), Quaternion.identity);
                    var filter = side.GetComponentsInChildren<MeshFilter>(true).Single();
                    filter.name = direction < 0 ? "Gate wall left" : "Gate wall right";
                    if (Mathf.Abs(frontSpan - rearSpan) < .001f)
                    {
                        unit ??= saveMesh(CampaignBorderGeometry.GateWall());
                        filter.sharedMesh = unit;
                        filter.transform.SetPositionAndRotation(new Vector3(gateEdge + direction * span * .5f, floor, center), Quaternion.identity);
                        filter.transform.localScale = new Vector3(span, CampaignBorderGeometry.Height, CampaignBorderGeometry.Depth);
                    }
                    else
                    {
                        filter.sharedMesh = saveMesh(CampaignBorderGeometry.GateJamb(side.name + " jamb", direction, frontSpan, rearSpan));
                        filter.transform.SetPositionAndRotation(new Vector3(gateEdge, floor, center), Quaternion.identity);
                        filter.transform.localScale = Vector3.one;
                    }
                    filter.GetComponent<MeshRenderer>().sharedMaterial = wallMaterial;
                }
            }
            return gates.Length;
        }

        private static Vector2 CourtCrossSection(Vector2[] outline, float z, float center)
        {
            float left = float.NegativeInfinity, right = float.PositiveInfinity;
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i]; var b = outline[(i + 1) % outline.Length];
                if ((a.y > z) == (b.y > z)) continue;
                float x = Mathf.Lerp(a.x, b.x, (z - a.y) / (b.y - a.y));
                if (x < center) left = Mathf.Max(left, x);
                else right = Mathf.Min(right, x);
            }
            if (float.IsInfinity(left) || float.IsInfinity(right))
                throw new InvalidOperationException("The gate must sit between opposite walls.");
            return new Vector2(left, right);
        }

        private static void FitGate(BarricadeAuthoring gate, Vector3 position, float width)
        {
            if (Mathf.Abs(gate.size.x - width) < .0001f &&
                Mathf.Abs(gate.size.y - CampaignBorderGeometry.Height) < .0001f &&
                Mathf.Abs(gate.size.z - CampaignBorderGeometry.Depth) < .0001f &&
                (gate.transform.position - position).sqrMagnitude < .000001f) return;
            bool resizeWidth = Mathf.Abs(gate.size.x - width) >= .0001f;
            gate.size = new Vector3(width, CampaignBorderGeometry.Height, CampaignBorderGeometry.Depth);
            FitArtwork(gate, position, resizeWidth);
        }

        internal static void FitArtwork(BarricadeAuthoring gate, Vector3 position, bool resizeWidth)
        {
            var parts = gate.GetComponentsInChildren<BarricadeVisualAuthoring>(true)
                .Select(v => (visual: v, renderer: v.GetComponent<MeshRenderer>(), piece: v.GetComponent<VillageGatePieceAuthoring>())).ToArray();
            if (parts.Length == 0 || parts.Any(p => p.renderer == null))
                throw new InvalidOperationException("Expected authored visuals on " + gate.name);
            var oldPosition = gate.transform.position;
            var artwork = parts.Where(p => p.visual.crackStage == 0 && (p.piece == null || p.piece.kind == 0)).Select(p => p.renderer).ToArray();
            var original = artwork[0].bounds;
            foreach (var renderer in artwork.Skip(1)) original.Encapsulate(renderer.bounds);
            var artworkOffset = original.center - oldPosition;
            float xScale = resizeWidth ? gate.size.x / original.size.x : 1;
            float yScale = gate.size.y / original.size.y;
            float zScale = gate.size.z / original.size.z;
            gate.transform.position = position;
            var target = new Bounds(position, gate.size);
            foreach (var part in parts)
            {
                var t = part.visual.transform;
                var offset = t.position - position - artworkOffset;
                t.position = new Vector3(resizeWidth ? position.x + offset.x * xScale : t.position.x,
                    position.y + offset.y * yScale, position.z + offset.z * zScale);
                var scale = t.localScale;
                t.localScale = new Vector3(scale.x * xScale, scale.y * yScale, scale.z * zScale);
                if (part.visual.name == "Solid plate")
                {
                    scale = t.localScale;
                    scale.z *= gate.size.z / part.renderer.bounds.size.z;
                    t.localScale = scale;
                    t.position += Vector3.forward * (position.z - part.renderer.bounds.center.z);
                }
                if (part.visual.name == "Damage crack")
                {
                    var crackBounds = part.renderer.bounds;
                    float face = offset.z < 0 ? target.min.z - crackBounds.extents.z - .003f : target.max.z + crackBounds.extents.z + .003f;
                    t.position += Vector3.forward * (face - crackBounds.center.z);
                    continue;
                }
                // Angled timber braces can exceed the envelope after scaling their local axes.
                var size = part.renderer.bounds.size;
                t.localScale *= Mathf.Min(1, resizeWidth ? gate.size.x / size.x : 1, gate.size.y / size.y, gate.size.z / size.z);
                var bounds = part.renderer.bounds;
                float x = resizeWidth ? Mathf.Clamp(bounds.center.x, target.min.x + bounds.extents.x,
                    target.max.x - bounds.extents.x) : bounds.center.x;
                float y = Mathf.Clamp(bounds.center.y, target.min.y + bounds.extents.y, target.max.y - bounds.extents.y);
                float z = Mathf.Clamp(bounds.center.z, target.min.z + bounds.extents.z, target.max.z - bounds.extents.z);
                t.position += new Vector3(x - bounds.center.x, y - bounds.center.y, z - bounds.center.z);
                if (part.piece != null) part.piece.restingHeight = t.localScale.z * .5f + .03f;
            }

            var lowest = artwork.OrderBy(r => r.bounds.min.y).First();
            lowest.transform.position += Vector3.up * (target.min.y - lowest.bounds.min.y);
            var highest = artwork.OrderByDescending(r => r.bounds.max.y).First();
            highest.transform.position += Vector3.up * (target.max.y - highest.bounds.max.y);
            var front = artwork.OrderBy(r => r.bounds.min.z).First();
            front.transform.position += Vector3.forward * (target.min.z - front.bounds.min.z);
            var rear = artwork.OrderByDescending(r => r.bounds.max.z).First();
            rear.transform.position += Vector3.forward * (target.max.z - rear.bounds.max.z);
            var artBounds = artwork[0].bounds;
            foreach (var r in artwork.Skip(1)) artBounds.Encapsulate(r.bounds);
            if (Mathf.Abs(artBounds.min.y - target.min.y) > .001f || Mathf.Abs(artBounds.max.y - target.max.y) > .001f ||
                Mathf.Abs(artBounds.min.z - target.min.z) > .001f || Mathf.Abs(artBounds.max.z - target.max.z) > .001f)
                throw new InvalidOperationException("Gate artwork failed to fit the collider: " + gate.name + " " + artBounds);
        }
    }
}
