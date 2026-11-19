using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    internal static class LinkedPairLaunchResolution
    {
        public static bool TryGet(EntityManager em, Entity endpoint, out Entity record, out LinkedPair pair)
        {
            record = em.HasComponent<LinkedPairEndpoint>(endpoint) ? em.GetComponentData<LinkedPairEndpoint>(endpoint).Pair : Entity.Null;
            pair = em.HasComponent<LinkedPair>(record) ? em.GetComponentData<LinkedPair>(record) : default;
            return record != Entity.Null && em.Exists(pair.A) && em.Exists(pair.B)
                && em.GetComponentData<EnemyLaunchState>(pair.A).Phase != EnemyLaunchPhase.Defeated
                && em.GetComponentData<EnemyLaunchState>(pair.B).Phase != EnemyLaunchPhase.Defeated
                && !em.IsComponentEnabled<RespawnRequest>(pair.A) && !em.IsComponentEnabled<RespawnRequest>(pair.B);
        }

        public static bool ArePartners(EntityManager em, Entity a, Entity b)
        {
            return em.HasComponent<LinkedPairEndpoint>(a) && em.HasComponent<LinkedPairEndpoint>(b)
                && SameRecord(em.GetComponentData<LinkedPairEndpoint>(a), em.GetComponentData<LinkedPairEndpoint>(b));
        }

        public static bool ArePartners(in ComponentLookup<LinkedPairEndpoint> endpoints, Entity a, Entity b)
        {
            return endpoints.TryGetComponent(a, out var first) && endpoints.TryGetComponent(b, out var second)
                && SameRecord(first, second);
        }

        private static bool SameRecord(in LinkedPairEndpoint a, in LinkedPairEndpoint b)
            => a.Pair != Entity.Null && a.Pair == b.Pair;

        public static Entity FlyingLeader(in LinkedPair pair, in EnemyLaunchState a, in EnemyLaunchState b)
        {
            bool flyingA = a.Phase == EnemyLaunchPhase.Launched;
            bool flyingB = b.Phase == EnemyLaunchPhase.Launched;
            if (!flyingA) return flyingB ? pair.B : Entity.Null;
            if (!flyingB) return pair.A;

            if (a.LaunchSequence != pair.FlightA.Sequence) return pair.A;
            if (b.LaunchSequence != pair.FlightB.Sequence) return pair.B;
            return pair.Leader == pair.B ? pair.B : pair.A;
        }
    }
}
