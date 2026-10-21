using Unity.Entities;

namespace CrowdPunch.Components
{
    public enum GroundHazardStyle : byte { Default, Quarry, Observatory, Necropolis, Canyon }

    // Presentation only; footprint geometry, phase and damage retain their existing owners.
    public struct GroundHazardAppearance : IComponentData
    {
        public GroundHazardStyle Style;
    }
}
