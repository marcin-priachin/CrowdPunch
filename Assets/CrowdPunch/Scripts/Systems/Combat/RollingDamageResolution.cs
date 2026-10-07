using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    internal static class RollingDamageResolution
    {
        internal static bool CanTarget(in RollingBoss b,in RollingTuning t) => b.Phase!=RollingPhase.Defeated
            && (t.Targeting==RollingTargeting.EveryLivingState || b.Phase!=RollingPhase.Roll);

        internal static void QueueBody(EntityManager em,Entity boss,Entity source,float impulse,in EnemyLaunchSettings settings)
        {
            var launch=em.GetComponentData<EnemyLaunchState>(source);
            var t=em.GetComponentData<RollingTuning>(boss);
            if(launch.Phase!=EnemyLaunchPhase.Launched || t.Ownership==RollingBodyOwnership.PlayerOnly && launch.Owner!=EnemyLaunchOwner.Player) return;
            float damage=EnemyCollisionDamage.Calculate(launch.LaunchDamage,impulse,settings);
            if(em.HasComponent<DasherSettings>(source) && impulse>=settings.MinimumDamageImpulse)
                damage=em.GetComponentData<DasherSettings>(source).BossDamage;
            Queue(em,boss,source,launch.LaunchSequence,damage*t.BodyDamageMultiplier);
        }

        internal static void Queue(EntityManager em,Entity boss,Entity source,uint launch,float damage)
        {
            if(damage<=0 || !math.isfinite(damage)) return;
            uint lifetime=em.HasComponent<EnemyLifetime>(source)?em.GetComponentData<EnemyLifetime>(source).Generation:0;
            var hits=em.GetBuffer<RollingHit>(boss);
            for(int i=0;i<hits.Length;i++)
                if(hits[i].Source==source && hits[i].Launch==launch && hits[i].Lifetime==lifetime)
                { var h=hits[i]; h.Damage=math.max(h.Damage,damage); hits[i]=h; return; }
            hits.Add(new RollingHit { Source=source,Launch=launch,Lifetime=lifetime,Damage=damage });
        }

        internal static bool Apply(ref RollingBoss b,ref Health h,in RollingTuning t,float damage,double now)
        {
            if(b.Phase==RollingPhase.Roll || b.Phase==RollingPhase.Defeated || now<b.InvulnerableUntil || damage<=0) return false;
            float floor=b.Stage==1?h.Max*t.StageTwoThreshold:b.Stage==2?h.Max*t.StageThreeThreshold:0;
            h.Current=math.max(floor,h.Current-damage);
            if(b.Stage<3 && h.Current<=floor) b.Stage++;
            if(h.Current<=0) b.Phase=RollingPhase.Defeated;
            // No action, remaining timer, cycle stage or speed writes on a surviving hit.
            b.InvulnerableUntil=now+t.Invulnerability; b.AcceptedHits++;
            return true;
        }
    }
}
