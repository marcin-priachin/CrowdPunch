using CrowdPunch.Components;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Maps direct launch causes to the owner whose rules the body carries.</summary>
    public static class EnemyLaunchOwnership
    {
        public static EnemyLaunchOwner FromCause(EnemyLaunchCause cause)
        {
            return cause == EnemyLaunchCause.PlayerPunch
                ? EnemyLaunchOwner.Player
                : cause == EnemyLaunchCause.None
                    ? EnemyLaunchOwner.None
                    : EnemyLaunchOwner.Enemy;
        }
    }
}
