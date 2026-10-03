using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct ProtectedPoint : IComponentData
    {
        public float2 Center, HalfSize;
        public int BreachThreshold, MaximumPlayerAttackers;
        public int Breaches;
        public bool Failed => Breaches >= BreachThreshold;
        // Root-centre inclusion avoids collider size and visual scale changing the objective.
        public bool Contains(float3 position) => math.all(math.abs(position.xz - Center) <= HalfSize);
    }

    [InternalBufferCapacity(4)]
    public struct ProtectedPointAttacker : IBufferElementData
    {
        public Entity Enemy;
        public float DistanceSq;

        public static bool Contains(NativeArray<ProtectedPointAttacker> attackers, Entity enemy)
        {
            if (!attackers.IsCreated) return false;
            for (int i = 0; i < attackers.Length; i++) if (attackers[i].Enemy == enemy) return true;
            return false;
        }
    }
}
