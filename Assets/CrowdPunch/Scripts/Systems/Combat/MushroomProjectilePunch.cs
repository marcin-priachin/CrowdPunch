using CrowdPunch.Components;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    internal static class MushroomProjectilePunch
    {
        internal static bool Overlaps(float3 offset, float3 forward, float reach, float width, float shotRadius)
        {
            // PunchResolution uses a finite horizontal strip with a separate height band.
            float along = math.dot(offset, forward);
            float side = math.abs(math.dot(offset, math.cross(math.up(), forward)));
            float3 gap = new float3(along - math.clamp(along, 0f, math.max(0f, reach)),
                math.max(0f, math.abs(offset.y) - width), math.max(0f, side - width));
            return math.lengthsq(gap) <= shotRadius * shotRadius;
        }

        internal static void Redirect(ref MushroomProjectile shot, float3 direction, uint sequence)
        {
            shot.Direction = direction;
            shot.Owner = EnemyLaunchOwner.Player;
            shot.RangeRemaining = shot.Range;
            shot.PunchSequence = sequence;
            shot.ExitingRedirectContact = true;
        }
    }
}
