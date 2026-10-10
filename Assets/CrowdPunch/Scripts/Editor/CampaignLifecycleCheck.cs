using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    /// <summary>Controlled lifecycle probe, with injected defeats explicitly distinguished from playtesting.</summary>
    [InitializeOnLoad]
    public static class CampaignLifecycleCheck
    {
        private const string Key="CrowdPunch.CampaignCheck";
        private static int Chapter => SessionState.GetInt(Key+".Chapter",1);
        private static int FirstLevel => (Chapter-1)*10;
        private static string Output => $"Temp/CampaignValidation/chapter{Chapter}-lifecycle.txt";
        private static int level=-1,wave=-1,observedWaves,phase;
        private static double at,entered;
        private static bool testedDeath,waitingForRetry;
        private static bool testedBreach,waitingForBreachRetry;
        private static int hazardPhases,lastDefenseSpawn=-1;
        private static string savePath;
        private static readonly List<float> frames=new();
        static CampaignLifecycleCheck() { EditorApplication.update+=Tick; }
        public static void Start() => StartChapter(1);
        public static void StartChapterTwo() => StartChapter(2);
        public static void StartChapterThree() => StartChapter(3);
        public static void StartChapterFour() => StartChapter(4);
        public static void StartChapterFive() => StartChapter(5);
        public static void StartChapterSix() => StartChapter(6);
        public static void StartChapterSeven() => StartChapter(7);
        public static void StartChapterEight() => StartChapter(8);
        private static void StartChapter(int chapter)
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Start in Edit mode.");
            SessionState.SetInt(Key+".Chapter",chapter);
            Directory.CreateDirectory("Temp/CampaignValidation");
            File.WriteAllText(Output,"Controlled Editor lifecycle probe: isolated temporary save, injected enemy damage and objective/boss completion. Not a human combat or duration playtest.\n");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CampaignBuildRegistration.BootstrapPath);
            SessionState.EraseString(GauntletSequence.EditorStartLevelKey);
            SessionState.EraseBool(GauntletSequence.EditorLegacyKey);
            SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
        }
        public static void Stop() => SessionState.SetBool(Key,false);
        private static void Record(string value) => File.AppendAllText(Output,value+"\n");
        private static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isPaused) return;
            try { Inspect(); }
            catch(Exception ex) { Record("FAIL "+ex); Stop(); Debug.LogException(ex); }
        }
        private static void Inspect()
        {
            var flow=Object.FindFirstObjectByType<GauntletSequence>();
            var world=World.DefaultGameObjectInjectionWorld;
            if(flow==null||world==null||!world.IsCreated||flow.TransitionInProgress) return;
            if(phase==0)
            {
                Require(flow.HasCampaign && flow.CurrentLevelIndex==-1,"Expected main menu before any encounter.");
                savePath=Path.GetFullPath("Temp/CampaignValidation/progress-"+Guid.NewGuid()+".json");
                typeof(GauntletSequence).GetField("<Progress>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)
                    .SetValue(flow,new CampaignProgress(savePath));
                // Start at the preceding boss to exercise the newly available Continue boundary.
                for(int i=0;i<Math.Max(0,FirstLevel-1);i++) flow.Progress.Complete(flow.Campaign.Get(i).id);
                Record("PASS main menu; isolated test save installed; normal campaign save untouched.");
                phase=1; flow.ContinueCampaign(); entered=EditorApplication.timeSinceStartup; return;
            }
            var player=Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if(waitingForBreachRetry)
            {
                if(!flow.RunFailed) return;
                Require(flow.NextUnfinished==level,"Breach changed campaign progress.");
                waitingForBreachRetry=false; flow.RestartCurrentLevel(); wave=-1;
                Record("PASS first active-enemy breach fails defense; retry resets objective and wave schedule."); return;
            }
            if(waitingForRetry)
            {
                if(!flow.RunFailed) return;
                Require(flow.NextUnfinished==FirstLevel,"Death changed campaign progress.");
                waitingForRetry=false; flow.RestartCurrentLevel(); wave=-1;
                Record("PASS player death opens failure; retry requested without progress loss."); return;
            }
            if(player!=null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            if(flow.Screen==CampaignScreen.ChapterComplete || (Chapter==8 && flow.Screen==CampaignScreen.CampaignComplete))
            {
                if(Chapter>1 && flow.CurrentLevelIndex==FirstLevel-1)
                {
                    Require(flow.NextUnfinished==FirstLevel && flow.CanSelect(FirstLevel),"Previous milestone did not unlock installed chapter.");
                    Record($"PASS previous chapter milestone unlocks Chapter {Chapter}; Continue loads its first level.");
                    flow.ContinueCampaign(); return;
                }
                int next=Chapter*10, expectedWaves=Chapter==1?19:Chapter==2?17:Chapter==3?20:Chapter==4?18:Chapter==5?22:Chapter==8?24:21;
                Require(observedWaves==expectedWaves,"Unexpected observed wave count: "+observedWaves);
                Require(flow.CurrentLevelIndex==next-1 && flow.NextUnfinished==next,"Incorrect chapter milestone.");
                if(next==80) Require(flow.Screen==CampaignScreen.CampaignComplete && !flow.CanSelect(80) && !flow.Progress.IsUnlocked(flow.Campaign,80),"Final campaign completion gate failed.");
                else Require(flow.Progress.IsUnlocked(flow.Campaign,next) && flow.CanSelect(next)==flow.Campaign.Get(next).Available,"Next chapter availability gate failed.");
                Require(new CampaignProgress(savePath).NextUnfinished(flow.Campaign)==next,"Chapter progress did not survive reload.");
                Record($"PASS Chapter {Chapter}, {expectedWaves} observed waves, saved progress and chapter/campaign completion gate.");
                phase=2; flow.SelectLevel(FirstLevel); return;
            }
            if(phase==2 && flow.CurrentLevelIndex==FirstLevel)
            {
                Require(flow.IsReplay,"Completed level did not enter replay mode.");
                GauntletCompletionRegistry.ReportCompletion(); phase=3; return;
            }
            if(phase==3)
            {
                if(flow.Screen!=CampaignScreen.ReplayComplete) return;
                Require(flow.NextUnfinished==Chapter*10 && flow.CurrentLevelIndex==FirstLevel,"Replay reduced progress or auto-advanced.");
                Record("PASS replay completion returns to selection and preserves progress.");
                flow.RestartCurrentLevel(); phase=4; return;
            }
            if(phase==4)
            {
                Require(flow.IsReplay && !flow.RunFailed && player.CurrentHealth==player.MaxHealth,"Replay retry/full-health reset failed.");
                flow.ReturnToMenu(); Record("PASS replay retry restores full health; returned to main menu. COMPLETE."); Stop(); return;
            }
            if(flow.Screen!=CampaignScreen.Playing) return;
            double now=EditorApplication.timeSinceStartup;
            // A completed encounter can exceed the loading timeout; start the next level's
            // clock before waiting for its SubScene, not after the sequence finishes baking.
            if(level!=flow.CurrentLevelIndex)
            {
                level=flow.CurrentLevelIndex; wave=-1; entered=now; hazardPhases=0; testedBreach=false;
                Require(player.CurrentHealth==player.MaxHealth,"Level entry did not restore full health.");
                Record($"ENTER {level+1:00}: {flow.GetLevelName(level)}; full health.");
            }
            var em=world.EntityManager; em.CompleteAllTrackedJobs();
            using var query=em.CreateEntityQuery(typeof(EnemyWaveSequence));
            if(query.CalculateEntityCount()!=1) { Require(now-entered<90,"Expected one loaded encounter sequence."); return; }
            var owner=query.GetSingletonEntity(); var sequence=em.GetComponentData<EnemyWaveSequence>(owner);
            Require(sequence.Phase!=EnemyWaveRuntimePhase.Invalid,"Invalid baked wave in level "+(level+1));
            Require(now-entered<160,"Encounter stalled in level "+(level+1));
            using var enemiesQuery=em.CreateEntityQuery(typeof(EnemyWaveOwnership),typeof(EnemyLaunchState));
            using var enemies=enemiesQuery.ToEntityArray(Allocator.Temp);
            foreach(var e in enemies)
            {
                var ownership=em.GetComponentData<EnemyWaveOwnership>(e);
                Require(ownership.Sequence==owner && ownership.RunGeneration==sequence.RunGeneration,"Encounter-owned enemy leaked across transition/retry.");
            }
            if(Chapter>=3)
            {
                using var hazards=em.CreateEntityQuery(typeof(GroundHazard),typeof(GroundHazardState));
                int expectedHazards=Chapter==3?(level==27?3:level>=24 && level<=26?1:0):Chapter==4?(level==33 || level==36?1:0):Chapter==5?(level==41 || level==47 || level==49?2:level==44?1:0):Chapter==6?(level==49 || level==53 || level==57 || level==59?2:0):Chapter==8?(level==69 || level==72 || level==73 || level==77 || level==79?2:0):(level==59 || level==61 || level==63 || level==66 || level==69?2:level==64?1:level==67?3:0);
                Require(hazards.CalculateEntityCount()==expectedHazards,"Hazard leakage or missing baked patches.");
                using var patches=hazards.ToComponentDataArray<GroundHazardState>(Allocator.Temp);
                foreach(var patch in patches) hazardPhases|=1<<(int)patch.Phase;
                if((level==23 || level==35 || level==46 || level==56 || level==74 || level==76) && (sequence.Phase==EnemyWaveRuntimePhase.Spawning || sequence.Phase==EnemyWaveRuntimePhase.AwaitingActivation))
                {
                    if(!testedBreach && enemies.Length>0)
                    {
                        var e=enemies[0]; var transform=em.GetComponentData<Unity.Transforms.LocalTransform>(e);
                        transform.Position=new Unity.Mathematics.float3(0,.55f,-46); em.SetComponentData(e,transform);
                        var launch=em.GetComponentData<EnemyLaunchState>(e); launch.Phase=EnemyLaunchPhase.Active; em.SetComponentData(e,launch);
                        testedBreach=true; waitingForBreachRetry=true; return;
                    }
                    if(wave!=sequence.CurrentWaveIndex)
                    {
                        wave=sequence.CurrentWaveIndex; observedWaves++; lastDefenseSpawn=0;
                        Record($"WAVE {level+1}.{wave+1}: finite defense, batches of four; enemy defeats injected during advance.");
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath($"Temp/CampaignValidation/level{level+1}-wave{wave+1}.png"));
                    }
                    if(sequence.SpawnedCount!=lastDefenseSpawn)
                    {
                        var definition=em.GetBuffer<EnemyWaveDefinition>(owner)[wave];
                        Require(sequence.SpawnedCount-lastDefenseSpawn==Math.Min(4,definition.TotalEnemyCount+definition.TotalEliteCount-lastDefenseSpawn),"Defense did not spawn batches of four with its final remainder.");
                        lastDefenseSpawn=sequence.SpawnedCount; Record($"DEFENSE BATCH {wave+1}: cumulative={lastDefenseSpawn}");
                    }
                    foreach(var e in enemies) InjectDefeat(em,e);
                    return;
                }
            }
            if(sequence.Phase!=EnemyWaveRuntimePhase.AwaitingActivation) return;
            if(!testedDeath && level==FirstLevel)
            {
                testedDeath=true; waitingForRetry=true; player.ApplyDamage(player.MaxHealth); return;
            }
            if(wave!=sequence.CurrentWaveIndex)
            {
                wave=sequence.CurrentWaveIndex; at=now; frames.Clear(); observedWaves++;
                var definition=em.GetBuffer<EnemyWaveDefinition>(owner)[wave];
                Require(definition.IsValid==1 && sequence.SpawnedCount==definition.TotalEnemyCount+definition.TotalEliteCount,"Wave did not finish spawning authored population.");
                Record($"WAVE {level+1:00}.{wave+1}: spawned={sequence.SpawnedCount}; owned roots={enemies.Length}");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath($"Temp/CampaignValidation/level{level+1:00}-wave{wave+1}.png"));
            }
            frames.Add(Time.unscaledDeltaTime*1000);
            if(now-at<(level==7 || level==17 || level==27 || level==37 || level==47 || level==57 || level==67 || level==77?15:level==26 || level==33 || level==44 || level==49 || level==53 || level==59 || level==61 || level==64 || level==66 || level==69 || level==72 || level==79?9:4)) return;
            if(level==26 || level==27 || level==33 || level==44 || level==49 || level==53 || level==57 || level==59 || level==61 || level==64 || level==66 || level==67 || level==69 || level==72 || level==77 || level==79) Require(hazardPhases==7,"Periodic hazards did not show inactive, warning and active phases.");
            if(level==3 || level==5 || level==6 || level==11 || level==12 || level==16 || level==21 || level==28 || level==33 || level==34 || level==36 || level==41 || level==43 || level==50 || level==52 || level==55 || level==61 || level==63 || level==65 || level==71 || level==73 || level==75)
            {
                using var walls=em.CreateEntityQuery(typeof(Barricade));
                Require(walls.CalculateEntityCount()==1,"Objective missing.");
                var target=walls.GetSingletonEntity(); var wall=em.GetComponentData<Barricade>(target);
                if(level==11 || level==36 || level==50 || level==63 || level==75)
                {
                    var shell=em.GetComponentData<ShellTarget>(target);
                    Require(shell.RequiredExplosions==(level==63?4:3) && shell.CoreHealth>0,"Shell durability was not baked.");
                    shell.ExplosionsRemaining=0; shell.CoreHealth=0; em.SetComponentData(target,shell);
                }
                wall.HitsRemaining=0; em.SetComponentData(target,wall);
                if(wall.CompleteOnDestruction==0)
                    player.GetComponent<PlayerController>().SetLevelEntryPoint(wall.ExitPosition,Quaternion.identity);
            }
            else if(level==9 || level==49)
            {
                using var bosses=em.CreateEntityQuery(typeof(BossEncounter));
                Require(bosses.CalculateEntityCount()==1,"Boss missing.");
                var head=bosses.GetSingletonEntity(); var state=em.GetComponentData<BossEncounter>(head);
                state.Cycle=BossCycle.Defeated; em.SetComponentData(head,state);
            }
            else if(level==15 || level==18 || level==25 || level==31 || level==38 || level==45 || level==53 || level==68)
            {
                using var tracks=em.CreateEntityQuery(typeof(TrackObject),typeof(TrackObjectState));
                Require(tracks.CalculateEntityCount()==1,"Track missing.");
                var target=tracks.GetSingletonEntity(); var track=em.GetComponentData<TrackObject>(target);
                var motion=em.GetComponentData<TrackObjectState>(target);
                Require(track.RequiredNetHits==(level==53?6:5) && motion.Locked==0,"Track was not baked ready to play.");
                CrowdPunch.Systems.Physics.TrackObjectMotion.Retarget(track,ref motion,track.RequiredNetHits);
                em.SetComponentData(target,motion);
                Record("INJECT track destination; actual physics slide/arrival must complete the encounter.");
            }
            else if(level==19 || level==59)
            {
                using var bosses=em.CreateEntityQuery(typeof(ChickenBoss));
                Require(bosses.CalculateEntityCount()==1,"Chicken missing.");
                var boss=bosses.GetSingletonEntity(); var state=em.GetComponentData<ChickenBoss>(boss);
                state.Phase=ChickenPhase.Defeated; em.SetComponentData(boss,state);
            }
            else if(level==29 || level==69)
            {
                using var bosses=em.CreateEntityQuery(typeof(RollingBoss));
                Require(bosses.CalculateEntityCount()==1,"Rolling boss missing.");
                var boss=bosses.GetSingletonEntity(); var state=em.GetComponentData<RollingBoss>(boss);
                state.Phase=RollingPhase.Defeated; em.SetComponentData(boss,state);
            }
            else if(level==39 || level==79)
            {
                using var bosses=em.CreateEntityQuery(typeof(DinoBoss));
                using var pillars=em.CreateEntityQuery(typeof(FallingPillar));
                using var locals=em.CreateEntityQuery(typeof(PillarCrowdMember));
                Require(bosses.CalculateEntityCount()==1 && pillars.CalculateEntityCount()==3,"Dino/pillars missing.");
                Require(locals.CalculateEntityCount()==6,"Expected two local ammunition bodies per pillar.");
                var boss=bosses.GetSingletonEntity(); var state=em.GetComponentData<DinoBoss>(boss);
                state.Phase=DinoPhase.Defeated; em.SetComponentData(boss,state);
            }
            else
            {
                foreach(var e in enemies)
                {
                    InjectDefeat(em,e);
                }
            }
            if(frames.Count>0)
            {
                frames.Sort(); Record($"CLEAR INJECTED {level+1:00}.{wave+1}; editor frame median={frames[frames.Count/2]:0.0}ms; includes tool/editor overhead.");
                frames.Clear();
            }
            at=now+1000;
        }

        private static void InjectDefeat(EntityManager em,Entity enemy)
        {
            if(em.IsComponentEnabled<RespawnRequest>(enemy)) return;
            // This lifecycle probe bypasses armor deliberately; combat/supply tests cover its rules.
            if(em.HasComponent<EnemyArmor>(enemy)) em.SetComponentData(enemy,default(EnemyArmor));
            em.SetComponentData(enemy,new DamageRequest{Amount=10000}); em.SetComponentEnabled<DamageRequest>(enemy,true);
        }
    }
}
