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
    public partial struct CoverEnclosureContactSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<CoverEnclosure>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            state.Dependency = new FilterContacts {
                Enclosures = SystemAPI.GetComponentLookup<CoverEnclosure>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true)
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), ref world, state.Dependency);
        }

        [BurstCompile]
        private struct FilterContacts : IContactsJob
        {
            [ReadOnly] public ComponentLookup<CoverEnclosure> Enclosures;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            public void Execute(ref ModifiableContactHeader header, ref ModifiableContactPoint contact)
            {
                var body = Enclosures.HasComponent(header.EntityA) ? header.EntityB
                    : Enclosures.HasComponent(header.EntityB) ? header.EntityA : Entity.Null;
                if (Launches.HasComponent(body) && Launches[body].Phase == EnemyLaunchPhase.Launched)
                    header.JacobianFlags |= JacobianFlags.Disabled;
            }
        }
    }
}
