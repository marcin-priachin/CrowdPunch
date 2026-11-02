using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    internal static class CommittedLaunchPlan
    {
        public const int MaximumRebounds = 8;
        public const float NormalToleranceDegrees = 20f;

        public static bool Owns(EntityManager em, Entity source, in EnemyLaunchState launch)
            => em.HasComponent<CommittedLaunchFlight>(source) && em.IsComponentEnabled<CommittedLaunchFlight>(source)
                && em.GetComponentData<CommittedLaunchFlight>(source).LaunchSequence == launch.LaunchSequence;

        public static uint Generation(EntityManager em, Entity target)
            => em.HasComponent<EnemyLifetime>(target) ? em.GetComponentData<EnemyLifetime>(target).Generation : 0;

        public static bool ValidTarget(EntityManager em, Entity source, Entity target, uint generation)
        {
            if (!PunchAimAssist.IsValidTarget(em, source, target) || Generation(em, target) != generation) return false;
            if (!em.HasComponent<EnemyLaunchState>(target)) return true;
            var phase = em.GetComponentData<EnemyLaunchState>(target).Phase;
            return phase == EnemyLaunchPhase.Active || phase == EnemyLaunchPhase.Recovering;
        }

        public static void Reset(EntityManager em, Entity source)
        {
            AuthoritativeLaunchPlan.Reset(em, source);
            if (em.HasComponent<CommittedLaunchFlight>(source))
            {
                em.SetComponentData(source, new CommittedLaunchFlight());
                em.SetComponentEnabled<CommittedLaunchFlight>(source, false);
            }
            if (em.HasBuffer<DisplayedLaunchRebound>(source)) em.GetBuffer<DisplayedLaunchRebound>(source).Clear();
            if (em.HasBuffer<CommittedLaunchRebound>(source)) em.GetBuffer<CommittedLaunchRebound>(source).Clear();
        }

        public static void Begin(EntityManager em, Entity source, in PunchSpecification punch, in EnemyLaunchState launch)
        {
            bool authoritative = AuthoritativeLaunchPlan.Begin(em, source, punch, launch);
            if (!em.HasComponent<CommittedLaunchFlight>(source)) return;
            em.SetComponentEnabled<CommittedLaunchFlight>(source, false);
            if (!em.HasBuffer<CommittedLaunchRebound>(source)) return;
            var active = em.GetBuffer<CommittedLaunchRebound>(source);
            active.Clear();
            if (punch.CommitDisplayedPlan == 0 || punch.Cause != EnemyLaunchCause.PlayerPunch) return;
            var aim = em.GetComponentData<PunchAimAssistTarget>(source);
            if (aim.PreviewRevision != punch.AimRevision) return;
            if (em.HasBuffer<DisplayedLaunchRebound>(source))
                foreach (var rebound in em.GetBuffer<DisplayedLaunchRebound>(source, true))
                    if (active.Length < MaximumRebounds) active.Add(new CommittedLaunchRebound { Value = rebound.Value });
            em.SetComponentData(source, new CommittedLaunchFlight {
                LaunchSequence = launch.LaunchSequence, AimPoint = aim.PreviewAimPoint,
                Target = aim.PreviewTarget, TargetGeneration = aim.PreviewTargetGeneration,
                HasAimPoint = authoritative ? (byte)0 : aim.PreviewHasAimPoint,
                Cancelled = authoritative ? (byte)1 : (byte)0,
                SecondsRemaining = math.max(punch.HomingDuration, aim.PreviewPlanSeconds) });
            em.SetComponentEnabled<CommittedLaunchFlight>(source, true);
        }

        public static void Cancel(ref CommittedLaunchFlight flight, ref EnemyLaunchState launch)
        {
            flight.Cancelled = 1; flight.HasAimPoint = 0; flight.Contact = Entity.Null;
            LaunchAimPrediction.StopHoming(ref launch);
        }

        public static void Cancel(EntityManager em, Entity source, ref EnemyLaunchState launch)
        {
            if (!Owns(em, source, launch)) return;
            var flight = em.GetComponentData<CommittedLaunchFlight>(source);
            Cancel(ref flight, ref launch);
            em.SetComponentData(source, flight);
        }

        public static void RecordContact(ref CommittedLaunchFlight flight, Entity surface, float3 normal)
        {
            if (flight.Cancelled != 0) return;
            if (flight.Contact != Entity.Null && flight.Contact != surface) { flight.Cancelled = 1; return; }
            flight.Contact = surface; flight.ContactNormal = normal;
        }

        public static bool Blocked(EntityManager em, CollisionWorld world, Entity source, Entity target,
            Entity expectedSurface, float3 start, float3 end, ref NativeList<RaycastHit> rays)
        {
            rays.Clear();
            world.CastRay(new RaycastInput { Start = start, End = end, Filter = CollisionFilter.Default }, ref rays);
            foreach (var hit in rays)
                if (hit.Entity != source && hit.Entity != target && hit.Entity != expectedSurface && !em.HasComponent<Enemy>(hit.Entity)
                    && hit.Material.CollisionResponse != CollisionResponsePolicy.None
                    && hit.Material.CollisionResponse != CollisionResponsePolicy.RaiseTriggerEvents) return true;
            return false;
        }

        public static void Contact(EntityManager em, Entity source, in EnemyLaunchState launch, Entity surface, float3 normal)
        {
            if (!Owns(em, source, launch)) return;
            var flight = em.GetComponentData<CommittedLaunchFlight>(source);
            RecordContact(ref flight, surface, normal);
            em.SetComponentData(source, flight);
        }

        public static bool Align(float3 solved, in LaunchReboundPlan rebound, Entity surface, float3 normal,
            float maximumDegrees, out float3 result)
        {
            result = solved;
            if (rebound.Surface != surface || maximumDegrees <= 0f || math.lengthsq(solved.xz) < .000001f
                || math.dot(math.normalizesafe(rebound.Normal), math.normalizesafe(normal))
                    < math.cos(math.radians(NormalToleranceDegrees))
                || math.dot(rebound.Direction.xz, normal.xz) <= .0001f
                || !LaunchAimPrediction.WithinAngle(solved, rebound.Direction, maximumDegrees)) return false;
            result.xz = math.normalizesafe(rebound.Direction.xz) * math.length(solved.xz);
            return true;
        }
    }
}
