using System;
using System.Collections.Generic;
using System.IO;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaveSequence = CrowdPunch.Components.EnemyWaveSequence;
using EnemyArchetype = CrowdPunch.Components.EnemyArchetype;

namespace CrowdPunch.Editor
{
    /// <summary>Editor-only lifecycle probe. Injected damage is not a gameplay or balance test.</summary>
    [InitializeOnLoad]
    public static class GauntletSequenceSmokeCheck
    {
        private const string RunningKey = "CrowdPunch.GauntletSmoke.Running";
        private const string Output = "Temp/GauntletValidation";
        private static int level = -1, wave = -1, peak, waveCount, completedLevels;
        private static double progressAt, gateAt;
        private static int eliteStep;
        private static Entity replenishedNormal;
        private static bool observedPooled;
        private static Vector3[] floorVertices;
        private static readonly List<float> frameMilliseconds = new();
        private static readonly List<string> evidence = new();

        static GauntletSequenceSmokeCheck() { EditorApplication.update += Tick; }

        [MenuItem("Crowd Punch/Levels/Run Sequence Lifecycle Smoke Check")]
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Start this check from Edit mode.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Bootstrap.unity");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/smoke.txt", "Editor lifecycle probe: idle player kept alive; stage clears use injected DamageRequest. Not a difficulty test.\n");
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Crowd Punch/Levels/Stop Sequence Lifecycle Smoke Check")]
        public static void Stop() { SessionState.SetBool(RunningKey, false); }

        private static void Tick()
        {
            if (!SessionState.GetBool(RunningKey, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try { Inspect(); }
            catch (Exception exception)
            {
                Record("FAIL: " + exception);
                Stop();
                Time.timeScale = 0;
                Debug.LogException(exception);
            }
        }

        private static void Inspect()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
            World world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || !world.IsCreated) return;
            if (flow.RunComplete)
            {
                Require(completedLevels == 10, "Did not observe every level clear");
                Require(waveCount == 39, "Did not observe all 39 waves");
                Record("PASS: 10 levels, 39 valid fully spawned waves, final completion reported.");
                Stop();
                return;
            }
            if (flow.TransitionInProgress || flow.CurrentLevelIndex < 0) return;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            // Preserve normal invulnerability; resetting it every frame would stack knockback
            // at an attack cadence that cannot occur in normal play.
            if (player != null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            var em = world.EntityManager;
            em.CompleteAllTrackedJobs();
            using var sequenceQuery = em.CreateEntityQuery(typeof(WaveSequence));
            if (sequenceQuery.CalculateEntityCount() != 1) return;
            Entity owner = sequenceQuery.GetSingletonEntity();
            WaveSequence sequence = em.GetComponentData<WaveSequence>(owner);
            double now = EditorApplication.timeSinceStartup;
            if (level != flow.CurrentLevelIndex)
            {
                level = flow.CurrentLevelIndex;
                wave = -1;
                peak = 0;
                progressAt = now;
                frameMilliseconds.Clear();
                floorVertices = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/CrowdPunch/Data/GauntletLayouts/Gauntlet_{level + 1:00}_Floor.asset").vertices;
                Record($"ENTER {level + 1:00}: {flow.GetLevelName(level)}");
            }
            if (player != null)
            {
                Vector3 position = player.transform.position;
                int corners = floorVertices.Length / 2;
                for (int i = 0; i < corners; i++)
                {
                    Vector3 a = floorVertices[i], edge = floorVertices[(i + 1) % corners] - a;
                    Require(edge.x * (position.z - a.z) - edge.z * (position.x - a.x) >= -0.1f,
                        "Player escaped the closed court at perimeter " + i);
                }
            }
            using var enemyQuery = em.CreateEntityQuery(typeof(EnemyWaveOwnership), typeof(EnemyArchetype));
            using var enemies = enemyQuery.ToEntityArray(Allocator.Temp);
            int alive = 0;
            foreach (Entity enemy in enemies)
            {
                var ownership = em.GetComponentData<EnemyWaveOwnership>(enemy);
                Require(ownership.Sequence == owner && ownership.RunGeneration == sequence.RunGeneration,
                    "Old scene or generation enemy leaked into new encounter");
                if (!em.IsComponentEnabled<RespawnRequest>(enemy)
                    && em.GetComponentData<EnemyLaunchState>(enemy).Phase != EnemyLaunchPhase.Defeated) alive++;
            }
            peak = Math.Max(peak, alive);
            Require(alive <= 30, "Active population exceeded authored maximum");
            if (Time.unscaledDeltaTime > 0) frameMilliseconds.Add(Time.unscaledDeltaTime * 1000);
            Require(sequence.Phase != EnemyWaveRuntimePhase.Invalid, "Invalid baked wave");
            Require(now - progressAt < 90, "Encounter made no progress for 90 seconds");
            if (sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation) return;
            var definitions = em.GetBuffer<EnemyWaveDefinition>(owner);
            var definition = definitions[sequence.CurrentWaveIndex];
            if (wave != sequence.CurrentWaveIndex)
            {
                wave = sequence.CurrentWaveIndex;
                gateAt = progressAt = now;
                eliteStep = 0;
                observedPooled = false;
                Require(definition.IsValid == 1, "Baker rejected wave");
                Require(sequence.SpawnedCount == definition.TotalEnemyCount + definition.TotalEliteCount, "Wave has pending spawns");
                int[] actual = new int[5];
                foreach (Entity enemy in enemies)
                    if (em.GetComponentData<EnemyWaveOwnership>(enemy).WaveIndex == wave)
                        actual[(int)em.GetComponentData<EnemyArchetype>(enemy).Value]++;
                int[] expected = new int[5];
                var profiles = em.GetBuffer<EnemyWaveProfile>(owner);
                for (int i = 0; i < definition.ProfileCount; i++)
                {
                    var profile = profiles[definition.ProfileStart + i];
                    expected[(int)profile.Profile.Archetype] += profile.MinimumCount;
                }
                expected[4] = definition.TotalEliteCount;
                Require(string.Join(",", actual) == string.Join(",", expected), "Spawned composition differs from exact recipe");
                waveCount++;
                Record($"WAVE {level + 1:00}.{wave + 1:00}: exact B/R/X/D/E={string.Join("/", actual)}, alive={alive}, cohortEntities={enemies.Length}");
            }
            if (definition.ActivationMode != (byte)EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated) return;
            if (now - gateAt < 4) return;
            if (eliteStep == 0) Capture($"level{level + 1:00}_wave{wave + 1:00}");
            if (definition.TotalEliteCount > 0 && !CheckElite(em, enemies, now)) return;
            // Clears use the real damage/defeat/counting pipeline, not completion flags or timer edits.
            foreach (Entity enemy in enemies) Damage(em, enemy);
            if (sequence.CurrentWaveIndex == definitions.Length - 1 && eliteStep != 99)
            {
                frameMilliseconds.Sort();
                float median = frameMilliseconds.Count == 0 ? 0 : frameMilliseconds[frameMilliseconds.Count / 2];
                Record($"CLEAR {level + 1:00}: peakActive={peak}; retainedRoots={enemies.Length}; editor frame median={median:0.0}ms (includes Editor/tool overhead)");
                completedLevels++;
            }
            eliteStep = 99;
        }

        private static bool CheckElite(EntityManager em, NativeArray<Entity> enemies, double now)
        {
            if (eliteStep == 99) return true;
            if (eliteStep == 0)
            {
                foreach (Entity enemy in enemies)
                    if (em.HasComponent<EliteWaveReplenishment>(enemy) && !em.IsComponentEnabled<RespawnRequest>(enemy))
                    { replenishedNormal = enemy; Damage(em, enemy); eliteStep = 1; gateAt = now; Record("ELITE: injected one normal defeat; waiting for existing-instance replenishment"); return false; }
                throw new InvalidOperationException("No eligible elite-wave normal");
            }
            if (eliteStep == 1)
            {
                var ownership = em.GetComponentData<EnemyWaveOwnership>(replenishedNormal);
                if (ownership.DefeatCounted != 0) observedPooled = true;
                if (!observedPooled || ownership.DefeatCounted != 0 || em.IsComponentEnabled<RespawnRequest>(replenishedNormal))
                { Require(now - gateAt < 30, "Normal did not replenish within 30 seconds"); return false; }
                Record("ELITE: same normal entity returned and defeat accounting restored");
                foreach (Entity enemy in enemies)
                    if (em.GetComponentData<EnemyArchetype>(enemy).Value == EnemyArchetypeKind.Elite) Damage(em, enemy);
                eliteStep = 2;
                gateAt = now;
                return false;
            }
            if (now - gateAt < 8) return false;
            foreach (Entity enemy in enemies)
                if (em.HasComponent<EliteWaveReplenishment>(enemy))
                    Require(em.GetComponentData<EnemyRespawnSettings>(enemy).Enabled == 0, "Pooled elite re-enabled replenishment");
            Record("ELITE: replenishment remains disabled eight seconds after elite defeat");
            return true;
        }

        private static void Damage(EntityManager em, Entity enemy)
        {
            if (em.IsComponentEnabled<RespawnRequest>(enemy)) return;
            em.SetComponentData(enemy, new DamageRequest { Amount = 10000 });
            em.SetComponentEnabled<DamageRequest>(enemy, true);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private static void Record(string line)
        {
            evidence.Add(line);
            Directory.CreateDirectory(Output);
            File.AppendAllText(Output + "/smoke.txt", line + "\n");
        }

        private static void Capture(string name)
        {
            var camera = Camera.main;
            if (camera == null) return;
            RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
