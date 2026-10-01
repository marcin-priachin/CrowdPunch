using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    // Transient outputs from Burst zone simulation, drained before post-physics recovery.
    [InternalBufferCapacity(8)]
    public struct WizardPlayerHit : IBufferElementData
    {
        public float Damage;
        public float3 Impulse;
    }
}
