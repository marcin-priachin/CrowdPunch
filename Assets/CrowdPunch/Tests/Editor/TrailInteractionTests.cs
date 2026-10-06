using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Lifetime;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class TrailInteractionTests
    {
        private World world;
        private EntityManager em;
        private Entity target, source, record, section, player;
        [SetUp] public void Setup()
        {
            world = new World("TRAIL interaction contracts"); em = world.EntityManager;
            target = em.CreateEntity(typeof(Enemy), typeof(Health), typeof(EnemyLifetime), typeof(EnemyArchetype),
                typeof(EnemyTier), typeof(EnemyLaunchState), typeof(LocalTransform), typeof(NavigationAgent),
                typeof(EnemyDamageState), typeof(DamageRequest), typeof(DeathRequest), typeof(RespawnRequest),
                typeof(EnemyHealthBarVisibility), typeof(PhysicsVelocity), typeof(DesiredMovement), typeof(NavigationIntent));
            em.SetComponentData(target, new Health { Current = 100, Max = 100 });
            em.SetComponentData(target, new EnemyLifetime { Generation = 1 });
            em.SetComponentData(target, LocalTransform.Identity);
            em.SetComponentData(target, new NavigationAgent { Radius = .5f });
            em.SetComponentEnabled<RespawnRequest>(target, false); em.SetComponentEnabled<DamageRequest>(target, false);
            em.SetComponentEnabled<DeathRequest>(target, false);
            source = em.CreateEntity(); record = em.CreateEntity(typeof(TrailSource)); em.AddBuffer<TrailDamageTarget>(record);
            em.SetComponentData(record, new TrailSource { Enemy = source, Lifetime = 1, ExpiresAt = 10 });
            section = em.CreateEntity(typeof(TrailSection));
            em.SetComponentData(section, new TrailSection { Record = record, Source = source, SourceLifetime = 1,
                Start = new float3(-1, -20, 0), End = new float3(1, -20, 0), Width = 1, Damage = 4,
                DamagesEnemies = 1, TickInterval = .25f, PlayerProtection = 1, ExpiresAt = 10,
                Avoidance = TrailAvoidanceMode.DamagingTrails });
            player = em.CreateEntity(typeof(PlayerSnapshot));
        }
        [TearDown] public void TearDown() => world.Dispose();
        private void Step(double time)
        {
            world.SetTime(new TimeData(time, .02f));
            world.GetOrCreateSystem<TrailDamageSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
        }
        private float Hp => em.GetComponentData<Health>(target).Current;

        [Test] public void Trail004_IntactArmorReceivesNoDamageOrConsumedClock()
        {
            em.AddComponentData(target, EnemyArmor.Fresh); Step(0); Assert.That(Hp, Is.EqualTo(100));
            Assert.That(em.GetBuffer<TrailDamageTarget>(record).Length, Is.Zero);
            em.SetComponentData(target, new EnemyArmor()); Step(.01); Assert.That(Hp, Is.EqualTo(96));
        }
        [Test] public void Trail004_EliteDamagesWithoutLaunchingOrChangingVelocity()
        {
            em.SetComponentData(target, new EnemyTier { Value = EnemyCombatTier.Elite });
            em.SetComponentData(target, new PhysicsVelocity { Linear = new float3(2, 3, 4) }); Step(0);
            Assert.That(Hp, Is.EqualTo(96));
            Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear, Is.EqualTo(new float3(2, 3, 4)));
        }
        [Test] public void Trail004_BossAndEnvironmentAreUnaffected()
        {
            em.AddComponent<BossPart>(target); Step(0); Assert.That(Hp, Is.EqualTo(100));
            em.RemoveComponent<BossPart>(target); em.RemoveComponent<Enemy>(target); Step(1); Assert.That(Hp, Is.EqualTo(100));
        }
        [Test] public void Trail004_LethalDamageDoesNotRequestExplosion()
        {
            em.AddComponent<ExplosiveEnemyState>(target); em.AddComponent<ExplosiveDetonationRequest>(target);
            em.SetComponentEnabled<ExplosiveDetonationRequest>(target, false);
            em.SetComponentData(target, new Health { Current = 4, Max = 100 }); Step(0);
            Assert.That(Hp, Is.Zero); Assert.That(em.IsComponentEnabled<ExplosiveDetonationRequest>(target), Is.False);
            Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Defeated));
        }
        [Test] public void Trail003_PlayerProtectionIsPerSourceAndIndependentOfEnemyTimer()
        {
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Radius = .5f });
            Step(0); using var q = em.CreateEntityQuery(typeof(TrailPlayerHit));
            Assert.That(q.GetSingletonBuffer<TrailPlayerHit>().Length, Is.EqualTo(1));
            Step(.3); Assert.That(q.GetSingletonBuffer<TrailPlayerHit>().Length, Is.Zero); Assert.That(Hp, Is.EqualTo(92));
            Entity secondRecord = em.CreateEntity(typeof(TrailSource)); em.AddBuffer<TrailDamageTarget>(secondRecord);
            var second = em.GetComponentData<TrailSection>(section); second.Record = secondRecord;
            em.AddComponentData(em.CreateEntity(), second); Step(.31);
            Assert.That(q.GetSingletonBuffer<TrailPlayerHit>().Length, Is.EqualTo(1), "Different source stacks during first source protection");
            Step(1.01); Assert.That(q.GetSingletonBuffer<TrailPlayerHit>().Length, Is.EqualTo(1), "Only first source is ready again");
        }
        [Test] public void Trail003_PooledTargetLifetimeStartsNewContactClock()
        {
            Step(0); Assert.That(Hp, Is.EqualTo(96));
            em.SetComponentData(target, new EnemyLifetime { Generation = 2 }); Step(.01); Assert.That(Hp, Is.EqualTo(92));
        }
        [TestCase(EnemyLaunchPhase.Active, DasherPhase.Positioning, true)]
        [TestCase(EnemyLaunchPhase.Active, DasherPhase.Dashing, false)]
        [TestCase(EnemyLaunchPhase.Launched, DasherPhase.Positioning, false)]
        public void Trail005_AvoidanceRespectsCommittedDashAndLaunch(EnemyLaunchPhase phase, DasherPhase dash, bool avoids)
        {
            em.AddComponentData(target, new DasherState { Phase = dash });
            em.SetComponentData(target, new EnemyLaunchState { Phase = phase });
            world.GetOrCreateSystem<TrailAvoidanceSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(math.lengthsq(em.GetComponentData<NavigationIntent>(target).Separation) > 0, Is.EqualTo(avoids));
        }
        [Test] public void Trail002_SourceDeathDoesNotExpireButSceneOwnerLossDoes()
        {
            em.DestroyEntity(source);
            world.GetOrCreateSystem<TrailExpirySystem>().Update(world.Unmanaged); Assert.That(em.Exists(section), Is.True);
            var scene = em.CreateEntity(); var r = em.GetComponentData<TrailSource>(record); r.SceneOwner = scene; em.SetComponentData(record, r);
            em.DestroyEntity(scene); world.GetExistingSystem<TrailExpirySystem>().Update(world.Unmanaged);
            Assert.That(em.Exists(section), Is.False); Assert.That(em.Exists(record), Is.False);
        }
    }
}
