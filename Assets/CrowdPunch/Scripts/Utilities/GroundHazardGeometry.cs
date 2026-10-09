using CrowdPunch.Components;
using Unity.Collections;
using Unity.Mathematics;

namespace CrowdPunch.Utilities
{
    public static class GroundHazardGeometry
    {
        public static GroundHazardPhase Phase(in GroundHazard h, double age)
        {
            if (h.Operation == GroundHazardOperation.AlwaysActive) return GroundHazardPhase.Active;
            double duration = h.InactiveDuration + h.WarningDuration + h.ActiveDuration;
            double t = (age + h.CycleOffset) % math.max(.01, duration);
            if (t < 0) t += duration;
            return t < h.InactiveDuration ? GroundHazardPhase.Inactive :
                t < h.InactiveDuration + h.WarningDuration ? GroundHazardPhase.Warning : GroundHazardPhase.Active;
        }
        private static float2 Local(in GroundHazard h, float2 point)
        {
            float2 d = point - h.Position.xz;
            float c = math.cos(h.Angle), s = math.sin(h.Angle);
            return new float2(c * d.x - s * d.y, s * d.x + c * d.y);
        }
        public static bool Overlaps(in GroundHazard h, float2 point, float radius)
        {
            if (h.Shape == GroundHazardShape.Circle) return math.distancesq(point, h.Position.xz) <= math.square(h.Radius + radius);
            float2 local = Local(h, point);
            return math.lengthsq(local - math.clamp(local, -h.HalfSize, h.HalfSize)) <= radius * radius;
        }
        public static bool Intersects(in GroundHazard h, float2 start, float2 end, float radius)
        {
            if (h.Shape == GroundHazardShape.Rectangle)
                return NavigationGeometry.SweptCircleIntersectsRectangle(Local(h, start), Local(h, end), radius, -h.HalfSize, h.HalfSize);
            float2 d = end - start;
            float t = math.saturate(math.dot(h.Position.xz - start, d) / math.max(1e-10f, math.lengthsq(d)));
            return math.distancesq(h.Position.xz, start + d * t) <= math.square(h.Radius + radius);
        }
        public static bool Clear(NativeArray<GroundHazard> hazards, float2 start, float2 end, float radius)
        {
            if (!hazards.IsCreated) return true;
            foreach (var h in hazards) if (Intersects(h, start, end, radius)) return false;
            return true;
        }
        public static float ExitDistance(in GroundHazard h, float2 point, float2 direction, float radius)
        {
            if (h.Shape == GroundHazardShape.Circle)
            {
                float2 q = point - h.Position.xz;
                float along = math.dot(q,direction);
                return math.max(0,-along + math.sqrt(math.max(0,along * along + math.square(h.Radius + radius) - math.lengthsq(q))));
            }
            float2 local = Local(h,point), ray = Local(h,h.Position.xz + direction);
            float2 size = h.HalfSize + radius;
            float x = math.abs(ray.x) < 1e-6f ? float.MaxValue : ((ray.x > 0 ? size.x : -size.x) - local.x) / ray.x;
            float y = math.abs(ray.y) < 1e-6f ? float.MaxValue : ((ray.y > 0 ? size.y : -size.y) - local.y) / ray.y;
            return math.max(0,math.min(x,y));
        }
    }
}
