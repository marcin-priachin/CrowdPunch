using CrowdPunch.Components;
using Unity.Mathematics;

namespace CrowdPunch.Systems.AI
{
    /// <summary>Short arc waypoints keep a changing surround position from steering through the crowd's centre.</summary>
    public static class EnemySurroundRouting
    {
        public static float3 Destination(float3 position, float3 player, float2 offset, float spacing, in ArenaBounds bounds)
        {
            float2 current = position.xz - player.xz;
            float2 from = math.normalizesafe(current, math.normalizesafe(offset, new float2(1, 0)));
            float2 to = math.normalizesafe(offset, from);
            float angle = math.atan2(from.x * to.y - from.y * to.x, math.dot(from, to));
            const float step = math.PI / 6f;
            if (math.abs(angle) <= step)
                return EnemyCrowdFormation.Clamp(new float3(player.x + offset.x, position.y, player.z + offset.y), bounds);
            float turn = math.clamp(angle, -step, step);
            float2 direction = new float2(from.x * math.cos(turn) - from.y * math.sin(turn),
                from.x * math.sin(turn) + from.y * math.cos(turn));
            float radius = math.max(math.length(offset) + math.max(.5f, spacing),
                math.min(math.length(current), math.length(offset) + math.max(1f, spacing * 2f)));
            return EnemyCrowdFormation.Clamp(new float3(player.x + direction.x * radius, position.y,
                player.z + direction.y * radius), bounds);
        }
    }
}
