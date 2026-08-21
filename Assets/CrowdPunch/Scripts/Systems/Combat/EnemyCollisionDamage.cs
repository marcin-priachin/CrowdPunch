using CrowdPunch.Components;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Shared impulse-to-damage rule for launched-body impacts.</summary>
    public static class EnemyCollisionDamage
    {
        public static float Calculate(float launchDamage, float estimatedImpulse, EnemyLaunchSettings settings)
        {
            float minimumImpulse = math.max(0f, settings.MinimumDamageImpulse);
            if (estimatedImpulse < minimumImpulse)
            {
                return 0f;
            }

            float multiplier = math.min(
                math.max(0f, settings.MaximumCollisionDamageMultiplier),
                math.max(0f, settings.BaseCollisionDamageMultiplier)
                + (estimatedImpulse - minimumImpulse)
                * math.max(0f, settings.DamageMultiplierPerExcessImpulse));
            return math.max(0f, launchDamage) * multiplier;
        }
    }
}
