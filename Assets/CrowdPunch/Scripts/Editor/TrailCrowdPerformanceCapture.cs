using System;
using System.IO;
using System.Text;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Physics;
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
    public static class TrailCrowdPerformanceCapture
    {
        private const string Key = "CrowdPunch.TrailPerformance";
        private static int step;
        private static double started;
        static TrailCrowdPerformanceCapture() { EditorApplication.update += Tick; }
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            step = 0;
            EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Bootstrap.unity");
            SessionState.SetBool(Key, true); EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            Application.runInBackground = true;
            var world = World.DefaultGameObjectInjectionWorld;
            var flow = Object.FindFirstObjectByType<GauntletSequence>();
            if (world == null || flow == null || flow.TransitionInProgress) return;
            var health = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (health != null) { health.gameObject.SetActive(true); health.Restore(health.MaxHealth); }
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            if (step == 0) { flow.SelectLevel(18); step = 1; return; }
            using var q = em.CreateEntityQuery(typeof(Enemy), typeof(EnemyArchetype));
            using var roots = q.ToEntityArray(Allocator.Temp);
            if (step == 1)
            {
                if (flow.CurrentLevelIndex != 18 || roots.Length < 7) return;
                Entity baseline = Entity.Null, trail = Entity.Null;
                foreach (var e in roots) if (em.HasComponent<TrailEmitter>(e)) trail = e; else baseline = e;
                if (baseline == Entity.Null || trail == Entity.Null) return;
                for (int i = 0; i < 250; i++)
                {
                    Entity e = i < roots.Length ? roots[i] : em.Instantiate(i % 25 == 0 ? trail : baseline);
                    if (i >= roots.Length) em.RemoveComponent<EnemyWaveOwnership>(e);
                    em.SetComponentData(e, LocalTransform.FromPosition(new float3(-11 + i % 20 * 1.15f, 0, -7 + i / 20 * 1.2f)));
                    em.SetComponentData(e, new Health { Current = 10000, Max = 10000 });
                    em.SetComponentData(e, new PhysicsVelocity()); em.SetComponentData(e, new EnemyLaunchState());
                    em.SetComponentEnabled<RespawnRequest>(e, false); em.SetComponentEnabled<DeathRequest>(e, false);
                    if (em.HasComponent<TrailEmitter>(e)) em.SetComponentData(e, new TrailEmitter { OrbitSign = 1, ReverseRemaining = 4 });
                }
                started = world.Time.ElapsedTime; step = 2; return;
            }
            if (world.Time.ElapsedTime - started < 5) return;
            SessionState.SetBool(Key, false); EditorApplication.isPaused = true;
            var handles = new[] { world.GetExistingSystem<TrailCirclingSystem>(), world.GetExistingSystem<TrailAvoidanceSystem>(),
                world.GetExistingSystem<TrailEmissionSystem>(), world.GetExistingSystem<TrailDamageSystem>() };
            var names = new[] { "Circling", "Avoidance", "Emission (stationary sampling)", "Damage (active sections, cooldown protected)" };
            var report = new StringBuilder();
            using var sq = em.CreateEntityQuery(typeof(TrailSection));
            using var tq = em.CreateEntityQuery(typeof(TrailEmitter));
            report.AppendLine($"Unity {Application.unityVersion}; 250 bodies, {tq.CalculateEntityCount()} Trail sources, {sq.CalculateEntityCount()} live sections; 5 seconds advancing Editor simulation.");
            report.AppendLine("20 warmups and 120 completed updates per system. Frozen physics/time during measurement; not standalone FPS or an impact-burst benchmark. Calling-thread allocation only.");
            for (int n = 0; n < handles.Length; n++)
            {
                for (int i = 0; i < 20; i++) { handles[n].Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                long allocated = GC.GetAllocatedBytesForCurrentThread(), begin = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int i = 0; i < 120; i++) { handles[n].Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - begin) * 1000d / System.Diagnostics.Stopwatch.Frequency / 120;
                report.AppendLine($"{names[n]}: {ms:F4} ms/update, {GC.GetAllocatedBytesForCurrentThread() - allocated} bytes / 120 updates");
            }
            Directory.CreateDirectory("Temp/TrailValidation");
            File.WriteAllText("Temp/TrailValidation/performance.txt", report.ToString()); Debug.Log(report.ToString());
        }
    }
}
