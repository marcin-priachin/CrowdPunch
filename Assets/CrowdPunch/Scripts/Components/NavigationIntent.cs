using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum NavigationMode : byte { Hold, Travel, Committed }
    public enum NavigationGoalKind : byte { Position, Coverage, DistanceBand, ExactSetup }
    public struct NavigationIntent : IComponentData
    {
        public float3 Destination, Separation;
        public float Speed, ArrivalDistance;
        public NavigationMode Mode;
        public NavigationGoalKind Kind;
        public static NavigationIntent Travel(float3 destination, float speed, float arrival, float3 separation,
            NavigationGoalKind kind = NavigationGoalKind.Position) => new NavigationIntent
            { Destination = destination, Speed = speed, ArrivalDistance = arrival, Separation = separation,
                Mode = NavigationMode.Travel, Kind = kind };
    }
    public struct NavigationAgent : IComponentData
    {
        public float Radius;
    }
    public enum NavigationTravelState : byte { Idle, Direct, Pending, Path, Failed }
    public struct NavigationPathState : IComponentData
    {
        public float2 RequestedGoal, ResolvedGoal, ProgressPosition, LastPosition;
        public uint Version, PathVersion;
        public int Waypoint;
        public double NextRequestAt, NextDirectAt, ProgressAt;
        public byte Pending, Initialized;
        public NavigationTravelState TravelState;
        public NavigationGoalKind Kind;
        public void Reset() { uint version = Version + 1; this = default; Version = version; }
        public bool Accepts(uint version) => Initialized != 0 && Pending != 0 && Version == version;
    }
    [InternalBufferCapacity(0)]
    public struct NavigationWaypoint : IBufferElementData { public float2 Position; }
}
