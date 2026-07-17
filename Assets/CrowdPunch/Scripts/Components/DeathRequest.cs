using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Marks an entity whose health reached zero for lifetime systems.
    /// </summary>
    public struct DeathRequest : IComponentData, IEnableableComponent
    {
    }
}
