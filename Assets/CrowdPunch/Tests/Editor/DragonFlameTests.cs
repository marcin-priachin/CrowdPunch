using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
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
    public sealed class DragonFlameTests
    {
        private World world;
        private EntityManager em;
        private Entity source, target, player;
        private DragonTuning tuning;
        private PhysicsWorld physics;
        private BlobAssetReference<Unity.Physics.Collider> solid;
        [SetUp] public void Setup()
        {
            world = new World("DRAGON-001..007"); em = world.EntityManager;
            world.SetTime(new TimeData(0, .05f));
            tuning = AssetDatabase.LoadAssetAtPath<DragonSettings>("Assets/CrowdPunch/Data/Settings/DragonSettings.asset").Tuning;
            tuning.MouthOffset = float3.zero;
            source = Enemy(float3.zero); target = Enemy(new float3(0, 90, 4));
            em.AddComponentData(source, tuning);
            em.AddComponentData(source, new DragonFlameState { Phase = DragonFlamePhase.Active });
            em.AddBuffer<DragonFlameClock>(source); em.AddBuffer<DragonMouthFrame>(source);
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, Owner = EnemyLaunchOwner.Player,
                LaunchSequence = 5, ContinuousFlight = 3, FeedbackChainDepth = 4 });
            player = em.CreateEntity(typeof(PlayerSnapshot));
            physics = new PhysicsWorld(0, 0, 0); physics.CollisionWorld.BuildBroadphase(ref physics, .05f, float3.zero, true);
            em.SetComponentData(em.CreateEntity(typeof(PhysicsWorldSingleton)), new PhysicsWorldSingleton { PhysicsWorld = physics });
        }
        [TearDown] public void Teardown() { world.Dispose(); physics.Dispose(); if (solid.IsCreated) solid.Dispose(); }
        private Entity Enemy(float3 p)
        {
            var e = em.CreateEntity(typeof(Enemy), typeof(Health), typeof(EnemyLaunchState), typeof(EnemyDamageState),
                typeof(LocalTransform), typeof(NavigationAgent), typeof(EnemyLifetime), typeof(RespawnRequest), typeof(DamageRequest),
                typeof(DeathRequest), typeof(EnemyHealthBarVisibility), typeof(PhysicsVelocity), typeof(DesiredMovement), typeof(NavigationIntent));
            em.SetComponentData(e, LocalTransform.FromPosition(p)); em.SetComponentData(e, new Health { Current = 100, Max = 100 });
            em.SetComponentData(e, new NavigationAgent { Radius = .5f }); em.SetComponentData(e, new EnemyLifetime { Generation = 1 });
            em.SetComponentEnabled<RespawnRequest>(e, false); em.SetComponentEnabled<DamageRequest>(e, false); em.SetComponentEnabled<DeathRequest>(e, false);
            return e;
        }
        private void Damage(double time = 0)
        { world.SetTime(new TimeData(time, .05f)); world.GetOrCreateSystem<DragonFlameDamageSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
        private float Hp => em.GetComponentData<Health>(target).Current;
        private void SourceLaunch(EnemyLaunchPhase phase, EnemyLaunchOwner owner)
        { var l = em.GetComponentData<EnemyLaunchState>(source); l.Phase = phase; l.Owner = owner; em.SetComponentData(source, l); }
        private void Move(Entity e, float3 p) { var pose = em.GetComponentData<LocalTransform>(e); pose.Position = p; em.SetComponentData(e, pose); }

        [Test]
        public void Dragon006CastAndJawAdvanceTogetherThroughWarningActiveAndLaunch()
        {
            tuning.WarningDuration = 1; tuning.ActiveDuration = 1; tuning.CastDuration = 1;
            em.SetComponentData(source, tuning);
            SourceLaunch(EnemyLaunchPhase.Active, EnemyLaunchOwner.None);
            em.SetComponentData(source, new DragonFlameState { Initialized = 1, Generation = 1,
                ObservedLaunchSequence = 5, Phase = DragonFlamePhase.Warning, Remaining = .5f });
            var mouth = em.GetBuffer<DragonMouthFrame>(source);
            for (int i = 0; i < 96; i++) mouth.Add(new DragonMouthFrame { Position = new float3(i % 32, 0, 0) });
            void Cycle()
            {
                world.GetOrCreateSystem<DragonFlameCycleSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
            }
            Cycle();
            var warning = em.GetComponentData<DragonFlameState>(source);
            Assert.That(warning.Motion, Is.EqualTo(2));
            Assert.That(warning.AnimationPhase, Is.EqualTo(.2475f).Within(.0001f));
            Assert.That(warning.MouthOffset.x, Is.EqualTo(warning.AnimationPhase * 31).Within(.0001f));
            em.SetComponentData(source, new DragonFlameState { Initialized = 1, Generation = 1,
                ObservedLaunchSequence = 5, Phase = DragonFlamePhase.Active, Remaining = .5f });
            Cycle();
            var active = em.GetComponentData<DragonFlameState>(source);
            Assert.That(active.AnimationPhase, Is.EqualTo(.7525f).Within(.0001f), "Active flame continues the cast instead of freezing it.");
            Assert.That(active.MouthOffset.x, Is.EqualTo(active.AnimationPhase * 31).Within(.0001f));
            SourceLaunch(EnemyLaunchPhase.Launched, EnemyLaunchOwner.Player);
            Cycle();
            var launched = em.GetComponentData<DragonFlameState>(source);
            Assert.That(launched.Motion, Is.EqualTo(2));
            Assert.That(launched.AnimationPhase, Is.EqualTo(.8025f).Within(.0001f));
            Assert.That(launched.MouthOffset.x, Is.EqualTo(launched.AnimationPhase * 31).Within(.0001f));
        }

        [TestCase(DragonEnemyFlameMode.PlayerLaunches, EnemyLaunchPhase.Active, EnemyLaunchOwner.None, false)]
        [TestCase(DragonEnemyFlameMode.PlayerLaunches, EnemyLaunchPhase.Launched, EnemyLaunchOwner.Player, true)]
        [TestCase(DragonEnemyFlameMode.PlayerLaunches, EnemyLaunchPhase.Launched, EnemyLaunchOwner.Boss, false)]
        [TestCase(DragonEnemyFlameMode.AnyLaunch, EnemyLaunchPhase.Launched, EnemyLaunchOwner.Boss, true)]
        [TestCase(DragonEnemyFlameMode.AnyLaunch, EnemyLaunchPhase.Active, EnemyLaunchOwner.None, false)]
        [TestCase(DragonEnemyFlameMode.BurstsAndLaunches, EnemyLaunchPhase.Active, EnemyLaunchOwner.None, true)]
        public void Dragon003_EnemyEligibilityModes(DragonEnemyFlameMode mode, EnemyLaunchPhase phase, EnemyLaunchOwner owner, bool eligible)
        {
            tuning.EnemyMode = mode; em.SetComponentData(source, tuning); SourceLaunch(phase, owner); Damage();
            Assert.That(Hp < 100, Is.EqualTo(eligible)); Assert.That(em.GetComponentData<Health>(source).Current, Is.EqualTo(100));
        }
        [Test] public void Dragon003_FirstContactReentryStateTransitionAndIndependentSources()
        {
            Damage(); float first = Hp; Assert.That(first, Is.EqualTo(100 - tuning.Launched.EnemyDamage));
            Move(target, new float3(50, 0, 50)); Damage(.1); Move(target, new float3(0, 0, 4)); Damage(.2); Assert.That(Hp, Is.EqualTo(first));
            tuning.EnemyMode = DragonEnemyFlameMode.BurstsAndLaunches; em.SetComponentData(source, tuning);
            SourceLaunch(EnemyLaunchPhase.Active, EnemyLaunchOwner.None); Damage(.25); Assert.That(Hp, Is.EqualTo(first));
            SourceLaunch(EnemyLaunchPhase.Launched, EnemyLaunchOwner.Player); Damage(.41); Assert.That(Hp, Is.EqualTo(first - tuning.Launched.EnemyDamage));
            var second = em.Instantiate(source); em.GetBuffer<DragonFlameClock>(second).Clear(); Damage(.42);
            Assert.That(Hp, Is.EqualTo(first - tuning.Launched.EnemyDamage * 2));
        }
        [Test] public void Dragon003_ReusedVictimHasIndependentLifetimeClock()
        { Damage(); float first = Hp; em.SetComponentData(target, new EnemyLifetime { Generation = 2 }); Damage(.01); Assert.That(Hp, Is.EqualTo(first - tuning.Launched.EnemyDamage)); }
        [TestCase(EnemyLaunchOwner.None)] [TestCase(EnemyLaunchOwner.Player)] [TestCase(EnemyLaunchOwner.Boss)]
        public void Dragon003_PlayerIsAlwaysVulnerableAndHasOwnClock(EnemyLaunchOwner owner)
        {
            SourceLaunch(EnemyLaunchPhase.Launched, owner);
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Radius = 1, Position = new float3(0, -40, 3) });
            Damage(); using var q = em.CreateEntityQuery(typeof(DragonPlayerHit)); var hits = q.GetSingletonBuffer<DragonPlayerHit>();
            Assert.That(hits.Length, Is.EqualTo(1)); Assert.That(hits[0].Damage, Is.EqualTo(tuning.Launched.PlayerDamage));
            Assert.That(hits[0].Push.z, Is.GreaterThan(0)); Damage(.1); Assert.That(q.GetSingletonBuffer<DragonPlayerHit>().Length, Is.Zero);
        }
        [Test] public void Dragon004_LethalLaunchedVictimPreservesTrajectoryAndOwnership()
        {
            var original = new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, Owner = EnemyLaunchOwner.Boss, LaunchSequence = 11,
                ContinuousFlight = 7, FeedbackChainDepth = 2, LastCause = EnemyLaunchCause.BossAttack };
            var velocity = new PhysicsVelocity { Linear = new float3(4, 8, -3) };
            em.SetComponentData(target, original); em.SetComponentData(target, velocity); em.SetComponentData(target, new Health { Current = 1, Max = 100 });
            Damage(); Assert.That(Hp, Is.Zero); Assert.That(em.GetComponentData<EnemyLaunchState>(target), Is.EqualTo(original));
            Assert.That(em.GetComponentData<PhysicsVelocity>(target), Is.EqualTo(velocity)); Assert.That(em.IsComponentEnabled<DeathRequest>(target), Is.False);
            var credit = em.GetComponentData<EnemyDamageState>(target); Assert.That(credit.IsDefeatDeferred, Is.EqualTo(1));
            Assert.That(credit.DefeatOwner, Is.EqualTo(EnemyLaunchOwner.Player)); Assert.That(credit.DefeatChainDepth, Is.EqualTo(4));
            Assert.That(credit.DefeatSource, Is.EqualTo(source)); Assert.That(credit.DefeatSourceLifetime, Is.EqualTo(1));
        }
        [TestCase(true)] [TestCase(false)] public void Dragon004_ArmorAndProtectionBlockDamagePushAndClocks(bool intact)
        {
            em.AddComponentData(target, intact ? EnemyArmor.Fresh : new EnemyArmor { ProtectedUntil = 1 }); Damage();
            Assert.That(Hp, Is.EqualTo(100)); Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear, Is.EqualTo(float3.zero));
            Assert.That(em.GetBuffer<DragonFlameClock>(source).Length, Is.Zero);
            if (intact) Assert.That(em.GetComponentData<EnemyArmor>(target).Stages, Is.EqualTo(EnemyArmor.Fresh.Stages));
        }
        [Test] public void Dragon004_ExploderDeathDoesNotDetonate()
        {
            em.AddComponent<ExplosiveEnemyState>(target); em.AddComponent<ExplosiveDetonationRequest>(target);
            em.SetComponentEnabled<ExplosiveDetonationRequest>(target, false); em.SetComponentData(target, new Health { Current = 1, Max = 100 }); Damage();
            Assert.That(Hp, Is.Zero); Assert.That(em.IsComponentEnabled<ExplosiveDetonationRequest>(target), Is.False);
        }
        [Test] public void Dragon004_NormalBurstDoesNotInheritHistoricalPlayerCredit()
        {
            tuning.EnemyMode = DragonEnemyFlameMode.BurstsAndLaunches; em.SetComponentData(source, tuning);
            SourceLaunch(EnemyLaunchPhase.Active, EnemyLaunchOwner.Player);
            em.SetComponentData(target, new Health { Current = 1, Max = 100 }); Damage();
            Assert.That(em.GetComponentData<EnemyDamageState>(target).DefeatOwner, Is.EqualTo(EnemyLaunchOwner.None));
        }
        [Test] public void Dragon004_ElitePushDoesNotLaunchAndBossPuzzleAreExcluded()
        {
            em.AddComponentData(target, new EnemyTier { Value = EnemyCombatTier.Elite }); Damage();
            Assert.That(Hp, Is.LessThan(100)); Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear.z, Is.GreaterThan(0));
            em.SetComponentData(target, new Health { Current = 100, Max = 100 }); em.AddComponent<BossPart>(target); Damage(1); Assert.That(Hp, Is.EqualTo(100));
            em.RemoveComponent<BossPart>(target); em.RemoveComponent<Enemy>(target); Damage(2); Assert.That(Hp, Is.EqualTo(100));
        }
        [TestCase(DragonImmunityMode.OwnFlame, true)] [TestCase(DragonImmunityMode.AllDragonFlame, false)]
        public void Dragon005_DragonImmunityModes(DragonImmunityMode mode, bool hit)
        { tuning.Immunity = mode; em.SetComponentData(source, tuning); em.AddComponentData(target, tuning); Damage(); Assert.That(Hp < 100, Is.EqualTo(hit)); }
        [Test] public void Dragon002_RadiusOverlapAndHeightIndependentMembership()
        {
            DragonFlameGeometry.TryCone(source, LocalTransform.Identity, tuning, default, em.GetComponentData<EnemyLaunchState>(source), 1, out var cone);
            Assert.That(DragonFlameGeometry.Overlaps(cone, new float3(0, 900, tuning.Launched.Length + .4f), .5f), Is.True);
            Assert.That(DragonFlameGeometry.Overlaps(cone, new float3(0, 900, tuning.Launched.Length + .6f), .5f), Is.False);
            Assert.That(DragonFlameGeometry.Overlaps(cone, new float3(0, 0, -1), .5f), Is.False);
        }
        private void Wall(bool enemyBody)
        {
            physics.Dispose(); physics = new PhysicsWorld(1, 0, 0);
            solid = Unity.Physics.BoxCollider.Create(new BoxGeometry { Size = new float3(3, 2, .5f), Orientation = quaternion.identity }, CollisionFilter.Default);
            var wall = enemyBody ? Enemy(new float3(0, 0, 2)) : em.CreateEntity();
            var bodies = physics.Bodies;
            bodies[0] = new RigidBody { Collider = solid, Entity = wall, WorldFromBody = new RigidTransform(quaternion.identity, new float3(0, tuning.GroundY + .6f, 2)), Scale = 1 };
            physics.CollisionWorld.BuildBroadphase(ref physics, .05f, float3.zero, true);
            using var q = em.CreateEntityQuery(typeof(PhysicsWorldSingleton)); em.SetComponentData(q.GetSingletonEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
        }
        [TestCase(false, false)] [TestCase(true, true)] public void Dragon002_SolidCoverBlocksAndBodiesDoNot(bool enemyBody, bool damage)
        { Wall(enemyBody); Damage(); Assert.That(Hp < 100, Is.EqualTo(damage)); }
        [Test] public void Dragon002_WarningCannotDamageOrPush()
        {
            tuning.EnemyMode = DragonEnemyFlameMode.BurstsAndLaunches; em.SetComponentData(source, tuning); SourceLaunch(EnemyLaunchPhase.Active, EnemyLaunchOwner.None);
            em.SetComponentData(source, new DragonFlameState { Phase = DragonFlamePhase.Warning }); Damage(); Assert.That(Hp, Is.EqualTo(100));
            Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear, Is.EqualTo(float3.zero));
        }
        [Test] public void Dragon001_CommittedWarningSurvivesRangeExitAndRecoveryStartsFullCooldown()
        {
            var f = new DragonFlameState(); var l = new EnemyLaunchState();
            DragonFlameCycleSystem.Advance(ref f, tuning, l, false, .1f); Assert.That(f.Phase, Is.EqualTo(DragonFlamePhase.Cooldown));
            DragonFlameCycleSystem.Advance(ref f, tuning, l, true, .1f); Assert.That(f.Phase, Is.EqualTo(DragonFlamePhase.Warning));
            DragonFlameCycleSystem.Advance(ref f, tuning, l, false, tuning.WarningDuration); Assert.That(f.Phase, Is.EqualTo(DragonFlamePhase.Active));
            l.Phase = EnemyLaunchPhase.Launched; l.LaunchSequence = 3; DragonFlameCycleSystem.Advance(ref f, tuning, l, true, .1f);
            l.Phase = EnemyLaunchPhase.Recovering; DragonFlameCycleSystem.Advance(ref f, tuning, l, true, .1f);
            l.Phase = EnemyLaunchPhase.Active; DragonFlameCycleSystem.Advance(ref f, tuning, l, true, .1f);
            Assert.That(f.Remaining, Is.EqualTo(tuning.CooldownDuration)); Assert.That(f.Phase, Is.EqualTo(DragonFlamePhase.Cooldown));
        }
        [TestCase(EnemyLaunchPhase.Launched, true)] [TestCase(EnemyLaunchPhase.Recovering, false)] [TestCase(EnemyLaunchPhase.Defeated, false)]
        public void Dragon001_ZeroHealthFlightRetainsFlameUntilRecovery(EnemyLaunchPhase phase, bool emits)
        { SourceLaunch(phase, EnemyLaunchOwner.Boss); em.SetComponentData(source, new Health()); Damage(); Assert.That(Hp < 100, Is.False); tuning.EnemyMode = DragonEnemyFlameMode.AnyLaunch; em.SetComponentData(source, tuning); Damage(1); Assert.That(Hp < 100, Is.EqualTo(emits)); }
        [TestCase(DragonFlamePhase.Cooldown)] [TestCase(DragonFlamePhase.Warning)] [TestCase(DragonFlamePhase.Active)]
        public void Dragon001_LimitedTurnInAllNormalPhases(DragonFlamePhase phase)
        {
            SourceLaunch(EnemyLaunchPhase.Active, EnemyLaunchOwner.None); em.SetComponentData(source, new DragonFlameState { Phase = phase });
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(10, 0, 0) });
            world.GetOrCreateSystem<EnemyFacingSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            float3 forward = math.forward(em.GetComponentData<LocalTransform>(source).Rotation);
            Assert.That(math.degrees(math.atan2(forward.x, forward.z)), Is.EqualTo(tuning.TurnDegreesPerSecond * .05f).Within(.001));
        }
        [TestCase(3f, -1)] [TestCase(12f, 1)] [TestCase(7f, 0)]
        public void Dragon001_DistanceBandApproachRetreatAndHold(float distance, int direction)
        {
            SourceLaunch(EnemyLaunchPhase.Active, EnemyLaunchOwner.None);
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(0, 0, distance) });
            world.GetOrCreateSystem<DragonPositioningSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            var move = em.GetComponentData<DesiredMovement>(source);
            if (direction == 0) Assert.That(move.Speed, Is.Zero);
            else
            {
                Assert.That(move.Speed, Is.EqualTo(direction < 0 ? tuning.RetreatSpeed : tuning.ApproachSpeed));
                Assert.That(math.sign(move.Direction.z), Is.EqualTo(direction));
            }
            Assert.That(em.GetComponentData<LocalTransform>(source).Position, Is.EqualTo(float3.zero));
            Assert.That(em.GetComponentData<PhysicsVelocity>(source).Linear, Is.EqualTo(float3.zero));
        }
        [TestCase(EnemyLaunchPhase.Active, false, true)] [TestCase(EnemyLaunchPhase.Launched, false, false)]
        [TestCase(EnemyLaunchPhase.Active, true, false)]
        public void Dragon005_AvoidanceHonorsLaunchAndCommittedDash(EnemyLaunchPhase phase, bool committed, bool avoids)
        {
            em.SetComponentData(target, new EnemyLaunchState { Phase = phase });
            em.AddComponentData(target, new DasherState { Phase = committed ? DasherPhase.Dashing : DasherPhase.Positioning });
            world.GetOrCreateSystem<DragonFlameAvoidanceSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(math.lengthsq(em.GetComponentData<NavigationIntent>(target).Separation) > 0, Is.EqualTo(avoids));
            tuning.Avoidance = DragonAvoidanceMode.None; em.SetComponentData(source, tuning); em.SetComponentData(target, new NavigationIntent());
            world.GetOrCreateSystem<DragonFlameAvoidanceSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(math.lengthsq(em.GetComponentData<NavigationIntent>(target).Separation), Is.Zero);
        }
        [Test] public void Dragon006_SourceGenerationClearsOldVictimRecords()
        {
            Damage(); Assert.That(em.GetBuffer<DragonFlameClock>(source).Length, Is.EqualTo(1));
            em.SetComponentData(source, new EnemyLifetime { Generation = 2 });
            world.GetOrCreateSystem<DragonFlameCycleSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetBuffer<DragonFlameClock>(source).Length, Is.Zero);
        }
        [Test] public void Dragon007_SavedDefaultsAndExactFiniteWaves()
        {
            var asset = AssetDatabase.LoadAssetAtPath<DragonSettings>("Assets/CrowdPunch/Data/Settings/DragonSettings.asset");
            Assert.That(asset.enemyMode, Is.EqualTo(DragonEnemyFlameMode.PlayerLaunches)); Assert.That(asset.immunity, Is.EqualTo(DragonImmunityMode.OwnFlame));
            Assert.That(asset.avoidance, Is.EqualTo(DragonAvoidanceMode.DamagingFlame));
            CheckWave("CP26_01_First_Flame", 7, 1); CheckWave("CP26_02_Crossfire", 14, 2);
            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/DragonEnemySpawnSettings.asset");
            Assert.That(profile.Health.Max, Is.EqualTo(AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/EnemySpawnSettings.asset").Health.Max));
            Assert.That(profile.ContactDamageSettings.DamagePercent, Is.Zero);
            Assert.That(profile.EnemyPrefab.GetComponent<CrowdPunch.Authoring.DragonAuthoring>().mouthFrames.Length, Is.EqualTo(96));
        }
        private static void CheckWave(string name, int count, int dragons)
        {
            using var s = new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>("Assets/CrowdPunch/Data/Settings/Waves/Progression/" + name + ".asset"));
            Assert.That(s.FindProperty("totalEnemyCount").intValue, Is.EqualTo(count));
            Assert.That(s.FindProperty("enemies").arraySize, Is.EqualTo(2));
            Assert.That(s.FindProperty("enemies").GetArrayElementAtIndex(1).FindPropertyRelative("MinimumCount").intValue, Is.EqualTo(dragons));
            Assert.That(s.FindProperty("replenishWhileBossLives").boolValue, Is.False);
        }
    }
}
