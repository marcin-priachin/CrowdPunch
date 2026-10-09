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

namespace CrowdPunch.Editor
{
    // Controlled physical shots and lifecycle checks, separate from human balance playtesting.
    [InitializeOnLoad]
    public static class DinoPlayCheck
    {
        private const string Key="CrowdPunch.DinoProbe";
        private const string Output="Temp/DinoValidation/playcheck.txt";
        private static int step,phaseMask;
        private static double since;
        private static Entity shot,victim,oldBoss;
        private static uint completion;
        static DinoPlayCheck() { EditorApplication.update+=Tick; }
        public static void Start()
        {
            Directory.CreateDirectory("Temp/DinoValidation");
            File.WriteAllText(Output,"Controlled live probe: restores health, places physical shots, holds boss for geometry checks, and injects final fall contacts. Not a balance playtest.\n");
            step=phaseMask=0; since=EditorApplication.timeSinceStartup; oldBoss=shot=victim=Entity.Null;
            SessionState.SetBool(Key,true);
            SessionState.SetString(GauntletSequence.EditorStartLevelKey,"Gauntlet_22");
            EditorSceneManagerStart();
        }
        private static void EditorSceneManagerStart()
        {
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/CrowdPunch/Scenes/Bootstrap.unity");
            Application.runInBackground=true; EditorApplication.isPlaying=true;
        }
        public static void Stop() => SessionState.EraseBool(Key);
        private static void Tick()
        {
            if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying) return;
            try { Advance(); }
            catch(Exception e) { Record("FAIL "+e); Stop(); Debug.LogException(e); }
        }
        private static void Advance()
        {
            var world=World.DefaultGameObjectInjectionWorld; if(world==null || !world.IsCreated) return;
            var em=world.EntityManager; em.CompleteAllTrackedJobs();
            double now=EditorApplication.timeSinceStartup;
            if(step!=0 && now-since>45) throw new Exception("Timed out at step "+step);
            var sequence=UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
            var player=UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(); if(player!=null) player.ResetHealth();
            using var bq=em.CreateEntityQuery(typeof(DinoBoss));
            using var pq=em.CreateEntityQuery(typeof(FallingPillar));
            using var eq=em.CreateEntityQuery(new EntityQueryDesc { All=new[]{ComponentType.ReadOnly<Enemy>(),ComponentType.ReadOnly<RespawnRequest>()},Options=EntityQueryOptions.IgnoreComponentEnabledState });
            if(step==10)
            {
                if(sequence.CurrentLevelIndex!=20 || bq.CalculateEntityCount()!=0) return;
                Require(pq.CalculateEntityCount()==0,"Pillars leaked on unload"); Record("PASS switching to Rolling removes Dino and all pillars");
                sequence.SelectLevel(21); Next(11); return;
            }
            if(bq.CalculateEntityCount()!=1 || pq.CalculateEntityCount()!=3) return;
            var boss=bq.GetSingletonEntity(); var b=em.GetComponentData<DinoBoss>(boss); var bt=em.GetComponentData<DinoTuning>(boss);
            if(eq.CalculateEntityCount()<8+3*bt.EnemiesPerPillar) return;
            using var pillars=pq.ToEntityArray(Allocator.Temp); using var enemies=eq.ToEntityArray(Allocator.Temp);
            Entity west=Entity.Null,east=Entity.Null,north=Entity.Null;
            foreach(var e in pillars)
            { var p=em.GetComponentData<PillarTuning>(e).InitialPosition; if(p.x<0) west=e; else if(p.x>0) east=e; else north=e; }
            if(step==0)
            {
                Require(eq.CalculateEntityCount()==8+3*bt.EnemiesPerPillar,"Crowd is not bounded at wave count plus local pillar slots");
                using var aq=em.CreateEntityQuery(typeof(EnemyAnimation)); bool sampled=false;
                foreach(var a in aq.ToComponentDataArray<EnemyAnimation>(Allocator.Temp)) sampled|=a.Owner==boss && a.Samples.IsCreated;
                Require(sampled,"Dino samples are not baked"); Record("PASS baked Dino, three pillars, eight wave Baselines plus configured local pillar crowd, sampled animation");
                b.Remaining=.1f; em.SetComponentData(boss,b); oldBoss=boss; Next(1); return;
            }
            if(step==1)
            {
                phaseMask|=1<<(int)b.Phase;
                if((phaseMask&7)!=7) return;
                Record("PASS live chase / moving warning / burst cycle");
                if(player!=null) player.GetComponent<PlayerController>().SetLevelEntryPoint(new Vector3(-8,-1,12),player.transform.rotation);
                Hold(em,boss,new float3(-8,bt.InitialPosition.y,-11),DinoPhase.Chase); Next(2); return;
            }
            if(step==2)
            {
                if(now-since<5) return;
                var position=em.GetComponentData<LocalTransform>(boss).Position;
                Require(position.z>0,"Boss failed to navigate around upright west pillar: "+position);
                Record("PASS continuous pursuit around upright pillar: "+position);
                if(player!=null) player.GetComponent<PlayerController>().SetLevelEntryPoint(new Vector3(16,-1,-15),player.transform.rotation);
                Hold(em,boss,new float3(-8,bt.InitialPosition.y,3),DinoPhase.Stagger);
                shot=enemies[0]; Shoot(em,shot,new float3(-8,-.25f,-6),new float3(0,0,22)); Next(3); return;
            }
            if(step==3)
            {
                var p=em.GetComponentData<FallingPillar>(west);
                if(p.Phase==PillarPhase.Upright) return;
                var launch=em.GetComponentData<EnemyLaunchState>(shot);
                Require(launch.Owner==EnemyLaunchOwner.Player && launch.Phase==EnemyLaunchPhase.Launched,"Trigger stole launch ownership/state");
                Require(em.GetComponentData<PhysicsVelocity>(shot).Linear.z>0,"Trigger rebounded/stopped body");
                Require(em.GetComponentData<PhysicsCollider>(west).Value.Value.GetCollisionFilter().CollidesWith==0,"Falling pillar remains solid");
                Record("PASS actual swept Player-owned shot topples and passes through; no solid falling collider"); Next(4); return;
            }
            if(step==4)
            {
                var p=em.GetComponentData<FallingPillar>(west);
                if(p.Phase!=PillarPhase.Consumed) return;
                Require(p.HitBoss==1 && b.SuccessfulHits==1,"Moving pillar did not count exactly one boss hit");
                Require(em.GetComponentData<Health>(boss).Current==2,"Unexpected pillar health"); Record("PASS moving shape damages boss once and successful pillar stays consumed");
                Hold(em,boss,new float3(-14,bt.InitialPosition.y,14),DinoPhase.Stagger);
                var t=em.GetComponentData<PillarTuning>(east); t.FallDirection=PillarFallDirection.IncomingTravel; em.SetComponentData(east,t);
                Shoot(em,shot,new float3(5,-.25f,-3),new float3(22,0,0)); Next(5); return;
            }
            if(step==5)
            {
                var p=em.GetComponentData<FallingPillar>(east); if(p.Phase!=PillarPhase.Waiting) return;
                Require(p.HitBoss==0 && p.Direction.x>.99f,"Incoming fall mode failed or miss damaged boss");
                if(player!=null) player.GetComponent<PlayerController>().SetLevelEntryPoint(new Vector3(8,-1,-3),player.transform.rotation);
                Record("PASS incoming-direction miss disappears"); Next(6); return;
            }
            if(step==6)
            {
                if(player!=null) player.GetComponent<PlayerController>().SetLevelEntryPoint(new Vector3(8,-1,-3),player.transform.rotation);
                var p=em.GetComponentData<FallingPillar>(east);
                if(now-since<5) return;
                Require(p.Phase==PillarPhase.Waiting,"Regenerated inside player");
                Record("PASS regeneration waits while player occupies upright space");
                if(player!=null) player.GetComponent<PlayerController>().SetLevelEntryPoint(new Vector3(16,-1,-15),player.transform.rotation);
                victim=enemies[1]; Shoot(em,victim,new float3(8,-.25f,-3),float3.zero);
                em.SetComponentData(victim,new EnemyLaunchState()); Next(7); return;
            }
            if(step==7)
            {
                if(em.GetComponentData<FallingPillar>(east).Phase!=PillarPhase.Upright) return;
                Require(math.distance(em.GetComponentData<LocalTransform>(victim).Position.xz,new float2(8,-3))>1,"Regenerated around overlapping ordinary body");
                Require(em.GetComponentData<EnemyLaunchState>(victim).Phase!=EnemyLaunchPhase.Launched,"Regeneration launched ordinary body");
                Record("PASS missed pillar restores after clearing crowd; ordinary body pushed without launch");
                completion=GauntletCompletionRegistry.Sequence;
                // Finish through actual moving-shape contacts while holding geometry; no direct boss damage.
                var t=em.GetComponentData<PillarTuning>(east); t.FallDirection=PillarFallDirection.TowardBoss; em.SetComponentData(east,t);
                Hold(em,boss,new float3(8,bt.InitialPosition.y,3),DinoPhase.Stagger);
                Shoot(em,shot,new float3(8,-.25f,-6),new float3(0,0,22)); Next(8); return;
            }
            if(step==8)
            {
                if(b.SuccessfulHits<2) return;
                Hold(em,boss,new float3(0,bt.InitialPosition.y,13),DinoPhase.Stagger);
                Shoot(em,shot,new float3(0,-.25f,4),new float3(0,0,22)); Next(9); return;
            }
            if(step==9)
            {
                if(!sequence.RunComplete) return;
                Require(b.Phase==DinoPhase.Defeated && b.SuccessfulHits==3,"Default three-hit defeat failed");
                Require(GauntletCompletionRegistry.Sequence==completion+1,"Completion was not exactly once");
                foreach(var e in enemies) if(em.HasComponent<BossCrowdMember>(e)) Require(em.GetComponentData<EnemyRespawnSettings>(e).Enabled==0,"Replenishment continued after boss defeat");
                Record("PASS three physical pillar hits complete immediately with surviving support; replenishment stops");
                sequence.RestartCurrentLevel(); Next(12); return;
            }
            if(step==12)
            {
                if(sequence.RunComplete || b.SuccessfulHits!=0) return;
                Require(em.GetComponentData<Health>(boss).Current==3,"Retry health incorrect");
                foreach(var e in pillars) Require(em.GetComponentData<FallingPillar>(e).Phase==PillarPhase.Upright && em.GetBuffer<PillarFallHit>(e).Length==0,"Retry pillar not restored");
                Record("PASS retry restores full boss, all upright pillars, cleared histories and eight crowd");
                sequence.SelectLevel(20); Next(10); return;
            }
            if(step==11)
            {
                Require(b.SuccessfulHits==0 && em.GetComponentData<Health>(boss).Current==3,"Returning encounter has stale health");
                Record("PASS returning from Rolling creates fresh encounter; probe complete"); Stop();
            }
        }
        private static void Hold(EntityManager em,Entity boss,float3 position,DinoPhase phase)
        {
            var b=em.GetComponentData<DinoBoss>(boss); b.Phase=phase; b.Remaining=100; b.Direction=math.forward(); b.PreviousPosition=position;
            em.SetComponentData(boss,b); em.SetComponentData(boss,LocalTransform.FromPosition(position)); em.SetComponentData(boss,new PhysicsVelocity());
        }
        private static void Shoot(EntityManager em,Entity e,float3 position,float3 velocity)
        {
            var ground=em.GetComponentData<EnemyGroundConstraint>(e);
            if(ground.HasGround!=0) position.y=ground.Height;
            em.SetComponentEnabled<RespawnRequest>(e,false); em.SetComponentEnabled<DamageRequest>(e,false); em.SetComponentEnabled<DeathRequest>(e,false);
            em.SetComponentEnabled<ExternalImpulse>(e,false); em.SetComponentEnabled<KnockbackRecovery>(e,false);
            em.SetComponentData(e,new Health { Current=100,Max=100 });
            var launch=em.GetComponentData<EnemyLaunchState>(e);
            launch.Phase=EnemyLaunchPhase.Launched; launch.Owner=EnemyLaunchOwner.Player; launch.LaunchSequence++;
            launch.LaunchDamage=20; launch.HomingTarget=Entity.Null; launch.BelowUsefulMomentumSeconds=0;
            em.SetComponentData(e,launch); em.SetComponentData(e,new PhysicsVelocity { Linear=velocity });
            em.SetComponentData(e,LocalTransform.FromPosition(position));
            ground.Height=position.y; ground.HasGround=ground.IsLocked=1;
            em.SetComponentData(e,ground);
        }
        private static void Next(int value) { step=value; since=EditorApplication.timeSinceStartup; }
        private static void Require(bool valid,string message) { if(!valid) throw new Exception(message); }
        private static void Record(string message) => File.AppendAllText(Output,message+"\n");
    }
}
