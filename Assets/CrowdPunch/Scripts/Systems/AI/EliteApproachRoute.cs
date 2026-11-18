using CrowdPunch.Components;
using CrowdPunch.Utilities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.AI
{
    internal static class EliteApproachRoute
    {
        public const float ArrivalTolerance = .05f;
        private const float ArcStep = math.PI / 8f;
        private const float CandidateStep = math.PI / 72f;

        internal static bool TryFind(float3 position, float3 projectile, float3 desired, float clearance,
            float tolerance, in NavigationGrid grid, int clearanceClass, out float3 waypoint)
        {
            float2 start = position.xz;
            float2 centre = projectile.xz;
            float2 facing = math.normalizesafe(desired.xz - centre, new float2(0, 1));
            waypoint = position;
            if (grid.Data.IsCreated && NavigationGeometry.Anchor(ref grid.Data.Value, start, clearanceClass) < 0)
                return false;

            if (ClearSegment(start, desired.xz, centre, clearance, grid, clearanceClass))
            {
                waypoint = new float3(desired.x, position.y, desired.z);
                return true;
            }

            // Chords between these points stay outside the projectile's body clearance.
            float radius = math.max(.01f, clearance / math.cos(ArcStep * .5f) + .01f);
            float allowed = math.max(0f, tolerance - math.min(ArrivalTolerance, tolerance * .25f));
            for (int i = 0; i <= 24; i++)
            {
                float angle = ((i + 1) / 2) * CandidateStep;
                if (i % 2 == 0) angle = -angle;
                float2 goal = centre + Rotate(facing, angle) * math.max(radius, math.length(desired.xz - centre));
                if (math.distancesq(goal, desired.xz) > allowed * allowed) continue;
                if (ClearSegment(start, goal, centre, clearance, grid, clearanceClass)
                    || TryDetour(start, goal, centre, radius, clearance, grid, clearanceClass, out goal))
                {
                    waypoint = new float3(goal.x, position.y, goal.y);
                    return true;
                }
            }
            return false;
        }

        private static bool TryDetour(float2 start, float2 goal, float2 centre, float radius, float clearance,
            in NavigationGrid grid, int clearanceClass, out float2 waypoint)
        {
            float2 from = math.normalizesafe(start - centre, math.normalizesafe(goal - centre));
            float2 to = math.normalizesafe(goal - centre);
            float turn = math.atan2(from.x * to.y - from.y * to.x, math.dot(from, to));
            return CheckArc(start, goal, centre, from, turn, radius, clearance, grid, clearanceClass, out waypoint)
                || CheckArc(start, goal, centre, from, turn - math.sign(turn) * 2f * math.PI,
                    radius, clearance, grid, clearanceClass, out waypoint);
        }

        private static bool CheckArc(float2 start, float2 goal, float2 centre, float2 from, float turn, float radius,
            float clearance, in NavigationGrid grid, int clearanceClass, out float2 waypoint)
        {
            waypoint = start;
            float step = math.distancesq(start, centre) < radius * radius ? ArcStep * .5f : ArcStep;
            int legs = math.max(1, (int)math.ceil(math.abs(turn) / step));
            float2 previous = start;
            for (int leg = 1; leg <= legs; leg++)
            {
                float2 next = leg == legs ? goal : centre + Rotate(from, turn * leg / legs) * radius;
                if (!ClearSegment(previous, next, centre, clearance, grid, clearanceClass)) return false;
                if (leg == 1) waypoint = next;
                previous = next;
            }
            return true;
        }

        private static bool ClearSegment(float2 start, float2 end, float2 centre, float clearance,
            in NavigationGrid grid, int clearanceClass)
        {
            float2 offset = start - centre;
            float2 delta = end - start;
            float closest = math.clamp(-math.dot(offset, delta) / math.max(.000001f, math.lengthsq(delta)), 0f, 1f);
            // A touching body can move out of contact, but the route cannot travel deeper into it.
            if (math.lengthsq(end - centre) < clearance * clearance
                || closest > .0001f && math.lengthsq(offset + delta * closest) < clearance * clearance) return false;
            if (!grid.Data.IsCreated) return true;
            ref var geometry = ref grid.Data.Value;
            return clearanceClass >= 0
                && NavigationGeometry.Segment(ref geometry, start, end, geometry.Radii[clearanceClass])
                && NavigationGeometry.Anchor(ref geometry, end, clearanceClass) >= 0;
        }

        private static float2 Rotate(float2 direction, float angle)
        {
            math.sincos(angle, out float sin, out float cos);
            return new float2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }
    }
}
