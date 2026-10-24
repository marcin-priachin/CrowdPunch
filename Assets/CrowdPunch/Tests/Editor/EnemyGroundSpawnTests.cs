using CrowdPunch.Components;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Physics;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class EnemyGroundSpawnTests
    {
        private World world;
        private Entity enemy;
        private BlobAssetReference<Collider> collider;

        [SetUp]
        public void SetUp()
        {
            world = new World("Ground spawn animation");
            var em = world.EntityManager;
            em.AddComponentData(em.CreateEntity(), new ArenaGroundPlane { SurfaceHeight = 3 });
            em.AddComponentData(em.CreateEntity(), new EnemyLaunchSettings { UsefulMomentumSpeed = 1, LowMomentumPeriod = 1 });
            collider = CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = new float3(0, -.5f, 0), Vertex1 = new float3(0, .5f, 0), Radius = .5f
            }, CollisionFilter.Default, Material.Default);
            enemy = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(PhysicsVelocity), typeof(PhysicsCollider),
                typeof(EnemyGroundConstraint), typeof(EnemyLaunchState), typeof(EnemyMovementSettings), typeof(DesiredMovement),
                typeof(EnemySpawnAnimation), typeof(RespawnRequest), typeof(Health), typeof(EnemyDamageState), typeof(DeathRequest));
            em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(5, 20, 7)));
            em.SetComponentData(enemy, new PhysicsCollider { Value = collider });
            em.SetComponentData(enemy, new PhysicsVelocity { Linear = new float3(0, -8, 0) });
            em.SetComponentData(enemy, new EnemySpawnAnimation { Duration = .8f, UseAnimation = 1 });
            em.SetComponentData(enemy, new Health { Current = 10, Max = 10 });
            em.SetComponentEnabled<RespawnRequest>(enemy, false);
            em.SetComponentEnabled<DeathRequest>(enemy, false);
            em.SetComponentEnabled<EnemySpawnAnimation>(enemy, false);
            QueueWarning();
        }

        [TearDown]
        public void TearDown() { world.Dispose(); collider.Dispose(); }

        private void QueueWarning()
        {
            var em = world.EntityManager;
            em.AddComponentData(enemy, new EnemySpawnWarning { Position = new float3(5, 20, 7), Radius = .75f });
            em.SetEnabled(enemy, false);
        }

        private void Activate()
        {
            world.SetTime(new TimeData(world.Time.ElapsedTime + .1f, .1f));
            world.GetOrCreateSystem<EnemySpawnWarningSystem>().Update(world.Unmanaged);
            world.EntityManager.CompleteAllTrackedJobs();
        }

        private void Recover(float delta)
        {
            world.SetTime(new TimeData(world.Time.ElapsedTime + delta, delta));
            world.GetOrCreateSystem<EnemyRecoverySystem>().Update(world.Unmanaged);
            world.EntityManager.CompleteAllTrackedJobs();
        }

        [Test]
        public void Info006OptionStartsAtColliderAlignedFloorAndPreservesXZ()
        {
            Activate();
            var em = world.EntityManager;
            Assert.That(em.HasComponent<Disabled>(enemy), Is.False);
            Assert.That(em.HasComponent<EnemySpawnWarning>(enemy), Is.False);
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Position, Is.EqualTo(new float3(5, 4, 7)));
            Assert.That(em.GetComponentData<EnemyGroundConstraint>(enemy).IsLocked, Is.EqualTo(1));
            Assert.That(em.GetComponentData<PhysicsVelocity>(enemy).Linear, Is.EqualTo(float3.zero));
            Assert.That(em.IsComponentEnabled<EnemySpawnAnimation>(enemy), Is.True);
            Assert.That(em.GetComponentData<EnemyLaunchState>(enemy).Phase, Is.EqualTo(EnemyLaunchPhase.Recovering));
            Assert.That(em.GetComponentData<EnemyLaunchState>(enemy).RecoverySecondsRemaining, Is.EqualTo(.8f));
        }

        [TestCase(false, .8f)]
        [TestCase(true, 0f)]
        public void Info006DisabledOptionOrMissingClipKeepsDropSpawn(bool enabled, float duration)
        {
            var em = world.EntityManager;
            em.SetComponentData(enemy, new EnemySpawnAnimation { UseAnimation = (byte)(enabled ? 1 : 0), Duration = duration });
            Activate();
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Position.y, Is.EqualTo(20));
            Assert.That(em.IsComponentEnabled<EnemySpawnAnimation>(enemy), Is.False);
            Assert.That(em.GetComponentData<EnemyLaunchState>(enemy).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
        }

        [Test]
        public void Info006ObstructedWarningDoesNotStartAnimationEarly()
        {
            var em = world.EntityManager;
            var player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(5, 3, 7), Radius = 1 });
            Activate();
            Assert.That(em.HasComponent<Disabled>(enemy), Is.True);
            Assert.That(em.IsComponentEnabled<EnemySpawnAnimation>(enemy), Is.False);
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(30, 3, 30), Radius = 1 });
            Activate();
            Assert.That(em.IsComponentEnabled<EnemySpawnAnimation>(enemy), Is.True);
        }

        [Test]
        public void Info006AnimationCompletesAndRestartsForPooledReuse()
        {
            Activate();
            Recover(.4f);
            var em = world.EntityManager;
            Assert.That(em.IsComponentEnabled<EnemySpawnAnimation>(enemy), Is.True);
            Recover(.4f);
            Assert.That(em.IsComponentEnabled<EnemySpawnAnimation>(enemy), Is.False);
            Assert.That(em.GetComponentData<EnemyLaunchState>(enemy).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            QueueWarning();
            Activate();
            Assert.That(em.IsComponentEnabled<EnemySpawnAnimation>(enemy), Is.True);
            Assert.That(em.GetComponentData<EnemyLaunchState>(enemy).RecoverySecondsRemaining, Is.EqualTo(.8f));
        }

        [Test]
        public void Info006ActualRespawnSystemClearsPlayingStateAndQueuesFreshEmergence()
        {
            Activate();
            var em = world.EntityManager;
            em.AddComponentData(em.CreateEntity(), new ArenaBounds { Center = new float3(0, 20, 0), Extents = new float3(30, 10, 30) });
            em.AddComponentData(enemy, new EnemyRespawnSettings { Enabled = 1 });
            em.AddComponentData(enemy, new HealthBar { Normalized = 1 });
            em.AddComponentData(enemy, new EnemyLifetime { Generation = 1 });
            em.AddBuffer<CollisionDamageHistory>(enemy);
            em.AddComponent<KnockbackRecovery>(enemy);
            em.SetComponentEnabled<KnockbackRecovery>(enemy, false);
            em.SetComponentEnabled<RespawnRequest>(enemy, true);
            world.SetTime(new TimeData(1, .1f));
            var respawn = world.GetOrCreateSystem<EnemyRespawnSystem>();
            respawn.Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.IsComponentEnabled<EnemySpawnAnimation>(enemy), Is.False);
            Assert.That(em.GetComponentData<RespawnRequest>(enemy).IsPooled, Is.EqualTo(1));
            Assert.That(em.GetComponentData<EnemySpawnAnimation>(enemy).UseAnimation, Is.EqualTo(1));
            world.SetTime(new TimeData(7, .1f));
            respawn.Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.HasComponent<Disabled>(enemy), Is.True);
            var point = em.GetComponentData<EnemySpawnWarning>(enemy).Position;
            var warning = em.GetComponentData<EnemySpawnWarning>(enemy);
            warning.SecondsRemaining = 0;
            em.SetComponentData(enemy, warning);
            Activate();
            Assert.That(em.IsComponentEnabled<EnemySpawnAnimation>(enemy), Is.True);
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Position.xz, Is.EqualTo(point.xz));
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Position.y, Is.EqualTo(4));
        }

        [Test]
        public void Combat014PunchCancelsEmergenceWithoutChangingLaunchMomentum()
        {
            Activate();
            var em = world.EntityManager;
            var velocity = new float3(10, 0, 4);
            em.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            em.SetComponentData(enemy, new PhysicsVelocity { Linear = velocity });
            Recover(.1f);
            Assert.That(em.IsComponentEnabled<EnemySpawnAnimation>(enemy), Is.False);
            Assert.That(em.GetComponentData<EnemyLaunchState>(enemy).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
            Assert.That(em.GetComponentData<PhysicsVelocity>(enemy).Linear, Is.EqualTo(velocity));
        }

        [TestCase(false, EnemyArchetypeKind.Ranged)]
        [TestCase(true, EnemyArchetypeKind.Ranged)]
        [TestCase(false, EnemyArchetypeKind.Explosive)]
        [TestCase(true, EnemyArchetypeKind.Explosive)]
        [TestCase(false, EnemyArchetypeKind.Elite)]
        [TestCase(true, EnemyArchetypeKind.Elite)]
        public void Info004SpawnPreservesAuthoredProportions(bool authoredScale, EnemyArchetypeKind archetype)
        {
            var em = world.EntityManager;
            var prefab = em.CreateEntity(typeof(Prefab), typeof(Enemy), typeof(LocalTransform),
                typeof(EnemyMovementSettings), typeof(NavigationAgent), typeof(Health),
                typeof(EnemyContactDamageSettings), typeof(EnemyContactAttemptState),
                typeof(EnemySeparationDistance), typeof(EnemyArchetypeSeparationDistances), typeof(KnockbackResponse));
            var original = new PostTransformMatrix { Value = float4x4.Scale(new float3(1.2f, .9f, 1.1f)) };
            if (authoredScale) em.AddComponentData(prefab, original);
            em.AddComponentData(em.CreateEntity(), new MatchState());
            em.AddComponentData(em.CreateEntity(), new SpawnSettings
            {
                InitialCount = 1,
                Profile = new EnemySpawnProfile
                {
                    EnemyPrefab = prefab, Archetype = archetype,
                    Health = new Health { Current = 10, Max = 10 },
                    RangedSettings = new RangedEnemySettings { ProjectileLaunchProgress = .4f }
                }
            });
            world.GetOrCreateSystem<EnemySpawnSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { archetype == EnemyArchetypeKind.Elite ? ComponentType.ReadOnly<ElitePunchState>() : archetype == EnemyArchetypeKind.Ranged
                    ? ComponentType.ReadOnly<RangedAttackState>() : ComponentType.ReadOnly<ExplosiveEnemyState>() },
                Options = EntityQueryOptions.IncludeDisabledEntities
            });
            var spawned = query.GetSingletonEntity();
            Assert.That(em.HasComponent<PostTransformMatrix>(spawned), Is.EqualTo(authoredScale),
                "Enemy identity must not add scripted proportion scaling.");
            if (authoredScale) Assert.That(em.GetComponentData<PostTransformMatrix>(spawned).Value, Is.EqualTo(original.Value));
            if (archetype == EnemyArchetypeKind.Ranged)
                Assert.That(em.GetComponentData<RangedEnemySettings>(spawned).ProjectileLaunchProgress, Is.EqualTo(.4f));
        }
    }
}
