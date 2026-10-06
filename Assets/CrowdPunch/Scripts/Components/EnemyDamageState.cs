using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Development-facing record of the last applied damage and deferred-defeat decision.
    /// </summary>
    public struct EnemyDamageState : IComponentData
    {
        public float LastDamageReceived;
        public byte IsDefeatDeferred;
        public EnemyLaunchOwner LastDamageOwner, DefeatOwner;
        public Entity LastDamageSource, DefeatSource;
        public uint LastDamageSourceLifetime, DefeatSourceLifetime;
        public int LastDamageChainDepth, DefeatChainDepth;
    }
}
