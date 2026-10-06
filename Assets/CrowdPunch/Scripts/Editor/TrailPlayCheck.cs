using System;
using System.IO;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.InputBridge;
using CrowdPunch.Systems.Lifetime;
using Unity.Collections;
using Unity.Core;
using Unity.Deformations;
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
    // TRAIL-001..007: injected Play-mode cases using the real baked world and systems.
    [InitializeOnLoad]
    public static class TrailPlayCheck
    {
        private const string Key = "CrowdPunch.TrailCheck";
        private const string Output = "Temp/TrailValidation/playcheck.txt";
        private static int step;
        private static double since, simSince;
        private static float orbitSign;
        private static Entity source;
        static TrailPlayCheck() { since = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            Directory.CreateDirectory("Temp/TrailValidation");
            File.WriteAllText(Output, "TRAIL-001..007 controlled baked-world validation. Injected states; not a balance playtest.\n");
            EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Bootstrap.unity");
            step = 0; since = EditorApplication.timeSinceStartup;
            SessionState.SetBool(Key, true); EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }
        public static void Stop() => SessionState.SetBool(Key, false);
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            Application.runInBackground = true;
            try { Inspect(); }
            catch (Exception e) { Record("FAIL step " + step + ": " + e); Stop(); EditorApplication.isPaused = true; Debug.LogException(e); }
        }
        private static void Inspect()
        {
            Require(EditorApplication.timeSinceStartup - since < 90, "No advancing Editor/step timeout");
            var flow = Object.FindFirstObjectByType<GauntletSequence>();
            var world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || !world.IsCreated || flow.TransitionInProgress) return;
            var player = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (player != null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            if (step == 0) { flow.SelectLevel(18); Next(1, world); return; }
            using var sq = em.CreateEntityQuery(typeof(EnemyWaveSequence));
            if (sq.CalculateEntityCount() != 1 || flow.CurrentLevelIndex != 18) return;
            Entity sequenceEntity = sq.GetSingletonEntity();
            var sequence = em.GetComponentData<EnemyWaveSequence>(sequenceEntity);
            using var eq = em.CreateEntityQuery(typeof(Enemy), typeof(EnemyWaveOwnership));
            using var enemies = eq.ToEntityArray(Allocator.Temp);
            if (step == 1)
            {
                if (sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation) return;
                int trails = 0, baselines = 0;
                foreach (var e in enemies)
                    if (em.HasComponent<TrailEmitter>(e)) { source = e; trails++; } else baselines++;
                Require(trails == 1 && baselines == 6, "Opening 6+1 composition");
                var settings = em.GetComponentData<TrailSettings>(source);
                Require(settings.EnemyDamage == TrailEnemyDamageMode.Both && settings.Immunity == TrailImmunityMode.OwnSource &&
                    settings.Avoidance == TrailAvoidanceMode.DamagingTrails, "Baked defaults");
                int visuals = 0;
                using var aq = em.CreateEntityQuery(typeof(EnemyAnimation));
                using var animated = aq.ToEntityArray(Allocator.Temp);
                foreach (var v in animated)
                {
                    var animation = em.GetComponentData<EnemyAnimation>(v);
                    if (animation.Owner != source) continue;
                    Require(animation.Samples.IsCreated, "Fish animation not baked");
                    foreach (var skin in em.GetBuffer<SkinMatrix>(v)) Require(math.all(math.isfinite(skin.Value.c3)), "Fish skin matrix");
                    visuals++;
                }
                Require(visuals > 0, "Fish visual missing");
                orbitSign = em.GetComponentData<TrailEmitter>(source).OrbitSign;
                Record("PASS actual 6+1 baked wave, dedicated defaults, Fish skinning"); Next(2, world); return;
            }
            if (step == 2)
            {
                if (world.Time.ElapsedTime - simSince < 4.3) return;
                using var sections = em.CreateEntityQuery(typeof(TrailSection));
                Require(sections.CalculateEntityCount() > 0, "No moving trails");
                Require(em.GetComponentData<TrailEmitter>(source).OrbitSign == -orbitSign, "Periodic reversal");
                Require(em.GetComponentData<DesiredMovement>(source).Speed > 0, "Circling movement");
                Record("PASS advancing simulation, circling/reversal, live sections");
                ControlledCases(world, sequenceEntity, enemies, player);
                flow.RestartCurrentLevel(); Record("Requested real level restart"); Next(3, world); return;
            }
            if (step == 3)
            {
                if (sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation) return;
                using var hazards = em.CreateEntityQuery(typeof(TrailSection));
                using var records = em.CreateEntityQuery(typeof(TrailSource));
                // The restarted wave may already be moving; every remaining record must own this fresh generation.
                using var values = records.ToComponentDataArray<TrailSource>(Allocator.Temp);
                foreach (var r in values) Require(r.RunGeneration == sequence.RunGeneration, "Stale restart record");
                foreach (var e in enemies) Defeat(em, e);
                InjectGate(em, sequenceEntity, sequence, world.Time.ElapsedTime + 1.5);
                Record("PASS restart removes old source clocks and sections; injected wave-one defeat + expiry gate");
                Next(4, world); return;
            }
            if (step == 4)
            {
                if (world.Time.ElapsedTime - simSince < 1)
                { Require(sequence.CurrentWaveIndex == 0, "Wave advanced before trail expiry"); return; }
                if (sequence.CurrentWaveIndex != 1 || sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation) return;
                int trails = 0, living = 0;
                foreach (var e in enemies) if (em.GetComponentData<EnemyLaunchState>(e).Phase != EnemyLaunchPhase.Defeated && !em.IsComponentEnabled<RespawnRequest>(e))
                { living++; if (em.HasComponent<TrailEmitter>(e)) trails++; }
                Require(living == 14 && trails == 2, "Second wave 12+2 / no replenishment");
                foreach (var e in enemies) Defeat(em, e);
                InjectGate(em, sequenceEntity, sequence, world.Time.ElapsedTime + 1.5);
                Record("PASS wave-one expiry gate and actual second 12+2 finite wave"); Next(5, world); return;
            }
            if (step == 5)
            {
                if (world.Time.ElapsedTime - simSince < 1)
                { Require(!flow.RunComplete, "Final completion before trail expiry"); return; }
                if (!flow.RunComplete) return;
                Record("PASS final completion after all bodies defeated and trails expire");
                Stop(); EditorApplication.isPaused = true;
            }
        }

        private static void ControlledCases(World world, Entity sequence, NativeArray<Entity> enemies, PlayerHealth player)
        {
            var em = world.EntityManager;
            var originalTime = world.Time;
            double t = originalTime.ElapsedTime;
            var emission = world.GetExistingSystem<TrailEmissionSystem>();
            var originalPose = em.GetComponentData<LocalTransform>(source);
            var oldEmitter = em.GetComponentData<TrailEmitter>(source);
            oldEmitter.PreviousPosition = oldEmitter.Anchor = originalPose.Position;
            oldEmitter.Initialized = 1; oldEmitter.Emitting = 0; em.SetComponentData(source, oldEmitter);
            var punch = new PunchSpecification { Origin = originalPose.Position - new float3(0, 0, 1), Direction = new float3(0, 0, 1),
                Range = 2, Radius = 1, Strength = 25, Damage = 1, Cause = EnemyLaunchCause.PlayerPunch,
                AffectActive = 1, AffectRecovering = 1, AffectLaunched = 1, ApplyDamage = 1 };
            Require(PunchResolution.TryApply(em, source, punch), "Fish ordinary punch rejected");
            world.GetExistingSystem<CrowdPunch.Systems.Physics.ApplyImpulseSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Require(em.GetComponentData<EnemyLaunchState>(source).Owner == EnemyLaunchOwner.Player &&
                math.length(em.GetComponentData<PhysicsVelocity>(source).Linear.xz) > 0, "Fish launch velocity/owner");
            Record("PASS ordinary player punch launches Fish through shared impulse path");
            var moved = originalPose; moved.Position += new float3(1, 6, 0); em.SetComponentData(source, moved);
            emission.Update(world.Unmanaged);
            using (var launchedSections = em.CreateEntityQuery(typeof(TrailSection)))
            using (var values = launchedSections.ToComponentDataArray<TrailSection>(Allocator.Temp))
            {
                bool foundLaunch = false, foundNormal = false;
                foreach (var section in values)
                {
                    if (section.Launched != 0)
                    { foundLaunch = true; Require(section.Width == 2 && section.Damage == 8 && section.Owner == EnemyLaunchOwner.Player && section.End.y < 1, "Launched ground projection/snapshot"); }
                    else { foundNormal = true; Require(section.Width == 1.2f && section.Damage == 4 && section.Owner == EnemyLaunchOwner.None, "Old normal section mutated"); }
                }
                Require(foundLaunch && foundNormal, "Both section types emitted");
                int count = values.Length;
                em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering });
                moved.Position.x++; em.SetComponentData(source, moved); emission.Update(world.Unmanaged);
                Require(launchedSections.CalculateEntityCount() == count, "Recovering source emitted");
                em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
                emission.Update(world.Unmanaged);
                Require(launchedSections.CalculateEntityCount() == count, "Stationary source emitted");
            }
            em.SetComponentData(source, originalPose);
            Record("PASS launched ground projection and separate tuning/credit, older normal snapshot, no recovering/stationary emission");
            using var sections = em.CreateEntityQuery(typeof(TrailSection)); em.DestroyEntity(sections);
            using var records = em.CreateEntityQuery(typeof(TrailSource)); em.DestroyEntity(records);
            Entity target = Entity.Null;
            foreach (var e in enemies) if (e != source) { target = e; break; }
            Require(target != Entity.Null, "Damage target");
            em.SetComponentData(target, new Health { Current = 100, Max = 100 });
            em.SetComponentData(target, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            float3 position = em.GetComponentData<LocalTransform>(target).Position;
            Entity record = em.CreateEntity();
            em.AddComponentData(record, new TrailSource { Enemy = source, Lifetime = 1, SceneOwner = sequence, Sequence = sequence,
                RunGeneration = em.GetComponentData<EnemyWaveSequence>(sequence).RunGeneration, ExpiresAt = t + 100 });
            em.AddBuffer<TrailDamageTarget>(record);
            var fixture = new TrailSection { Record = record, Source = source, SourceLifetime = 1,
                Start = position + new float3(-.5f, -50, 0), End = position + new float3(.5f, -50, 0),
                Width = 1.2f, Damage = 4, DamagesEnemies = 1, TickInterval = .75f, ExpiresAt = t + 100 };
            var a = em.CreateEntity(); em.AddComponentData(a, fixture);
            var b = em.CreateEntity(); var launched = fixture; launched.Launched = 1; launched.Owner = EnemyLaunchOwner.Player;
            launched.ChainDepth = 3; em.AddComponentData(b, launched);
            var system = world.GetExistingSystem<TrailDamageSystem>();
            system.Update(world.Unmanaged);
            Require(em.GetComponentData<Health>(target).Current == 96, "Duplicate sections multiplied damage / horizontal overlap");
            var away = fixture; away.Start.x += 100; away.End.x += 100;
            em.SetComponentData(a, away); em.SetComponentData(b, away);
            world.SetTime(new TimeData(t + .2, .02f)); system.Update(world.Unmanaged);
            em.SetComponentData(a, fixture); em.SetComponentData(b, launched);
            world.SetTime(new TimeData(t + .4, .02f)); system.Update(world.Unmanaged);
            Require(em.GetComponentData<Health>(target).Current == 96, "Exit/reentry reset cooldown");
            world.SetTime(new TimeData(t + .8, .02f)); system.Update(world.Unmanaged);
            Require(em.GetComponentData<Health>(target).Current == 92, "Regular source tick");
            var second = em.CreateEntity(); em.AddComponentData(second, new TrailSource { ExpiresAt = t + 100 }); em.AddBuffer<TrailDamageTarget>(second);
            var secondSection = fixture; secondSection.Record = second;
            var c = em.CreateEntity(); em.AddComponentData(c, secondSection); system.Update(world.Unmanaged);
            Require(em.GetComponentData<Health>(target).Current == 88, "Independent sources did not stack");
            Record("PASS height ignored, immediate/timed hits, section dedupe, normal+launched shared clock, reentry persistence, source stacking");
            Require(!TrailGeometry.CanDamage(new TrailSection { DamagesEnemies = 0 }, target, 1, false, false), "Launched-only mode");
            var immunity = fixture; immunity.Source = target; immunity.Immunity = TrailImmunityMode.OwnSource;
            Require(!TrailGeometry.CanDamage(immunity, target, 1, true, false) && TrailGeometry.CanDamage(immunity, target, 2, true, false), "Pooled source identity");
            immunity.Immunity = TrailImmunityMode.AllTrailEnemies;
            Require(!TrailGeometry.CanDamage(immunity, source, 2, true, false), "All-type immunity");
            immunity.Immunity = TrailImmunityMode.None;
            Require(TrailGeometry.CanDamage(immunity, target, 1, true, false) && !TrailGeometry.CanDamage(immunity, target, 1, false, true), "No immunity / intact armor");
            Record("PASS supported damage modes, own/all/no immunity, pooled-lifetime immunity, armor filtering");
            em.SetComponentData(target, new DesiredMovement { Direction = new float3(0, 0, 1), Speed = 1 });
            em.SetComponentData(target, NavigationIntent.Travel(position + new float3(0, 0, 3), 1, .1f, 0));
            var avoid = fixture; avoid.Avoidance = TrailAvoidanceMode.DamagingTrails;
            avoid.Start.z -= .7f; avoid.End.z -= .7f; em.SetComponentData(a, avoid);
            world.GetExistingSystem<TrailAvoidanceSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Require(math.lengthsq(em.GetComponentData<NavigationIntent>(target).Separation) > 0, "Damaging trail not avoided");
            em.SetComponentData(target, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            em.SetComponentData(target, new NavigationIntent());
            world.GetExistingSystem<TrailAvoidanceSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Require(math.lengthsq(em.GetComponentData<NavigationIntent>(target).Separation) == 0, "Launched body avoided trail");
            em.SetComponentData(target, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            avoid.Avoidance = TrailAvoidanceMode.None; em.SetComponentData(a, avoid);
            world.GetExistingSystem<TrailAvoidanceSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Require(math.lengthsq(em.GetComponentData<NavigationIntent>(target).Separation) == 0, "No-avoidance setting ignored");
            Record("PASS actual avoidance intent, launched exemption and no-avoidance mode");
            em.DestroyEntity(a); em.DestroyEntity(c);
            em.GetBuffer<TrailDamageTarget>(record).Clear();
            em.SetComponentData(target, new Health { Current = 4, Max = 100 });
            em.SetComponentData(target, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, Owner = EnemyLaunchOwner.Enemy });
            system.Update(world.Unmanaged);
            var damage = em.GetComponentData<EnemyDamageState>(target);
            Require(em.GetComponentData<EnemyLaunchState>(target).Phase == EnemyLaunchPhase.Launched && damage.IsDefeatDeferred != 0 &&
                damage.DefeatOwner == EnemyLaunchOwner.Player && damage.DefeatChainDepth == 3, "Deferred death / launch ownership / reward credit");
            // Changing source phase and generation cannot change older sections or their attribution.
            var oldLife = em.GetComponentData<EnemyLifetime>(source);
            em.SetComponentData(source, new EnemyLifetime { Generation = oldLife.Generation + 1 });
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            world.GetExistingSystem<TrailExpirySystem>().Update(world.Unmanaged);
            Require(em.Exists(b) && em.GetComponentData<TrailSection>(b).Owner == EnemyLaunchOwner.Player, "Source reuse/death changed old trails");
            em.SetComponentData(source, oldLife);
            Record("PASS lethal trail keeps launched target physics and deferred defeat; player kill/chain credit; source death/reuse preserves sections");
            var bridge = player.GetComponent<PlayerEcsBridge>();
            bridge.ReceiveEnemyContactHit(.01f, 10, Vector3.zero);
            Require(player.IsInvincible, "Ordinary invulnerability setup");
            float hp = player.CurrentHealth; bridge.ReceiveTrailDamage(4);
            Require(player.CurrentHealth == hp - 4, "Ordinary invulnerability blocked trail damage");
            Record("PASS dedicated player bridge ignores ordinary hit protection and applies damage only");
            em.DestroyEntity(sections); em.DestroyEntity(records);
            world.SetTime(originalTime);
        }

        private static void InjectGate(EntityManager em, Entity sequence, EnemyWaveSequence state, double until)
        {
            Entity record = em.CreateEntity(); em.AddComponentData(record, new TrailSource { SceneOwner = sequence, Sequence = sequence,
                RunGeneration = state.RunGeneration, WaveIndex = state.CurrentWaveIndex, ExpiresAt = until });
            em.AddBuffer<TrailDamageTarget>(record);
            Entity section = em.CreateEntity(); em.AddComponentData(section, new TrailSection { Record = record, ExpiresAt = until });
        }
        private static void Defeat(EntityManager em, Entity enemy)
        {
            if (em.IsComponentEnabled<RespawnRequest>(enemy)) return;
            var launch = em.GetComponentData<EnemyLaunchState>(enemy); launch.Phase = EnemyLaunchPhase.Defeated;
            em.SetComponentData(enemy, launch); var health = em.GetComponentData<Health>(enemy); health.Current = 0; em.SetComponentData(enemy, health);
            em.SetComponentEnabled<DeathRequest>(enemy, true);
        }
        private static void Next(int value, World world) { step = value; since = EditorApplication.timeSinceStartup; simSince = world.Time.ElapsedTime; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Record(string text) { File.AppendAllText(Output, text + "\n"); Debug.Log("TrailCheck: " + text); }
    }
}
