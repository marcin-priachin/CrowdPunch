using System;
using System.IO;
using System.Text;
using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Systems.Physics;
using Unity.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
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
            report.AppendLine("Owner-collision rearming now uses existing collision events; there is no spatial clearance scan.");
            Directory.CreateDirectory("Temp/SnakeValidation");
            File.WriteAllText("Temp/SnakeValidation/performance.txt",report.ToString());
            return report.ToString();
        }
        // Synthetic repeated-contact cost in the baked arena. Exit Play Mode to discard fixtures.
        public static string CaptureCollisions()
        {
            if (!EditorApplication.isPlaying || !EditorApplication.isPaused)
                throw new InvalidOperationException("Pause Gauntlet_24 in Play Mode first.");
            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var prefabs = em.CreateEntityQuery(new EntityQueryDesc {
                All = new ComponentType[] {typeof(SnakeHead),typeof(Prefab)}, Options=EntityQueryOptions.IncludePrefab });
            if(prefabs.CalculateEntityCount()!=1) throw new InvalidOperationException("Expected one baked snake prefab.");
            var prefab=prefabs.GetSingletonEntity();
            using var bodies=new NativeList<Entity>(Allocator.Temp);
            using var heads=new NativeList<Entity>(Allocator.Temp);
            using var projectiles=new NativeList<Entity>(Allocator.Temp);
            for(int i=0;i<264;i++)
            {
                var e=em.Instantiate(prefab); bodies.Add(e);
                if(em.HasComponent<EnemyWaveOwnership>(e))em.RemoveComponent<EnemyWaveOwnership>(e);
                var pose=em.GetComponentData<LocalTransform>(e);
                pose.Position=i<64 ? new float3(-13+i%8*3.7f,0,-13+i/8*3.7f)
                    : new float3(-13+(i-64)%20*1.3f,0,-8+(i-64)/20*1.6f);
                em.SetComponentData(e,pose);
                em.SetComponentEnabled<RespawnRequest>(e,false);
                if(i<64)heads.Add(e);
                else
                {
                    em.RemoveComponent<SnakeHead>(e);em.RemoveComponent<SnakeTuning>(e);
                    em.RemoveComponent<SnakeTailLink>(e);em.RemoveComponent<SnakePathPoint>(e);
                }
            }
            world.GetOrCreateSystem<CrowdPunch.Systems.Initialization.SnakeTailSpawnSystem>().Update(world.Unmanaged);
            using(var commands=new EntityCommandBuffer(Allocator.Temp))
            {
                foreach(var head in heads)
                {
                    var links=em.GetBuffer<SnakeTailLink>(head);
                    foreach(var link in links)bodies.Add(link.Orb);
                    var tip=links[links.Length-1].Orb; projectiles.Add(tip);
                    em.SetComponentData(tip,new EnemyLaunchState {Phase=EnemyLaunchPhase.Launched,Owner=EnemyLaunchOwner.Player,LaunchSequence=1,LaunchDamage=1});
                    var h=em.GetComponentData<SnakeHead>(head);
                    SnakeSevering.Resolve(em,head,ref h,links,commands);em.SetComponentData(head,h);
                    em.SetComponentData(head,new PhysicsMass());
                }
                commands.Playback(em);
            }
            using var poses=new NativeArray<LocalTransform>(bodies.Length,Allocator.Temp);
            var writablePoses=poses;
            for(int i=0;i<bodies.Length;i++)writablePoses[i]=em.GetComponentData<LocalTransform>(bodies[i]);
            var capture=world.GetOrCreateSystem<ImpactVelocityCaptureSystem>();
            var collisions=world.GetOrCreateSystem<EnemyLaunchCollisionSystem>();
            var physics=world.GetExistingSystemManaged<PhysicsSystemGroup>();
            double physicsMs=0,collisionMs=0;int maxArmed=0;
            for(int sample=-20;sample<120;sample++)
            {
                for(int i=0;i<bodies.Length;i++)
                {
                    var e=bodies[i];em.SetComponentData(e,poses[i]);em.SetComponentData(e,new PhysicsVelocity());
                    em.SetComponentData(e,new EnemyLaunchState());em.SetComponentData(e,new Health {Current=10000,Max=10000});
                    em.SetComponentEnabled<DamageRequest>(e,false);
                }
                foreach(var e in projectiles)
                {
                    var segment=em.GetComponentData<SnakeSegment>(e);segment.BouncedOutsideOwner=0;em.SetComponentData(e,segment);
                    em.SetComponentData(e,new EnemyLaunchState {Phase=EnemyLaunchPhase.Launched,Owner=EnemyLaunchOwner.Player,LaunchSequence=1,LaunchDamage=1});
                    em.SetComponentData(e,new PhysicsVelocity {Linear=new float3(20,0,0)});
                }
                capture.Update(world.Unmanaged);em.CompleteAllTrackedJobs();
                long start=System.Diagnostics.Stopwatch.GetTimestamp();physics.Update();em.CompleteAllTrackedJobs();
                double pm=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency;
                start=System.Diagnostics.Stopwatch.GetTimestamp();collisions.Update(world.Unmanaged);em.CompleteAllTrackedJobs();
                double cm=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency;
                if(sample>=0){physicsMs+=pm;collisionMs+=cm;}
                int armed=0;foreach(var e in projectiles)if(em.GetComponentData<SnakeSegment>(e).BouncedOutsideOwner!=0)armed++;
                maxArmed=math.max(maxArmed,armed);
            }
            string report=$"64 heads + 384 orbs + 200 unrelated bodies; 64 launched tips. 20 warmups + 120 samples, reset synthetic poses/launch state per sample.\n"
                +$"Physics build/simulation including contact filter: {physicsMs/120:F6} ms/completed step.\n"
                +$"Collision-event interpretation including external-bounce arming: {collisionMs/120:F6} ms/completed update.\n"
                +$"Maximum externally armed orbs/sample: {maxArmed}. Excludes rendering, reset writes and full gameplay; not frame-rate/target-hardware acceptance.\n";
            Directory.CreateDirectory("Temp/SnakeValidation");File.WriteAllText("Temp/SnakeValidation/collision-performance.txt",report);
            return report;
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
