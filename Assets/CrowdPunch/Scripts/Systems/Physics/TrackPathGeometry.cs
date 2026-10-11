using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Physics
{
    public static class TrackPathGeometry
    {
        private const float Tolerance = .00001f;

        public static float3 Position(in TrackObject track, DynamicBuffer<TrackPathPoint> path, float distance)
        {
            distance = math.clamp(distance, 0, track.Length);
            if (!path.IsCreated || path.Length < 2) return track.Start + track.Direction * distance;
            for (int i = 1; i < path.Length; i++)
                if (distance <= path[i].Distance)
                    return math.lerp(path[i - 1].Position, path[i].Position,
                        math.saturate((distance - path[i - 1].Distance) / (path[i].Distance - path[i - 1].Distance)));
            return path[path.Length - 1].Position;
        }

        public static float3 Direction(in TrackObject track, DynamicBuffer<TrackPathPoint> path, float distance, bool forward)
        {
            if (!path.IsCreated || path.Length < 2) return track.Direction;
            for (int i = 1; i < path.Length; i++)
                if (forward ? distance < path[i].Distance - Tolerance : distance <= path[i].Distance + Tolerance)
                    return math.normalize(path[i].Position - path[i - 1].Position);
            return math.normalize(path[path.Length - 1].Position - path[path.Length - 2].Position);
        }

        public static int SignedStep(in TrackObject track, DynamicBuffer<TrackPathPoint> path, float distance, float3 hit)
        {
            hit.y = 0;
            hit = math.normalizesafe(hit);
            float forward = math.dot(hit, Direction(track, path, distance, true));
            float backward = math.dot(hit, Direction(track, path, distance, false));
            // At a corner, the next leg accepts forward hits and the previous leg accepts
            // reverse hits. This keeps both sides usable without extending the old tangent.
            bool advances = forward > Tolerance, reverses = backward < -Tolerance;
            if (advances && reverses) return forward >= -backward ? 1 : -1;
            return advances ? 1 : reverses ? -1 : 0;
        }

        public static float StopAtCorner(DynamicBuffer<TrackPathPoint> path, float distance, float next)
        {
            if (!path.IsCreated) return next;
            // One physics step stays on one segment, including when several hits were queued.
            // Swept impacts and character displacement then use the actual rail, never its chord.
            if (next > distance)
            {
                for (int i = 1; i < path.Length - 1; i++)
                    if (path[i].Distance > distance + Tolerance) return math.min(next, path[i].Distance);
            }
            else
            {
                for (int i = path.Length - 2; i > 0; i--)
                    if (path[i].Distance < distance - Tolerance) return math.max(next, path[i].Distance);
            }
            return next;
        }
    }
}
