using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace CrowdPunch.Utilities
{
    public static class NavigationGridConstruction
    {
        public static BlobAssetReference<NavigationGridBlob> Build(float2 minimum, float2 maximum, float cellSize, float3 radii,
            NativeArray<NavigationRectangle> rectangles, Allocator allocator)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var g = ref builder.ConstructRoot<NavigationGridBlob>();
            g.Minimum = minimum; g.Maximum = maximum; g.CellSize = cellSize; g.Radii = radii;
            g.Size = (int2)math.ceil((maximum - minimum) / cellSize);
            int count = g.Size.x * g.Size.y;
            if (count <= 0 || count > 65536) throw new System.ArgumentException("Navigation grid must contain 1..65536 cells. Increase cell size or correct arena bounds.");
            var obstacles = builder.Allocate(ref g.Obstacles, rectangles.Length);
            for (int i = 0; i < rectangles.Length; i++) obstacles[i] = rectangles[i];
            var regions = builder.Allocate(ref g.Regions, count * 3); var edges = builder.Allocate(ref g.Edges, count * 3);
            // BlobBuilder offsets cannot be read as final blob arrays until creation.
            var result = builder.CreateBlobAssetReference<NavigationGridBlob>(allocator);
            ref var data = ref result.Value;
            var queue = new NativeArray<int>(count, Allocator.Temp);
            for (int cls = 0; cls < 3; cls++)
            {
                for (int cell = 0; cell < count; cell++)
                {
                    float2 p = NavigationGeometry.Center(ref data, cell);
                    data.Regions[cls * count + cell] = NavigationGeometry.Segment(ref data, p, p, radii[cls]) ? -1 : 0;
                }
                for (int cell = 0; cell < count; cell++)
                {
                    if (data.Regions[cls * count + cell] == 0) continue;
                    byte mask = 0;
                    for (int d = 0; d < 8; d++)
                    {
                        int n = NavigationGeometry.Neighbor(ref data, cell, d);
                        if (n < 0 || data.Regions[cls * count + n] == 0) continue;
                        int2 off = NavigationGeometry.Offset(d);
                        if (d >= 4 && (data.Regions[cls * count + cell + off.x] == 0 || data.Regions[cls * count + cell + off.y * data.Size.x] == 0)) continue;
                        if (NavigationGeometry.Segment(ref data, NavigationGeometry.Center(ref data, cell), NavigationGeometry.Center(ref data, n), radii[cls])) mask |= (byte)(1 << d);
                    }
                    data.Edges[cls * count + cell] = mask;
                }
                int region = 0;
                for (int cell = 0; cell < count; cell++)
                {
                    if (data.Regions[cls * count + cell] != -1) continue;
                    region++; int head = 0, tail = 1; queue[0] = cell; data.Regions[cls * count + cell] = region;
                    while (head < tail)
                    {
                        int current = queue[head++]; byte mask = data.Edges[cls * count + current];
                        for (int d = 0; d < 8; d++) if ((mask & (1 << d)) != 0)
                            {
                                int n = NavigationGeometry.Neighbor(ref data, current, d);
                                if (data.Regions[cls * count + n] == -1) { data.Regions[cls * count + n] = region; queue[tail++] = n; }
                            }
                    }
                }
            }
            queue.Dispose(); return result;
        }
    }
}
