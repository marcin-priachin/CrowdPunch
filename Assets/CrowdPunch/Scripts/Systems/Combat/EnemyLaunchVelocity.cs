using CrowdPunch.Components;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Starts player-launched bodies from rest so the impulse matches the direction preview.</summary>
    public static class EnemyLaunchVelocity
    {
        public static void ResetForPlayerPunchReplacement(
            ref PhysicsVelocity velocity,
            EnemyLaunchPhase phase,
            EnemyLaunchCause cause)
        {
            if (phase == EnemyLaunchPhase.Defeated || cause != EnemyLaunchCause.PlayerPunch)
            {
                return;
            }

            velocity.Linear = float3.zero;
            velocity.Angular = float3.zero;
        }
    }
}
