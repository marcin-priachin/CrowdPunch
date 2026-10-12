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
    public partial struct SnakeContactSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SnakeSegment>();
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            state.Dependency = new Contacts { Segments = SystemAPI.GetComponentLookup<SnakeSegment>(true) }
                .Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), ref world, state.Dependency);
        }
        internal static bool Excludes(in SnakeSegment s, Entity other, ComponentLookup<SnakeSegment> segments)
        {
            if (s.Detached != 0 && s.Cleared != 0) return false;
            if (other == s.Head) return true;
            return segments.HasComponent(other) && segments[other].Head == s.Head
                && segments[other].Generation == s.Generation && segments[other].Detached == 0;
        }
        [BurstCompile]
        private struct Contacts : IContactsJob
        {
            [ReadOnly] public ComponentLookup<SnakeSegment> Segments;
            public void Execute(ref ModifiableContactHeader header, ref ModifiableContactPoint point)
            {
                if (Segments.HasComponent(header.EntityA) && Excludes(Segments[header.EntityA], header.EntityB, Segments)
                    || Segments.HasComponent(header.EntityB) && Excludes(Segments[header.EntityB], header.EntityA, Segments))
                    header.JacobianFlags |= JacobianFlags.Disabled;
            }
        }
    }
}
