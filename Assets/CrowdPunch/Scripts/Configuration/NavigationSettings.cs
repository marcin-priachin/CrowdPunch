using CrowdPunch.Components;
using UnityEngine;
namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(fileName = "NavigationSettings", menuName = "Crowd Punch/Navigation Settings")]
    public sealed class NavigationSettings : ScriptableObject
    {
        [Header("Baked configuration - all edits require rebaking / scene reload")]
        [Tooltip("Disables route assistance only; solid collision remains active. Runtime comparison is available in the Navigation inspector window.")]
        public bool assistanceEnabled = true;
        [Range(0.25f, 4f), Tooltip("Square navigation resolution in world metres, independent of obstacle placement units.")]
        public float cellSize = 1f;
        [Range(0f, 0.5f), Tooltip("Added to every clearance class; conservative square corner inflation.")]
        public float clearanceMargin = 0.12f;
        [Tooltip("Three maximum horizontal collider radii. Each enemy rounds up. Larger bodies are rejected, never silently under-cleared.")]
        public Vector3 clearanceRadii = new Vector3(0.5f, 0.85f, 1.5f);
        [Header("Direct travel and path following")]
        [Range(0.02f, 1f), Tooltip("Seconds between full tactical-goal direct checks. Local safety is checked each update.")]
        public float directInterval = 0.2f;
        [Range(0.05f, 1f), Tooltip("Waypoint arrival distance in metres.")]
        public float waypointArrival = 0.25f;
        [Range(0.2f, 4f), Tooltip("Minimum forward safety probe, enlarged by braking distance.")]
        public float lookAhead = 0.7f;
        [Range(1, 32), Tooltip("Maximum safe shortcut candidates inspected per actor per update.")]
        public int smoothingChecks = 8;
        [Range(0.25f, 5f), Tooltip("Tactical goal movement needed to invalidate cached destination.")]
        public float destinationThreshold = 1.5f;
        [Header("Replanning and recovery")]
        [Range(0.1f, 5f), Tooltip("Minimum interval between admitted requests.")]
        public float repathCooldown = 0.6f;
        [Range(0f, 2f), Tooltip("Deterministic entity-based request / retry spread in seconds.")]
        public float requestStagger = 0.3f;
        [Range(0.5f, 10f), Tooltip("Seconds without meaningful movement before one replan.")]
        public float stuckDuration = 2.5f;
        [Range(0.05f, 2f), Tooltip("Metres of progress that reset the stuck timer.")]
        public float minimumProgress = 0.3f;
        [Range(0.5f, 15f), Tooltip("Retry cooldown for failed or search-limit results.")]
        public float failureDelay = 3f;
        [Header("Shared resource bounds")]
        [Range(1, 8192), Tooltip("Total A* node expansions per simulation update, shared fairly.")]
        public int globalNodeBudget = 512;
        [Range(16, 65536), Tooltip("Total expansions allowed for one search. Reaching this is a resource limit, not proof of unreachability.")]
        public int perSearchLimit = 4096;
        [Range(1, 16), Tooltip("Persistent concurrent search scratch slots.")]
        public int concurrentSearches = 4;
        [Range(8, 512), Tooltip("Maximum stored grid waypoints per enemy. Overlong paths fail safely.")]
        public int maximumPathLength = 128;
        [Range(16, 16384), Tooltip("Bounded FIFO request storage. Full queues brake and retry with staggering.")]
        public int maximumRequests = 4096;
        [Header("Optional diagnostics - no normal HUD")]
        [Tooltip("Draw grid, regions, goals and routes in Scene view through the navigation window.")]
        public bool debugDrawing;
        [Tooltip("Collect navigation elapsed CPU time. Profiler markers remain available independently.")]
        public bool diagnostics = true;
        public NavigationRuntimeSettings Runtime => new NavigationRuntimeSettings {
            Enabled = assistanceEnabled ? (byte)1 : (byte)0, DebugDrawing = debugDrawing ? (byte)1 : (byte)0,
            Diagnostics = diagnostics ? (byte)1 : (byte)0, DirectInterval = directInterval,
            WaypointArrival = waypointArrival, LookAhead = lookAhead, SmoothingChecks = smoothingChecks,
            DestinationThreshold = destinationThreshold, RepathCooldown = repathCooldown, Stagger = requestStagger,
            StuckDuration = stuckDuration, MinimumProgress = minimumProgress, FailureDelay = failureDelay,
            GlobalBudget = globalNodeBudget, SearchLimit = perSearchLimit, ConcurrentSearches = concurrentSearches,
            MaxPathLength = maximumPathLength, MaxRequests = maximumRequests };
        private void OnValidate()
        {
            cellSize = Mathf.Clamp(cellSize, .25f, 4); clearanceMargin = Mathf.Clamp(clearanceMargin, 0, .5f);
            clearanceRadii.x = Mathf.Max(.05f, clearanceRadii.x);
            clearanceRadii.y = Mathf.Max(clearanceRadii.x, clearanceRadii.y);
            clearanceRadii.z = Mathf.Max(clearanceRadii.y, clearanceRadii.z);
            directInterval = Mathf.Clamp(directInterval,.02f,1); waypointArrival = Mathf.Clamp(waypointArrival,.05f,1);
            lookAhead = Mathf.Clamp(lookAhead,.2f,4); smoothingChecks = Mathf.Clamp(smoothingChecks,1,32);
            destinationThreshold = Mathf.Clamp(destinationThreshold,.25f,5); repathCooldown = Mathf.Clamp(repathCooldown,.1f,5);
            requestStagger = Mathf.Clamp(requestStagger,0,2); stuckDuration = Mathf.Clamp(stuckDuration,.5f,10);
            minimumProgress = Mathf.Clamp(minimumProgress,.05f,2); failureDelay = Mathf.Clamp(failureDelay,.5f,15);
            globalNodeBudget = Mathf.Clamp(globalNodeBudget,1,8192); perSearchLimit = Mathf.Clamp(perSearchLimit,16,65536);
            concurrentSearches = Mathf.Clamp(concurrentSearches,1,16); maximumPathLength = Mathf.Clamp(maximumPathLength,8,512);
            maximumRequests = Mathf.Clamp(maximumRequests,16,16384);
        }
    }
}
