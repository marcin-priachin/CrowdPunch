using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    internal static class ShellHitResolution
    {
        public static void Punch(EntityManager em, Entity target, float damage, double now, float3 point)
        {
            var shell = em.GetComponentData<ShellTarget>(target);
            if (shell.CoreHealth <= 0) return;
            bool blocked = shell.ExplosionsRemaining > 0;
            if (!blocked) shell.CoreHealth = math.max(0, shell.CoreHealth - math.max(0, damage));
            Record(em, target, ref shell, now, point, blocked);
        }

        public static void Explosion(EntityManager em, Entity target, float damage, double now, float3 point)
        {
            var shell = em.GetComponentData<ShellTarget>(target);
            if (shell.CoreHealth <= 0) return;
            // SHELL-002: one blast resolves exactly one phase, even when it breaks the shell.
            if (shell.ExplosionsRemaining > 0)
            {
                shell.ExplosionsRemaining--;
                if (shell.ExplosionsRemaining == 0) shell.ShellBrokenAt = now;
            }
            else shell.CoreHealth = math.max(0, shell.CoreHealth - math.max(0, damage));
            Record(em, target, ref shell, now, point, false);
        }

        public static bool Impact(EntityManager em, Entity target, Entity source, uint sequence,
            float damage, double now, float3 point)
        {
            var shell = em.GetComponentData<ShellTarget>(target);
            if (shell.CoreHealth <= 0 || shell.ExplosionsRemaining > 0 || damage <= 0) return false;
            var history = em.GetBuffer<BarricadeHitHistory>(target);
            foreach (var hit in history)
                if (hit.Source == source && hit.LaunchSequence == sequence) return false;
            history.Add(new BarricadeHitHistory { Source = source, LaunchSequence = sequence });
            shell.CoreHealth = math.max(0, shell.CoreHealth - damage);
            Record(em, target, ref shell, now, point, false);
            return true;
        }

        private static void Record(EntityManager em, Entity target, ref ShellTarget shell,
            double now, float3 point, bool blocked)
        {
            shell.LastHitBlocked = blocked ? (byte)1 : (byte)0;
            em.SetComponentData(target, shell);
            var solid = em.GetComponentData<Barricade>(target);
            solid.HitSequence++;
            solid.LastHitTime = now;
            solid.LastHitPosition = point;
            // The shared solid remains alive through shell break. Only core death completes it.
            if (shell.CoreHealth <= 0)
            {
                solid.HitsRemaining = 0;
                em.SetComponentData(target, new PhysicsCollider { Value = solid.BrokenCollider });
            }
            em.SetComponentData(target, solid);
        }
    }
}
