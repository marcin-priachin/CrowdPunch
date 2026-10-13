using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    internal static class SnakeHitRules
    {
        public static bool Protected(EntityManager em, Entity target)
        {
            if (em.HasComponent<SnakeHead>(target)) return Protected(em.GetComponentData<SnakeHead>(target));
            if (!em.HasComponent<SnakeSegment>(target)) return false;
            var segment = em.GetComponentData<SnakeSegment>(target);
            return segment.Detached == 0 && (!em.HasComponent<SnakeHead>(segment.Head)
                || !IsTip(segment, em.GetComponentData<SnakeHead>(segment.Head)));
        }
        public static bool Protected(in SnakeHead head) => head.AttachedCount > 0;
        public static bool IsTip(in SnakeSegment segment, in SnakeHead head) => segment.Detached == 0
            && segment.Generation == head.Generation && head.AttachedCount > 0
            && segment.Index == head.AttachedCount - 1;
        public static bool Protected(Entity target, ComponentLookup<SnakeHead> heads, ComponentLookup<SnakeSegment> segments)
        {
            if (heads.HasComponent(target)) return Protected(heads[target]);
            if (!segments.HasComponent(target)) return false;
            var segment = segments[target];
            return segment.Detached == 0 && (!heads.HasComponent(segment.Head) || !IsTip(segment, heads[segment.Head]));
        }
        public static bool Attached(EntityManager em, Entity target) => em.HasComponent<SnakeSegment>(target)
            && em.GetComponentData<SnakeSegment>(target).Detached == 0;
    }
}
