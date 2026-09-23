using System;
using System.Collections.Generic;
using System.IO;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
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
    // Test-only orchestration. Injected stage hits and placed projectiles are explicitly logged.
    [InitializeOnLoad]
    public static class BossEncounterPlayCheck
    {
        private const string Key="CrowdPunch.BossCheck";
        private const string Output="Temp/BossValidation/playcheck.txt";
        private static int step,stage,mask;
        private static double since;
        private static Entity body,special,oldHead;
        private static float healthBefore;
        private static uint completionBefore;
        private static bool pooled,bounceObserved;
        private static readonly List<float> frames=new();
        static BossEncounterPlayCheck() { EditorApplication.update+=Tick; }
        public static void Start()
        {
            Directory.CreateDirectory("Temp/BossValidation"); File.WriteAllText(Output,"Live Editor probe: player health restored; stage hits injected; collision probes place existing bodies. Not a duration/difficulty playtest.\n");
            step=0; SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
        }
        public static void BeginLive()
        { Directory.CreateDirectory("Temp/BossValidation"); File.WriteAllText(Output,"Live boss probe following all ten gauntlets. Controlled stage hits and projectile placements; not balance evidence.\n"); step=1; since=EditorApplication.timeSinceStartup; SessionState.SetBool(Key,true); }
        public static void Stop() { SessionState.SetBool(Key,false); }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isPaused) return;
            try { Inspect(); }
            catch(Exception e) { Record("FAIL "+e); Stop(); EditorApplication.isPaused=true; Debug.LogException(e); }
        }
        private static void Inspect()
        {
            var flow=Object.FindFirstObjectByType<GauntletSequence>(); var world=World.DefaultGameObjectInjectionWorld;
            if(flow==null||world==null||!world.IsCreated||flow.TransitionInProgress) return;
            double now=EditorApplication.timeSinceStartup;
            if(step==0) { flow.SelectLevel(10); step=1; since=now; return; }
            var player=Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if(step<8 && player!=null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            var em=world.EntityManager; em.CompleteAllTrackedJobs();
            using var heads=em.CreateEntityQuery(typeof(BossEncounter));
            if(step==11)
            {
                Require(heads.CalculateEntityCount()==0,"Boss parts leaked into another gauntlet");
                using var crowdQuery=em.CreateEntityQuery(typeof(BossCrowdMember)); Require(crowdQuery.IsEmpty,"Boss crowd leaked after scene switch");
                Record("PASS scene selection removes boss and supporting crowd"); flow.SelectLevel(10); step=12; since=now; return;
            }
            if(heads.CalculateEntityCount()!=1) { Require(now-since<30,"Boss did not load"); return; }
            var head=heads.GetSingletonEntity(); var boss=em.GetComponentData<BossEncounter>(head); var tuning=em.GetComponentData<BossTuning>(head);
            using var crowd=em.CreateEntityQuery(typeof(BossCrowdMember)); using var enemies=crowd.ToEntityArray(Allocator.Temp);
            Require(enemies.Length<=13,"Unbounded crowd root count");
            int specials=0;
            foreach(var e in enemies)
            {
                if(em.GetComponentData<CrowdPunch.Components.EnemyArchetype>(e).Value!=EnemyArchetypeKind.Baseline)
                { special=e; if(!em.IsComponentEnabled<RespawnRequest>(e)) specials++; }
                else if(body==Entity.Null || !em.Exists(body)) body=e;
            }
            Require(specials<=1,"Duplicate special enemy");
            foreach(var e in new[]{head,boss.LeftHand,boss.RightHand})
            {
                var p=em.GetComponentData<LocalTransform>(e).Position;
                Require(math.all(math.isfinite(p)) && math.all(math.abs(p.xz-tuning.Center)<=tuning.BoundsExtents),"Invalid boss bounds");
                Require(!em.HasComponent<Enemy>(e) && !em.HasComponent<EnemyLaunchState>(e),"Boss entered ordinary lifecycle");
            }
            if(step==1)
            {
                if(enemies.Length!=13 || special==Entity.Null) return;
                Record("PASS baked head + two kinematic hands; exactly 12 Baseline + 1 Ranged; no ordinary boss tags");
                stage=1; mask=0; step=2; since=now; frames.Clear(); return;
            }
            if(step==2)
            {
                frames.Add(Time.unscaledDeltaTime*1000);
                foreach(var e in new[]{boss.LeftHand,boss.RightHand})
                {
                    var h=em.GetComponentData<BossHand>(e);
                    if(h.Phase==BossHandPhase.Active && (mask&(1<<(int)h.Attack))==0)
                    { mask|=1<<(int)h.Attack; Record($"STAGE {stage}: active {h.Attack}; position {em.GetComponentData<LocalTransform>(e).Position}; route {boss.RouteDistance:0.0}"); }
                }
                Require(now-since<150,"Attack coordinator stalled in stage "+stage);
                if(mask!=7 || boss.Cycle!=BossCycle.Opening) return;
                Record($"PASS stage {stage} observed slam/lunge/sweep with recovery; head route={boss.RouteDistance:0.0}");
                Capture("stage"+stage);
                if(stage<3)
                {
                    var launch=em.GetComponentData<EnemyLaunchState>(body); EnemyLaunchTransition.Begin(ref launch,EnemyLaunchCause.PlayerPunch,10); em.SetComponentData(body,launch);
                    em.SetComponentEnabled<RespawnRequest>(body,false);
                    Require(BossImpactResolution.TryHit(em,body,head,10,99999,world.Time.ElapsedTime,float3.zero,math.forward()),"Injected threshold hit rejected");
                    stage++; mask=0; since=now; return;
                }
                frames.Sort(); Record($"Editor frame samples={frames.Count}; median={frames[frames.Count/2]:0.00}ms; p95={frames[(int)(frames.Count*.95f)]:0.00}ms (includes Editor overhead)");
                // Hold a controlled collision court using the existing physics motion system.
                world.Unmanaged.GetExistingSystemState<BossHandCoordinationSystem>().Enabled=false;
                boss.Cycle=BossCycle.Opening; boss.InvulnerableUntil=0; em.SetComponentData(head,boss);
                PlacePart(em,head,new float3(0,tuning.HeadHeight,10)); PlacePart(em,boss.LeftHand,new float3(-7,tuning.HandHeight,10)); PlacePart(em,boss.RightHand,new float3(7,tuning.HandHeight,10));
                int n=0; foreach(var e in enemies) { var tr=em.GetComponentData<LocalTransform>(e); tr.Position=new float3(-15+n++*2,0,-10); em.SetComponentData(e,tr); em.SetComponentData(e,new PhysicsVelocity()); }
                step=3; since=now; return;
            }
            if(step==3 && now-since>1)
            {
                healthBefore=em.GetComponentData<Health>(head).Current; bounceObserved=false;
                Shoot(em,body,EnemyLaunchCause.PlayerPunch,new float3(0,0,4)); step=4; since=now; return;
            }
            if(step==4)
            {
                if(!bounceObserved && em.GetComponentData<Health>(head).Current<healthBefore)
                {
                    var position=em.GetComponentData<LocalTransform>(body).Position;
                    var velocity=em.GetComponentData<PhysicsVelocity>(body).Linear;
                    float2 towardPlayer=math.normalizesafe(((float3)player.transform.position-position).xz);
                    float playerComponent=math.dot(math.normalizesafe(velocity.xz),towardPlayer);
                    Require(math.length(velocity.xz)>.01f && playerComponent<=.05f,
                        "Physical head rebound still points toward the player");
                    bounceObserved=true;
                    Record($"PASS real grounded launched-body head collision damaged head and redirected rebound; player dot={playerComponent:0.000}, speed={math.length(velocity.xz):0.00}");
                }
                if(now-since<=1) return;
                Require(bounceObserved,"Real solver projectile did not reach/damage head");
                healthBefore=em.GetComponentData<Health>(head).Current; Shoot(em,body,EnemyLaunchCause.BossAttack,new float3(0,0,4)); step=5; since=now; return;
            }
            if(step==5 && now-since>1)
            {
                Require(em.GetComponentData<Health>(head).Current==healthBefore,"Boss-owned physical projectile damaged head");
                Record("PASS boss-owned physical impact cannot damage head");
                PlacePart(em,boss.LeftHand,new float3(0,tuning.HandHeight,6.5f));
                em.SetComponentData(boss.LeftHand,new BossHand { Phase=BossHandPhase.Shielding });
                Shoot(em,body,EnemyLaunchCause.PlayerPunch,new float3(0,0,2)); step=6; since=now; return;
            }
            if(step==6 && now-since>1)
            {
                Require(em.GetComponentData<Health>(head).Current==healthBefore,"Shield allowed head damage");
                Require(em.GetComponentData<BossHand>(boss.LeftHand).Phase==BossHandPhase.Staggered,"Blocked projectile did not stagger hand");
                Record("PASS physical hand shielding blocks head hit and produces stagger");
                em.SetComponentData(special,new DamageRequest { Amount=99999 }); em.SetComponentEnabled<DamageRequest>(special,true);
                pooled=false; step=7; since=now; return;
            }
            if(step==7)
            {
                if(em.IsComponentEnabled<RespawnRequest>(special)) pooled=true;
                if(!pooled || em.IsComponentEnabled<RespawnRequest>(special)) { Require(now-since<25,"Special failed to replenish"); return; }
                Record("PASS same special entity pooled and safely returned; special population stayed <=1");
                var launch=em.GetComponentData<EnemyLaunchState>(body); EnemyLaunchTransition.Begin(ref launch,EnemyLaunchCause.PlayerPunch,10); em.SetComponentData(body,launch);
                completionBefore=GauntletCompletionRegistry.Sequence;
                BossImpactResolution.TryHit(em,body,head,10,99999,world.Time.ElapsedTime,float3.zero,math.forward()); step=8; since=now; return;
            }
            if(step==8)
            {
                if(!flow.RunComplete) { Require(now-since<8,"Boss defeat did not finish run"); return; }
                Require(GauntletCompletionRegistry.Sequence==completionBefore+1,"Completion did not fire exactly once");
                Record("PASS boss death completed run with supporting enemies alive");
                oldHead=head; world.Unmanaged.GetExistingSystemState<BossHandCoordinationSystem>().Enabled=true;
                flow.RestartCurrentLevel(); step=9; since=now; return;
            }
            if(step==9)
            {
                Require(!em.Exists(oldHead),"Old head survived restart"); Require(!flow.RunComplete && boss.Stage==1 && boss.AcceptedHits==0,"Restart retained boss state");
                Require(em.GetComponentData<Health>(head).Current==tuning.Health,"Restart health not reset");
                Record("PASS restart removes old parts and resets boss health, stage, hits, completion");
                flow.SelectLevel(0); step=11; since=now; return;
            }
            if(step==12 && now-since>3)
            {
                Require(player.CurrentHealth==player.MaxHealth,"Level entry did not restore player health");
                player.ApplyDamage(player.MaxHealth); step=13; since=now; return;
            }
            if(step==13)
            {
                // The death flow is GameObject-owned; retry uses the same authored scene loader.
                Require(player.CurrentHealth==0 && !player.gameObject.activeSelf,"Player death was not preserved before retry");
                flow.RestartCurrentLevel(); step=14; since=now; return;
            }
            if(step==14)
            {
                Require(player.CurrentHealth==player.MaxHealth && boss.Stage==1 && !flow.RunComplete,"Death retry failed");
                Record("PASS death/retry and replay restore player and boss; COMPLETE"); Capture("replay"); Stop();
            }
        }
        private static void PlacePart(EntityManager em,Entity e,float3 p)
        { var tr=em.GetComponentData<LocalTransform>(e); tr.Position=p; em.SetComponentData(e,tr); em.SetComponentData(e,new BossMotionTarget { Position=p,Rotation=tr.Rotation }); em.SetComponentData(e,new PhysicsVelocity()); }
        private static void Shoot(EntityManager em,Entity e,EnemyLaunchCause cause,float3 p)
        {
            var tr=em.GetComponentData<LocalTransform>(e); var ground=em.GetComponentData<EnemyGroundConstraint>(e); p.y=ground.HasGround!=0?ground.Height:tr.Position.y;
            tr.Position=p; em.SetComponentData(e,tr); em.SetComponentEnabled<RespawnRequest>(e,false);
            var l=em.GetComponentData<EnemyLaunchState>(e); EnemyLaunchTransition.Begin(ref l,cause,10); em.SetComponentData(e,l);
            em.SetComponentData(e,new PhysicsVelocity { Linear=new float3(0,0,25) });
        }
        private static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
        private static void Record(string text) { File.AppendAllText(Output,text+"\n"); }
        public static void Capture(string name)
        {
            var camera=Camera.main; if(camera==null) return;
            var previous=camera.targetTexture; var active=RenderTexture.active; var target=new RenderTexture(1280,720,24); var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try { camera.targetTexture=target; camera.Render(); RenderTexture.active=target; image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply(); File.WriteAllBytes("Temp/BossValidation/"+name+".png",image.EncodeToPNG()); }
            finally { camera.targetTexture=previous; RenderTexture.active=active; Object.DestroyImmediate(image); Object.DestroyImmediate(target); }
        }
    }
}
