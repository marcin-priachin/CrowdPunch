using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // LOOP-009: real baked-world integration with injected launches/defeats and
    // an isolated save. This is not a balance or encounter-duration playtest.
    [InitializeOnLoad]
    public static class CampaignEnemyEncounterCheck
    {
        private const string Key = "CrowdPunch.CampaignEnemyEncounterCheck";
        private const string Output = "Temp/CampaignEnemyValidation/live-check.txt";
        private static readonly int[] Levels = { 41, 43, 45, 48, 52, 55, 58, 61, 63, 65, 67, 68, 71, 73, 78 };
        private static int index = -1, wave = -1, checkedWaves, initialFrame, clearingFrame, frames;
        private static bool retried, retryPending, launched, clearing;
        private static double entered, observed;
        private static Entity launchedBody;
        private static readonly List<float> frameMilliseconds = new();

        static CampaignEnemyEncounterCheck() => EditorApplication.update += Tick;

        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Start in Edit mode.");
            index = -1; wave = -1; checkedWaves = frames = 0;
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllText(Output, "LOOP-009/LINK/DRAGON/SNAKE/NINJA controlled campaign integration; injected launches/defeats; isolated save.\n");
            SessionState.SetBool(Key, true);
            SessionState.EraseString(GauntletSequence.EditorStartLevelKey);
            SessionState.EraseBool(GauntletSequence.EditorLegacyKey);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CampaignBuildRegistration.BootstrapPath);
            EditorApplication.isPlaying = true;
        }

        public static void Stop() => SessionState.SetBool(Key, false);

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try { Inspect(); }
            catch (Exception error) { Record("FAIL " + error); Stop(); EditorApplication.isPaused = true; Debug.LogException(error); }
        }

        private static void Inspect()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
            var world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || !world.IsCreated || flow.TransitionInProgress) return;
            if (index < 0)
            {
                var progress = new CampaignProgress(Path.GetFullPath("Temp/CampaignEnemyValidation/progress-" + Guid.NewGuid() + ".json"));
                for (int n = 0; n < flow.Campaign.Count; n++) progress.Complete(flow.Campaign.Get(n).id);
                typeof(GauntletSequence).GetField("<Progress>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, progress);
                index = 0; Enter(flow); return;
            }
            Require(EditorApplication.timeSinceStartup - entered < 180, "Level timed out: " + Levels[index]);
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (player != null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            Require(!flow.RunFailed, "Unexpected campaign failure");
            if (flow.Screen == CampaignScreen.ReplayComplete)
            {
                Record("PASS completion " + Levels[index]);
                index++;
                if (index == Levels.Length)
                {
                    Record($"PASS all {Levels.Length} revised encounters, {checkedWaves} waves; advancing frames={frames}.");
                    Stop(); EditorApplication.isPaused = true; return;
                }
                Enter(flow); return;
            }
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var sequences = em.CreateEntityQuery(typeof(EnemyWaveSequence));
            if (sequences.CalculateEntityCount() != 1) return;
            var owner = sequences.GetSingletonEntity();
            var sequence = em.GetComponentData<EnemyWaveSequence>(owner);
            Require(sequence.Phase != EnemyWaveRuntimePhase.Invalid, "Invalid baked wave");
            var definitions = em.GetBuffer<EnemyWaveDefinition>(owner);
            if (wave < 0) ValidateBaking(em, owner, definitions);
            // Asset tests verify the three-second cadence. Keep the first intro
            // delay natural; shorten later idle waits in this controlled probe.
            if (sequence.Phase == EnemyWaveRuntimePhase.PreWaveDelay && sequence.Initialized != 0
                && (!IsIntroduction() || retried || sequence.CurrentWaveIndex > 0))
            {
                sequence.NextActionAt = world.Time.ElapsedTime;
                em.SetComponentData(owner, sequence);
            }
            if (sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation) return;
            var definition = definitions[sequence.CurrentWaveIndex];
            Require(sequence.SpawnedCount == definition.TotalEnemyCount + definition.TotalEliteCount, "Incomplete authored spawn count");
            using var roots = em.CreateEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<EnemyWaveOwnership>(), ComponentType.ReadOnly<EnemyLaunchState>() },
                None = new[] { ComponentType.ReadOnly<RespawnRequest>() }
            }).ToEntityArray(Allocator.Temp);
            // Intact walking crowds can occupy pending warning points. Ordinary
            // combat clears them; the probe must likewise keep clearing active
            // occupants instead of waiting for all reservations simultaneously.
            if (retryPending && roots.Length < sequence.UndefeatedCount) return;
            if (wave != sequence.CurrentWaveIndex)
            {
                wave = sequence.CurrentWaveIndex; observed = EditorApplication.timeSinceStartup; initialFrame = Time.frameCount;
                frameMilliseconds.Clear(); launched = false; clearing = false;
                if (retryPending)
                {
                    foreach (var e in roots)
                    {
                        var health = em.GetComponentData<Health>(e);
                        Require(health.Current == health.Max, "Retry did not restore health");
                        Require(em.GetComponentData<EnemyLaunchState>(e).Phase == EnemyLaunchPhase.Active, "Retry retained launch/recovery");
                        if (em.HasComponent<SnakeHead>(e)) Require(em.GetComponentData<SnakeHead>(e).AttachedCount == 6, "Retry did not restore the full tail");
                    }
                    using var links = em.CreateEntityQuery(typeof(LinkedPair)).ToEntityArray(Allocator.Temp);
                    foreach (var link in links) Require(em.GetBuffer<LinkedPairClock>(link).Length == 0, "Retry retained tether clocks");
                    Record("PASS retry/full health and clean special state " + Levels[index]); retryPending = false;
                }
                else checkedWaves++;
                Record($"WAVE {Levels[index]}.{wave + 1}: baked clearance={definition.TotalEnemyCount + definition.TotalEliteCount}, pairs={definition.LinkedPairCount}; spawned={sequence.SpawnedCount}.");
            }
            if (!clearing) frameMilliseconds.Add(Time.unscaledDeltaTime * 1000);
            using var ninjas = em.CreateEntityQuery(typeof(NinjaState)).ToComponentDataArray<NinjaState>(Allocator.Temp);
            Require(ninjas.Count(n => n.OccupiesAmbushSlot) <= 2, "Ninja concurrency exceeded two");
            if (IsIntroduction() && wave == 0 && !retried)
            {
                if (!launched)
                {
                    launchedBody = roots.FirstOrDefault(e => em.HasComponent<LinkedPairEndpoint>(e) || em.HasComponent<DragonTuning>(e)
                        || em.HasComponent<SnakeHead>(e) || em.HasComponent<NinjaState>(e));
                    // Wave spawn accounting includes pending visual warnings. Wait
                    // for the special roots to become active before probing combat.
                    if (launchedBody == Entity.Null) return;
                    if (em.HasComponent<SnakeHead>(launchedBody))
                    {
                        var head = em.GetComponentData<SnakeHead>(launchedBody);
                        if (head.Initialized == 0 || head.AttachedCount != 6) return;
                        launchedBody = em.GetBuffer<SnakeTailLink>(launchedBody)[head.AttachedCount - 1].Orb;
                    }
                    var position = em.GetComponentData<LocalTransform>(launchedBody).Position;
                    Require(PunchResolution.TryApply(em, launchedBody, new PunchSpecification {
                        Origin = position - math.forward(), Direction = math.forward(), Range = 2, Radius = .5f,
                        Strength = 22, Damage = 0, EventTime = world.Time.ElapsedTime,
                        Cause = EnemyLaunchCause.PlayerPunch, AffectActive = 1
                    }), "Introductory special body rejected an eligible punch");
                    launched = true; initialFrame = Time.frameCount; return;
                }
                if (Time.frameCount <= initialFrame + 2) return;
                Require(em.GetComponentData<EnemyLaunchState>(launchedBody).Phase == EnemyLaunchPhase.Launched, "Introductory body did not launch");
                if (em.HasComponent<LinkedPairEndpoint>(launchedBody))
                {
                    var pair = em.GetComponentData<LinkedPair>(em.GetComponentData<LinkedPairEndpoint>(launchedBody).Pair);
                    Require(em.GetComponentData<EnemyLaunchState>(pair.A).Phase == EnemyLaunchPhase.Launched
                        && em.GetComponentData<EnemyLaunchState>(pair.B).Phase == EnemyLaunchPhase.Launched, "Partner did not launch");
                }
                if (em.HasComponent<SnakeSegment>(launchedBody))
                {
                    var segment = em.GetComponentData<SnakeSegment>(launchedBody);
                    Require(segment.Detached == 1 && em.GetComponentData<SnakeHead>(segment.Head).AttachedCount == 5, "Tip punch did not remove exactly one orb");
                }
                Record("PASS special launch and real simulation " + Levels[index]);
                retried = true; retryPending = true; wave = -1; flow.RestartCurrentLevel(); return;
            }
            double duration = Levels[index] % 10 == 8 ? 5 : 2;
            if (EditorApplication.timeSinceStartup - observed < duration) return;
            if (!clearing)
            {
                Require(Time.frameCount > initialFrame + 2, "Game did not advance"); frames += Time.frameCount - initialFrame;
                frameMilliseconds.Sort();
                Record($"OBSERVED {Levels[index]}.{wave + 1}: active roots={roots.Length}, pending/remaining={sequence.UndefeatedCount}; editor frame median={frameMilliseconds[frameMilliseconds.Count / 2]:0.00}ms; includes rendering/editor overhead, not an FPS acceptance claim.");
                if (wave == 0 && (IsIntroduction() || Levels[index] % 10 == 8))
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath($"Temp/CampaignEnemyValidation/level{Levels[index]}-wave1.png"));
                clearingFrame = Time.frameCount; clearing = true;
            }
            // CaptureScreenshot renders at frame end. Let it observe the live
            // tether/tail/flame before injected defeat removes those visuals.
            if (Time.frameCount <= clearingFrame + 2) return;
            foreach (var e in roots)
            {
                var health = em.GetComponentData<Health>(e); health.Current = 0; em.SetComponentData(e, health);
                var launch = em.GetComponentData<EnemyLaunchState>(e); launch.Phase = EnemyLaunchPhase.Defeated;
                em.SetComponentData(e, launch); em.SetComponentEnabled<DeathRequest>(e, true);
            }
        }

        private static void ValidateBaking(EntityManager em, Entity owner, DynamicBuffer<EnemyWaveDefinition> definitions)
        {
            var authored = AssetDatabase.FindAssets("t:EnemyWaveSettings", new[] { "Assets/CrowdPunch/Data/Campaign/Waves" })
                .Select(g => AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w.name.StartsWith($"CP{Levels[index]}_")).OrderBy(w => w.name).ToArray();
            Require(definitions.Length == authored.Length, "Baked wave count differs from assets");
            var profiles = em.GetBuffer<EnemyWaveProfile>(owner);
            for (int w = 0; w < definitions.Length; w++)
            {
                var definition = definitions[w];
                Require(definition.IsValid == 1 && definition.TotalEnemyCount == authored[w].TotalEnemyCount + authored[w].LinkedPairCount * 2
                    && definition.TotalEliteCount == authored[w].EliteEnemies.Sum(e => e.Count), "Baked composition or clearance differs");
                if (definition.LinkedPairCount > 0) Require(em.HasComponent<LinkedPairEndpoint>(definition.LinkedPairProfile.EnemyPrefab), "Unbaked pair endpoint");
                for (int p = 0; p < definition.ProfileCount; p++)
                {
                    var profile = profiles[definition.ProfileStart + p];
                    var prefab = profile.Profile.EnemyPrefab;
                    if (profile.Profile.Archetype == EnemyArchetypeKind.Dragon) Require(em.HasComponent<DragonTuning>(prefab), "Unbaked Dragon");
                    if (profile.Profile.Archetype == EnemyArchetypeKind.Ninja) Require(em.HasComponent<NinjaTuning>(prefab), "Unbaked Ninja");
                    if (em.HasComponent<SnakeHead>(prefab)) Require(em.GetComponentData<SnakeTuning>(prefab).SegmentCount == 6, "Wrong baked tail count");
                }
            }
        }

        private static void Enter(GauntletSequence flow)
        {
            wave = -1; retried = retryPending = launched = false; entered = EditorApplication.timeSinceStartup;
            flow.SelectLevel(Levels[index] - 1);
        }
        private static bool IsIntroduction() => Levels[index] == 41 || Levels[index] == 52 || Levels[index] == 61 || Levels[index] == 71;
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Record(string line) => File.AppendAllText(Output, line + "\n");
    }
}
