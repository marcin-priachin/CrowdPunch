using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    // FINALE-002/004: shared objective entry points enforce the pause atomically, even within one explosion chain.
    internal static class FinaleHitResolution
    {
        public static bool Active(EntityManager em, Entity target)
        {
            if (!em.HasComponent<FinaleAnchor>(target)) return true;
            var a = em.GetComponentData<FinaleAnchor>(target);
            return a.Spent == 0 && em.Exists(a.Boss) && em.GetComponentData<FinaleBoss>(a.Boss).Phase == FinalePhase.Flying;
        }

        public static bool Owned(EntityManager em, Entity source) => em.HasComponent<EnemyLaunchState>(source)
            && em.GetComponentData<EnemyLaunchState>(source).Phase == EnemyLaunchPhase.Launched
            && em.GetComponentData<EnemyLaunchState>(source).Owner == EnemyLaunchOwner.Player;

        public static bool Allows(EntityManager em, Entity target, Entity source) =>
            !em.HasComponent<FinaleAnchor>(target) || Active(em,target) && Owned(em,source);

        public static void CompleteAnchor(EntityManager em, Entity target)
        {
            if (!em.HasComponent<FinaleAnchor>(target) || !Active(em,target)
                || em.GetComponentData<Barricade>(target).HitsRemaining > 0) return;
            var a = em.GetComponentData<FinaleAnchor>(target);
            var boss = em.GetComponentData<FinaleBoss>(a.Boss);
            var t = em.GetComponentData<FinaleTuning>(a.Boss);
            a.Spent = 1;
            boss.SpentCount++;
            boss.Phase = FinalePhase.Falling;
            boss.Remaining = t.FallSeconds;
            boss.Landing = new float3(a.Landing.x,t.GroundHeight,a.Landing.z);
            boss.TetherOrigin = em.GetComponentData<Unity.Transforms.LocalTransform>(target).Position;
            boss.TransitionStart = em.GetComponentData<Unity.Transforms.LocalTransform>(a.Boss).Position;
            boss.Attack = FinaleAttack.Landing;
            boss.AttackSequence++;
            boss.WarningCenter = boss.Landing;
            boss.AttackRemaining = t.FallSeconds;
            em.SetComponentData(target,a);
            em.SetComponentData(a.Boss,boss);
        }

        public static void Damage(EntityManager em, Entity target, Entity source, float damage, bool explosion, double now)
        {
            if (!Owned(em,source) || damage <= 0 || !math.isfinite(damage)) return;
            var boss = em.GetComponentData<FinaleBoss>(target);
            if (boss.Phase != FinalePhase.Downed) return;
            uint sequence = em.GetComponentData<EnemyLaunchState>(source).LaunchSequence;
            var hits = em.GetBuffer<FinaleHit>(target);
            byte kind = explosion ? (byte)1 : (byte)0;
            foreach (var h in hits) if (h.Source == source && h.Sequence == sequence && h.Explosion == kind) return;
            hits.Add(new FinaleHit { Source=source, Sequence=sequence, Explosion=kind });
            boss.Health = math.max(0,boss.Health-damage);
            boss.Flash = .25f;
            if (boss.Health <= 0) {
                boss.Phase = FinalePhase.Defeated;
                boss.Attack = FinaleAttack.None;
                boss.Remaining = em.GetComponentData<FinaleTuning>(target).VictorySeconds;
                if (Mono.Player.PlayerBridgeRegistry.TryGetBridge(out var bridge)) bridge.EncounterVictory = true;
            }
            em.SetComponentData(target,boss);
        }
    }
}
