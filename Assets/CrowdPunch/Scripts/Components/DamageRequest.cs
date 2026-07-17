using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Pending damage to apply without changing archetypes every hit.
    /// </summary>
    public struct DamageRequest : IComponentData, IEnableableComponent
    {
        public float Amount;
    }
}
