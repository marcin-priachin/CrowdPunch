using Unity.Entities;

namespace CrowdPunch.Components
{
    public enum EnemyLaunchPhase : byte
    {
        Active,
        Launched,
        Recovering,
        Defeated
    }

    public enum EnemyLaunchCause : byte
    {
        None,
        PlayerPunch,
        EnemyCollision
    }

    /// <summary>
    /// ECS-owned enemy launch lifecycle. The cause and count are retained for development inspection.
    /// </summary>
    public struct EnemyLaunchState : IComponentData
    {
        public EnemyLaunchPhase Phase;
        public EnemyLaunchCause LastCause;
        public float BelowUsefulMomentumSeconds;
        public float RecoverySecondsRemaining;
        public uint LaunchSequence;
        public float LaunchDamage;
        public uint PropagatedLaunchCount;
        public float LastPropagationImpulse;
    }
}
