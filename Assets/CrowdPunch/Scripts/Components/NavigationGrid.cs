using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct NavigationRectangle { public float2 Minimum, Maximum; }
    public struct NavigationGridBlob
    {
        public float2 Minimum, Maximum;
        public float CellSize, Margin;
        public int2 Size;
        public float3 Radii;
        public BlobArray<NavigationRectangle> Obstacles;
        // Class-major arrays. Region zero is blocked; edges use the eight direction bits.
        public BlobArray<int> Regions;
        public BlobArray<byte> Edges;
    }
    public struct NavigationGrid : IComponentData
    {
        public BlobAssetReference<NavigationGridBlob> Data;
        public float2 ParticipationAnchor;
    }
    public struct NavigationDiagnostics : IComponentData
    {
        public int Queued, Searching, Expanded, Direct, Following, Waiting;
        public int Failures, LimitReached, Replans, StaleResults, RejectedSpawns;
        public double Milliseconds;
    }
}
