using CrowdPunch.Components;
using Unity.Mathematics;

namespace CrowdPunch.Systems.AI
{
    /// <summary>Geometry for concentrated reserve areas and committed interception orders.</summary>
    public static class EnemyCrowdFormation
    {
        public static int PursuitCount(int count, in EnemyCrowdPressureSettings settings)
        {
            int pursuit = math.min(count, math.max(1, (int)math.round(count * math.saturate(settings.PursuitShare))));
            return count - pursuit < 2 * math.max(1, settings.MinimumInterceptionGroupSize) ? count : pursuit;
        }

        public static float3 Clamp(float3 destination, in ArenaBounds bounds)
        {
            float2 extent = math.max(float2.zero, bounds.Extents.xz - .75f);
            destination.xz = math.clamp(destination.xz, bounds.Center.xz - extent, bounds.Center.xz + extent);
            return destination;
        }

        public static float2 Slot(int identity, float radius)
        {
            uint hash = math.hash(new uint2((uint)math.max(1, identity + 1), 741u));
            float angle = (hash & 0xffffu) * (2f * math.PI / 65536f);
            float distance = math.sqrt((hash >> 16) / 65535f) * math.max(0f, radius);
            return new float2(math.cos(angle), math.sin(angle)) * distance;
        }

        public static float3 ReserveDestination(int identity, float3 position,
            in ArenaBounds bounds, in EnemyCrowdPressureSettings settings)
        {
            int count = math.clamp(settings.ReserveGroupCount, 1, 32);
            float2 extent = math.max(float2.zero, bounds.Extents.xz - math.max(1f, settings.GroupRadius));
            int columns = math.clamp((int)math.ceil(math.sqrt(count * math.max(1f, extent.x) / math.max(1f, extent.y))), 1, count);
            int rows = (count + columns - 1) / columns;
            float2 nearest = bounds.Center.xz;
            float nearestSq = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                float2 sample = new float2((index % columns + .5f) / columns, (index / columns + .5f) / rows);
                float2 anchor = bounds.Center.xz + (sample * 2f - 1f) * extent;
                float distanceSq = math.distancesq(position.xz, anchor);
                if (distanceSq < nearestSq) { nearest = anchor; nearestSq = distanceSq; }
            }
            float2 point = nearest + Slot(identity, settings.GroupRadius);
            return Clamp(new float3(point.x, position.y, point.y), bounds);
        }

        public static void InterceptionTargets(int identity, int side, in PlayerSnapshot player,
            in EnemyCrowdPressureSettings settings, in ArenaBounds bounds, out float3 flank, out float3 destination)
        {
            float2 velocity = player.Velocity.xz;
            float2 forward = math.normalizesafe(velocity, math.normalizesafe(player.Forward.xz, new float2(0, 1)));
            float2 lateral = new float2(forward.y, -forward.x) * side;
            float2 prediction = velocity * math.max(0f, settings.PredictionSeconds);
            float maxDistance = math.max(0f, settings.MaximumPredictionDistance);
            if (math.lengthsq(prediction) > maxDistance * maxDistance)
                prediction = math.normalizesafe(prediction) * maxDistance;
            float2 offset = Slot(identity, settings.GroupRadius);
            float2 flankXZ = player.Position.xz + lateral * settings.FlankDistance + offset;
            float2 targetXZ = player.Position.xz + prediction + lateral * (settings.FlankDistance * .5f) + offset;
            flank = Clamp(new float3(flankXZ.x, player.Position.y, flankXZ.y), bounds);
            destination = Clamp(new float3(targetXZ.x, player.Position.y, targetXZ.y), bounds);
        }
    }
}
