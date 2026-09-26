using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Presentation;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Mono.Levels;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class BarricadeTests
    {
        private World world;
        private EntityManager em;
        private Entity wall, body, second;
        [SetUp] public void Setup()
        {
            world = new World("Barricade requirements"); em = world.EntityManager;
            wall = em.CreateEntity(typeof(Barricade), typeof(LocalTransform), typeof(PhysicsCollider));
            em.AddBuffer<BarricadeHitHistory>(wall);
            em.AddBuffer<BarricadeRebound>(wall);
            em.SetComponentData(wall, new Barricade { RequiredHits = 3, HitsRemaining = 3, Size = new float3(28,4,1),
                Sources = BarricadeLaunchSources.All, ExitPosition = new float3(0,0,5), ExitRadius = 2 });
            em.SetComponentData(wall, LocalTransform.Identity);
            body = em.CreateEntity(); second = em.CreateEntity();
        }
        [TearDown] public void Cleanup() => world.Dispose();

        [Test] public void Barricade002_BodyAndItsExplosionCountOnceButRepunchAndOtherBodiesCount()
        {
            Assert.That(BarricadeHitResolution.TryHit(em, wall, body, 1, 1, default), Is.True);
            Assert.That(BarricadeHitResolution.TryHit(em, wall, body, 1, 2, default), Is.False, "Impact and explosion share identity");
            Assert.That(BarricadeHitResolution.TryHit(em, wall, body, 2, 3, default), Is.True, "Re-punch is a new launch");
            Assert.That(BarricadeHitResolution.TryHit(em, wall, second, 1, 3, default), Is.True, "Different chain bodies count");
            Assert.That(em.GetComponentData<Barricade>(wall).HitsRemaining, Is.Zero);
            Assert.That(BarricadeHitResolution.TryHit(em, wall, second, 2, 4, default), Is.False);
        }

        [TestCase(EnemyLaunchOwner.Player)] [TestCase(EnemyLaunchOwner.Enemy)]
        [TestCase(EnemyLaunchOwner.Boss)] [TestCase(EnemyLaunchOwner.None)]
        public void Barricade002_DefaultAcceptsEveryLaunchOwner(EnemyLaunchOwner owner)
            => Assert.That(BarricadeHitResolution.Allows(BarricadeLaunchSources.All, owner), Is.True);

        [Test] public void Barricade002_ConfiguredSourcesRestrictOwnership()
        {
            Assert.That(BarricadeHitResolution.Allows(BarricadeLaunchSources.Player, EnemyLaunchOwner.Player), Is.True);
            Assert.That(BarricadeHitResolution.Allows(BarricadeLaunchSources.Player, EnemyLaunchOwner.Boss), Is.False);
            Assert.That(BarricadeHitResolution.Allows(BarricadeLaunchSources.None, EnemyLaunchOwner.Enemy), Is.False);
        }

        [Test] public void Barricade003_WideWallPunchUsesSurfaceAndDoesNotDamage()
        {
            var punch = new PunchSpecification { Origin = new float3(10,0,-2), Direction = new float3(0,0,1), Range = 2, Radius = .5f };
            Assert.That(BarricadeHitResolution.ContainsPunch(em.GetComponentData<Barricade>(wall), LocalTransform.Identity, punch), Is.True);
            punch.Direction = new float3(0,0,-1);
            Assert.That(BarricadeHitResolution.ContainsPunch(em.GetComponentData<Barricade>(wall), LocalTransform.Identity, punch), Is.False);
            Assert.That(em.GetComponentData<Barricade>(wall).HitsRemaining, Is.EqualTo(3));
        }

        [Test] public void Barricade004_AssistUsesExistingLimitsAndInvalidatesBrokenTarget()
        {
            using var candidates = new NativeArray<Entity>(new[] { wall }, Allocator.Temp);
            float3 origin = new float3(0,0,-5);
            Assert.That(PunchAimAssist.TryGetFallbackTarget(em, body, origin, new float3(0,0,1), 10, 30, candidates, out var target), Is.True);
            Assert.That(target, Is.EqualTo(wall));
            Assert.That(PunchAimAssist.IsWithinAssistLimits(em, wall, origin, new float3(1,0,0), 10, 30), Is.False);
            Assert.That(PunchAimAssist.TryGetFallbackTarget(em, body, origin, new float3(0,0,1), 0, 30, candidates, out _), Is.False);
            var data = em.GetComponentData<Barricade>(wall); data.HitsRemaining = 0; em.SetComponentData(wall, data);
            Assert.That(PunchAimAssist.IsValidTarget(em, body, wall), Is.False);
        }

        [Test] public void Barricade003_DetectionConfirmsCooldownWithoutEnemiesOrDamage()
        {
            var player = em.CreateEntity(typeof(PlayerSnapshot), typeof(PunchRequest));
            em.SetComponentData(player, new PunchRequest { Origin = new float3(10,0,-2), Direction = new float3(0,0,1),
                Range = 2, Radius = .5f, Sequence = 42 });
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            var result = em.GetComponentData<PunchRequest>(player);
            Assert.That(result.IsResolved && result.HitEnemy, Is.True, "Existing bridge consumes this result to start cooldown");
            Assert.That(em.IsComponentEnabled<PunchRequest>(player), Is.False);
            Assert.That(em.GetComponentData<Barricade>(wall).HitsRemaining, Is.EqualTo(3));
        }

        [Test] public void Barricade001_RequiresBreakAndExitEvenWithSurvivors()
        {
            var player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { Position = new float3(0,0,5), IsAvailable = true });
            em.AddComponent<Enemy>(body);
            var system = world.GetOrCreateSystemManaged<GauntletCompletionSystem>();
            uint before = GauntletCompletionRegistry.Sequence;
            system.Update(); Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before));
            var data = em.GetComponentData<Barricade>(wall); data.HitsRemaining = 0; em.SetComponentData(wall, data);
            em.SetComponentData(player, new PlayerSnapshot { Position = new float3(0,0,-5), IsAvailable = true });
            system.Update(); Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before));
            em.SetComponentData(player, new PlayerSnapshot { Position = new float3(0,0,5), IsAvailable = true });
            system.Update(); Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before + 1));
            system.Update(); Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before + 1));
        }

        [TestCase(false)] [TestCase(true)]
        public void Barricade003_ReboundCorrectsTunnelingButBrokenWallPreservesPassThrough(bool broken)
        {
            em.AddComponentData(body, LocalTransform.FromPosition(new float3(1,0,3)));
            em.AddComponentData(body, new PhysicsVelocity { Linear = new float3(4,0,35) });
            em.AddComponentData(body, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1, HomingTarget = wall });
            var data = em.GetComponentData<Barricade>(wall); data.ReboundMultiplier = .85f;
            if (broken) data.HitsRemaining = 0;
            em.SetComponentData(wall, data);
            em.GetBuffer<BarricadeRebound>(wall).Add(new BarricadeRebound { Source = body, LaunchSequence = 1,
                ContactCenter = new float3(0,0,-1), Normal = new float3(0,0,-1), IncomingVelocity = new float3(4,0,35) });
            world.GetOrCreateSystem<BarricadeReboundSystem>().Update(world.Unmanaged);
            var position = em.GetComponentData<LocalTransform>(body).Position;
            Assert.That(position.x, Is.EqualTo(1), "Do not overwrite tangential solver position");
            Assert.That(position.z, Is.EqualTo(broken ? 3 : -1));
            Assert.That(em.GetComponentData<PhysicsVelocity>(body).Linear.z, Is.EqualTo(broken ? 35 : -29.75f).Within(.001));
            Assert.That(em.GetBuffer<BarricadeRebound>(wall).Length, Is.Zero);
            if (!broken) Assert.That(em.GetComponentData<EnemyLaunchState>(body).HomingTarget, Is.EqualTo(Entity.Null));
        }
    }
}
