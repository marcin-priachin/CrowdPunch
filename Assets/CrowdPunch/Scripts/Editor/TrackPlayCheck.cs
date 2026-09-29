using System;
using System.IO;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.InputBridge;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    [InitializeOnLoad]
    public static class TrackPlayCheck
    {
        private const string Key = "CrowdPunch.TrackCheck";
        private const string Output = "Temp/TrackValidation/playcheck.txt";
        private static int step, expected;
        private static double since, simulationSince;
        private static Entity body, explosive;
        private static bool observedRebound;
        static TrackPlayCheck() { since = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Bootstrap.unity");
            Directory.CreateDirectory("Temp/TrackValidation");
            File.WriteAllText(Output, "TRACK-001..005 controlled baked-world checks. Injected positions/velocities; not balance evidence.\n");
            step = 0; expected = 0; observedRebound = false; body = explosive = Entity.Null;
            since = EditorApplication.timeSinceStartup;
            SessionState.SetBool(Key, true); EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
        }
        public static void Stop() { SessionState.SetBool(Key, false); }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            Application.runInBackground = true;
            try { Inspect(); }
            catch (Exception e) { Record("FAIL step " + step + ": " + e); Stop(); EditorApplication.isPaused = true; Debug.LogException(e); }
        }
        private static void Inspect()
        {
            Require(EditorApplication.timeSinceStartup - since < 90, "Step timed out");
            var flow = Object.FindFirstObjectByType<GauntletSequence>();
            var world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || !world.IsCreated || flow.TransitionInProgress) return;
            var player = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (player != null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            if (step == 0) { flow.SelectLevel(15); Next(1, world); return; }
            using var targets = em.CreateEntityQuery(typeof(TrackObject));
            using var bodies = em.CreateEntityQuery(typeof(Enemy), typeof(BarricadeCrowdMember));
            if (targets.CalculateEntityCount() != 1) return;
            var target = targets.GetSingletonEntity(); var motion = em.GetComponentData<TrackObjectState>(target);
            var pose = em.GetComponentData<LocalTransform>(target);
            double elapsed = world.Time.ElapsedTime - simulationSince;
            if (step == 2 && em.GetComponentData<PhysicsVelocity>(body).Linear.z < 0) observedRebound = true;
            if (step == 1 && bodies.CalculateEntityCount() == 12)
            {
                Require(motion.TargetStep == 0 && motion.Distance == 0, "Fresh track starts at opposite endpoint");
                using var enemies = bodies.ToEntityArray(Allocator.Temp); int i = 0;
                foreach (var e in enemies)
                {
                    Require(!em.HasComponent<ExplosiveEnemyState>(e), "Baseline-only opening");
                    if (body == Entity.Null) body = e;
                    Freeze(em, e); Park(em, e, new float3(-11 + (i % 6) * 4.3f, .05f, 10 + i / 6 * 2)); i++;
                }
                Require(PunchAimAssist.IsValidTarget(em, body, target), "Existing aim target accepts block");
                player.transform.position = new Vector3(-7, .5f, -10);
                Record("PASS baked 12 Baselines, no opening Explosives, start pose and existing aim assistance");
                Launch(em, body, pose.Position, 1); Next(2, world);
            }
            else if (step == 2 && elapsed > .65)
            {
                Require(motion.TargetStep == 1 && math.abs(motion.Distance - 2) < .01f, "Actual launched-body contact advances one step: " + motion.TargetStep);
                Require(observedRebound, "Ordinary rebound continues before later arena-wall contact");
                Park(em, body, new float3(-11, .05f, 9));
                var bridge = Object.FindFirstObjectByType<PlayerEcsBridge>();
                bridge.PublishMovement(new Vector3(0, .5f, -8), new Vector3(0, .5f, 2), .5f, .02f);
                world.GetOrCreateSystem<PlayerObstacleCollisionSystem>().Update(world.Unmanaged);
                Require(bridge.ResolvedMovementPosition.z < pose.Position.z - 1.6f, "Hybrid traversal blocked at updated pose");
                bridge.ReceiveObstacleDisplacement(new float3(0, .5f, pose.Position.z + 2.4f));
                Record("PASS physical body impact, one smooth 2m slide, rebound and updated player collision");
                Launch(em, body, pose.Position, 1); Next(3, world);
            }
            else if (step == 3 && elapsed > .65)
            {
                Require(motion.TargetStep == 2 && math.abs(motion.Distance - 4) < .01f, "Relaunch advances again");
                Require(math.abs(player.transform.position.x) > 2, "Slide displaces player sideways: " + player.transform.position);
                player.transform.position = new Vector3(-7, .5f, -10);
                Launch(em, body, pose.Position, -1);
                Record("PASS re-launch eligibility and player push without blocking slide"); Next(4, world);
            }
            else if (step == 4 && elapsed > .65)
            {
                Require(motion.TargetStep == 1 && math.abs(motion.Distance - 2) < .01f, "Opposite physical impact reverses progress");
                Park(em, body, new float3(-11, .05f, 9));
                Record("PASS backward hit reverses track progress"); Next(5, world);
            }
            else if (step == 5 && bodies.CalculateEntityCount() == 14)
            {
                using var enemies = bodies.ToEntityArray(Allocator.Temp); int i = 0;
                foreach (var e in enemies) if (em.HasComponent<ExplosiveEnemyState>(e))
                {
                    Freeze(em, e); Park(em, e, new float3(11, .05f, -9 + i * 18));
                    if (explosive == Entity.Null) explosive = e; i++;
                }
                Require(i == 2, "Delayed wave supplies two Explosives");
                Launch(em, explosive, pose.Position, 1); Next(6, world);
            }
            else if (step == 6 && elapsed > .65)
            {
                Require(motion.TargetStep == 2, "Exploder impact plus blast advances once: " + motion.TargetStep);
                Require(em.GetComponentData<ExplosiveEnemyState>(explosive).HasExploded != 0, "Impact detonates Explosive");
                Record("PASS delayed Explosives and actual impact/blast deduplication"); Next(7, world);
            }
            else if (step == 7 && elapsed > 5 && !em.IsComponentEnabled<RespawnRequest>(explosive))
            {
                Require(em.GetComponentData<ExplosiveEnemyState>(explosive).HasExploded == 0, "Pooled Explosive replenishes");
                Require(bodies.CalculateEntityCount() == 14, "Replenishment stays bounded");
                Freeze(em, explosive); Park(em, explosive, new float3(11, .05f, 9));
                Record("PASS replenishment reuses slots at 14-root cap");
                ProfileNavigation(world, em, target);
                expected = 3; Launch(em, body, pose.Position, 1); Next(8, world);
            }
            else if (step == 8 && (elapsed > .65 || motion.Locked != 0))
            {
                Require(motion.TargetStep == expected, "Finishing shot advances exactly once: " + motion.TargetStep);
                if (expected < 5) { expected++; Launch(em, body, pose.Position, 1); Next(8, world); }
                else
                {
                    Require(motion.Locked != 0 && flow.RunComplete && math.abs(pose.Position.z - 5) < .01f,
                        "Physical socket arrival locks and completes despite survivors");
                    Record("PASS physical socket completion with survivors");
                    FeedbackTimeController.SetPaused(false); flow.RestartCurrentLevel(); Next(9, world);
                }
            }
            else if (step == 9 && bodies.CalculateEntityCount() == 12)
            {
                Require(motion.TargetStep == 0 && motion.Distance == 0 && motion.Locked == 0 && !flow.RunComplete, "Retry restores motion and completion");
                Require(em.GetBuffer<BarricadeHitHistory>(target).Length == 0, "Retry clears counted hits");
                Record("PASS restart restores track, history and staged crowd"); Record("COMPLETE"); Stop(); EditorApplication.isPaused = true;
            }
        }
        private static void Freeze(EntityManager em, Entity e)
        {
            var m = em.GetComponentData<EnemyMovementSettings>(e); m.MoveSpeed = m.WanderSpeed = 0; em.SetComponentData(e, m);
        }
        private static void ProfileNavigation(World world, EntityManager em, Entity target)
        {
            var pose = em.GetComponentData<LocalTransform>(target);
            var system = world.GetOrCreateSystem<CrowdPunch.Systems.AI.TrackNavigationSystem>();
            double total = 0, maximum = 0;
            var watch = new System.Diagnostics.Stopwatch();
            for (int i = 0; i < 31; i++)
            {
                var sample = pose; sample.Position.z = -5 + i % 21 * .5f;
                em.SetComponentData(target, sample);
                watch.Restart(); system.Update(world.Unmanaged); watch.Stop();
                if (i == 0) continue;
                total += watch.Elapsed.TotalMilliseconds; maximum = System.Math.Max(maximum, watch.Elapsed.TotalMilliseconds);
            }
            em.SetComponentData(target, pose); system.Update(world.Unmanaged);
            Record("PROFILE TrackNavigation at 14-root cap: 30 forced moving-footprint rebuilds; mean "
                + (total / 30).ToString("F3") + " ms, max " + maximum.ToString("F3") + " ms (Editor stopwatch, no whole-frame claim).");
        }
        private static void Launch(EntityManager em, Entity e, float3 center, float sign)
        {
            Park(em, e, new float3(0, .05f, center.z - sign * 4));
            var launch = em.GetComponentData<EnemyLaunchState>(e); EnemyLaunchTransition.Begin(ref launch, EnemyLaunchCause.PlayerPunch, 10); em.SetComponentData(e, launch);
            em.SetComponentData(e, new PhysicsVelocity { Linear = new float3(0,0,sign * 24) });
            em.SetComponentData(e, new EnemyGroundConstraint { HasGround = 1, IsLocked = 1, Height = .05f });
        }
        private static void Park(EntityManager em, Entity e, float3 position)
        {
            var t = em.GetComponentData<LocalTransform>(e); t.Position = position; em.SetComponentData(e, t); em.SetComponentData(e, new PhysicsVelocity());
        }
        private static void Next(int value, World world) { step = value; since = EditorApplication.timeSinceStartup; simulationSince = world.Time.ElapsedTime; }
        private static void Record(string value) => File.AppendAllText(Output, value + "\n");
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
