using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Lifetime
{
    [BurstCompile, UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(ExplosionResolutionSystem)), UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct LinkedPairLifecycleSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LinkedPair>();
            state.RequireForUpdate<EnemyLaunchSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager; float dt = SystemAPI.Time.DeltaTime;
            float usefulMomentumSpeed = math.max(0f, SystemAPI.GetSingleton<EnemyLaunchSettings>().UsefulMomentumSpeed);
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (pairRef, tuning, record) in SystemAPI.Query<RefRW<LinkedPair>, RefRO<LinkedPairTuning>>().WithEntityAccess()) {
                var p = pairRef.ValueRO; var t = tuning.ValueRO;
                bool stale = !em.HasComponent<EnemyWaveSequence>(p.Sequence) || em.GetComponentData<EnemyWaveSequence>(p.Sequence).RunGeneration != p.RunGeneration;
                if (stale || !LinkedPairLaunchResolution.TryGet(em, p.A, out _, out _) ||
                    em.GetComponentData<EnemyLifetime>(p.A).Generation != p.LifetimeA || em.GetComponentData<EnemyLifetime>(p.B).Generation != p.LifetimeB) {
                    Detach(p.A); Detach(p.B); commands.DestroyEntity(record); continue;
                }
                if (!em.IsEnabled(p.A) || !em.IsEnabled(p.B)) continue; // Pending spawn warnings reserve an intact pair.
                var a = em.GetComponentData<EnemyLaunchState>(p.A);
                var b = em.GetComponentData<EnemyLaunchState>(p.B);
                p.Leader = LinkedPairLaunchResolution.FlyingLeader(p, a, b);
                AdvanceEndpoint(em, p.A, ref a, ref p.FlightA, t, usefulMomentumSpeed, dt);
                AdvanceEndpoint(em, p.B, ref b, ref p.FlightB, t, usefulMomentumSpeed, dt);
                if (a.Phase == EnemyLaunchPhase.Defeated || b.Phase == EnemyLaunchPhase.Defeated) {
                    Detach(p.A); Detach(p.B); commands.DestroyEntity(record); continue;
                }

                p.Leader = LinkedPairLaunchResolution.FlyingLeader(p, a, b);
                p.Phase = p.Leader != Entity.Null ? EnemyLaunchPhase.Launched
                    : a.Phase == EnemyLaunchPhase.Recovering || b.Phase == EnemyLaunchPhase.Recovering
                        ? EnemyLaunchPhase.Recovering : EnemyLaunchPhase.Active;
                pairRef.ValueRW = p;

                void Detach(Entity endpoint) {
                    if (!em.HasComponent<LinkedPairEndpoint>(endpoint)
                        || em.GetComponentData<LinkedPairEndpoint>(endpoint).Pair != record) return;
                    em.SetComponentData(endpoint, new LinkedPairEndpoint());
                    em.SetComponentData(endpoint, new EnemyArchetype { Value = EnemyArchetypeKind.Baseline });
                }
            }
            commands.Playback(em);
        }

        private static void AdvanceEndpoint(EntityManager em, Entity endpoint, ref EnemyLaunchState launch,
            ref LinkedPairFlight flight, in LinkedPairTuning tuning, float usefulMomentumSpeed, float dt)
        {
            if (launch.Phase == EnemyLaunchPhase.Active) return;
            if (launch.Phase == EnemyLaunchPhase.Launched)
            {
                if (flight.Sequence != launch.LaunchSequence)
                    flight = new LinkedPairFlight { Sequence = launch.LaunchSequence, Remaining = tuning.FlightLimit };

                flight.Remaining -= dt;
                float speed = math.length(em.GetComponentData<PhysicsVelocity>(endpoint).Linear.xz);
                bool belowUsefulMomentum = speed < usefulMomentumSpeed;
                launch.BelowUsefulMomentumSeconds = belowUsefulMomentum ? launch.BelowUsefulMomentumSeconds + dt : 0f;
                if (flight.Remaining <= 0f || (belowUsefulMomentum && launch.BelowUsefulMomentumSeconds >= tuning.SettlePeriod))
                {
                    if (em.GetComponentData<Health>(endpoint).Current <= 0f)
                    {
                        var damage = em.GetComponentData<EnemyDamageState>(endpoint);
                        EnemyRecoverySystem.MarkDefeated(ref launch, ref damage);
                        em.SetComponentData(endpoint, damage);
                        em.SetComponentEnabled<DeathRequest>(endpoint, true);
                    }
                    else
                    {
                        launch.Phase = EnemyLaunchPhase.Recovering;
                        launch.RecoverySecondsRemaining = tuning.RecoveryDuration;
                    }
                }
            }
            else if (launch.Phase == EnemyLaunchPhase.Recovering)
            {
                launch.RecoverySecondsRemaining = math.max(0f, launch.RecoverySecondsRemaining - dt);
                if (launch.RecoverySecondsRemaining <= 0f)
                {
                    launch.Phase = EnemyLaunchPhase.Active;
                    launch.LastCause = EnemyLaunchCause.None;
                    launch.BelowUsefulMomentumSeconds = 0f;
                }
            }
            em.SetComponentData(endpoint, launch);
        }
    }
}
