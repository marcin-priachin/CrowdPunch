using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class GroundHazardNavigationTests
    {
        private World world;
        private EntityManager em;
        private Entity enemy, patch, arena;
        private BlobAssetReference<NavigationGridBlob> blob;
        [SetUp] public void Setup()
        {
            world = new World("GROUND route contracts"); em = world.EntityManager;
            using var obstacles = new NativeArray<NavigationRectangle>(0,Allocator.Temp);
            blob = NavigationGridConstruction.Build(new float2(-10),new float2(10),1,new float3(.3f,.7f,1.2f),obstacles,Allocator.Persistent);
            arena = em.CreateEntity(typeof(NavigationGrid),typeof(NavigationRuntimeSettings),typeof(GroundHazardPolicy));
            em.SetComponentData(arena,new NavigationGrid { Data = blob });
            em.SetComponentData(arena,new NavigationRuntimeSettings { GlobalBudget = 64,SearchLimit = 1024,ConcurrentSearches = 2,
                MaxRequests = 32,MaxPathLength = 128,FailureDelay = 3,DestinationThreshold = 1,WaypointArrival = .2f,
                SmoothingChecks = 6,LookAhead = .5f,StuckDuration = 20,MinimumProgress = .1f });
            enemy = em.CreateEntity(typeof(Enemy),typeof(LocalTransform),typeof(NavigationAgent),typeof(NavigationIntent),
                typeof(DesiredMovement),typeof(GroundHazardRoute),typeof(EnemyLaunchState),typeof(RespawnRequest),typeof(EnemyMovementSettings));
            em.AddBuffer<GroundHazardWaypoint>(enemy); em.SetComponentEnabled<RespawnRequest>(enemy,false);
            em.SetComponentData(enemy,LocalTransform.FromPosition(new float3(-7,0,0))); em.SetComponentData(enemy,new NavigationAgent { Radius = .25f });
            em.SetComponentData(enemy,new EnemyMovementSettings { BrakingAcceleration = 10 });
            em.SetComponentData(enemy,NavigationIntent.Travel(new float3(7,0,0),3,.2f,float3.zero,NavigationGoalKind.Position));
            patch = em.CreateEntity(typeof(GroundHazard),typeof(GroundHazardState));
            em.SetComponentData(patch,new GroundHazard { HalfSize = new float2(2,3),Radius = 3 });
            em.SetComponentData(patch,new GroundHazardState { Introduced = 1,Phase = GroundHazardPhase.Active });
        }
        [TearDown] public void TearDown() { world.Dispose(); blob.Dispose(); }
        private DesiredMovement Tick(int step)
        {
            world.SetTime(new TimeData(step * .05,.05f));
            var pose = em.GetComponentData<LocalTransform>(enemy);
            em.SetComponentData(enemy,new DesiredMovement { Direction = math.normalizesafe(new float3(7,0,0)-pose.Position),Speed = 3 });
            world.GetOrCreateSystem<GroundHazardNavigationSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            return em.GetComponentData<DesiredMovement>(enemy);
        }
        [TestCase(GroundHazardShape.Rectangle)] [TestCase(GroundHazardShape.Circle)]
        public void Ground005_RoutesAroundWholePatchWithoutCrossingFootprint(GroundHazardShape shape)
        {
            var h = em.GetComponentData<GroundHazard>(patch); h.Shape = shape; em.SetComponentData(patch,h);
            float maxSide = 0;
            for (int i = 0; i < 350; i++)
            {
                var movement = Tick(i); var pose = em.GetComponentData<LocalTransform>(enemy); float3 next = pose.Position + movement.Direction * movement.Speed * .05f;
                Assert.False(GroundHazardGeometry.Intersects(h,pose.Position.xz,next.xz,.25f),"Voluntary travel crossed the damage footprint");
                maxSide = math.max(maxSide,math.abs(next.z)); pose.Position = next; em.SetComponentData(enemy,pose);
            }
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Position.x,Is.GreaterThan(6.5)); Assert.That(maxSide,Is.GreaterThan(3));
        }
        [TestCase(GroundHazardFallback.WaitSafely,false)] [TestCase(GroundHazardFallback.CrossAsLastResort,true)]
        public void Ground005_SealedHazardBarrierUsesConfiguredFallback(GroundHazardFallback fallback, bool crosses)
        {
            em.SetComponentData(arena,new GroundHazardPolicy { Fallback = fallback });
            var h = em.GetComponentData<GroundHazard>(patch); h.HalfSize = new float2(2,20); em.SetComponentData(patch,h);
            DesiredMovement movement = default;
            for (int i = 0; i < 20; i++) movement = Tick(i);
            Assert.That(movement.Speed > 0,Is.EqualTo(crosses));
            Assert.That(em.GetComponentData<GroundHazardRoute>(enemy).Failed,Is.EqualTo(1));
        }
        [TestCase(GroundHazardAvoidance.ActiveOnly,true)] [TestCase(GroundHazardAvoidance.WarningAndActive,false)]
        public void Ground005_WarningAvoidanceRemainsConfigurable(GroundHazardAvoidance mode, bool direct)
        {
            em.SetComponentData(arena,new GroundHazardPolicy { Avoidance = mode });
            em.SetComponentData(patch,new GroundHazardState { Introduced = 1,Phase = GroundHazardPhase.Warning });
            Assert.That(Tick(0).Speed > 0,Is.EqualTo(direct));
        }
        [Test] public void Ground005_ArmoredEnemyStillRoutesAndCaughtBodyEscapes()
        {
            em.AddComponentData(enemy,EnemyArmor.Fresh); em.SetComponentData(enemy,LocalTransform.Identity);
            Assert.That(Tick(0).Speed,Is.GreaterThan(0));
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Position,Is.EqualTo(float3.zero),"Navigation writes intent only");
        }
        [TestCase(EnemyLaunchPhase.Launched,NavigationMode.Travel)]
        [TestCase(EnemyLaunchPhase.Active,NavigationMode.Committed)]
        public void Ground005_LaunchAndCommittedLungeKeepOriginalMovement(EnemyLaunchPhase phase, NavigationMode mode)
        {
            em.SetComponentData(enemy,new EnemyLaunchState { Phase = phase });
            var intent = em.GetComponentData<NavigationIntent>(enemy); intent.Mode = mode; em.SetComponentData(enemy,intent);
            Assert.That(Tick(0).Speed,Is.EqualTo(3));
        }
        [Test] public void Ground005_CommittedDasherKeepsOriginalMovement()
        {
            em.AddComponentData(enemy,new DasherState { Phase = DasherPhase.Dashing }); Assert.That(Tick(0).Speed,Is.EqualTo(3));
        }
        [Test] public void Ground005_ActivationInvalidatesCachedHazardRoute()
        {
            for (int i = 0; i < 10; i++) Tick(i);
            Assert.That(em.GetBuffer<GroundHazardWaypoint>(enemy).Length,Is.GreaterThan(0));
            var second = em.CreateEntity(typeof(GroundHazard),typeof(GroundHazardState));
            em.SetComponentData(second,new GroundHazard { Position = new float3(-7,0,0),HalfSize = new float2(1) });
            em.SetComponentData(second,new GroundHazardState { Introduced = 1,Phase = GroundHazardPhase.Active });
            Tick(11); Assert.That(em.GetBuffer<GroundHazardWaypoint>(enemy).Length,Is.Zero,"Caught-body escape replaces the stale route");
        }
        [Test] public void Ground005_SearchLimitNeverPermitsUnprovenLastResortCrossing()
        {
            em.SetComponentData(arena,new GroundHazardPolicy { Fallback = GroundHazardFallback.CrossAsLastResort });
            var settings = em.GetComponentData<NavigationRuntimeSettings>(arena); settings.SearchLimit = 1; em.SetComponentData(arena,settings);
            Tick(0); Assert.That(Tick(1).Speed,Is.Zero);
            Assert.That(em.GetComponentData<GroundHazardRoute>(enemy).Failed,Is.EqualTo(2));
        }
    }
}
