using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class RotatingCoverTests
    {
        private World world;
        private EntityManager em;
        private Entity cover, target, body;
        [SetUp] public void Setup()
        {
            world = new World("COVER requirements"); em = world.EntityManager;
            target = em.CreateEntity(typeof(Barricade), typeof(LocalTransform), typeof(PhysicsCollider));
            em.AddBuffer<BarricadeHitHistory>(target);
            cover = em.CreateEntity(typeof(RotatingCover), typeof(RotatingCoverState), typeof(LocalTransform));
            em.AddBuffer<CoverReflection>(cover);
            em.SetComponentData(cover, new RotatingCover { Target = target, Radius = 5, Thickness = .5f, ReflectionMultiplier = 1 });
            em.SetComponentData(cover, LocalTransform.Identity);
            em.SetComponentData(target, new Barricade { Cover = cover, RequiredHits = 4, HitsRemaining = 4,
                CompleteOnDestruction = 1, Sources = BarricadeLaunchSources.All });
            em.SetComponentData(target, LocalTransform.Identity);
            body = em.CreateEntity(typeof(EnemyLaunchState), typeof(LocalTransform), typeof(PhysicsVelocity));
            em.SetComponentData(body, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 7,
                Owner = EnemyLaunchOwner.Player, BelowUsefulMomentumSeconds = .03f, RecoverySecondsRemaining = .4f,
                PlayerImpactLaunchSequence = 3, LaunchDamage = 6, HomingTarget = target });
            em.SetComponentData(body, LocalTransform.FromPosition(new float3(0, 0, -4)));
        }
        [TearDown] public void Cleanup() => world.Dispose();

        [TestCase(0f)] [TestCase(1f)] [TestCase(1.7f)]
        public void Cover003_ReflectionAimsAtSampledPlayerAndPreservesLaunch(float multiplier)
        {
            var c = em.GetComponentData<RotatingCover>(cover); c.ReflectionMultiplier = multiplier; em.SetComponentData(cover, c);
            var contact = new CoverReflection { Source = body, LaunchSequence = 7, ContactCenter = new float3(0, 0, -5),
                Normal = new float3(0, 0, -1), IncomingVelocity = new float3(0, 0, 20), PlayerPosition = new float3(3, 0, -9) };
            em.GetBuffer<CoverReflection>(cover).Add(contact);
            world.GetOrCreateSystem<CoverReflectionSystem>().Update(world.Unmanaged);
            var velocity = em.GetComponentData<PhysicsVelocity>(body).Linear;
            Assert.That(velocity.x, Is.EqualTo(12 * multiplier).Within(.001));
            Assert.That(velocity.z, Is.EqualTo(-16 * multiplier).Within(.001));
            var launch = em.GetComponentData<EnemyLaunchState>(body);
            Assert.That(launch.HomingTarget, Is.EqualTo(Entity.Null));
            Assert.That(launch.LaunchSequence, Is.EqualTo(7));
            Assert.That(launch.Owner, Is.EqualTo(EnemyLaunchOwner.Player));
            Assert.That(launch.LaunchDamage, Is.EqualTo(6));
            Assert.That(launch.PlayerImpactLaunchSequence, Is.EqualTo(3));
            Assert.That(launch.BelowUsefulMomentumSeconds, Is.EqualTo(.03f));
            Assert.That(launch.RecoverySecondsRemaining, Is.EqualTo(.4f));
            Assert.That(em.GetComponentData<LocalTransform>(body).Position.z, Is.EqualTo(-5));
            Assert.That(em.GetBuffer<CoverReflection>(cover).Length, Is.Zero);
        }

        [Test] public void Cover004_OutsideExplosionsAreRejectedEvenAtTheOpening()
        {
            var wall = em.GetComponentData<Barricade>(target);
            Assert.That(BarricadeHitResolution.AllowsExplosion(em, wall, body, new float3(0, 0, -6)), Is.False);
            Assert.That(BarricadeHitResolution.AllowsExplosion(em, wall, body, new float3(0, 0, -2)), Is.True);
            wall.Sources = BarricadeLaunchSources.Boss;
            Assert.That(BarricadeHitResolution.AllowsExplosion(em, wall, body, float3.zero), Is.False);
            wall.Cover = Entity.Null;
            Assert.That(BarricadeHitResolution.AllowsExplosion(em, wall, body, new float3(0, 0, -6)), Is.True, "Gauntlet 13 blast rules stay intact");
        }

        [Test] public void Cover004_ImpactAndBlastCountOnceAndProgressNeverExpires()
        {
            Assert.That(BarricadeHitResolution.TryHit(em, target, body, 7, 0, default), Is.True);
            Assert.That(BarricadeHitResolution.TryHit(em, target, body, 7, 999, default), Is.False);
            Assert.That(em.GetComponentData<Barricade>(target).HitsRemaining, Is.EqualTo(3));
            Assert.That(PunchAimAssist.IsValidTarget(em, body, target), Is.True, "Cover never invalidates aim assistance");
        }

        [Test] public void Cover005_DestructionCompletesImmediatelyWithoutExitOrCleanup()
        {
            var system = world.GetOrCreateSystemManaged<GauntletCompletionSystem>();
            uint before = GauntletCompletionRegistry.Sequence;
            system.Update(); Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before));
            var wall = em.GetComponentData<Barricade>(target); wall.HitsRemaining = 0; em.SetComponentData(target, wall);
            system.Update(); Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before + 1));
            system.Update(); Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before + 1));
        }

        [TestCase(CoverRotationMode.Continuous, 3.5f)]
        [TestCase(CoverRotationMode.RotateAndPause, 2.5f)]
        [TestCase(CoverRotationMode.ReversePeriodically, .5f)]
        public void Cover002_RotationModesCrossCycleBoundaries(CoverRotationMode mode, float expected)
        {
            var c = new RotatingCover { Mode = mode, RadiansPerSecond = 1, RotateSeconds = 2, PauseSeconds = 1, ReverseSeconds = 2 };
            var state = new RotatingCoverState();
            RotatingCoverSystem.Advance(c, ref state, 0, 3.5f);
            Assert.That(state.Angle, Is.EqualTo(expected).Within(.0001));
        }

        [Test] public void Cover002_HitPauseOccursOnceAndAccelerationUsesAccumulatedHits()
        {
            var c = new RotatingCover { RadiansPerSecond = 1, HitResponse = CoverHitResponse.Pause, HitPauseSeconds = 1 };
            var state = new RotatingCoverState();
            RotatingCoverSystem.Advance(c, ref state, 1, .5f); Assert.That(state.Angle, Is.Zero);
            RotatingCoverSystem.Advance(c, ref state, 1, 1); Assert.That(state.Angle, Is.EqualTo(.5f));
            c.HitResponse = CoverHitResponse.Accelerate; c.SpeedIncreasePerHit = .5f;
            RotatingCoverSystem.Advance(c, ref state, 2, 1); Assert.That(state.Angle, Is.EqualTo(2.5f));
        }
    }
}
