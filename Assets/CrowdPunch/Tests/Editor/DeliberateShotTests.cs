using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Physics;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
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

        [TestCase(EnemyLaunchPhase.Active)]
        [TestCase(EnemyLaunchPhase.Launched)]
        [TestCase(EnemyLaunchPhase.Recovering)]
        public void Player004_MovingBodyUsesLockedDirectionWhileEliteKeepsAdditiveKnockback(EnemyLaunchPhase phase)
        {
            using var world = new World("Deliberate shot resolution test");
            var manager = world.EntityManager;
            var detection = world.GetOrCreateSystem<PunchDetectionSystem>();
            var impulses = world.GetOrCreateSystem<ApplyImpulseSystem>();

            Entity player = manager.CreateEntity(typeof(PlayerSnapshot), typeof(PunchRequest));
            manager.SetComponentData(player, new PunchRequest
            {
                Direction = math.forward(), Range = 8f, Radius = 1.15f, Strength = 90f,
                Damage = 10f, EliteKnockbackMultiplier = 0.1f
            });
            Entity body = CreateMovingSubject(EnemyCombatTier.Normal, phase, 4f);
            Entity elite = CreateMovingSubject(EnemyCombatTier.Elite, EnemyLaunchPhase.Active, 6f);
            Entity destination = manager.CreateEntity(typeof(LocalTransform), typeof(Health), typeof(EnemyLaunchState));
            manager.SetComponentData(destination, LocalTransform.FromPosition(new float3(2f, 0f, 20f)));
            manager.SetComponentData(destination, new Health { Current = 5f, Max = 5f });
            manager.AddComponentData(body, new PunchAimAssistTarget { Target = destination });

            // Isolate detection -> impulse; real solver integration is checked separately in Play mode.
            detection.Update(world.Unmanaged);
            impulses.Update(world.Unmanaged);
            manager.CompleteAllTrackedJobs();

            PhysicsVelocity launched = manager.GetComponentData<PhysicsVelocity>(body);
            float3 expected = math.normalizesafe(new float3(2f, 0f, 16f)) * 90f;
            Assert.That(math.distance(launched.Linear, expected), Is.LessThan(0.001f));
            Assert.That(launched.Angular, Is.EqualTo(float3.zero));
            Assert.That(manager.GetComponentData<EnemyLaunchState>(body).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
            Assert.That(manager.GetComponentData<PhysicsVelocity>(elite).Linear, Is.EqualTo(new float3(12f, 3f, 5f)));
            Assert.That(manager.GetComponentData<PhysicsVelocity>(elite).Angular, Is.EqualTo(new float3(0f, 2f, 0f)));
            Assert.That(manager.GetComponentData<EnemyLaunchState>(elite).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Assert.That(manager.GetComponentData<PunchRequest>(player).HitEnemy, Is.True);
            Assert.That(manager.IsComponentEnabled<PunchRequest>(player), Is.False);

            Entity CreateMovingSubject(EnemyCombatTier tier, EnemyLaunchPhase initialPhase, float z)
            {
                Entity entity = manager.CreateEntity(typeof(Enemy), typeof(EnemyTier), typeof(LocalTransform),
                    typeof(Health), typeof(EnemyLaunchState), typeof(PhysicsVelocity), typeof(ExternalImpulse), typeof(DamageRequest));
                manager.SetComponentData(entity, new EnemyTier { Value = tier });
                manager.SetComponentData(entity, LocalTransform.FromPosition(new float3(0f, 0f, z)));
                manager.SetComponentData(entity, new Health { Current = 5f, Max = 5f });
                manager.SetComponentData(entity, new EnemyLaunchState { Phase = initialPhase });
                manager.SetComponentData(entity, new PhysicsVelocity
                {
                    Linear = new float3(12f, 3f, -4f), Angular = new float3(0f, 2f, 0f)
                });
                manager.SetComponentEnabled<ExternalImpulse>(entity, false);
                manager.SetComponentEnabled<DamageRequest>(entity, false);
                return entity;
            }
        }

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
