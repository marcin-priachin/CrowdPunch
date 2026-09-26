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
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // Controlled launches use real baked colliders and the running fixed-step pipeline. No saved scene edits.
    [InitializeOnLoad]
    public static class BarricadeEncounterPlayCheck
    {
        private const string Key = "CrowdPunch.BarricadeCheck";
        private const string Output = "Temp/BarricadeValidation/playcheck.txt";
        private static int step;
        private static double since, simulationSince;
        private static Entity source, explosive, finalBody;
        static BarricadeEncounterPlayCheck() { since = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before starting the check.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before running the check.");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Bootstrap.unity");
            Directory.CreateDirectory("Temp/BarricadeValidation");
            File.WriteAllText(Output, "Controlled live BARRICADE-001..005 validation. Injected launches/positions; not balance evidence.\n");
            step = 0; source = explosive = finalBody = Entity.Null;
            since = EditorApplication.timeSinceStartup;
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        public static void Stop() => SessionState.SetBool(Key, false);
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try { Inspect(); }
            catch (Exception e) { Record("FAIL step " + step + ": " + e); Stop(); EditorApplication.isPaused = true; Debug.LogException(e); }
        }
        private static void Inspect()
        {
            Require(EditorApplication.timeSinceStartup - since < 60, "Step timed out");
            var flow = Object.FindFirstObjectByType<GauntletSequence>();
            var world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || !world.IsCreated || flow.TransitionInProgress) return;
            var player = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (player != null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            if (step == 0) { flow.SelectLevel(12); Next(1, world); return; }
            using var walls = em.CreateEntityQuery(typeof(Barricade));
            using var bodies = em.CreateEntityQuery(typeof(Enemy), typeof(BarricadeCrowdMember));
            if (walls.CalculateEntityCount() != 1 || bodies.CalculateEntityCount() != 16) return;
            var wall = walls.GetSingletonEntity(); var data = em.GetComponentData<Barricade>(wall);
            using var enemies = bodies.ToEntityArray(Allocator.Temp);
            double elapsed = world.Time.ElapsedTime - simulationSince;
            if (step == 1)
            {
                Require(data.HitsRemaining == 3, "Fresh barricade must have three hits");
                int i = 0;
                foreach (var e in enemies)
                {
                    if (em.HasComponent<ExplosiveEnemyState>(e)) explosive = e;
                    else if (source == Entity.Null) source = e;
                    else finalBody = e;
                    var movement = em.GetComponentData<EnemyMovementSettings>(e); movement.MoveSpeed = 0; em.SetComponentData(e, movement);
                    var transform = em.GetComponentData<LocalTransform>(e);
                    transform.Position = new float3(-10 + (i % 6) * 4, 1, -8 + (i / 6) * 3); em.SetComponentData(e, transform); i++;
                }
                Record("PASS baked objective, 16 bounded crowd roots, three durability hits");
                Next(2, world); return;
            }
            if (step == 2 && elapsed > .3)
            {
                Launch(em, source, true, EnemyLaunchCause.ElitePunch); Next(3, world); return;
            }
            if (step == 3 && data.HitsRemaining < 3)
            {
                Require(data.HitsRemaining == 2 && em.GetComponentData<PhysicsVelocity>(source).Linear.z < 0, "First hit must rebound");
                Record("PASS elite-owned body damages and rebounds, velocity=" + em.GetComponentData<PhysicsVelocity>(source).Linear);
                var receiver = em.GetComponentData<LocalTransform>(finalBody);
                receiver.Position = em.GetComponentData<LocalTransform>(source).Position - new float3(0,0,2.8f);
                em.SetComponentData(finalBody, receiver);
                em.SetComponentData(finalBody, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
                em.SetComponentData(finalBody, new PhysicsVelocity());
                Next(30, world); return;
            }
            if (step == 30 && em.GetComponentData<EnemyLaunchState>(finalBody).Phase == EnemyLaunchPhase.Launched)
            {
                Require(em.GetComponentData<EnemyLaunchState>(finalBody).Owner == EnemyLaunchOwner.Enemy, "Rebound propagation lost ownership");
                Record("PASS rebounding body propagates a launch through ordinary solver collision");
                Launch(em, source, false, EnemyLaunchCause.ElitePunch); Next(4, world); return;
            }
            if (step == 4 && elapsed > .2)
            {
                Require(data.HitsRemaining == 2, "Same launch repeated contact must not damage");
                Record("PASS repeated contact in same launch consumes no hit");
                Launch(em, explosive, true, EnemyLaunchCause.PlayerPunch); Next(5, world); return;
            }
            if (step == 5 && data.HitsRemaining < 2 && elapsed > .1)
            {
                Require(data.HitsRemaining == 1, "Exploder impact + explosion must count once");
                Require(em.GetComponentData<ExplosiveEnemyState>(explosive).HasExploded != 0, "Impact must trigger explosion");
                Record("PASS explosive impact and radial blast share one hit");
                Launch(em, finalBody, true, EnemyLaunchCause.BossAttack); Next(6, world); return;
            }
            if (step == 6 && data.HitsRemaining == 0 && elapsed > .12)
            {
                var transform = em.GetComponentData<LocalTransform>(finalBody);
                Require(transform.Position.z > 11, "Destroying body did not pass through: " + transform.Position);
                Require(!flow.RunComplete, "Break alone must not complete level");
                Record("PASS boss-owned destroying body passes through at " + transform.Position + "; exit still required");
                // Give a pending pooled member an immediate deadline; disabled replenishment must still suppress it.
                em.SetComponentData(source, new RespawnRequest { IsPooled = 1, RespawnAt = 0 });
                em.SetComponentEnabled<RespawnRequest>(source, true);
                Next(7, world); return;
            }
            if (step == 7 && elapsed > 2.5)
            {
                foreach (var e in enemies) Require(em.GetComponentData<EnemyRespawnSettings>(e).Enabled == 0, "Replenishment remains enabled");
                Require(em.IsComponentEnabled<RespawnRequest>(source), "A pending replacement spawned after break");
                Require(em.GetComponentData<EnemyLaunchState>(finalBody).Phase != EnemyLaunchPhase.Defeated, "Survivors were defeated on break");
                Record("PASS replenishment stops including pending pool deadlines; survivors remain alive");
                player.transform.position = new Vector3(0,.5f,15);
                Next(8, world); return;
            }
            if (step == 8 && flow.RunComplete)
            {
                Record("PASS reaching exposed exit completes level with surviving enemies");
                flow.RestartCurrentLevel(); source = explosive = finalBody = Entity.Null; Next(9, world); return;
            }
            if (step == 9 && elapsed > .2)
            {
                Require(data.HitsRemaining == 3 && data.HitSequence == 0, "Restart did not restore barricade");
                Require(!flow.RunComplete, "Restart did not clear completion");
                Require(em.GetBuffer<BarricadeHitHistory>(wall).Length == 0, "Restart leaked hit history");
                source = enemies[0];
                em.SetComponentData(source, new RespawnRequest { IsPooled = 1, RespawnAt = world.Time.ElapsedTime });
                em.SetComponentEnabled<RespawnRequest>(source, true);
                var transform = em.GetComponentData<LocalTransform>(source); transform.Position = new float3(0,-30,0); em.SetComponentData(source, transform);
                Next(10, world); return;
            }
            if (step == 10 && !em.IsComponentEnabled<RespawnRequest>(source))
            {
                var position = em.GetComponentData<LocalTransform>(source).Position;
                Require(position.z < 10, "Replacement spawned behind barricade");
                Record("PASS restart restores wall/history/crowd; replenishment reuses root at " + position);
                Record("PASS ALL LIVE CHECKS"); Stop(); EditorApplication.isPaused = true;
            }
        }
        private static void Launch(EntityManager em, Entity e, bool fresh, EnemyLaunchCause cause)
        {
            var transform = em.GetComponentData<LocalTransform>(e); transform.Position = new float3(0, .05f, 8.5f); em.SetComponentData(e, transform);
            var launch = em.GetComponentData<EnemyLaunchState>(e);
            if (fresh) EnemyLaunchTransition.Begin(ref launch, cause, 10);
            launch.HomingTarget = Entity.Null; em.SetComponentData(e, launch);
            em.SetComponentData(e, new PhysicsVelocity { Linear = new float3(0,0,35) });
            em.SetComponentEnabled<ExternalImpulse>(e, false);
            em.SetComponentEnabled<RespawnRequest>(e, false);
        }
        private static void Next(int next, World world) { step = next; since = EditorApplication.timeSinceStartup; simulationSince = world.Time.ElapsedTime; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Record(string message) => File.AppendAllText(Output, message + "\n");
    }
}
