using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
namespace CrowdPunch.Systems.Combat
{
    internal static class ChickenProjectilePunch
    {
        internal static float3 Direction(EntityManager em,Entity shot,float3 position,in PunchSpecification punch)
        {
            if(PunchAimAssist.TryGetLockedDirection(em,shot,position,out var assisted)) return assisted;
            float3 forward=math.normalizesafe(new float3(punch.Direction.x,0,punch.Direction.z),new float3(0,0,1));
            float3 radial=math.normalizesafe(new float3(position.x-punch.Origin.x,0,position.z-punch.Origin.z),forward);
            return math.normalizesafe(math.lerp(forward,radial,math.saturate(punch.PositionWeight)),forward);
        }
        internal static void Redirect(EntityManager em,Entity shot,ref ChickenProjectile p,float3 position,in PunchSpecification punch,in ChickenTuning t)
        {
            p.Velocity=Direction(em,shot,position,punch)*t.ReturnSpeed;
            p.HomingTarget=em.GetComponentData<PunchAimAssistTarget>(shot).Target;
            p.Redirected=1; p.Age=0; p.Bounces=0; p.Launch++;
            em.GetBuffer<ChickenProjectileHit>(shot).Clear();
        }
    }
}
