using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Physics;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;

namespace CrowdPunch.Tests
{
    public sealed class LinkedPairTests
    {
        private World world; private EntityManager em; private PhysicsWorld physics;
        private Entity a, b, pair, target, player; private LinkedPairTuning tuning;
        private BlobAssetReference<Unity.Physics.Collider> solid;
        [SetUp] public void Setup() {
            world = new World("LINK-001..007"); em = world.EntityManager; world.SetTime(new TimeData(1, .05f));
            tuning = AssetDatabase.LoadAssetAtPath<LinkedPairSettings>("Assets/CrowdPunch/Data/Settings/LinkedPairSettings.asset").Tuning;
            a = Body(new float3(-2, 0, 0)); b = Body(new float3(2, 0, 0)); target = Body(float3.zero);
            var sequence = em.CreateEntity(typeof(EnemyWaveSequence)); em.SetComponentData(sequence, new EnemyWaveSequence { RunGeneration = 1 });
            pair = em.CreateEntity(typeof(LinkedPair), typeof(LinkedPairTuning)); em.AddBuffer<LinkedPairClock>(pair);
            em.SetComponentData(pair, tuning); em.SetComponentData(pair, new LinkedPair { A = a, B = b, Leader = a, Sequence = sequence,
                RunGeneration = 1, LifetimeA = 1, LifetimeB = 1, LastPunchAt = -1 });
            em.AddComponentData(a, new LinkedPairEndpoint { Pair = pair }); em.AddComponentData(b, new LinkedPairEndpoint { Pair = pair });
            player = em.CreateEntity(typeof(PlayerSnapshot));
            physics = new PhysicsWorld(0, 0, 0); physics.CollisionWorld.BuildBroadphase(ref physics, .05f, float3.zero, true);
            em.SetComponentData(em.CreateEntity(typeof(PhysicsWorldSingleton)), new PhysicsWorldSingleton { PhysicsWorld = physics });
        }
        [TearDown] public void Teardown() { world.Dispose(); physics.Dispose(); if (solid.IsCreated) solid.Dispose(); }
        private Entity Body(float3 position) {
            var e = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(PhysicsVelocity), typeof(Health), typeof(EnemyLaunchState),
                typeof(EnemyTier), typeof(CrowdPunch.Components.EnemyArchetype), typeof(EnemyDamageState), typeof(EnemyLifetime), typeof(NavigationAgent),
                typeof(EnemyGroundConstraint), typeof(RespawnRequest), typeof(ExternalImpulse), typeof(DamageRequest), typeof(DeathRequest), typeof(EnemyHealthBarVisibility));
            em.SetComponentData(e, LocalTransform.FromPosition(position)); em.SetComponentData(e, new Health { Current = 100, Max = 100 });
            em.SetComponentData(e, new EnemyLifetime { Generation = 1 }); em.SetComponentData(e, new NavigationAgent { Radius = .5f });
            em.SetComponentEnabled<RespawnRequest>(e, false); em.SetComponentEnabled<ExternalImpulse>(e, false);
            em.SetComponentEnabled<DamageRequest>(e, false); em.SetComponentEnabled<DeathRequest>(e, false);
            return e;
        }
        private void Damage(double now = 1) { world.SetTime(new TimeData(now, .05f)); world.GetOrCreateSystem<LinkedPairDamageSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
        private void Lifecycle(double now = 1) { world.SetTime(new TimeData(now, .05f)); world.GetOrCreateSystem<LinkedPairLifecycleSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
        private void Launch(Entity endpoint) {
            var l = em.GetComponentData<EnemyLaunchState>(endpoint); EnemyLaunchTransition.Begin(ref l, EnemyLaunchCause.PlayerPunch, 20);
            em.SetComponentData(endpoint, l); LinkedPairLaunchResolution.Begin(em, endpoint, l, new float3(0, 0, 20));
        }
        private float Hp => em.GetComponentData<Health>(target).Current;

        [Test] public void Link001_WalkingAndRecoveryOnlyDamagePlayerWithOneClock() {
            em.SetComponentData(player, new PlayerSnapshot { Position = float3.zero, Radius = 1, IsAvailable = true });
            Damage(); using var q = em.CreateEntityQuery(typeof(LinkedPairPlayerHit));
            Assert.That(q.GetSingletonBuffer<LinkedPairPlayerHit>().Length, Is.EqualTo(1)); Assert.That(Hp, Is.EqualTo(100));
            var p = em.GetComponentData<LinkedPair>(pair); p.Phase = EnemyLaunchPhase.Recovering; em.SetComponentData(pair, p);
            Damage(1.1); Assert.That(q.GetSingletonBuffer<LinkedPairPlayerHit>().Length, Is.Zero);
            Damage(1.51); Assert.That(q.GetSingletonBuffer<LinkedPairPlayerHit>().Length, Is.EqualTo(1)); Assert.That(Hp, Is.EqualTo(100));
        }
        [TestCase(EnemyLaunchOwner.Player)] [TestCase(EnemyLaunchOwner.Boss)] [TestCase(EnemyLaunchOwner.Enemy)]
        public void Link003_FlyingLinkDamagesPlayerUnderEveryOwner(EnemyLaunchOwner owner) {
            Launch(a); var l = em.GetComponentData<EnemyLaunchState>(a); l.Owner = owner; em.SetComponentData(a, l);
            em.SetComponentData(player, new PlayerSnapshot { Radius = 1, IsAvailable = true }); Damage();
            using var q = em.CreateEntityQuery(typeof(LinkedPairPlayerHit)); Assert.That(q.GetSingletonBuffer<LinkedPairPlayerHit>().Length, Is.EqualTo(1));
        }
        [TestCase(LinkedPairResponse.DamageAndLaunch, EnemyLaunchPhase.Launched)]
        [TestCase(LinkedPairResponse.DamageAndPush, EnemyLaunchPhase.Active)]
        public void Link004_ModesAndTickInterval(LinkedPairResponse mode, EnemyLaunchPhase expected) {
            Launch(a); tuning.Response = mode; em.SetComponentData(pair, tuning); Damage();
            Assert.That(Hp, Is.EqualTo(100 - tuning.EnemyDamage)); Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(expected));
            Damage(1.1); Assert.That(Hp, Is.EqualTo(100 - tuning.EnemyDamage));
            Damage(1.51); Assert.That(Hp, Is.EqualTo(100 - tuning.EnemyDamage * 2));
        }
        [Test] public void Link004_AlreadyFlyingVictimRetainsVelocityAndOwnerThroughLethalDamage() {
            Launch(a); var original = new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, Owner = EnemyLaunchOwner.Boss, LaunchSequence = 9, ContinuousFlight = 3 };
            var velocity = new PhysicsVelocity { Linear = new float3(3, 0, -2), Angular = new float3(0, 1, 0) };
            em.SetComponentData(target, original); em.SetComponentData(target, velocity); em.SetComponentData(target, new Health { Current = 1, Max = 100 }); Damage();
            Assert.That(Hp, Is.Zero); Assert.That(em.GetComponentData<EnemyLaunchState>(target), Is.EqualTo(original));
            Assert.That(em.GetComponentData<PhysicsVelocity>(target), Is.EqualTo(velocity)); Assert.That(em.IsComponentEnabled<DeathRequest>(target), Is.False);
        }
        [Test] public void Link004_ArmorProtectionAndElitePush() {
            Launch(a); em.AddComponentData(target, EnemyArmor.Fresh); Damage(); Assert.That(Hp, Is.EqualTo(100));
            em.RemoveComponent<EnemyArmor>(target); em.SetComponentData(target, new EnemyTier { Value = EnemyCombatTier.Elite }); Damage(1.6);
            Assert.That(Hp, Is.LessThan(100)); Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Assert.That(math.length(em.GetComponentData<PhysicsVelocity>(target).Linear), Is.GreaterThan(0));
        }
        [TestCase(LinkedPairResponse.DamageAndLaunch)] [TestCase(LinkedPairResponse.DamageAndPush)]
        public void Link004_ExploderDetonatesInBothModes(LinkedPairResponse mode) {
            Launch(a); tuning.Response = mode; em.SetComponentData(pair, tuning); em.AddComponent<ExplosiveEnemyState>(target);
            em.AddComponent<ExplosiveDetonationRequest>(target); em.SetComponentEnabled<ExplosiveDetonationRequest>(target, false); Damage();
            Assert.That(em.IsComponentEnabled<ExplosiveDetonationRequest>(target), Is.True);
        }
        [Test] public void Link002_PunchEitherEndpointAndRepunchSwitchesLeader() {
            Launch(a); var first = em.GetComponentData<LinkedPair>(pair); Assert.That(first.Leader, Is.EqualTo(a));
            Assert.That(em.GetComponentData<EnemyLaunchState>(b).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
            Assert.That(math.length(em.GetComponentData<PhysicsVelocity>(b).Linear), Is.LessThan(math.length(em.GetComponentData<PhysicsVelocity>(a).Linear)));
            Launch(b); Assert.That(em.GetComponentData<LinkedPair>(pair).Leader, Is.EqualTo(b)); Assert.That(em.GetComponentData<EnemyLaunchState>(a).Owner, Is.EqualTo(EnemyLaunchOwner.Player));
        }
        [Test] public void Link002_SimultaneousPunchHasOneLeaderAndIndependentEndpointDamage() {
            var punch = new PunchSpecification { Origin = new float3(0, 0, -1), Direction = math.forward(), Range = 4, Radius = 3,
                Strength = 20, Damage = 10, Cause = EnemyLaunchCause.PlayerPunch, EventTime = 1, AffectActive = 1, AffectLaunched = 1, ApplyDamage = 1 };
            Assert.That(PunchResolution.TryApply(em, a, punch), Is.True); Assert.That(PunchResolution.TryApply(em, b, punch), Is.True);
            Assert.That(em.GetComponentData<LinkedPair>(pair).Leader, Is.EqualTo(a));
            Assert.That(em.GetComponentData<EnemyLaunchState>(a).LaunchSequence, Is.EqualTo(1)); Assert.That(em.GetComponentData<EnemyLaunchState>(b).LaunchSequence, Is.EqualTo(1));
            EnemyDamageResolution.ApplyPending(em, a, 1); EnemyDamageResolution.ApplyPending(em, b, 1);
            Assert.That(em.GetComponentData<Health>(a).Current, Is.EqualTo(90)); Assert.That(em.GetComponentData<Health>(b).Current, Is.EqualTo(90));
            punch.EventTime = 2; PunchResolution.TryApply(em, b, punch); Assert.That(em.GetComponentData<LinkedPair>(pair).Leader, Is.EqualTo(b));
        }
        [Test] public void Link002_ExplosionChoosesNearestLeaderAndIndependentSplash() {
            var blast = Body(new float3(2, 0, 0));
            em.AddComponentData(blast, new ExplosiveEnemySettings { Radius = 1, Damage = 20, NormalEnemyKnockbackForce = 15 });
            em.AddComponent<EnemyContactDamageSettings>(blast); em.AddComponent<ExplosiveEnemyState>(blast); em.AddComponent<ExplosiveDetonationRequest>(blast);
            world.GetOrCreateSystemManaged<ExplosionResolutionSystem>().Update(); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<LinkedPair>(pair).Leader, Is.EqualTo(b));
            Assert.That(em.GetComponentData<EnemyLaunchState>(a).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
            Assert.That(em.GetComponentData<EnemyLaunchState>(b).LaunchSequence, Is.EqualTo(1)); Assert.That(em.GetComponentData<EnemyLaunchState>(a).LaunchSequence, Is.EqualTo(1));
            EnemyDamageResolution.ApplyPending(em, a, 1); EnemyDamageResolution.ApplyPending(em, b, 1);
            Assert.That(em.GetComponentData<Health>(a).Current, Is.EqualTo(100)); Assert.That(em.GetComponentData<Health>(b).Current, Is.EqualTo(80));
        }
        [Test] public void Link004_ArmorProtectionWindowBlocksPushAndLaunch() {
            Launch(a); em.AddComponentData(target, new EnemyArmor { Stages = 0, ProtectedUntil = 2 }); Damage();
            Assert.That(Hp, Is.EqualTo(100)); Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Damage(2.1); Assert.That(Hp, Is.LessThan(100));
        }
        [Test] public void Link005_EitherMomentumKeepsFlightAliveThenBothRecoverTogether() {
            Launch(a); em.SetComponentData(a, new PhysicsVelocity()); Lifecycle();
            Assert.That(em.GetComponentData<LinkedPair>(pair).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
            em.SetComponentData(b, new PhysicsVelocity());
            for (int i = 0; i < 9; i++) Lifecycle(1.1 + i * .05);
            Assert.That(em.GetComponentData<EnemyLaunchState>(a).Phase, Is.EqualTo(EnemyLaunchPhase.Recovering));
            Assert.That(em.GetComponentData<EnemyLaunchState>(b).Phase, Is.EqualTo(EnemyLaunchPhase.Recovering)); Assert.That(em.Exists(pair), Is.True);
        }
        [Test] public void Link005_ZeroHealthRemainsAttachedUntilFlightEndsThenSurvivorIsIndependent() {
            Launch(a); em.SetComponentData(a, new Health { Max = 100 }); Lifecycle(); Assert.That(em.Exists(pair), Is.True);
            var p = em.GetComponentData<LinkedPair>(pair); p.FlightRemaining = 0; em.SetComponentData(pair, p); Lifecycle(1.1);
            Assert.That(em.Exists(pair), Is.False); Assert.That(em.GetComponentData<EnemyLaunchState>(a).Phase, Is.EqualTo(EnemyLaunchPhase.Defeated));
            Assert.That(em.GetComponentData<LinkedPairEndpoint>(b).Pair, Is.EqualTo(Entity.Null)); Assert.That(em.GetComponentData<Health>(b).Current, Is.EqualTo(100));
        }
        [Test] public void Link007_MissingEndpointClearsRecordAndSurvivor() {
            em.DestroyEntity(a); Lifecycle(); Assert.That(em.Exists(pair), Is.False); Assert.That(em.GetComponentData<LinkedPairEndpoint>(b).Pair, Is.EqualTo(Entity.Null));
        }
        [Test] public void Link007_WarningsActivateBothEndpointsOnlyWhenBothAreClear() {
            em.AddComponentData(a, new EnemySpawnWarning { Position = new float3(-2, 0, 0), Radius = .5f });
            em.AddComponentData(b, new EnemySpawnWarning { Position = new float3(2, 0, 0), Radius = .5f });
            em.SetEnabled(a, false); em.SetEnabled(b, false);
            em.SetComponentData(player, new PlayerSnapshot { Position = new float3(2, 0, 0), Radius = 1, IsAvailable = true });
            var system = world.GetOrCreateSystem<CrowdPunch.Systems.Initialization.EnemySpawnWarningSystem>(); system.Update(world.Unmanaged);
            Assert.That(em.IsEnabled(a), Is.False); Assert.That(em.IsEnabled(b), Is.False);
            em.SetComponentData(player, new PlayerSnapshot { Position = new float3(100, 0, 100), Radius = 1, IsAvailable = true }); system.Update(world.Unmanaged);
            Assert.That(em.IsEnabled(a), Is.True); Assert.That(em.IsEnabled(b), Is.True);
            Assert.That(em.HasComponent<EnemySpawnWarning>(a), Is.False); Assert.That(em.HasComponent<EnemySpawnWarning>(b), Is.False);
        }
        [Test] public void Link003_FastSweepCatchesVictimBetweenFrames() {
            float3 x = new float3(-2, 0, -10), y = new float3(2, 0, -10);
            Assert.That(LinkedPairGeometry.Sweep(x, y, x + new float3(0, 0, 20), y + new float3(0, 0, 20), float3.zero, float3.zero,
                .5f, physics.CollisionWorld, default, out _, out _), Is.True);
        }
        private void Wall(float3 center) {
            physics.Dispose(); physics = new PhysicsWorld(1, 0, 0);
            solid = Unity.Physics.BoxCollider.Create(new BoxGeometry { Size = new float3(1, 2, 4), Orientation = quaternion.identity }, CollisionFilter.Default);
            var bodies = physics.Bodies; bodies[0] = new RigidBody { Collider = solid, Entity = em.CreateEntity(), WorldFromBody = new RigidTransform(quaternion.identity, center), Scale = 1 };
            physics.CollisionWorld.BuildBroadphase(ref physics, .05f, float3.zero, true);
            using var q = em.CreateEntityQuery(typeof(PhysicsWorldSingleton)); em.SetComponentData(q.GetSingletonEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
        }
        [Test] public void Link006_CoverBlocksBothPlayerAndEnemyLinkDamage() {
            Launch(a); Wall(float3.zero); em.SetComponentData(player, new PlayerSnapshot { Radius = 1, IsAvailable = true }); Damage();
            Assert.That(Hp, Is.EqualTo(100)); using var q = em.CreateEntityQuery(typeof(LinkedPairPlayerHit)); Assert.That(q.GetSingletonBuffer<LinkedPairPlayerHit>().Length, Is.Zero);
        }
        [Test] public void Link006_SlackShortensFreelyAndOnlyExcessLengthAddsTension() {
            var system = world.GetOrCreateSystem<LinkedPairConstraintSystem>(); system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<PhysicsVelocity>(a).Linear, Is.EqualTo(float3.zero));
            em.SetComponentData(b, LocalTransform.FromPosition(new float3(12, 0, 0))); system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<PhysicsVelocity>(a).Linear.x, Is.GreaterThan(0)); Assert.That(em.GetComponentData<PhysicsVelocity>(b).Linear.x, Is.LessThan(0));
            Assert.That(em.GetComponentData<LocalTransform>(a).Position.x, Is.EqualTo(-2)); Assert.That(em.GetComponentData<LocalTransform>(b).Position.x, Is.EqualTo(12));
        }
        [Test] public void Link006_FlyingSnagRetainsPairAndDissipatesSpeed() {
            Launch(a); Wall(float3.zero); float before = math.length(em.GetComponentData<PhysicsVelocity>(a).Linear);
            world.GetOrCreateSystem<LinkedPairConstraintSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.Exists(pair), Is.True); Assert.That(math.length(em.GetComponentData<PhysicsVelocity>(a).Linear), Is.LessThan(before));
        }
        [Test] public void Link007_ExactAuthoredWavesAndDefaults() {
            var first = AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>("Assets/CrowdPunch/Data/Settings/Waves/Progression/CP27_01_First_Tether.asset");
            var second = AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>("Assets/CrowdPunch/Data/Settings/Waves/Progression/CP27_02_Crossing_Links.asset");
            Assert.That(first.LinkedPairCount, Is.EqualTo(1)); Assert.That(first.TotalEnemyCount, Is.EqualTo(6));
            Assert.That(second.LinkedPairCount, Is.EqualTo(2)); Assert.That(second.TotalEnemyCount, Is.EqualTo(12));
            Assert.That(first.LinkedPairProfile.Health.Max, Is.EqualTo(AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/EnemySpawnSettings.asset").Health.Max));
            Assert.That(tuning.Response, Is.EqualTo(LinkedPairResponse.DamageAndLaunch));
            foreach (var s in EditorBuildSettings.scenes) Assert.That(s.path.Contains("Gauntlet_27"), Is.False);
        }
    }
}
