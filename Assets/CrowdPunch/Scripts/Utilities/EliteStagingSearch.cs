using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Utilities
{
    // System-owned search scratch. Entity versions and the arena owner bound its lifetime.
    internal sealed class EliteStagingSearch
    {
        public Entity Projectile, Arena;
        public float2 ProjectileOrigin, PlayerOrigin, EliteOrigin;
        public int2 Origin, Candidate;
        public bool HasCandidate;
        public double RetryAt;
        private int ring = 1;
        private int offset;

        public bool TryNext(int2 size, out int2 cell)
        {
            int maximum = math.cmax(math.max(Origin, size - 1 - Origin));
            if (ring > maximum)
            {
                cell = default;
                return false;
            }

            int firstEdges = 4 * ring + 2;
            cell = Origin + (offset < firstEdges
                ? new int2(-ring + offset / 2, (offset & 1) == 0 ? -ring : ring)
                : new int2((offset & 1) == 0 ? -ring : ring, -ring + 1 + (offset - firstEdges) / 2));
            offset++;
            if (offset >= 8 * ring)
            {
                offset = 0;
                ring++;
            }
            return true;
        }
    }
}
