using CrowdPunch.Components;
using CrowdPunch.Utilities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Movement
{
    internal static class SnakeHeadMovement
    {
        // SNAKE-001: a protected kinematic head cannot rely on solver push-out. Navigation
        // validates desired intent, but acceleration can leave the actual velocity across a corner.
        public static float2 LimitStep(ref NavigationGridBlob grid, float2 position, float2 velocity,
            float radius, float deltaTime)
        {
            if (deltaTime <= 0 || math.lengthsq(velocity) <= .000001f) return velocity;
            float2 end = position + velocity * deltaTime;
            int clearance = NavigationGeometry.ClearanceClass(ref grid, radius);
            if (clearance >= 0 && NavigationGeometry.Segment(ref grid, position, position, grid.Radii[clearance]))
                return NavigationGeometry.Segment(ref grid, position, end, grid.Radii[clearance]) ? velocity : float2.zero;

            // Retain navigation's physical-radius escape from conservative corner margins or
            // out-of-grid recovery positions. Never force a clear body back into the obstacle.
            float2 minimum = grid.Minimum + radius, maximum = grid.Maximum - radius;
            for (int axis = 0; axis < 2; axis++)
            {
                if (position[axis] < minimum[axis] ? end[axis] < position[axis]
                    : position[axis] > maximum[axis] ? end[axis] > position[axis]
                    : end[axis] < minimum[axis] || end[axis] > maximum[axis]) return float2.zero;
            }
            for (int i = 0; i < grid.Obstacles.Length; i++)
                if (NavigationGeometry.SweptCircleIntersectsRectangle(position, end, radius,
                    grid.Obstacles[i].Minimum, grid.Obstacles[i].Maximum)) return float2.zero;
            return velocity;
        }
    }
}
