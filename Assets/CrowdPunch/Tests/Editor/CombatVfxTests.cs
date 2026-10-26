using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using NUnit.Framework;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class CombatVfxTests
    {
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

