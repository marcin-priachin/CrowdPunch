using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Movement;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class RollingBossTests
    {
        private World world;
        private EntityManager em;
        private Entity boss,body;
        private RollingTuning t;
        [SetUp] public void Setup()
        {
            world=new World("Rolling regressions"); em=world.EntityManager;
            var asset=ScriptableObject.CreateInstance<RollingBossSettings>(); t=asset.Bake(); Object.DestroyImmediate(asset);
            t.InitialPosition=new float3(0,.8f,10); t.InitialRotation=quaternion.identity;
            boss=em.CreateEntity(typeof(RollingBoss),typeof(RollingTuning),typeof(Health),typeof(LocalTransform),typeof(PhysicsVelocity));
            em.SetComponentData(boss,t); em.SetComponentData(boss,LocalTransform.FromPosition(t.InitialPosition));
            em.SetComponentData(boss,new RollingBoss { Stage=1,CycleStage=1,Phase=RollingPhase.Pause,Remaining=2,Direction=math.forward() });
            em.SetComponentData(boss,new Health { Current=t.Health,Max=t.Health });
            em.AddBuffer<RollingHit>(boss); em.AddBuffer<RollingHitHistory>(boss); em.AddBuffer<RollingCrowdContact>(boss);
            body=em.CreateEntity(typeof(EnemyLaunchState),typeof(EnemyLifetime),typeof(RespawnRequest),typeof(PhysicsVelocity),typeof(LocalTransform),
                typeof(EnemyTier),typeof(Health),typeof(EnemyDamageState),typeof(DamageRequest),typeof(DeathRequest),
                typeof(EnemyHealthBarVisibility),typeof(ExternalImpulse),typeof(KnockbackRecovery));
            em.SetComponentData(body,new Health { Current=100,Max=100 }); em.SetComponentData(body,new EnemyTier { Value=EnemyCombatTier.Normal });
            em.SetComponentData(body,LocalTransform.FromPosition(new float3(0,.5f,8)));
            em.SetComponentEnabled<RespawnRequest>(body,false); em.SetComponentEnabled<DamageRequest>(body,false);
            world.SetTime(new TimeData(10,.02f));
        }
        [TearDown] public void Cleanup() { world.Dispose(); }
        private void Damage() => world.GetOrCreateSystem<RollingDamageSystem>().Update(world.Unmanaged);
        private EnemyLaunchSettings DamageCurve() => new EnemyLaunchSettings { MinimumDamageImpulse=1,BaseCollisionDamageMultiplier=1,MaximumCollisionDamageMultiplier=1 };

        [TestCase(RollingPhase.Pause,true)] [TestCase(RollingPhase.WindUp,true)] [TestCase(RollingPhase.Roll,false)]
        public void Roll001_DamageGatesPreserveActionAndTimer(RollingPhase phase,bool accepted)
        {
            var b=em.GetComponentData<RollingBoss>(boss); b.Phase=phase; em.SetComponentData(boss,b);
            RollingDamageResolution.Queue(em,boss,body,0,40); Damage();
            b=em.GetComponentData<RollingBoss>(boss);
            Assert.AreEqual(accepted?t.Health-40:t.Health,em.GetComponentData<Health>(boss).Current);
            Assert.AreEqual(phase,b.Phase); Assert.AreEqual(2,b.Remaining);
        }
        [Test] public void Roll002_OverflowClampsEachStageAndNewTuningWaitsForNextCycle()
        {
            var b=em.GetComponentData<RollingBoss>(boss); var h=em.GetComponentData<Health>(boss);
            Assert.IsTrue(RollingDamageResolution.Apply(ref b,ref h,t,100000,10));
            Assert.AreEqual(t.Health*t.StageTwoThreshold,h.Current,.01f); Assert.AreEqual(2,b.Stage);
            Assert.AreEqual(1,b.CycleStage); Assert.AreEqual(2,b.Remaining);
            b.Phase=RollingPhase.WindUp; b.Remaining=0;
            RollingCycleSystem.Advance(ref b,t,float3.zero,math.forward(),.02f);
            Assert.AreEqual(t.RollSpeeds.x,b.Speed);
            b.Remaining=0; RollingCycleSystem.Advance(ref b,t,float3.zero,math.forward(),.02f);
            Assert.AreEqual(t.PauseDurations.y,b.Remaining); Assert.AreEqual(2,b.CycleStage);
            RollingDamageResolution.Apply(ref b,ref h,t,100000,20); Assert.AreEqual(3,b.Stage);
            Assert.AreEqual(t.Health*t.StageThreeThreshold,h.Current,.01f);
            RollingDamageResolution.Apply(ref b,ref h,t,100000,30); Assert.AreEqual(0,h.Current); Assert.AreEqual(RollingPhase.Defeated,b.Phase);
        }
        [Test] public void Roll001_WindUpTracksThenRollCommitsWithoutContinuousSteering()
        {
            var b=new RollingBoss { Phase=RollingPhase.WindUp,Remaining=.01f,CycleStage=1,Direction=math.forward() };
            RollingCycleSystem.Advance(ref b,t,float3.zero,new float3(10,0,0),.02f);
            Assert.AreEqual(RollingPhase.Roll,b.Phase); Assert.AreEqual(new float3(1,0,0),b.Direction);
            RollingCycleSystem.Advance(ref b,t,float3.zero,new float3(-10,0,0),.02f);
            Assert.AreEqual(new float3(1,0,0),b.Direction);
        }
        [TestCase(RollingEnd.Duration,false)] [TestCase(RollingEnd.BounceCount,true)]
        public void Roll003_EndModesAndSafeguard(RollingEnd mode,bool endsAtLimit)
        {
            t.End=mode;
            var b=new RollingBoss { Phase=RollingPhase.Roll,Remaining=2,Bounces=t.BounceLimit,Stage=1,CycleStage=1 };
            RollingCycleSystem.Advance(ref b,t,float3.zero,math.forward(),.02f);
            Assert.AreEqual(endsAtLimit?RollingPhase.Pause:RollingPhase.Roll,b.Phase);
            b.Phase=RollingPhase.Roll; b.Remaining=.01f; b.Bounces=0;
            RollingCycleSystem.Advance(ref b,t,float3.zero,math.forward(),.02f); Assert.AreEqual(RollingPhase.Pause,b.Phase);
        }
        [TestCase(RollingAim.Reflect)] [TestCase(RollingAim.ReaimOnCollision)]
        public void Roll003_ObstacleRedirectNeverAimsBackIntoContactedWall(RollingAim mode)
        {
            var result=RollingMotionSystem.Redirect(new float3(1,0,0),new float3(10,0,3),new float3(-1,0,0),mode);
            Assert.Less(result.x,-.01f); Assert.AreEqual(1,math.length(result),.001f);
            if(mode==RollingAim.Reflect) Assert.AreEqual(new float3(-1,0,0),result);
        }
        [TestCase(RollingBodyOwnership.PlayerOnly,EnemyLaunchOwner.Player,true)]
        [TestCase(RollingBodyOwnership.PlayerOnly,EnemyLaunchOwner.Boss,false)]
        [TestCase(RollingBodyOwnership.Any,EnemyLaunchOwner.Boss,true)]
        public void Roll003_BodyOwnershipMode(RollingBodyOwnership mode,EnemyLaunchOwner owner,bool accepted)
        {
            t.Ownership=mode; em.SetComponentData(boss,t);
            em.SetComponentData(body,new EnemyLaunchState { Phase=EnemyLaunchPhase.Launched,Owner=owner,LaunchSequence=1,LaunchDamage=10 });
            RollingDamageResolution.QueueBody(em,boss,body,20,DamageCurve()); Damage();
            Assert.AreEqual(accepted?t.Health-10:t.Health,em.GetComponentData<Health>(boss).Current);
        }
        [Test] public void Roll005_CombinedBlastProtectionRepunchAndPoolLifetime()
        {
            RollingDamageResolution.Queue(em,boss,body,0,10); RollingDamageResolution.Queue(em,boss,body,0,35); Damage();
            Assert.AreEqual(t.Health-35,em.GetComponentData<Health>(boss).Current);
            world.SetTime(new TimeData(20,.02f)); RollingDamageResolution.Queue(em,boss,body,0,100); Damage();
            Assert.AreEqual(t.Health-35,em.GetComponentData<Health>(boss).Current);
            em.SetComponentData(body,new EnemyLaunchState { LaunchSequence=1 });
            RollingDamageResolution.Queue(em,boss,body,1,10); Damage();
            Assert.AreEqual(t.Health-45,em.GetComponentData<Health>(boss).Current);
            em.SetComponentData(body,new EnemyLaunchState { LaunchSequence=2 });
            RollingDamageResolution.Queue(em,boss,body,2,10); Damage(); // spent during protection
            world.SetTime(new TimeData(30,.02f)); RollingDamageResolution.Queue(em,boss,body,2,10); Damage();
            Assert.AreEqual(t.Health-45,em.GetComponentData<Health>(boss).Current);
            em.SetComponentData(body,new EnemyLifetime { Generation=2 });
            RollingDamageResolution.Queue(em,boss,body,2,10); Damage();
            Assert.AreEqual(t.Health-55,em.GetComponentData<Health>(boss).Current);
        }
        [TestCase(RollingCrowdDirection.RollDirection)] [TestCase(RollingCrowdDirection.Outward)]
        public void Roll004_RollReclaimsLaunchAndDamagesCrowd(RollingCrowdDirection direction)
        {
            t.CrowdDirection=direction;
            em.SetComponentData(body,new EnemyLaunchState { Phase=EnemyLaunchPhase.Launched,Owner=EnemyLaunchOwner.Player,LaunchSequence=7,LaunchDamage=20 });
            var b=new RollingBoss { Direction=new float3(1,0,0) };
            RollingCollisionSystem.HitCrowd(em,boss,body,b,t,10);
            var launch=em.GetComponentData<EnemyLaunchState>(body); var velocity=em.GetComponentData<PhysicsVelocity>(body).Linear;
            Assert.AreEqual(8,launch.LaunchSequence); Assert.AreEqual(EnemyLaunchOwner.Boss,launch.Owner);
            Assert.AreEqual(100-t.CrowdDamage,em.GetComponentData<Health>(body).Current);
            Assert.AreEqual(t.LaunchSpeed,math.length(velocity),.001f);
            Assert.AreEqual(direction==RollingCrowdDirection.RollDirection?new float3(1,0,0):new float3(0,0,-1),math.normalize(velocity));
            Assert.IsTrue(LaunchedEnemyPlayerImpactSystem.CanDamagePlayer(launch));
            EnemyLaunchTransition.Begin(ref launch,EnemyLaunchCause.PlayerPunch,20);
            Assert.AreEqual(EnemyLaunchOwner.Player,launch.Owner); Assert.AreEqual(9,launch.LaunchSequence);
        }
        [TestCase(true)] [TestCase(false)] public void Roll004_ResistanceNeverConsumesArmorOrLaunchesElite(bool armored)
        {
            if(armored) em.AddComponentData(body,new EnemyArmor { Stages=3 });
            else em.SetComponentData(body,new EnemyTier { Value=EnemyCombatTier.Elite });
            RollingCollisionSystem.HitCrowd(em,boss,body,new RollingBoss { Direction=math.forward() },t,10);
            Assert.AreEqual(EnemyLaunchPhase.Active,em.GetComponentData<EnemyLaunchState>(body).Phase);
            Assert.AreEqual(armored?100:100-t.CrowdDamage,em.GetComponentData<Health>(body).Current);
            if(armored) Assert.AreEqual(3,em.GetComponentData<EnemyArmor>(body).Stages);
            Assert.AreEqual(t.ResistantPush,math.length(em.GetComponentData<PhysicsVelocity>(body).Linear.xz),.001f);
        }
        [TestCase(RollingTargeting.EveryLivingState,true)] [TestCase(RollingTargeting.VulnerableStates,false)]
        public void Roll003_AimAndHomingShareStateEligibility(RollingTargeting targeting,bool rollingEligible)
        {
            t.Targeting=targeting; em.SetComponentData(boss,t);
            var b=em.GetComponentData<RollingBoss>(boss); b.Phase=RollingPhase.Roll; em.SetComponentData(boss,b);
            Assert.AreEqual(rollingEligible,PunchAimAssist.IsValidTarget(em,body,boss));
            b.Phase=RollingPhase.WindUp; em.SetComponentData(boss,b); Assert.IsTrue(PunchAimAssist.IsValidTarget(em,body,boss));
            b.Phase=RollingPhase.Defeated; em.SetComponentData(boss,b); Assert.IsFalse(PunchAimAssist.IsValidTarget(em,body,boss));
        }
        [Test] public void Roll006_ResetClearsHistoryHealthPoseAndPendingReplenishmentStopsOnDefeat()
        {
            em.AddComponentData(body,new BossCrowdMember { Encounter=boss,ReplenishDelay=3 }); em.AddComponent<EnemyRespawnSettings>(body);
            em.SetComponentData(boss,new RollingBoss { Phase=RollingPhase.Defeated,Stage=3,Remaining=99 });
            world.GetOrCreateSystem<BossCrowdReplenishmentSystem>().Update(world.Unmanaged);
            Assert.AreEqual(0,em.GetComponentData<EnemyRespawnSettings>(body).Enabled);
            em.GetBuffer<RollingHitHistory>(boss).Add(new RollingHitHistory { Source=body });
            var skin=em.CreateEntity(typeof(RollingAnimationPivot),typeof(EnemyAnimationPlayback));
            em.SetComponentData(skin,new RollingAnimationPivot { Center=new float3(1),Angle=2 });
            em.SetComponentData(skin,new EnemyAnimationPlayback { Phase=.6f });
            RollingEncounterReset.Reset(em);
            Assert.AreEqual(0,em.GetBuffer<RollingHitHistory>(boss).Length); Assert.AreEqual(t.Health,em.GetComponentData<Health>(boss).Current);
            Assert.AreEqual(t.OpeningPause,em.GetComponentData<RollingBoss>(boss).Remaining);
            Assert.AreEqual(0,em.GetComponentData<RollingAnimationPivot>(skin).Angle);
            Assert.AreEqual(new float3(1),em.GetComponentData<RollingAnimationPivot>(skin).Center);
            Assert.AreEqual(0,em.GetComponentData<EnemyAnimationPlayback>(skin).Phase);
            world.GetOrCreateSystem<BossCrowdReplenishmentSystem>().Update(world.Unmanaged);
            Assert.AreEqual(1,em.GetComponentData<EnemyRespawnSettings>(body).Enabled);
        }

        [TestCase(RollingContactDanger.RollingOnly,RollingPhase.Pause,false)]
        [TestCase(RollingContactDanger.AllLivingStates,RollingPhase.Pause,true)]
        [TestCase(RollingContactDanger.RollingOnly,RollingPhase.Roll,true)]
        public void Roll003_PlayerContactModesAndRepeatProtection(RollingContactDanger mode,RollingPhase phase,bool dangerous)
        {
            t.ContactDanger=mode; em.SetComponentData(boss,t);
            var b=em.GetComponentData<RollingBoss>(boss); b.Phase=phase; b.PreviousPosition=t.InitialPosition; em.SetComponentData(boss,b);
            var p=em.CreateEntity(typeof(PlayerSnapshot)); em.SetComponentData(p,new PlayerSnapshot { IsAvailable=true,Position=t.InitialPosition,Radius=.5f });
            var system=world.GetOrCreateSystem<RollingPlayerImpactSystem>(); system.Update(world.Unmanaged);
            Assert.AreEqual(dangerous?10+t.ContactInterval:0,em.GetComponentData<RollingBoss>(boss).NextPlayerContact,.001);
            system.Update(world.Unmanaged);
            Assert.AreEqual(dangerous?10+t.ContactInterval:0,em.GetComponentData<RollingBoss>(boss).NextPlayerContact,.001);
        }

        [TestCase(EnemyLaunchOwner.None)] [TestCase(EnemyLaunchOwner.Boss)]
        public void Roll005_ActualExplosionIntegrationAcceptsAnyOwnership(EnemyLaunchOwner owner)
        {
            em.AddComponent<Enemy>(body);
            em.SetComponentData(body,LocalTransform.FromPosition(t.InitialPosition+new float3(0,0,1)));
            em.SetComponentData(body,new EnemyLaunchState { Owner=owner,Phase=owner==EnemyLaunchOwner.None?EnemyLaunchPhase.Active:EnemyLaunchPhase.Launched });
            em.AddComponentData(body,new ExplosiveEnemySettings { Radius=4,Damage=30 });
            em.AddComponent<ExplosiveEnemyState>(body); em.AddComponent<ExplosiveDetonationRequest>(body);
            em.AddComponentData(body,new EnemyContactDamageSettings { ContactRadius=.5f });
            em.CreateEntity(typeof(PlayerSnapshot));
            world.GetOrCreateSystemManaged<ExplosionResolutionSystem>().Update(); Damage();
            Assert.AreEqual(t.Health-30,em.GetComponentData<Health>(boss).Current);
        }

        [Test] public void Roll006_DefeatCompletesOnceWithSurvivorsAndResetAllowsNewCompletion()
        {
            var b=em.GetComponentData<RollingBoss>(boss); b.Phase=RollingPhase.Defeated; em.SetComponentData(boss,b);
            var completion=world.GetOrCreateSystemManaged<CrowdPunch.Systems.Presentation.GauntletCompletionSystem>();
            uint before=CrowdPunch.Mono.Levels.GauntletCompletionRegistry.Sequence;
            completion.Update(); completion.Update();
            Assert.AreEqual(before+1,CrowdPunch.Mono.Levels.GauntletCompletionRegistry.Sequence);
            Assert.IsTrue(em.Exists(body)); RollingEncounterReset.Reset(em); completion.Update();
            b=em.GetComponentData<RollingBoss>(boss); b.Phase=RollingPhase.Defeated; em.SetComponentData(boss,b); completion.Update();
            Assert.AreEqual(before+2,CrowdPunch.Mono.Levels.GauntletCompletionRegistry.Sequence);
        }

        [Test] public void Roll001_RealDirectPunchCannotDamageOrLaunchBoss()
        {
            var p=em.CreateEntity(typeof(PlayerSnapshot),typeof(PunchRequest));
            em.SetComponentData(p,new PunchRequest { Origin=t.InitialPosition-new float3(0,0,2),Direction=math.forward(),Range=5,Radius=2,Damage=1000,Strength=100 });
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            Assert.AreEqual(t.Health,em.GetComponentData<Health>(boss).Current);
            Assert.AreEqual(float3.zero,em.GetComponentData<PhysicsVelocity>(boss).Linear);
            Assert.AreEqual(RollingPhase.Pause,em.GetComponentData<RollingBoss>(boss).Phase);
            Assert.IsFalse(em.GetComponentData<PunchRequest>(p).HitEnemy);
        }

        [TestCase(RollingTargeting.EveryLivingState,true)] [TestCase(RollingTargeting.VulnerableStates,false)]
        public void Roll003_ActualHomingHonorsRollingTargetMode(RollingTargeting mode,bool homes)
        {
            t.Targeting=mode; em.SetComponentData(boss,t);
            var b=em.GetComponentData<RollingBoss>(boss); b.Phase=RollingPhase.Roll; em.SetComponentData(boss,b);
            em.AddComponent<Enemy>(body);
            var settings=em.CreateEntity(typeof(EnemyLaunchSettings));
            em.SetComponentData(settings,new EnemyLaunchSettings { LaunchHomingDegreesPerSecond=90 });
            em.SetComponentData(body,new EnemyLaunchState { Phase=EnemyLaunchPhase.Launched,HomingTarget=boss });
            em.SetComponentData(body,new PhysicsVelocity { Linear=new float3(10,3,0) });
            world.GetOrCreateSystem<CrowdPunch.Systems.Physics.EnemyLaunchHomingSystem>().Update(world.Unmanaged);
            var velocity=em.GetComponentData<PhysicsVelocity>(body).Linear;
            Assert.AreEqual(homes,velocity.z>0); Assert.AreEqual(3,velocity.y); Assert.AreEqual(10,math.length(velocity.xz),.001f);
        }

        [Test] public void Roll004_LethalRollingDamageDefersDefeatUntilBossOwnedFlightEnds()
        {
            t.CrowdDamage=1000;
            RollingCollisionSystem.HitCrowd(em,boss,body,new RollingBoss { Direction=math.forward() },t,10);
            Assert.AreEqual(0,em.GetComponentData<Health>(body).Current);
            Assert.AreEqual(EnemyLaunchPhase.Launched,em.GetComponentData<EnemyLaunchState>(body).Phase);
            Assert.AreEqual(1,em.GetComponentData<EnemyDamageState>(body).IsDefeatDeferred);
        }

        [Test] public void Roll004_ExpiredEarlierContactCannotInvalidateSustainedContactOrStealRepunch()
        {
            var history=em.GetBuffer<RollingCrowdContact>(boss);
            history.Add(new RollingCrowdContact { Body=boss,LastSeen=8,Roll=1 });
            history.Add(new RollingCrowdContact { Body=body,LastSeen=9.98,Roll=1 });
            Assert.IsFalse(RollingCollisionSystem.RegisterContact(em,history,body,0,1,10));
            Assert.AreEqual(1,history.Length); Assert.AreEqual(body,history[0].Body);
            Assert.IsFalse(RollingCollisionSystem.RegisterContact(em,history,body,0,1,10.02));
            Assert.IsTrue(RollingCollisionSystem.RegisterContact(em,history,body,0,1,10.2)); // new physical contact
            Assert.IsTrue(RollingCollisionSystem.RegisterContact(em,history,body,1,1,10.22)); // pooled lifetime
            Assert.IsTrue(RollingCollisionSystem.RegisterContact(em,history,body,1,2,10.24)); // next roll
        }
    }
}
