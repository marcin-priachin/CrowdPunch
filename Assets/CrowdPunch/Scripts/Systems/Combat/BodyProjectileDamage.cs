using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    internal static class BodyProjectileDamage
    {
        internal static bool TryDamage(EntityManager em, Entity target, Entity shotEntity,
            DynamicBuffer<BodyProjectileHit> history, float enemyDamage, uint punchSequence, double now, int chainDepth)
        {
            if (!em.HasComponent<Enemy>(target) || !em.HasComponent<EnemyTier>(target)
                || em.GetComponentData<EnemyTier>(target).Value == EnemyCombatTier.Boss
                || em.HasComponent<BossPart>(target) || em.HasComponent<Barricade>(target)
                || em.HasComponent<RespawnRequest>(target) && em.IsComponentEnabled<RespawnRequest>(target)
                || !em.HasComponent<DamageRequest>(target) || !em.HasComponent<Health>(target)
                || em.GetComponentData<Health>(target).Current <= 0f
                || em.GetComponentData<EnemyLaunchState>(target).Phase == EnemyLaunchPhase.Defeated
                || SnakeHitRules.Protected(em, target) || SnakeHitRules.Attached(em, target)) return false;
            if (em.HasComponent<EnemyArmor>(target))
            {
                var armor = em.GetComponentData<EnemyArmor>(target);
                if (armor.Stages > 0 || now < armor.ProtectedUntil) return false;
            }
            uint generation = em.HasComponent<EnemyLifetime>(target) ? em.GetComponentData<EnemyLifetime>(target).Generation : 0;
            foreach (var hit in history)
                if (hit.Enemy == target && hit.Lifetime == generation) return false;
            history.Add(new BodyProjectileHit { Enemy = target, Lifetime = generation });
            EnemyDamageResolution.ApplyPending(em, target, now);
            var health = em.GetComponentData<Health>(target);
            if (health.Current <= 0f) return false;
            var damage = new DamageRequest();
            damage.Add(enemyDamage, health.Current, punchSequence);
            em.SetComponentData(target, damage);
            em.SetComponentEnabled<DamageRequest>(target, true);
            EnemyDamageResolution.ApplyPending(em, target, now, EnemyLaunchOwner.Player, shotEntity, chainDepth);
            return true;
        }
    }
}
