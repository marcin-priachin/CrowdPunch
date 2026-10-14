using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // Explicit injected-action check of the baked encounter, not a human combat/balance test.
    [InitializeOnLoad]
    public static class FinalePlayCheck
    {
        private const string Key="CrowdPunch.FinaleCheck", Output="Temp/FinaleValidation/playcheck.txt";
        private static int step, mask, count;
        private static double since, started;
        private static float health;
        private static Entity projectile;
        private static int lastFrame;
        private static readonly List<float> frames=new();
        static FinalePlayCheck() { since=EditorApplication.timeSinceStartup; EditorApplication.update+=Tick; }
        public static void Start()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit play first.");
            Directory.CreateDirectory("Temp/FinaleValidation"); File.WriteAllText(Output,"Controlled baked-world probe; injected anchors, launches and lethal damage; health restored except death/retry test. Not balance evidence.\n");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CampaignBuildRegistration.BootstrapPath);
            SessionState.SetString(GauntletSequence.EditorStartLevelKey,"Assets/CrowdPunch/Scenes/Campaign/Campaign_81.unity");
            SessionState.SetBool(GauntletSequence.EditorLegacyKey,false); SessionState.SetBool(Key,true);
            step=mask=count=0; frames.Clear(); since=started=EditorApplication.timeSinceStartup;
            EditorApplication.isPaused=false; EditorApplication.isPlaying=true;
        }
        public static void Stop() => SessionState.SetBool(Key,false);
        private static void Record(string s) => File.AppendAllText(Output,s+"\n");
        private static void Require(bool b,string s) {if(!b) throw new InvalidOperationException(s);}
        private static void Next(int s) {step=s; since=EditorApplication.timeSinceStartup;}
        private static void Tick()
        {
            if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try { Inspect(); } catch(Exception ex) {Record("FAIL step "+step+": "+ex);Stop();EditorApplication.isPaused=true;Debug.LogException(ex);}
        }
        private static void Inspect()
        {
            Application.runInBackground=true;
            Require(EditorApplication.timeSinceStartup-since<100,"Step timeout");
            var flow=Object.FindFirstObjectByType<GauntletSequence>(); var world=World.DefaultGameObjectInjectionWorld;
            if(flow==null || world==null || !world.IsCreated || flow.TransitionInProgress) return;
            var player=Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if(step!=7 && player!=null) player.Restore(player.MaxHealth);
            var em=world.EntityManager; em.CompleteAllTrackedJobs();
            using var q=em.CreateEntityQuery(typeof(FinaleBoss)); if(q.CalculateEntityCount()!=1) return;
            Entity boss=q.GetSingletonEntity(); var b=em.GetComponentData<FinaleBoss>(boss); var t=em.GetComponentData<FinaleTuning>(boss);
            using var aq=em.CreateEntityQuery(typeof(FinaleAnchor)); using var anchors=aq.ToEntityArray(Allocator.Temp);
            using var eq=em.CreateEntityQuery(new EntityQueryDesc {All=new[]{ComponentType.ReadOnly<Enemy>(),ComponentType.ReadOnly<EnemyLaunchState>()},Options=EntityQueryOptions.IncludeDisabledEntities});
            using var enemies=eq.ToEntityArray(Allocator.Temp);
            mask|=1<<(int)b.Attack;
            if(step==0) {
                Require(flow.EditorPreview && flow.CurrentLevelIndex==80,"Level Play preview did not select 81");
                Require(anchors.Length==4,"Four baked anchors required");
                if(enemies.Length<35) return;
                Record("PASS level 81 baked, four anchors, all initial supply instantiated: "+enemies.Length+" physical bodies.");
                frames.Clear(); mask=0; Next(1);
            }
            else if(step==1) {
                SampleFrame();
                if((mask & (1<<(int)FinaleAttack.Strike))==0 || (mask & (1<<(int)FinaleAttack.Sweep))==0) return;
                Record("PASS flight advances with both strike and committed sweep warnings.");
                var narrow=anchors.First(e=>!em.HasComponent<ShellTarget>(e)&&!em.HasComponent<TrackObject>(e)&&em.GetComponentData<Barricade>(e).Cover==Entity.Null);
                projectile=enemies.First(e=>em.HasComponent<EnemyArchetype>(e)&&em.GetComponentData<EnemyArchetype>(e).Value==EnemyArchetypeKind.Baseline&&!em.HasComponent<SnakeSegment>(e)&&!em.IsComponentEnabled<RespawnRequest>(e));
                for(uint i=1;i<=4;i++) {Launch(em,projectile,i);BarricadeHitResolution.TryHit(em,narrow,projectile,i,world.Time.ElapsedTime,default);}
                Require(em.GetComponentData<FinaleBoss>(boss).Phase==FinalePhase.Falling,"Anchor did not initiate fall");
                Require(em.GetComponentData<FinaleBoss>(boss).Attack==FinaleAttack.Landing,"Airborne warning not cancelled"); Next(2);
            }
            else if(step==2 && b.Phase==FinalePhase.Downed) {
                Require(b.Remaining>t.DownedSeconds-.5f,"Vulnerability did not start on landing");
                Require(b.Attack==FinaleAttack.None,"Initial stun missing");
                var p=em.GetComponentData<LocalTransform>(boss).Position;
                foreach(var e in enemies) if(!em.HasComponent<SnakeSegment>(e) && e!=projectile) {
                    var pose=em.GetComponentData<LocalTransform>(e); pose.Position=new float3(-35+(count++%14)*5,.05f,25+(count/14)*3); em.SetComponentData(e,pose);
                }
                player.transform.position=new Vector3(p.x,.5f,p.z-12);
                var bodyPose=em.GetComponentData<LocalTransform>(projectile); bodyPose.Position=new float3(p.x,.05f,p.z-7);
                em.SetComponentData(projectile,bodyPose); em.SetComponentEnabled<RespawnRequest>(projectile,false);
                Launch(em,projectile,100); em.SetComponentData(projectile,new PhysicsVelocity {Linear=new float3(0,0,30)});
                // Keep the probe nonlethal even when the saved boss health has been tuned down.
                var shot=em.GetComponentData<EnemyLaunchState>(projectile);
                shot.LaunchDamage=math.min(shot.LaunchDamage,t.Health*.01f); em.SetComponentData(projectile,shot);
                health=b.Health; Record("PASS fall/landing and fixed initial stun; injected physical body shot."); Next(3);
            }
            else if(step==3 && EditorApplication.timeSinceStartup-since>.5) {
                Require(b.Health<health,"Physical launched-body impact did not damage boss");
                Require(em.GetComponentData<LocalTransform>(boss).Position.Equals(b.Landing),"Boss translated from impact");
                Record("PASS physical swept impact damages stationary boss: "+health+" -> "+b.Health);
                health=b.Health; mask=0; Next(4);
            }
            else if(step==4) {
                if((mask & (1<<(int)FinaleAttack.Slam))==0 || (mask & (1<<(int)FinaleAttack.Shockwave))==0) return;
                Record("PASS grounded initial stun followed by alternating slam and shockwave warnings.");
                b.Remaining=.02f; em.SetComponentData(boss,b); Next(5);
            }
            else if(step==5 && b.Phase==FinalePhase.Flying) {
                Require(anchors.Count(e=>em.GetComponentData<FinaleAnchor>(e).Spent!=0)==1,"Spent anchor reset too early");
                Record("PASS harmless takeoff and resumed flight preserve spent anchor.");
                // Exercise the remaining three cycles quickly through their completion handoff.
                foreach(var e in anchors) if(em.GetComponentData<FinaleAnchor>(e).Spent==0) {
                    b=em.GetComponentData<FinaleBoss>(boss); b.Phase=FinalePhase.Flying; em.SetComponentData(boss,b);
                    var wall=em.GetComponentData<Barricade>(e); wall.HitsRemaining=0; em.SetComponentData(e,wall);
                    FinaleHitResolution.CompleteAnchor(em,e);
                }
                b=em.GetComponentData<FinaleBoss>(boss); b.Phase=FinalePhase.TakingOff; b.Remaining=.01f; em.SetComponentData(boss,b); Next(6);
            }
            else if(step==6 && b.Phase==FinalePhase.Flying) {
                Require(b.SpentCount==0 && anchors.All(e=>em.GetComponentData<FinaleAnchor>(e).Spent==0),"Set did not reset after fourth use");
                foreach(var e in anchors) {
                    var w=em.GetComponentData<Barricade>(e); Require(w.HitsRemaining==w.RequiredHits,"Anchor durability not restored");
                    if(em.HasComponent<ShellTarget>(e)) Require(em.GetComponentData<ShellTarget>(e).ExplosionsRemaining==3,"Shell did not reset");
                    if(em.HasComponent<TrackObject>(e)) Require(em.GetComponentData<TrackObjectState>(e).TargetStep==0,"Rail did not reset");
                }
                Record("PASS fourth-use reset restores all four installations."); player.ApplyDamage(player.MaxHealth*2); Next(7);
            }
            else if(step==7 && flow.RunFailed) {flow.RestartCurrentLevel(); Next(8);}
            else if(step==8 && !flow.RunFailed && b.Health==t.Health && enemies.Length>=35 && EditorApplication.timeSinceStartup-since>2) {
                Require(b.Phase==FinalePhase.Flying && b.SpentCount==0 && b.Stage==1,"Retry state wrong");
                Require(player.CurrentHealth==player.MaxHealth,"Retry health wrong");
                Record("PASS player-death retry restores full player/boss health, flight, anchors and supply.");
                b.Health=t.Health*.5f; em.SetComponentData(boss,b); frames.Clear(); Next(9);
            }
            else if(step==9) {
                SampleFrame();
                if(EditorApplication.timeSinceStartup-since<12) return;
                Require(b.Stage==2,"Second health stage not applied");
                Require(enemies.Count(e=>!em.HasComponent<SnakeSegment>(e)&&!em.HasComponent<LinkedPairEndpoint>(e))==42
                    && enemies.Count(e=>em.HasComponent<LinkedPairEndpoint>(e))<=2,"Stage 2 slot cap must be 44 including the recyclable pair");
                Record("PASS stage 2 replenishing slot population: 44 roots, up to 50 physical bodies.");
                b.Health=t.Health*.2f; em.SetComponentData(boss,b); frames.Clear(); Next(11);
            }
            else if(step==11) {
                SampleFrame();
                if(EditorApplication.timeSinceStartup-since<12) return;
                Require(b.Stage==3,"Third health stage not applied");
                Require(enemies.Count(e=>!em.HasComponent<SnakeSegment>(e)&&!em.HasComponent<LinkedPairEndpoint>(e))==58
                    && enemies.Count(e=>em.HasComponent<LinkedPairEndpoint>(e))<=2,"Stage 3 slot cap must remain 60 including the recyclable pair");
                Record("Stage 3 population: "+enemies.Length+"; editor frame samples "+frames.Count+", mean ms "+frames.Average().ToString("F2")+", p95 ms "+frames.OrderBy(x=>x).ElementAt((int)(frames.Count*.95f)).ToString("F2"));
                b.Phase=FinalePhase.Downed; em.SetComponentData(boss,b);
                projectile=enemies.First(e=>!em.HasComponent<SnakeSegment>(e)); Launch(em,projectile,999);
                FinaleHitResolution.Damage(em,boss,projectile,999999,false,world.Time.ElapsedTime); Next(10);
            }
            else if(step==10 && flow.Screen==CampaignScreen.CampaignComplete) {
                Require(enemies.Length==0,"Survivors were not neutralized");
                Require(b.Attack==FinaleAttack.None,"Attack survived victory");
                Record("PASS immediate combat/supply stop, survivor cleanup and campaign completion without anchor/survivor clearance.");
                Record("PASS controlled finale probe complete. Human duration, readability and difficulty playtest remains."); Stop();
            }
        }
        private static void Launch(EntityManager em,Entity e,uint sequence) => em.SetComponentData(e,new EnemyLaunchState {
            Phase=EnemyLaunchPhase.Launched,Owner=EnemyLaunchOwner.Player,LastCause=EnemyLaunchCause.PlayerPunch,LaunchSequence=sequence,LaunchDamage=100 });
        private static void SampleFrame() {
            if(lastFrame==Time.frameCount) return;
            lastFrame=Time.frameCount;
            if(Time.unscaledDeltaTime>0 && Time.unscaledDeltaTime<.5f) frames.Add(Time.unscaledDeltaTime*1000);
        }
    }
}
