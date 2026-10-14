using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

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

        public static void Begin(EntityManager em, Entity leader, EnemyLaunchState source, float3 velocity, bool replaceLeader = true)
        {
            if (!TryGet(em, leader, out var record, out var pair)) return;
            var tuning = em.GetComponentData<LinkedPairTuning>(record);
            Entity follower = leader == pair.A ? pair.B : pair.A;
            var state = em.GetComponentData<EnemyLaunchState>(follower);
            EnemyLaunchTransition.Begin(ref state, source.LastCause, source.LaunchDamage, source.Owner);
            state.FeedbackChainDepth = source.FeedbackChainDepth;
            em.SetComponentData(follower, state);
            if (replaceLeader) {
                em.SetComponentData(leader, source);
                em.SetComponentData(leader, new PhysicsVelocity { Linear = new float3(velocity.x, 0, velocity.z) });
                em.SetComponentEnabled<ExternalImpulse>(leader, false);
            }
            // Momentum is deliberately asymmetric: the tether accelerates the trailing endpoint.
            em.SetComponentData(follower, new PhysicsVelocity { Linear = new float3(velocity.x, 0, velocity.z) * .35f });
            em.SetComponentEnabled<ExternalImpulse>(follower, false);
            if (source.LastCause == EnemyLaunchCause.PlayerPunch) {
                var ground = em.GetComponentData<EnemyGroundConstraint>(follower); ground.SnapRequested = 1; em.SetComponentData(follower, ground);
            }
            pair.Leader = leader; pair.Phase = EnemyLaunchPhase.Launched;
            pair.FlightRemaining = tuning.FlightLimit; pair.SettledFor = pair.RecoveryRemaining = 0;
            em.SetComponentData(record, pair);
        }
    }
}
