using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateAfter(typeof(PhysicsCreateBodyPairsGroup)), UpdateBefore(typeof(PhysicsCreateContactsGroup))]
    public partial struct LinkedPairContactSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LinkedPair>();
            state.RequireForUpdate<SimulationSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            state.Dependency = new SuppressPartnerContactJob
            {
                Endpoints = SystemAPI.GetComponentLookup<LinkedPairEndpoint>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true)
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), ref world, state.Dependency);
        }

        [BurstCompile]
        private struct SuppressPartnerContactJob : IBodyPairsJob
        {
            [ReadOnly] public ComponentLookup<LinkedPairEndpoint> Endpoints;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;

            public void Execute(ref ModifiableBodyPair pair)
            {
                if (LinkedPairLaunchResolution.ArePartners(Endpoints, pair.EntityA, pair.EntityB)
                    && (IsFlying(pair.EntityA) || IsFlying(pair.EntityB)))
                    pair.Disable();
            }

            private bool IsFlying(Entity endpoint)
                => Launches.TryGetComponent(endpoint, out var launch) && launch.Phase == EnemyLaunchPhase.Launched;
        }
    }
}
