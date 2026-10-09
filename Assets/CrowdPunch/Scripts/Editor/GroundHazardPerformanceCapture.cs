using System;
using System.IO;
using System.Text;
using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static class GroundHazardPerformanceCapture
    {
        public static string Run()
        {
            // Editor Burst compilation is asynchronous by default. Complete it before warmup
            // so this report measures compiled execution rather than the managed fallback.
            bool previous = Unity.Burst.BurstCompiler.Options.EnableBurstCompileSynchronously;
            try { Unity.Burst.BurstCompiler.Options.EnableBurstCompileSynchronously = true; return Capture(); }
            finally { Unity.Burst.BurstCompiler.Options.EnableBurstCompileSynchronously = previous; }
        }
        private static string Capture()
        {
            var report = new StringBuilder();
            report.AppendLine($"Unity {Application.unityVersion}; isolated ECS CPU measurements; {SystemInfo.processorType}; {SystemInfo.graphicsDeviceName}.");
            report.AppendLine("Synchronous Burst warmup with safety checks enabled. Four mixed-shape active patches, frozen actor positions, opposing travel goals, cached and queued routes. 20 warmups + 120 completed updates per system; includes allocations and resolution, excludes physics/GPU/whole-frame cost.");
            foreach (int count in new[] { 500,2000 }) Measure(count,report);
            Directory.CreateDirectory("Temp/GroundHazardValidation"); File.WriteAllText("Temp/GroundHazardValidation/performance.txt",report.ToString());
            return report.ToString();
        }
        private static void Measure(int count, StringBuilder report)
        {
            using var world = new World("GROUND performance " + count); var em = world.EntityManager;
            using var obstacles = new NativeArray<NavigationRectangle>(0,Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-16),new float2(16),1,new float3(.62f,.97f,1.62f),obstacles,Allocator.Persistent);
            var arena = em.CreateEntity(typeof(NavigationGrid),typeof(NavigationRuntimeSettings),typeof(GroundHazardPolicy));
            em.SetComponentData(arena,new NavigationGrid { Data = blob });
            em.SetComponentData(arena,new NavigationRuntimeSettings { GlobalBudget = 512,SearchLimit = 4096,ConcurrentSearches = 4,
                MaxRequests = 4096,MaxPathLength = 128,FailureDelay = 3,DestinationThreshold = 1.5f,WaypointArrival = .25f,
                SmoothingChecks = 8,LookAhead = .7f,StuckDuration = 2.5f,MinimumProgress = .3f });
            for (int i = 0; i < 4; i++)
            {
                var e = em.CreateEntity(typeof(GroundHazard),typeof(GroundHazardState));
                em.SetComponentData(e,new GroundHazard { Position = new float3((i % 2 == 0 ? -1 : 1)*4,0,(i < 2 ? -1 : 1)*4),
                    HalfSize = new float2(2,3),Radius = 2.5f,Shape = i % 2 == 0 ? GroundHazardShape.Rectangle : GroundHazardShape.Circle,Damage = 12,DamageInterval = .75f });
                em.SetComponentData(e,new GroundHazardState { Introduced = 1,Phase = GroundHazardPhase.Active });
            }
            var archetype = em.CreateArchetype(typeof(Enemy),typeof(LocalTransform),typeof(NavigationAgent),typeof(NavigationIntent),typeof(DesiredMovement),
                typeof(GroundHazardRoute),typeof(EnemyLaunchState),typeof(RespawnRequest),typeof(EnemyMovementSettings),typeof(GroundHazardWaypoint),
                typeof(Health),typeof(EnemyLifetime),typeof(GroundHazardDamageClock),typeof(EnemyDamageState),typeof(DamageRequest),typeof(DeathRequest),typeof(EnemyHealthBarVisibility));
            using var enemies = new NativeArray<Entity>(count,Allocator.Temp); em.CreateEntity(archetype,enemies);
            for (int i = 0; i < count; i++)
            {
                float3 p = new float3(-13 + (i % 40) * (26f/39),0,-13 + (i / 40) * (26f/math.max(1,(count+39)/40-1)));
                var e = enemies[i]; em.SetComponentData(e,LocalTransform.FromPosition(p)); em.SetComponentData(e,new NavigationAgent { Radius = .5f });
                em.SetComponentData(e,new EnemyMovementSettings { BrakingAcceleration = 10 });
                em.SetComponentData(e,NavigationIntent.Travel(-p,3,.25f,float3.zero));
                em.SetComponentData(e,new DesiredMovement { Direction = math.normalizesafe(-p),Speed = 3 });
                em.SetComponentData(e,new Health { Current = 10000,Max = 10000 }); em.SetComponentData(e,new EnemyLifetime { Generation = 1 });
                em.SetComponentEnabled<RespawnRequest>(e,false); em.SetComponentEnabled<DamageRequest>(e,false); em.SetComponentEnabled<DeathRequest>(e,false);
            }
            var navigation = world.GetOrCreateSystem<GroundHazardNavigationSystem>();
            var damage = world.GetOrCreateSystem<GroundHazardDamageSystem>();
            foreach (var entry in new[] { (Name:"Navigation",Handle:navigation),(Name:"Damage",Handle:damage) })
            {
                for (int i = 0; i < 20; i++) { world.SetTime(new TimeData(i*.02,.02f)); entry.Handle.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                long allocations = GC.GetAllocatedBytesForCurrentThread(); var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 120; i++) { world.SetTime(new TimeData((i+20)*.02,.02f)); entry.Handle.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                watch.Stop(); report.AppendLine($"{count} enemies, {entry.Name}: {watch.Elapsed.TotalMilliseconds/120:F4} ms/update; {GC.GetAllocatedBytesForCurrentThread()-allocations} managed bytes/120 updates.");
            }
        }
    }
}
