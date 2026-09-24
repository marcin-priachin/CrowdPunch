using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    internal enum ArmorHitOutcome : byte { Ordinary, Blocked, Absorbed, Broken }

    // ENEMY-014: body and explosion share identity; accepted and protected contacts are consumed.
    internal static class ArmorHitResolution
    {
        public static bool IsProtected(EntityManager em, Entity target) =>
            em.HasComponent<EnemyArmor>(target) && em.GetComponentData<EnemyArmor>(target).Stages > 0;

        public static ArmorHitOutcome Resolve(ref EnemyArmor armor, in EnemyArmorSettings settings,
            DynamicBuffer<ArmorHitHistory> history, Entity source, uint launchSequence,
            double now, float damage)
        {
            for (int i = 0; i < history.Length; i++)
                if (history[i].Source == source && history[i].LaunchSequence == launchSequence)
                    return ArmorHitOutcome.Blocked;
            if (armor.Stages == 0 && now >= armor.ProtectedUntil) return ArmorHitOutcome.Ordinary;
            history.Add(new ArmorHitHistory { Source = source, LaunchSequence = launchSequence });
            if (now < armor.ProtectedUntil) return ArmorHitOutcome.Blocked;
            armor.Stages--;
            armor.HitSequence++;
            armor.ProtectedUntil = now + math.max(0f, settings.InvulnerabilityDuration);
            armor.StaggerUntil = armor.Stages > 0 ? now + math.max(0f, settings.StaggerDuration) : 0;
            if (armor.Stages > 0) return ArmorHitOutcome.Absorbed;
            armor.PendingBreakDamage = math.max(0f, damage);
            return ArmorHitOutcome.Broken;
        }

        public static ArmorHitOutcome Resolve(EntityManager em, Entity target, Entity source,
            uint sequence, double now, float damage)
        {
            if (!em.HasComponent<EnemyArmor>(target)) return ArmorHitOutcome.Ordinary;
            var armor = em.GetComponentData<EnemyArmor>(target);
            var outcome = Resolve(ref armor, em.GetComponentData<EnemyArmorSettings>(target),
                em.GetBuffer<ArmorHitHistory>(target), source, sequence, now, damage);
            em.SetComponentData(target, armor);
            return outcome;
        }
    }
}
