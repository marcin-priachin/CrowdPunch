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
    public static class ShellPlayCheck
    {
        private const string Key = "CrowdPunch.ShellCheck";
        private const string Output = "Temp/ShellValidation/playcheck.txt";
        private static int step;
        private static double since, simulationSince;
        private static Entity body, a, b;
        static ShellPlayCheck() { since = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Bootstrap.unity");
            Directory.CreateDirectory("Temp/ShellValidation");
            File.WriteAllText(Output, "SHELL-001..006 controlled baked-world checks. Injected positions/actions; not balance evidence.\n");
            step = 0; body = a = b = Entity.Null;
            since = EditorApplication.timeSinceStartup;
            SessionState.SetBool(Key, true); EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
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
            Require(EditorApplication.timeSinceStartup - since < 90, "Step timed out");
            var flow = Object.FindFirstObjectByType<GauntletSequence>();
            var world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || !world.IsCreated || flow.TransitionInProgress) return;
            var player = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (player != null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            if (step == 0) { flow.SelectLevel(14); Next(1, world); return; }
            using var targets = em.CreateEntityQuery(typeof(ShellTarget));
            using var bodies = em.CreateEntityQuery(typeof(Enemy), typeof(BarricadeCrowdMember));
            if (targets.CalculateEntityCount() != 1 || bodies.CalculateEntityCount() != 14) return;
            var target = targets.GetSingletonEntity(); var shell = em.GetComponentData<ShellTarget>(target);
            double elapsed = world.Time.ElapsedTime - simulationSince;
            if (step == 1)
            {
                Require(shell.ExplosionsRemaining == 3 && shell.CoreHealth == 5, "Baked three-hit shell and baseline-health core");
                using var enemies = bodies.ToEntityArray(Allocator.Temp);
                int i = 0;
                foreach (var e in enemies)
                {
                    if (em.HasComponent<ExplosiveEnemyState>(e)) { if (a == Entity.Null) a = e; else b = e; }
                    else if (body == Entity.Null) body = e;
                    var movement = em.GetComponentData<EnemyMovementSettings>(e); movement.MoveSpeed = 0; em.SetComponentData(e, movement);
                    Park(em, e, new float3(-11 + (i % 7) * 3.5f, .05f, 9 + (i / 7) * 2)); i++;
                }
                Require(a != Entity.Null && b != Entity.Null, "Two exploders baked");
                Require(PunchAimAssist.IsValidTarget(em, body, target), "Intact shell accepts ordinary aim lock");
                player.transform.position = new Vector3(0, .5f, -10);
                Punch(world, em, 2);
                Require(em.GetComponentData<ShellTarget>(target).ExplosionsRemaining == 3, "Punch blocked");
                Require(em.GetComponentData<ShellTarget>(target).LastHitBlocked == 1, "Blocked feedback");
                Record("PASS baked geometry, 12 baseline + 2 exploders, aim target, blocked punch confirmation");
                Launch(em, body); Next(2, world);
            }
            else if (step == 2 && elapsed > .4)
            {
                Require(shell.ExplosionsRemaining == 3 && em.GetComponentData<PhysicsVelocity>(body).Linear.z < 0, "Body rebounds without shell damage");
                Require(em.GetComponentData<EnemyLaunchState>(body).Phase == EnemyLaunchPhase.Launched, "Rebound continues launch");
                Park(em, body, new float3(-10, .05f, 7));
                var bridge = Object.FindFirstObjectByType<PlayerEcsBridge>();
                bridge.PublishMovement(new Vector3(0,.5f,-6), new Vector3(0,.5f,0), .5f, .02f);
                world.GetOrCreateSystem<PlayerObstacleCollisionSystem>().Update(world.Unmanaged);
                Require(bridge.ResolvedMovementPosition.z < -1.6f, "Hybrid player blocked");
                Record("PASS launched body rebounds and hybrid player cannot cross solid target");
                Detonate(em, a); Next(3, world);
            }
            else if (step == 3 && elapsed > 5)
            {
                Require(shell.ExplosionsRemaining == 2 && shell.CoreHealth == 5, "Unlaunched explosion counts once");
                Require(em.GetComponentData<EnemyRespawnSettings>(a).Enabled == 0, "No replacement while partner lives");
                Require(em.IsComponentEnabled<RespawnRequest>(a), "First exploder stays pooled");
                Record("PASS unlaunched blast damages shell; one survivor suppresses replacement beyond delay");
                Detonate(em, b); Next(4, world);
            }
            else if (step == 4 && elapsed > 3 && !em.IsComponentEnabled<RespawnRequest>(a)
                && !em.IsComponentEnabled<RespawnRequest>(b)
                && em.GetComponentData<ExplosiveEnemyState>(a).HasExploded == 0
                && em.GetComponentData<ExplosiveEnemyState>(b).HasExploded == 0)
            {
                Require(shell.ExplosionsRemaining == 1, "Second blast persists");
                Record("PASS both existing exploder slots replenish after zero survivors and delay; still 14 roots");
                Park(em, b, new float3(10,.05f,10)); Detonate(em, a); Next(5, world);
            }
            else if (step == 5 && elapsed > .2)
            {
                Require(shell.ExplosionsRemaining == 0 && shell.CoreHealth == 5, "Breaking blast leaves core untouched");
                Require(!flow.RunComplete, "Shell break cannot win");
                Require(PunchAimAssist.IsValidTarget(em, body, target), "Exposed core retains aim lock");
                Require(em.GetComponentData<EnemyRespawnSettings>(body).Enabled != 0, "Baselines continue replenishing");
                Record("PASS permanent exposure, untouched core, same aim target and continuing baseline supply");
                shell.CoreHealth = shell.CoreMaxHealth = 100; em.SetComponentData(target, shell);
                Launch(em, body); Next(6, world);
            }
            else if (step == 6 && elapsed > .4)
            {
                Require(shell.CoreHealth < 100 && em.GetComponentData<PhysicsVelocity>(body).Linear.z < 0, "Core takes impact and body bounces");
                Park(em, body, new float3(-10,.05f,7));
                shell.CoreHealth = 100; em.SetComponentData(target, shell);
                Launch(em, b); Next(7, world);
            }
            else if (step == 7 && elapsed > .5)
            {
                float blast = em.GetComponentData<ExplosiveEnemySettings>(b).Damage;
                Require(shell.CoreHealth < 100 - blast, "Exposed core receives impact plus separate blast: " + shell.CoreHealth);
                Require(em.GetComponentData<ExplosiveEnemyState>(b).HasExploded != 0, "Impact detonates exploder");
                Record("PASS core rebound, normal impact damage and additive exploder impact/blast damage"); Next(8, world);
            }
            else if (step == 8 && elapsed > 5)
            {
                Require(em.GetComponentData<EnemyRespawnSettings>(a).Enabled == 0 && em.GetComponentData<EnemyRespawnSettings>(b).Enabled == 0,
                    "No guaranteed exploders after exposure");
                shell.CoreHealth = 1; em.SetComponentData(target, shell); Punch(world, em, 2); Next(9, world);
            }
            else if (step == 9 && flow.RunComplete)
            {
                Require(shell.CoreHealth == 0, "Core death immediately wins with crowd survivors");
                Record("PASS no exploder replacement after shell break; direct core punch wins without cleanup");
                flow.RestartCurrentLevel(); Next(10, world);
            }
            else if (step == 10)
            {
                Require(shell.CoreHealth == 5 && shell.ExplosionsRemaining == 3 && !flow.RunComplete, "Retry resets objective and completion");
                Require(em.GetBuffer<BarricadeHitHistory>(target).Length == 0, "Retry clears source history");
                Record("PASS restart rebakes fresh shell/core, crowd and completion"); Record("COMPLETE");
                Stop(); EditorApplication.isPaused = true;
            }
        }
        private static void Punch(World world, EntityManager em, float damage)
        {
            using var q = em.CreateEntityQuery(typeof(PlayerSnapshot)); var p = q.GetSingletonEntity();
            em.SetComponentData(p, new PunchRequest { Origin = new float3(0,.5f,-3), Direction = new float3(0,0,1), Range = 3, Radius = 1, Damage = damage });
            em.SetComponentEnabled<PunchRequest>(p, true);
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            Require(em.GetComponentData<PunchRequest>(p).HitEnemy, "Punch connection confirmed for cooldown");
        }
        private static void Launch(EntityManager em, Entity e)
        {
            Park(em, e, new float3(0,.05f,-5));
            var launch = em.GetComponentData<EnemyLaunchState>(e); EnemyLaunchTransition.Begin(ref launch, EnemyLaunchCause.PlayerPunch, 10); em.SetComponentData(e, launch);
            em.SetComponentData(e, new PhysicsVelocity { Linear = new float3(0,0,24) });
            em.SetComponentData(e, new EnemyGroundConstraint { HasGround = 1, IsLocked = 1, Height = .05f });
        }
        private static void Detonate(EntityManager em, Entity e)
        {
            Park(em, e, new float3(0,.05f,-3));
            em.SetComponentData(e, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentEnabled<ExplosiveDetonationRequest>(e, true);
        }
        private static void Park(EntityManager em, Entity e, float3 position)
        {
            var t = em.GetComponentData<LocalTransform>(e); t.Position = position; em.SetComponentData(e, t);
            em.SetComponentData(e, new PhysicsVelocity());
        }
        private static void Next(int value, World world) { step = value; since = EditorApplication.timeSinceStartup; simulationSince = world.Time.ElapsedTime; }
        private static void Record(string value) => File.AppendAllText(Output, value + "\n");
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
