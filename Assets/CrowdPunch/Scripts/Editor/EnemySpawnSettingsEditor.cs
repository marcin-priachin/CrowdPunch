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
            "maxHealth", "contactDamagePercent", "contactPushStrength", "contactInvincibilitySeconds", "contactRadius",
            "contactAttemptDistance", "contactAttemptIntervalMin", "contactAttemptIntervalMax",
            "contactAttemptDuration", "contactAttemptSpeedMultiplier", "contactAttemptSeparationWeight"
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

        private static readonly string[] EliteCadence = { "eliteInitialDelay", "eliteCooldown", "eliteCooldownVariation", "eliteMaximumSetupDuration", "eliteRetargetInterval" };
        private static readonly string[] EliteEligibility = { "eliteMaximumSearchRange", "eliteMinimumTargetPlayerDistance", "eliteMaximumTargetPlayerDistance", "eliteAllowActiveTargets", "eliteAllowRecoveringTargets", "eliteAllowLaunchedTargets", "eliteAllowSharedTargets", "eliteMaximumEvaluatedCandidates" };
        private static readonly string[] EliteTactics = { "eliteClearPathTacticProbability", "eliteClearPathAlignmentWeight", "eliteClearPathRepositionWeight", "eliteClearPathDistanceWeight" };
        private static readonly string[] EliteCrowd = { "eliteCrowdCorridorRadius", "eliteCrowdDistanceBeyondPlayer", "eliteCrowdNearPlayerWeight", "eliteMinimumCrowdScore" };
        private static readonly string[] EliteRepositioning = { "eliteDesiredPunchDistance", "elitePositionTolerance", "eliteAimAngleToleranceDegrees", "elitePlayerMovementInvalidationDistance", "eliteTargetMovementInvalidationDistance", "eliteSetupMovementSpeedMultiplier", "eliteApplySeparationDuringSetup" };
        private static readonly string[] EliteEffects = { "elitePunchRange", "elitePunchRadius", "eliteLaunchForce", "elitePunchDamage", "elitePushDirectionPositionWeight", "eliteInteractionMode", "eliteProjectileReceivesDamage", "eliteAffectActive", "eliteAffectRecovering", "eliteAffectLaunched", "eliteCanDirectlyHitPlayer", "eliteDirectPlayerDamage", "elitePlayerPush", "elitePlayerInvincibilityDuration" };
        private static readonly string[] EliteWindUp = { "eliteWindUpDuration", "eliteEnableTelegraph", "eliteTelegraphDuration" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSection("Spawn", SpawnProperties);
            DrawSection("Common Movement (provisional)", MovementProperties);
            DrawArchetypeSeparationOverrides();
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
                DrawEliteSettings();
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
                    case EnemyArchetype.Elite:
                        DrawEliteSettings();
                        break;
                    case EnemyArchetype.Baseline:
                        break;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEliteSettings()
        {
            DrawSection("Elite Punch - Cadence", EliteCadence);
            DrawSection("Elite Punch - Target Eligibility", EliteEligibility);
            DrawSection("Elite Punch - Clear Path", EliteTactics);
            DrawSection("Elite Punch - Crowd Shot", EliteCrowd);
            DrawSection("Elite Punch - Repositioning", EliteRepositioning);
            DrawSection("Elite Punch - Punch Effects", EliteEffects);
            DrawSection("Elite Punch - Optional Wind-up / Telegraph", EliteWindUp);
            SerializedProperty active = serializedObject.FindProperty("eliteAllowActiveTargets");
            SerializedProperty recovering = serializedObject.FindProperty("eliteAllowRecoveringTargets");
            if (!active.boolValue && !recovering.boolValue)
                EditorGUILayout.HelpBox("Active and recovering targets are both disabled; enable at least one or explicitly allow launched targets.", MessageType.Warning);
            float min = serializedObject.FindProperty("eliteMinimumTargetPlayerDistance").floatValue;
            float max = serializedObject.FindProperty("eliteMaximumTargetPlayerDistance").floatValue;
            if (min > max) EditorGUILayout.HelpBox("Minimum target-to-player distance exceeds the maximum.", MessageType.Error);
            float distance = serializedObject.FindProperty("eliteDesiredPunchDistance").floatValue;
            float range = serializedObject.FindProperty("elitePunchRange").floatValue;
            float tolerance = serializedObject.FindProperty("elitePositionTolerance").floatValue;
            if (distance > range + tolerance) EditorGUILayout.HelpBox("Desired punch distance is outside the punch range, making valid setup impossible.", MessageType.Error);
            float timeout = serializedObject.FindProperty("eliteMaximumSetupDuration").floatValue;
            float windup = serializedObject.FindProperty("eliteWindUpDuration").floatValue;
            if (timeout < windup) EditorGUILayout.HelpBox("Setup timeout is shorter than wind-up.", MessageType.Error);
            if (serializedObject.FindProperty("eliteEnableTelegraph").boolValue)
                EditorGUILayout.HelpBox("Telegraph state is baked and inspectable; a dedicated grey-box presentation renderer is not yet assigned.", MessageType.Info);
        }

        private void DrawArchetypeSeparationOverrides()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Target Archetype Separation Overrides (provisional)", EditorStyles.boldLabel);
            SerializedProperty overrides = serializedObject.FindProperty("archetypeSeparationOverrides");
            EditorGUILayout.PropertyField(overrides, true);

            int seenArchetypes = 0;
            bool hasDuplicate = false;
            for (int index = 0; index < overrides.arraySize; index++)
            {
                int archetypeIndex = overrides.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("archetype").enumValueIndex;
                int bit = 1 << archetypeIndex;
                hasDuplicate |= (seenArchetypes & bit) != 0;
                seenArchetypes |= bit;
            }

            if (hasDuplicate)
            {
                EditorGUILayout.HelpBox(
                    "Each target archetype should appear at most once. Later duplicate entries currently replace earlier ones during baking.",
                    MessageType.Warning);
            }
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
