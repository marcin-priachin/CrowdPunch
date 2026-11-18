using CrowdPunch.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateAfter(typeof(PhysicsCreateJacobiansGroup)), UpdateBefore(typeof(PhysicsSolveAndIntegrateGroup))]
    public partial struct EliteImpactJacobianSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<ElitePunchState>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            state.Dependency = new ResistImpacts
            {
                Tiers = SystemAPI.GetComponentLookup<EnemyTier>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true)
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), ref world, state.Dependency);
        }

        [BurstCompile]
        private struct ResistImpacts : IJacobiansJob
        {
            [ReadOnly] public ComponentLookup<EnemyTier> Tiers;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;

            public void Execute(ref ModifiableJacobianHeader header, ref ModifiableContactJacobian contact)
            {
                if (!header.HasMassFactors || (header.Flags & JacobianFlags.Disabled) != 0) return;
                // COMBAT-012: suppress the Elite's impulse and torque for this contact only.
                // The projectile still gets the solver response and generates impact events.
                var factors = header.MassFactors;
                if (EliteImpactContactSystem.Resists(header.EntityA, header.EntityB, Tiers, Launches))
                {
                    factors.InverseMassFactorA = 0f;
                    factors.InverseInertiaFactorA = 0f;
                }
                if (EliteImpactContactSystem.Resists(header.EntityB, header.EntityA, Tiers, Launches))
                {
                    factors.InverseMassFactorB = 0f;
                    factors.InverseInertiaFactorB = 0f;
                }
                header.MassFactors = factors;
            }

            public void Execute(ref ModifiableJacobianHeader header, ref ModifiableTriggerJacobian trigger) { }
        }
    }
}
