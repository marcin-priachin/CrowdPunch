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
            if (other == s.Head) return s.Detached == 0 || s.BouncedOutsideOwner == 0;
            return segments.HasComponent(other) && Excludes(s, segments[other]);
        }
        internal static bool Excludes(in SnakeSegment s, in SnakeSegment other)
        {
            if (other.Head != s.Head || other.Generation != s.Generation) return false;
            // An attached counterpart must not re-disable a returning, bounced projectile.
            return s.Detached != 0 ? s.BouncedOutsideOwner == 0 : other.Detached == 0;
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
