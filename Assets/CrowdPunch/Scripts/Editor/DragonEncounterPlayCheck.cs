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
    // DRAGON-001..007: controlled checks in the real baked/solver world, not a balance playtest.
    [InitializeOnLoad]
    public static class DragonEncounterPlayCheck
    {
        private const string Output = "Temp/DragonValidation/live-check.txt";
        private static bool running, warned;
        private static int stage;
        private static Entity dragon, target;
        private static double started, stageTime;
        static DragonEncounterPlayCheck() => EditorApplication.update += Tick;
        public static void Start()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
            if (!EditorApplication.isPlaying || flow == null || flow.HasCampaign || flow.CurrentLevelIndex != 25)
                throw new InvalidOperationException("Launch legacy Gauntlet_26 first.");
            Directory.CreateDirectory("Temp/DragonValidation");
            File.WriteAllText(Output, "DRAGON-001..007 controlled baked-world check, injected punches/defeats.\n");
            running = true; stage = 0; warned = false; started = EditorApplication.timeSinceStartup;
            flow.RestartCurrentLevel(); FeedbackTimeController.SetPaused(false); EditorApplication.isPaused = false;
        }
        private static void Tick()
        {
            if (!running || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try { Check(); }
            catch (Exception e) { Record("FAIL " + e); running = false; EditorApplication.isPaused = true; Debug.LogException(e); }
        }
        private static void Check()
        {
            Require(EditorApplication.timeSinceStartup - started < 150, "Timed out at stage " + stage);
            var flow = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>();
            var world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || !world.IsCreated || flow.TransitionInProgress) return;
            var health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (health != null) { health.gameObject.SetActive(true); health.Restore(health.MaxHealth); }
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var sq = em.CreateEntityQuery(typeof(EnemyWaveSequence));
            using var dq = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<DragonTuning>() }, None = new[] { ComponentType.ReadOnly<RespawnRequest>() } });
            if (stage == 10 && !flow.HasCampaign && (flow.RunComplete || flow.CurrentLevelIndex == 26))
            {
                Require(dq.CalculateEntityCount() == 0, "Flame source survived scene unload");
                Record("PASS retry, exact finite waves, final completion and scene cleanup"); running = false; EditorApplication.isPaused = true; return;
            }
            Require(!flow.HasCampaign && flow.CurrentLevelIndex == 25, "Changed campaign/level selection");
            if (sq.CalculateEntityCount() != 1) return;
            if (stage > 0 && world.Time.ElapsedTime <= stageTime + .02) return;
            var wave = sq.GetSingleton<EnemyWaveSequence>();
            using var dragons = dq.ToEntityArray(Allocator.Temp);
            using var eq = em.CreateEntityQuery(typeof(Enemy), typeof(EnemyWaveOwnership));
            using var bodies = eq.ToEntityArray(Allocator.Temp);
            if (stage == 0)
            {
                if (wave.CurrentWaveIndex != 0 || wave.Phase != EnemyWaveRuntimePhase.AwaitingActivation || dragons.Length != 1) return;
                Require(wave.SpawnedCount == 7, "First wave is not 6+1"); dragon = dragons[0];
                Require(em.GetBuffer<DragonMouthFrame>(dragon).Length == 96, "Missing sampled Head attachment");
                foreach (var body in bodies)
                {
                    var p = em.GetComponentData<LocalTransform>(body); p.Position = new float3(-12 + body.Index % 4, 0, 10); em.SetComponentData(body, p);
                    // The ordinary profile has only 5 HP. Durable injected bodies isolate solver propagation from flame lethality.
                    em.SetComponentData(body, new Health { Current = 1000, Max = 1000 });
                    if (body != dragon) target = body;
                }
                em.SetComponentData(dragon, LocalTransform.FromPosition(new float3(0, 0, 4)));
                var f = em.GetComponentData<DragonFlameState>(dragon); f.Remaining = 0; em.SetComponentData(dragon, f);
                Record("PASS real baked Dragon, sampled head, finite 6+1 first wave"); Next(world, 1);
            }
            if (stage <= 4 && PlayerBridgeRegistry.TryGetBridge(out var bridge))
            {
                bridge.transform.position = new Vector3(0, .5f, -6); bridge.ClearMovement();
                bridge.PublishPlayerSnapshot(bridge.transform.position, Vector3.forward, bridge.Radius);
            }
            if (stage == 1)
            {
                var f = em.GetComponentData<DragonFlameState>(dragon);
                warned |= f.Phase == DragonFlamePhase.Warning;
                if (f.Phase != DragonFlamePhase.Active) return;
                Require(warned, "Burst started without warning");
                Record("PASS advancing moving warning then active burst in real system order");
                em.SetComponentData(dragon, LocalTransform.FromPosition(new float3(0, 0, 4)));
                em.SetComponentData(dragon, new PhysicsVelocity());
                em.SetComponentData(target, LocalTransform.FromPosition(new float3(0, 0, 6)));
                em.SetComponentData(target, new PhysicsVelocity());
                Require(PunchResolution.TryApply(em, dragon, new PunchSpecification { Origin = new float3(0, 0, 3), Direction = math.forward(),
                    Range = 3, Radius = 1, Strength = 30, Damage = 5, AffectActive = 1, ApplyDamage = 1, Cause = EnemyLaunchCause.PlayerPunch }), "Injected ordinary punch failed");
                Next(world, 2);
            }
            else if (stage == 2)
            {
                var launch = em.GetComponentData<EnemyLaunchState>(dragon);
                if (launch.Phase == EnemyLaunchPhase.Launched)
                {
                    Require(DragonFlameGeometry.TryCone(dragon, em.GetComponentData<LocalTransform>(dragon), em.GetComponentData<DragonTuning>(dragon),
                        em.GetComponentData<DragonFlameState>(dragon), launch, 1, out var cone) && cone.Active != 0, "Launched Dragon did not emit");
                }
                if (em.GetComponentData<EnemyLaunchState>(target).LaunchSequence == 0) return;
                Require(em.GetComponentData<EnemyLaunchState>(target).Owner == EnemyLaunchOwner.Player, "Dragon-body solver chain lost Player ownership");
                Record("PASS ordinary punch immediately enables flame and real Dragon-body solver propagation");
                em.SetComponentData(dragon, new PhysicsVelocity()); Next(world, 3);
            }
            else if (stage == 3)
            {
                var launch = em.GetComponentData<EnemyLaunchState>(dragon);
                if (launch.Phase == EnemyLaunchPhase.Launched) { em.SetComponentData(dragon, new PhysicsVelocity()); return; }
                Require(launch.Phase == EnemyLaunchPhase.Recovering, "Dragon failed to survive ordinary recovery");
                Require(!DragonFlameGeometry.TryCone(dragon, em.GetComponentData<LocalTransform>(dragon), em.GetComponentData<DragonTuning>(dragon),
                    em.GetComponentData<DragonFlameState>(dragon), launch, 1, out _), "Recovery kept emitting");
                Record("PASS recovery stops attached flame"); Next(world, 4);
            }
            else if (stage == 4)
            {
                if (em.GetComponentData<EnemyLaunchState>(dragon).Phase != EnemyLaunchPhase.Active) return;
                var f = em.GetComponentData<DragonFlameState>(dragon);
                if (f.WasInactive != 0) return;
                Require(f.Phase == DragonFlamePhase.Cooldown && f.Remaining >= em.GetComponentData<DragonTuning>(dragon).CooldownDuration - .06f,
                    "Surviving recovery skipped full cooldown");
                em.SetComponentData(dragon, LocalTransform.FromPosition(new float3(0, 0, 4))); em.SetComponentData(dragon, new PhysicsVelocity());
                Require(PunchResolution.TryApply(em, dragon, new PunchSpecification { Origin = new float3(0, 0, 3), Direction = math.forward(),
                    Range = 3, Radius = 1, Strength = 20, Damage = 10000, AffectActive = 1, ApplyDamage = 1, Cause = EnemyLaunchCause.PlayerPunch }), "Lethal launch failed");
                Next(world, 5);
            }
            else if (stage == 5)
            {
                var l = em.GetComponentData<EnemyLaunchState>(dragon);
                Require(em.GetComponentData<Health>(dragon).Current == 0 && l.Phase == EnemyLaunchPhase.Launched, "Lethal launch was not deferred");
                Require(DragonFlameGeometry.TryCone(dragon, em.GetComponentData<LocalTransform>(dragon), em.GetComponentData<DragonTuning>(dragon),
                    em.GetComponentData<DragonFlameState>(dragon), l, 1, out var cone) && cone.Active != 0, "Zero-health flight lost flame");
                Record("PASS full cooldown after recovery and continuous zero-health deferred flight"); Defeat(em, bodies); Next(world, 6);
            }
            else if (stage == 6)
            {
                if (wave.CurrentWaveIndex != 1 || wave.Phase != EnemyWaveRuntimePhase.AwaitingActivation || dragons.Length != 2) return;
                Require(wave.SpawnedCount == 14, "Second wave is not 12+2");
                Record("PASS exact finite 12+2 second wave"); flow.RestartCurrentLevel(); Next(world, 7);
            }
            else if (stage == 7)
            {
                if (wave.CurrentWaveIndex != 0 || wave.Phase != EnemyWaveRuntimePhase.AwaitingActivation || dragons.Length != 1) return;
                Require(em.GetBuffer<DragonFlameClock>(dragons[0]).Length == 0, "Retry retained old hit clocks");
                Defeat(em, bodies); Next(world, 8);
            }
            else if (stage == 8)
            {
                if (wave.CurrentWaveIndex != 1 || wave.Phase != EnemyWaveRuntimePhase.AwaitingActivation || dragons.Length != 2) return;
                Defeat(em, bodies); Next(world, 10);
            }
        }
        private static void Defeat(EntityManager em, NativeArray<Entity> bodies)
        {
            foreach (var e in bodies)
            {
                em.SetComponentData(e, new Health()); var l = em.GetComponentData<EnemyLaunchState>(e); l.Phase = EnemyLaunchPhase.Defeated;
                em.SetComponentData(e, l); em.SetComponentEnabled<DeathRequest>(e, true);
            }
        }
        private static void Next(World world, int value) { stage = value; stageTime = world.Time.ElapsedTime; }
        private static void Require(bool valid, string text) { if (!valid) throw new InvalidOperationException(text); }
        private static void Record(string text) => File.AppendAllText(Output, text + "\n");
    }
}
