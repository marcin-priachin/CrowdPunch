using CrowdPunch.Components;
using Unity.Mathematics;
namespace CrowdPunch.Utilities
{
    public static class NavigationGeometry
    {
        public static int Cell(ref NavigationGridBlob g, float2 p)
        {
            int2 c = (int2)math.floor((p - g.Minimum) / g.CellSize);
            return math.any(p < g.Minimum) || math.any(p >= g.Maximum) || math.any(c < 0) || math.any(c >= g.Size) ? -1 : c.y * g.Size.x + c.x;
        }
        public static float2 Center(ref NavigationGridBlob g, int cell) => g.Minimum + (new float2(cell % g.Size.x, cell / g.Size.x) + .5f) * g.CellSize;
        public static int ClearanceClass(ref NavigationGridBlob g, float radius)
        {
            for (int i = 0; i < 3; i++) if (radius + g.Margin <= g.Radii[i]) return i;
            return -1;
        }
        public static int Region(ref NavigationGridBlob g, int cell, int clearance) => cell < 0 || clearance < 0 ? 0 : g.Regions[clearance * g.Size.x * g.Size.y + cell];
        public static bool Segment(ref NavigationGridBlob g, float2 a, float2 b, float radius)
        {
            if (math.any(a < g.Minimum + radius) || math.any(a > g.Maximum - radius)
                || math.any(b < g.Minimum + radius) || math.any(b > g.Maximum - radius)) return false;
            return ObstacleFreeSegment(ref g, a, b, radius);
        }
        public static bool ObstacleFreeSegment(ref NavigationGridBlob g, float2 a, float2 b, float radius)
        {
            for (int i = 0; i < g.Obstacles.Length; i++)
                if (Intersects(a, b, g.Obstacles[i].Minimum - radius, g.Obstacles[i].Maximum + radius)) return false;
            return true;
        }
        // Slab test on the entire swept segment, not centre samples. Square Minkowski inflation
        // deliberately over-clears round corners, so every accepted edge is also physics-safe.
        public static bool Intersects(float2 a, float2 b, float2 min, float2 max)
        {
            float lo = 0, hi = 1; float2 d = b - a;
            for (int axis = 0; axis < 2; axis++)
            {
                if (math.abs(d[axis]) < 1e-7f) { if (a[axis] < min[axis] || a[axis] > max[axis]) return false; }
                else
                {
                    float t0 = (min[axis] - a[axis]) / d[axis], t1 = (max[axis] - a[axis]) / d[axis];
                    lo = math.max(lo, math.min(t0, t1)); hi = math.min(hi, math.max(t0, t1)); if (lo > hi) return false;
                }
            }
            return true;
        }
        public static int2 Offset(int direction)
        {
            switch (direction)
            {
                case 0: return new int2(1, 0);
                case 1: return new int2(0, 1);
                case 2: return new int2(-1, 0);
                case 3: return new int2(0, -1);
                case 4: return new int2(1, 1);
                case 5: return new int2(-1, 1);
                case 6: return new int2(-1, -1);
                default: return new int2(1, -1);
            }
        }
        public static int Neighbor(ref NavigationGridBlob g, int cell, int direction)
        { int2 c = new int2(cell % g.Size.x, cell / g.Size.x) + Offset(direction); return math.any(c < 0) || math.any(c >= g.Size) ? -1 : c.y * g.Size.x + c.x; }
        public static int Anchor(ref NavigationGridBlob g, float2 position, int clearance)
        {
            if (clearance < 0) return -1;
            int cell = Cell(ref g, position);
            if (Region(ref g, cell, clearance) > 0 && Segment(ref g, position, Center(ref g, cell), g.Radii[clearance])) return cell;
            if (cell < 0) return -1;
            // Boundary cells can have unsafe centres while the continuous position is safe.
            for (int d = 0; d < 8; d++) { int n = Neighbor(ref g, cell, d); if (Region(ref g, n, clearance) > 0 && Segment(ref g, position, Center(ref g, n), g.Radii[clearance])) return n; }
            return -1;
        }
        public static float3 DistanceBandDestination(NavigationGrid grid, float3 position, float3 focus,
            float minimum, float maximum, float radius)
        {
            float2 away = math.normalizesafe(position.xz - focus.xz, new float2(1, 0));
            float distance = (minimum + maximum) * .5f;
            float2 desired = focus.xz + away * distance;
            if (!grid.Data.IsCreated) return new float3(desired.x, position.y, desired.y);
            ref var g = ref grid.Data.Value; int cls = ClearanceClass(ref g, radius);
            int region = Region(ref g, Anchor(ref g, position.xz, cls), cls);
            if (region == 0) return new float3(desired.x, position.y, desired.y);
            float angle = math.atan2(away.y, away.x), best = float.MaxValue; float2 selected = position.xz;
            // Angular alternatives stay on the preferred-distance ring, rather than backing into a wall.
            for (int i = 0; i < 24; i++)
            {
                float a = angle + (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * (math.PI * 2 / 24);
                float2 candidate = focus.xz + new float2(math.cos(a), math.sin(a)) * distance;
                if (Region(ref g, Anchor(ref g, candidate, cls), cls) != region) continue;
                float score = math.distancesq(position.xz, candidate);
                if (score < best) { best = score; selected = candidate; }
            }
            return new float3(selected.x, position.y, selected.y);
        }
        public static bool TryEscape(ref NavigationGridBlob g, float2 position, float actualRadius, int cls, out float2 goal)
        {
            goal = position; float best = float.MaxValue; int2 centre = (int2)math.floor((position - g.Minimum) / g.CellSize);
            centre = math.clamp(centre, int2.zero, g.Size - 1);
            for (int z = -3; z <= 3; z++) for (int x = -3; x <= 3; x++)
                {
                    int2 c = centre + new int2(x, z); if (math.any(c < 0) || math.any(c >= g.Size)) continue;
                    int cell = c.y * g.Size.x + c.x; if (Region(ref g, cell, cls) == 0) continue;
                    float2 candidate = Center(ref g, cell); float distance = math.distancesq(candidate, position);
                    if (distance < best && EscapeSegment(ref g, position, candidate, actualRadius)) { best = distance; goal = candidate; }
                }
            return best < float.MaxValue;
        }
        private static bool EscapeSegment(ref NavigationGridBlob g, float2 start, float2 end, float radius)
        {
            // Spacing bounds are not defeat bounds: a launched body can recover outside them.
            // Permit entry to a clear interior point, but never cross inflated static geometry.
            if (math.any(end < g.Minimum + radius) || math.any(end > g.Maximum - radius)) return false;
            for (int i = 0; i < g.Obstacles.Length; i++)
                if (SweptCircleIntersectsRectangle(start, end, radius,
                        g.Obstacles[i].Minimum, g.Obstacles[i].Maximum)) return false;
            return true;
        }
        public static bool SweptCircleIntersectsRectangle(float2 start, float2 end, float radius, float2 minimum, float2 maximum)
        {
            if (Intersects(start, end, minimum, maximum)) return true;
            // Escape uses the actual round body. Square inflation falsely traps a physically clear
            // body in the extra corner area after solver contact; ordinary routing stays conservative.
            float distanceSq = math.min(PointRectangleDistanceSq(start, minimum, maximum),
                PointRectangleDistanceSq(end, minimum, maximum));
            distanceSq = math.min(distanceSq, PointSegmentDistanceSq(minimum, start, end));
            distanceSq = math.min(distanceSq, PointSegmentDistanceSq(maximum, start, end));
            distanceSq = math.min(distanceSq, PointSegmentDistanceSq(new float2(minimum.x, maximum.y), start, end));
            distanceSq = math.min(distanceSq, PointSegmentDistanceSq(new float2(maximum.x, minimum.y), start, end));
            return distanceSq <= radius * radius;
        }
        private static float PointRectangleDistanceSq(float2 point, float2 minimum, float2 maximum)
            => math.lengthsq(point - math.clamp(point, minimum, maximum));
        private static float PointSegmentDistanceSq(float2 point, float2 start, float2 end)
        {
            float2 segment = end - start;
            float t = math.saturate(math.dot(point - start, segment) / math.max(1e-10f, math.lengthsq(segment)));
            return math.distancesq(point, start + segment * t);
        }
        // Bake-time selection only. COMBAT-017: use spacing bounds, never defeat bounds.
        public static bool TryResolveParticipationAnchor(ref NavigationGridBlob g, bool useOverride,
            float2 explicitAnchor, out float2 anchor)
        {
            anchor = useOverride ? explicitAnchor : (g.Minimum + g.Maximum) * .5f;
            if (ParticipationAnchorIsClear(ref g, anchor)) return true;
            if (useOverride) return false;

            float2 centre = anchor;
            float bestDistance = float.MaxValue;
            for (int cell = 0; cell < g.Size.x * g.Size.y; cell++)
            {
                float2 candidate = Center(ref g, cell);
                float distance = math.distancesq(candidate, centre);
                if (distance >= bestDistance || !ParticipationAnchorIsClear(ref g, candidate)) continue;
                anchor = candidate;
                bestDistance = distance;
            }
            // Equal-distance candidates retain grid order, keeping rebakes deterministic.
            return bestDistance < float.MaxValue;
        }
        private static bool ParticipationAnchorIsClear(ref NavigationGridBlob g, float2 point)
        {
            for (int c = 0; c < 3; c++) if (Anchor(ref g, point, c) < 0) return false;
            return true;
        }
        public static bool SpawnAllowed(NavigationGrid grid, float2 position, float radius)
        {
            if (!grid.Data.IsCreated) return true;
            ref var g = ref grid.Data.Value; int c = ClearanceClass(ref g, radius);
            int start = Anchor(ref g, position, c), anchor = Anchor(ref g, grid.ParticipationAnchor, c);
            return start >= 0 && anchor >= 0 && Region(ref g, start, c) == Region(ref g, anchor, c);
        }
        public static bool ResolveGoal(ref NavigationGridBlob g, float2 position, float2 requested, int clearance, int seed,
            NavigationGoalKind kind, out float2 goal)
        {
            goal = requested; int start = Anchor(ref g, position, clearance); int region = Region(ref g, start, clearance);
            if (region == 0) return false;
            int target = Anchor(ref g, requested, clearance);
            if (Region(ref g, target, clearance) == region) return true;
            if (kind == NavigationGoalKind.ExactSetup) return false;
            // Stable per-entity ring orientation distributes invalid slots around obstacles.
            float angle = (seed % 1000) * 2.3999631f;
            for (int ring = 1; ring <= 8; ring++) for (int i = 0; i < 12; i++)
                {
                    float a = angle + i * (math.PI * 2 / 12); float2 candidate = requested + new float2(math.cos(a), math.sin(a)) * ring * g.CellSize;
                    int n = Anchor(ref g, candidate, clearance);
                    if (Region(ref g, n, clearance) == region) { goal = candidate; return true; }
                }
            if (kind != NavigationGoalKind.Coverage) return false;
            int count = g.Size.x * g.Size.y;
            // Coverage alone may relocate arena-wide; no common nearest fallback cell.
            for (int i = 0; i < 64; i++)
            { int n = (int)(math.hash(new int2(seed, i)) % (uint)count); if (Region(ref g, n, clearance) == region) { goal = Center(ref g, n); return true; } }
            return false;
        }
    }
}
