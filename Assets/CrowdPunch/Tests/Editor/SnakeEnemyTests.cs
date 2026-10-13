using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class SnakeEnemyTests
    {
        private World world;
        private EntityManager em;
        private Entity head;
        private Entity[] orbs;
        private BlobAssetReference<Unity.Physics.Collider> headCollider;
        [SetUp] public void Setup()
        {
            world = new World("SNAKE-001..007"); em = world.EntityManager;
            world.SetTime(new TimeData(1, .2f));
            head = Body(float3.zero);
            em.AddComponentData(head, new SnakeTuning { SegmentCount = 6, OrbRadius = .6f,
                Spacing = 1.5f, ProtectedColor = new float4(.3f,.8f,.95f,1), VulnerableColor = new float4(1,.65f,.08f,1) });
            em.AddComponentData(head, new SnakeHead { AttachedCount = 6, Generation = 1, Initialized = 1,
                DynamicMass = new PhysicsMass { InverseMass = 1 } });
            em.AddComponentData(head, new PhysicsMass { InverseMass = 1 });
            headCollider = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = new float3(0,-.25f,0), Vertex1 = new float3(0,.25f,0), Radius = .75f
            }, CollisionFilter.Default, Unity.Physics.Material.Default);
            em.AddComponentData(head, new PhysicsCollider { Value = headCollider });
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
        [TearDown] public void TearDown() { world.Dispose(); headCollider.Dispose(); }
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
        private PunchSpecification TailPunch { get { var punch = Punch; punch.Origin.z = 1; return punch; } }

        [TestCase(EnemyLaunchOwner.Player), TestCase(EnemyLaunchOwner.Enemy), TestCase(EnemyLaunchOwner.Boss)]
        public void Snake002_TipLaunchPreservesEveryPredecessorAndOwner(EnemyLaunchOwner owner)
        {
            Launch(orbs[5], owner); Sever();
            Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(5));
            foreach (var orb in orbs) Assert.That(em.Exists(orb), Is.True);
            Assert.That(em.GetComponentData<SnakeSegment>(orbs[5]).Detached, Is.EqualTo(1));
            Assert.That(em.GetComponentData<EnemyLaunchState>(orbs[5]).Owner, Is.EqualTo(owner));
            Assert.That(SnakeHitRules.Protected(em, orbs[4]), Is.False);
            for (int i=0;i<4;i++) Assert.That(SnakeHitRules.Protected(em,orbs[i]), Is.True);
            Assert.That(em.GetComponentData<EnemyLaunchState>(head).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
        }
        [TestCase(false), TestCase(true)]
        public void Snake002_OneWidePunchLaunchesOnlyCurrentTipInEitherQueryOrder(bool reverse)
        {
            for (int j=0;j<6;j++)
            {
                int i=reverse?5-j:j;
                Assert.That(PunchResolution.TryApply(em,orbs[i],TailPunch), Is.EqualTo(i==5));
            }
            Sever();
            Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(5));
            for(int i=0;i<5;i++) Assert.That(em.GetComponentData<EnemyLaunchState>(orbs[i]).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Assert.That(em.GetComponentData<SnakeSegment>(orbs[5]).Detached, Is.EqualTo(1));
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
            for(int i=5;i>=0;i--) { Launch(orbs[i]); Sever(); }
            Sever();
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
            for(int i=5;i>=0;i--) { Launch(em.GetBuffer<SnakeTailLink>(head)[i].Orb); Sever(); }
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
        [Test] public void Snake004_HeadRemainsProtectedUntilEveryOrbHasLaunched()
        {
            for(int i=5;i>=0;i--)
            {
                Assert.That(PunchResolution.TryApply(em,head,Punch), Is.False);
                Assert.That(PunchResolution.TryApply(em,orbs[i],TailPunch), Is.True); Sever();
                Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(i));
            }
            Assert.That(PunchResolution.TryApply(em,head,Punch), Is.True);
        }
        [TestCase(SnakeExplosionResponse.LaunchAndSever), TestCase(SnakeExplosionResponse.IgnoreAttachedOrbs)]
        public void Snake005_ExplosionModeAndProtectedHead(SnakeExplosionResponse mode)
        {
            var t = em.GetComponentData<SnakeTuning>(head); t.ExplosionResponse = mode; em.SetComponentData(head, t);
            var source = Body(new float3(1,0,4.5f));
            em.AddComponentData(source, new ExplosiveEnemySettings { Radius = 20, Damage = 20, NormalEnemyKnockbackForce = 15 });
            em.AddComponent<ExplosiveEnemyState>(source); em.AddComponent<ExplosiveDetonationRequest>(source);
            world.GetOrCreateSystemManaged<ExplosionResolutionSystem>().Update(); Sever();
            if (mode == SnakeExplosionResponse.LaunchAndSever)
            {
                Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(5));
                Assert.That(em.GetComponentData<SnakeSegment>(orbs[5]).Detached, Is.EqualTo(1));
                for(int i=0;i<5;i++) Assert.That(em.GetComponentData<EnemyLaunchState>(orbs[i]).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            }
            else Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(6));
            Assert.That(em.GetComponentData<EnemyLaunchState>(head).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
        }
        [Test] public void Snake003_ReturnRequiresExternalBounceNotDepartureDistanceOrFloorContact()
        {
            Launch(orbs[5]); Sever();
            em.SetComponentData(orbs[5], LocalTransform.FromPosition(new float3(100,0,0)));
            var segment = em.GetComponentData<SnakeSegment>(orbs[5]);
            Assert.That(PunchAimAssist.IsValidTarget(em,orbs[5],orbs[4]), Is.False);
            SnakeProjectileCollision.RecordExternalBounce(ref segment,new float3(20,-1,0),math.up(),10);
            Assert.That(segment.BouncedOutsideOwner, Is.Zero, "Floor support is not a rebound");
            SnakeProjectileCollision.RecordExternalBounce(ref segment,new float3(20,0,0),new float3(1,0,0),0);
            Assert.That(segment.BouncedOutsideOwner, Is.Zero, "An unresolved contact is not a rebound");
            SnakeProjectileCollision.RecordExternalBounce(ref segment,new float3(20,0,0),new float3(1,0,0),10);
            em.SetComponentData(orbs[5],segment);
            Assert.That(PunchAimAssist.IsValidTarget(em,orbs[5],orbs[4]), Is.True);
            SnakeProjectileCollision.RecordExternalBounce(ref segment,float3.zero,math.up(),0);
            Assert.That(segment.BouncedOutsideOwner, Is.EqualTo(1));
        }
        [Test] public void Snake003_OwnerFilterIncludesDetachedSiblingsButAllowsOtherSnakesAndReturn()
        {
            Launch(orbs[5]); Sever(); Launch(orbs[4]); Sever();
            var projectile = em.GetComponentData<SnakeSegment>(orbs[5]);
            var attached = em.GetComponentData<SnakeSegment>(orbs[3]);
            var sibling = em.GetComponentData<SnakeSegment>(orbs[4]);
            Assert.That(SnakeContactSystem.Excludes(projectile,attached), Is.True);
            Assert.That(SnakeContactSystem.Excludes(projectile,sibling), Is.True);
            var anotherSnake = attached; anotherSnake.Head = Body(new float3(10,0,0));
            Assert.That(SnakeContactSystem.Excludes(projectile,anotherSnake), Is.False);
            var newGeneration = attached; newGeneration.Generation++;
            Assert.That(SnakeContactSystem.Excludes(projectile,newGeneration), Is.False);
            projectile.BouncedOutsideOwner = 1;
            Assert.That(SnakeContactSystem.Excludes(projectile,attached), Is.False);
            Assert.That(SnakeContactSystem.Excludes(attached,projectile), Is.False);
            Assert.That(SnakeContactSystem.Excludes(projectile,sibling), Is.False);
            Assert.That(SnakeContactSystem.Excludes(sibling,projectile), Is.True, "Unbounced sibling still needs its exemption");
        }
        [TestCase(false), TestCase(true)]
        public void Snake003_SweptWallOrCoverReboundArmsReturnWithoutSolverEvent(bool cover)
        {
            Launch(orbs[5]); Sever();
            em.AddComponentData(orbs[5],new PhysicsMass {InverseMass=1});
            var wall = em.CreateEntity();
            var launch = em.GetComponentData<EnemyLaunchState>(orbs[5]);
            if (cover)
            {
                em.AddComponentData(wall,new RotatingCover {ReflectionMultiplier=1});
                em.AddBuffer<CoverReflection>(wall).Add(new CoverReflection {Source=orbs[5],LaunchSequence=launch.LaunchSequence,
                    ContactCenter=new float3(0,0,9),Normal=new float3(0,0,1),IncomingVelocity=new float3(0,0,-20),PlayerPosition=new float3(0,0,12)});
                world.GetOrCreateSystem<CoverReflectionSystem>().Update(world.Unmanaged);
            }
            else
            {
                em.AddComponentData(wall,new Barricade {HitsRemaining=2,ReboundMultiplier=1});
                em.AddBuffer<BarricadeRebound>(wall).Add(new BarricadeRebound {Source=orbs[5],LaunchSequence=launch.LaunchSequence,
                    ContactCenter=new float3(0,0,9),Normal=new float3(0,0,1),IncomingVelocity=new float3(0,0,-20)});
                world.GetOrCreateSystem<BarricadeReboundSystem>().Update(world.Unmanaged);
            }
            Assert.That(em.GetComponentData<SnakeSegment>(orbs[5]).BouncedOutsideOwner, Is.EqualTo(1));
            Assert.That(PunchAimAssist.IsValidTarget(em,orbs[5],orbs[4]), Is.True);
            Assert.That(em.GetComponentData<PhysicsVelocity>(orbs[5]).Linear.z, Is.EqualTo(20));
            Assert.That(em.GetComponentData<EnemyLaunchState>(orbs[5]).Owner, Is.EqualTo(launch.Owner));
        }
        [Test] public void Snake006_OcclusionUsesCurrentHeadPositionAndScale()
        {
            em.SetComponentData(head,LocalTransform.FromPosition(new float3(1,0,4)));
            Assert.That(SnakePunchOcclusion.CanReach(em,orbs[5],Punch.Origin), Is.True);
            var pose = em.GetComponentData<LocalTransform>(head); pose.Scale=2; em.SetComponentData(head,pose);
            Assert.That(SnakePunchOcclusion.CanReach(em,orbs[5],Punch.Origin), Is.False);
            pose.Position.x=4; em.SetComponentData(head,pose);
            Assert.That(SnakePunchOcclusion.CanReach(em,orbs[5],Punch.Origin), Is.True);
        }
        [Test] public void Snake006_HeadBlocksPlayerPunchButSideAndTailApproachesCanLaunchTip()
        {
            Assert.That(PunchResolution.TryApply(em,orbs[5],Punch), Is.False);
            Assert.That(em.IsComponentEnabled<ExternalImpulse>(orbs[5]), Is.False);
            Assert.That(em.IsComponentEnabled<DamageRequest>(orbs[5]), Is.False);
            var side = Punch; side.Origin = new float3(2,0,8); side.Direction = math.normalizesafe(new float3(-2,0,1));
            Assert.That(SnakePunchOcclusion.CanReach(em,orbs[5],side.Origin), Is.True);
            Assert.That(PunchResolution.TryApply(em,orbs[5],side), Is.True);
            Assert.That(SnakePunchOcclusion.CanReach(em,orbs[5],new float3(0,0,10)), Is.True);
            Launch(orbs[5]); Sever();
            Assert.That(PunchResolution.TryApply(em,orbs[5],Punch), Is.False, "Detached repunch also cannot pass through the head");
        }
        [Test] public void Snake003_DetachedOrbDiesAtLaunchEndInsteadOfRecovering()
        {
            Launch(orbs[5]); Sever();
            world.GetOrCreateSystem<EnemyRecoverySystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyLaunchState>(orbs[5]).Phase, Is.EqualTo(EnemyLaunchPhase.Defeated));
            world.GetOrCreateSystem<SnakeOrbCleanupSystem>().Update(world.Unmanaged);
            Assert.That(em.Exists(orbs[5]), Is.False);
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
        [Test] public void Snake006_AimEligibilityFollowsTipThenHead()
        {
            var source=Body(new float3(-2,0,9));
            for(int tip=5;tip>=0;tip--)
            {
                Assert.That(PunchAimAssist.IsValidTarget(em,source,head), Is.False);
                for(int i=0;i<=tip;i++)
                    Assert.That(PunchAimAssist.IsValidTarget(em,source,orbs[i]), Is.EqualTo(i==tip));
                Launch(orbs[tip]); Sever();
            }
            Assert.That(PunchAimAssist.IsValidTarget(em,source,head), Is.True);
        }
        [Test] public void Snake006_OnlyCurrentAttachedTipPublishesDirectionPreview()
        {
            PlayerBridgeRegistry.TryGetBridge(out var previous);
            var root=new UnityEngine.GameObject("Snake preview regression");
            var bridge=root.AddComponent<PlayerEcsBridge>();
            try
            {
                PlayerBridgeRegistry.Register(bridge);
                bridge.PublishPunchPreview(new UnityEngine.Vector3(0,0,1),UnityEngine.Vector3.forward,2,12,2,0,10,180);
                var presentation=world.GetOrCreateSystem<PresentationBridgeSystem>();
                presentation.Update(world.Unmanaged);
                Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.EqualTo(1));
                Assert.That(math.distance(bridge.TrajectoryPreviewSegments[0].Start,
                    em.GetComponentData<LocalTransform>(orbs[5]).Position), Is.LessThan(.001f));
                Launch(orbs[5]); Sever();
                em.SetComponentData(orbs[5],LocalTransform.FromPosition(new float3(50,0,50)));
                presentation.Update(world.Unmanaged);
                Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.EqualTo(1));
                Assert.That(math.distance(bridge.TrajectoryPreviewSegments[0].Start,
                    em.GetComponentData<LocalTransform>(orbs[4]).Position), Is.LessThan(.001f));
            }
            finally
            {
                PlayerBridgeRegistry.Unregister(bridge);
                if(previous!=null)PlayerBridgeRegistry.Register(previous);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        [Test] public void Snake006_BlockedTipClearsAimLockAndHidesArrowThenReappearsFromTailSide()
        {
            PlayerBridgeRegistry.TryGetBridge(out var previous);
            var root = new UnityEngine.GameObject("Snake head occlusion regression");
            var bridge = root.AddComponent<PlayerEcsBridge>();
            var aimTarget = Body(new float3(0,0,11));
            em.AddComponentData(orbs[5],new PunchAimAssistTarget { Target=aimTarget,IsAiming=1 });
            // An empty world is sufficient here: the existing lock is valid if the source is reachable.
            var physics = em.CreateEntity(typeof(PhysicsWorldSingleton));
            em.SetComponentData(physics,new PhysicsWorldSingleton { PhysicsWorld = new PhysicsWorld(0,0,0) });
            var aim = world.GetOrCreateSystem<PunchAimAssistSystem>();
            var presentation = world.GetOrCreateSystem<PresentationBridgeSystem>();
            try
            {
                PlayerBridgeRegistry.Register(bridge);
                bridge.PublishPunchPreview(new UnityEngine.Vector3(0,0,-1),UnityEngine.Vector3.forward,2,10,2,0,10,180);
                aim.Update(world.Unmanaged); presentation.Update(world.Unmanaged);
                Assert.That(em.GetComponentData<PunchAimAssistTarget>(orbs[5]).IsAiming, Is.Zero);
                Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.Zero);
                bridge.PublishPunchPreview(new UnityEngine.Vector3(0,0,1),UnityEngine.Vector3.forward,2,9,2,0,10,180);
                aim.Update(world.Unmanaged); presentation.Update(world.Unmanaged);
                Assert.That(em.GetComponentData<PunchAimAssistTarget>(orbs[5]).IsAiming, Is.EqualTo(1));
                Assert.That(em.GetComponentData<PunchAimAssistTarget>(orbs[5]).Target, Is.EqualTo(aimTarget));
                Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.EqualTo(1));
            }
            finally
            {
                var physicsWorld = em.GetComponentData<PhysicsWorldSingleton>(physics).PhysicsWorld; physicsWorld.Dispose();
                PlayerBridgeRegistry.Unregister(bridge);
                if(previous!=null)PlayerBridgeRegistry.Register(previous);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        [Test] public void Snake006_OnlyTipUsesVulnerableColorThenHeadInheritsIt()
        {
            var entities=new Entity[7]; entities[0]=head;
            for(int i=0;i<6;i++)entities[i+1]=orbs[i];
            var visuals=new Entity[7];
            for(int i=0;i<7;i++)
            {
                em.AddComponent<EnemyArchetype>(entities[i]);
                visuals[i]=em.CreateEntity(typeof(EnemyVisualOwner),typeof(URPMaterialPropertyBaseColor));
                em.SetComponentData(visuals[i],new EnemyVisualOwner {Value=entities[i]});
            }
            var presentation=world.GetOrCreateSystem<EnemyReadabilitySystem>();
            var tuning=em.GetComponentData<SnakeTuning>(head);
            for(int tip=5;tip>=-1;tip--)
            {
                presentation.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                for(int i=0;i<7;i++)
                {
                    bool vulnerable=tip<0 ? i==0 : i==tip+1;
                    var expected=vulnerable?tuning.VulnerableColor:tuning.ProtectedColor;
                    Assert.That(math.distance(em.GetComponentData<URPMaterialPropertyBaseColor>(visuals[i]).Value,expected), Is.LessThan(.001f));
                }
                if(tip>=0) { Launch(orbs[tip]); Sever(); }
            }
        }
        [Test] public void Snake002_MissingTipCannotStrandTheEncounter()
        {
            em.DestroyEntity(orbs[5]); Sever();
            Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount, Is.EqualTo(5));
            Assert.That(SnakeHitRules.Protected(em,orbs[4]), Is.False);
        }
        [Test] public void Snake007_RetryGenerationClearsDetachedOrbs()
        {
            var sequence = em.CreateEntity(typeof(EnemyWaveSequence));
            em.SetComponentData(sequence, new EnemyWaveSequence { RunGeneration = 2 });
            var s = em.GetComponentData<SnakeSegment>(orbs[5]); s.Encounter = new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 };
            em.SetComponentData(orbs[5], s); Launch(orbs[5]); Sever();
            world.GetOrCreateSystem<SnakeOrbCleanupSystem>().Update(world.Unmanaged);
            Assert.That(em.Exists(orbs[5]), Is.False);
        }
    }
}
