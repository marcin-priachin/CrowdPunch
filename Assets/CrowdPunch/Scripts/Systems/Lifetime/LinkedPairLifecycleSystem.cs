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
    [UpdateAfter(typeof(LinkedPairDamageSystem)), UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct LinkedPairLifecycleSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<LinkedPair>();
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager; float dt = SystemAPI.Time.DeltaTime;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (pairRef, tuning, record) in SystemAPI.Query<RefRW<LinkedPair>, RefRO<LinkedPairTuning>>().WithEntityAccess()) {
                var p = pairRef.ValueRO; var t = tuning.ValueRO;
                bool stale = !em.HasComponent<EnemyWaveSequence>(p.Sequence) || em.GetComponentData<EnemyWaveSequence>(p.Sequence).RunGeneration != p.RunGeneration;
                if (stale || !LinkedPairLaunchResolution.TryGet(em, p.A, out _, out _) ||
                    em.GetComponentData<EnemyLifetime>(p.A).Generation != p.LifetimeA || em.GetComponentData<EnemyLifetime>(p.B).Generation != p.LifetimeB) {
                    Detach(p.A); Detach(p.B); commands.DestroyEntity(record); continue;
                }
                if (!em.IsEnabled(p.A) || !em.IsEnabled(p.B)) continue; // Pending spawn warnings reserve an intact pair.
                if (p.Phase == EnemyLaunchPhase.Launched) {
                    p.FlightRemaining -= dt;
                    bool moving = math.length(em.GetComponentData<PhysicsVelocity>(p.A).Linear.xz) >= t.SettleSpeed
                        || math.length(em.GetComponentData<PhysicsVelocity>(p.B).Linear.xz) >= t.SettleSpeed;
                    p.SettledFor = moving ? 0 : p.SettledFor + dt;
                    if (p.FlightRemaining <= 0 || p.SettledFor >= t.SettlePeriod) {
                        Finish(p.A); Finish(p.B);
                        p.Phase = EnemyLaunchPhase.Recovering; p.RecoveryRemaining = t.RecoveryDuration;
                        if (em.GetComponentData<Health>(p.A).Current <= 0 || em.GetComponentData<Health>(p.B).Current <= 0) {
                            Detach(p.A); Detach(p.B); commands.DestroyEntity(record); continue;
                        }
                    }
                } else if (p.Phase == EnemyLaunchPhase.Recovering) {
                    p.RecoveryRemaining = math.max(0, p.RecoveryRemaining - dt);
                    SetRecovery(p.A); SetRecovery(p.B);
                    if (p.RecoveryRemaining <= 0) p.Phase = EnemyLaunchPhase.Active;
                }
                pairRef.ValueRW = p;

                void Finish(Entity endpoint) {
                    var launch = em.GetComponentData<EnemyLaunchState>(endpoint);
                    if (em.GetComponentData<Health>(endpoint).Current <= 0) {
                        launch.Phase = EnemyLaunchPhase.Defeated;
                        var damage = em.GetComponentData<EnemyDamageState>(endpoint); damage.IsDefeatDeferred = 0; em.SetComponentData(endpoint, damage);
                        em.SetComponentEnabled<DeathRequest>(endpoint, true);
                    } else { launch.Phase = EnemyLaunchPhase.Recovering; launch.RecoverySecondsRemaining = t.RecoveryDuration; }
                    em.SetComponentData(endpoint, launch);
                }
                void SetRecovery(Entity endpoint) {
                    var launch = em.GetComponentData<EnemyLaunchState>(endpoint); launch.RecoverySecondsRemaining = p.RecoveryRemaining;
                    if (p.RecoveryRemaining <= 0) { launch.Phase = EnemyLaunchPhase.Active; launch.LastCause = EnemyLaunchCause.None; }
                    em.SetComponentData(endpoint, launch);
                }
                void Detach(Entity endpoint) {
                    if (!em.HasComponent<LinkedPairEndpoint>(endpoint)) return;
                    em.SetComponentData(endpoint, new LinkedPairEndpoint());
                    em.SetComponentData(endpoint, new EnemyArchetype { Value = EnemyArchetypeKind.Baseline });
                }
            }
            commands.Playback(em);
        }
    }
}
