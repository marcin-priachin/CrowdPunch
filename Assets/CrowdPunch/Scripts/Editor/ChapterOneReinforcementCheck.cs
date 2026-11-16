using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // LOOP-011: controlled baked-world checks, not a combat balance playtest.
    [InitializeOnLoad]
    public static class ChapterOneReinforcementCheck
    {
        private const string Key = "CrowdPunch.ChapterOneReinforcementCheck";
        private const string Output = "Temp/ChapterOneRevision/live-check.txt";
        private static int level, wave, spawned, observedWaves, screenshotFrame;
        private static double entered, firstBatchAt, awaitingAt, finalWaitAt, sampleAt;
        private static bool retried, overlap, sampled, introCaptured;
        private static Entity sentinel;
        private static readonly List<float> frames = new();

        static ChapterOneReinforcementCheck() => EditorApplication.update += Tick;

        public static void Start(int firstLevel = 1)
        {
            if (firstLevel < 1 || firstLevel > 10) throw new ArgumentOutOfRangeException(nameof(firstLevel));
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Start in Edit mode.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes first.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            string heading = "LOOP-011: isolated save, injected defeats/objective completion. " +
                "Natural first batch intervals at 2/8; other waits shortened. Not duration/balance acceptance.\n";
            if (firstLevel == 1) File.WriteAllText(Output, heading);
            else File.AppendAllText(Output, $"RESUME from {firstLevel}: " + heading);
            SessionState.SetInt(Key + ".FirstLevel", firstLevel);
            level = 0; observedWaves = 0; retried = false;
            SessionState.EraseString(GauntletSequence.EditorStartLevelKey);
            SessionState.EraseBool(GauntletSequence.EditorLegacyKey);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CampaignBuildRegistration.BootstrapPath);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        public static void Stop() => SessionState.SetBool(Key, false);

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try { Inspect(); }
            catch (Exception error)
            {
                Record("FAIL " + error); Stop(); EditorApplication.isPaused = true; Debug.LogException(error);
            }
        }

        private static void Inspect()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
            var world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || !world.IsCreated || flow.TransitionInProgress) return;
            if (level == 0)
            {
                var progress = new CampaignProgress(Path.GetFullPath("Temp/ChapterOneRevision/progress-" + Guid.NewGuid() + ".json"));
                foreach (var item in flow.Campaign.levels) progress.Complete(item.id);
                typeof(GauntletSequence).GetField("<Progress>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, progress);
                level = SessionState.GetInt(Key + ".FirstLevel", 1); Enter(flow); return;
            }
            Require(EditorApplication.timeSinceStartup - entered < 180, "Timed out at level " + level);
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (player != null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            Require(!flow.RunFailed, "Unexpected failure at " + level);
            if (flow.Screen == CampaignScreen.ReplayComplete)
            {
                Record("PASS completion " + level);
                if (++level > 10)
                {
                    int first = SessionState.GetInt(Key + ".FirstLevel", 1);
                    int expected = new[] { 3, 2, 2, 1, 2, 1, 1, 4, 2, 1 }.Skip(first - 1).Sum() + (first <= 5 ? 1 : 0);
                    Require(observedWaves == expected, "Unexpected observed phase count: " + observedWaves);
                    Record($"PASS levels {first}-10, {observedWaves} observed phases including retry when applicable. COMPLETE.");
                    Stop(); flow.ReturnToMenu(); return;
                }
                Enter(flow); return;
            }
            if (flow.Screen != CampaignScreen.Playing) return;
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(typeof(EnemyWaveSequence));
            if (query.CalculateEntityCount() == 0) return;
            Require(query.CalculateEntityCount() == 1, "Sequence leaked between levels");
            var owner = query.GetSingletonEntity();
            var sequence = em.GetComponentData<EnemyWaveSequence>(owner);
            var definitions = em.GetBuffer<EnemyWaveDefinition>(owner);
            Require(sequence.Phase != EnemyWaveRuntimePhase.Invalid, "Invalid baked phase");
            if (sequence.CurrentWaveIndex >= definitions.Length) return;
            var definition = definitions[sequence.CurrentWaveIndex];
            if (wave != sequence.CurrentWaveIndex)
            {
                wave = sequence.CurrentWaveIndex; spawned = 0; firstBatchAt = awaitingAt = 0; observedWaves++;
                Require(definition.IsValid == 1 && definition.BatchSize == (level == 8 ? 8 : 4)
                    && definition.BatchInterval == 6, "Wrong baked cadence");
                bool timed = (level == 2 || level == 8 || level == 9) && wave < definitions.Length - 1;
                Require(definition.ActivationMode == (byte)(timed ? EnemyWaveActivationMode.DurationElapsed
                    : EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated), "Wrong completion gate");
                if (wave == definitions.Length - 1)
                    Require(definition.ActivationMode == (byte)EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated,
                        "Final phase must include earlier survivors");
                if (wave > 0 && sentinel != Entity.Null && em.Exists(sentinel))
                {
                    overlap = true; Record($"PASS {level}.{wave + 1}: earlier survivor remains after timed transition");
                }
                Record($"PHASE {level}.{wave + 1}: baked count={definition.TotalEnemyCount}; gate={definition.ActivationMode}");
            }
            double now = world.Time.ElapsedTime;
            if (sequence.SpawnedCount != spawned)
            {
                int delta = sequence.SpawnedCount - spawned;
                Require(delta > 0 && delta <= definition.BatchSize, "Batch exceeded authored size");
                if (spawned == 0) firstBatchAt = now;
                else if (wave == 0 && (level == 2 || level == 8) && spawned == definition.BatchSize)
                {
                    Require(now - firstBatchAt >= 5.9, "Second batch arrived early");
                    Record($"PASS {level}: natural first batch interval {now - firstBatchAt:0.00}s");
                }
                spawned = sequence.SpawnedCount;
            }
            using var rootsQuery = em.CreateEntityQuery(typeof(EnemyWaveOwnership), typeof(EnemyLaunchState), typeof(Health));
            using var roots = rootsQuery.ToEntityArray(Allocator.Temp);
            foreach (var e in roots)
            {
                var ownership = em.GetComponentData<EnemyWaveOwnership>(e);
                Require(ownership.Sequence == owner && ownership.RunGeneration == sequence.RunGeneration, "Stale ownership after retry/unload");
                if ((level == 2 || level == 8 || level == 9) && sentinel == Entity.Null && ownership.WaveIndex == 0
                    && em.GetComponentData<EnemyLaunchState>(e).Phase == EnemyLaunchPhase.Active) sentinel = e;
            }
            // Warning reservations can materialize after the next timed phase
            // starts, especially when the probe shortens clocks. Observe ownership
            // after activation rather than requiring a visible body on that boundary.
            if (!overlap && sentinel != Entity.Null && em.Exists(sentinel)
                && em.GetComponentData<EnemyWaveOwnership>(sentinel).WaveIndex < wave)
            {
                overlap = true; Record($"PASS {level}: earlier-phase survivor active during reinforcement");
            }
            if (level == 8 && !sampled && roots.Length >= 96)
            {
                if (sampleAt == 0) sampleAt = EditorApplication.timeSinceStartup;
                frames.Add(Time.unscaledDeltaTime * 1000);
                if (EditorApplication.timeSinceStartup - sampleAt < 3) return;
                frames.Sort();
                Record($"OBSERVED large mixed crowd: {roots.Length} roots, editor frame median={frames[frames.Count / 2]:0.00}ms; rendering/tool overhead included.");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/ChapterOneRevision/level08.png"));
                sampled = true; screenshotFrame = Time.frameCount;
                return;
            }
            if (sampled && Time.frameCount <= screenshotFrame + 3) return;
            bool final = wave == definitions.Length - 1;
            if (level == 8 && sampled && !final)
                foreach (var e in roots) if (e != sentinel) Defeat(em, e);

            // Preserve two natural batches for cadence evidence. All other clocks
            // can be shortened; settings/assets themselves are never changed.
            bool natural = wave == 0 && (level == 2 || level == 8) && sequence.SpawnedCount <= definition.BatchSize;
            if (!natural && (sequence.Phase == EnemyWaveRuntimePhase.PreWaveDelay
                || sequence.Phase == EnemyWaveRuntimePhase.Spawning
                || sequence.Phase == EnemyWaveRuntimePhase.AwaitingActivation && !final
                    && definition.ActivationMode == (byte)EnemyWaveActivationMode.DurationElapsed))
            {
                sequence.NextActionAt = now; em.SetComponentData(owner, sequence);
            }
            if (sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation) return;
            if (awaitingAt == 0) awaitingAt = now;
            if (level == 5 && wave == 0)
            {
                var mushroom = roots.FirstOrDefault(e => em.HasComponent<CrowdPunch.Components.MushroomShooterSettings>(e)
                    && em.GetComponentData<EnemyLaunchState>(e).Phase == EnemyLaunchPhase.Active);
                if (mushroom == Entity.Null) return;
                Require(em.GetComponentData<Health>(mushroom).Current == em.GetComponentData<Health>(mushroom).Max, "Mushroom retry health");
                if (!retried)
                {
                    Record("PASS baked active Mushroom Shooter; retrying introduction");
                    retried = true; wave = -1; flow.RestartCurrentLevel(); return;
                }
                if (now - awaitingAt < 1) return;
                if (!introCaptured)
                {
                    introCaptured = true; screenshotFrame = Time.frameCount;
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/ChapterOneRevision/level05.png"));
                    return;
                }
                if (Time.frameCount <= screenshotFrame + 3) return;
            }
            if (now - awaitingAt < 1) return;
            if (level == 4 || level == 6 || level == 7)
            {
                var target = em.GetComponentData<BarricadeCrowdSequence>(owner).Barricade;
                var wall = em.GetComponentData<Barricade>(target); wall.HitsRemaining = 0; em.SetComponentData(target, wall);
                if (wall.CompleteOnDestruction == 0)
                    player.GetComponent<PlayerController>().SetLevelEntryPoint(wall.ExitPosition, Quaternion.identity);
                return;
            }
            if (level == 10)
            {
                using var bosses = em.CreateEntityQuery(typeof(BossEncounter));
                var boss = bosses.GetSingletonEntity(); var state = em.GetComponentData<BossEncounter>(boss);
                state.Cycle = BossCycle.Defeated; em.SetComponentData(boss, state); return;
            }
            if (!final && definition.ActivationMode == (byte)EnemyWaveActivationMode.DurationElapsed) return;
            foreach (var e in roots) if (e != sentinel) Defeat(em, e);
            if (final && sentinel != Entity.Null && sequence.UndefeatedCount > 1 && now - awaitingAt > 2)
            {
                // Crowd push can leave this stationary test player on a warning
                // reservation. Move to clear ground so safe activation can proceed.
                player.GetComponent<PlayerController>().SetLevelEntryPoint(new Vector3(0, .5f, -5), Quaternion.identity);
            }
            if (final && sentinel != Entity.Null && em.Exists(sentinel) && sequence.UndefeatedCount == 1)
            {
                Require(overlap && !em.IsComponentEnabled<EnemyWaveEncounterComplete>(owner), "Earlier survivor did not block final clear");
                if (finalWaitAt == 0) finalWaitAt = now;
                if (now - finalWaitAt < .5) return;
                Record("PASS earlier survivor blocks final completion at " + level);
                Defeat(em, sentinel);
                sentinel = Entity.Null;
            }
        }

        private static void Enter(GauntletSequence flow)
        {
            wave = -1; sentinel = Entity.Null; overlap = sampled = introCaptured = false;
            finalWaitAt = sampleAt = 0; frames.Clear(); entered = EditorApplication.timeSinceStartup;
            flow.SelectLevel(level - 1);
        }

        private static void Defeat(EntityManager em, Entity enemy)
        {
            var health = em.GetComponentData<Health>(enemy); health.Current = 0; em.SetComponentData(enemy, health);
            var launch = em.GetComponentData<EnemyLaunchState>(enemy); launch.Phase = EnemyLaunchPhase.Defeated; em.SetComponentData(enemy, launch);
            em.SetComponentEnabled<DeathRequest>(enemy, true);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Record(string message) => File.AppendAllText(Output, message + "\n");
    }
}
