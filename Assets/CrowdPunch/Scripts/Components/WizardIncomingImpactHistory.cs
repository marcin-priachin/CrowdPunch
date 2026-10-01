using Unity.Entities;

namespace CrowdPunch.Components
{
    [InternalBufferCapacity(0)]
    public struct WizardIncomingImpactHistory : IBufferElementData
    {
        public Entity Source;
        public uint ContinuousFlight;
    }
}
