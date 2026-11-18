using CrowdPunch.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateAfter(typeof(PhysicsCreateContactsGroup)), UpdateBefore(typeof(PhysicsCreateJacobiansGroup))]
    public partial struct EliteImpactContactSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<ElitePunchState>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            state.Dependency = new MarkContacts
            {
                Tiers = SystemAPI.GetComponentLookup<EnemyTier>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true)
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), ref world, state.Dependency);
        }

        internal static bool Resists(Entity target, Entity source,
            in ComponentLookup<EnemyTier> tiers, in ComponentLookup<EnemyLaunchState> launches)
            => tiers.TryGetComponent(target, out var tier) && tier.Value == EnemyCombatTier.Elite
                && launches.TryGetComponent(source, out var launch) && launch.Phase == EnemyLaunchPhase.Launched;

        [BurstCompile]
        private struct MarkContacts : IContactsJob
        {
            [ReadOnly] public ComponentLookup<EnemyTier> Tiers;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;

            public void Execute(ref ModifiableContactHeader header, ref ModifiableContactPoint contact)
            {
                // The solver allocates mass-factor storage when building this Jacobian.
                if (Resists(header.EntityA, header.EntityB, Tiers, Launches)
                    || Resists(header.EntityB, header.EntityA, Tiers, Launches))
                    header.JacobianFlags |= JacobianFlags.EnableMassFactors;
            }
        }
    }
}
