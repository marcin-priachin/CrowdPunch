using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Enableable marker for enemies that should be returned to a valid spawn position.
    /// </summary>
    public struct RespawnRequest : IComponentData, IEnableableComponent
    {
        public double RespawnAt;
        public byte IsPooled;
        public double ForcePoolAt;
        public byte FromPlayerPunch;
    }
}
