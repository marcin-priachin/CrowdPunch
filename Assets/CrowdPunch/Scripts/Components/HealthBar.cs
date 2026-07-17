using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Presentation-facing health bar value derived from health.
    /// </summary>
    public struct HealthBar : IComponentData
    {
        public float Normalized;
    }
}
