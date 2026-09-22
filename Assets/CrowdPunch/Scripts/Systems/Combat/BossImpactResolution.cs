using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    // BOSS-001/002: the only health/stagger entry point. Generic damage requests never apply to parts.
    internal static class BossImpactResolution
    {
        public static bool Eligible(in EnemyLaunchState launch) =>
            launch.Phase==EnemyLaunchPhase.Launched && launch.Owner==EnemyLaunchOwner.Player;

        public static bool TryHit(EntityManager em, Entity source, Entity part, float impulse,
            float damage, double now, float3 point, float3 direction)
        {
            if(!em.HasComponent<BossPart>(part) || !em.HasComponent<EnemyLaunchState>(source)) return false;
            var launch=em.GetComponentData<EnemyLaunchState>(source);
            if(!Eligible(launch) || damage<=0 || !math.isfinite(damage)
                || em.HasComponent<RespawnRequest>(source) && em.IsComponentEnabled<RespawnRequest>(source)) return false;
            var p=em.GetComponentData<BossPart>(part);
            if(!em.HasComponent<BossEncounter>(p.Encounter)) return false;
            var boss=em.GetComponentData<BossEncounter>(p.Encounter);
            var tuning=em.GetComponentData<BossTuning>(p.Encounter);
            if(boss.Cycle==BossCycle.Defeated) return false;
            var history=em.GetBuffer<CollisionDamageHistory>(part);
            for(int i=0;i<history.Length;i++)
                if(history[i].Source==source && history[i].SourceLaunchSequence==launch.LaunchSequence) return false;
            // Consume even protected contacts: resting against a collider cannot become a delayed hit.
            history.Add(new CollisionDamageHistory { Source=source, SourceLaunchSequence=launch.LaunchSequence });
            em.SetComponentData(part,new BossImpactFeedback { FlashRemaining=.22f, Pending=1, Position=point, Direction=direction });
            if(p.Kind!=BossPartKind.Head)
            {
                var hand=em.GetComponentData<BossHand>(part);
                if(boss.Cycle==BossCycle.Transition || now<hand.StaggerProtectedUntil || impulse<tuning.StaggerMinimumImpulse) return false;
                hand.Phase=BossHandPhase.Staggered; hand.Remaining=tuning.StaggerDuration;
                hand.StaggerProtectedUntil=now+tuning.StaggerDuration+tuning.StaggerProtection;
                hand.PlayerHit=1;
                em.SetComponentData(part,hand);
                return true;
            }
            if(boss.Cycle==BossCycle.Transition || now<boss.InvulnerableUntil) return false;
            var health=em.GetComponentData<Health>(part);
            ApplyHeadDamage(ref boss,ref health,tuning,damage,now);
            em.SetComponentData(part,health); em.SetComponentData(part,boss);
            // Cancel both hitboxes in this same physics step, before player-hit interpretation.
            if(boss.Cycle==BossCycle.Transition || boss.Cycle==BossCycle.Defeated)
            {
                CancelHand(em,boss.LeftHand); CancelHand(em,boss.RightHand);
            }
            return true;
        }

        internal static void ApplyHeadDamage(ref BossEncounter boss, ref Health health, in BossTuning t, float damage, double now)
        {
            float floor=boss.Stage==1 ? health.Max*t.StageTwoThreshold : boss.Stage==2 ? health.Max*t.StageThreeThreshold : 0;
            health.Current=math.max(floor,health.Current-math.max(0,damage));
            boss.InvulnerableUntil=now+t.Invulnerability; boss.AcceptedHits++;
            if(health.Current<=0) { boss.Cycle=BossCycle.Defeated; boss.SecondAttackPending=0; return; }
            if(health.Current<=floor && boss.Stage<3)
            {
                boss.Stage++; boss.TransitionCount++; boss.Cycle=BossCycle.Transition;
                boss.Remaining=t.TransitionDuration; boss.SecondAttackPending=0;
            }
        }

        private static void CancelHand(EntityManager em, Entity e)
        {
            if(!em.HasComponent<BossHand>(e)) return;
            var hand=em.GetComponentData<BossHand>(e);
            hand.Phase=BossHandPhase.Returning; hand.Remaining=0; hand.PlayerHit=1;
            em.SetComponentData(e,hand);
        }
    }
}
