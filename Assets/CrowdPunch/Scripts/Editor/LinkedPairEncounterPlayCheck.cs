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
    [InitializeOnLoad]
    public static class LinkedPairEncounterPlayCheck
    {
        private const string Output = "Temp/LinkedPairValidation/live-check.txt";
        private static bool running; private static int stage; private static double started, stepAt;
        private static Entity pair, target; private static float2 initialSpan;
        static LinkedPairEncounterPlayCheck() => EditorApplication.update += Tick;
        public static void Start() {
            var flow = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
            if (!EditorApplication.isPlaying || flow == null || flow.HasCampaign || flow.CurrentLevelIndex != 26) throw new InvalidOperationException("Launch legacy Gauntlet_27 first.");
            Directory.CreateDirectory("Temp/LinkedPairValidation"); File.WriteAllText(Output, "LINK-001..007: controlled baked-world checks with injected punches, stops and defeats.\n");
            running = true; stage = 0; started = EditorApplication.timeSinceStartup;
            flow.RestartCurrentLevel(); FeedbackTimeController.SetPaused(false); EditorApplication.isPaused = false;
        }
        private static void Tick() {
            if (!running || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try { Check(); } catch (Exception e) { Record("FAIL " + e); running = false; EditorApplication.isPaused = true; Debug.LogException(e); }
        }
        private static void Check() {
            Require(EditorApplication.timeSinceStartup - started < 150, "Timed out at stage " + stage);
            var flow = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>(); var world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || flow.TransitionInProgress) return;
            var playerHealth = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (playerHealth != null) { playerHealth.gameObject.SetActive(true); playerHealth.Restore(playerHealth.MaxHealth); }
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var sq = em.CreateEntityQuery(typeof(EnemyWaveSequence)); using var pq = em.CreateEntityQuery(typeof(LinkedPair));
            if (stage == 10 && flow.RunComplete) { Require(pq.CalculateEntityCount() == 0, "Record survived unload"); Record("PASS final completion and scene cleanup"); running = false; EditorApplication.isPaused = true; return; }
            if (sq.CalculateEntityCount() != 1) return;
            var sequence = sq.GetSingleton<EnemyWaveSequence>(); using var records = pq.ToEntityArray(Allocator.Temp);
            using var eq = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<EnemyWaveOwnership>() }, None = new[] { ComponentType.ReadOnly<RespawnRequest>() } });
            using var bodies = eq.ToEntityArray(Allocator.Temp);
            if (stage == 0) {
                if (sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation || records.Length != 1) return;
                pair = records[0]; var p = em.GetComponentData<LinkedPair>(pair);
                if (!em.IsEnabled(p.A) || !em.IsEnabled(p.B) || em.GetComponentData<EnemyGroundConstraint>(p.A).IsLocked == 0) return;
                if (p.Phase != EnemyLaunchPhase.Active) return; // Ground Spawn finishes before the controlled punch.
                Require(sequence.SpawnedCount == 8, "First wave is not 2+6");
                target = Entity.Null;
                foreach (var e in bodies) if (!em.HasComponent<LinkedPairEndpoint>(e)) { target = e; break; }
                Require(target != Entity.Null, "No Baseline body");
                Move(em, p.A, new float3(-10, 0, 0)); Move(em, p.B, new float3(-10, 0, -4)); Move(em, target, new float3(-10, 0, 3));
                var pose = em.GetComponentData<LocalTransform>(p.A).Position;
                Require(PunchResolution.TryApply(em, p.A, new PunchSpecification { Origin = pose - math.forward(), Direction = math.forward(),
                    Range = 2, Radius = .5f, Strength = 22, Damage = 0, EventTime = world.Time.ElapsedTime,
                    Cause = EnemyLaunchCause.PlayerPunch, AffectActive = 1 }), "Initial punch failed");
                Record("PASS exact 1 pair + 6 Baselines and baked Goblin endpoints"); Next(world, 1);
            } else if (stage == 1) {
                var p = em.GetComponentData<LinkedPair>(pair);
                Require(em.GetComponentData<EnemyLaunchState>(p.A).Phase == EnemyLaunchPhase.Launched && em.GetComponentData<EnemyLaunchState>(p.B).Phase == EnemyLaunchPhase.Launched, "Pair split launch phases");
                var hit = em.GetComponentData<EnemyLaunchState>(target);
                if (hit.LastPropagationImpulse <= 0 && world.Time.ElapsedTime - stepAt < 1.5) return;
                Require(hit.LastPropagationImpulse > 0 && hit.Owner == EnemyLaunchOwner.Player, "Real endpoint-body collision did not propagate");
                Record("PASS simultaneous pair flight and real body solver propagation");
                float3 position = em.GetComponentData<LocalTransform>(p.B).Position;
                initialSpan = em.GetComponentData<LocalTransform>(p.A).Position.xz - position.xz;
                Require(PunchResolution.TryApply(em, p.B, new PunchSpecification { Origin = position - new float3(1, 0, 0), Direction = new float3(1, 0, 0),
                    Range = 2, Radius = .5f, Strength = 15, Damage = 0, EventTime = world.Time.ElapsedTime,
                    Cause = EnemyLaunchCause.PlayerPunch, AffectLaunched = 1 }), "Repunch failed"); Next(world, 2);
            } else if (stage == 2) {
                if (world.Time.ElapsedTime - stepAt < .4) return;
                var p = em.GetComponentData<LinkedPair>(pair); Require(p.Leader == p.B, "Repunch failed leader switch");
                float2 span = em.GetComponentData<LocalTransform>(p.A).Position.xz - em.GetComponentData<LocalTransform>(p.B).Position.xz;
                Require(math.abs(math.normalizesafe(span).x - math.normalizesafe(initialSpan).x) > .05f, "Partner did not visibly swing");
                Record("PASS repunch switches leader and relative tether angle changes in real physics");
                em.SetComponentData(p.A, new PhysicsVelocity()); em.SetComponentData(p.B, new PhysicsVelocity()); Next(world, 3);
            } else if (stage == 3) {
                var p = em.GetComponentData<LinkedPair>(pair);
                em.SetComponentData(p.A, new PhysicsVelocity()); em.SetComponentData(p.B, new PhysicsVelocity());
                if (p.Phase == EnemyLaunchPhase.Launched) return;
                Require(p.Phase == EnemyLaunchPhase.Recovering, "Flight did not enter shared recovery");
                Require(em.GetComponentData<EnemyLaunchState>(p.A).Phase == EnemyLaunchPhase.Recovering && em.GetComponentData<EnemyLaunchState>(p.B).Phase == EnemyLaunchPhase.Recovering, "Recovery split");
                Record("PASS shared settling/recovery retains tether"); Defeat(em, bodies); Next(world, 4);
            } else if (stage == 4) {
                if (sequence.CurrentWaveIndex != 1 || sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation || records.Length != 2 || bodies.Length != 16) return;
                Require(sequence.SpawnedCount == 16, "Second wave is not 4+12"); Record("PASS exact 2 pairs + 12 Baselines"); flow.RestartCurrentLevel(); Next(world, 5);
            } else if (stage == 5) {
                if (sequence.CurrentWaveIndex != 0 || sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation || records.Length != 1 || bodies.Length != 8) return;
                var p = em.GetComponentData<LinkedPair>(records[0]);
                if (em.HasComponent<EnemySpawnAnimation>(p.A) && em.IsComponentEnabled<EnemySpawnAnimation>(p.A)) return;
                Require(p.Phase == EnemyLaunchPhase.Active && em.GetBuffer<LinkedPairClock>(records[0]).Length == 0, "Retry retained flight/clock");
                Require(em.GetComponentData<Health>(p.A).Current == em.GetComponentData<Health>(p.A).Max && em.GetComponentData<Health>(p.B).Current == em.GetComponentData<Health>(p.B).Max, "Retry missing health");
                Record("PASS retry restores intact healthy pairs and clears clocks"); Defeat(em, bodies); Next(world, 6);
            } else if (stage == 6) {
                if (sequence.CurrentWaveIndex != 1 || sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation || records.Length != 2 || bodies.Length != 16) return;
                Defeat(em, bodies); Next(world, 10);
            }
        }
        private static void Move(EntityManager em, Entity e, float3 p) { em.SetComponentData(e, LocalTransform.FromPosition(p)); em.SetComponentData(e, new PhysicsVelocity()); }
        private static void Defeat(EntityManager em, NativeArray<Entity> bodies) { foreach (var e in bodies) { em.SetComponentData(e, new Health()); var l = em.GetComponentData<EnemyLaunchState>(e); l.Phase = EnemyLaunchPhase.Defeated; em.SetComponentData(e, l); em.SetComponentEnabled<DeathRequest>(e, true); } }
        private static void Next(World world, int next) { stage = next; stepAt = world.Time.ElapsedTime; }
        private static void Require(bool valid, string text) { if (!valid) throw new InvalidOperationException(text); }
        private static void Record(string text) => File.AppendAllText(Output, text + "\n");
    }
}
