using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Systems.Presentation;
using CrowdPunch.Utilities;
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
        [TestCase(.3f, 1.1f, -.6f)]
        [TestCase(.8f, 2.4f, .5f)]
        [TestCase(.6f, 1.5f, -.4f)]
        public void Snake006_SegmentSizeSpacingAndHeightSurvivePhysicsProjection(float radius,float spacing,float offset)
        {
            foreach(var orb in orbs)em.DestroyEntity(orb);
            var prefab=Body(float3.zero);em.AddComponent<Prefab>(prefab);em.RemoveComponent<EnemyTier>(prefab);
            em.AddComponent<EnemyMovementSettings>(prefab);em.AddComponent<EnemyGroundConstraint>(prefab);em.AddComponent<NavigationAgent>(prefab);
            em.AddComponent<EnemyMovementSettings>(head);em.AddComponent<EnemyGroundConstraint>(head);
            em.AddComponentData(head,new EnemyLifetime {Generation=1});
            em.SetComponentData(head,LocalTransform.FromPosition(new float3(0,1,0)));
            em.SetComponentData(head,new SnakeHead());
            var tuning=em.GetComponentData<SnakeTuning>(head);
            tuning.OrbPrefab=prefab;tuning.OrbRadius=radius;tuning.Spacing=spacing;tuning.HeightOffset=offset;
            em.SetComponentData(head,tuning);
            world.GetOrCreateSystem<SnakeTailSpawnSystem>().Update(world.Unmanaged);
            var links=em.GetBuffer<SnakeTailLink>(head);
            for(int i=0;i<links.Length;i++)
            {
                var orb=links[i].Orb;var pose=em.GetComponentData<LocalTransform>(orb);
                Assert.That(pose.Scale,Is.EqualTo(radius));
                Assert.That(em.GetComponentData<SnakeSegment>(orb).Radius,Is.EqualTo(radius));
                Assert.That(em.GetComponentData<NavigationAgent>(orb).Radius,Is.EqualTo(radius));
                Assert.That(em.GetComponentData<EnemyContactDamageSettings>(orb).ContactRadius,Is.EqualTo(radius));
                Assert.That(pose.Position.y,Is.EqualTo(1+offset).Within(.001f));
                var previous=i==0?em.GetComponentData<LocalTransform>(head).Position:em.GetComponentData<LocalTransform>(links[i-1].Orb).Position;
                Assert.That(math.distance(previous.xz,pose.Position.xz),Is.EqualTo(spacing).Within(.001f));
            }
            var released=links[5].Orb;var segment=em.GetComponentData<SnakeSegment>(released);segment.Detached=1;em.SetComponentData(released,segment);
            em.SetComponentData(head,LocalTransform.FromPosition(new float3(0,2.25f,0)));
            world.GetOrCreateSystem<SnakeTailMovementSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyGroundReconciliationSystem>().Update(world.Unmanaged);em.CompleteAllTrackedJobs();
            links=em.GetBuffer<SnakeTailLink>(head);
            for(int i=0;i<links.Length;i++)
                Assert.That(em.GetComponentData<LocalTransform>(links[i].Orb).Position.y,
                    Is.EqualTo((i==5?1:2.25f)+offset).Within(.001f),"Only attached segments follow the head-relative Y plane.");
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
        [TestCase(false,false), TestCase(true,false), TestCase(false,true), TestCase(true,true)]
        public void Snake001_ActualSolverIgnoresAttachedSegmentsAndRestoresReleasedContacts(bool differentHead,bool released)
        {
            var collisionWorld=new World("Snake attached contact regression");
            var sphere=Unity.Physics.SphereCollider.Create(new SphereGeometry {Center=float3.zero,Radius=.6f});
            try
            {
                var types=new System.Collections.Generic.List<System.Type>();
                foreach(var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                {
                    if(type.Namespace=="Unity.Physics.Systems")types.Add(type);
                }
                types.Add(typeof(SnakeContactSystem));
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(collisionWorld,types);
                var manager=collisionWorld.EntityManager;
                var step=Unity.Physics.PhysicsStep.Default;step.Gravity=float3.zero;
                var settings=manager.CreateEntity(typeof(Unity.Physics.PhysicsStep));manager.SetComponentData(settings,step);
                var firstHead=manager.CreateEntity();var secondHead=differentHead?manager.CreateEntity():firstHead;
                var bodies=new Entity[2];
                for(int i=0;i<2;i++)
                {
                    var e=manager.CreateEntity(typeof(LocalTransform),typeof(LocalToWorld),typeof(PhysicsCollider),typeof(PhysicsMass),
                        typeof(PhysicsVelocity),typeof(PhysicsWorldIndex),typeof(Simulate),typeof(SnakeSegment));bodies[i]=e;
                    manager.SetComponentData(e,LocalTransform.FromPosition(new float3(i*.5f,0,0)));
                    manager.SetComponentData(e,new PhysicsCollider {Value=sphere});
                    manager.SetComponentData(e,PhysicsMass.CreateDynamic(sphere.Value.MassProperties,1));
                    manager.SetComponentData(e,new SnakeSegment {Head=i==0?firstHead:secondHead,Generation=1,Radius=.6f,
                        Detached=(byte)(i==1&&released?1:0),BouncedOutsideOwner=(byte)(released?1:0)});
                }
                var physics=collisionWorld.GetOrCreateSystemManaged<Unity.Physics.Systems.PhysicsSystemGroup>();
                for(int i=0;i<5;i++)
                {
                    collisionWorld.SetTime(new TimeData((i+1)*.02,.02f));physics.Update();manager.CompleteAllTrackedJobs();
                }
                float speed=math.length(manager.GetComponentData<PhysicsVelocity>(bodies[0]).Linear);
                if(released)Assert.That(speed,Is.GreaterThan(.1f),"Detached projectiles retain eligible solver contacts.");
                else
                {
                    Assert.That(speed,Is.LessThan(.0001f),"Overlapping attached segments must not repel one another.");
                    Assert.That(math.distance(manager.GetComponentData<LocalTransform>(bodies[0]).Position,float3.zero),Is.LessThan(.0001f));
                    Assert.That(math.distance(manager.GetComponentData<LocalTransform>(bodies[1]).Position,new float3(.5f,0,0)),Is.LessThan(.0001f));
                }
            }
            finally {collisionWorld.Dispose();sphere.Dispose();}
        }

        [TestCase(false),TestCase(true)]
        public void Snake001_TailMotorHoldsPathDuringGroundEmergenceOnly(bool spawning)
        {
            em.AddComponent<EnemySpawnAnimation>(head);em.SetComponentEnabled<EnemySpawnAnimation>(head,spawning);
            em.SetComponentData(head,new EnemyLaunchState {Phase=EnemyLaunchPhase.Recovering,RecoverySecondsRemaining=.8f});
            var tuning=em.GetComponentData<SnakeTuning>(head);tuning.WaveAmplitude=0;em.SetComponentData(head,tuning);
            var path=em.GetBuffer<SnakePathPoint>(head);for(int i=12;i>=0;i--)path.Add(new SnakePathPoint {Position=new float3(0,0,i)});
            var original=new float3(2,0,1.5f);em.SetComponentData(orbs[0],LocalTransform.FromPosition(original));
            world.GetOrCreateSystem<SnakeTailMovementSystem>().Update(world.Unmanaged);em.CompleteAllTrackedJobs();
            float correction=em.GetComponentData<PhysicsVelocity>(orbs[0]).Linear.x;
            if(spawning)Assert.That(correction,Is.LessThan(0),"The tail must steer back to its path while the head emerges.");
            else Assert.That(correction,Is.Zero,"Ordinary recovery retains its existing movement gate.");
            Assert.That(em.GetComponentData<LocalTransform>(orbs[0]).Position,Is.EqualTo(original),"The tail motor must steer velocity, not teleport bodies.");
            Assert.That(em.GetComponentData<SnakeHead>(head).AttachedCount,Is.EqualTo(6));
            Assert.That(em.GetComponentData<SnakeSegment>(orbs[0]).Detached,Is.Zero);
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
        [TestCase(3.1f), TestCase(6f), TestCase(12f)]
        public void Snake001_HeadMotorStopsResidualMomentumBeforeObstacleOrArenaBoundary(float speed)
        {
            using var obstacles = new NativeArray<NavigationRectangle>(new[] {
                new NavigationRectangle {Minimum=new float2(2,-5.5f),Maximum=new float2(4,-.5f)} },Allocator.Temp);
            using var grid = NavigationGridConstruction.Build(new float2(-10),new float2(10),1,
                new float3(.62f,.97f,1.62f),obstacles,Allocator.Persistent);
            Assert.That(math.lengthsq(SnakeHeadMovement.LimitStep(ref grid.Value,new float2(4.99f,-3),
                new float2(-speed,0),.75f,.02f)), Is.Zero);
            Assert.That(math.lengthsq(SnakeHeadMovement.LimitStep(ref grid.Value,new float2(9.02f,0),
                new float2(speed,0),.75f,.02f)), Is.Zero);
            Assert.That(SnakeHeadMovement.LimitStep(ref grid.Value,new float2(6,-3),
                new float2(0,speed),.75f,.02f).y, Is.EqualTo(speed));
        }
        [Test] public void Snake001_HeadStepGuardPreservesRoundCornerAndBoundsEscape()
        {
            using var obstacles = new NativeArray<NavigationRectangle>(new[] {
                new NavigationRectangle {Minimum=new float2(2,-5.5f),Maximum=new float2(4,-.5f)} },Allocator.Temp);
            using var grid = NavigationGridConstruction.Build(new float2(-10),new float2(10),1,
                new float3(.62f,.97f,1.62f),obstacles,Allocator.Persistent);
            var escape=new float2(0,1.5f);
            Assert.That(SnakeHeadMovement.LimitStep(ref grid.Value,new float2(4.8f,.25f),escape,.75f,.02f), Is.EqualTo(escape));
            Assert.That(SnakeHeadMovement.LimitStep(ref grid.Value,new float2(-11,0),new float2(1.5f,0),.75f,.02f).x, Is.EqualTo(1.5f));
            Assert.That(math.lengthsq(SnakeHeadMovement.LimitStep(ref grid.Value,new float2(-11,0),new float2(-1.5f,0),.75f,.02f)), Is.Zero);
        }
        [Test] public void Snake001_ActiveHeadGuardDoesNotTeleportOrChangeLaunchedMomentum()
        {
            using var obstacles = new NativeArray<NavigationRectangle>(new[] {
                new NavigationRectangle {Minimum=new float2(2,-5.5f),Maximum=new float2(4,-.5f)} },Allocator.Temp);
            using var grid = NavigationGridConstruction.Build(new float2(-10),new float2(10),1,
                new float3(.62f,.97f,1.62f),obstacles,Allocator.Persistent);
            em.AddComponentData(em.CreateEntity(),new NavigationGrid {Data=grid});
            em.AddComponentData(head,new NavigationAgent {Radius=.75f});
            var pose=LocalTransform.FromPosition(new float3(4.99f,0,-3));
            var velocity=new PhysicsVelocity {Linear=new float3(-6,2,0)};
            em.SetComponentData(head,pose);em.SetComponentData(head,velocity);
            var motor=world.GetOrCreateSystem<SnakeTailMovementSystem>();
            motor.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<PhysicsVelocity>(head).Linear.x, Is.Zero);
            Assert.That(em.GetComponentData<PhysicsVelocity>(head).Linear.y, Is.EqualTo(2));
            Assert.That(em.GetComponentData<LocalTransform>(head).Position, Is.EqualTo(pose.Position));
            var exposed=em.GetComponentData<SnakeHead>(head);exposed.AttachedCount=0;em.SetComponentData(head,exposed);
            Launch(head);em.SetComponentData(head,velocity);motor.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<PhysicsVelocity>(head).Linear, Is.EqualTo(velocity.Linear));
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
        [Test]
        public void Snake006_TipAndExposedHeadChangeColorWithoutTintingFace()
        {
            var entities=new Entity[7]; entities[0]=head;
            for(int i=0;i<6;i++)entities[i+1]=orbs[i];
            var visuals=new Entity[7];
            for(int i=0;i<7;i++)
            {
                em.AddComponent<EnemyArchetype>(entities[i]);
                visuals[i]=em.CreateEntity(typeof(EnemyVisualOwner),typeof(URPMaterialPropertyBaseColor));
                em.SetComponentData(visuals[i],new EnemyVisualOwner {Value=entities[i],UsesAtlas=1,AnimationDriven=1});
            }
            foreach(var visual in visuals) {em.AddComponent<SnakeBodyColor>(visual);em.AddComponent<SnakeBodyTintAmount>(visual);}
            var presentation=world.GetOrCreateSystem<EnemyReadabilitySystem>();
            var tuning=em.GetComponentData<SnakeTuning>(head);
            for(int tip=5;tip>=-1;tip--)
            {
                presentation.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                for(int i=0;i<7;i++)
                {
                    var expected=new float4(1);
                    bool tinted=i==0?tip<0:i-1>=tip;
                    Assert.That(em.GetComponentData<SnakeBodyTintAmount>(visuals[i]).Value,Is.EqualTo(tinted?1f:0f));
                    Assert.That(math.distance(em.GetComponentData<URPMaterialPropertyBaseColor>(visuals[i]).Value,expected),Is.LessThan(.001f));
                }
                Assert.That(em.GetComponentData<SnakeBodyTintAmount>(visuals[0]).Value,Is.EqualTo(tip<0?1f:0f));
                Assert.That(em.GetComponentData<SnakeBodyColor>(visuals[0]).Value,Is.EqualTo(tuning.VulnerableColor));
                if(tip>=0) { Launch(orbs[tip]); Sever(); }
            }
        }
        [Test] public void Snake006_SegmentsUseHeadMeshMaterialSamplesAndAuthoredScale()
        {
            var headPrefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/CrowdPunch/Prefabs/EnemySnake.prefab");
            var segment=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/CrowdPunch/Prefabs/SnakeOrb.prefab");
            var headSkin=headPrefab.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
            var skin=segment.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
            Assert.That(skin,Is.Not.Null);Assert.That(skin.sharedMesh,Is.EqualTo(headSkin.sharedMesh));
            Assert.That(skin.sharedMaterials,Is.EqualTo(headSkin.sharedMaterials));
            var animator=segment.GetComponentInChildren<UnityEngine.Animator>();
            var headAnimator=headPrefab.GetComponentInChildren<UnityEngine.Animator>();
            Assert.That(animator.transform.localScale,Is.EqualTo(headAnimator.transform.localScale));
            Assert.That(animator.runtimeAnimatorController,Is.EqualTo(headAnimator.runtimeAnimatorController));
            Assert.That(animator.applyRootMotion,Is.False);
            var authoring=skin.GetComponent<CrowdPunch.Authoring.EnemyAnimationAuthoring>();
            Assert.That(authoring.Samples,Is.EqualTo(headSkin.GetComponent<CrowdPunch.Authoring.EnemyAnimationAuthoring>().Samples));
            Assert.That(authoring.Profile,Is.EqualTo(EnemyAnimationProfile.SlimeSegment));
            Assert.That(authoring.SpawnMotion,Is.EqualTo(7));
            Assert.That(segment.GetComponentsInChildren<UnityEngine.MeshRenderer>(),Is.Empty,"No primitive sphere visual remains.");
            Assert.That(segment.GetComponentsInChildren<UnityEngine.Collider>().Length,Is.EqualTo(1));
            Assert.That(segment.GetComponent<UnityEngine.SphereCollider>().radius,Is.EqualTo(1));
            Assert.That(segment.GetComponent<CrowdPunch.Authoring.SnakeAuthoring>(),Is.Null,"A segment cannot spawn a recursive tail.");
        }

        [Test] public void Snake006_SlimeSamplesRemainBakeableAndPrefabStartsInForwardIdle()
        {
            var root=UnityEditor.PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/EnemySnake.prefab");
            try
            {
                var animator=root.GetComponentInChildren<UnityEngine.Animator>();
                var renderer=root.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
                var authoring=renderer.GetComponent<CrowdPunch.Authoring.EnemyAnimationAuthoring>();
                using var reader=new System.IO.BinaryReader(new System.IO.MemoryStream(authoring.Samples.bytes));
                Assert.That(reader.ReadInt32(),Is.EqualTo(0x43504133));
                string currentHash=UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)).ToString()
                    + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(renderer.sharedMesh))
                    + UnityEditor.AssetDatabase.GetAssetDependencyHash(UnityEditor.AssetDatabase.GetAssetPath(animator.avatar));
                Assert.That(reader.ReadString(),Is.EqualTo(currentHash),
                    "Stale samples prevent the Slime renderer from baking.");
                int bones=reader.ReadInt32(); reader.ReadInt32();
                for(int bone=0;bone<bones;bone++)reader.ReadString();
                for(int motion=0;motion<EnemyAnimationSamples.MotionCount;motion++)reader.ReadSingle();
                for(int bone=0;bone<bones;bone++)
                {
                    var saved=animator.transform.worldToLocalMatrix*renderer.bones[bone].localToWorldMatrix*renderer.sharedMesh.bindposes[bone];
                    for(int column=0;column<4;column++)for(int row=0;row<3;row++)
                        Assert.That(reader.ReadSingle(),Is.EqualTo(saved[row,column]).Within(.001f),
                            "The saved Slime skeleton must face forward in Idle, rather than upwards in the last sampled Death frame.");
                }
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
        }
        [Test] public void Snake006_SlimePreservesPrefabScaleTextureAndGroundEmergence()
        {
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/CrowdPunch/Prefabs/EnemySnake.prefab");
            var source=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(CrowdPunch.Editor.EnemySnakePrefabBuilder.ModelPath);
            var animator=prefab.GetComponentInChildren<UnityEngine.Animator>();
            Assert.That(animator.transform.localScale,Is.EqualTo(source.transform.localScale));
            Assert.That(prefab.transform.localScale,Is.EqualTo(UnityEngine.Vector3.one));
            Assert.That(prefab.transform.Find("Slime"),Is.Not.Null);
            Assert.That(animator.applyRootMotion,Is.False);
            var renderer=prefab.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
            var material=renderer.sharedMaterial;
            Assert.That(material.shader.name,Is.EqualTo("CrowdPunch/Cel/Enemy"));
            Assert.That(material.GetTexture("_ColorMap"),Is.EqualTo(source.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>().sharedMaterial.mainTexture));
            Assert.That(material.GetFloat("_SkinColorAmount"),Is.Zero);
            var authoring=renderer.GetComponent<CrowdPunch.Authoring.EnemyAnimationAuthoring>();
            Assert.That(authoring.Profile,Is.EqualTo(EnemyAnimationProfile.Slime));
            Assert.That(authoring.SpawnMotion,Is.EqualTo(7));
            var settings=UnityEditor.AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/SnakeEnemySpawnSettings.asset");
            Assert.That(settings.SpawnFromGround,Is.True);
            Assert.That(settings.EnemyPrefab,Is.EqualTo(prefab));
            var controller=(UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
            foreach(var state in controller.layers[0].stateMachine.states)
                Assert.That(UnityEditor.AssetDatabase.GetAssetPath(state.state.motion),Does.StartWith(CrowdPunch.Editor.EnemySnakePrefabBuilder.AnimationFolder));
            Assert.That(controller.layers[0].stateMachine.states[7].state.motion.name,Is.EqualTo("Spawn"));
            using var reader=new System.IO.BinaryReader(new System.IO.MemoryStream(authoring.Samples.bytes));
            reader.ReadInt32();reader.ReadString();int bones=reader.ReadInt32(),frames=reader.ReadInt32();
            for(int b=0;b<bones;b++)reader.ReadString();
            for(int m=0;m<EnemyAnimationSamples.MotionCount;m++)reader.ReadSingle();
            var poses=new UnityEngine.Matrix4x4[bones];var vs=renderer.sharedMesh.vertices;var ws=renderer.sharedMesh.boneWeights;
            float spawnStart=float.NegativeInfinity,spawnEnd=float.PositiveInfinity;
            for(int pose=0;pose<frames*EnemyAnimationSamples.MotionCount;pose++)
            {
                for(int b=0;b<bones;b++)
                {
                    poses[b]=UnityEngine.Matrix4x4.identity;
                    for(int c=0;c<4;c++)for(int r=0;r<3;r++)poses[b][r,c]=reader.ReadSingle();
                }
                float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
                for(int v=0;v<vs.Length;v++)
                {
                    var w=ws[v];var local=poses[w.boneIndex0].MultiplyPoint3x4(vs[v])*w.weight0
                        +poses[w.boneIndex1].MultiplyPoint3x4(vs[v])*w.weight1
                        +poses[w.boneIndex2].MultiplyPoint3x4(vs[v])*w.weight2
                        +poses[w.boneIndex3].MultiplyPoint3x4(vs[v])*w.weight3;
                    Assert.That(renderer.localBounds.Contains(local),Is.True,"Animated vertex escaped culling bounds.");
                    float y=animator.transform.TransformPoint(local).y;
                    minimum=UnityEngine.Mathf.Min(minimum,y);maximum=UnityEngine.Mathf.Max(maximum,y);
                }
                if(pose/frames!=7)Assert.That(minimum,Is.GreaterThanOrEqualTo(-1.001f),"Grounded poses must stay above the capsule floor.");
                if(pose==7*frames)spawnStart=maximum;
                if(pose==8*frames-1)spawnEnd=minimum;
            }
            Assert.That(spawnStart,Is.LessThan(-1.1f),"Spawn begins completely underground.");
            Assert.That(spawnEnd,Is.EqualTo(-1f).Within(.01f),"Extracted root motion must emerge to ground Idle.");
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
