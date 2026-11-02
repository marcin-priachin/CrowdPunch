using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    // Gameplay decisions shared with the advisory forecast; ordinary impulses remain solver-owned.
    internal static class LaunchContactResponse
    {
        public static bool Meaningful(float3 normal, float impulse, float minimumImpulse)
            => math.abs(normal.y) <= .7f && impulse >= math.max(.01f, minimumImpulse);

        public static float3 BarricadeVelocity(float3 solved, float3 incoming, float3 normal, float multiplier)
        {
            solved.xz = math.reflect(incoming, normal).xz * multiplier;
            return solved;
        }

        public static float3 FinaleSafety(float3 velocity, float3 position, float3 player, float3 normal, float angle)
        {
            float2 toward = math.normalizesafe(player.xz - position.xz);
            if (math.dot(math.normalizesafe(velocity.xz), toward) > math.cos(math.radians(angle)))
            {
                float2 side = new float2(-toward.y, toward.x);
                if (math.dot(side, normal.xz) < 0) side = -side;
                velocity.xz = math.normalizesafe(side + normal.xz * .2f) * math.length(velocity.xz);
            }
            return velocity;
        }

        public static float3 OutsideSurface(float3 position, float3 center, float3 normal)
        {
            float penetration = math.dot(position - center, normal);
            return penetration < 0 ? position - normal * penetration : position;
        }
    }
}
