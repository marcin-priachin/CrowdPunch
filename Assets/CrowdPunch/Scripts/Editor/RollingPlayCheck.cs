using System;
using System.IO;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
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
    // Controlled live physics/lifecycle probe. It does not measure human difficulty or fight duration.
    [InitializeOnLoad]
    public static class RollingPlayCheck
    {
        private const string Key="CrowdPunch.RollingCheck";
        private const string Output="Temp/RollingValidation/playcheck.txt";
        private static int step,phases,bounces,startFrame;
        private static double since;
        private static float simulationSince;
        private static Entity body,oldBoss;
        private static uint launchBefore,hitsBefore,completionBefore;
        private static float healthBefore;
        static RollingPlayCheck() { EditorApplication.update+=Tick; }
        public static void Start()
        {
            Directory.CreateDirectory("Temp/RollingValidation");
            File.WriteAllText(Output,"Controlled live probe: restores player health, places bodies, commits test rolls and injects final damage. Not a balance playtest.\n");
            step=0; phases=bounces=0; startFrame=Time.frameCount; body=oldBoss=Entity.Null;
            since=EditorApplication.timeSinceStartup; SessionState.SetBool(Key,true);
            Application.runInBackground=true; EditorApplication.isPlaying=true;
        }
        public static void Stop() { SessionState.SetBool(Key,false); }
        private static void Record(string text) { File.AppendAllText(Output,text+"\n"); Debug.Log(text); }
        private static void Require(bool condition,string text) { if(!condition) throw new InvalidOperationException(text); }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isPaused) return;
            try { Inspect(); }
            catch(Exception e) { Record("FAIL "+e); Stop(); EditorApplication.isPaused=true; Debug.LogException(e); }
        }
        private static void Commit(EntityManager em,Entity e,RollingAim aim,float3 position,float3 direction)
        {
            var t=em.GetComponentData<RollingTuning>(e); t.Aim=aim; em.SetComponentData(e,t);
            var b=em.GetComponentData<RollingBoss>(e); b.Phase=RollingPhase.Roll; b.Remaining=3;
            b.Direction=direction; b.Speed=t.RollSpeeds.z; b.Bounces=0; b.RollSequence++;
            b.PreviousPosition=position; em.SetComponentData(e,b);
            em.SetComponentData(e,LocalTransform.FromPositionRotation(position,quaternion.LookRotationSafe(direction,math.up())));
            em.SetComponentData(e,new PhysicsVelocity());
        }
        private static void Inspect()
        {
            Application.runInBackground=true;
            var flow=Object.FindFirstObjectByType<GauntletSequence>();
            var world=World.DefaultGameObjectInjectionWorld;
            if(flow==null||world==null||!world.IsCreated||flow.TransitionInProgress) return;
            double now=EditorApplication.timeSinceStartup;
            if(step!=0) Require(now-since<45,"Live probe stage timed out");
            var player=Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if(player!=null && step<9) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            if(step==0) { flow.SelectLevel(20); step=1; since=now; simulationSince=Time.time; startFrame=Time.frameCount; return; }
            var em=world.EntityManager; em.CompleteAllTrackedJobs();
            using var bosses=em.CreateEntityQuery(typeof(RollingBoss));
            if(step==11)
            {
                Require(bosses.IsEmpty,"Blob leaked into Chicken gauntlet");
                Require(!em.Exists(oldBoss),"Old boss survived scene unload");
                using var chicken=em.CreateEntityQuery(typeof(ChickenBoss));
                if(chicken.CalculateEntityCount()!=1) return; // ECS SubScene baking/loading can finish after the GameObject transition.
                Record("PASS scene selection removes Blob and loads Chicken"); flow.SelectLevel(20); step=12; since=now; return;
            }
            if(bosses.CalculateEntityCount()!=1) return;
            Entity boss=bosses.GetSingletonEntity(); var b=em.GetComponentData<RollingBoss>(boss);
            var t=em.GetComponentData<RollingTuning>(boss); var pose=em.GetComponentData<LocalTransform>(boss);
            Require(!em.HasComponent<Enemy>(boss),"Boss entered ordinary enemy lifecycle");
            Require(math.all(math.isfinite(pose.Position)) && math.all(math.abs(pose.Position.xz)<19),"Boss left playable space");
            using var crowd=em.CreateEntityQuery(typeof(BossCrowdMember)); using var enemies=crowd.ToEntityArray(Allocator.Temp);
            Require(enemies.Length<=3,"Supporting crowd exceeded authored budget");
            if(step==1)
            {
                if(enemies.Length<3) return;
                phases|=1<<(int)b.Phase; bounces=math.max(bounces,b.Bounces);
                if(Time.time-simulationSince<12) return;
                Require(Time.frameCount>startFrame+30,"Game frames did not advance");
                Require((phases&7)==7,"Pause/wind-up/roll cycle did not advance");
                Require(bounces>0,"Natural roll did not redirect against arena geometry");
                using var skin=em.CreateEntityQuery(typeof(RollingAnimationPivot),typeof(EnemyAnimation));
                Require(skin.CalculateEntityCount()==2,"Blob sampled animation owners missing");
                Record($"PASS live baked cycle, {Time.frameCount-startFrame} advancing frames, {bounces} natural redirects, three bounded Baselines and sampled animation");
                Commit(em,boss,RollingAim.Reflect,new float3(16,t.InitialPosition.y,-7),new float3(1,0,0));
                step=2; since=now; return;
            }
            if(step==2)
            {
                if(now-since<.65) return;
                Require(b.Bounces==1 && b.Direction.x<0,"Perimeter reflection stuck or counted sustained contact repeatedly");
                Record("PASS perimeter reflection at final-stage speed with one bounce");
                Commit(em,boss,RollingAim.ReaimOnCollision,new float3(4,t.InitialPosition.y,3),new float3(1,0,0));
                step=3; since=now; return;
            }
            if(step==3)
            {
                if(now-since<.4) return;
                Require(b.Bounces==1 && b.Direction.x<0,"Interior obstacle re-aim stuck or failed");
                Require(math.abs(b.Direction.z)>.01f,"Collision-time re-aim did not acquire player direction");
                Record("PASS interior obstacle collision-time re-aim and sustained-contact suppression");
                body=enemies[0]; var launch=em.GetComponentData<EnemyLaunchState>(body);
                launch.Phase=EnemyLaunchPhase.Launched; launch.Owner=EnemyLaunchOwner.Player; launch.LaunchSequence++; launch.LaunchDamage=20;
                launchBefore=launch.LaunchSequence; em.SetComponentData(body,launch);
                em.SetComponentEnabled<RespawnRequest>(body,false);
                em.SetComponentData(body,new Health { Current=100,Max=100 });
                em.SetComponentData(body,LocalTransform.FromPosition(new float3(0,.5f,-3)));
                em.SetComponentData(body,new PhysicsVelocity());
                Commit(em,boss,RollingAim.ReaimOnCollision,new float3(0,t.InitialPosition.y,-5),math.forward());
                step=4; since=now; return;
            }
            if(step==4)
            {
                if(now-since<.2) return;
                var launch=em.GetComponentData<EnemyLaunchState>(body);
                Require(launch.Owner==EnemyLaunchOwner.Boss && launch.LaunchSequence>launchBefore,"Real rolling contact did not reclaim launched body");
                Require(em.GetComponentData<Health>(body).Current<100,"Real rolling contact did not damage body");
                Record("PASS real solver crowd contact creates damaging fresh Boss-owned launch");
                b.Phase=RollingPhase.Pause; b.Remaining=5; b.InvulnerableUntil=0;
                b.PreviousPosition=new float3(0,t.InitialPosition.y,-5); em.SetComponentData(boss,b);
                em.SetComponentData(boss,LocalTransform.FromPosition(b.PreviousPosition));
                em.SetComponentData(boss,new PhysicsVelocity());
                launch.Owner=EnemyLaunchOwner.Player; launch.LaunchSequence++; launch.LaunchDamage=20; launch.HomingTarget=boss;
                em.SetComponentData(body,launch); em.SetComponentData(body,LocalTransform.FromPosition(new float3(0,.5f,-8)));
                em.SetComponentData(body,new PhysicsVelocity { Linear=new float3(0,0,30) });
                healthBefore=em.GetComponentData<Health>(boss).Current; hitsBefore=b.AcceptedHits;
                step=5; since=now; return;
            }
            if(step==5)
            {
                if(now-since<.3) return;
                Require(b.AcceptedHits>hitsBefore && em.GetComponentData<Health>(boss).Current<healthBefore,"Real body impact did not damage vulnerable boss");
                Require(b.Phase==RollingPhase.Pause && b.Remaining<5 && b.Remaining>4,"Body impact rewrote vulnerable timer");
                Require(em.GetComponentData<EnemyLaunchState>(body).HomingTarget!=boss,"Boss rebound retained homing");
                Record("PASS real launched-body impact damages pause without resetting timer and clears boss homing");
                b.Stage=3; b.Phase=RollingPhase.Pause; b.InvulnerableUntil=0; em.SetComponentData(boss,b);
                em.SetComponentData(boss,new Health { Current=1,Max=t.Health });
                completionBefore=GauntletCompletionRegistry.Sequence;
                em.GetBuffer<RollingHit>(boss).Add(new RollingHit { Source=body,Launch=em.GetComponentData<EnemyLaunchState>(body).LaunchSequence+1,Damage=10 });
                step=6; since=now; return;
            }
            if(step==6)
            {
                if(!flow.RunComplete) return;
                Require(GauntletCompletionRegistry.Sequence==completionBefore+1,"Defeat completion was not emitted once");
                foreach(var e in enemies) Require(em.GetComponentData<EnemyRespawnSettings>(e).Enabled==0,"Defeat left replenishment enabled");
                Record("PASS immediate final-gauntlet completion with survivors and disabled replenishment");
                oldBoss=boss; flow.RestartCurrentLevel(); step=7; since=now; return;
            }
            if(step==7)
            {
                Require(b.AcceptedHits==0 && b.Stage==1 && em.GetComponentData<Health>(boss).Current==t.Health,"Retry retained boss health/history");
                Require(em.GetBuffer<RollingHitHistory>(boss).Length==0,"Retry retained launch hit history");
                if(enemies.Length<3) return;
                Require(!flow.RunComplete,"Retry retained completion state");
                Record("PASS retry restores full boss health/stage/history and three bounded crowd members");
                oldBoss=boss; flow.SelectLevel(19); step=11; since=now; return;
            }
            if(step==12)
            {
                if(enemies.Length<3) return;
                Require(b.Stage==1 && b.AcceptedHits==0,"Reentry retained boss state");
                Record("PASS reentry restores fresh encounter; live probe complete"); Stop();
            }
        }
    }
}
