using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    // Enabled for the whole committed launch, including cancellation, to prevent reacquisition.
    public struct CommittedLaunchFlight : IComponentData, IEnableableComponent
    {
        public uint LaunchSequence, TargetGeneration;
        public float3 AimPoint;
        public Entity Target;
        public float SecondsRemaining;
        public int NextRebound;
        public byte HasAimPoint, Cancelled;
        public Entity Contact;
        public float3 ContactNormal;
    }

    public struct LaunchReboundPlan
    {
        public Entity Surface, Target;
        public float3 Normal, Direction, AimPoint;
        public uint TargetGeneration;
        public byte HasAimPoint;
    }

    [InternalBufferCapacity(0)]
    public struct DisplayedLaunchRebound : IBufferElementData { public LaunchReboundPlan Value; }

    [InternalBufferCapacity(0)]
    public struct CommittedLaunchRebound : IBufferElementData { public LaunchReboundPlan Value; }
}
