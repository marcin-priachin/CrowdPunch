using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct EnemyAnimationGrounding : IComponentData
    {
        public Entity Root;
        public Entity Owner;
        public float Clearance;
        public BlobAssetReference<EnemyGroundingMesh> Mesh;
        public float3 Offset;
    }

    public struct EnemyGroundingMesh
    {
        public BlobArray<EnemyGroundingVertex> Vertices;
    }

    public struct EnemyGroundingVertex
    {
        public float3 Position;
        public int4 Bones;
        public float4 Weights;
    }

    public struct EnemyGroundingVisual : IComponentData
    {
        public Entity Animation;
        public AABB AuthoredBounds;
    }
}
