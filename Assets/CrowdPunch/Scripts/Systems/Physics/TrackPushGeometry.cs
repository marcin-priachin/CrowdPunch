using Unity.Mathematics;

namespace CrowdPunch.Systems.Physics
{
    public static class TrackPushGeometry
    {
        // Swept box in object-local XZ. Rotation and track direction need not agree.
        public static bool Intersects(float3 point, float radius, float3 start, float3 end, quaternion rotation, float3 size)
        {
            quaternion inverse = math.inverse(rotation);
            float3 local = math.rotate(inverse, point - start);
            float3 delta = math.rotate(inverse, end - start);
            float2 lo = math.min(float2.zero, delta.xz) - size.xz * .5f;
            float2 hi = math.max(float2.zero, delta.xz) + size.xz * .5f;
            float2 closest = math.clamp(local.xz, lo, hi);
            return math.distancesq(local.xz, closest) < radius * radius
                && math.abs(point.y - start.y) < size.y * .5f + radius;
        }

        public static float3 SideCandidate(float3 point, float radius, float3 center, quaternion rotation,
            float3 size, float3 trackDirection, bool opposite)
        {
            float3 side = new float3(trackDirection.z, 0, -trackDirection.x);
            float3 right = math.rotate(rotation, new float3(1, 0, 0));
            float3 forward = math.rotate(rotation, new float3(0, 0, 1));
            float extent = math.abs(math.dot(side, right)) * size.x * .5f + math.abs(math.dot(side, forward)) * size.z * .5f;
            float offset = math.dot(point - center, side);
            float sign = offset < 0 ? -1 : 1;
            if (opposite) sign = -sign;
            return point + side * (sign * (extent + radius + .04f) - offset);
        }
    }
}
