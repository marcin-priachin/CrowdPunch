using Unity.Entities;

namespace CrowdPunch.Components
{
    public struct BarricadeFeedbackState : IComponentData
    {
        public uint ReportedHit;
    }
}
