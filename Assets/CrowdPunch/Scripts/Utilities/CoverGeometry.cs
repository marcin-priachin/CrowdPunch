using CrowdPunch.Components;
using Unity.Mathematics;

namespace CrowdPunch.Utilities
{
    public static class CoverGeometry
    {
        public const int PanelCount = 36;

        public static void Panel(in RotatingCover cover, int index, out float3 position,
            out quaternion rotation, out float3 size)
        {
            float step = (2 * math.PI - cover.OpeningRadians) / PanelCount;
            float angle = cover.OpeningRadians * .5f + (index + .5f) * step;
            rotation = quaternion.RotateY(angle);
            position = math.rotate(rotation, new float3(0, 0, cover.Radius));
            size = new float3(2 * (cover.Radius + cover.Thickness * .5f) * math.tan(step * .5f) + .02f,
                cover.Height, cover.Thickness);
        }
    }
}
