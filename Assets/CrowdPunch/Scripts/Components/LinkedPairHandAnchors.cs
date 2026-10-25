using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    // Bind-space marker positions are transformed by the final animated skin, including launch rotation.
    public struct LinkedPairHandAnchors : IComponentData
    {
        public Entity Owner;
        public Entity Root;
        public int LeftBone;
        public int RightBone;
        public float3 LeftBindPosition;
        public float3 RightBindPosition;
    }
}
