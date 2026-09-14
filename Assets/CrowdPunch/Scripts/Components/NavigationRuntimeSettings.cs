using Unity.Entities;
namespace CrowdPunch.Components
{
    public struct NavigationRuntimeSettings : IComponentData
    {
        public byte Enabled, DebugDrawing, Diagnostics;
        public float DirectInterval, WaypointArrival, LookAhead, DestinationThreshold;
        public float RepathCooldown, Stagger, StuckDuration, MinimumProgress, FailureDelay;
        public int SmoothingChecks, GlobalBudget, SearchLimit, ConcurrentSearches, MaxPathLength, MaxRequests;
    }
}
