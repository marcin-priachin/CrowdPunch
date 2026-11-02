using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Components
{
    public struct LaunchPathPoint
    {
        public float Seconds;
        public float3 Position, Velocity;
        public Entity Surface;
        public float3 Normal;
    }

    [InternalBufferCapacity(0)]
    public struct DisplayedLaunchPathPoint : IBufferElementData { public LaunchPathPoint Value; }

    [InternalBufferCapacity(0)]
    public struct CommittedLaunchPathPoint : IBufferElementData { public LaunchPathPoint Value; }

    // Only enabled while the directly punched body is being driven along its saved path.
    public struct AuthoritativeLaunchPath : IComponentData, IEnableableComponent
    {
        public uint LaunchSequence;
        public int NextPoint;
        public float Seconds;
        public PhysicsMassOverride SavedMassOverride;
        public float3 ScheduledPosition, ExitVelocity, IncomingVelocity;
        public Entity CrowdSurface;
        public float3 CrowdIncomingVelocity;
        // Confirmed by this tick's live collider sweep, never copied from the forecast.
        public Entity Contact;
        public float3 ContactPoint, ContactNormal;
        public float ContactImpulse;
        public byte ReachedPoint, ExpectedContact, ReleaseAfterContact;
    }
}
