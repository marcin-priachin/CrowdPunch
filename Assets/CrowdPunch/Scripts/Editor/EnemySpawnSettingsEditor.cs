using CrowdPunch.Configuration;
using UnityEditor;

namespace CrowdPunch.Editor
{
    /// <summary>Shows only tuning that is consumed by the selected enemy archetype.</summary>
    [CustomEditor(typeof(EnemySpawnSettings))]
    [CanEditMultipleObjects]
    public sealed class EnemySpawnSettingsEditor : UnityEditor.Editor
    {
        private static readonly string[] SpawnProperties =
        {
            "enemyPrefab", "archetype", "initialCount", "radius", "respawnEnabled"
        };

        private static readonly string[] MovementProperties =
        {
            "moveSpeed", "wanderSpeed", "chargeDistance", "chargeSpeedMultiplier", "acceleration",
            "brakingAcceleration", "turnSpeed", "stoppingDistance", "surroundDistance",
            "surroundRingSpacing", "separationDistanceMin", "separationDistanceMax", "separationWeight"
        };

        private static readonly string[] HealthAndContactProperties =
        {
            "maxHealth", "contactDamagePercent", "contactPushStrength", "contactInvincibilitySeconds", "contactRadius"
        };

        private static readonly string[] RangedProperties =
        {
            "rangedProjectilePrefab", "preferredMinimumDistance", "preferredMaximumDistance", "engagementRange",
            "retreatSpeed", "approachSpeed", "initialAttackDelay", "initialDelayVariation", "windUpDuration",
            "cooldown", "cooldownVariation", "projectileDamage", "playerInvincibilitySeconds", "projectileSpeed",
            "projectileAimSpreadRadius", "projectileAimTargetYOffset", "projectileArcHeight",
            "projectileMinimumAltitude", "projectileLifetime", "projectileRadius", "projectilePlayerLayers"
        };

        private static readonly string[] ExplosiveProperties =
        {
            "explosionRadius", "explosionDamage", "normalEnemyKnockbackForce", "playerEliteKnockbackForce",
            "bossKnockbackForce", "explosionPlayerInvincibilitySeconds", "explosionVisualDuration",
            "explosionVisualSizeMultiplier"
        };

        private static readonly string[] DasherProperties =
        {
            "dasherPreferredMinimumDistance", "dasherPreferredMaximumDistance", "dasherPreparationMinimumDistance",
            "dasherPreparationMaximumDistance", "approachSpeed", "retreatSpeed", "dasherPreparationMovement",
            "dasherTelegraphDuration", "dasherDashSpeed", "dasherMaximumDistance", "dasherRecoveryDuration",
            "dasherAvoidancePolicy", "dasherCorridorWidth", "dasherBehindPlayerDistance", "dasherPlayerDamage",
            "dasherPlayerKnockback", "dasherPlayerInvincibilitySeconds", "dasherLaunchedEnemyDamage",
            "dasherLaunchedEnemyKnockback", "dasherLaunchedImpactPositionWeight", "dasherEliteDamage",
            "dasherEliteKnockback", "dasherBossDamage", "dasherBossKnockback",
            "dasherPreserveMomentumAgainstElites", "dasherPreserveMomentumAgainstBosses"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSection("Spawn", SpawnProperties);
            DrawSection("Common Movement (provisional)", MovementProperties);
            DrawSection("Common Health and Contact (provisional)", HealthAndContactProperties);

            SerializedProperty archetype = serializedObject.FindProperty("archetype");
            if (archetype.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox(
                    "The selected assets use different archetypes. All archetype-specific settings are shown.",
                    MessageType.Info);
                DrawSection("Ranged (provisional)", RangedProperties);
                DrawSection("Explosion (provisional)", ExplosiveProperties);
                DrawSection("Dasher (provisional)", DasherProperties);
            }
            else
            {
                switch ((EnemyArchetype)archetype.enumValueIndex)
                {
                    case EnemyArchetype.Ranged:
                        DrawSection("Ranged (provisional)", RangedProperties);
                        break;
                    case EnemyArchetype.Explosive:
                        DrawSection("Explosion (provisional)", ExplosiveProperties);
                        break;
                    case EnemyArchetype.Dasher:
                        DrawSection("Dasher (provisional)", DasherProperties);
                        break;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSection(string title, string[] propertyNames)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                if (property != null)
                    EditorGUILayout.PropertyField(property, true);
            }
        }
    }
}
