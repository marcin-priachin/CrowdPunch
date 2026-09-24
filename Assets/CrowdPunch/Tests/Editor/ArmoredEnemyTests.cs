using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Systems.Movement;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class ArmoredEnemyTests
    {
        private World world;
        private EntityManager em;
        private Entity armored;
        [SetUp] public void Setup()
        {
            world = new World("ENEMY-014 armor"); em = world.EntityManager;
            armored = Enemy(new float3(0, 0, 1));
            em.AddComponentData(armored, EnemyArmor.Fresh);
            em.AddComponentData(armored, new EnemyArmorSettings { StaggerDuration = .3f, KnockbackSpeed = 3,
                InvulnerabilityDuration = .25f });
            em.AddBuffer<ArmorHitHistory>(armored);
            em.CreateEntity(typeof(PlayerSnapshot));
            var tuning = em.CreateEntity(typeof(EnemyLaunchSettings));
            em.SetComponentData(tuning, new EnemyLaunchSettings { UsefulMomentumSpeed = 1, LowMomentumPeriod = .1f,
                RecoveryDuration = .1f, MinimumDamageImpulse = 1, MinimumPropagationImpulse = 1,
                BaseCollisionDamageMultiplier = 1, MaximumCollisionDamageMultiplier = 1 });
            Time(1);
        }
        [TearDown] public void Teardown() => world.Dispose();
        private void Time(double now) => world.SetTime(new TimeData(now, .2f));
        private Entity Enemy(float3 position)
        {
            var e = em.CreateEntity(typeof(Enemy), typeof(EnemyTier), typeof(EnemyLaunchState), typeof(Health),
                typeof(EnemyDamageState), typeof(PhysicsVelocity), typeof(LocalTransform), typeof(DamageRequest),
                typeof(ExternalImpulse), typeof(DeathRequest), typeof(RespawnRequest), typeof(EnemyHealthBarVisibility),
                typeof(EnemyContactDamageSettings), typeof(KnockbackResponse), typeof(DesiredMovement), typeof(HealthBar),
                typeof(KnockbackRecovery), typeof(EnemyRespawnSettings));
            em.SetComponentData(e, LocalTransform.FromPosition(position));
            em.SetComponentData(e, new Health { Current = 100, Max = 100 });
            em.SetComponentData(e, new EnemyContactDamageSettings { ContactRadius = .5f });
            em.AddBuffer<CollisionDamageHistory>(e);
            em.SetComponentEnabled<DamageRequest>(e, false); em.SetComponentEnabled<ExternalImpulse>(e, false);
            em.SetComponentEnabled<DeathRequest>(e, false); em.SetComponentEnabled<RespawnRequest>(e, false);
            em.SetComponentEnabled<KnockbackRecovery>(e, false); em.SetComponentEnabled<EnemyHealthBarVisibility>(e, false);
            return e;
        }
        private void Damage() => world.GetOrCreateSystem<DamageApplicationSystem>().Update(world.Unmanaged);
        private Entity Explosive()
        {
            var e = Enemy(float3.zero);
            em.AddComponentData(e, new ExplosiveEnemySettings { Radius = 4, Damage = 20, NormalEnemyKnockbackForce = 16 });
            em.AddComponent<ExplosiveEnemyState>(e); em.AddComponent<ExplosiveDetonationRequest>(e);
            em.SetComponentData(e, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            return e;
        }
        private void Explode() => world.GetOrCreateSystemManaged<ExplosionResolutionSystem>().Update();
        private void Stages(byte stages) { var a = em.GetComponentData<EnemyArmor>(armored); a.Stages = stages; em.SetComponentData(armored, a); }

        [Test] public void Enemy014_TwoShieldsAbsorbHits_NextExplosionLaunchesAndDamagesOnce()
        {
            for (int i = 0; i < 3; i++)
            {
                Time(1 + i); var source = Explosive(); Explode(); Damage();
                var armor = em.GetComponentData<EnemyArmor>(armored);
                Assert.That(armor.Stages, Is.EqualTo(math.max(0, 1 - i)));
                Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(i < 2 ? 100 : 80));
                Assert.That(em.GetComponentData<EnemyLaunchState>(armored).Phase,
                    Is.EqualTo(i < 2 ? EnemyLaunchPhase.Active : EnemyLaunchPhase.Launched));
                if (i < 2)
                {
                    Assert.That(armor.StaggerUntil, Is.GreaterThan(1 + i));
                    Assert.That(math.length(em.GetComponentData<PhysicsVelocity>(armored).Linear.xz), Is.EqualTo(3).Within(.001));
                }
                em.DestroyEntity(source);
            }
            Damage(); Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(80));
            em.SetComponentData(armored, new PhysicsVelocity());
            Time(5); world.GetOrCreateSystem<EnemyRecoverySystem>().Update(world.Unmanaged);
            Time(6); world.GetOrCreateSystem<EnemyRecoverySystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyLaunchState>(armored).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Assert.That(em.GetComponentData<EnemyArmor>(armored).Stages, Is.Zero);
        }

        [TestCase(false, 2)] [TestCase(true, 2)] [TestCase(false, 1)] [TestCase(true, 1)]
        public void Enemy014_ExplosiveBodyAndBlastShareIdentityInEitherOrder(bool blastFirst, int stages)
        {
            Stages((byte)stages); var source = Explosive();
            if (blastFirst) Explode();
            Assert.That(ArmorHitResolution.Resolve(em, armored, source, 1, 1, 12),
                Is.EqualTo(blastFirst ? ArmorHitOutcome.Blocked : ArmorHitOutcome.Absorbed));
            if (!blastFirst) Explode();
            Damage();
            Assert.That(em.GetComponentData<EnemyArmor>(armored).Stages, Is.EqualTo(stages - 1));
            Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(100));
            Assert.That(em.GetComponentData<EnemyLaunchState>(armored).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            // Replay after protection has expired, without relying on explosion system ordering.
            Assert.That(ArmorHitResolution.Resolve(em, armored, source, 1, 2, 20), Is.EqualTo(ArmorHitOutcome.Blocked));
        }

        [Test] public void Enemy014_LaunchPairAndInvulnerabilityConsumeBurstContacts()
        {
            var source = Enemy(float3.zero); var second = Enemy(float3.zero);
            Assert.That(ArmorHitResolution.Resolve(em, armored, source, 1, 1, 10), Is.EqualTo(ArmorHitOutcome.Absorbed));
            Assert.That(ArmorHitResolution.Resolve(em, armored, source, 1, 2, 10), Is.EqualTo(ArmorHitOutcome.Blocked));
            Assert.That(ArmorHitResolution.Resolve(em, armored, second, 1, 1.1, 10), Is.EqualTo(ArmorHitOutcome.Blocked));
            Assert.That(ArmorHitResolution.Resolve(em, armored, second, 1, 2, 10), Is.EqualTo(ArmorHitOutcome.Blocked));
            Assert.That(ArmorHitResolution.Resolve(em, armored, source, 2, 2, 10), Is.EqualTo(ArmorHitOutcome.Absorbed));
            Assert.That(em.GetComponentData<EnemyArmor>(armored).Stages, Is.Zero);
        }

        [Test] public void Player009_ProtectedPunchConfirmsConnectionWithoutAnyGameplayEffect()
        {
            using var query = em.CreateEntityQuery(typeof(PlayerSnapshot)); var player = query.GetSingletonEntity();
            em.AddComponentData(player, new PunchRequest { Origin = float3.zero, Direction = math.forward(), Range = 3,
                Radius = 1, Strength = 20, Damage = 20 });
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<PunchRequest>(player).HitEnemy, Is.True);
            Assert.That(em.GetComponentData<PunchRequest>(player).IsResolved, Is.True);
            Assert.That(em.IsComponentEnabled<ExternalImpulse>(armored), Is.False);
            Assert.That(em.IsComponentEnabled<DamageRequest>(armored), Is.False);
            Assert.That(em.GetComponentData<EnemyLaunchState>(armored).LaunchSequence, Is.Zero);
            Assert.That(em.GetComponentData<EnemyArmor>(armored).Stages, Is.EqualTo(2));
        }

        [Test] public void Enemy014_OrdinaryDamageBlockedAndBreakingProtectionSurvivesTransition()
        {
            em.SetComponentData(armored, new DamageRequest { Amount = 30 }); em.SetComponentEnabled<DamageRequest>(armored, true);
            Damage(); Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(100));
            Stages(1); Explosive(); Explode();
            // Neither the last shield hit nor an unrelated burst event may damage health.
            em.SetComponentData(armored, new DamageRequest { Amount = 50 }); Damage();
            Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(100));
            em.SetComponentData(armored, new DamageRequest { Amount = 30 }); em.SetComponentEnabled<DamageRequest>(armored, true);
            Damage(); Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(100));
            Time(2); em.SetComponentData(armored, new DamageRequest { Amount = 30 });
            em.SetComponentEnabled<DamageRequest>(armored, true); Damage();
            Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(70));
        }

        [Test] public void Combat014_UnarmoredPunchAndRepunchReplaceLaunchNormally()
        {
            Stages(0);
            var punch = new PunchSpecification { Origin = float3.zero, Direction = math.forward(), Range = 3, Radius = 1,
                Strength = 20, Damage = 10, ApplyDamage = 1, AffectActive = 1, AffectLaunched = 1, Cause = EnemyLaunchCause.PlayerPunch };
            Assert.That(PunchResolution.TryApply(em, armored, punch), Is.True); Damage();
            Assert.That(PunchResolution.TryApply(em, armored, punch), Is.True); Damage();
            Assert.That(em.GetComponentData<EnemyLaunchState>(armored).LaunchSequence, Is.EqualTo(2));
            Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(80));
        }

        [Test] public void Player004_ArmorRemainsAnAssistTargetForOtherBodies()
        {
            var source = Enemy(float3.zero);
            using var candidates = new NativeArray<Entity>(new[] { armored }, Allocator.Temp);
            Assert.That(PunchAimAssist.TryGetFallbackTarget(em, source, float3.zero, math.forward(), 10, 30,
                candidates, out var target), Is.True);
            Assert.That(target, Is.EqualTo(armored));
        }

        [TestCase(EnemyLaunchOwner.Player)] [TestCase(EnemyLaunchOwner.Enemy)] [TestCase(EnemyLaunchOwner.Boss)]
        public void Enemy014_LaunchedDasherStripsArmorRegardlessOfOwner(EnemyLaunchOwner owner)
        {
            var source = Enemy(float3.zero);
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1, Owner = owner });
            em.AddComponentData(source, new DasherSettings { LaunchedEnemyDamage = 15, LaunchedEnemyKnockback = 18 });
            em.AddComponentData(source, new DasherState { PreviousPosition = new float3(0, 0, 2), PreservedLaunchedVelocity = new float3(0, 0, 18) });
            em.AddBuffer<DasherHitHistory>(source);
            world.GetOrCreateSystem<DasherEnemyImpactSystem>().Update(world.Unmanaged); Damage();
            Assert.That(em.GetComponentData<EnemyArmor>(armored).Stages, Is.EqualTo(1));
            Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(100));
            Time(2); world.GetOrCreateSystem<DasherEnemyImpactSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyArmor>(armored).Stages, Is.EqualTo(1));
        }

        [Test] public void Enemy014_EliteStaleAndAreaPunchesCannotBypassArmor()
        {
            var punch = new PunchSpecification { Origin = float3.zero, Direction = math.forward(), Range = 3, Radius = 1,
                Strength = 20, Damage = 10, ApplyDamage = 1, AffectActive = 1, Cause = EnemyLaunchCause.ElitePunch };
            Assert.That(PunchResolution.TryApply(em, armored, punch), Is.False);
            Assert.That(em.IsComponentEnabled<ExternalImpulse>(armored), Is.False);
            Stages(0); Assert.That(PunchResolution.TryApply(em, armored, punch), Is.True);
        }

        [Test] public void Enemy014_StaggerPreservesRecoilThenMovementResumes()
        {
            em.CreateEntity(typeof(ArenaBounds));
            em.AddComponent<PhysicsMass>(armored);
            em.AddComponentData(armored, new EnemyMovementSettings { Acceleration = 100, BrakingAcceleration = 100 });
            em.SetComponentData(armored, new DesiredMovement { Direction = math.forward(), Speed = 10 });
            em.SetComponentData(armored, new PhysicsVelocity { Linear = new float3(0, 0, -3) });
            var armor = em.GetComponentData<EnemyArmor>(armored); armor.StaggerUntil = 1.3; em.SetComponentData(armored, armor);
            var movement = world.GetOrCreateSystem<EnemyMovementSystem>();
            movement.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<PhysicsVelocity>(armored).Linear.z, Is.EqualTo(-3));
            Time(2); movement.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<PhysicsVelocity>(armored).Linear.z, Is.EqualTo(10));
            em.SetComponentData(armored, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            em.SetComponentData(armored, new PhysicsVelocity { Linear = new float3(0, 0, 25) });
            movement.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<PhysicsVelocity>(armored).Linear.z, Is.EqualTo(25));
        }

        [Test] public void Enemy014_HistoryExpiresWithSourceLaunchAndPooling()
        {
            var source = Explosive();
            ArmorHitResolution.Resolve(em, armored, source, 1, 1, 10);
            var cleanup = world.GetOrCreateSystem<CollisionDamageHistoryCleanupSystem>();
            cleanup.Update(world.Unmanaged);
            Assert.That(em.GetBuffer<ArmorHitHistory>(armored).Length, Is.EqualTo(1));
            em.SetComponentData(source, new ExplosiveEnemyState { HasExploded = 1 });
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated, LaunchSequence = 1 });
            cleanup.Update(world.Unmanaged);
            Assert.That(em.GetBuffer<ArmorHitHistory>(armored).Length, Is.EqualTo(1), "Blast identity survives defeat until pooling");
            em.SetComponentData(source, new RespawnRequest { IsPooled = 1 });
            cleanup.Update(world.Unmanaged);
            Assert.That(em.GetBuffer<ArmorHitHistory>(armored).Length, Is.Zero);
        }

        [TestCase(2, 100)] [TestCase(0, 85)]
        public void Enemy014_GentleDasherCannotStripArmor_BrokenTargetUsesOrdinaryRules(int stages, int health)
        {
            Stages((byte)stages); var source = Enemy(float3.zero);
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            em.AddComponentData(source, new DasherSettings { LaunchedEnemyDamage = 15, LaunchedEnemyKnockback = 18 });
            em.AddComponentData(source, new DasherState { PreviousPosition = new float3(0, 0, 2), PreservedLaunchedVelocity = new float3(0, 0, .1f) });
            em.AddBuffer<DasherHitHistory>(source);
            world.GetOrCreateSystem<DasherEnemyImpactSystem>().Update(world.Unmanaged); Damage();
            Assert.That(em.GetComponentData<EnemyArmor>(armored).Stages, Is.EqualTo(stages));
            Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(health));
        }

        [Test] public void Enemy014_PoolingRestoresArmorAndClearsHistory()
        {
            Stages(0); em.GetBuffer<ArmorHitHistory>(armored).Add(new ArmorHitHistory { Source = armored });
            var arena = em.CreateEntity(typeof(ArenaBounds)); em.SetComponentData(arena, new ArenaBounds { Extents = new float3(10) });
            em.SetComponentEnabled<RespawnRequest>(armored, true);
            world.GetOrCreateSystem<EnemyRespawnSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyArmor>(armored).Stages, Is.EqualTo(2));
            Assert.That(em.GetBuffer<ArmorHitHistory>(armored).Length, Is.Zero);
            Assert.That(em.GetComponentData<EnemyArmor>(armored).ProtectedUntil, Is.Zero);
        }

        [Test] public void Enemy015_OnlyProtectedLivingOwnedEnemiesNeedAmmunition()
        {
            var sequence = em.CreateEntity();
            em.AddComponentData(armored, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 });
            using var enemies = new NativeArray<Entity>(new[] { armored }, Allocator.Temp);
            Assert.That(ArmoredAmmunitionSupply.NeedsAmmunition(em, enemies, sequence, 1), Is.True);
            Assert.That(ArmoredAmmunitionSupply.NeedsAmmunition(em, enemies, sequence, 2), Is.False);
            Stages(0); Assert.That(ArmoredAmmunitionSupply.NeedsAmmunition(em, enemies, sequence, 1), Is.False);
            Stages(2); em.SetComponentEnabled<RespawnRequest>(armored, true);
            Assert.That(ArmoredAmmunitionSupply.NeedsAmmunition(em, enemies, sequence, 1), Is.False);
        }
    }
}
