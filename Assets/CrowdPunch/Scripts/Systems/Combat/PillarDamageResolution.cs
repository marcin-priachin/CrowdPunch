using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
namespace CrowdPunch.Systems.Combat
{
    internal static class PillarDamageResolution
    {
        internal static bool HitBoss(ref FallingPillar p,ref DinoBoss b,ref Health health,in DinoTuning t)
        {
            if(p.HitBoss!=0 || b.Phase==DinoPhase.Defeated) return false;
            p.HitBoss=1; b.SuccessfulHits++;
            health.Current=math.max(0,t.RequiredHits-b.SuccessfulHits);
            b.Stage=health.Current/t.RequiredHits<=1f/3f?3:health.Current/t.RequiredHits<=2f/3f?2:1;
            b.Phase=health.Current<=0?DinoPhase.Defeated:DinoPhase.Stagger;
            b.Remaining=t.StaggerDuration;
            return true;
        }
        internal static void HitEnemy(EntityManager em,Entity pillar,Entity target,in PillarTuning t,float3 direction,double now)
        {
            var launch=em.GetComponentData<EnemyLaunchState>(target);
            if(launch.Phase==EnemyLaunchPhase.Defeated) return;
            EnemyDamageResolution.ApplyPending(em,target,now);
            launch=em.GetComponentData<EnemyLaunchState>(target);
            if(launch.Phase==EnemyLaunchPhase.Defeated) return;
            bool launchable=EnemyLaunchTransition.IsLaunchable(em.GetComponentData<EnemyTier>(target)) && !ArmorHitResolution.IsProtected(em,target);
            var velocity=em.GetComponentData<PhysicsVelocity>(target);
            if(t.ImpactMode==PillarImpactMode.DamageAndLaunch && launchable)
            {
                EnemyLaunchTransition.Begin(ref launch,EnemyLaunchCause.EnvironmentImpact,t.EnemyDamage,EnemyLaunchOwner.Environment);
                em.SetComponentData(target,launch);
                em.SetComponentEnabled<ExternalImpulse>(target,false); em.SetComponentEnabled<KnockbackRecovery>(target,false);
                velocity.Linear=direction*t.LaunchSpeed; velocity.Angular=float3.zero;
            }
            else velocity.Linear.xz+=direction.xz*t.PushSpeed;
            em.SetComponentData(target,velocity);
            em.SetComponentData(target,new DamageRequest { Amount=t.EnemyDamage }); em.SetComponentEnabled<DamageRequest>(target,true);
            EnemyDamageResolution.ApplyPending(em,target,now,EnemyLaunchOwner.Environment,pillar);
        }
    }
}
