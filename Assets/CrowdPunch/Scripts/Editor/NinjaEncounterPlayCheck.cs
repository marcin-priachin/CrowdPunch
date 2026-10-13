using System;
using System.IO;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // Controlled solver/lifecycle check; injected defeat and launch are not a balance playtest.
    [InitializeOnLoad]
    public static class NinjaEncounterPlayCheck
    {
        private const string Output = "Temp/NinjaValidation/live-check.txt";
        private static bool running;
        private static int stage, maximumSlots;
        private static Entity ninja, sequence, target;
        private static double started, preparationAt, arrivalAt;
        private static float3 direction;
        private static uint teleport;
        static NinjaEncounterPlayCheck() => EditorApplication.update += Tick;
        public static void Start()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
            if (!EditorApplication.isPlaying || flow == null || flow.HasCampaign || flow.CurrentLevelIndex != 24)
                throw new InvalidOperationException("Launch legacy Gauntlet_25 first.");
            Directory.CreateDirectory("Temp/NinjaValidation");
            File.WriteAllText(Output, "Controlled Play Mode check with injected launches and wave defeats.\n");
            running = true; stage = 0; maximumSlots = 0; started = EditorApplication.timeSinceStartup;
            preparationAt = arrivalAt = -1;
            flow.RestartCurrentLevel();
            FeedbackTimeController.SetPaused(false);
        }
        private static void Tick()
        {
            if (!running && EditorApplication.isPlaying && SessionState.GetBool("NinjaValidation.StartRequested", false))
            {
                var flow = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
                if (flow != null && !flow.HasCampaign && flow.CurrentLevelIndex == 24 && !FeedbackTimeController.IsSuspended)
                {
                    SessionState.EraseBool("NinjaValidation.StartRequested");
                    Start();
                }
            }
            if (!running || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try { Check(); }
            catch (Exception error) { Record("FAIL " + error); running = false; EditorApplication.isPaused = true; }
        }
        private static void Check()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            if (FeedbackTimeController.IsSuspended && stage < 5) return;
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            var health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (health != null) { health.gameObject.SetActive(true); health.Restore(health.MaxHealth); }
            if (stage <= 2 && PlayerBridgeRegistry.TryGetBridge(out var bridge))
            {
                bridge.transform.SetPositionAndRotation(new Vector3(0, .5f, -10), Quaternion.identity);
                bridge.ClearMovement();
                bridge.PublishPlayerSnapshot(bridge.transform.position, Vector3.forward, bridge.Radius);
                using var playerQuery = em.CreateEntityQuery(typeof(PlayerSnapshot));
                if (playerQuery.CalculateEntityCount() != 1) return;
                var snapshot = playerQuery.GetSingleton<PlayerSnapshot>();
                snapshot.Position = new float3(0, .5f, -10); snapshot.Forward = math.forward();
                em.SetComponentData(playerQuery.GetSingletonEntity(), snapshot);
            }
            var flow = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
            Require(flow.CurrentLevelIndex == 24 && !flow.HasCampaign, "Legacy selection changed campaign state");
            Require(stage == 5 && flow.RunComplete || EditorApplication.timeSinceStartup - started < 90, "Live check timed out at stage " + stage);
            using var ninjas = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<NinjaState>() },
                None = new[] { ComponentType.ReadOnly<RespawnRequest>() } }).ToEntityArray(Allocator.Temp);
            int slots = 0;
            foreach (var e in ninjas)
                if (em.GetComponentData<EnemyLaunchState>(e).Phase == EnemyLaunchPhase.Active && em.GetComponentData<NinjaState>(e).OccupiesAmbushSlot) slots++;
            maximumSlots = System.Math.Max(slots, maximumSlots);
            Require(slots <= 2, "More than two simultaneous ambushes");
            using var sequences = em.CreateEntityQuery(typeof(EnemyWaveSequence));
            if (sequences.CalculateEntityCount() == 0)
            {
                if (stage == 5 && flow.RunComplete) { Record("PASS final legacy completion unloads all encounter state"); Finish(); }
                return;
            }
            sequence = sequences.GetSingletonEntity(); var wave = em.GetComponentData<EnemyWaveSequence>(sequence);
            if (stage == 0)
            {
                if (wave.CurrentWaveIndex != 0 || wave.Phase != EnemyWaveRuntimePhase.AwaitingActivation || ninjas.Length != 1) return;
                ninja = ninjas[0]; Require(wave.SpawnedCount == 7, "First wave composition is not 1+6");
                Require(!em.HasComponent<DasherSettings>(ninja), "Ninja inherited enhanced Dasher combat");
                float baselineHealth = UnityEditor.AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.EnemySpawnSettings>(
                    "Assets/CrowdPunch/Data/Settings/Enemies/EnemySpawnSettings.asset").Health.Max;
                Require(em.GetComponentData<Health>(ninja).Max == baselineHealth, "Baked Ninja health differs from Baseline");
                Record("PASS baked Ninja prefab, matching Baseline health, finite 1+6 first wave"); stage = 1;
            }
            if (stage == 1)
            {
                var n = em.GetComponentData<NinjaState>(ninja);
                double now = world.Time.ElapsedTime;
                if (n.Phase == NinjaPhase.Preparing && preparationAt < 0) preparationAt = now;
                if (n.Phase == NinjaPhase.ArrivalWarning && arrivalAt < 0)
                {
                    arrivalAt = now; teleport = n.TeleportSequence; direction = n.LockedDirection;
                    Require(preparationAt > 0 && now - preparationAt >= .70, "Preparation shorter than .75s within fixed-step observation tolerance");
                    Require(math.length(em.GetComponentData<PhysicsVelocity>(ninja).Linear.xz) < .1f, "Ninja moved during arrival warning");
                    Record("PASS actual preparation, safe teleport, stationary arrival warning and locked direction");
                }
                if (arrivalAt < 0 || n.TeleportSequence != teleport || n.Phase != NinjaPhase.Recovering) return;
                Require(n.LockedDirection.Equals(direction), "Dash steered after teleport");
                Require(now - arrivalAt >= .18, "Arrival warning was skipped");
                Require(n.DistanceTravelled <= 5.1f, "Dash exceeded its maximum distance");
                Require(n.HasHitPlayer != 0, "Natural dash missed stationary player");
                Record("PASS natural ambush hit, locked dash, bounded travel and recovery");
                DefeatCurrentWave(em); stage = 2;
            }
            else if (stage == 2)
            {
                if (wave.CurrentWaveIndex != 1 || wave.Phase != EnemyWaveRuntimePhase.AwaitingActivation || ninjas.Length != 3) return;
                Require(wave.SpawnedCount == 15, "Second wave composition is not 3+12");
                foreach (var e in ninjas)
                {
                    var n = em.GetComponentData<NinjaState>(e); n.ReadyAt = n.PeriodicAt = 0; n.RetryAt = 0; em.SetComponentData(e, n);
                }
                Record("PASS second finite wave begins only after first clears, with 3+12"); stage = 3;
            }
            else if (stage == 3)
            {
                if (maximumSlots < 2) return;
                ninja = ninjas[0];
                var n = em.GetComponentData<NinjaState>(ninja); n.Phase = NinjaPhase.Dashing; n.LockedDirection = math.forward(); n.HasDashSegment = 1;
                em.SetComponentData(ninja, n);
                var pose = em.GetComponentData<LocalTransform>(ninja); pose.Position = new float3(-10, 0, -5); em.SetComponentData(ninja, pose);
                em.SetComponentData(ninja, new PhysicsVelocity());
                using var bodies = em.CreateEntityQuery(typeof(Enemy), typeof(EnemyLaunchState)).ToEntityArray(Allocator.Temp);
                foreach (var body in bodies)
                {
                    if (body == ninja || em.HasComponent<NinjaState>(body) || em.IsComponentEnabled<RespawnRequest>(body)) continue;
                    target = body; break;
                }
                Require(target != Entity.Null, "Missing Baseline chain target");
                var targetPose = em.GetComponentData<LocalTransform>(target); targetPose.Position = new float3(-10, 0, -3); em.SetComponentData(target, targetPose);
                em.SetComponentData(target, new PhysicsVelocity());
                var punch = new PunchSpecification { Origin = new float3(-10, 0, -6), Direction = math.forward(), Range = 3, Radius = 2,
                    Strength = 30, Damage = 20, AffectActive = 1, ApplyDamage = 1, Cause = EnemyLaunchCause.PlayerPunch };
                Require(PunchResolution.TryApply(em, ninja, punch), "Punch failed against committed Ninja");
                Record("PASS two-slot arbitration; injected punch during committed dash to test shared chain physics"); stage = 4;
            }
            else if (stage == 4)
            {
                var n = em.GetComponentData<NinjaState>(ninja);
                Require(!n.OccupiesAmbushSlot, "Punch did not interrupt Ninja ambush");
                if (em.GetComponentData<EnemyLaunchState>(target).LaunchSequence == 0) return;
                var chain = em.GetComponentData<EnemyLaunchState>(target);
                Require(chain.Owner == EnemyLaunchOwner.Player && chain.LaunchDamage == 20, "Ordinary launch chain lost damage/ownership");
                Record("PASS actual Ninja-to-Baseline solver propagation, ordinary launch damage and Player ownership");
                DefeatCurrentWave(em); stage = 5;
            }
            else if (stage == 5 && flow.RunComplete)
            {
                Record("PASS both finite waves complete through existing legacy lifecycle"); Finish();
            }
        }
        public static string ValidateBoundary()
        {
            var world = World.DefaultGameObjectInjectionWorld; var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var prefabs = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<NinjaState>(), ComponentType.ReadOnly<Prefab>() }, Options = EntityQueryOptions.IncludePrefab });
            using var player = em.CreateEntityQuery(typeof(PlayerSnapshot));
            var playerEntity = player.GetSingletonEntity(); var original = player.GetSingleton<PlayerSnapshot>();
            var probe = em.Instantiate(prefabs.GetSingletonEntity());
            try
            {
                var snapshot = original; snapshot.Position = new float3(0, .5f, 20.5f); snapshot.Forward = math.forward();
                em.SetComponentData(playerEntity, snapshot);
                em.SetComponentData(probe, LocalTransform.FromPosition(new float3(0, 0, 14)));
                em.SetComponentData(probe, new NavigationAgent { Radius = .45f });
                em.SetComponentData(probe, new NinjaState { Phase = NinjaPhase.TeleportPending, RandomState = 1 });
                world.GetOrCreateSystem<Systems.Physics.NinjaTeleportSystem>().Update(world.Unmanaged);
                var ninja = em.GetComponentData<NinjaState>(probe);
                Require(ninja.Phase == NinjaPhase.ArrivalWarning, "Physically clear wall-adjacent target failed teleport");
                Require(math.abs(em.GetComponentData<LocalTransform>(probe).Position.z - 18.5f) < .01f, "Boundary teleport moved to incorrect position");
                Record("PASS wall-adjacent target at z=20.5 teleports safely to z=18.5 with full capsule and physical-radius topology");
                return "Baked boundary teleport passed";
            }
            finally
            {
                em.SetComponentData(playerEntity, original); em.DestroyEntity(probe);
                world.GetExistingSystemManaged<Systems.Groups.GamePrePhysicsGroup>().RequestPhysicsRebuild();
            }
        }

        public static string ValidateIncomingDash()
        {
            if (!EditorApplication.isPlaying || !EditorApplication.isPaused) throw new InvalidOperationException("Pause the Ninja preview first.");
            var world = World.DefaultGameObjectInjectionWorld; var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var prefabs = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<NinjaState>(), ComponentType.ReadOnly<Prefab>() }, Options = EntityQueryOptions.IncludePrefab });
            using var sequenceQuery = em.CreateEntityQuery(typeof(EnemyWaveSequence));
            Entity baselinePrefab = Entity.Null;
            foreach (var profile in em.GetBuffer<EnemyWaveProfile>(sequenceQuery.GetSingletonEntity()))
                if (profile.Profile.Archetype == EnemyArchetypeKind.Baseline) { baselinePrefab = profile.Profile.EnemyPrefab; break; }
            var victim = em.Instantiate(prefabs.GetSingletonEntity()); var source = em.Instantiate(baselinePrefab);
            var oldTime = world.Time;
            try
            {
                world.SetTime(new Unity.Core.TimeData(oldTime.ElapsedTime, .02f));
                foreach (var e in new[] { victim, source })
                {
                    em.AddComponentData(e, new EnemyTier { Value = EnemyCombatTier.Normal });
                    em.AddComponentData(e, new CrowdPunch.Components.EnemyArchetype { Value = e == victim ? EnemyArchetypeKind.Ninja : EnemyArchetypeKind.Baseline });
                    em.SetComponentData(e, new Health { Current = 100, Max = 100 });
                    em.SetComponentEnabled<RespawnRequest>(e, false);
                }
                em.SetComponentData(victim, new NavigationAgent { Radius = .45f });
                em.SetComponentData(victim, LocalTransform.FromPosition(new float3(-10, 0, -5)));
                em.SetComponentData(source, LocalTransform.FromPosition(new float3(-10, 0, -2)));
                em.SetComponentData(victim, new NinjaState { Phase = NinjaPhase.Dashing, LockedDirection = math.forward() });
                em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, Owner = EnemyLaunchOwner.Boss, LaunchDamage = 20, LaunchSequence = 1 });
                em.SetComponentData(source, new PhysicsVelocity { Linear = new float3(0, 0, -20) });
                var physics = world.GetExistingSystemManaged<Unity.Physics.Systems.PhysicsSystemGroup>();
                physics.Update(); em.CompleteAllTrackedJobs();
                for (int step = 0; step < 12; step++)
                {
                    world.GetOrCreateSystem<Systems.Movement.NinjaMovementSystem>().Update(world.Unmanaged);
                    world.GetOrCreateSystem<Systems.Physics.ImpactVelocityCaptureSystem>().Update(world.Unmanaged);
                    physics.Update(); em.CompleteAllTrackedJobs();
                    world.GetOrCreateSystem<EnemyLaunchCollisionSystem>().Update(world.Unmanaged);
                    world.GetOrCreateSystem<Systems.Physics.NinjaCollisionSystem>().Update(world.Unmanaged);
                    em.CompleteAllTrackedJobs();
                    if (em.GetComponentData<EnemyLaunchState>(victim).Phase != EnemyLaunchPhase.Launched) continue;
                    var launch = em.GetComponentData<EnemyLaunchState>(victim);
                    Require(!em.GetComponentData<NinjaState>(victim).OccupiesAmbushSlot, "Incoming projectile did not interrupt committed dash");
                    Require(launch.Owner == EnemyLaunchOwner.Boss && launch.LaunchDamage == 20, "Incoming propagation altered ordinary launch data");
                    Record("PASS actual launched Baseline interrupts committed Ninja dash through retained solver contacts, preserving Boss ownership and standard damage");
                    return "Incoming launched-body dash interruption passed";
                }
                throw new InvalidOperationException("Incoming launched body failed to collide with committed Ninja");
            }
            finally
            {
                em.DestroyEntity(victim); em.DestroyEntity(source); world.SetTime(oldTime);
                world.GetExistingSystemManaged<Systems.Groups.GamePrePhysicsGroup>().RequestPhysicsRebuild();
            }
        }

        private static void DefeatCurrentWave(EntityManager em)
        {
            using var bodies = em.CreateEntityQuery(typeof(EnemyWaveOwnership), typeof(EnemyLaunchState)).ToEntityArray(Allocator.Temp);
            foreach (var body in bodies)
            {
                if (em.IsComponentEnabled<RespawnRequest>(body)) continue;
                var health = em.GetComponentData<Health>(body); health.Current = 0; em.SetComponentData(body, health);
                var launch = em.GetComponentData<EnemyLaunchState>(body); launch.Phase = EnemyLaunchPhase.Defeated; em.SetComponentData(body, launch);
                em.SetComponentEnabled<DeathRequest>(body, true);
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Record(string message) => File.AppendAllText(Output, message + "\n");
        private static void Finish() { running = false; EditorApplication.isPaused = true; }
    }
}
