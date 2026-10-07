using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
namespace CrowdPunch.Systems.Combat
{
    internal static class ChickenDamageResolution
    {
        internal static void QueueBody(EntityManager em, Entity boss, Entity source, float impulse, in EnemyLaunchSettings settings)
        {
            if(!em.HasComponent<EnemyLaunchState>(source) || !em.HasComponent<ChickenTuning>(boss)
                || em.HasComponent<RespawnRequest>(source) && em.IsComponentEnabled<RespawnRequest>(source)) return;
            var launch=em.GetComponentData<EnemyLaunchState>(source);
            if(launch.Phase!=EnemyLaunchPhase.Launched || launch.Owner!=EnemyLaunchOwner.Player) return;
            float damage=EnemyCollisionDamage.Calculate(launch.LaunchDamage,impulse,settings);
            if(em.HasComponent<DasherSettings>(source) && impulse>=settings.MinimumDamageImpulse)
                damage=em.GetComponentData<DasherSettings>(source).BossDamage;
            Queue(em,boss,source,launch.LaunchSequence,damage*em.GetComponentData<ChickenTuning>(boss).BodyDamageMultiplier);
        }
        public static void Queue(EntityManager em, Entity boss, Entity source, uint launch, float damage)
        {
            if(!em.HasComponent<ChickenBoss>(boss) || damage<=0 || !math.isfinite(damage)) return;
            uint lifetime=em.HasComponent<EnemyLifetime>(source)?em.GetComponentData<EnemyLifetime>(source).Generation:0;
            var hits=em.GetBuffer<ChickenHit>(boss);
            for(int i=0;i<hits.Length;i++)
                if(hits[i].Source==source && hits[i].Launch==launch && hits[i].Lifetime==lifetime)
                { var h=hits[i]; h.Damage=math.max(h.Damage,damage); hits[i]=h; return; }
            hits.Add(new ChickenHit { Source=source,Launch=launch,Lifetime=lifetime,Damage=damage });
        }

        internal static bool Apply(ref ChickenBoss b,ref Health h,in ChickenTuning t,float damage,double now)
        {
            if(b.Phase==ChickenPhase.Defeated || now<b.InvulnerableUntil || damage<=0) return false;
            h.Current=math.max(0,h.Current-damage);
            b.Stage=h.Current<=h.Max*t.StageThreeThreshold?3:h.Current<=h.Max*t.StageTwoThreshold?2:1;
            b.Phase=h.Current<=0?ChickenPhase.Defeated:ChickenPhase.Stagger;
            b.Remaining=t.Stagger; b.ShotsRemaining=0; b.RushHitPlayer=1;
            b.InvulnerableUntil=now+t.Invulnerability; b.AcceptedHits++;
            return true;
        }
    }
}
