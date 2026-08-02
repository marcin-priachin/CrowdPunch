using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Retains collision hit suppression only while the recorded source launch is still current.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateBefore(typeof(PunchDetectionSystem))]
    public partial struct CollisionDamageHistoryCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ComponentLookup<EnemyLaunchState> launchStateLookup =
                SystemAPI.GetComponentLookup<EnemyLaunchState>(true);

            foreach (DynamicBuffer<CollisionDamageHistory> history in
                     SystemAPI.Query<DynamicBuffer<CollisionDamageHistory>>().WithAll<Enemy>())
            {
                for (int index = history.Length - 1; index >= 0; index--)
                {
                    CollisionDamageHistory entry = history[index];
                    if (!launchStateLookup.HasComponent(entry.Source))
                    {
                        history.RemoveAtSwapBack(index);
                        continue;
                    }

                    EnemyLaunchState sourceState = launchStateLookup[entry.Source];
                    if (sourceState.Phase != EnemyLaunchPhase.Launched
                        || sourceState.LaunchSequence != entry.SourceLaunchSequence)
                    {
                        history.RemoveAtSwapBack(index);
                    }
                }
            }
        }
    }
}
