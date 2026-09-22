using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class BossEncounterTests
    {
        private World world;
        private EntityManager em;
        private Entity head,left,right,source;
        private BossTuning tuning;

        [SetUp] public void SetUp()
        {
            world=new World("Boss encounter regression"); em=world.EntityManager;
            var asset=ScriptableObject.CreateInstance<BossEncounterSettings>(); tuning=asset.Bake(); Object.DestroyImmediate(asset);
            tuning.Health=240; // Fixed fixture health; production balance remains independently tunable.
            head=Part(BossPartKind.Head); left=Part(BossPartKind.LeftHand); right=Part(BossPartKind.RightHand);
            foreach(var e in new[]{head,left,right}) { var p=em.GetComponentData<BossPart>(e); p.Encounter=head; em.SetComponentData(e,p); }
            em.AddComponentData(head,tuning);
            em.AddComponentData(head,new BossEncounter { LeftHand=left,RightHand=right,Stage=1,Cycle=BossCycle.Opening });
            em.AddComponentData(head,new Health { Current=240,Max=240 });
            source=em.CreateEntity(typeof(Enemy),typeof(EnemyTier),typeof(EnemyLaunchState),typeof(Health),typeof(LocalTransform),typeof(PhysicsVelocity),typeof(ExternalImpulse),typeof(DamageRequest),typeof(RespawnRequest));
            em.SetComponentEnabled<RespawnRequest>(source,false);
            em.SetComponentData(source,new Health { Current=100,Max=100 });
            em.SetComponentData(source,LocalTransform.FromPosition(new float3(0,0,1)));
            Launch(EnemyLaunchCause.PlayerPunch);
        }
        [TearDown] public void TearDown() { world.Dispose(); }
        private Entity Part(BossPartKind kind)
        {
            var e=em.CreateEntity(typeof(BossPart),typeof(BossImpactFeedback),typeof(BossMotionTarget),typeof(PhysicsVelocity),typeof(LocalTransform));
            em.SetComponentData(e,new BossPart { Kind=kind,InitialRotation=quaternion.identity });
            em.SetComponentData(e,LocalTransform.Identity); em.AddBuffer<CollisionDamageHistory>(e);
            if(kind!=BossPartKind.Head) { em.AddComponentData(e,new BossHand { Phase=BossHandPhase.Active }); em.AddBuffer<BossScatterHistory>(e); }
            return e;
        }
        private void Launch(EnemyLaunchCause cause)
        { var l=em.GetComponentData<EnemyLaunchState>(source); EnemyLaunchTransition.Begin(ref l,cause,10); em.SetComponentData(source,l); }
        private bool Hit(Entity part,double now=10,float damage=10) => BossImpactResolution.TryHit(em,source,part,10,damage,now,float3.zero,math.forward());

        [TestCase(EnemyLaunchCause.ElitePunch)]
        [TestCase(EnemyLaunchCause.BossAttack)]
        [TestCase(EnemyLaunchCause.Explosion)]
        public void Boss001_NonPlayerBodiesAndDescendantsCannotDamageHead(EnemyLaunchCause cause)
        {
            Launch(cause); Assert.IsFalse(Hit(head));
            var original=em.GetComponentData<EnemyLaunchState>(source); var descendant=default(EnemyLaunchState);
            EnemyLaunchTransition.Begin(ref descendant,EnemyLaunchCause.EnemyCollision,10,original.Owner);
            em.SetComponentData(source,descendant); Assert.IsFalse(Hit(head)); Assert.AreEqual(240,em.GetComponentData<Health>(head).Current);
        }
        [Test] public void Boss001_PlayerPropagationRemainsEligible()
        {
            var l=default(EnemyLaunchState); EnemyLaunchTransition.Begin(ref l,EnemyLaunchCause.EnemyCollision,10,EnemyLaunchOwner.Player);
            em.SetComponentData(source,l); Assert.IsTrue(Hit(head));
        }
        [Test] public void Boss001_ExplosiveBodyContactRequestsOrdinaryDetonationOnlyWhileLaunched()
        {
            em.AddComponentData(source,new ExplosiveEnemyState());
            em.AddComponent<ExplosiveDetonationRequest>(source);
            em.SetComponentEnabled<ExplosiveDetonationRequest>(source,false);
            BossCollisionSystem.RequestExplosiveContact(em,source);
            Assert.IsTrue(em.IsComponentEnabled<ExplosiveDetonationRequest>(source));
            em.SetComponentEnabled<ExplosiveDetonationRequest>(source,false);
            em.SetComponentData(source,new ExplosiveEnemyState { HasExploded=1 });
            BossCollisionSystem.RequestExplosiveContact(em,source);
            Assert.IsFalse(em.IsComponentEnabled<ExplosiveDetonationRequest>(source));
            em.SetComponentData(source,new ExplosiveEnemyState());
            em.SetComponentData(source,default(EnemyLaunchState));
            BossCollisionSystem.RequestExplosiveContact(em,source);
            Assert.IsFalse(em.IsComponentEnabled<ExplosiveDetonationRequest>(source));
            Assert.AreEqual(240,em.GetComponentData<Health>(head).Current);
        }
        [Test] public void Boss003_RealFreshPunchReclaimsBossBodyAndReplacesMomentum()
        {
            Launch(EnemyLaunchCause.BossAttack); em.SetComponentData(source,new PhysicsVelocity { Linear=new float3(90,0,-90) });
            Assert.IsTrue(PunchResolution.TryApply(em,source,new PunchSpecification { Origin=float3.zero,Direction=math.forward(),Range=3,Radius=2,
                Cause=EnemyLaunchCause.PlayerPunch,AffectLaunched=1,Strength=90,Damage=10 }));
            Assert.AreEqual(EnemyLaunchOwner.Player,em.GetComponentData<EnemyLaunchState>(source).Owner);
            Assert.AreEqual(float3.zero,em.GetComponentData<PhysicsVelocity>(source).Linear); Assert.IsTrue(Hit(head));
        }
        [TestCase(BossPartKind.Head)] [TestCase(BossPartKind.LeftHand)]
        public void Boss002_PlayerPunchCannotAffectParts(BossPartKind kind)
        {
            var p=kind==BossPartKind.Head?head:left;
            Assert.IsFalse(PunchResolution.TryApply(em,p,new PunchSpecification { Direction=math.forward(),Range=100,Radius=100,
                AffectActive=1,AffectLaunched=1,AffectRecovering=1,ApplyDamage=1,Damage=9999,Cause=EnemyLaunchCause.PlayerPunch }));
            Assert.IsFalse(em.HasComponent<EnemyLaunchState>(p)); Assert.IsFalse(em.HasComponent<ExternalImpulse>(p));
        }
        [Test] public void Boss001_InvulnerabilityAndDuplicateContactsNeverBecomeDelayedDamage()
        {
            Assert.IsTrue(Hit(head)); Assert.IsFalse(Hit(head,20));
            Launch(EnemyLaunchCause.PlayerPunch); Assert.IsFalse(Hit(head,10.2)); Assert.IsFalse(Hit(head,30));
            Launch(EnemyLaunchCause.PlayerPunch); Assert.IsTrue(Hit(head,30));
            Assert.AreEqual(220,em.GetComponentData<Health>(head).Current);
        }
        [TestCase(EnemyLaunchOwner.Player,true)] [TestCase(EnemyLaunchOwner.Boss,false)] [TestCase(EnemyLaunchOwner.Enemy,false)]
        public void Boss001_DasherUsesSameEligibilityAndInvulnerability(EnemyLaunchOwner owner,bool expected)
        {
            var l=em.GetComponentData<EnemyLaunchState>(source); l.Owner=owner; em.SetComponentData(source,l);
            em.AddComponentData(source,new DasherSettings { BossDamage=12 });
            Assert.AreEqual(expected,DasherEnemyImpactSystem.ResolveBossContact(em,source,head,10,2,10,float3.zero,math.forward()));
            Assert.IsFalse(DasherEnemyImpactSystem.ResolveBossContact(em,source,head,10,2,20,float3.zero,math.forward()));
            Assert.AreEqual(expected?228:240,em.GetComponentData<Health>(head).Current);
        }
        [Test] public void Boss002_StaggerInterruptsActiveStrikeAndHasRepeatProtection()
        {
            Assert.IsTrue(Hit(left)); Assert.AreEqual(BossHandPhase.Staggered,em.GetComponentData<BossHand>(left).Phase);
            Assert.IsFalse(em.HasComponent<Health>(left)); Assert.IsFalse(em.HasComponent<DeathRequest>(left));
            Launch(EnemyLaunchCause.PlayerPunch); Assert.IsFalse(Hit(left,10.2));
            Launch(EnemyLaunchCause.PlayerPunch); Assert.IsTrue(Hit(left,20));
        }
        [Test] public void Boss004_BurstStopsAtEachThresholdAndCancelsBothHitboxes()
        {
            Assert.IsTrue(Hit(head,10,99999)); var b=em.GetComponentData<BossEncounter>(head);
            Assert.AreEqual(2,b.Stage); Assert.AreEqual(1,b.TransitionCount); Assert.AreEqual(BossCycle.Transition,b.Cycle);
            Assert.That(em.GetComponentData<Health>(head).Current,Is.EqualTo(160).Within(.001f));
            Assert.AreEqual(BossHandPhase.Returning,em.GetComponentData<BossHand>(left).Phase);
            Assert.AreEqual(BossHandPhase.Returning,em.GetComponentData<BossHand>(right).Phase);
            Launch(EnemyLaunchCause.PlayerPunch); Assert.IsFalse(Hit(head,20,99999));
            b.Cycle=BossCycle.Opening; em.SetComponentData(head,b); Launch(EnemyLaunchCause.PlayerPunch);
            Assert.IsTrue(Hit(head,30,99999)); b=em.GetComponentData<BossEncounter>(head); Assert.AreEqual(3,b.Stage); Assert.AreEqual(2,b.TransitionCount);
            Assert.That(em.GetComponentData<Health>(head).Current,Is.EqualTo(80).Within(.001f));
            b.Cycle=BossCycle.Opening; em.SetComponentData(head,b); Launch(EnemyLaunchCause.PlayerPunch); Assert.IsTrue(Hit(head,40,99999));
            Assert.AreEqual(BossCycle.Defeated,em.GetComponentData<BossEncounter>(head).Cycle);
        }
        [Test] public void Boss006_ReplenishmentStopsWithBossDeathAndDoesNotCreateExtraEntities()
        {
            em.AddComponentData(source,new BossCrowdMember { Encounter=head,ReplenishDelay=4 }); em.AddComponent<EnemyRespawnSettings>(source);
            var s=world.GetOrCreateSystem<BossCrowdReplenishmentSystem>(); s.Update(world.Unmanaged);
            Assert.AreEqual(1,em.GetComponentData<EnemyRespawnSettings>(source).Enabled);
            var b=em.GetComponentData<BossEncounter>(head); b.Cycle=BossCycle.Defeated; em.SetComponentData(head,b); s.Update(world.Unmanaged);
            Assert.AreEqual(0,em.GetComponentData<EnemyRespawnSettings>(source).Enabled);
        }
        [Test] public void Boss007_CrowdCompletionCannotWinAndHeadDefeatReportsOnce()
        {
            em.CreateEntity(typeof(EnemyWaveSequence),typeof(EnemyWaveEncounterComplete));
            var s=world.GetOrCreateSystemManaged<GauntletCompletionSystem>(); uint before=GauntletCompletionRegistry.Sequence;
            s.Update(); Assert.AreEqual(before,GauntletCompletionRegistry.Sequence);
            var b=em.GetComponentData<BossEncounter>(head); b.Cycle=BossCycle.Defeated; em.SetComponentData(head,b);
            s.Update(); s.Update(); Assert.AreEqual(before+1,GauntletCompletionRegistry.Sequence);
            BossEncounterReset.Reset(em); s.Update(); Assert.AreEqual(before+1,GauntletCompletionRegistry.Sequence);
        }
        [Test] public void Boss007_RestartClearsAllBossAndCrowdOwnership()
        {
            em.CreateEntity(typeof(MatchState)); em.AddComponentData(source,new EnemyWaveOwnership());
            var restart=world.GetOrCreateSystemManaged<GameRestartSystem>();
            Hit(head); Hit(left); em.GetBuffer<BossScatterHistory>(left).Add(new BossScatterHistory { Body=source,AttackSequence=3 });
            GameRestartRegistry.RequestRestart(); restart.Update();
            Assert.IsFalse(em.Exists(source)); Assert.AreEqual(240,em.GetComponentData<Health>(head).Current);
            var b=em.GetComponentData<BossEncounter>(head); Assert.AreEqual(1,b.Stage); Assert.AreEqual(0,b.AcceptedHits); Assert.AreEqual(0,b.InvulnerableUntil);
            Assert.AreEqual(0,em.GetBuffer<CollisionDamageHistory>(head).Length); Assert.AreEqual(0,em.GetBuffer<BossScatterHistory>(left).Length);
            Assert.AreEqual(BossHandPhase.Returning,em.GetComponentData<BossHand>(left).Phase);
        }
        [Test] public void Boss005_RoundedPerimeterIsContinuousAndAlwaysInsideBounds()
        {
            float3 previous=BossPerimeterRoute.Position(0,tuning);
            for(float d=.05f;d<400;d+=.05f)
            {
                float3 p=BossPerimeterRoute.Position(d,tuning);
                Assert.LessOrEqual(math.distance(previous,p),.051f); Assert.IsTrue(math.all(math.abs(p.xz-tuning.Center)<tuning.BoundsExtents-2)); previous=p;
            }
        }
        [Test] public void Boss001_ExplosionAndGenericDamageCannotDamageOrLaunchParts()
        {
            em.CreateEntity(typeof(PlayerSnapshot));
            var explosive=em.CreateEntity(typeof(Enemy),typeof(LocalTransform),typeof(ExplosiveEnemySettings),typeof(ExplosiveEnemyState),
                typeof(ExplosiveDetonationRequest),typeof(EnemyLaunchState),typeof(DeathRequest),typeof(DamageRequest),typeof(ExternalImpulse));
            em.SetComponentData(explosive,LocalTransform.Identity);
            em.SetComponentData(explosive,new ExplosiveEnemySettings { Radius=5,Damage=9999,NormalEnemyKnockbackForce=20 });
            world.GetOrCreateSystemManaged<ExplosionResolutionSystem>().Update();
            Assert.AreEqual(240,em.GetComponentData<Health>(head).Current);
            foreach(var part in new[]{head,left,right}) Assert.IsFalse(em.HasComponent<EnemyLaunchState>(part));
            em.AddComponentData(head,new DamageRequest { Amount=99999 });
            world.GetOrCreateSystem<DamageApplicationSystem>().Update(world.Unmanaged);
            Assert.AreEqual(240,em.GetComponentData<Health>(head).Current);
        }
        [Test] public void Boss001_RangedProjectileCrossingHeadCannotDamageIt()
        {
            em.CreateEntity(typeof(PlayerSnapshot));
            var shot=em.CreateEntity(typeof(RangedProjectile),typeof(LocalTransform));
            em.SetComponentData(shot,LocalTransform.FromPosition(new float3(-1,0,0)));
            em.SetComponentData(shot,new RangedProjectile { Start=new float3(-1,0,0),Target=new float3(1,0,0),TravelDuration=1,Lifetime=10,MinimumAltitude=-10,Damage=9999 });
            world.SetTime(new TimeData(1,.5f)); world.GetOrCreateSystemManaged<RangedProjectileSystem>().Update();
            Assert.AreEqual(240,em.GetComponentData<Health>(head).Current); Assert.IsFalse(em.HasComponent<EnemyLaunchState>(head));
        }
    }
}
