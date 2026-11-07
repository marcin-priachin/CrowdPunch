using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Pending damage to apply without changing archetypes every hit.
    /// </summary>
    public struct DamageRequest : IComponentData, IEnableableComponent
    {
        public float Amount;
        public uint DefeatPunchSequence;

        public void Add(float amount, float currentHealth, uint punchSequence)
        {
            // Preserve the first queued hit that crosses zero.
            if (Amount < currentHealth && Amount + amount >= currentHealth)
                DefeatPunchSequence = punchSequence;
            Amount += amount;
        }
    }
}
