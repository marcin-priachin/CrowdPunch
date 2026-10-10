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
        private const string Output="Temp/CampaignValidation/lifecycle.txt";
        private static int level=-1,wave=-1,observedWaves,phase;
        private static double at,entered;
        private static bool testedDeath,waitingForRetry;
        private static string savePath;
        private static readonly List<float> frames=new();
        static CampaignLifecycleCheck() { EditorApplication.update+=Tick; }
        public static void Start()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Start in Edit mode.");
            Directory.CreateDirectory("Temp/CampaignValidation");
            File.WriteAllText(Output,"Controlled Editor lifecycle probe: isolated temporary save, injected enemy damage and objective/boss completion. Not a human combat or duration playtest.\n");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CampaignBuildRegistration.BootstrapPath);
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
                Record("PASS main menu; isolated test save installed; normal campaign save untouched.");
                phase=1; flow.ContinueCampaign(); entered=EditorApplication.timeSinceStartup; return;
            }
            var player=Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if(waitingForRetry)
            {
                if(!flow.RunFailed) return;
                Require(flow.NextUnfinished==0,"Death changed campaign progress.");
                waitingForRetry=false; flow.RestartCurrentLevel(); wave=-1;
                Record("PASS player death opens failure; retry requested without progress loss."); return;
            }
            if(player!=null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            if(flow.Screen==CampaignScreen.ChapterComplete)
            {
                Require(observedWaves==19,"Not all 19 wave definitions observed: "+observedWaves);
                Require(flow.CurrentLevelIndex==9 && flow.NextUnfinished==10,"Incorrect chapter milestone.");
                Require(flow.Progress.IsUnlocked(flow.Campaign,10) && !flow.CanSelect(10),"Future chapter availability gate failed.");
                Require(new CampaignProgress(savePath).NextUnfinished(flow.Campaign)==10,"Chapter progress did not survive reload.");
                Record("PASS all ten encounters, 19 waves, chapter completion and saved next-chapter unlock; missing chapter cannot load.");
                phase=2; flow.SelectLevel(0); return;
            }
            if(phase==2 && flow.CurrentLevelIndex==0)
            {
                Require(flow.IsReplay,"Completed level did not enter replay mode.");
                GauntletCompletionRegistry.ReportCompletion(); phase=3; return;
            }
            if(phase==3)
            {
                if(flow.Screen!=CampaignScreen.ReplayComplete) return;
                Require(flow.NextUnfinished==10 && flow.CurrentLevelIndex==0,"Replay reduced progress or auto-advanced.");
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
            var em=world.EntityManager; em.CompleteAllTrackedJobs();
            using var query=em.CreateEntityQuery(typeof(EnemyWaveSequence));
            if(query.CalculateEntityCount()!=1) { Require(now-entered<90,"Expected one loaded encounter sequence."); return; }
            var owner=query.GetSingletonEntity(); var sequence=em.GetComponentData<EnemyWaveSequence>(owner);
            if(level!=flow.CurrentLevelIndex)
            {
                level=flow.CurrentLevelIndex; wave=-1; entered=now;
                Require(player.CurrentHealth==player.MaxHealth,"Level entry did not restore full health.");
                Record($"ENTER {level+1:00}: {flow.GetLevelName(level)}; full health.");
            }
            Require(sequence.Phase!=EnemyWaveRuntimePhase.Invalid,"Invalid baked wave in level "+(level+1));
            Require(now-entered<160,"Encounter stalled in level "+(level+1));
            using var enemiesQuery=em.CreateEntityQuery(typeof(EnemyWaveOwnership),typeof(EnemyLaunchState));
            using var enemies=enemiesQuery.ToEntityArray(Allocator.Temp);
            foreach(var e in enemies)
            {
                var ownership=em.GetComponentData<EnemyWaveOwnership>(e);
                Require(ownership.Sequence==owner && ownership.RunGeneration==sequence.RunGeneration,"Encounter-owned enemy leaked across transition/retry.");
            }
            if(sequence.Phase!=EnemyWaveRuntimePhase.AwaitingActivation) return;
            if(!testedDeath && level==0)
            {
                testedDeath=true; waitingForRetry=true; player.ApplyDamage(player.MaxHealth); return;
            }
            if(wave!=sequence.CurrentWaveIndex)
            {
                wave=sequence.CurrentWaveIndex; at=now; frames.Clear(); observedWaves++;
                var definition=em.GetBuffer<EnemyWaveDefinition>(owner)[wave];
                Require(definition.IsValid==1 && sequence.SpawnedCount==definition.TotalEnemyCount,"Wave did not finish spawning authored population.");
                Record($"WAVE {level+1:00}.{wave+1}: spawned={sequence.SpawnedCount}; owned roots={enemies.Length}");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath($"Temp/CampaignValidation/level{level+1:00}-wave{wave+1}.png"));
            }
            frames.Add(Time.unscaledDeltaTime*1000);
            if(now-at<(level==7?15:4)) return;
            if(level==3 || level==5 || level==6)
            {
                using var walls=em.CreateEntityQuery(typeof(Barricade));
                Require(walls.CalculateEntityCount()==1,"Objective missing.");
                var target=walls.GetSingletonEntity(); var wall=em.GetComponentData<Barricade>(target);
                wall.HitsRemaining=0; em.SetComponentData(target,wall);
                if(wall.CompleteOnDestruction==0)
                    player.GetComponent<PlayerController>().SetLevelEntryPoint(wall.ExitPosition,Quaternion.identity);
            }
            else if(level==9)
            {
                using var bosses=em.CreateEntityQuery(typeof(BossEncounter));
                Require(bosses.CalculateEntityCount()==1,"Boss missing.");
                var head=bosses.GetSingletonEntity(); var state=em.GetComponentData<BossEncounter>(head);
                state.Cycle=BossCycle.Defeated; em.SetComponentData(head,state);
            }
            else
            {
                foreach(var e in enemies)
                {
                    if(em.IsComponentEnabled<RespawnRequest>(e)) continue;
                    em.SetComponentData(e,new DamageRequest{Amount=10000}); em.SetComponentEnabled<DamageRequest>(e,true);
                }
            }
            if(frames.Count>0)
            {
                frames.Sort(); Record($"CLEAR INJECTED {level+1:00}.{wave+1}; editor frame median={frames[frames.Count/2]:0.0}ms; includes tool/editor overhead.");
                frames.Clear();
            }
            at=now+1000;
        }
    }
}
