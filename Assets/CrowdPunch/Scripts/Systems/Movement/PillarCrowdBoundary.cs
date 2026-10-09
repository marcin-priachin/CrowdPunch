using Unity.Mathematics;
namespace CrowdPunch.Systems.Movement
{
    internal static class PillarCrowdBoundary
    {
        internal static float2 RemoveOutwardVelocity(float2 position,float2 center,float radius,float2 velocity)
        {
            float2 offset=position-center;
            if(math.lengthsq(offset)<math.square(math.max(0,radius-.05f))) return velocity;
            float2 normal=math.normalizesafe(offset);
            return velocity-normal*math.max(0,math.dot(velocity,normal));
        }
    }
}
