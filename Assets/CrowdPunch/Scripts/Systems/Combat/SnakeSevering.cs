using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    internal static class SnakeSevering
    {
        // SNAKE-002: expose the predecessor only after the current hit batch has resolved.
        public static void Resolve(EntityManager em, Entity headEntity, ref SnakeHead head,
            DynamicBuffer<SnakeTailLink> links, EntityCommandBuffer commands)
        {
            var launch = em.GetComponentData<EnemyLaunchState>(headEntity);
            bool removeAll = launch.Phase == EnemyLaunchPhase.Launched || launch.Phase == EnemyLaunchPhase.Defeated
                || em.IsComponentEnabled<RespawnRequest>(headEntity);
            if (removeAll)
            {
                for (int i = 0; i < links.Length; i++)
                {
                    if (em.Exists(links[i].Orb)) commands.DestroyEntity(links[i].Orb);
                    links[i] = default;
                }
                head.AttachedCount = 0;
            }
            else
            {
                head.AttachedCount = mathMin(head.AttachedCount, links.Length);
                // Missing/escaped terminal pieces cannot leave an unreachable weak point.
                while (head.AttachedCount > 0)
                {
                    int index = head.AttachedCount - 1;
                    var orb = links[index].Orb;
                    if (!em.HasComponent<SnakeSegment>(orb)
                        || em.GetComponentData<EnemyLaunchState>(orb).Phase == EnemyLaunchPhase.Defeated)
                    {
                        if (em.Exists(orb)) commands.DestroyEntity(orb);
                        links[index] = default; head.AttachedCount--; continue;
                    }
                    var segment = em.GetComponentData<SnakeSegment>(orb);
                    if (!SnakeHitRules.IsTip(segment, head)
                        || em.GetComponentData<EnemyLaunchState>(orb).Phase != EnemyLaunchPhase.Launched) break;
                    segment.Detached = 1; segment.Cleared = 0;
                    em.SetComponentData(orb, segment);
                    links[index] = default; head.AttachedCount--;
                    break;
                }
            }
            if (head.AttachedCount == 0) em.SetComponentData(headEntity, head.DynamicMass);
        }
        private static int mathMin(int a, int b) => a < b ? a : b;
    }
}
