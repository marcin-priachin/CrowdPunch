using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.AI
{
    /// <summary>Normal movement territory follows spawn ownership, including after a gate breaks.</summary>
    public static class BarricadeRoomMovement
    {
        public static bool TryGetRoom(Entity enemy, in ComponentLookup<EnemyWaveOwnership> owners,
            in ComponentLookup<BarricadeRoom> rooms, out BarricadeRoom room)
        {
            room = default;
            if (!owners.HasComponent(enemy)) return false;
            Entity owner = owners[enemy].Sequence;
            if (!rooms.HasComponent(owner)) return false;
            room = rooms[owner];
            return true;
        }

        public static bool Contains(in BarricadeRoom room, float3 position)
        {
            return math.all(position.xz >= room.Minimum) && math.all(position.xz <= room.Maximum);
        }

        public static ArenaBounds Bounds(in BarricadeRoom room)
        {
            float2 center = (room.Minimum + room.Maximum) * .5f;
            // Activation reaches just beyond the gate; walking bodies keep a clearance from it.
            float2 extents = math.max((room.Maximum - room.Minimum) * .5f - .75f, float2.zero);
            return new ArenaBounds { Center = new float3(center.x, 0, center.y),
                Extents = new float3(extents.x, 0, extents.y) };
        }

        public static float2 LimitTargetVelocity(float3 position, float2 velocity, in ArenaBounds bounds, float braking)
        {
            float2 minimum = bounds.Center.xz - bounds.Extents.xz;
            float2 maximum = bounds.Center.xz + bounds.Extents.xz;
            float2 distance = math.select(position.xz - minimum, maximum - position.xz, velocity >= 0f);
            float2 limit = math.sqrt(2f * math.max(.0001f, braking) * math.max(distance, float2.zero));
            return math.clamp(velocity, -limit, limit);
        }

        public static float2 RemoveOutwardVelocity(float3 position, float2 velocity, in ArenaBounds bounds)
        {
            float2 minimum = bounds.Center.xz - bounds.Extents.xz;
            float2 maximum = bounds.Center.xz + bounds.Extents.xz;
            return math.select(velocity, float2.zero,
                (position.xz <= minimum & velocity < 0f) | (position.xz >= maximum & velocity > 0f));
        }
    }
}
