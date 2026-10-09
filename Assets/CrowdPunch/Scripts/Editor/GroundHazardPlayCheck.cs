using System;
using System.IO;
using System.Reflection;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.InputBridge;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    [InitializeOnLoad]
    public static class GroundHazardPlayCheck
    {
        private const string Key = "CrowdPunch.GroundHazardCheck";
        private const string Output = "Temp/GroundHazardValidation/playcheck.txt";
        private static int step, frame;
        private static double started, simStarted;
        static GroundHazardPlayCheck() { started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            Directory.CreateDirectory("Temp/GroundHazardValidation");
            File.WriteAllText(Output,"GROUND-001..007 controlled baked-world checks; injected states, not a balance playtest.\n");
            EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Bootstrap.unity");
            SessionState.SetString(GauntletSequence.EditorStartLevelKey,"Gauntlet_23");
            step = 0; started = EditorApplication.timeSinceStartup; SessionState.SetBool(Key,true);
            EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
        }
        private static void Record(string text) => File.AppendAllText(Output,text + "\n");
        private static void Require(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            Application.runInBackground = true;
            try { Check(); }
            catch (Exception e) { Record("FAIL step " + step + ": " + e); SessionState.SetBool(Key,false); EditorApplication.isPaused = true; Debug.LogException(e); }
        }
        private static void Check()
        {
            Require(EditorApplication.timeSinceStartup - started < 120,"Simulation/level transition timeout");
            var flow = Object.FindFirstObjectByType<GauntletSequence>(); var world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || flow.TransitionInProgress || flow.CurrentLevelIndex != 22) return;
            var player = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            player.gameObject.SetActive(true); player.Restore(player.MaxHealth);
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var sequenceQuery = em.CreateEntityQuery(typeof(EnemyWaveSequence)); if (sequenceQuery.CalculateEntityCount() != 1) return;
            var sequenceEntity = sequenceQuery.GetSingletonEntity(); var sequence = em.GetComponentData<EnemyWaveSequence>(sequenceEntity);
            using var patches = em.CreateEntityQuery(typeof(GroundHazard),typeof(GroundHazardState));
            using var patchRoots = patches.ToEntityArray(Allocator.Temp);
            using var query = em.CreateEntityQuery(typeof(Enemy),typeof(EnemyWaveOwnership)); using var enemies = query.ToEntityArray(Allocator.Temp);
            if (step == 0)
            {
                if (sequence.CurrentWaveIndex != 0 || sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation) return;
                Require(sequence.SpawnedCount == 12 && patchRoots.Length == 4,"First wave and baked patches");
                int introduced = 0;
                foreach (var h in patchRoots) if (em.GetComponentData<GroundHazardState>(h).Introduced != 0) introduced++;
                Require(introduced == 2,"Wave one introduces permanent patches only");
                using var hazards = GroundHazardSpawnClearance.Capture(em);
                foreach (var enemy in enemies)
                {
                    Require(em.HasComponent<GroundHazardDamageClock>(enemy) && em.HasComponent<GroundHazardRoute>(enemy) && em.HasBuffer<GroundHazardWaypoint>(enemy),"Enemy prefab baking omitted hazard data");
                    Require(GroundHazardGeometry.Clear(hazards.AsArray(),em.GetComponentData<LocalTransform>(enemy).Position.xz,
                        em.GetComponentData<LocalTransform>(enemy).Position.xz,em.GetComponentData<NavigationAgent>(enemy).Radius),"Initial spawn touched an active patch");
                }
                Record("PASS baked 12 Baselines, four patches, two introduced, safe spawning and hazard components");
                simStarted = world.Time.ElapsedTime; frame = Time.frameCount; step = 1; return;
            }
            if (step == 1)
            {
                if (world.Time.ElapsedTime - simStarted < 1) return;
                Require(Time.frameCount > frame + 10,"No advancing game frames");
                ControlledHits(world,enemies[0],em.GetComponentData<GroundHazard>(patchRoots[0]),player);
                Record("PASS advancing frames, baked damage resolver, player invulnerability/dash bypass and unchanged movement");
                flow.RestartCurrentLevel(); step = 2; return;
            }
            if (step == 2)
            {
                if (sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation || sequence.CurrentWaveIndex != 0) return;
                Require(sequence.SpawnedCount == 12,"Retry did not restore finite wave one");
                foreach (var h in patchRoots)
                {
                    var patch = em.GetComponentData<GroundHazard>(h); var status = em.GetComponentData<GroundHazardState>(h);
                    Require(status.RunGeneration == sequence.RunGeneration && status.WaveIndex == 0,"Old cycle ownership survived retry");
                    Require(status.Introduced == (patch.FirstWave == 0 ? 1 : 0),"Retry retained second-wave hazards");
                }
                foreach (var e in enemies) Defeat(em,e);
                Record("PASS actual retry restores first-wave patch introduction and encounter clocks; injected first-wave defeats"); step = 3; return;
            }
            if (step == 3)
            {
                if (sequence.CurrentWaveIndex != 1 || sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation) return;
                Require(sequence.SpawnedCount == 20,"Second finite wave composition");
                foreach (var h in patchRoots) Require(em.GetComponentData<GroundHazardState>(h).Introduced != 0,"Periodic patch not introduced");
                Record("PASS actual second finite wave has 20 Baselines and all four patches");
                simStarted = world.Time.ElapsedTime; step = 4; return;
            }
            if (step == 4)
            {
                if (world.Time.ElapsedTime - simStarted < 2) return;
                SessionState.SetBool(Key,false); EditorApplication.isPaused = true;
                Record("PASS second-wave simulation; paused for visual capture and crowd measurement");
            }
        }
        public static void Complete()
        {
            var world = World.DefaultGameObjectInjectionWorld; var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(typeof(Enemy),typeof(EnemyWaveOwnership)); using var enemies = query.ToEntityArray(Allocator.Temp);
            foreach (var e in enemies) Defeat(em,e);
            Record("Injected final-wave defeats while permanent hazards remain active");
            EditorApplication.isPaused = false;
        }
        private static void Defeat(EntityManager em, Entity e)
        {
            var health = em.GetComponentData<Health>(e); health.Current = 0; em.SetComponentData(e,health);
            var launch = em.GetComponentData<EnemyLaunchState>(e); launch.Phase = EnemyLaunchPhase.Defeated; em.SetComponentData(e,launch);
            em.SetComponentEnabled<DeathRequest>(e,true);
        }
        private static void ControlledHits(World world, Entity target, GroundHazard hazard, PlayerHealth player)
        {
            var em = world.EntityManager;
            var oldPose = em.GetComponentData<LocalTransform>(target); var oldLaunch = em.GetComponentData<EnemyLaunchState>(target);
            var oldVelocity = em.GetComponentData<PhysicsVelocity>(target); var oldHealth = em.GetComponentData<Health>(target);
            em.SetComponentData(target,LocalTransform.FromPosition(hazard.Position + new float3(0,50,0)));
            em.SetComponentData(target,new Health { Current = 100,Max = 100 }); em.SetComponentData(target,default(GroundHazardDamageClock));
            em.SetComponentData(target,new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched,Owner = EnemyLaunchOwner.Player,FeedbackChainDepth = 4 });
            em.SetComponentData(target,new PhysicsVelocity { Linear = new float3(5,0,2) });
            var damage = world.GetExistingSystem<GroundHazardDamageSystem>(); damage.Update(world.Unmanaged);
            Require(em.GetComponentData<Health>(target).Current == 100-hazard.Damage,"Baked horizontal damage");
            Require(em.GetComponentData<PhysicsVelocity>(target).Linear.Equals(new float3(5,0,2)),"Hazard changed launch velocity");
            damage.Update(world.Unmanaged); Require(em.GetComponentData<Health>(target).Current == 100-hazard.Damage,"Shared clock double hit");
            em.SetComponentData(target,oldPose); em.SetComponentData(target,oldLaunch); em.SetComponentData(target,oldVelocity); em.SetComponentData(target,oldHealth);
            em.SetComponentData(target,default(GroundHazardDamageClock));
            var controller = player.GetComponent<PlayerController>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var invulnerability = typeof(PlayerHealth).GetField("invincibilityRemainingSeconds",flags);
            var dash = typeof(PlayerController).GetField("dashActive",flags);
            object oldInvulnerability = invulnerability.GetValue(player), oldDash = dash.GetValue(controller);
            invulnerability.SetValue(player,10f); dash.SetValue(controller,true);
            using var snapshots = em.CreateEntityQuery(typeof(PlayerSnapshot)); var oldSnapshot = snapshots.GetSingleton<PlayerSnapshot>();
            using var clocks = em.CreateEntityQuery(typeof(GroundHazardPlayerClock)); em.SetComponentData(clocks.GetSingletonEntity(),default(GroundHazardPlayerClock));
            em.SetComponentData(snapshots.GetSingletonEntity(),new PlayerSnapshot { Position = hazard.Position + new float3(0,50,0),Radius = oldSnapshot.Radius,IsAvailable = true });
            float hp = player.CurrentHealth;
            damage.Update(world.Unmanaged); world.GetExistingSystem<GroundHazardPlayerHitSystem>().Update(world.Unmanaged);
            Require(player.CurrentHealth == hp-hazard.Damage && controller.IsDashing && player.IsInvincible,"Player protection blocked hazard or dash was cancelled");
            invulnerability.SetValue(player,oldInvulnerability); dash.SetValue(controller,oldDash);
            em.SetComponentData(snapshots.GetSingletonEntity(),oldSnapshot); player.Restore(player.MaxHealth);
        }
    }
}
