using CrowdPunch.Configuration;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class CombatVfxTests
    {
        [Test]
        public void Info004DasherBridgeKeepsEffectsThroughDashAndStopsOnRecoveryOrLaunch()
        {
            using var world = new World("Dasher effect phases");
            var em = world.EntityManager;
            var go = new GameObject("Dasher effect bridge");
            var settings = ScriptableObject.CreateInstance<CombatFeedbackSettings>();
            var bridge = go.AddComponent<PlayerEcsBridge>(); bridge.FeedbackSettings = settings;
            PlayerBridgeRegistry.TryGetBridge(out var previous);
            PlayerBridgeRegistry.Register(bridge);
            int samples = 0; float progress = -1;
            bridge.DasherTelegraphReceived += (_, __, ___, value) => { samples++; progress = value; };
            try
            {
                var enemy = em.CreateEntity(typeof(Enemy), typeof(DasherState), typeof(DasherSettings),
                    typeof(EnemyLaunchState), typeof(LocalTransform), typeof(RespawnRequest));
                em.SetComponentEnabled<RespawnRequest>(enemy, false);
                em.SetComponentData(enemy, LocalTransform.Identity);
                em.SetComponentData(enemy, new DasherSettings { TelegraphDuration = 2 });
                var system = world.GetOrCreateSystem<DasherTelegraphBridgeSystem>();
                foreach (var phase in new[] { DasherPhase.Preparing, DasherPhase.Dashing, DasherPhase.Recovering })
                {
                    em.SetComponentData(enemy, new DasherState { Phase = phase, SecondsRemaining = 1, LockedDirection = math.forward() });
                    system.Update(world.Unmanaged);
                    if (phase == DasherPhase.Preparing) { Assert.That(samples, Is.EqualTo(1)); Assert.That(progress, Is.EqualTo(.5f)); }
                    else if (phase == DasherPhase.Dashing) { Assert.That(samples, Is.EqualTo(2)); Assert.That(progress, Is.EqualTo(1)); }
                    else Assert.That(samples, Is.EqualTo(2));
                }
                em.SetComponentData(enemy, new DasherState { Phase = DasherPhase.Dashing });
                em.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
                system.Update(world.Unmanaged);
                Assert.That(samples, Is.EqualTo(2), "Shared launch effects take over on interruption.");
            }
            finally
            {
                PlayerBridgeRegistry.Unregister(bridge);
                if (previous != null) PlayerBridgeRegistry.Register(previous);
                Object.DestroyImmediate(go); Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Info004DasherCartoonEffectsRampFollowDashAndClearBeforePoolReuse()
        {
            var settings = Object.Instantiate(Resources.Load<CombatFeedbackSettings>("CombatFeedbackSettings"));
            settings.ParticlePoolSizePerEffect = 1;
            var go = new GameObject("Dasher particles test");
            try
            {
                Assert.That(settings.DasherTelegraphParticles, Is.Not.Null);
                var pool = new DasherTelegraphParticlePool(go.transform, settings, null);
                pool.Begin(); pool.Show(1, Vector3.up, Vector3.forward, 0); pool.End();
                var effect = go.GetComponentInChildren<ParticleSystem>();
                var layers = effect.GetComponentsInChildren<ParticleSystem>();
                Assert.That(layers.Length, Is.EqualTo(2));
                float initialRate = effect.emission.rateOverTimeMultiplier;
                pool.Begin(); pool.Show(1, new Vector3(4, 1, 2), Vector3.right, 1); pool.End();
                Assert.That(effect.emission.rateOverTimeMultiplier, Is.GreaterThan(initialRate));
                Assert.That(Vector3.Distance(effect.transform.position, new Vector3(4, .1f, 2)), Is.LessThan(.0001f));
                Assert.That(effect.transform.localScale, Is.EqualTo(Vector3.one));
                foreach (var layer in layers)
                {
                    Assert.That(layer.isPlaying, Is.True);
                    Assert.That(layer.main.loop, Is.True);
                    Assert.That(layer.main.maxParticles, Is.LessThanOrEqualTo(24));
                    Assert.That(layer.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                    Assert.That(layer.GetComponent<ParticleSystemRenderer>().sharedMaterial, Is.Not.Null);
                }
                pool.Begin(); pool.End();
                Assert.That(effect.IsAlive(true), Is.False, "Recovery or interruption clears every layer.");
                pool.Begin(); pool.Show(2, Vector3.up, Vector3.forward, 0); pool.End();
                Assert.That(go.transform.childCount, Is.EqualTo(1));
                Assert.That(effect.emission.rateOverTimeMultiplier, Is.EqualTo(initialRate));
                pool.Clear();
                Assert.That(effect.IsAlive(true), Is.False);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(settings); }
        }

        [Test]
        public void Vision005ProjectileSaturationRetainsBossBudgetAndReusesAfterDespawn()
        {
            var library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            Assert.That(library, Is.Not.Null);
            var go = new GameObject("Projectile VFX lifecycle test");
            var bridge = go.AddComponent<PlayerEcsBridge>();
            var presentation = go.AddComponent<ProjectileVfxPresentation>();
            typeof(ProjectileVfxPresentation).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(presentation, null);
            try
            {
                bridge.BeginProjectileVisuals();
                for (ulong i = 1; i <= 500; i++)
                    bridge.ReceiveProjectileVisual(new ProjectileVisualSample { Key = i,
                        Kind = ProjectileVisualKind.Ranged, Position = Vector3.right, Radius = .3f });
                bridge.ReceiveProjectileVisual(new ProjectileVisualSample { Key = 1000,
                    Kind = ProjectileVisualKind.Boss, Radius = .6f });
                bridge.EndProjectileVisuals();
                Assert.That(presentation.ActiveFlightCount, Is.EqualTo(library.RangedCapacity + 1),
                    "Crowd projectiles must not consume the boss's reserved visual budget.");
                for (ulong i = 2000; i < 2500; i++)
                    bridge.ReceiveProjectileVisual(new ProjectileVisualSample { Key = i,
                        Kind = ProjectileVisualKind.RangedCharge, Position = Vector3.up, Radius = .13f, ChargeProgress = .8f });
                Assert.That(presentation.ActiveFlightCount, Is.EqualTo(library.RangedCapacity * 2 + 1),
                    "Charging enemies have their own bounded budget and cannot displace flying shots.");
                int capacity = presentation.FlightCapacity;
                bridge.BeginProjectileVisuals(); bridge.EndProjectileVisuals();
                Assert.That(presentation.ActiveFlightCount, Is.Zero);
                bridge.BeginProjectileVisuals();
                bridge.ReceiveProjectileVisual(new ProjectileVisualSample { Key = 2000,
                    Kind = ProjectileVisualKind.Boss, Radius = .6f, Sequence = 2, Redirected = true });
                bridge.EndProjectileVisuals();
                Assert.That(presentation.ActiveFlightCount, Is.EqualTo(1));
                Assert.That(presentation.FlightCapacity, Is.EqualTo(capacity));
                presentation.enabled = false;
                typeof(ProjectileVfxPresentation).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(presentation, null);
                Assert.That(presentation.ActiveFlightCount, Is.Zero);
                presentation.enabled = true;
                typeof(ProjectileVfxPresentation).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(presentation, null);
                Assert.That(presentation.FlightCapacity, Is.EqualTo(capacity));
            }
            finally
            {
                typeof(ProjectileVfxPresentation).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(presentation, null);
                typeof(ProjectileVfxPresentation).GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(presentation, null);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Vision005BurstOverloadIsBoundedAndClearAllowsImmediateReuse()
        {
            var library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            var go = new GameObject("Burst overload test");
            try
            {
                var pool = new ParticleBurstPool(go.transform, library.RangedImpact, 3);
                for (int i = 0; i < 500; i++) pool.Show(Vector3.zero, Vector3.forward);
                Assert.That(go.transform.childCount, Is.EqualTo(3));
                foreach (Transform child in go.transform)
                    Assert.That(child.GetComponent<ParticleSystem>().isPlaying, Is.True);
                pool.Clear();
                foreach (Transform child in go.transform)
                    Assert.That(child.GetComponent<ParticleSystem>().IsAlive(true), Is.False);
                pool.Show(Vector3.one, Vector3.right);
                Assert.That(go.transform.GetChild(0).GetComponent<ParticleSystem>().isPlaying, Is.True);
                Assert.That(go.transform.GetChild(0).position, Is.EqualTo(Vector3.one));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

