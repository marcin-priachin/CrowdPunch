using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    internal static class AuthoritativeLaunchPlan
    {
        // Four contacts plus a step endpoint per forecast step, bounded by the existing 400-step cap.
        public const int MaximumPoints = 2001;
        public const float ContactTolerance = .025f;

        public static bool Owns(EntityManager em, Entity source, in EnemyLaunchState launch)
            => em.HasComponent<AuthoritativeLaunchPath>(source) && em.IsComponentEnabled<AuthoritativeLaunchPath>(source)
                && em.GetComponentData<AuthoritativeLaunchPath>(source).LaunchSequence == launch.LaunchSequence;

        public static void Release(EntityManager em, Entity source)
        {
            if (!em.HasComponent<AuthoritativeLaunchPath>(source) || !em.IsComponentEnabled<AuthoritativeLaunchPath>(source)) return;
            var path = em.GetComponentData<AuthoritativeLaunchPath>(source);
            if (em.HasComponent<PhysicsMassOverride>(source)) em.SetComponentData(source, path.SavedMassOverride);
            em.SetComponentEnabled<AuthoritativeLaunchPath>(source, false);
        }

        public static void Reset(EntityManager em, Entity source)
        {
            Release(em, source);
            if (em.HasBuffer<DisplayedLaunchPathPoint>(source)) em.GetBuffer<DisplayedLaunchPathPoint>(source).Clear();
            if (em.HasBuffer<CommittedLaunchPathPoint>(source)) em.GetBuffer<CommittedLaunchPathPoint>(source).Clear();
        }

        public static bool Begin(EntityManager em, Entity source, in PunchSpecification punch, in EnemyLaunchState launch)
        {
            Release(em, source);
            if (!em.HasComponent<AuthoritativeLaunchPath>(source) || !em.HasComponent<PhysicsMassOverride>(source)
                || !em.HasBuffer<CommittedLaunchPathPoint>(source)) return false;
            var active = em.GetBuffer<CommittedLaunchPathPoint>(source); active.Clear();
            if (punch.CommitDisplayedPlan == 0 || !em.HasBuffer<DisplayedLaunchPathPoint>(source)
                || punch.Cause != EnemyLaunchCause.PlayerPunch) return false;
            var aim = em.GetComponentData<PunchAimAssistTarget>(source);
            if (aim.PreviewRevision != punch.AimRevision) return false;
            var displayed = em.GetBuffer<DisplayedLaunchPathPoint>(source, true);
            if (displayed.Length < 2) return false;
            foreach (var point in displayed)
                if (active.Length < MaximumPoints) active.Add(new CommittedLaunchPathPoint { Value = point.Value });
            em.SetComponentData(source, new AuthoritativeLaunchPath { LaunchSequence = launch.LaunchSequence,
                NextPoint = 1, SavedMassOverride = em.GetComponentData<PhysicsMassOverride>(source) });
            em.SetComponentEnabled<AuthoritativeLaunchPath>(source, true);
            return true;
        }

        public static bool CrowdBody(EntityManager em, Entity body)
            => em.HasComponent<Enemy>(body) && !em.HasComponent<BossPart>(body) && !em.HasComponent<Barricade>(body)
                && !em.HasComponent<BossEncounter>(body) && !em.HasComponent<ChickenBoss>(body)
                && !em.HasComponent<RollingBoss>(body) && !em.HasComponent<DinoBoss>(body)
                && !em.HasComponent<FallingPillar>(body) && em.HasComponent<EnemyTier>(body)
                && EnemyLaunchTransition.IsLaunchable(em.GetComponentData<EnemyTier>(body));

        public static float3 ExitVelocity(in LaunchPathPoint point, in LaunchPathPoint next)
            // Geometry contains a small contact-separation offset. It must not turn a saved
            // safe boss rebound back toward the player when checking outgoing velocity.
            => point.Surface != Entity.Null ? next.Velocity : point.Velocity;

    }
}
