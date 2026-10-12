using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    internal static class SnakeHitRules
    {
        public static bool Protected(EntityManager em, Entity target) => em.HasComponent<SnakeHead>(target)
            && Protected(em.GetComponentData<SnakeHead>(target), em.GetComponentData<SnakeTuning>(target));
        public static bool Protected(in SnakeHead head, in SnakeTuning tuning) => head.AttachedCount > 0
            && tuning.HeadVulnerability == SnakeHeadVulnerability.ProtectedWhileTailRemains;
        public static bool Attached(EntityManager em, Entity target) => em.HasComponent<SnakeSegment>(target)
            && em.GetComponentData<SnakeSegment>(target).Detached == 0;
    }
}
