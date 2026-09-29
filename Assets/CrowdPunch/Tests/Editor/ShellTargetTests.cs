using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Presentation;
using CrowdPunch.Mono.Levels;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class ShellTargetTests
    {
        private World world;
        private EntityManager em;
        private Entity target, source, sequence;
        [SetUp] public void Setup()
        {
            world = new World("SHELL requirements"); em = world.EntityManager;
            target = em.CreateEntity(typeof(Barricade), typeof(ShellTarget), typeof(PhysicsCollider), typeof(LocalTransform));
            em.AddBuffer<BarricadeHitHistory>(target);
            em.SetComponentData(target, new Barricade { HitsRemaining = 1, RequiredHits = 1, CompleteOnDestruction = 1 });
            em.SetComponentData(target, new ShellTarget { RequiredExplosions = 3, ExplosionsRemaining = 3,
                CoreHealth = 5, CoreMaxHealth = 5, ReplaceExplodersAt = -1, ExploderReplacementDelay = 2 });
            em.SetComponentData(target, LocalTransform.Identity);
            source = em.CreateEntity(typeof(EnemyLaunchState));
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            sequence = em.CreateEntity(typeof(EnemyWaveSequence));
            em.SetComponentData(sequence, new EnemyWaveSequence { Phase = EnemyWaveRuntimePhase.AwaitingActivation });
        }
        [TearDown] public void Cleanup() => world.Dispose();

        [Test] public void Shell002_OnlyExplosionsBreakShellAndBreakingBlastCannotLeak()
        {
            ShellHitResolution.Punch(em, target, 100, 0, default);
            Assert.That(em.GetComponentData<ShellTarget>(target).LastHitBlocked, Is.EqualTo(1));
            Assert.That(ShellHitResolution.Impact(em, target, source, 1, 100, 0, default), Is.False);
            for (int i = 0; i < 3; i++) ShellHitResolution.Explosion(em, target, 100, i, default);
            var shell = em.GetComponentData<ShellTarget>(target);
            Assert.That(shell.ExplosionsRemaining, Is.Zero);
            Assert.That(shell.CoreHealth, Is.EqualTo(5));
            Assert.That(em.GetComponentData<Barricade>(target).HitsRemaining, Is.EqualTo(1));
            Assert.That(PunchAimAssist.IsValidTarget(em, source, target), Is.True);
            ShellHitResolution.Punch(em, target, 2, 4, default);
            Assert.That(em.GetComponentData<ShellTarget>(target).CoreHealth, Is.EqualTo(3));
        }

        [Test] public void Shell003_ImpactDeduplicatesButExplosionDealsSeparateDamage()
        {
            Expose(100);
            Assert.That(ShellHitResolution.Impact(em, target, source, 1, 7, 0, default), Is.True);
            Assert.That(ShellHitResolution.Impact(em, target, source, 1, 7, 1, default), Is.False);
            ShellHitResolution.Explosion(em, target, 20, 1, default);
            Assert.That(em.GetComponentData<ShellTarget>(target).CoreHealth, Is.EqualTo(73));
            Assert.That(ShellHitResolution.Impact(em, target, source, 2, 7, 2, default), Is.True);
            Assert.That(em.GetComponentData<ShellTarget>(target).CoreHealth, Is.EqualTo(66));
        }

        [Test] public void Shell001_CoreDeathCompletesWithSurvivorsAndWithoutPlayerAtTarget()
        {
            Expose(5);
            var completion = world.GetOrCreateSystemManaged<GauntletCompletionSystem>();
            uint before = GauntletCompletionRegistry.Sequence;
            completion.Update(); Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before));
            ShellHitResolution.Punch(em, target, 10, 0, default);
            completion.Update(); completion.Update();
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before + 1));
            Assert.That(em.Exists(source), Is.True);
        }

        [Test] public void Shell005_ReplacementsWaitForZeroAndDelayThenReleaseBothSlotsOnce()
        {
            Entity a = Exploder(), b = Exploder();
            var system = world.GetOrCreateSystem<ShellExploderReplenishmentSystem>();
            Pool(a); Tick(system, 10);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(a).Enabled, Is.Zero);
            Assert.That(em.GetComponentData<ShellTarget>(target).ReplaceExplodersAt, Is.EqualTo(-1));
            Pool(b); Tick(system, 20); Tick(system, 21.9);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(a).Enabled, Is.Zero);
            Tick(system, 22);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(a).Enabled, Is.EqualTo(1));
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(b).Enabled, Is.EqualTo(1));
            Assert.That(em.GetComponentData<RespawnRequest>(a).RespawnAt, Is.EqualTo(22));
            em.SetComponentEnabled<RespawnRequest>(a, false); Tick(system, 22.1);
            Pool(a); Tick(system, 23); // b still waiting for safe placement: a must not be issued twice.
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(a).Enabled, Is.Zero);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(b).Enabled, Is.EqualTo(1));
            Expose(5); Tick(system, 24);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(b).Enabled, Is.Zero);
            Assert.That(em.GetComponentData<ShellTarget>(target).ReplacementBatchActive, Is.Zero);
        }

        [Test] public void Shell005_BaselinesContinueAfterShellBreakAndStopOnCoreDeath()
        {
            var baseline = em.CreateEntity(typeof(BarricadeCrowdMember), typeof(EnemyRespawnSettings));
            em.SetComponentData(baseline, new BarricadeCrowdMember { Barricade = target });
            Expose(5);
            var system = world.GetOrCreateSystem<BarricadeCrowdReplenishmentSystem>(); system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(baseline).Enabled, Is.EqualTo(1));
            ShellHitResolution.Explosion(em, target, 10, 0, default); system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(baseline).Enabled, Is.Zero);
        }

        private void Expose(float health)
        {
            var s = em.GetComponentData<ShellTarget>(target); s.ExplosionsRemaining = 0;
            s.CoreHealth = s.CoreMaxHealth = health; em.SetComponentData(target, s);
        }
        private Entity Exploder()
        {
            var e = em.CreateEntity(typeof(BarricadeCrowdMember), typeof(EnemyRespawnSettings), typeof(RespawnRequest),
                typeof(EnemyLaunchState), typeof(EnemyWaveOwnership), typeof(ExplosiveEnemyState));
            em.SetComponentData(e, new BarricadeCrowdMember { Barricade = target });
            em.SetComponentData(e, new EnemyWaveOwnership { Sequence = sequence });
            em.SetComponentEnabled<RespawnRequest>(e, false);
            return e;
        }
        private void Pool(Entity e)
        {
            em.SetComponentEnabled<RespawnRequest>(e, true);
            em.SetComponentData(e, new RespawnRequest { IsPooled = 1, RespawnAt = double.MaxValue });
        }
        private void Tick(SystemHandle system, double time)
        {
            world.SetTime(new TimeData(time, .02f)); system.Update(world.Unmanaged);
        }
    }
}
