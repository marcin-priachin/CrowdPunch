using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;

namespace CrowdPunch.Tests
{
    public sealed class DeliberateShotTests
    {
        private static readonly Entity Enemy = new Entity { Index = 12, Version = 1 };
        private static readonly EnemyContactDamageSettings Contact = new EnemyContactDamageSettings
        {
            AttemptDistance = 10f, AttemptWindUpDuration = 0.65f, AttemptDuration = 1.1f,
            AttemptIntervalMin = 1f, AttemptIntervalMax = 3f
        };

        [Test]
        public void Vision002_PreparationLeavesTimeToPositionBeforeContactCommitment()
        {
            EnemyContactAttemptState state = default;
            EnemyContactCommitment.Tick(Enemy, 6f, math.forward(), 0.02f, Contact, ref state);
            Assert.That(state.IsWindingUp, Is.EqualTo(1));
            Assert.That(state.IsAttempting, Is.Zero);
            EnemyContactCommitment.Tick(Enemy, 6f, math.forward(), 0.5f, Contact, ref state);
            Assert.That(state.IsAttempting, Is.Zero);
            EnemyContactCommitment.Tick(Enemy, 6f, math.right(), 0.16f, Contact, ref state);
            Assert.That(state.IsWindingUp, Is.Zero);
            Assert.That(state.IsAttempting, Is.EqualTo(1));
            Assert.That(state.CommittedDirection, Is.EqualTo(math.right()));
        }

        [Test]
        public void Vision002_SidestepDoesNotRetargetACommittedLunge()
        {
            var state = new EnemyContactAttemptState { IsWindingUp = 1 };
            EnemyContactCommitment.Tick(Enemy, 6f, math.forward(), 0.02f, Contact, ref state);
            EnemyContactCommitment.Tick(Enemy, 20f, math.right(), 0.5f, Contact, ref state);
            Assert.That(state.IsAttempting, Is.EqualTo(1));
            Assert.That(state.CommittedDirection, Is.EqualTo(math.forward()));
            EnemyContactCommitment.Tick(Enemy, 20f, math.right(), 0.61f, Contact, ref state);
            Assert.That(state.IsAttempting, Is.Zero);
            Assert.That(state.SecondsRemaining, Is.InRange(1f, 3f));
        }

        [Test]
        public void Combat014_LaunchInterruptionClearsBothPreparationAndCommitment()
        {
            var state = new EnemyContactAttemptState { IsWindingUp = 1, CommittedDirection = math.forward() };
            EnemyContactCommitment.Cancel(Enemy, Contact, ref state);
            Assert.That(state.IsWindingUp, Is.Zero);
            Assert.That(state.CommittedDirection, Is.EqualTo(float3.zero));
            Assert.That(state.SecondsRemaining, Is.InRange(1f, 3f));
            uint sequence = state.Sequence;
            EnemyContactCommitment.Cancel(Enemy, Contact, ref state);
            Assert.That(state.Sequence, Is.EqualTo(sequence), "Repeated inactive updates must not reset the cooldown.");
        }

        [Test]
        public void Combat005_LeavingContactRangeCancelsPreparation()
        {
            var state = new EnemyContactAttemptState { IsWindingUp = 1, SecondsRemaining = 0.3f };
            EnemyContactCommitment.Tick(Enemy, 11f, math.forward(), 0.02f, Contact, ref state);
            Assert.That(state.IsWindingUp, Is.Zero);
            Assert.That(state.IsAttempting, Is.Zero);
        }

        [Test]
        public void Combat003_AuthoredForcefulBodyImpactOutperformsDirectDamageButWeakContactDoesNot()
        {
            var runtime = AssetDatabase.LoadAssetAtPath<GameRuntimeSettings>(
                "Assets/CrowdPunch/Data/Settings/GameRuntimeSettings.asset");
            var punch = AssetDatabase.LoadAssetAtPath<PlayerPunchSettings>(
                "Assets/CrowdPunch/Data/Settings/PlayerPunchSettings.asset");
            var curve = new EnemyLaunchSettings
            {
                MinimumDamageImpulse = runtime.MinimumDamageImpulse,
                BaseCollisionDamageMultiplier = runtime.BaseCollisionDamageMultiplier,
                DamageMultiplierPerExcessImpulse = runtime.DamageMultiplierPerExcessImpulse,
                MaximumCollisionDamageMultiplier = runtime.MaximumCollisionDamageMultiplier
            };
            Assert.That(EnemyCollisionDamage.Calculate(punch.Damage, 20f, curve), Is.GreaterThan(punch.Damage));
            Assert.That(EnemyCollisionDamage.Calculate(punch.Damage, curve.MinimumDamageImpulse, curve),
                Is.LessThanOrEqualTo(punch.Damage));
            Assert.That(EnemyCollisionDamage.Calculate(punch.Damage, curve.MinimumDamageImpulse - 0.01f, curve), Is.Zero);
        }
    }
}
