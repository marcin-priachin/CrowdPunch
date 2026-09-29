using System;
using System.IO;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.InputBridge;
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
    [InitializeOnLoad]
    public static class RotatingCoverPlayCheck
    {
        private const string Key = "CrowdPunch.CoverCheck";
        private const string Output = "Temp/RotatingCoverValidation/playcheck.txt";
        private static int step;
        private static double since, simulationSince;
        private static Entity source, explosive, outsideExplosive;
        static RotatingCoverPlayCheck() { since = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Bootstrap.unity");
            Directory.CreateDirectory("Temp/RotatingCoverValidation");
            File.WriteAllText(Output, "COVER-001..005 controlled baked-world checks; injected positions, not balance evidence.\n");
            step = 0; source = explosive = outsideExplosive = Entity.Null;
            since = EditorApplication.timeSinceStartup;
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
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
            Require(EditorApplication.timeSinceStartup - since < 90, "Step timed out");
            var flow = Object.FindFirstObjectByType<GauntletSequence>();
            var world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || !world.IsCreated || flow.TransitionInProgress) return;
            var player = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (player != null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            if (step == 0) { flow.SelectLevel(13); Next(1, world); return; }
            if (step == 10 && flow.CurrentLevelIndex == 14)
            {
                Require(!flow.RunComplete, "Cover destruction advances to the shell gauntlet");
                Record("PASS target destruction advances to Gauntlet_15 without survivor cleanup"); Record("COMPLETE");
                Stop(); EditorApplication.isPaused = true; return;
            }
            using var covers = em.CreateEntityQuery(typeof(RotatingCover));
            using var bodies = em.CreateEntityQuery(typeof(Enemy), typeof(BarricadeCrowdMember));
            if (covers.CalculateEntityCount() != 1 || bodies.CalculateEntityCount() != 16) return;
            var coverEntity = covers.GetSingletonEntity(); var cover = em.GetComponentData<RotatingCover>(coverEntity);
            var wall = em.GetComponentData<Barricade>(cover.Target);
            double elapsed = world.Time.ElapsedTime - simulationSince;
            if (step == 1)
            {
                Require(wall.HitsRemaining == 4, "Four baked target hits");
                Require(math.abs(em.GetComponentData<RotatingCoverState>(coverEntity).Angle - cover.InitialAngle) > .01f,
                    "Default cover rotates while initial crowd spawns");
                using var panels = em.CreateEntityQuery(typeof(CoverPanel)); Require(panels.CalculateEntityCount() == 36, "36 rendered cover panels");
                using var enemies = bodies.ToEntityArray(Allocator.Temp);
                int i = 0;
                foreach (var e in enemies)
                {
                    if (em.HasComponent<ExplosiveEnemyState>(e)) { if (explosive == Entity.Null) explosive = e; else outsideExplosive = e; }
                    else if (source == Entity.Null) source = e;
                    var movement = em.GetComponentData<EnemyMovementSettings>(e); movement.MoveSpeed = 0; em.SetComponentData(e, movement);
                    var transform = em.GetComponentData<LocalTransform>(e); transform.Position = new float3(-12 + (i % 7) * 4, .05f, 9 + (i / 7) * 2);
                    em.SetComponentData(e, transform); em.SetComponentData(e, new PhysicsVelocity()); i++;
                }
                cover.RadiansPerSecond = 0; em.SetComponentData(coverEntity, cover);
                SetAngle(em, coverEntity, 0);
                player.transform.position = new Vector3(3, .5f, -11);
                Record("PASS baked target, shield panels, bounded 16-root crowd"); Next(2, world);
            }
            else if (step == 2 && elapsed > .3) { Launch(em, source); Next(3, world); }
            else if (step == 3 && elapsed > .25)
            {
                var velocity = em.GetComponentData<PhysicsVelocity>(source).Linear;
                Require(wall.HitsRemaining == 4 && velocity.z < 0 && velocity.x > 0, "Cover reflects toward offset player, not incoming line: " + velocity);
                Require(em.GetComponentData<EnemyLaunchState>(source).HomingTarget == Entity.Null, "Reflection clears homing");
                Record("PASS reflected body aimed toward sampled player; core undamaged; homing cleared");
                Park(em, source); Launch(em, explosive); Next(4, world);
            }
            else if (step == 4 && elapsed > .25)
            {
                Require(em.GetComponentData<ExplosiveEnemyState>(explosive).HasExploded == 0, "Exploder must not detonate on shield");
                Require(em.GetComponentData<PhysicsVelocity>(explosive).Linear.z < 0 && wall.HitsRemaining == 4, "Exploder reflected");
                Record("PASS exploder reflects without detonation or target damage"); Park(em, explosive);
                SetAngle(em, coverEntity, math.PI); Next(5, world);
            }
            else if (step == 5 && elapsed > .1)
            {
                var bridge = Object.FindFirstObjectByType<PlayerEcsBridge>();
                bridge.PublishMovement(new Vector3(0,.5f,-7), new Vector3(0,.5f,0), .5f, .02f);
                world.GetOrCreateSystem<PlayerObstacleCollisionSystem>().Update(world.Unmanaged);
                Require(bridge.ResolvedMovementPosition.z < -4.5f, "Player cannot walk through open shot aperture");
                Record("PASS hybrid player's swept movement blocked through opening");
                Launch(em, source);
                var walking = em.GetComponentData<EnemyLaunchState>(source);
                walking.Phase = EnemyLaunchPhase.Recovering; walking.RecoverySecondsRemaining = 2;
                em.SetComponentData(source, walking); Next(51, world);
            }
            else if (step == 51 && elapsed > .3)
            {
                Require(em.GetComponentData<LocalTransform>(source).Position.z < -4.5f, "Non-launched enemy cannot enter enclosure through opening");
                Record("PASS non-launched enemy solver contacts block entry through opening");
                Launch(em, source); Next(6, world);
            }
            else if (step == 6 && elapsed > .55)
            {
                Require(wall.HitsRemaining == 3, "Launched body passes enclosure through opening and hits target once; remaining=" + wall.HitsRemaining);
                Record("PASS launched body crosses enclosure and damages core once"); Park(em, source);
                var transform = em.GetComponentData<LocalTransform>(outsideExplosive); transform.Position = new float3(0,.05f,-5.6f); em.SetComponentData(outsideExplosive, transform);
                var blast = em.GetComponentData<ExplosiveEnemySettings>(outsideExplosive); blast.Radius = 20; em.SetComponentData(outsideExplosive, blast);
                em.SetComponentEnabled<ExplosiveDetonationRequest>(outsideExplosive, true); Next(7, world);
            }
            else if (step == 7 && elapsed > .1)
            {
                Require(wall.HitsRemaining == 3, "Outside explosion radius cannot damage core");
                Record("PASS outside explosion blocked despite overlapping core");
                // Restore the second exploder after the deliberately oversized blast chained into it.
                em.SetComponentData(explosive, new ExplosiveEnemyState());
                em.SetComponentEnabled<DeathRequest>(explosive, false); em.SetComponentEnabled<RespawnRequest>(explosive, false);
                Launch(em, explosive); Next(8, world);
            }
            else if (step == 8 && elapsed > .55)
            {
                Require(wall.HitsRemaining == 2, "Exploder impact plus blast is one hit; remaining=" + wall.HitsRemaining);
                Require(em.GetComponentData<ExplosiveEnemyState>(explosive).HasExploded != 0, "Core collision detonates exploder normally");
                Record("PASS admitted exploder impact and blast count once"); Launch(em, source); Next(9, world);
            }
            else if (step == 9 && elapsed > .55)
            {
                Require(wall.HitsRemaining == 1, "Third accepted hit"); Launch(em, source); Next(10, world);
            }
        }
        private static void Launch(EntityManager em, Entity e)
        {
            var transform = em.GetComponentData<LocalTransform>(e); transform.Position = new float3(0,.05f,-7); em.SetComponentData(e, transform);
            var launch = em.GetComponentData<EnemyLaunchState>(e); EnemyLaunchTransition.Begin(ref launch, EnemyLaunchCause.PlayerPunch, 10); em.SetComponentData(e, launch);
            em.SetComponentData(e, new PhysicsVelocity { Linear = new float3(0,0,24) });
            em.SetComponentData(e, new EnemyGroundConstraint { HasGround = 1, IsLocked = 1, Height = .05f });
            em.SetComponentEnabled<RespawnRequest>(e, false); em.SetComponentEnabled<DeathRequest>(e, false);
            em.SetComponentEnabled<ExternalImpulse>(e, false); em.SetComponentEnabled<DamageRequest>(e, false);
        }
        private static void Park(EntityManager em, Entity e)
        {
            var transform = em.GetComponentData<LocalTransform>(e); transform.Position = new float3(13,.05f,-10); em.SetComponentData(e, transform);
            em.SetComponentData(e, new PhysicsVelocity());
        }
        private static void SetAngle(EntityManager em, Entity cover, float angle)
        {
            em.SetComponentData(cover, new RotatingCoverState { Angle = angle });
            var transform = em.GetComponentData<LocalTransform>(cover); transform.Rotation = quaternion.RotateY(angle); em.SetComponentData(cover, transform);
        }
        private static void Next(int value, World world) { step = value; since = EditorApplication.timeSinceStartup; simulationSince = world.Time.ElapsedTime; }
        private static void Record(string value) => File.AppendAllText(Output, value + "\n");
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
