using Unity.Entities;
using Unity.Mathematics;

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
        EnemyCollision,
        Explosion,
        ElitePunch,
        BossAttack,
        WizardZone,
        EnvironmentImpact
    }

    public enum EnemyLaunchOwner : byte
    {
        None,
        Player,
        Enemy,
        Boss,
        Environment
    }

    public struct LaunchChainOrigin
    {
        public Entity Source;
        public uint Lifetime, Sequence;
    }

    /// <summary>
    /// ECS-owned enemy launch lifecycle. The cause and count are retained for development inspection.
    /// </summary>
    public struct EnemyLaunchState : IComponentData
    {
        public EnemyLaunchPhase Phase;
        public EnemyLaunchCause LastCause;
        public EnemyLaunchOwner Owner;
        public float BelowUsefulMomentumSeconds;
        public float RecoverySecondsRemaining;
        public uint LaunchSequence;
        public uint OriginatingPunchSequence;
        public LaunchChainOrigin FallbackChain;
        public uint ContinuousFlight;
        public int FeedbackChainDepth;
        public float LaunchDamage;
        public uint PropagatedLaunchCount;
        public float LastPropagationImpulse;
        public uint PlayerImpactLaunchSequence;
        public Entity HomingTarget;
        public float HomingSecondsRemaining;
        public Entity RicochetSurface;
        public float3 RicochetNormal;
    }
}
