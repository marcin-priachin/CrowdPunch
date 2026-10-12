using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    internal static class SnakeSevering
    {
        // SNAKE-002: inspect the complete launch batch before deleting any disconnected suffix.
        public static void Resolve(EntityManager em, Entity headEntity, ref SnakeHead head,
            DynamicBuffer<SnakeTailLink> links, EntityCommandBuffer commands)
        {
            var launch = em.GetComponentData<EnemyLaunchState>(headEntity);
            bool removeAll = launch.Phase == EnemyLaunchPhase.Launched || launch.Phase == EnemyLaunchPhase.Defeated
                || em.IsComponentEnabled<RespawnRequest>(headEntity);
            int cut = removeAll ? 0 : links.Length;
            for (int i = 0; i < links.Length; i++)
            {
                var orb = links[i].Orb;
                if (!em.HasComponent<SnakeSegment>(orb)) { cut = mathMin(cut, i); continue; }
                var segment = em.GetComponentData<SnakeSegment>(orb);
                if (segment.Detached != 0) continue;
                if (em.GetComponentData<EnemyLaunchState>(orb).Phase != EnemyLaunchPhase.Launched) continue;
                cut = mathMin(cut, i);
                segment.Detached = 1; segment.Cleared = 0;
                em.SetComponentData(orb, segment);
                // Attached-orb health never determines cutting; detached projectiles can take ordinary damage.
                links[i] = new SnakeTailLink();
            }
            for (int i = cut; i < links.Length; i++)
            {
                var orb = links[i].Orb;
                if (em.Exists(orb)) commands.DestroyEntity(orb);
                links[i] = new SnakeTailLink();
            }
            head.AttachedCount = mathMin(head.AttachedCount, cut);
            if (head.AttachedCount == 0) em.SetComponentData(headEntity, head.DynamicMass);
        }
        private static int mathMin(int a, int b) => a < b ? a : b;
    }
}
