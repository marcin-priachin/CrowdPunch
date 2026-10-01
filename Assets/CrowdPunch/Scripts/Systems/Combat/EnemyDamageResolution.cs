using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    internal static class EnemyDamageResolution
    {
        // Shared by pre-physics requests and same-step Wizard ticks before recovery.
        public static void ApplyPending(EntityManager em, Entity target, double now)
        {
            if (!em.IsComponentEnabled<DamageRequest>(target)) return;
            var health = em.GetComponentData<Health>(target);
            var launch = em.GetComponentData<EnemyLaunchState>(target);
            var damage = em.GetComponentData<EnemyDamageState>(target);
            if (launch.Phase == EnemyLaunchPhase.Defeated)
            {
                damage.LastDamageReceived = 0; damage.IsDefeatDeferred = 0;
                em.SetComponentData(target, damage); em.SetComponentEnabled<DamageRequest>(target, false); return;
            }
            float amount = health.Current <= 0 ? 0 : math.max(0, em.GetComponentData<DamageRequest>(target).Amount);
            if (em.HasComponent<EnemyArmor>(target))
            {
                var armor = em.GetComponentData<EnemyArmor>(target);
                if (armor.Stages > 0 || now < armor.ProtectedUntil) amount = 0;
            }
            health.Current = math.clamp(health.Current - amount, 0, math.max(0, health.Max));
            damage.LastDamageReceived = amount;
            if (amount > 0)
            {
                em.SetComponentData(target, new EnemyHealthBarVisibility { SecondsRemaining = 1 });
                em.SetComponentEnabled<EnemyHealthBarVisibility>(target, true);
            }
            em.SetComponentEnabled<DamageRequest>(target, false);
            if (health.Current <= 0)
            {
                damage.IsDefeatDeferred = launch.Phase == EnemyLaunchPhase.Launched ? (byte)1 : (byte)0;
                if (launch.Phase != EnemyLaunchPhase.Launched)
                { launch.Phase = EnemyLaunchPhase.Defeated; em.SetComponentEnabled<DeathRequest>(target, true); }
            }
            em.SetComponentData(target, health); em.SetComponentData(target, damage); em.SetComponentData(target, launch);
        }
    }
}
