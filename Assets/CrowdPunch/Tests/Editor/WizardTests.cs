using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Mono.Player;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class WizardTests
    {
        private World world;
        private EntityManager em;
        private PhysicsWorld physics;
        private BlobAssetReference<Unity.Physics.Collider> collider;
        private Entity physicsEntity, playerEntity, source, target, zone;
        private WizardSettings settings;

        [SetUp] public void Setup()
        {
            world = new World("WIZARD requirements"); em = world.EntityManager;
            physics = new PhysicsWorld(0, 0, 0);
            collider = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f }, CollisionFilter.Default);
            physicsEntity = em.CreateEntity(typeof(PhysicsWorldSingleton));
            em.SetComponentData(physicsEntity, new PhysicsWorldSingleton { PhysicsWorld = physics });
            playerEntity = em.CreateEntity(typeof(PlayerSnapshot));
            source = Enemy(float3.zero); target = Enemy(new float3(2, 0, 0));
            settings = WizardSettings.Default;
            em.AddComponentData(source, settings);
            em.AddComponentData(source, new WizardCastState { RandomState = 1, Remaining = 2 });
            em.AddBuffer<WizardIncomingImpactHistory>(source);
            zone = Zone();
        }
        [TearDown] public void Cleanup() { world.Dispose(); physics.Dispose(); collider.Dispose(); }

        private Entity Enemy(float3 position)
        {
            var e = em.CreateEntity(typeof(Enemy), typeof(EnemyTier), typeof(EnemyArchetype), typeof(EnemyLaunchState), typeof(Health),
                typeof(EnemyDamageState), typeof(PhysicsVelocity), typeof(LocalTransform), typeof(DamageRequest),
                typeof(ExternalImpulse), typeof(DeathRequest), typeof(RespawnRequest), typeof(EnemyHealthBarVisibility),
                typeof(NavigationAgent), typeof(EnemyContactDamageSettings));
            em.SetComponentData(e, LocalTransform.FromPosition(position));
            em.SetComponentData(e, new Health { Current = 100, Max = 100 });
            em.SetComponentData(e, new NavigationAgent { Radius = .5f });
            em.SetComponentData(e, new EnemyContactDamageSettings { ContactRadius = .5f });
            em.SetComponentEnabled<DamageRequest>(e, false); em.SetComponentEnabled<ExternalImpulse>(e, false);
            em.SetComponentEnabled<DeathRequest>(e, false); em.SetComponentEnabled<RespawnRequest>(e, false);
            em.SetComponentEnabled<EnemyHealthBarVisibility>(e, false);
            return e;
        }
        private Entity Zone(bool follow = false, bool active = true)
        {
            var e = em.CreateEntity(typeof(WizardZone));
            em.SetComponentData(e, new WizardZone { Source = source, Settings = settings, Active = active ? (byte)1 : (byte)0,
                Kind = follow ? WizardZoneKind.Cast : WizardZoneKind.Impact,
                Follow = follow ? (byte)1 : (byte)0, ExpiresAt = 3 });
            em.AddBuffer<WizardZoneTarget>(e); return e;
        }
        private void Step(double now)
        {
            using var q = em.CreateEntityQuery(typeof(Enemy), typeof(LocalTransform));
            using var entities = q.ToEntityArray(Allocator.Temp);
            physics.Dispose(); physics = new PhysicsWorld(entities.Length, 0, 0);
            var bodies = physics.StaticBodies;
            for (int i = 0; i < entities.Length; i++) bodies[i] = new Unity.Physics.RigidBody
            { Entity = entities[i], Collider = collider, WorldFromBody = new RigidTransform(quaternion.identity,
                em.GetComponentData<LocalTransform>(entities[i]).Position), Scale = 1 };
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero);
            em.SetComponentData(physicsEntity, new PhysicsWorldSingleton { PhysicsWorld = physics });
            world.SetTime(new TimeData(now, .02f));
            world.GetOrCreateSystem<WizardZoneSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<CrowdPunch.Systems.InputBridge.WizardPlayerHitSystem>().Update(world.Unmanaged);
        }
        private float Health(Entity e) => em.GetComponentData<Health>(e).Current;
        private void Mode(WizardForceMode mode) { settings.ForceMode = mode; var z = em.GetComponentData<WizardZone>(zone); z.Settings = settings; em.SetComponentData(zone, z); }

        [Test] public void Wizard003_CastAndImpactUseIndependentRadii()
        {
            em.DestroyEntity(zone);
            settings.CastRadius = 2; settings.ImpactRadius = 5;
            zone = Zone(follow: true); var impact = Zone();
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(4.5f, 0, 0)));
            em.SetComponentData(playerEntity, new PlayerSnapshot { IsAvailable = true, Position = new float3(4.5f, 0, 0), Radius = .5f });
            Step(0);
            Assert.That(Health(target), Is.EqualTo(92));
            Assert.That(em.GetBuffer<WizardZoneTarget>(zone).Length, Is.Zero);
            Assert.That(em.GetBuffer<WizardZoneTarget>(impact).Length, Is.EqualTo(2));

            settings.CastRadius = 6; settings.ImpactRadius = 1;
            var castData = em.GetComponentData<WizardZone>(zone); castData.Settings = settings; em.SetComponentData(zone, castData);
            var impactData = em.GetComponentData<WizardZone>(impact); impactData.Settings = settings; em.SetComponentData(impact, impactData);
            Step(.1);
            Assert.That(Health(target), Is.EqualTo(92));
            Assert.That(em.GetBuffer<WizardZoneTarget>(zone).Length, Is.EqualTo(1));
            Assert.That(em.GetBuffer<WizardZoneTarget>(impact).Length, Is.Zero);
        }

        [Test] public void Wizard003_CastZoneAffectsPlayerButNotEnemies()
        {
            var cast = em.GetComponentData<WizardZone>(zone); cast.Kind = WizardZoneKind.Cast;
            cast.Settings.ForceMode = WizardForceMode.StrongKnockback; em.SetComponentData(zone, cast);
            var go = new GameObject("Wizard cast player test"); var bridge = go.AddComponent<PlayerEcsBridge>();
            var health = go.AddComponent<PlayerHealth>();
            PlayerBridgeRegistry.TryGetBridge(out var previous); PlayerBridgeRegistry.Register(bridge);
            try
            {
                foreach (string method in new[] { "Awake", "OnDisable", "OnEnable" })
                    typeof(PlayerHealth).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(health, null);
                health.ResetHealth();
                em.SetComponentData(playerEntity, new PlayerSnapshot { IsAvailable = true, Position = new float3(2, 0, 0), Radius = .5f });
                Step(0);
                Assert.That(health.CurrentHealth, Is.EqualTo(90));
                Assert.That(Health(target), Is.EqualTo(100));
                Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear, Is.EqualTo(float3.zero));
                Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
                Assert.That(em.GetBuffer<WizardZoneTarget>(zone).Length, Is.EqualTo(1));
            }
            finally { PlayerBridgeRegistry.Unregister(bridge); if (previous != null) PlayerBridgeRegistry.Register(previous); Object.DestroyImmediate(go); }
        }

        [Test] public void Wizard005_IncomingFlightCreatesOnlyOneZonePerWizard()
        {
            var history = em.GetBuffer<WizardIncomingImpactHistory>(source);
            Assert.That(WizardImpactZoneSystem.RegisterIncoming(history, target, 1), Is.True);
            Assert.That(WizardImpactZoneSystem.RegisterIncoming(history, target, 1), Is.False);
            Assert.That(WizardImpactZoneSystem.RegisterIncoming(history, target, 2), Is.True);
            Assert.That(WizardImpactZoneSystem.RegisterIncoming(history, Enemy(new float3(9, 0, 0)), 1), Is.True);
            Assert.That(history.Length, Is.EqualTo(2));
        }

        [Test] public void Wizard005_FastLaunchedDasherSweepsThroughWizard()
        {
            Assert.That(WizardImpactZoneSystem.SweptImpact(new float3(0, 0, 0),
                new float3(-5, 0, 0), new float3(5, 0, 0), 1), Is.True);
            Assert.That(WizardImpactZoneSystem.SweptImpact(new float3(0, 0, 2),
                new float3(-5, 0, 0), new float3(5, 0, 0), 1), Is.False);
        }

        [Test] public void Wizard003_EntryTicksExitAndReentryAreIndependentPerTarget()
        {
            Step(0); Assert.That(Health(target), Is.EqualTo(92)); Assert.That(Health(source), Is.EqualTo(100));
            var late = Enemy(new float3(3, 0, 0)); Step(.2); Assert.That(Health(late), Is.EqualTo(92));
            Step(.5); Assert.That(Health(target), Is.EqualTo(84)); Assert.That(Health(late), Is.EqualTo(92));
            Step(.7); Assert.That(Health(late), Is.EqualTo(84));
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(10, 0, 0))); Step(.71);
            Assert.That(em.GetBuffer<WizardZoneTarget>(zone).Length, Is.EqualTo(1));
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(2, 0, 0))); Step(.72);
            Assert.That(Health(target), Is.EqualTo(76));
        }
        [Test] public void Wizard003_XZMembershipIncludesPhysicalRadiusAndIgnoresHeight()
        {
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(4.49f, 20, 0)));
            Step(0); Assert.That(Health(target), Is.EqualTo(92));
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(4.51f, 20, 0)));
            Step(.5); Assert.That(Health(target), Is.EqualTo(92)); Assert.That(em.GetBuffer<WizardZoneTarget>(zone).Length, Is.Zero);
        }
        [Test] public void Wizard003_OverlappingZonesStackAndTelegraphCannotHit()
        {
            var second = Zone(); var telegraph = Zone(active: false); Step(0);
            Assert.That(Health(target), Is.EqualTo(84)); Assert.That(em.GetBuffer<WizardZoneTarget>(telegraph).Length, Is.Zero);
            Assert.That(em.GetBuffer<WizardZoneTarget>(second).Length, Is.EqualTo(1));
        }
        [Test] public void Wizard003_ArmorImmunityEndsAfterBreakingAndExplodersDoNotDetonate()
        {
            em.AddComponentData(target, EnemyArmor.Fresh); Step(0); Assert.That(Health(target), Is.EqualTo(100));
            Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear, Is.EqualTo(float3.zero));
            em.SetComponentData(target, new EnemyArmor()); em.AddComponent<ExplosiveEnemyState>(target);
            em.AddComponent<ExplosiveDetonationRequest>(target); em.SetComponentEnabled<ExplosiveDetonationRequest>(target, false);
            Step(.1); Assert.That(Health(target), Is.EqualTo(92)); Assert.That(em.IsComponentEnabled<ExplosiveDetonationRequest>(target), Is.False);
        }
        [Test] public void Wizard004_SmallPushDoesNotLaunchAndCenterSkipsForce()
        {
            Step(0); Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear.x, Is.EqualTo(3));
            Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            var center = Enemy(float3.zero); Step(.1); Assert.That(Health(center), Is.EqualTo(92));
            Assert.That(em.GetComponentData<PhysicsVelocity>(center).Linear, Is.EqualTo(float3.zero));
        }
        [Test] public void Wizard004_StrongForceLaunchesNormalsButPreservesExistingFlightAndOwner()
        {
            Mode(WizardForceMode.StrongKnockback); Step(0);
            var launched = em.GetComponentData<EnemyLaunchState>(target);
            Assert.That(launched.Phase, Is.EqualTo(EnemyLaunchPhase.Launched)); Assert.That(launched.Owner, Is.EqualTo(EnemyLaunchOwner.Enemy));
            launched.Owner = EnemyLaunchOwner.Player; launched.HomingTarget = source; em.SetComponentData(target, launched);
            Step(.5); var later = em.GetComponentData<EnemyLaunchState>(target);
            Assert.That(later.LaunchSequence, Is.EqualTo(launched.LaunchSequence)); Assert.That(later.ContinuousFlight, Is.EqualTo(launched.ContinuousFlight));
            Assert.That(later.Owner, Is.EqualTo(EnemyLaunchOwner.Player)); Assert.That(later.HomingTarget, Is.EqualTo(source));
        }
        [Test] public void Wizard004_ElitePushAndCommittedDasherImmunityPreserveTheirStates()
        {
            Mode(WizardForceMode.StrongKnockback); em.SetComponentData(target, new EnemyTier { Value = EnemyCombatTier.Elite });
            var dasher = Enemy(new float3(3, 0, 0)); em.AddComponentData(dasher, new DasherState { Phase = DasherPhase.Dashing });
            em.SetComponentData(dasher, new PhysicsVelocity { Linear = new float3(0, 0, 18) });
            Step(0); Assert.That(Health(dasher), Is.EqualTo(92)); Assert.That(em.GetComponentData<PhysicsVelocity>(dasher).Linear.z, Is.EqualTo(18));
            Assert.That(em.GetComponentData<PhysicsVelocity>(dasher).Linear.x, Is.Zero);
            Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear.x, Is.EqualTo(12));
            Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
        }
        [Test] public void Wizard004_LethalLaunchDefersDeathAndFurtherTicksStillPush()
        {
            Mode(WizardForceMode.StrongKnockback); em.SetComponentData(target, new Health { Current = 5, Max = 5 }); Step(0);
            Assert.That(Health(target), Is.Zero); Assert.That(em.GetComponentData<EnemyDamageState>(target).IsDefeatDeferred, Is.EqualTo(1));
            Assert.That(em.IsComponentEnabled<DeathRequest>(target), Is.False); Step(.5);
            Assert.That(Health(target), Is.Zero); Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear.x, Is.EqualTo(24));
        }
        [Test] public void Wizard003_LethalOrdinaryHitDefeatsAndRemovesMembershipNextUpdate()
        {
            em.SetComponentData(target, new Health { Current = 5, Max = 5 }); Step(0);
            Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Defeated));
            Assert.That(em.IsComponentEnabled<DeathRequest>(target), Is.True); Step(.01);
            Assert.That(em.GetBuffer<WizardZoneTarget>(zone).Length, Is.Zero);
        }
        [Test] public void Wizard005_DetachedZoneSurvivesSourceDeathButNotEncounterUnload()
        {
            var owner = em.CreateEntity(); var z = em.GetComponentData<WizardZone>(zone); z.SceneOwner = owner; em.SetComponentData(zone, z);
            em.DestroyEntity(source); Step(0); Assert.That(Health(target), Is.EqualTo(92));
            em.DestroyEntity(owner); Step(.1); Assert.That(em.Exists(zone), Is.False);
        }
        [Test] public void Wizard005_FollowZoneCancelsOnLaunchAndDetachedZoneExpires()
        {
            var moving = Zone(follow: true); var launch = em.GetComponentData<EnemyLaunchState>(source);
            EnemyLaunchTransition.Begin(ref launch, EnemyLaunchCause.PlayerPunch, 10); em.SetComponentData(source, launch);
            Step(0); Assert.That(em.Exists(moving), Is.False); Assert.That(em.Exists(zone), Is.True);
            Step(3); Assert.That(em.Exists(zone), Is.False);
        }
        [Test] public void Wizard005_RepunchDoesNotRenewContinuousFlightAllowance()
        {
            var launch = new EnemyLaunchState(); EnemyLaunchTransition.Begin(ref launch, EnemyLaunchCause.PlayerPunch, 10);
            uint flight = launch.ContinuousFlight, sequence = launch.LaunchSequence;
            EnemyLaunchTransition.Begin(ref launch, EnemyLaunchCause.PlayerPunch, 10);
            Assert.That(launch.ContinuousFlight, Is.EqualTo(flight)); Assert.That(launch.LaunchSequence, Is.EqualTo(sequence + 1));
            launch.Phase = EnemyLaunchPhase.Recovering; EnemyLaunchTransition.Begin(ref launch, EnemyLaunchCause.WizardZone, 8);
            Assert.That(launch.ContinuousFlight, Is.EqualTo(flight + 1));
        }
        [Test] public void Wizard002_ProbabilityUsesProximityAndActualApproachSpeed()
        {
            settings.ImpactRadius = 100;
            Assert.That(WizardCastSystem.Chance(settings, 12, -4), Is.EqualTo(.15f).Within(.0001));
            Assert.That(WizardCastSystem.Chance(settings, 4, 4), Is.EqualTo(.85f).Within(.0001));
            Assert.That(WizardCastSystem.Chance(settings, 8, 2), Is.EqualTo(.5f).Within(.0001));
            settings.BaseChance = 2; Assert.That(WizardCastSystem.Chance(settings, 4, 4), Is.EqualTo(1));
        }
        [Test] public void Wizard003_PlayerZonesBypassGlobalInvulnerabilityAndStack()
        {
            var go = new GameObject("Wizard bridge test"); var bridge = go.AddComponent<PlayerEcsBridge>(); var health = go.AddComponent<PlayerHealth>();
            PlayerBridgeRegistry.TryGetBridge(out var previous); PlayerBridgeRegistry.Register(bridge);
            try
            {
                foreach (string method in new[] { "Awake", "OnDisable", "OnEnable" })
                    typeof(PlayerHealth).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(health, null);
                health.ResetHealth(); bridge.ReceiveEnemyHit(5, 2, float3.zero); Assert.That(health.IsInvincible, Is.True);
                em.SetComponentData(playerEntity, new PlayerSnapshot { IsAvailable = true, Position = new float3(2, 0, 0), Radius = .5f });
                Zone(); Step(0); Assert.That(health.CurrentHealth, Is.EqualTo(75));
                Step(.49); Assert.That(health.CurrentHealth, Is.EqualTo(75)); Step(.5); Assert.That(health.CurrentHealth, Is.EqualTo(55));
            }
            finally { PlayerBridgeRegistry.Unregister(bridge); if (previous != null) PlayerBridgeRegistry.Register(previous); Object.DestroyImmediate(go); }
        }
        [Test] public void Wizard007_SupplyNeedsWizardAndZeroBaselinesFromSameWave()
        {
            var sequence = em.CreateEntity(); em.AddComponentData(source, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 });
            em.SetComponentData(source, new EnemyArchetype { Value = EnemyArchetypeKind.Wizard });
            em.AddComponentData(target, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 });
            using var q = em.CreateEntityQuery(typeof(EnemyWaveOwnership)); using var enemies = q.ToEntityArray(Allocator.Temp);
            Assert.That(ArmoredAmmunitionSupply.NeedsAmmunition(em, enemies, sequence, 1, true, 0), Is.False);
            em.SetComponentData(target, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Assert.That(ArmoredAmmunitionSupply.NeedsAmmunition(em, enemies, sequence, 1, true, 0), Is.True);
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Assert.That(ArmoredAmmunitionSupply.NeedsAmmunition(em, enemies, sequence, 1, true, 0), Is.False);
        }

        private void Cast(double now, float dt)
        {
            world.SetTime(new TimeData(now, dt));
            world.GetOrCreateSystem<WizardCastSystem>().Update(world.Unmanaged);
        }
        [Test] public void Wizard002_GuaranteedCastingStillRequiresCooldownRangeAndAvailablePlayer()
        {
            em.DestroyEntity(zone);
            settings.CastWheneverInRange = true;
            settings.BaseChance = settings.ProximityBonus = settings.ApproachBonus = 0;
            em.SetComponentData(source, settings);
            em.SetComponentData(playerEntity, new PlayerSnapshot { IsAvailable = true, Position = new float3(7, 0, 0) });
            Cast(1, 1);
            Assert.That(em.GetComponentData<WizardCastState>(source).Phase, Is.EqualTo(WizardCastPhase.Cooldown));
            em.SetComponentData(playerEntity, new PlayerSnapshot { IsAvailable = true, Position = new float3(13, 0, 0) });
            Cast(2, 1);
            Assert.That(em.GetComponentData<WizardCastState>(source).Phase, Is.EqualTo(WizardCastPhase.Checking));
            em.SetComponentData(playerEntity, new PlayerSnapshot { Position = new float3(7, 0, 0) });
            Cast(3, 1);
            Assert.That(em.GetComponentData<WizardCastState>(source).Phase, Is.EqualTo(WizardCastPhase.Checking));
            em.SetComponentData(playerEntity, new PlayerSnapshot { IsAvailable = true, Position = new float3(12, 0, 0) });
            Cast(4, .02f);
            var cast = em.GetComponentData<WizardCastState>(source);
            Assert.That(cast.Phase, Is.EqualTo(WizardCastPhase.Telegraph));
            Assert.That(em.Exists(cast.MovingZone), Is.True);
            Assert.That(cast.RandomState, Is.EqualTo(1u));
        }
        [Test] public void Wizard002_GuaranteedCastingBypassesPendingProbabilityCheck()
        {
            em.DestroyEntity(zone);
            settings.BaseChance = settings.ProximityBonus = settings.ApproachBonus = 0;
            em.SetComponentData(source, settings);
            em.SetComponentData(source, new WizardCastState { RandomState = 1, Phase = WizardCastPhase.Checking });
            em.SetComponentData(playerEntity, new PlayerSnapshot { IsAvailable = true, Position = new float3(7, 0, 0) });
            Cast(0, .02f);
            var cast = em.GetComponentData<WizardCastState>(source);
            Assert.That(cast.Phase, Is.EqualTo(WizardCastPhase.Checking));
            Assert.That(cast.Remaining, Is.EqualTo(settings.CheckInterval));
            settings.CastWheneverInRange = true; em.SetComponentData(source, settings);
            Cast(.02, .02f);
            Assert.That(em.GetComponentData<WizardCastState>(source).Phase, Is.EqualTo(WizardCastPhase.Telegraph));
        }
        [Test] public void Wizard002_CastPersistsOutOfRangeAndDamageAloneDoesNotCancel()
        {
            em.DestroyEntity(zone); settings.BaseChance = 1;
            em.SetComponentData(source, settings); em.SetComponentData(source, new WizardCastState { RandomState = 1 });
            em.SetComponentData(playerEntity, new PlayerSnapshot { IsAvailable = true, Position = new float3(7, 0, 0) });
            Cast(0, .02f); var cast = em.GetComponentData<WizardCastState>(source);
            Assert.That(cast.Phase, Is.EqualTo(WizardCastPhase.Telegraph)); Assert.That(em.Exists(cast.MovingZone), Is.True);
            em.SetComponentData(source, new DamageRequest { Amount = 1 }); em.SetComponentEnabled<DamageRequest>(source, true);
            world.GetOrCreateSystem<DamageApplicationSystem>().Update(world.Unmanaged);
            em.SetComponentData(playerEntity, new PlayerSnapshot { IsAvailable = true, Position = new float3(70, 0, 0) });
            Cast(1, 1); cast = em.GetComponentData<WizardCastState>(source);
            Assert.That(cast.Phase, Is.EqualTo(WizardCastPhase.Active)); Assert.That(em.GetComponentData<WizardZone>(cast.MovingZone).Active, Is.EqualTo(1));
            Cast(4, 3); Assert.That(em.Exists(cast.MovingZone), Is.False);
            Assert.That(em.GetComponentData<WizardCastState>(source).Remaining, Is.EqualTo(2));
        }
        [Test] public void Wizard002_ControlLossCancelsCastAndResetsRecoveryCooldown()
        {
            em.DestroyEntity(zone); settings.BaseChance = 1; em.SetComponentData(source, settings);
            em.SetComponentData(source, new WizardCastState { RandomState = 1 });
            em.SetComponentData(playerEntity, new PlayerSnapshot { IsAvailable = true, Position = new float3(7, 0, 0) });
            Cast(0, .02f); var cast = em.GetComponentData<WizardCastState>(source);
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering }); Cast(1, 1);
            Assert.That(em.Exists(cast.MovingZone), Is.False); Assert.That(em.GetComponentData<WizardCastState>(source).Remaining, Is.EqualTo(2));
            Cast(2, 1); Assert.That(em.GetComponentData<WizardCastState>(source).Remaining, Is.EqualTo(2));
            em.SetComponentData(source, new EnemyLaunchState()); Cast(3, 1);
            Assert.That(em.GetComponentData<WizardCastState>(source).Phase, Is.EqualTo(WizardCastPhase.Cooldown));
        }
        [TestCase(WizardMovementMode.StopThroughout, true, true)]
        [TestCase(WizardMovementMode.StopTelegraph, true, false)]
        [TestCase(WizardMovementMode.KeepMoving, false, false)]
        public void Wizard002_MovementModes(WizardMovementMode mode, bool telegraph, bool active)
        {
            settings.MovementMode = mode;
            Assert.That(new WizardCastState { Phase = WizardCastPhase.Telegraph }.StopsMovement(settings), Is.EqualTo(telegraph));
            Assert.That(new WizardCastState { Phase = WizardCastPhase.Active }.StopsMovement(settings), Is.EqualTo(active));
        }
        [Test] public void Wizard003_PlayerProtectionDoesNotShiftEntryBasedTickCadence()
        {
            var buffer = em.GetBuffer<WizardZoneTarget>(zone);
            Assert.That(WizardZoneSystem.Tick(buffer, Entity.Null, 0, .5f, .75f), Is.True);
            var t = buffer[0]; t.Seen = 0; buffer[0] = t;
            Assert.That(WizardZoneSystem.Tick(buffer, Entity.Null, .5, .5f, .75f), Is.False);
            t = buffer[0]; t.Seen = 0; buffer[0] = t;
            Assert.That(WizardZoneSystem.Tick(buffer, Entity.Null, .76, .5f, .75f), Is.False);
            t = buffer[0]; t.Seen = 0; buffer[0] = t;
            Assert.That(WizardZoneSystem.Tick(buffer, Entity.Null, 1, .5f, .75f), Is.True);
        }
        [Test] public void Wizard007_HazardsGateWaveAdvanceUntilExpiry()
        {
            var seq = em.CreateEntity(typeof(EnemyWaveSequence), typeof(EnemyWaveEncounterComplete));
            em.SetComponentEnabled<EnemyWaveEncounterComplete>(seq, false);
            em.SetComponentData(seq, new EnemyWaveSequence { Initialized = 1, RunGeneration = 1,
                Phase = EnemyWaveRuntimePhase.AwaitingActivation, DefeatedCount = 1 });
            var waves = em.AddBuffer<EnemyWaveDefinition>(seq);
            waves.Add(new EnemyWaveDefinition { TotalEnemyCount = 1, WaitForPersistentHazards = 1 });
            waves.Add(new EnemyWaveDefinition { DelayBeforeWave = 2, IsValid = 1 });
            em.AddBuffer<EnemyWaveProfile>(seq); em.AddBuffer<EnemyWaveEliteProfile>(seq); em.AddBuffer<EnemyWaveSpawnRange>(seq);
            var z = em.GetComponentData<WizardZone>(zone); z.Sequence = seq; z.SceneOwner = seq; z.RunGeneration = 1; em.SetComponentData(zone, z);
            var system = world.GetOrCreateSystem<EnemyWaveSpawnSystem>(); world.SetTime(new TimeData(1, .02f)); system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyWaveSequence>(seq).CurrentWaveIndex, Is.Zero);
            world.SetTime(new TimeData(3, .02f)); system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyWaveSequence>(seq).CurrentWaveIndex, Is.EqualTo(1));
        }
        [Test] public void Wizard007_CappedRerollSelectsOnlyPositiveWeightEligibleArchetypes()
        {
            var e = em.CreateEntity(); var profiles = em.AddBuffer<EnemyWaveProfile>(e);
            profiles.Add(new EnemyWaveProfile { Profile = new EnemySpawnProfile { Archetype = EnemyArchetypeKind.Wizard }, Weight = 100 });
            profiles.Add(new EnemyWaveProfile { Profile = new EnemySpawnProfile { Archetype = EnemyArchetypeKind.Baseline }, Weight = 1 });
            profiles.Add(new EnemyWaveProfile { Profile = new EnemySpawnProfile { Archetype = EnemyArchetypeKind.Ranged }, Weight = 0 });
            var random = new Unity.Mathematics.Random(1);
            for (int i = 0; i < 100; i++) Assert.That(EnemyWaveSpawnSystem.SelectNonWizard(ref random,
                new EnemyWaveDefinition { ProfileCount = 3 }, profiles).Profile.Archetype, Is.EqualTo(EnemyArchetypeKind.Baseline));
        }
    }
}
