using CrowdPunch.Components;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Applies velocity replacement semantics when an existing launch is punched again.</summary>
    public static class EnemyLaunchVelocity
    {
        public static void ResetForPlayerPunchReplacement(
            ref PhysicsVelocity velocity,
            EnemyLaunchPhase phase,
            EnemyLaunchCause cause)
        {
            if (phase != EnemyLaunchPhase.Launched || cause != EnemyLaunchCause.PlayerPunch)
            {
                return;
            }

            velocity.Linear = float3.zero;
            velocity.Angular = float3.zero;
        }
    }
}
