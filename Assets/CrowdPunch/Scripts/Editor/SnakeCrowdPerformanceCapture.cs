using System;
using System.IO;
using System.Text;
using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Systems.Physics;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;

namespace CrowdPunch.Editor
{
    public static class SnakeCrowdPerformanceCapture
    {
        [MenuItem("Crowd Punch/Enemies/Benchmark Snake Systems")]
        public static void Run() => Capture();

        public static string Capture()
        {
            using var world = new World("Snake completed-update benchmark");
            var em = world.EntityManager;
            world.SetTime(new TimeData(10,.02f));
            for (int i=0;i<64;i++)
            {
                float3 position = new float3(i%8*20,0,i/8*20);
                var head = em.CreateEntity(typeof(SnakeHead),typeof(SnakeTuning),typeof(LocalTransform),typeof(PhysicsMass),
                    typeof(EnemyLaunchState),typeof(RespawnRequest));
                var mass = new PhysicsMass { InverseMass = 1 };
                em.SetComponentData(head,LocalTransform.FromPosition(position));
                em.SetComponentData(head,new SnakeHead { Initialized=1,Generation=1,AttachedCount=6,DynamicMass=mass });
                em.SetComponentData(head,new SnakeTuning { SegmentCount=6,Spacing=1.5f,OrbRadius=.6f,WaveAmplitude=.5f,WaveFrequencyHz=.7f,PursuitSpeed=3.1f });
                em.SetComponentEnabled<RespawnRequest>(head,false);
                var path=em.AddBuffer<SnakePathPoint>(head);
                for(int p=0;p<128;p++)path.Add(new SnakePathPoint {Position=position+new float3(0,0,(127-p)*.1f)});
                em.AddBuffer<SnakeTailLink>(head);
                for(int j=0;j<6;j++)
                {
                    var orb=em.CreateEntity(typeof(SnakeSegment),typeof(LocalTransform),typeof(PhysicsVelocity),typeof(EnemyLaunchState));
                    em.SetComponentData(orb,LocalTransform.FromPosition(position+new float3(0,0,(j+1)*1.5f)));
                    em.SetComponentData(orb,new SnakeSegment {Head=head,Generation=1,Index=j,Radius=.6f});
                    em.GetBuffer<SnakeTailLink>(head).Add(new SnakeTailLink {Orb=orb});
                }
            }
            // Unrelated bodies prove snake systems query only their own data.
            for(int i=0;i<200;i++)em.CreateEntity(typeof(Enemy),typeof(LocalTransform),typeof(PhysicsVelocity),typeof(EnemyLaunchState));
            var report=new StringBuilder("64 heads + 384 attached orbs + 200 unrelated enemies; 128 path samples/head.\n");
            report.AppendLine("Completed-update microbenchmark, 20 warmups and 120 samples/system, main-thread and job completion. Physics/rendering/time are not advanced; this is not a frame-rate result.");
            Measure(world,world.GetOrCreateSystem<SnakeTailMovementSystem>(),"Tail motor",report);
            Measure(world,world.GetOrCreateSystem<SnakePunchSeverSystem>(),"Sever batch scan",report);
            // Leave one launched orb per head uncleared to measure full occupied-region tests.
            using(var query=em.CreateEntityQuery(typeof(SnakeHead)))
            using(var heads=query.ToEntityArray(Unity.Collections.Allocator.Temp))
                foreach(var h in heads)
                {
                    em.AddComponentData(h,new EnemyContactDamageSettings {ContactRadius=.75f});
                    var orb=em.GetBuffer<SnakeTailLink>(h)[5].Orb;
                    var s=em.GetComponentData<SnakeSegment>(orb);s.Detached=1;em.SetComponentData(orb,s);
                    var pose=em.GetComponentData<LocalTransform>(orb);pose.Position=em.GetComponentData<LocalTransform>(h).Position;
                    em.SetComponentData(orb,pose);
                }
            Measure(world,world.GetOrCreateSystem<SnakeCollisionClearanceSystem>(),"64 spatial clearance tests",report);
            Directory.CreateDirectory("Temp/SnakeValidation");
            File.WriteAllText("Temp/SnakeValidation/performance.txt",report.ToString());
            return report.ToString();
        }
        private static void Measure(World world,SystemHandle system,string name,StringBuilder report)
        {
            var em=world.EntityManager;
            for(int i=0;i<20;i++){system.Update(world.Unmanaged);em.CompleteAllTrackedJobs();}
            long allocated=GC.GetAllocatedBytesForCurrentThread(), start=System.Diagnostics.Stopwatch.GetTimestamp();
            for(int i=0;i<120;i++){system.Update(world.Unmanaged);em.CompleteAllTrackedJobs();}
            double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency/120;
            report.AppendLine($"{name}: {ms:F6} ms/update; {GC.GetAllocatedBytesForCurrentThread()-allocated} managed bytes/120 updates (calling thread)");
        }
    }
}
