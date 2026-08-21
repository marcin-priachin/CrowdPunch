using CrowdPunch.Components;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Defines the one normal transition into the launched lifecycle for every launch source.</summary>
    internal static class EnemyLaunchTransition
    {
        public static bool CanReceivePlayerPunch(in EnemyLaunchState state, in Health health)
        {
            return state.Phase == EnemyLaunchPhase.Launched
                || (health.Current > 0f
                    && (state.Phase == EnemyLaunchPhase.Active
                        || state.Phase == EnemyLaunchPhase.Recovering));
        }

        public static bool IsLaunchable(in EnemyTier tier)
        {
            return tier.Value == EnemyCombatTier.Normal;
        }

        public static void Begin(ref EnemyLaunchState state, EnemyLaunchCause cause, float launchDamage)
        {
            EnemyLaunchOwner owner = EnemyLaunchOwnership.FromCause(cause);
            Begin(ref state, cause, launchDamage, owner);
        }

        public static void Begin(
            ref EnemyLaunchState state,
            EnemyLaunchCause cause,
            float launchDamage,
            EnemyLaunchOwner owner)
        {
            state.Phase = EnemyLaunchPhase.Launched;
            state.LastCause = cause;
            state.Owner = owner;
            state.BelowUsefulMomentumSeconds = 0f;
            state.RecoverySecondsRemaining = 0f;
            state.LaunchSequence++;
            state.LaunchDamage = launchDamage < 0f ? 0f : launchDamage;
            state.PropagatedLaunchCount = 0;
            state.LastPropagationImpulse = 0f;
        }
    }
}
