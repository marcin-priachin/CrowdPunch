using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    // Sparse skin probes measure the visible pose without changing render/collision bounds.
    public struct EnemyTrailVisual : IComponentData
    {
        public Entity Owner, Root;
        public BlobAssetReference<EnemyGroundingMesh> Probes;
        public float3 Center;
        // Across flight, vertical, along flight, in world metres.
        public float3 Size;
    }
}
