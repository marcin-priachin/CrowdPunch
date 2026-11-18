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
    public partial struct LaunchImpactResistanceJacobianSystem : ISystem
    {
        public void OnCreate(ref SystemState state) =>
            LaunchImpactResistanceContactSystem.RequireResistantTargets(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            state.Dependency = new ResistImpacts
            {
                Tiers = SystemAPI.GetComponentLookup<EnemyTier>(true),
                Dinos = SystemAPI.GetComponentLookup<DinoBoss>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true)
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), ref world, state.Dependency);
        }

        [BurstCompile]
        private struct ResistImpacts : IJacobiansJob
        {
            [ReadOnly] public ComponentLookup<EnemyTier> Tiers;
            [ReadOnly] public ComponentLookup<DinoBoss> Dinos;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;

            public void Execute(ref ModifiableJacobianHeader header, ref ModifiableContactJacobian contact)
            {
                if (!header.HasMassFactors || (header.Flags & JacobianFlags.Disabled) != 0) return;
                // COMBAT-012 / PILLAR-001: keep the launched body's solver response and
                // collision events while shielding the target from impulse and torque.
                var factors = header.MassFactors;
                if (LaunchImpactResistanceContactSystem.Resists(header.EntityA, header.EntityB, Tiers, Dinos, Launches))
                {
                    factors.InverseMassFactorA = 0f;
                    factors.InverseInertiaFactorA = 0f;
                }
                if (LaunchImpactResistanceContactSystem.Resists(header.EntityB, header.EntityA, Tiers, Dinos, Launches))
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
