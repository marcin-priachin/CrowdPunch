using System;
using System.IO;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Presentation;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // Controlled live validation, not a balance playtest. All injected state is confined to Play Mode.
    [InitializeOnLoad]
    public static class ArmoredEncounterPlayCheck
    {
        private const string Key = "CrowdPunch.ArmoredCheck";
        private const string Output = "Temp/ArmoredValidation/playcheck.txt";
        private static int step, hit, replacementRounds;
        private static double since;
        private static Entity target, source, previousSpare;
        private static float initialHealth;
        static ArmoredEncounterPlayCheck() { EditorApplication.update += Tick; }
        public static void Start()
        {
            Directory.CreateDirectory("Temp/ArmoredValidation");
            File.WriteAllText(Output, "Controlled Editor validation: placed bodies, injected launches/defeats, restored player health. Not balance evidence.\n");
            step = hit = replacementRounds = 0; target = source = previousSpare = Entity.Null;
            EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Bootstrap.unity");
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        public static void Stop() => SessionState.SetBool(Key, false);
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try { Inspect(); }
            catch (Exception e) { Record("FAIL " + e); Stop(); EditorApplication.isPaused = true; Debug.LogException(e); }
        }
        private static void Inspect()
        {
            var flow = Object.FindFirstObjectByType<GauntletSequence>();
            var world = World.DefaultGameObjectInjectionWorld;
            if (flow == null || world == null || !world.IsCreated || flow.TransitionInProgress) return;
            var player = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (player != null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            double now = EditorApplication.timeSinceStartup;
            if (step == 0) { flow.SelectLevel(11); Next(1); return; }
            using var query = em.CreateEntityQuery(typeof(Enemy), typeof(EnemyWaveOwnership));
            using var enemies = query.ToEntityArray(Allocator.Temp);
            using var sequenceQuery = em.CreateEntityQuery(typeof(EnemyWaveSequence));
            if (step == 9)
            {
                using var bosses = em.CreateEntityQuery(typeof(BossEncounter));
                if (flow.CurrentLevelIndex != 10 || bosses.IsEmpty) return;
                // Scene load queues a restart. Wait for the supporting wave to prove that reset has run.
                if (sequenceQuery.CalculateEntityCount() != 1 || em.GetComponentData<EnemyWaveSequence>(
                    sequenceQuery.GetSingletonEntity()).Phase != EnemyWaveRuntimePhase.AwaitingActivation) return;
                var head = bosses.GetSingletonEntity(); var boss = em.GetComponentData<BossEncounter>(head);
                boss.Cycle = BossCycle.Defeated; em.SetComponentData(head, boss);
                Record("Injected boss defeat to exercise actual completion/progression"); Next(10); return;
            }
            if (step == 10)
            {
                if (flow.CurrentLevelIndex != 11) { Require(now - since < 30, "Boss did not advance"); return; }
                Require(!flow.RunComplete, "Boss incorrectly completed the run");
                using var bosses = em.CreateEntityQuery(typeof(BossEncounter));
                using var crowd = em.CreateEntityQuery(typeof(BossCrowdMember));
                Require(bosses.IsEmpty && crowd.IsEmpty, "Boss ownership leaked");
                Record("PASS boss completion advances to Gauntlet_12, removing boss and supporting crowd");
                Stop(); EditorApplication.isPaused = true; return;
            }
            if (sequenceQuery.CalculateEntityCount() != 1) return;
            Entity seq = sequenceQuery.GetSingletonEntity(); var sequence = em.GetComponentData<EnemyWaveSequence>(seq);
            if (step == 1)
            {
                if (sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation) return;
                foreach (var e in enemies)
                    if (em.HasComponent<EnemyArmor>(e)) target = e; else if (source == Entity.Null) source = e;
                Require(target != Entity.Null && source != Entity.Null && enemies.Length == 7, "Opening composition");
                Require(em.GetComponentData<EnemyArmor>(target).Stages == 3, "Fresh armor");
                if (em.GetComponentData<DesiredMovement>(target).Speed <= 0)
                { Require(now - since < 30, "Continuous pursuit not active"); return; }
                using var animationQuery = em.CreateEntityQuery(typeof(EnemyAnimation), typeof(EnemyAnimationPlayback));
                using var visuals = animationQuery.ToEntityArray(Allocator.Temp);
                int animated = 0;
                foreach (var v in visuals)
                {
                    var animation = em.GetComponentData<EnemyAnimation>(v);
                    if (animation.Owner != target) continue;
                    Require(animation.Profile == (byte)EnemyAnimationProfile.Armored && animation.Samples.IsCreated, "Baked profile/samples");
                    foreach (var skin in em.GetBuffer<SkinMatrix>(v)) Require(math.all(math.isfinite(skin.Value.c3)), "Invalid skin matrix");
                    animated++;
                }
                Require(animated > 0, "No baked Armored skin renderer");
                Record("PASS opening 1 Armored + 6 Baselines; sampled skin matrices finite; active pursuit");
                Capture("opening");
                int n = 0;
                foreach (var e in enemies)
                {
                    var movement = em.GetComponentData<EnemyMovementSettings>(e); movement.MoveSpeed = movement.WanderSpeed = 0;
                    em.SetComponentData(e, movement); Place(em, e, new float3(8, 0, -8 + n++ * 2));
                }
                Place(em, target, float3.zero); em.SetComponentData(target, new Health { Current = 100, Max = 100 });
                initialHealth = 100;
                var bridge = Object.FindFirstObjectByType<PlayerEcsBridge>();
                bridge.PublishPunchPreview(new Vector3(0,0,-1), Vector3.forward, .6f, 2, 3, 0, 10, 30);
                world.GetExistingSystem<PresentationBridgeSystem>().Update(world.Unmanaged);
                Require(bridge.TrajectoryPreviewSegments.Count == 0, "Protected enemy has a preview");
                using var pq = em.CreateEntityQuery(typeof(PlayerSnapshot)); var pe = pq.GetSingletonEntity();
                em.SetComponentData(pe, new PunchRequest { Origin = new float3(0,0,-1), Direction = math.forward(), Range = 2,
                    Radius = .6f, Damage = 20, Strength = 25 }); em.SetComponentEnabled<PunchRequest>(pe, true);
                world.GetExistingSystem<PunchDetectionSystem>().Update(world.Unmanaged);
                Require(em.GetComponentData<PunchRequest>(pe).HitEnemy && !em.IsComponentEnabled<ExternalImpulse>(target), "Blocked punch confirmation");
                Record("PASS protected punch confirms cooldown connection, no impulse or trajectory preview");
                Shoot(em); Next(2); return;
            }
            if (step == 2)
            {
                byte stages = em.GetComponentData<EnemyArmor>(target).Stages;
                if (stages == 3 - hit) { Require(now - since < 5, "Solver body did not hit armor"); return; }
                Require(stages == 2 - hit, "One body consumed multiple stages");
                if (hit < 2)
                {
                    Require(em.GetComponentData<Health>(target).Current == initialHealth, "Armor lost health");
                    Require(em.GetComponentData<EnemyLaunchState>(target).Phase == EnemyLaunchPhase.Active, "Armor launched early");
                }
                else Require(em.GetComponentData<EnemyLaunchState>(target).Phase == EnemyLaunchPhase.Launched, "Breaking body did not launch");
                Record($"PASS real solver impact {hit + 1}, cause {em.GetComponentData<EnemyLaunchState>(source).LastCause}, armor={stages}, health={em.GetComponentData<Health>(target).Current}");
                Capture("armor-" + stages); Place(em, source, new float3(-8, 0, 8));
                hit++; Next(3); return;
            }
            if (step == 3 && now - since > .7)
            {
                if (hit < 3) { Shoot(em); Next(2); return; }
                Require(em.GetComponentData<Health>(target).Current < initialHealth, "Break damage missing");
                em.SetComponentData(target, new PhysicsVelocity()); Next(4); return;
            }
            if (step == 4)
            {
                if (em.GetComponentData<EnemyLaunchState>(target).Phase != EnemyLaunchPhase.Active)
                { Require(now - since < 12, "Survivor did not recover"); return; }
                Require(em.GetComponentData<EnemyArmor>(target).Stages == 0, "Recovery restored armor");
                Record("PASS surviving broken enemy recovers unarmored");
                flow.RestartCurrentLevel(); Next(5); return;
            }
            if (step == 5)
            {
                if (sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation) return;
                foreach (var e in enemies) if (em.HasComponent<EnemyArmor>(e))
                { target = e; Require(em.GetComponentData<EnemyArmor>(e).Stages == 3, "Restart did not restore armor"); }
                foreach (var e in enemies) if (!em.HasComponent<EnemyArmor>(e)) Defeat(em, e);
                Record("PASS restart restores fresh armor; exhausted original ammunition"); Next(6); return;
            }
            if (step == 6)
            {
                Entity spare = Entity.Null; int living = 0;
                foreach (var e in enemies)
                    if (!em.HasComponent<EnemyArmor>(e) && !em.IsComponentEnabled<RespawnRequest>(e)
                        && em.GetComponentData<EnemyLaunchState>(e).Phase != EnemyLaunchPhase.Defeated) { spare = e; living++; }
                if (living == 0) { Require(now - since < 10, "Ammunition replacement did not spawn"); return; }
                Require(living == 1 && sequence.CurrentWaveIndex == 0, "Replacement duplicated or completed wave early");
                if (now - since < 1) return;
                Require(spare != previousSpare, "Replacement failed to renew"); previousSpare = spare;
                Record("PASS one safe owned replacement; cumulative undefeated=" + sequence.UndefeatedCount);
                if (++replacementRounds < 3) { Defeat(em, spare); Next(6); return; }
                Defeat(em, spare); Defeat(em, target); Next(7); return;
            }
            if (step == 7)
            {
                if (sequence.CurrentWaveIndex != 1 || sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation)
                { Require(now - since < 15, "Wave failed to advance"); return; }
                int count = 0; foreach (var e in enemies) if (em.GetComponentData<EnemyWaveOwnership>(e).WaveIndex == 1) count++;
                Require(count == 15, "Second wave composition"); Record("PASS replacements drain correctly; second wave has 15 enemies");
                foreach (var e in enemies) Defeat(em, e); Next(8); return;
            }
            if (step == 8)
            {
                if (!flow.RunComplete) { Require(now - since < 15, "Final gauntlet failed to complete"); return; }
                Record("PASS Gauntlet_12 final-level completion"); flow.SelectLevel(10); Next(9);
            }
        }
        private static void Shoot(EntityManager em)
        {
            Place(em, target, float3.zero); Place(em, source, new float3(0,0,-3));
            var launch = em.GetComponentData<EnemyLaunchState>(source);
            var causes = new[] { EnemyLaunchCause.PlayerPunch, EnemyLaunchCause.ElitePunch, EnemyLaunchCause.BossAttack };
            EnemyLaunchTransition.Begin(ref launch, causes[hit], 10); em.SetComponentData(source, launch);
            em.SetComponentData(source, new PhysicsVelocity { Linear = new float3(0,0,25) });
        }
        private static void Place(EntityManager em, Entity e, float3 position)
        { var t = em.GetComponentData<LocalTransform>(e); t.Position = position; em.SetComponentData(e,t); em.SetComponentData(e,new PhysicsVelocity()); }
        private static void Defeat(EntityManager em, Entity e)
        {
            if (em.HasComponent<EnemyArmor>(e)) { var armor = em.GetComponentData<EnemyArmor>(e); armor.Stages=0; em.SetComponentData(e,armor); }
            var l=em.GetComponentData<EnemyLaunchState>(e); l.Phase=EnemyLaunchPhase.Defeated; em.SetComponentData(e,l);
            em.SetComponentEnabled<DeathRequest>(e,true); em.SetComponentData(e,new PhysicsVelocity());
        }
        private static void Next(int value) { step=value; since=EditorApplication.timeSinceStartup; }
        private static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
        private static void Record(string value) => File.AppendAllText(Output,value+"\n");
        private static void Capture(string name)
        {
            var camera=Camera.main; if(camera==null)return;
            var rt=new RenderTexture(960,540,24); var old=camera.targetTexture; var active=RenderTexture.active;
            var image=new Texture2D(960,540,TextureFormat.RGB24,false);
            try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,960,540),0,0); image.Apply(); File.WriteAllBytes("Temp/ArmoredValidation/"+name+".png",image.EncodeToPNG()); }
            finally { camera.targetTexture=old; RenderTexture.active=active; Object.DestroyImmediate(image); Object.DestroyImmediate(rt); }
        }
    }
}
