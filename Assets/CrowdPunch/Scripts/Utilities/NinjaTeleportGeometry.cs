using CrowdPunch.Components;
using Unity.Collections;
using Unity.Mathematics;

namespace CrowdPunch.Utilities
{
    public struct NinjaTeleportOccupant
    {
        public Unity.Entities.Entity Entity;
        public float3 Position;
        public float Radius, HalfHeight;
        public byte PlanarReservation;
    }

    public static class NinjaTeleportGeometry
    {
        public static float3 Candidate(in PlayerSnapshot player, in NinjaTuning tuning, float radius, int index, float height)
        {
            float3 rear = -player.Forward; rear.y = 0;
            rear = math.normalizesafe(rear, new float3(0, 0, -1));
            int step = (index + 1) / 2;
            float sign = index % 2 == 1 ? 1 : -1;
            float angle = index == 0 ? 0 : math.radians(math.clamp(tuning.RearArcDegrees, 0, 90))
                * step / math.max(1, tuning.RearSearchSteps) * sign;
            float distance = math.max(tuning.TeleportDistance, player.Radius + radius + .05f);
            float3 candidate = player.Position + math.rotate(quaternion.RotateY(angle), rear) * distance;
            candidate.y = height;
            return candidate;
        }

        public static bool Valid(NavigationGrid grid, ArenaBounds bounds, Unity.Entities.Entity self,
            float3 origin, float3 candidate, in PlayerSnapshot player, float radius,
            NativeArray<NinjaTeleportOccupant> occupants, NativeArray<GroundHazard> hazards)
        {
            if (math.any(math.abs(candidate.xz - bounds.Center.xz) > bounds.Extents.xz - radius)) return false;
            if (math.distancesq(candidate.xz, player.Position.xz) < math.square(player.Radius + radius)) return false;
            foreach (var other in occupants)
            {
                if (other.Entity == self) continue;
                float3 offset = candidate - other.Position;
                offset.y = other.PlanarReservation != 0 ? 0 : math.max(0, math.abs(offset.y)
                    - math.max(0, 1 - radius) - math.max(0, other.HalfHeight - other.Radius));
                if (math.lengthsq(offset) < math.square(radius + other.Radius)) return false;
            }
            foreach (var hazard in hazards)
                if (GroundHazardGeometry.Overlaps(hazard, candidate.xz, radius)) return false;
            // Failing closed without topology avoids crossing disconnected rooms through a teleport.
            if (!grid.Data.IsCreated) return false;
            ref var g = ref grid.Data.Value;
            int clearance = NavigationGeometry.ClearanceClass(ref g, radius);
            int source = PhysicalAnchor(ref g, origin.xz, clearance, radius);
            int destination = PhysicalAnchor(ref g, candidate.xz, clearance, radius);
            int target = PhysicalAnchor(ref g, player.Position.xz, clearance, radius);
            int region = NavigationGeometry.Region(ref g, source, clearance);
            return region > 0 && region == NavigationGeometry.Region(ref g, destination, clearance)
                && region == NavigationGeometry.Region(ref g, target, clearance)
                && NavigationGeometry.Segment(ref g, candidate.xz, player.Position.xz, radius);
        }

        private static int PhysicalAnchor(ref NavigationGridBlob grid, float2 position, int clearance, float radius)
        {
            int cell = NavigationGeometry.Cell(ref grid, position);
            if (cell < 0 || clearance < 0) return -1;
            // A continuous position can fit beside a wall even when its raster-cell centre
            // fails the coarser clearance class. Connect it to a region at its actual radius.
            if (NavigationGeometry.Region(ref grid, cell, clearance) > 0
                && NavigationGeometry.Segment(ref grid, position, NavigationGeometry.Center(ref grid, cell), radius)) return cell;
            for (int direction = 0; direction < 8; direction++)
            {
                int neighbor = NavigationGeometry.Neighbor(ref grid, cell, direction);
                if (NavigationGeometry.Region(ref grid, neighbor, clearance) > 0
                    && NavigationGeometry.Segment(ref grid, position, NavigationGeometry.Center(ref grid, neighbor), radius)) return neighbor;
            }
            return -1;
        }
    }
}

