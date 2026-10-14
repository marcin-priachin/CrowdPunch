using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class FinaleTests
    {
        private World world;
        private EntityManager em;
        private Entity boss,source,anchor;
        [SetUp] public void Setup()
        {
            world=new World("Finale rules"); em=world.EntityManager;
            boss=em.CreateEntity(typeof(FinaleBoss),typeof(FinaleTuning),typeof(LocalTransform));
            em.SetComponentData(boss,new FinaleBoss {Health=100,Stage=1});
            em.SetComponentData(boss,new FinaleTuning {Health=100,FallSeconds=2,GroundHeight=3,VictorySeconds=3,ReboundSafetyDegrees=35});
            em.SetComponentData(boss,LocalTransform.Identity); em.AddBuffer<FinaleHit>(boss);
            source=em.CreateEntity(typeof(EnemyLaunchState)); Launch(EnemyLaunchOwner.Player);
            anchor=Anchor();
        }
        [TearDown] public void Cleanup() => world.Dispose();
        private Entity Anchor()
        {
            var e=em.CreateEntity(typeof(Barricade),typeof(FinaleAnchor),typeof(PhysicsCollider),typeof(LocalTransform));
            em.SetComponentData(e,new Barricade {HitsRemaining=2,RequiredHits=2,CompleteOnDestruction=1});
            em.SetComponentData(e,new FinaleAnchor {Boss=boss,Landing=new float3(15,3,18)});
            em.SetComponentData(e,LocalTransform.Identity); em.AddBuffer<BarricadeHitHistory>(e); em.AddBuffer<BarricadeRebound>(e); return e;
        }
        private void Launch(EnemyLaunchOwner owner,uint sequence=1) => em.SetComponentData(source,new EnemyLaunchState {Owner=owner,Phase=EnemyLaunchPhase.Launched,LaunchSequence=sequence});
        private void Phase(FinalePhase phase) {var b=em.GetComponentData<FinaleBoss>(boss);b.Phase=phase;em.SetComponentData(boss,b);}

        [Test] public void Finale002_CompletionCancelsAttackAndRejectsOtherAnchorsInSameFrame()
        {
            var other=Anchor(); var b=em.GetComponentData<FinaleBoss>(boss); b.Attack=FinaleAttack.Sweep; em.SetComponentData(boss,b);
            Assert.That(BarricadeHitResolution.TryHit(em,anchor,source,1,0,default),Is.True);
            Assert.That(BarricadeHitResolution.TryHit(em,anchor,source,1,0,default),Is.False);
            Launch(EnemyLaunchOwner.Player,2);
            Assert.That(BarricadeHitResolution.TryHit(em,anchor,source,2,0,default),Is.True);
            b=em.GetComponentData<FinaleBoss>(boss);
            Assert.That(b.Phase,Is.EqualTo(FinalePhase.Falling)); Assert.That(b.Attack,Is.EqualTo(FinaleAttack.Landing));
            Assert.That(b.SpentCount,Is.EqualTo(1));
            Assert.That(BarricadeHitResolution.TryHit(em,other,source,2,0,default),Is.False);
            Assert.That(em.GetComponentData<Barricade>(other).HitsRemaining,Is.EqualTo(2));
            Phase(FinalePhase.Flying);
            Assert.That(FinaleHitResolution.Active(em,anchor),Is.False,"Spent remains unavailable on next flight");
            Assert.That(FinaleHitResolution.Active(em,other),Is.True);
        }

        [TestCase(EnemyLaunchOwner.Boss)] [TestCase(EnemyLaunchOwner.Enemy)] [TestCase(EnemyLaunchOwner.None)]
        public void Finale004_RejectsNonPlayerBodyAndBlast(EnemyLaunchOwner owner)
        {
            Launch(owner); Assert.That(BarricadeHitResolution.TryHit(em,anchor,source,1,0,default),Is.False);
            em.AddComponentData(anchor,new ShellTarget {RequiredExplosions=3,ExplosionsRemaining=3,CoreHealth=100,CoreMaxHealth=100});
            ShellHitResolution.Explosion(em,anchor,30,0,default,source);
            Assert.That(em.GetComponentData<ShellTarget>(anchor).ExplosionsRemaining,Is.EqualTo(3));
        }

        [Test] public void Finale004_ShellBlastDoesNotCrossPhaseAndDirectCorePunchCompletes()
        {
            em.AddComponentData(anchor,new ShellTarget {RequiredExplosions=1,ExplosionsRemaining=1,CoreHealth=30,CoreMaxHealth=30});
            ShellHitResolution.Punch(em,anchor,100,0,default);
            Assert.That(em.GetComponentData<ShellTarget>(anchor).CoreHealth,Is.EqualTo(30));
            ShellHitResolution.Explosion(em,anchor,100,0,default,source);
            Assert.That(em.GetComponentData<ShellTarget>(anchor).CoreHealth,Is.EqualTo(30));
            Assert.That(em.GetComponentData<FinaleBoss>(boss).Phase,Is.EqualTo(FinalePhase.Flying));
            Phase(FinalePhase.Downed); ShellHitResolution.Punch(em,anchor,100,0,default);
            Assert.That(em.GetComponentData<ShellTarget>(anchor).CoreHealth,Is.EqualTo(30));
            Phase(FinalePhase.Flying); ShellHitResolution.Punch(em,anchor,30,0,default);
            Assert.That(em.GetComponentData<FinaleBoss>(boss).Phase,Is.EqualTo(FinalePhase.Falling));
        }

        [Test] public void Finale005_BossWindowOwnershipAndIndependentImpactExplosionIdentity()
        {
            FinaleHitResolution.Damage(em,boss,source,20,false,0);
            Assert.That(em.GetComponentData<FinaleBoss>(boss).Health,Is.EqualTo(100));
            Phase(FinalePhase.Downed);
            FinaleHitResolution.Damage(em,boss,source,20,false,0); FinaleHitResolution.Damage(em,boss,source,20,false,0);
            FinaleHitResolution.Damage(em,boss,source,15,true,0); FinaleHitResolution.Damage(em,boss,source,15,true,0);
            Assert.That(em.GetComponentData<FinaleBoss>(boss).Health,Is.EqualTo(65));
            Launch(EnemyLaunchOwner.Boss,2); FinaleHitResolution.Damage(em,boss,source,50,false,0);
            Assert.That(em.GetComponentData<FinaleBoss>(boss).Health,Is.EqualTo(65));
            Launch(EnemyLaunchOwner.Player,3); FinaleHitResolution.Damage(em,boss,source,10,false,0);
            Assert.That(em.GetComponentData<FinaleBoss>(boss).Health,Is.EqualTo(55));
            Phase(FinalePhase.TakingOff); FinaleHitResolution.Damage(em,boss,source,30,true,0);
            Assert.That(em.GetComponentData<FinaleBoss>(boss).Health,Is.EqualTo(55));
        }

        [Test] public void Finale005_ReboundAvoidsPlayerAndRetainsLaunch()
        {
            em.AddComponentData(boss,new Barricade {HitsRemaining=1,ReboundMultiplier=1});
            em.AddBuffer<BarricadeRebound>(boss).Add(new BarricadeRebound {Source=source,LaunchSequence=1,Normal=new float3(0,0,-1),IncomingVelocity=new float3(0,0,30)});
            em.AddComponentData(source,LocalTransform.Identity); em.AddComponentData(source,new PhysicsVelocity());
            var p=em.CreateEntity(typeof(PlayerSnapshot)); em.SetComponentData(p,new PlayerSnapshot {IsAvailable=true,Position=new float3(0,0,-10)});
            world.GetOrCreateSystem<BarricadeReboundSystem>().Update(world.Unmanaged);
            var v=em.GetComponentData<PhysicsVelocity>(source).Linear;
            Assert.That(math.length(v.xz),Is.EqualTo(30).Within(.001));
            Assert.That(math.dot(math.normalize(v.xz),new float2(0,-1)),Is.LessThan(math.cos(math.radians(35))));
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).Phase,Is.EqualTo(EnemyLaunchPhase.Launched));
        }

        [Test] public void Finale007_CompletionIgnoresRemainingAnchorsAndWaitsOnlyVictoryPresentation()
        {
            var system=world.GetOrCreateSystemManaged<GauntletCompletionSystem>(); uint before=GauntletCompletionRegistry.Sequence;
            Phase(FinalePhase.Downed); FinaleHitResolution.Damage(em,boss,source,1000,false,0);
            system.Update(); Assert.That(GauntletCompletionRegistry.Sequence,Is.EqualTo(before));
            var b=em.GetComponentData<FinaleBoss>(boss); b.Remaining=0; em.SetComponentData(boss,b);
            system.Update(); Assert.That(GauntletCompletionRegistry.Sequence,Is.EqualTo(before+1));
        }

        [Test] public void Finale002_RailPausesInMotionAndDownsOnlyOnPhysicalDocking()
        {
            em.AddComponentData(anchor,new TrackObject {Start=float3.zero,Direction=new float3(0,0,1),Length=8,RequiredNetHits=4,SlideDuration=1,PlayerBodiesOnly=1});
            em.AddComponentData(anchor,new TrackObjectState {TargetStep=4,Distance=6,NextDistance=6,Moving=1,Elapsed=.5f,SlideStart=4});
            em.AddComponentData(anchor,new PhysicsVelocity()); em.AddBuffer<TrackPushHistory>(anchor); em.AddBuffer<TrackPushContact>(anchor);
            Phase(FinalePhase.Downed); world.SetTime(new Unity.Core.TimeData(1,.1f));
            world.GetOrCreateSystem<TrackObjectMotionSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<TrackObjectArrivalSystem>().Update(world.Unmanaged);
            var motion=em.GetComponentData<TrackObjectState>(anchor);
            Assert.That(motion.Distance,Is.EqualTo(6)); Assert.That(motion.Elapsed,Is.EqualTo(.5f));
            Phase(FinalePhase.Flying);
            world.GetOrCreateSystem<TrackObjectArrivalSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<FinaleBoss>(boss).Phase,Is.EqualTo(FinalePhase.Flying),"Selecting the end is insufficient");
            motion.Elapsed=1; motion.NextDistance=8; em.SetComponentData(anchor,motion);
            world.GetOrCreateSystem<TrackObjectArrivalSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<TrackObjectState>(anchor).Locked,Is.EqualTo(1));
            Assert.That(em.GetComponentData<FinaleBoss>(boss).Phase,Is.EqualTo(FinalePhase.Falling));
        }

        [Test] public void Finale001_PreFinaleSaveUnlocksOneLevelChapterWithoutChangingOldCompletions()
        {
            string path=Path.GetFullPath("Temp/FinaleTests/"+Guid.NewGuid()+".json");
            try {
                var c=AssetDatabase.LoadAssetAtPath<CampaignCatalog>("Assets/CrowdPunch/Data/Campaign/Campaign.asset");
                var progress=new CampaignProgress(path);
                for(int i=0;i<80;i++) progress.Complete($"cp-{i+1:000}");
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(c),Is.EqualTo(80)); Assert.That(progress.IsUnlocked(c,80),Is.True);
                Assert.That(c.ChapterStart(8),Is.EqualTo(80)); Assert.That(c.ChapterEnd(8),Is.EqualTo(81));
                Assert.That(c.levels.Take(80).Select(l=>l.id),Is.EqualTo(Enumerable.Range(1,80).Select(i=>$"cp-{i:000}")));
                progress.Complete("cp-081"); Assert.That(new CampaignProgress(path).NextUnfinished(c),Is.EqualTo(81));
            }
            finally { foreach(string suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test] public void Finale006_AuthoredSceneHasFourTypesAndBoundedStageSupply()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try {
                EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Campaign/Campaign_81/Campaign_81 Sub Scene.unity");
                var anchors=UnityEngine.Object.FindObjectsByType<FinaleAnchorAuthoring>(FindObjectsSortMode.None);
                Assert.That(anchors.Length,Is.EqualTo(4)); Assert.That(anchors.Count(a=>a.GetComponent<ShellTargetAuthoring>()!=null),Is.EqualTo(1));
                Assert.That(anchors.Count(a=>a.GetComponent<TrackObjectAuthoring>()!=null),Is.EqualTo(1));
                Assert.That(anchors.Count(a=>a.GetComponent<BarricadeAuthoring>().cover!=null),Is.EqualTo(1));
                var supplies=UnityEngine.Object.FindObjectsByType<EnemyWaveSequenceAuthoring>(FindObjectsSortMode.None);
                Assert.That(supplies.Length,Is.EqualTo(8)); Assert.That(supplies.All(a=>a.finaleBoss!=null && a.barricade==null),Is.True);
                Assert.That(supplies.All(a=>a.Waves.Count==1 && a.Waves[0].ReplenishWhileBossLives),Is.True);
                Assert.That(supplies.Count(a=>a.GetComponent<FinaleSupplyAuthoring>().localAmmunition),Is.EqualTo(4));
                Assert.That(supplies.Sum(a=>CampaignEnemyRevisionTests.PhysicalPopulation(a.Waves[0])),Is.LessThanOrEqualTo(80));
            } finally {
                if(setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
