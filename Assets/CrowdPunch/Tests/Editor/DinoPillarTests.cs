using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using NUnit.Framework;
using Unity.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class DinoPillarTests
    {
        private World world;
        private EntityManager em;
        private Entity boss,pillar,body;
        private DinoTuning bt;
        private PillarTuning pt;
        private BlobAssetReference<Unity.Physics.Collider> solid,empty,bodyShape;
        [SetUp] public void Setup()
        {
            world=new World("PILLAR regressions"); em=world.EntityManager;
            var settings=ScriptableObject.CreateInstance<DinoBossSettings>(); bt=settings.BakeBoss(); pt=settings.BakePillar(); Object.DestroyImmediate(settings);
            bt.InitialPosition=new float3(0,2,6); bt.InitialRotation=quaternion.identity;
            solid=Unity.Physics.BoxCollider.Create(new BoxGeometry { Center=new float3(0,pt.Height*.5f,0),Size=new float3(pt.Width,pt.Height,pt.Width),Orientation=quaternion.identity },CollisionFilter.Default);
            empty=Unity.Physics.BoxCollider.Create(new BoxGeometry { Size=new float3(1),Orientation=quaternion.identity },new CollisionFilter());
            bodyShape=Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius=.5f },CollisionFilter.Default);
            pt.UprightCollider=solid; pt.UnavailableCollider=empty;
            boss=em.CreateEntity(typeof(DinoBoss),typeof(DinoTuning),typeof(Health),typeof(LocalTransform),typeof(PhysicsVelocity));
            em.SetComponentData(boss,bt); em.SetComponentData(boss,LocalTransform.FromPosition(bt.InitialPosition));
            em.SetComponentData(boss,new DinoBoss { Stage=1,Direction=math.forward(),Remaining=7,PreviousPosition=bt.InitialPosition });
            em.SetComponentData(boss,new Health { Current=3,Max=3 });
            pillar=em.CreateEntity(typeof(FallingPillar),typeof(PillarTuning),typeof(LocalTransform),typeof(PhysicsCollider));
            em.SetComponentData(pillar,new FallingPillar { Boss=boss }); em.SetComponentData(pillar,pt);
            em.SetComponentData(pillar,LocalTransform.Identity); em.SetComponentData(pillar,new PhysicsCollider { Value=solid }); em.AddBuffer<PillarFallHit>(pillar);
            body=em.CreateEntity(typeof(Enemy),typeof(EnemyLaunchState),typeof(PhysicsVelocity),typeof(LocalTransform),typeof(PhysicsCollider),typeof(RespawnRequest),
                typeof(Health),typeof(EnemyTier),typeof(EnemyDamageState),typeof(DamageRequest),typeof(DeathRequest),typeof(EnemyHealthBarVisibility),typeof(ExternalImpulse),typeof(KnockbackRecovery));
            em.SetComponentData(body,new Health { Current=100,Max=100 }); em.SetComponentData(body,new EnemyTier { Value=EnemyCombatTier.Normal });
            em.SetComponentData(body,new PhysicsCollider { Value=bodyShape }); em.SetComponentData(body,LocalTransform.FromPosition(new float3(0,.5f,4)));
            em.SetComponentEnabled<RespawnRequest>(body,false); em.SetComponentEnabled<DamageRequest>(body,false);
            world.SetTime(new TimeData(10,.02f));
        }
        [TearDown] public void Cleanup() { world.Dispose(); solid.Dispose(); empty.Dispose(); bodyShape.Dispose(); }

        [TestCase(EnemyLaunchOwner.Player,true)] [TestCase(EnemyLaunchOwner.Boss,false)]
        [TestCase(EnemyLaunchOwner.Enemy,false)] [TestCase(EnemyLaunchOwner.Environment,false)] [TestCase(EnemyLaunchOwner.None,false)]
        public void Pillar002_OwnershipAndPassThrough(EnemyLaunchOwner owner,bool expected)
        {
            var launch=new EnemyLaunchState { Phase=EnemyLaunchPhase.Launched,Owner=owner,LaunchSequence=8,LaunchDamage=17 };
            em.SetComponentData(body,launch); em.SetComponentData(body,new PhysicsVelocity { Linear=new float3(0,0,20) });
            Assert.AreEqual(expected,PillarToppleSystem.TryTopple(em,pillar,launch,math.forward(),10));
            Assert.AreEqual(launch,em.GetComponentData<EnemyLaunchState>(body));
            Assert.AreEqual(new float3(0,0,20),em.GetComponentData<PhysicsVelocity>(body).Linear);
            Assert.AreEqual(expected?0u:CollisionFilter.Default.CollidesWith,em.GetComponentData<PhysicsCollider>(pillar).Value.Value.GetCollisionFilter().CollidesWith);
            Assert.IsFalse(PillarToppleSystem.TryTopple(em,pillar,new EnemyLaunchState { Phase=EnemyLaunchPhase.Active,Owner=EnemyLaunchOwner.Player },math.forward(),10));
        }
        [TestCase(PillarFallDirection.TowardBoss)] [TestCase(PillarFallDirection.IncomingTravel)]
        public void Pillar003_DirectionLocksAtImpact(PillarFallDirection mode)
        {
            pt.FallDirection=mode; em.SetComponentData(pillar,pt);
            PillarToppleSystem.TryTopple(em,pillar,new EnemyLaunchState { Phase=EnemyLaunchPhase.Launched,Owner=EnemyLaunchOwner.Player },new float3(1,0,0),10);
            var direction=em.GetComponentData<FallingPillar>(pillar).Direction;
            Assert.AreEqual(mode==PillarFallDirection.TowardBoss?math.forward():new float3(1,0,0),direction);
            em.SetComponentData(boss,LocalTransform.FromPosition(new float3(-10,2,0)));
            Assert.AreEqual(direction,em.GetComponentData<FallingPillar>(pillar).Direction);
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Pillar001_RequiredHitsAndStaggerRefresh(int required)
        {
            bt.RequiredHits=required; var b=new DinoBoss { Phase=DinoPhase.Burst,Stage=1,Remaining=9 }; var health=new Health { Current=required,Max=required };
            for(int i=0;i<required;i++)
            {
                var p=new FallingPillar(); Assert.IsTrue(PillarDamageResolution.HitBoss(ref p,ref b,ref health,bt));
                Assert.AreEqual(i+1,b.SuccessfulHits); Assert.AreEqual(required-i-1,health.Current);
                Assert.AreEqual(i==required-1?DinoPhase.Defeated:DinoPhase.Stagger,b.Phase);
                Assert.AreEqual(bt.StaggerDuration,b.Remaining);
                Assert.IsFalse(PillarDamageResolution.HitBoss(ref p,ref b,ref health,bt)); b.Remaining=.1f;
            }
        }
        [Test] public void Pillar003_RotatingShapeAndFastCrossing()
        {
            var p=new FallingPillar { Direction=math.forward(),PreviousAngle=0,Angle=math.PI*.5f };
            Assert.IsTrue(PillarFallGeometry.SweptContact(pt,p,new float3(0,1,6),new float3(0,1,6),.5f,.5f));
            Assert.IsFalse(PillarFallGeometry.SweptContact(pt,p,new float3(5,1,6),new float3(5,1,6),.5f,.5f));
            p.PreviousAngle=p.Angle;
            Assert.IsTrue(PillarFallGeometry.SweptContact(pt,p,new float3(-5,.5f,6),new float3(5,.5f,6),.3f,.3f));
        }
        [TestCase(PillarImpactMode.DamageAndPush)] [TestCase(PillarImpactMode.DamageAndLaunch)]
        public void Pillar004_OncePerFallAndEnvironmentOwnership(PillarImpactMode mode)
        {
            pt.ImpactMode=mode; em.SetComponentData(pillar,pt);
            em.SetComponentData(pillar,new FallingPillar { Boss=boss,Phase=PillarPhase.Falling,Direction=math.forward(),PreviousAngle=math.PI*.5f,Angle=math.PI*.5f,Elapsed=pt.FallDuration });
            var system=world.GetOrCreateSystem<PillarFallContactSystem>(); system.Update(world.Unmanaged); system.Update(world.Unmanaged);
            Assert.AreEqual(100-pt.EnemyDamage,em.GetComponentData<Health>(body).Current);
            Assert.AreEqual(1,em.GetBuffer<PillarFallHit>(pillar).Length);
            var launch=em.GetComponentData<EnemyLaunchState>(body);
            Assert.AreEqual(mode==PillarImpactMode.DamageAndLaunch?EnemyLaunchPhase.Launched:EnemyLaunchPhase.Active,launch.Phase);
            if(mode==PillarImpactMode.DamageAndLaunch)
            { Assert.AreEqual(EnemyLaunchOwner.Environment,launch.Owner); Assert.IsFalse(PillarToppleSystem.Eligible(launch)); }
        }
        [Test] public void Pillar005_MissRegeneratesButSuccessfulPillarStaysConsumed()
        {
            em.SetComponentData(body,LocalTransform.FromPosition(new float3(20,.5f,20)));
            var p=new FallingPillar { Boss=boss,Phase=PillarPhase.Falling,Elapsed=pt.FallDuration+pt.FallenDuration,Direction=math.forward() };
            em.SetComponentData(pillar,p);
            var system=world.GetOrCreateSystem<PillarRegenerationSystem>(); system.Update(world.Unmanaged);
            Assert.AreEqual(PillarPhase.Waiting,em.GetComponentData<FallingPillar>(pillar).Phase);
            world.SetTime(new TimeData(10+pt.RegenerationDelay+.1f,.02f)); system.Update(world.Unmanaged);
            Assert.AreEqual(PillarPhase.Upright,em.GetComponentData<FallingPillar>(pillar).Phase);
            p.HitBoss=1; em.SetComponentData(pillar,p); system.Update(world.Unmanaged);
            Assert.AreEqual(PillarPhase.Consumed,em.GetComponentData<FallingPillar>(pillar).Phase);
            world.SetTime(new TimeData(99,.02f)); system.Update(world.Unmanaged);
            Assert.AreEqual(PillarPhase.Consumed,em.GetComponentData<FallingPillar>(pillar).Phase);
        }
        [Test] public void Pillar005_RegenerationWaitsForPlayerBossAndPushesCrowd()
        {
            var p=new FallingPillar { Boss=boss,Phase=PillarPhase.Waiting }; em.SetComponentData(pillar,p);
            em.SetComponentData(boss,LocalTransform.FromPosition(new float3(0,2,0)));
            var system=world.GetOrCreateSystem<PillarRegenerationSystem>(); system.Update(world.Unmanaged);
            Assert.AreEqual(PillarPhase.Waiting,em.GetComponentData<FallingPillar>(pillar).Phase);
            em.SetComponentData(boss,LocalTransform.FromPosition(bt.InitialPosition));
            var player=em.CreateEntity(typeof(PlayerSnapshot)); em.SetComponentData(player,new PlayerSnapshot { IsAvailable=true,Radius=1,Position=float3.zero });
            system.Update(world.Unmanaged); Assert.AreEqual(PillarPhase.Waiting,em.GetComponentData<FallingPillar>(pillar).Phase);
            em.SetComponentData(player,new PlayerSnapshot()); em.SetComponentData(body,LocalTransform.FromPosition(new float3(0,.5f,0)));
            system.Update(world.Unmanaged); Assert.Greater(math.length(em.GetComponentData<PhysicsVelocity>(body).Linear.xz),0);
            Assert.AreEqual(EnemyLaunchPhase.Active,em.GetComponentData<EnemyLaunchState>(body).Phase);
            Assert.AreEqual(100,em.GetComponentData<Health>(body).Current);
            em.SetComponentData(body,LocalTransform.FromPosition(new float3(4,.5f,0))); system.Update(world.Unmanaged);
            Assert.AreEqual(PillarPhase.Upright,em.GetComponentData<FallingPillar>(pillar).Phase);
        }
        [Test] public void Pillar001_CycleWarningAndStaggerResume()
        {
            var b=new DinoBoss { Stage=3,Phase=DinoPhase.Chase,Remaining=0 };
            DinoCycleSystem.Advance(ref b,bt,.02f); Assert.AreEqual(DinoPhase.Warning,b.Phase);
            b.Remaining=0; DinoCycleSystem.Advance(ref b,bt,.02f); Assert.AreEqual(DinoPhase.Burst,b.Phase);
            b.Phase=DinoPhase.Stagger; b.Remaining=0; DinoCycleSystem.Advance(ref b,bt,.02f);
            Assert.AreEqual(DinoPhase.Chase,b.Phase); Assert.AreEqual(bt.ChaseDurations.z,b.Remaining);
            Assert.Less(bt.BurstTurnDegrees,bt.ChaseTurnDegrees); Assert.Greater(bt.ChaseSpeeds.z,bt.ChaseSpeeds.x);
        }
        [Test] public void Pillar006_TargetingAndRestartRestoreCompleteState()
        {
            Assert.IsFalse(PunchAimAssist.IsValidTarget(em,body,boss)); Assert.IsTrue(PunchAimAssist.IsValidTarget(em,body,pillar));
            em.SetComponentData(pillar,new FallingPillar { Boss=boss,Phase=PillarPhase.Consumed,HitBoss=1,FallSequence=5 });
            em.GetBuffer<PillarFallHit>(pillar).Add(new PillarFallHit { Target=body });
            Assert.IsFalse(PunchAimAssist.IsValidTarget(em,body,pillar));
            em.SetComponentData(boss,new DinoBoss { Phase=DinoPhase.Defeated,SuccessfulHits=3,NextPlayerContact=99 });
            DinoEncounterReset.Reset(em);
            Assert.AreEqual(DinoPhase.Chase,em.GetComponentData<DinoBoss>(boss).Phase);
            Assert.AreEqual(3,em.GetComponentData<Health>(boss).Current); Assert.AreEqual(0,em.GetComponentData<DinoBoss>(boss).NextPlayerContact);
            Assert.AreEqual(PillarPhase.Upright,em.GetComponentData<FallingPillar>(pillar).Phase);
            Assert.AreEqual(0,em.GetBuffer<PillarFallHit>(pillar).Length); Assert.AreEqual(1,em.GetComponentData<LocalTransform>(pillar).Scale);
            Assert.AreEqual(solid,em.GetComponentData<PhysicsCollider>(pillar).Value);
        }
        [Test] public void Pillar002_TriggerPassThroughSurvivesAlternateLaunchResponse()
        {
            pt.ImpactMode=PillarImpactMode.DamageAndLaunch; em.SetComponentData(pillar,pt);
            var launch=new EnemyLaunchState { Phase=EnemyLaunchPhase.Launched,Owner=EnemyLaunchOwner.Player,LaunchSequence=7 };
            em.SetComponentData(body,launch); em.SetComponentData(body,LocalTransform.FromPosition(new float3(0,.5f,0)));
            PillarToppleSystem.TryTopple(em,pillar,launch,math.forward(),10,body);
            world.GetOrCreateSystem<PillarFallContactSystem>().Update(world.Unmanaged);
            Assert.AreEqual(launch,em.GetComponentData<EnemyLaunchState>(body));
            Assert.AreEqual(100,em.GetComponentData<Health>(body).Current);
        }
        [Test] public void Pillar001_BossContactPushDoesNotLaunchOrDamageCrowd()
        {
            em.SetComponentData(body,LocalTransform.FromPosition(bt.InitialPosition+new float3(1.6f,0,0)));
            var before=em.GetComponentData<EnemyLaunchState>(body);
            world.GetOrCreateSystem<DinoContactSystem>().Update(world.Unmanaged);
            Assert.AreEqual(before,em.GetComponentData<EnemyLaunchState>(body));
            Assert.AreEqual(100,em.GetComponentData<Health>(body).Current);
            Assert.Greater(em.GetComponentData<PhysicsVelocity>(body).Linear.x,0);
        }
        [Test] public void Pillar006_BossDefeatOverridesWaveCompletionAndStopsReplenishment()
        {
            var sequence=em.CreateEntity(typeof(EnemyWaveSequence),typeof(EnemyWaveEncounterComplete));
            em.SetComponentEnabled<EnemyWaveEncounterComplete>(sequence,true);
            em.AddComponentData(body,new BossCrowdMember { Encounter=boss,ReplenishDelay=3 });
            em.AddComponentData(body,new EnemyRespawnSettings { Enabled=1 });
            uint before=CrowdPunch.Mono.Levels.GauntletCompletionRegistry.Sequence;
            var complete=world.GetOrCreateSystemManaged<CrowdPunch.Systems.Presentation.GauntletCompletionSystem>(); complete.Update();
            Assert.AreEqual(before,CrowdPunch.Mono.Levels.GauntletCompletionRegistry.Sequence);
            var supply=world.GetOrCreateSystem<BossCrowdReplenishmentSystem>(); supply.Update(world.Unmanaged);
            Assert.AreEqual(1,em.GetComponentData<EnemyRespawnSettings>(body).Enabled);
            em.SetComponentData(boss,new DinoBoss { Phase=DinoPhase.Defeated }); complete.Update(); complete.Update(); supply.Update(world.Unmanaged);
            Assert.AreEqual(before+1,CrowdPunch.Mono.Levels.GauntletCompletionRegistry.Sequence);
            Assert.AreEqual(0,em.GetComponentData<EnemyRespawnSettings>(body).Enabled);
        }
        [Test] public void Pillar006_LocalCrowdStaysNearPillarAndDoesNotOverrideLaunchPhysics()
        {
            em.AddComponentData(body,new PillarCrowdMember { Pillar=pillar,Slot=0 });
            em.AddComponentData(body,new EnemyMovementSettings { MoveSpeed=4 }); em.AddComponent<DesiredMovement>(body); em.AddComponent<NavigationIntent>(body);
            em.SetComponentData(body,LocalTransform.FromPosition(new float3(15,.5f,0)));
            var system=world.GetOrCreateSystem<PillarCrowdPositioningSystem>(); system.Update(world.Unmanaged);
            var intent=em.GetComponentData<NavigationIntent>(body);
            Assert.LessOrEqual(math.distance(intent.Destination.xz,pt.InitialPosition.xz),bt.PillarCrowdRadius);
            Assert.Greater(em.GetComponentData<DesiredMovement>(body).Speed,0);
            em.SetComponentData(body,new EnemyLaunchState { Phase=EnemyLaunchPhase.Launched });
            var sentinel=new DesiredMovement { Direction=new float3(1,0,0),Speed=99 }; em.SetComponentData(body,sentinel);
            system.Update(world.Unmanaged); Assert.AreEqual(sentinel,em.GetComponentData<DesiredMovement>(body));
            em.SetComponentData(body,new EnemyLaunchState()); em.SetComponentData(pillar,new FallingPillar { Boss=boss,Phase=PillarPhase.Consumed,HitBoss=1 });
            system.Update(world.Unmanaged); Assert.AreEqual(sentinel,em.GetComponentData<DesiredMovement>(body));
        }
        [Test] public void Pillar006_LocalReplenishmentStopsOnConsumptionAndDefeat()
        {
            em.AddComponentData(body,new PillarCrowdMember { Pillar=pillar });
            em.AddComponentData(body,new BossCrowdMember { Encounter=boss,ReplenishDelay=3 }); em.AddComponent<EnemyRespawnSettings>(body);
            var system=world.GetOrCreateSystem<BossCrowdReplenishmentSystem>(); system.Update(world.Unmanaged);
            Assert.AreEqual(1,em.GetComponentData<EnemyRespawnSettings>(body).Enabled);
            em.SetComponentData(pillar,new FallingPillar { Boss=boss,Phase=PillarPhase.Waiting }); system.Update(world.Unmanaged);
            Assert.AreEqual(1,em.GetComponentData<EnemyRespawnSettings>(body).Enabled);
            em.SetComponentData(pillar,new FallingPillar { Boss=boss,HitBoss=1 }); system.Update(world.Unmanaged);
            Assert.AreEqual(0,em.GetComponentData<EnemyRespawnSettings>(body).Enabled);
            em.SetComponentData(pillar,new FallingPillar { Boss=boss }); em.SetComponentData(boss,new DinoBoss { Phase=DinoPhase.Defeated }); system.Update(world.Unmanaged);
            Assert.AreEqual(0,em.GetComponentData<EnemyRespawnSettings>(body).Enabled);
        }
        [TestCase(NavigationMode.Travel)] [TestCase(NavigationMode.Committed)]
        public void Pillar006_LocalCrowdKeepsOrdinaryMovementInsideRadius(NavigationMode mode)
        {
            em.AddComponentData(body,new PillarCrowdMember { Pillar=pillar });
            em.AddComponentData(body,new EnemyMovementSettings { MoveSpeed=4 }); em.AddComponent<DesiredMovement>(body); em.AddComponent<NavigationIntent>(body);
            em.SetComponentData(body,LocalTransform.FromPosition(new float3(3,.5f,0)));
            var expected=new NavigationIntent { Destination=new float3(3,.5f,1),Speed=7,ArrivalDistance=.25f,Mode=mode,Separation=new float3(0,0,.2f) };
            var desired=new DesiredMovement { Direction=math.forward(),Speed=7 };
            em.SetComponentData(body,expected); em.SetComponentData(body,desired);
            world.GetOrCreateSystem<PillarCrowdPositioningSystem>().Update(world.Unmanaged);
            Assert.AreEqual(expected,em.GetComponentData<NavigationIntent>(body));
            Assert.AreEqual(desired,em.GetComponentData<DesiredMovement>(body));
        }
        [Test] public void Pillar006_LocalGoalsClipToRadiusAndDetourAroundUprightShaft()
        {
            var goal=PillarCrowdPositioningSystem.Destination(pt,bt,new float3(3,.5f,0),new float3(12,.5f,1),false);
            Assert.AreEqual(bt.PillarCrowdRadius,math.length(goal.xz),.001f);
            var detour=PillarCrowdPositioningSystem.Destination(pt,bt,new float3(3,.5f,0),new float3(-3,.5f,0),true);
            Assert.Greater(math.abs(detour.z),.5f);
            Assert.LessOrEqual(math.length(detour.xz),bt.PillarCrowdRadius);
        }
        [Test] public void Pillar006_BoundaryKeepsTangentialAndInwardVelocity()
        {
            Assert.AreEqual(new float2(0,3),CrowdPunch.Systems.Movement.PillarCrowdBoundary.RemoveOutwardVelocity(new float2(4,0),float2.zero,4,new float2(2,3)));
            Assert.AreEqual(new float2(-2,3),CrowdPunch.Systems.Movement.PillarCrowdBoundary.RemoveOutwardVelocity(new float2(4,0),float2.zero,4,new float2(-2,3)));
            Assert.AreEqual(new float2(2,3),CrowdPunch.Systems.Movement.PillarCrowdBoundary.RemoveOutwardVelocity(new float2(2,0),float2.zero,4,new float2(2,3)));
        }
        [TestCase(EnemyLaunchPhase.Launched)] [TestCase(EnemyLaunchPhase.Recovering)]
        public void Pillar006_MotorBoundaryDoesNotConstrainLaunchedBodies(EnemyLaunchPhase phase)
        {
            em.AddComponentData(body,new PillarCrowdMember { Pillar=pillar });
            em.AddComponentData(body,new PhysicsMass { InverseMass=1 }); em.AddComponent<DesiredMovement>(body); em.AddComponent<EnemyMovementSettings>(body);
            var arena=em.CreateEntity(typeof(ArenaBounds)); em.SetComponentData(arena,new ArenaBounds { Extents=new float3(30) });
            em.SetComponentData(body,LocalTransform.FromPosition(new float3(4,.5f,0)));
            em.SetComponentData(body,new EnemyLaunchState { Phase=phase });
            em.SetComponentData(body,new PhysicsVelocity { Linear=new float3(20,0,3) });
            world.GetOrCreateSystem<CrowdPunch.Systems.Movement.EnemyMovementSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.AreEqual(new float3(20,0,3),em.GetComponentData<PhysicsVelocity>(body).Linear);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(4)]
        public void Pillar006_SupplyHonorsCountAndReusesLaunchedOrPooledSlots(int count)
        {
            bt.EnemiesPerPillar=count; em.SetComponentData(boss,bt);
            var physics=new PhysicsWorld(0,0,0);
            try
            {
                var singleton=em.CreateEntity(typeof(PhysicsWorldSingleton)); em.SetComponentData(singleton,new PhysicsWorldSingleton { PhysicsWorld=physics });
                var prefab=em.CreateEntity(typeof(Prefab),typeof(Enemy),typeof(LocalTransform),typeof(EnemyMovementSettings),typeof(NavigationAgent),typeof(Health),
                    typeof(EnemyContactDamageSettings),typeof(EnemyContactAttemptState),typeof(EnemySeparationDistance),typeof(EnemyArchetypeSeparationDistances),
                    typeof(KnockbackResponse),typeof(EnemyLaunchState),typeof(RespawnRequest));
                var sequence=em.CreateEntity(typeof(BossCrowdSequence),typeof(EnemyWaveSequence));
                em.SetComponentData(sequence,new BossCrowdSequence { Encounter=boss }); em.SetComponentData(sequence,new EnemyWaveSequence { Initialized=1,RunGeneration=1,RandomState=1 });
                em.AddBuffer<EnemyWaveDefinition>(sequence).Add(new EnemyWaveDefinition { ProfileCount=1,BossReplenishDelay=3 });
                em.AddBuffer<EnemyWaveProfile>(sequence).Add(new EnemyWaveProfile { Profile=new EnemySpawnProfile { EnemyPrefab=prefab,
                    Archetype=EnemyArchetypeKind.Baseline,SpawnClearance=.5f,Health=new Health { Current=100,Max=100 } } });
                var system=world.GetOrCreateSystem<PillarCrowdSupplySystem>(); system.Update(world.Unmanaged);
                using var members=em.CreateEntityQuery(typeof(PillarCrowdMember)); Assert.AreEqual(count,members.CalculateEntityCount());
                using var locals=members.ToEntityArray(Allocator.Temp);
                foreach(var e in locals)
                { em.SetComponentData(e,new EnemyLaunchState { Phase=EnemyLaunchPhase.Launched }); em.SetComponentEnabled<RespawnRequest>(e,true); }
                system.Update(world.Unmanaged); Assert.AreEqual(count,members.CalculateEntityCount(),"Launching or pooling cannot grow the bounded local crowd");
            }
            finally { physics.Dispose(); }
        }
    }
}
