using Unity.Entities;

namespace CrowdPunch.Components
{
    // Presentation acknowledgements are independent of collision histories and damage state.
    public struct PillarFeedbackState : IComponentData
    {
        public uint FallSequence;
        public byte GroundShown, BossShown;
    }
}
