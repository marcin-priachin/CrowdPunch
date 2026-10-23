using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct GatekeeperRig : IComponentData
    {
        public Entity Model;
        public int LeftPalm, RightPalm;
        public float3 LeftBindPalm, RightBindPalm;
        public float GroundClearance;
        public float2 FistScale;
        public BlobAssetReference<GatekeeperHandBounds> Bounds;
    }

    [InternalBufferCapacity(24)]
    public struct GatekeeperArmBone : IBufferElementData
    {
        // Zero is the head/jaw; one and two are the RigLArm1/RigRArm1 subtrees.
        public byte Side;
    }

    public struct GatekeeperHandBounds
    {
        public int FrameCount;
        // Fixed reference radii over every clip keep authored finger motion from causing size pulses.
        public float LeftRadius, RightRadius;
        // Motion, frame, then left/right; each bound is relative to its sampled palm.
        public BlobArray<GatekeeperHandFrame> Frames;
    }

    public struct GatekeeperHandFrame
    {
        public float3 Min, Max;
    }

    public struct GatekeeperHandFootprint : IComponentData
    {
        public float Radius;
    }
}
