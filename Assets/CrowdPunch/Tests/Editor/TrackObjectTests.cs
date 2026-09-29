using CrowdPunch.Components;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.AI;
using CrowdPunch.Utilities;
using Unity.Collections;
using CrowdPunch.Systems.Presentation;
using CrowdPunch.Mono.Levels;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using CrowdPunch.Mono.Player;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class TrackObjectTests
    {
        private World world;
        private EntityManager em;
        private Entity target;
        private TrackObject track;
        [SetUp] public void Setup()
        {
            world = new World("TRACK requirements"); em = world.EntityManager;
            track = new TrackObject { Direction = new float3(0, 0, 1), Length = 10, RequiredNetHits = 5, SlideDuration = .4f };
            target = em.CreateEntity(typeof(TrackObject), typeof(TrackObjectState), typeof(Barricade), typeof(LocalTransform), typeof(PhysicsVelocity));
            em.SetComponentData(target, track); em.SetComponentData(target, LocalTransform.Identity);
            em.SetComponentData(target, new Barricade { HitsRemaining = 1, RequiredHits = 1, CompleteOnDestruction = 1 });
            em.AddBuffer<BarricadeHitHistory>(target); em.AddBuffer<TrackPushHistory>(target);
        }
        [TearDown] public void Cleanup() => world.Dispose();

        private Entity Source(EnemyLaunchOwner owner = EnemyLaunchOwner.Player)
        {
            var e = em.CreateEntity(typeof(EnemyLaunchState));
            em.SetComponentData(e, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, Owner = owner, LaunchSequence = 1 });
            return e;
        }
        private bool Hit(Entity source, float3 direction, bool explosion = false, uint sequence = 1)
            => TrackObjectHitResolution.TryHit(em, target, source, sequence, direction, explosion, 0, default);

        [Test] public void Track002_ImpactAndBlastDeduplicateButRelaunchAndOtherBodyCount()
        {
            var source = Source();
            Assert.That(Hit(source, track.Direction), Is.True);
            Assert.That(Hit(source, track.Direction, true), Is.False);
            Assert.That(Hit(source, track.Direction, false, 2), Is.True);
            Assert.That(Hit(Source(), track.Direction), Is.True);
            Assert.That(em.GetComponentData<TrackObjectState>(target).TargetStep, Is.EqualTo(3));
        }

        [TestCase(EnemyLaunchOwner.Player)] [TestCase(EnemyLaunchOwner.Enemy)] [TestCase(EnemyLaunchOwner.Boss)]
        public void Track002_DefaultAcceptsAllLaunchOwnersAndAnyExplosion(EnemyLaunchOwner owner)
        {
            var source = Source(owner);
            Assert.That(Hit(source, track.Direction), Is.True);
            Assert.That(Hit(em.CreateEntity(), track.Direction, true), Is.True, "Unlaunched explosions count");
        }

        [Test] public void Track002_PlayerFilterAndProvisionalExplosionOptionAreIndependent()
        {
            track.PlayerBodiesOnly = 1; em.SetComponentData(target, track);
            var source = Source(EnemyLaunchOwner.Boss);
            Assert.That(Hit(source, track.Direction), Is.False);
            Assert.That(Hit(source, track.Direction, true), Is.True);
            track.FilterLaunchedExplosions = 1; em.SetComponentData(target, track);
            Assert.That(Hit(Source(EnemyLaunchOwner.Enemy), track.Direction, true), Is.False);
            Assert.That(Hit(em.CreateEntity(), track.Direction, true), Is.True);
        }

        [Test] public void Track003_ShallowHitsUseWholeStepPerpendicularAndEndpointHitsDoNotBankProgress()
        {
            var source = Source();
            Assert.That(Hit(source, new float3(1, 0, 0)), Is.False);
            Assert.That(Hit(source, new float3(1, 0, .001f)), Is.True);
            for (int i = 0; i < 4; i++) Assert.That(Hit(Source(), track.Direction), Is.True);
            var excess = Source(); Assert.That(Hit(excess, track.Direction), Is.False);
            Assert.That(Hit(Source(), -track.Direction), Is.True);
            Assert.That(em.GetComponentData<TrackObjectState>(target).TargetStep, Is.EqualTo(4));
            Assert.That(Hit(excess, track.Direction), Is.True, "Default only consumes destination-changing contacts");
        }

        [Test] public void Track002_AlternateContactPolicyConsumesPerpendicularHitsAndLockedObjectRejectsHits()
        {
            track.ConsumeNonMovingHits = 1; em.SetComponentData(target, track);
            var source = Source();
            Assert.That(Hit(source, new float3(1, 0, 0)), Is.False);
            Assert.That(Hit(source, track.Direction), Is.False);
            Assert.That(Hit(source, track.Direction, false, 2), Is.True);
            var motion = em.GetComponentData<TrackObjectState>(target); motion.Locked = 1; em.SetComponentData(target, motion);
            Assert.That(Hit(Source(), track.Direction), Is.False);
        }

        [Test] public void Track003_CancellingToActualPoseStopsImmediately()
        {
            var state = new TrackObjectState { Distance = 4, TargetStep = 3, Moving = 1 };
            TrackObjectMotion.Retarget(track, ref state, 2);
            Assert.That(state.Moving, Is.Zero);
            Assert.That(TrackObjectMotion.Next(track, ref state, .2f), Is.EqualTo(4));
        }

        [TestCase(false, true)] [TestCase(true, true)] [TestCase(true, false)]
        public void Track004_PushDisplacesEnemyAndPlayerWithOptionalOncePerSlideDamage(bool damaging, bool playerDamage)
        {
            var physics = new PhysicsWorld(0, 0, 0);
            var physicsEntity = em.CreateEntity(typeof(PhysicsWorldSingleton));
            em.SetComponentData(physicsEntity, new PhysicsWorldSingleton { PhysicsWorld = physics });
            var playerObject = new GameObject("Track push bridge test");
            var bridge = playerObject.AddComponent<PlayerEcsBridge>();
            PlayerBridgeRegistry.TryGetBridge(out var previousBridge);
            PlayerBridgeRegistry.Register(bridge);
            try
            {
                bridge.PublishPlayerSnapshot(new Vector3(0, 0, 1), Vector3.forward, .5f);
                bridge.PublishPlayerHealth(10, 10);
                int playerHits = 0; bridge.EnemyContactHitReceived += (amount, invincibility, impulse) => playerHits++;
                track.DamagingPush = damaging ? (byte)1 : (byte)0;
                track.PushDamagesPlayer = playerDamage ? (byte)1 : (byte)0; track.PushDamage = 2;
                em.SetComponentData(target, track);
                var wall = em.GetComponentData<Barricade>(target); wall.Size = new float3(3); em.SetComponentData(target, wall);
                em.SetComponentData(target, new TrackObjectState { Moving = 1, NextDistance = 1 });
                var character = em.CreateEntity(typeof(Enemy), typeof(NavigationAgent), typeof(EnemyLaunchState), typeof(LocalTransform), typeof(DamageRequest));
                em.SetComponentData(character, new NavigationAgent { Radius = .5f });
                em.SetComponentEnabled<DamageRequest>(character, false);
                var system = world.GetOrCreateSystem<TrackCharacterPushSystem>();
                for (int i = 0; i < 2; i++)
                {
                    em.SetComponentData(character, LocalTransform.FromPosition(new float3(0, 0, 1)));
                    bridge.PublishPlayerSnapshot(new Vector3(0, 0, 1), Vector3.forward, .5f);
                    system.Update(world.Unmanaged);
                    Assert.That(math.abs(em.GetComponentData<LocalTransform>(character).Position.x), Is.GreaterThan(2));
                    Assert.That(math.abs(bridge.Position.x), Is.GreaterThan(2));
                }
                Assert.That(em.IsComponentEnabled<DamageRequest>(character), Is.EqualTo(damaging));
                Assert.That(em.GetComponentData<DamageRequest>(character).Amount, Is.EqualTo(damaging ? 2 : 0));
                Assert.That(playerHits, Is.EqualTo(damaging && playerDamage ? 1 : 0));
            }
            finally
            {
                PlayerBridgeRegistry.Unregister(bridge);
                if (previousBridge != null) PlayerBridgeRegistry.Register(previousBridge);
                UnityEngine.Object.DestroyImmediate(playerObject); physics.Dispose();
            }
        }

        [Test] public void Track002_DirectPunchHasNoEffectAndDoesNotConfirmCooldown()
        {
            var wall = em.GetComponentData<Barricade>(target); wall.Size = new float3(3); em.SetComponentData(target, wall);
            var player = em.CreateEntity(typeof(PlayerSnapshot), typeof(PunchRequest));
            em.SetComponentData(player, new PunchRequest { Origin = new float3(0,0,-2), Direction = track.Direction, Range = 3, Radius = 1 });
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<PunchRequest>(player).HitEnemy, Is.False);
            Assert.That(em.GetComponentData<TrackObjectState>(target).TargetStep, Is.Zero);
        }

        [Test] public void Track004_NavigationRebuildsFootprintAndReopensPreviousPosition()
        {
            var collider = BoxCollider.Create(new BoxGeometry { Size = new float3(3), Orientation = quaternion.identity });
            var wall = em.GetComponentData<Barricade>(target); wall.IntactCollider = collider; em.SetComponentData(target, wall);
            var rectangles = new NativeArray<NavigationRectangle>(0, Allocator.Temp);
            var blob = NavigationGridConstruction.Build(new float2(-15), new float2(15), 1, new float3(.5f, .8f, 1.5f), rectangles, Allocator.Persistent);
            rectangles.Dispose();
            var grid = em.CreateEntity(typeof(NavigationGrid)); em.SetComponentData(grid, new NavigationGrid { Data = blob });
            var system = world.GetOrCreateSystem<TrackNavigationSystem>();
            system.Update(world.Unmanaged);
            var first = em.GetComponentData<NavigationGrid>(grid);
            Assert.That(NavigationGeometry.Segment(ref first.Data.Value, new float2(-5, 0), new float2(5, 0), .5f), Is.False);
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(0, 0, 8)));
            system.Update(world.Unmanaged);
            var next = em.GetComponentData<NavigationGrid>(grid);
            Assert.That(NavigationGeometry.Segment(ref next.Data.Value, new float2(-5, 0), new float2(5, 0), .5f), Is.True);
            Assert.That(NavigationGeometry.Segment(ref next.Data.Value, new float2(-5, 8), new float2(5, 8), .5f), Is.False);
            em.DestroyEntity(target); system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<NavigationGrid>(grid).Data, Is.EqualTo(blob), "Restore original before freeing runtime blob");
            blob.Dispose(); collider.Dispose();
        }

        [Test] public void Track003_SlideMovesSmoothlyThenStopsExactly()
        {
            var state = new TrackObjectState(); TrackObjectMotion.Retarget(track, ref state, 1);
            float first = TrackObjectMotion.Next(track, ref state, .1f);
            Assert.That(first, Is.GreaterThan(0).And.LessThan(.5f));
            Assert.That(TrackObjectMotion.Next(track, ref state, .1f), Is.EqualTo(1).Within(.001));
            Assert.That(TrackObjectMotion.Next(track, ref state, 1), Is.EqualTo(2));
        }

        [Test] public void Track003_RetargetCanReverseBeforeArrivalAndClampsEndpoints()
        {
            var state = new TrackObjectState { Distance = 4 };
            TrackObjectMotion.Retarget(track, ref state, 3);
            state.Distance = TrackObjectMotion.Next(track, ref state, .2f);
            Assert.That(state.Distance, Is.EqualTo(5));
            TrackObjectMotion.Retarget(track, ref state, 2);
            Assert.That(TrackObjectMotion.Next(track, ref state, .2f), Is.LessThan(5));
            Assert.That(state.SlideSequence, Is.EqualTo(1), "Retargeting does not end a continuous slide");
            TrackObjectMotion.Retarget(track, ref state, 99); Assert.That(state.TargetStep, Is.EqualTo(5));
            TrackObjectMotion.Retarget(track, ref state, -10); Assert.That(state.TargetStep, Is.Zero);
        }

        [Test] public void Track001_CompletionWaitsForPhysicalArrivalAndKeepsSurvivors()
        {
            var survivor = em.CreateEntity(typeof(Enemy));
            em.SetComponentData(target, new TrackObjectState { TargetStep = 5, Moving = 1 });
            var completion = world.GetOrCreateSystemManaged<GauntletCompletionSystem>();
            uint before = GauntletCompletionRegistry.Sequence;
            completion.Update(); Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before));
            var motion = world.GetOrCreateSystem<TrackObjectMotionSystem>();
            var arrival = world.GetOrCreateSystem<TrackObjectArrivalSystem>();
            world.SetTime(new TimeData(.2, .2f)); motion.Update(world.Unmanaged); arrival.Update(world.Unmanaged);
            completion.Update(); Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before));
            world.SetTime(new TimeData(.4, .2f)); motion.Update(world.Unmanaged); arrival.Update(world.Unmanaged);
            completion.Update(); completion.Update();
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before + 1));
            Assert.That(em.GetComponentData<TrackObjectState>(target).Locked, Is.EqualTo(1));
            Assert.That(em.GetComponentData<LocalTransform>(target).Position.z, Is.EqualTo(10));
            Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear, Is.EqualTo(float3.zero));
            Assert.That(em.Exists(survivor), Is.True);
        }

        [Test] public void Track004_PushDetectsSweptCrossingAndBothSideCandidatesClearBlock()
        {
            var point = new float3(.2f, 0, 2);
            var end = new float3(0, 0, 4); var size = new float3(3, 3, 3);
            Assert.That(TrackPushGeometry.Intersects(point, .5f, float3.zero, end, quaternion.identity, size), Is.True);
            for (int i = 0; i < 2; i++)
            {
                var safe = TrackPushGeometry.SideCandidate(point, .5f, end, quaternion.identity, size, track.Direction, i == 1);
                Assert.That(math.abs(safe.x), Is.GreaterThan(2));
                Assert.That(safe.y, Is.EqualTo(point.y));
                Assert.That(TrackPushGeometry.Intersects(safe, .5f, float3.zero, end, quaternion.identity, size), Is.False);
            }
        }
    }
}
