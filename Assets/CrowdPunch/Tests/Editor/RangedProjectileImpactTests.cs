using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class RangedProjectileImpactTests
    {
        private World world;
        private Entity player;
        private GameObject go;
        private PlayerEcsBridge bridge, previous;
        private int bursts, hits;
        private Vector3 contact, normal;

        [SetUp] public void SetUp()
        {
            bursts = hits = 0; contact = normal = Vector3.zero;
            world = new World("Ranged splash contacts");
            player = world.EntityManager.CreateEntity(typeof(PlayerSnapshot));
            go = new GameObject("Splash bridge"); bridge = go.AddComponent<PlayerEcsBridge>();
            PlayerBridgeRegistry.TryGetBridge(out previous); PlayerBridgeRegistry.Register(bridge);
            bridge.ProjectileBurstReceived += (kind, point, direction, scale) => { bursts++; contact = point; normal = direction; };
            bridge.EnemyContactHitReceived += (damage, protection, push) => hits++;
            world.SetTime(new TimeData(1, 1));
        }
        [TearDown] public void TearDown()
        {
            world.Dispose(); PlayerBridgeRegistry.Unregister(bridge);
            if (previous != null) PlayerBridgeRegistry.Register(previous);
            Object.DestroyImmediate(go);
        }
        private Entity Shot(float3 start, float3 end, float lifetime = 10)
        {
            var em = world.EntityManager;
            var e = em.CreateEntity(typeof(LocalTransform), typeof(RangedProjectile));
            em.SetComponentData(e, LocalTransform.FromPosition(start));
            em.SetComponentData(e, new RangedProjectile { Start = start, Target = end, TravelDuration = 1,
                Radius = .2f, MinimumAltitude = -10, Lifetime = lifetime, PlayerCollisionLayers = 1, Damage = 10 });
            return e;
        }
        private void Ground() => world.EntityManager.AddComponentData(world.EntityManager.CreateEntity(), new ArenaGroundPlane());
        private void Tick() => world.GetOrCreateSystemManaged<RangedProjectileSystem>().Update();

        [Test] public void Enemy003FastGroundCrossingSplashesAtSurfaceOnce()
        {
            Ground(); var shot = Shot(new float3(0, 2, 0), new float3(4, -2, 0));
            Tick(); Tick();
            Assert.That(world.EntityManager.Exists(shot), Is.False);
            Assert.That(bursts, Is.EqualTo(1)); Assert.That(hits, Is.Zero);
            Assert.That(contact.x, Is.EqualTo(1.8f).Within(.001f));
            Assert.That(contact.y, Is.EqualTo(.025f).Within(.001f)); Assert.That(normal, Is.EqualTo(Vector3.up));
        }
        [Test] public void Enemy003PlayerCrossingSplashesAtEntryAndConsumesShotOnce()
        {
            world.EntityManager.SetComponentData(player, new PlayerSnapshot { IsAvailable = true,
                Position = new float3(0, 1, 0), Radius = .5f, CollisionLayer = 1 });
            var shot = Shot(new float3(-3, 1, 0), new float3(3, 1, 0)); Tick(); Tick();
            Assert.That(world.EntityManager.Exists(shot), Is.False);
            Assert.That(bursts, Is.EqualTo(1)); Assert.That(hits, Is.EqualTo(1));
            Assert.That(contact.x, Is.EqualTo(-.5f).Within(.001f)); Assert.That(normal.x, Is.LessThan(-.99f));
        }
        [Test] public void Enemy003GroundBeforePlayerCannotDamageThroughFloor()
        {
            Ground(); world.EntityManager.SetComponentData(player, new PlayerSnapshot { IsAvailable = true,
                Position = new float3(3, -1, 0), Radius = .3f, CollisionLayer = 1 });
            Shot(new float3(0, 2, 0), new float3(4, -2, 0)); Tick();
            Assert.That(hits, Is.Zero); Assert.That(bursts, Is.EqualTo(1)); Assert.That(normal, Is.EqualTo(Vector3.up));
        }
        [Test] public void Enemy003ExpiryInAirIsSilentCleanup()
        {
            var shot = Shot(new float3(0, 3, 0), new float3(1, 3, 0), .1f); Tick();
            Assert.That(world.EntityManager.Exists(shot), Is.False); Assert.That(bursts, Is.Zero);
        }
        [Test] public void Enemy003ExcludedPlayerLayerDoesNotSplashOrDamage()
        {
            world.EntityManager.SetComponentData(player, new PlayerSnapshot { IsAvailable = true,
                Position = new float3(0, 1, 0), Radius = .5f, CollisionLayer = 2 });
            var shot = Shot(new float3(-3, 1, 0), new float3(3, 1, 0)); Tick();
            Assert.That(world.EntityManager.Exists(shot), Is.True); Assert.That(bursts, Is.Zero); Assert.That(hits, Is.Zero);
        }
    }
}
