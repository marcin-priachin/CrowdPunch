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
    // Injected launches/positions prove real solver propagation and lifecycle, not encounter balance.
    [InitializeOnLoad]
    public static class SnakeEncounterPlayCheck
    {
        private const string Output = "Temp/SnakeValidation/live-check.txt";
        private static int step;
        private static double since;
        private static bool running;
        private static Entity head, target, returnedTarget, sequence, oldSequence, projectileSource;
        private static uint generation;
        private static float initialMaxHealth;
        private static int initialOwnedCount;
        static SnakeEncounterPlayCheck() => EditorApplication.update += Tick;
        public static void Start()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Launch Gauntlet_24 first.");
            Directory.CreateDirectory("Temp/SnakeValidation"); File.WriteAllText(Output, "Controlled Play Mode validation, injected positions and launches.\n");
            step = 0; running = true; since = EditorApplication.timeSinceStartup;
            UnityEngine.Object.FindFirstObjectByType<GauntletSequence>().RestartCurrentLevel();
        }
        private static void Tick()
        {
            if (!running || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try { Check(); }
            catch (Exception e) { Record("FAIL " + e); running = false; EditorApplication.isPaused = true; }
        }
        private static void Check()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var ph = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (ph != null) { ph.gameObject.SetActive(true); ph.Restore(ph.MaxHealth); }
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            var flow = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
            Require(flow.CurrentLevelIndex == 23 && !flow.HasCampaign, "Expected isolated legacy Gauntlet_24");
            using var heads = em.CreateEntityQuery(typeof(SnakeHead)).ToEntityArray(Allocator.Temp);
            if (step == 0 || step == 4)
            {
                using var seq = em.CreateEntityQuery(typeof(EnemyWaveSequence));
                if (seq.CalculateEntityCount() != 1) return;
                sequence = seq.GetSingletonEntity(); var wave = em.GetComponentData<EnemyWaveSequence>(sequence);
                if (wave.Phase != EnemyWaveRuntimePhase.AwaitingActivation || heads.Length != 2) return;
                using var orbs = em.CreateEntityQuery(typeof(SnakeSegment)).ToEntityArray(Allocator.Temp);
                if (orbs.Length != 12) return;
                foreach (var h in heads) if (em.GetComponentData<SnakeHead>(h).AttachedCount != 6) return;
                if (step == 4)
                {
                    Require(sequence != oldSequence || wave.RunGeneration != generation, "Retry retained the original encounter identity");
                    foreach (var orb in orbs) Require(em.GetComponentData<SnakeSegment>(orb).Detached == 0, "Retry retained detached state");
                    foreach (var h in heads)
                    {
                        var health = em.GetComponentData<Health>(h);
                        Require(health.Current == initialMaxHealth && health.Max == initialMaxHealth, "Retry did not restore profile head health");
                    }
                    Record("PASS retry restores full tails, profile health, authored composition and a new encounter identity");
                    using var enemies = em.CreateEntityQuery(typeof(EnemyWaveOwnership)).ToEntityArray(Allocator.Temp);
                    Require(enemies.Length == initialOwnedCount, "Tail entities changed encounter counting");
                    foreach (var enemy in enemies)
                    {
                        em.SetComponentData(enemy, new Health());
                        var l = em.GetComponentData<EnemyLaunchState>(enemy); l.Phase = EnemyLaunchPhase.Defeated;
                        em.SetComponentData(enemy, l); em.SetComponentEnabled<DeathRequest>(enemy, true);
                    }
                    Record("Injected head/baseline defeat to exercise completion accounting"); Next(5); return;
                }
                generation = wave.RunGeneration;
                oldSequence = sequence;
                using(var owned=em.CreateEntityQuery(typeof(EnemyWaveOwnership))) initialOwnedCount=owned.CalculateEntityCount();
                Record("PASS actual baked scene: two Yeti heads, twelve attached orbs, head-owned wave count");
                head = heads[0]; var tuning = em.GetComponentData<SnakeTuning>(head);
                initialMaxHealth = em.GetComponentData<Health>(head).Max;
                tuning.WaveAmplitude = tuning.PursuitSpeed = 0; em.SetComponentData(head, tuning);
                var movement = em.GetComponentData<EnemyMovementSettings>(head); movement.MoveSpeed = 0; em.SetComponentData(head, movement);
                em.SetComponentData(head, LocalTransform.FromPosition(new float3(-10,0,-6))); em.SetComponentData(head, new PhysicsVelocity());
                var path = em.GetBuffer<SnakePathPoint>(head); path.Clear();
                for (int i=6;i>=0;i--) path.Add(new SnakePathPoint { Position = new float3(-10+i*1.5f,0,-6) });
                var links = em.GetBuffer<SnakeTailLink>(head);
                for (int i=0;i<links.Length;i++)
                {
                    var p = em.GetComponentData<LocalTransform>(links[i].Orb); p.Position = new float3(-10+(i+1)*1.5f,-.4f,-6);
                    em.SetComponentData(links[i].Orb,p); em.SetComponentData(links[i].Orb,new PhysicsVelocity());
                }
                target = links[5].Orb; returnedTarget = links[4].Orb;
                // Use a test projectile independently of the user's authored Baseline count.
                var source=em.Instantiate(head); projectileSource=source;
                em.SetComponentData(source,em.GetComponentData<SnakeHead>(head).DynamicMass);
                em.RemoveComponent<SnakeHead>(source); em.RemoveComponent<SnakeTuning>(source);
                em.RemoveComponent<SnakeTailLink>(source); em.RemoveComponent<SnakePathPoint>(source);
                em.RemoveComponent<EnemyWaveOwnership>(source);
                // Isolate this trajectory from incidental crowd contacts; retry reconstructs all bodies.
                using (var bodies = em.CreateEntityQuery(typeof(Enemy)).ToEntityArray(Allocator.Temp))
                using (var commands = new EntityCommandBuffer(Allocator.Temp))
                {
                    foreach (var body in bodies)
                        if (body != head && body != source && (!em.HasComponent<SnakeSegment>(body)
                            || em.GetComponentData<SnakeSegment>(body).Head != head))
                            commands.SetSharedComponent(body, new PhysicsWorldIndex(uint.MaxValue));
                    commands.Playback(em);
                }
                using (var settings = em.CreateEntityQuery(typeof(EnemyLaunchSettings)))
                {
                    var launchTuning = settings.GetSingleton<EnemyLaunchSettings>(); launchTuning.PropagationAimCorrectionRadius = 0;
                    em.SetComponentData(settings.GetSingletonEntity(), launchTuning);
                }
                em.SetComponentData(source,LocalTransform.FromPosition(new float3(-5.5f,0,-3)));
                em.SetComponentData(source,new PhysicsVelocity { Linear=new float3(0,0,-20) });
                em.SetComponentData(source,new EnemyLaunchState { Phase=EnemyLaunchPhase.Launched, Owner=EnemyLaunchOwner.Boss,
                    LaunchDamage=20, LaunchSequence=1 });
                // First strike a protected middle part, then strike the exposed tip.
                Next(6); return;
            }
            if (step == 6)
            {
                if (EditorApplication.timeSinceStartup - since < .4) return;
                Require(em.GetComponentData<SnakeHead>(head).AttachedCount == 6, "Middle impact changed tail length");
                var middle=em.GetBuffer<SnakeTailLink>(head)[2].Orb;
                Require(em.GetComponentData<EnemyLaunchState>(middle).Phase == EnemyLaunchPhase.Active, "Middle impact launched protected segment");
                Record("PASS real body impact cannot launch a protected middle segment");
                var source=projectileSource;
                em.SetComponentData(source,LocalTransform.FromPosition(new float3(-1,0,-3)));
                em.SetComponentData(source,new PhysicsVelocity {Linear=new float3(0,0,-20)});
                var l=em.GetComponentData<EnemyLaunchState>(source); l.Phase=EnemyLaunchPhase.Launched; l.HomingTarget=Entity.Null;
                em.SetComponentData(source,l); Next(1); return;
            }
            if (step == 1)
            {
                Require(EditorApplication.timeSinceStartup - since < 8, "Real launched-body collision did not cut the tail");
                if (!em.Exists(target) || em.GetComponentData<SnakeSegment>(target).Detached == 0) return;
                Require(em.GetComponentData<SnakeHead>(head).AttachedCount == 5, "Tip launch removed more than one segment");
                Require(em.GetComponentData<EnemyLaunchState>(target).Owner == EnemyLaunchOwner.Boss, "Collision lost launch owner");
                Record("PASS real solver impact launches only exposed tip and preserves Boss ownership"); Next(2); return;
            }
            if (step == 2)
            {
                Require(EditorApplication.timeSinceStartup - since < 8, "Detached orb failed to leave its original snake");
                Require(em.Exists(target), "Orb expired before leaving original snake");
                if (em.GetComponentData<SnakeSegment>(target).Cleared == 0) return;
                Record("PASS launched orb physically clears original snake and re-enables collisions");
                var p=em.GetComponentData<LocalTransform>(target);p.Position=new float3(-2.5f,-.4f,-3);em.SetComponentData(target,p);
                var launch=em.GetComponentData<EnemyLaunchState>(target); launch.HomingTarget=returnedTarget; em.SetComponentData(target,launch);
                em.SetComponentData(target,new PhysicsVelocity { Linear=new float3(0,0,-20) }); Next(3); return;
            }
            if (step == 3)
            {
                Require(EditorApplication.timeSinceStartup - since < 8, "Cleared orb could not collide with original tail on return");
                if (em.GetComponentData<SnakeHead>(head).AttachedCount != 4) return;
                Require(em.GetComponentData<EnemyLaunchState>(head).Phase == EnemyLaunchPhase.Active, "Tail loss defeated the head");
                Require(CrowdPunch.Systems.Combat.SnakeHitRules.Protected(em,head), "Head exposed before tail was empty");
                Record("PASS return shot launches predecessor while head and inner segments remain protected");
                if(em.Exists(projectileSource))em.DestroyEntity(projectileSource);
                flow.RestartCurrentLevel(); Next(4); return;
            }
            if (step == 5)
            {
                Require(EditorApplication.timeSinceStartup - since < 8, "Head/baseline defeats did not complete the level");
                if (!flow.RunComplete) return;
                using var orbs=em.CreateEntityQuery(typeof(SnakeSegment));
                Require(orbs.IsEmpty, "Completion retained invisible tail entities");
                Record("PASS final legacy level completes on head/baseline defeat without tail objectives");
                running=false; EditorApplication.isPaused=true;
            }
        }
        private static void Next(int next) { step=next; since=EditorApplication.timeSinceStartup; }
        private static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
        private static void Record(string message) => File.AppendAllText(Output, message+"\n");
    }
}
