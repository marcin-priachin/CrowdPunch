using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Per-enemy preferred spacing selected from the authored range at spawn time.
    /// </summary>
    public struct EnemySeparationDistance : IComponentData
    {
        public float Value;
    }
}
