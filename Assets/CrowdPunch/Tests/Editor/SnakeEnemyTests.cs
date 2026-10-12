using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Systems.Physics;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class SnakeEnemyTests
    {
        private World world;
        private EntityManager em;
        private Entity head;
        private Entity[] orbs;
        [SetUp] public void Setup()
        {
            world = new World("SNAKE-001..007"); em = world.EntityManager;
            world.SetTime(new TimeData(1, .2f));
            head = Body(float3.zero);
            em.AddComponentData(head, new SnakeTuning { SegmentCount = 6, OrbRadius = .6f,
                Spacing = 1.5f, HeadVulnerability = SnakeHeadVulnerability.ProtectedWhileTailRemains });
            em.AddComponentData(head, new SnakeHead { AttachedCount = 6, Generation = 1, Initialized = 1,
                DynamicMass = new PhysicsMass { InverseMass = 1 } });
            em.AddComponentData(head, new PhysicsMass { InverseMass = 1 });
            em.AddBuffer<SnakeTailLink>(head); em.AddBuffer<SnakePathPoint>(head);
            orbs = new Entity[6];
            for (int i = 0; i < orbs.Length; i++)
            {
                orbs[i] = Body(new float3(0, 0, (i + 1) * 1.5f));
                em.AddComponentData(orbs[i], new SnakeSegment { Head = head, Generation = 1, Index = i, Radius = .6f });
                em.GetBuffer<SnakeTailLink>(head).Add(new SnakeTailLink { Orb = orbs[i] });
            }
            var settings = em.CreateEntity(typeof(EnemyLaunchSettings));
            em.SetComponentData(settings, new EnemyLaunchSettings { UsefulMomentumSpeed = 1, LowMomentumPeriod = .1f, RecoveryDuration = .5f });
            em.CreateEntity(typeof(PlayerSnapshot));
        }
        [TearDown] public void TearDown() => world.Dispose();
        private Entity Body(float3 position)
        {
            var e = em.CreateEntity(typeof(Enemy), typeof(EnemyLaunchState), typeof(EnemyTier), typeof(Health),
                typeof(EnemyDamageState), typeof(DamageRequest), typeof(DeathRequest), typeof(ExternalImpulse),
                typeof(PhysicsVelocity), typeof(LocalTransform), typeof(RespawnRequest), typeof(EnemyHealthBarVisibility),
                typeof(EnemyContactDamageSettings));
            em.SetComponentData(e, LocalTransform.FromPosition(position));
            em.SetComponentData(e, new Health { Current = 100, Max = 100 });
            em.SetComponentData(e, new EnemyContactDamageSettings { ContactRadius = .75f });
            em.SetComponentEnabled<DamageRequest>(e, false); em.SetComponentEnabled<DeathRequest>(e, false);
            em.SetComponentEnabled<ExternalImpulse>(e, false); em.SetComponentEnabled<RespawnRequest>(e, false);
            return e;
        }
        private void Launch(Entity e, EnemyLaunchOwner owner = EnemyLaunchOwner.Player)
        {
            var l = em.GetComponentData<EnemyLaunchState>(e); EnemyLaunchTransition.Begin(ref l, EnemyLaunchCause.EnemyCollision, 20, owner);
            em.SetComponentData(e, l);
        }
        private void Sever()
        {
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            var h = em.GetComponentData<SnakeHead>(head);
            SnakeSevering.Resolve(em, head, ref h, em.GetBuffer<SnakeTailLink>(head), commands);
            em.SetComponentData(head, h); commands.Playback(em);
        }
        private PunchSpecification Punch => new PunchSpecification { Origin = new float3(0,0,-1), Direction = new float3(0,0,1),
            Range = 12, Radius = 2, Strength = 20, Damage = 20, AffectActive = 1, AffectLaunched = 1,
            AffectRecovering = 1, ApplyDamage = 1, Cause = EnemyLaunchCause.PlayerPunch };

        [TestCase(EnemyLaunchOwner.Player), TestCase(EnemyLaunchOwner.Enemy), TestCase(EnemyLaunchOwner.Boss)]
        public void Snake002_MiddleCutPreservesPrefixAndLaunchOwner(EnemyLaunchOwner owner)
        {
            Launch(orbs[2], owner); Sever();
            Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(2));
            Assert.That(em.Exists(orbs[0]) && em.Exists(orbs[1]) && em.Exists(orbs[2]), Is.True);
            Assert.That(em.GetComponentData<SnakeSegment>(orbs[2]).Detached, Is.EqualTo(1));
            Assert.That(em.GetComponentData<EnemyLaunchState>(orbs[2]).Owner, Is.EqualTo(owner));
            Assert.That(em.Exists(orbs[3]) || em.Exists(orbs[4]) || em.Exists(orbs[5]), Is.False);
            Assert.That(em.GetComponentData<EnemyLaunchState>(head).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
        }
        [TestCase(false), TestCase(true)]
        public void Snake002_SimultaneousCutsKeepEveryHitRegardlessOfOrder(bool reverse)
        {
            Launch(orbs[reverse ? 4 : 1]); Launch(orbs[reverse ? 1 : 4]); Sever();
            Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(1));
            Assert.That(em.Exists(orbs[1]) && em.Exists(orbs[4]), Is.True);
            Assert.That(em.GetComponentData<SnakeSegment>(orbs[1]).Detached, Is.EqualTo(1));
            Assert.That(em.GetComponentData<SnakeSegment>(orbs[4]).Detached, Is.EqualTo(1));
            Assert.That(em.Exists(orbs[2]) || em.Exists(orbs[3]) || em.Exists(orbs[5]), Is.False);
        }
        [Test] public void Snake002_OrdinaryDamageCannotDestroyAttachedOrb()
        {
            em.SetComponentData(orbs[3], new DamageRequest { Amount = 1000 }); em.SetComponentEnabled<DamageRequest>(orbs[3], true);
            EnemyDamageResolution.ApplyPending(em, orbs[3], 1); Sever();
            Assert.That(em.GetComponentData<Health>(orbs[3]).Current, Is.EqualTo(100));
            Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(6));
        }
        [Test] public void Snake004_ProtectedPunchConsumesCooldownWithoutDamageOrLaunch()
        {
            using (var players = em.CreateEntityQuery(typeof(PlayerSnapshot)).ToEntityArray(Allocator.Temp)) em.DestroyEntity(players[0]);
            var player = em.CreateEntity(typeof(PlayerSnapshot), typeof(PunchRequest));
            em.SetComponentData(player, new PunchRequest { Origin = new float3(0,0,-1), Direction = new float3(0,0,1),
                Range = 1, Radius = 1, Strength = 20, Damage = 20 });
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<PunchRequest>(player).HitEnemy, Is.True);
            Assert.That(em.GetComponentData<EnemyLaunchState>(head).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Assert.That(em.IsComponentEnabled<DamageRequest>(head), Is.False);
            Assert.That(em.IsComponentEnabled<ExternalImpulse>(head), Is.False);
            Assert.That(PunchAimAssist.IsValidTarget(em, orbs[0], head), Is.False);
        }
        [Test] public void Snake004_LastCutExposesLivingHeadAndNeverRegrows()
        {
            Launch(orbs[0]); Sever(); Sever();
            Assert.That(SnakeHitRules.Protected(em, head), Is.False);
            Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.Zero);
            Assert.That(PunchResolution.TryApply(em, head, Punch), Is.True);
        }
        [TestCase(.1f, 1), TestCase(40f, 2)]
        public void Snake004_SpawnProfileHealthSurvivesTailInitializationAndExposure(float maximum, int punches)
        {
            foreach (var orb in orbs) em.DestroyEntity(orb);
            var prefab = Body(float3.zero);
            em.AddComponent<Prefab>(prefab);
            em.RemoveComponent<EnemyTier>(prefab);
            em.AddComponent<EnemyMovementSettings>(prefab);
            em.AddComponent<EnemyGroundConstraint>(prefab);
            em.AddComponent<NavigationAgent>(prefab);
            em.AddComponent<EnemyMovementSettings>(head);
            em.AddComponent<EnemyGroundConstraint>(head);
            em.AddComponentData(head, new EnemyLifetime { Generation = 1 });
            em.SetComponentData(head, new Health { Current = maximum, Max = maximum });
            em.SetComponentData(head, new SnakeHead());
            var tuning = em.GetComponentData<SnakeTuning>(head);
            tuning.OrbPrefab = prefab; tuning.Spacing = 1.5f; em.SetComponentData(head, tuning);
            var spawn = world.GetOrCreateSystem<SnakeTailSpawnSystem>();
            spawn.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<Health>(head).Current, Is.EqualTo(maximum));
            Assert.That(em.GetComponentData<Health>(head).Max, Is.EqualTo(maximum));
            Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(6));
            Assert.That(PunchResolution.TryApply(em, head, Punch), Is.False);
            Launch(em.GetBuffer<SnakeTailLink>(head)[0].Orb); Sever();
            Assert.That(em.GetComponentData<Health>(head).Current, Is.EqualTo(maximum));
            for (int i = 0; i < punches; i++)
            {
                Assert.That(PunchResolution.TryApply(em, head, Punch), Is.True);
                world.GetOrCreateSystem<DamageApplicationSystem>().Update(world.Unmanaged);
            }
            Assert.That(em.GetComponentData<Health>(head).Current, Is.Zero);
            Assert.That(em.GetComponentData<EnemyLaunchState>(head).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
            spawn.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<Health>(head).Current, Is.Zero, "Tail system must not heal a punched head");
            world.GetOrCreateSystem<EnemyRecoverySystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyLaunchState>(head).Phase, Is.EqualTo(EnemyLaunchPhase.Defeated));
        }
        [Test] public void Snake004_AlwaysVulnerableHeadLaunchRemovesTail()
        {
            var t = em.GetComponentData<SnakeTuning>(head); t.HeadVulnerability = SnakeHeadVulnerability.AlwaysVulnerable; em.SetComponentData(head, t);
            Assert.That(PunchResolution.TryApply(em, head, Punch), Is.True); Sever();
            foreach (var orb in orbs) Assert.That(em.Exists(orb), Is.False);
        }
        [TestCase(SnakeExplosionResponse.LaunchAndSever), TestCase(SnakeExplosionResponse.IgnoreAttachedOrbs)]
        public void Snake005_ExplosionModeAndProtectedHead(SnakeExplosionResponse mode)
        {
            var t = em.GetComponentData<SnakeTuning>(head); t.ExplosionResponse = mode; em.SetComponentData(head, t);
            var source = Body(new float3(0,0,4.5f));
            em.AddComponentData(source, new ExplosiveEnemySettings { Radius = 2, Damage = 20, NormalEnemyKnockbackForce = 15 });
            em.AddComponent<ExplosiveEnemyState>(source); em.AddComponent<ExplosiveDetonationRequest>(source);
            world.GetOrCreateSystemManaged<ExplosionResolutionSystem>().Update(); Sever();
            if (mode == SnakeExplosionResponse.LaunchAndSever)
            {
                Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(1));
                for (int i=1; i<=3; i++) Assert.That(em.GetComponentData<SnakeSegment>(orbs[i]).Detached, Is.EqualTo(1));
                Assert.That(em.Exists(orbs[4]), Is.False);
            }
            else Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(6));
            Assert.That(em.GetComponentData<EnemyLaunchState>(head).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
        }
        [Test] public void Snake003_ClearanceIsSpatialAndPermanent()
        {
            Launch(orbs[1]); Sever();
            em.SetComponentData(orbs[1], LocalTransform.FromPosition(new float3(0,0,1.5f)));
            world.GetOrCreateSystem<SnakeCollisionClearanceSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<SnakeSegment>(orbs[1]).Cleared, Is.Zero);
            em.SetComponentData(orbs[1], LocalTransform.FromPosition(new float3(10,0,0)));
            world.GetOrCreateSystem<SnakeCollisionClearanceSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<SnakeSegment>(orbs[1]).Cleared, Is.EqualTo(1));
            em.SetComponentData(orbs[1], LocalTransform.FromPosition(float3.zero));
            world.GetOrCreateSystem<SnakeCollisionClearanceSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<SnakeSegment>(orbs[1]).Cleared, Is.EqualTo(1));
        }
        [Test] public void Snake003_DetachedOrbDiesAtLaunchEndInsteadOfRecovering()
        {
            Launch(orbs[2]); Sever();
            world.GetOrCreateSystem<EnemyRecoverySystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyLaunchState>(orbs[2]).Phase, Is.EqualTo(EnemyLaunchPhase.Defeated));
            world.GetOrCreateSystem<SnakeOrbCleanupSystem>().Update(world.Unmanaged);
            Assert.That(em.Exists(orbs[2]), Is.False);
        }
        [Test] public void Snake001_PathSamplesFollowCornerInsteadOfCurrentFacing()
        {
            var path = em.GetBuffer<SnakePathPoint>(head);
            path.Add(new SnakePathPoint { Position = new float3(0,0,0) });
            path.Add(new SnakePathPoint { Position = new float3(0,0,3) });
            path.Add(new SnakePathPoint { Position = new float3(3,0,3) });
            float3 tangent; var p = SnakeTailMovementSystem.Sample(path, new float3(3,0,3), 4, out tangent);
            Assert.That(math.distance(p, new float3(0,0,2)), Is.LessThan(.001f));
        }
        [Test] public void Snake007_RetryGenerationClearsDetachedOrbs()
        {
            var sequence = em.CreateEntity(typeof(EnemyWaveSequence));
            em.SetComponentData(sequence, new EnemyWaveSequence { RunGeneration = 2 });
            var s = em.GetComponentData<SnakeSegment>(orbs[2]); s.Encounter = new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 };
            em.SetComponentData(orbs[2], s); Launch(orbs[2]); Sever();
            world.GetOrCreateSystem<SnakeOrbCleanupSystem>().Update(world.Unmanaged);
            Assert.That(em.Exists(orbs[2]), Is.False);
        }
    }
}
