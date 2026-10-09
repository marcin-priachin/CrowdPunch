using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class GroundHazardTests
    {
        private World world;
        private EntityManager em;
        private Entity victim, patch, player;
        [SetUp] public void Setup()
        {
            world = new World("GROUND contracts"); em = world.EntityManager;
            victim = em.CreateEntity(typeof(Enemy), typeof(Health), typeof(EnemyLifetime), typeof(EnemyLaunchState),
                typeof(LocalTransform), typeof(NavigationAgent), typeof(GroundHazardDamageClock), typeof(EnemyDamageState),
                typeof(DamageRequest), typeof(DeathRequest), typeof(RespawnRequest), typeof(EnemyHealthBarVisibility), typeof(PhysicsVelocity));
            em.SetComponentData(victim, new Health { Current = 100, Max = 100 });
            em.SetComponentData(victim, new EnemyLifetime { Generation = 1 });
            em.SetComponentData(victim, LocalTransform.Identity); em.SetComponentData(victim,new NavigationAgent { Radius = .5f });
            em.SetComponentEnabled<DamageRequest>(victim,false); em.SetComponentEnabled<DeathRequest>(victim,false); em.SetComponentEnabled<RespawnRequest>(victim,false);
            patch = em.CreateEntity(typeof(GroundHazard),typeof(GroundHazardState));
            em.SetComponentData(patch,new GroundHazard { Position = new float3(0,-100,0), HalfSize = new float2(2), Radius = 2,
                Damage = 12, DamageInterval = .75f, InactiveDuration = 3, WarningDuration = 1.5f, ActiveDuration = 2.5f });
            State(GroundHazardPhase.Active);
            player = em.CreateEntity(typeof(PlayerSnapshot));
        }
        [TearDown] public void TearDown() => world.Dispose();
        private void State(GroundHazardPhase phase) => em.SetComponentData(patch,new GroundHazardState { Initialized = 1, Introduced = 1, Phase = phase });
        private void Step(double time)
        { world.SetTime(new TimeData(time,.02f)); world.GetOrCreateSystem<GroundHazardDamageSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
        private float Hp => em.GetComponentData<Health>(victim).Current;

        [TestCase(EnemyLaunchPhase.Active)] [TestCase(EnemyLaunchPhase.Launched)] [TestCase(EnemyLaunchPhase.Recovering)]
        public void Ground002_EligiblePhasesIgnoreHeightAndPreserveVelocity(EnemyLaunchPhase phase)
        {
            var velocity = new PhysicsVelocity { Linear = new float3(3,4,5), Angular = new float3(1,2,3) };
            em.SetComponentData(victim,velocity); em.SetComponentData(victim,new EnemyLaunchState { Phase = phase }); Step(0);
            Assert.That(Hp,Is.EqualTo(88)); Assert.That(em.GetComponentData<PhysicsVelocity>(victim).Linear,Is.EqualTo(velocity.Linear));
            Assert.That(em.GetComponentData<EnemyLaunchState>(victim).Phase,Is.EqualTo(phase));
        }
        [Test] public void Ground003_OverlapReentryPatchSwitchAndActivationShareOneClock()
        {
            var second = em.CreateEntity(typeof(GroundHazard),typeof(GroundHazardState));
            em.SetComponentData(second,em.GetComponentData<GroundHazard>(patch)); em.SetComponentData(second,em.GetComponentData<GroundHazardState>(patch));
            Step(0); Assert.That(Hp,Is.EqualTo(88));
            em.SetComponentData(victim,LocalTransform.FromPosition(new float3(8,0,0))); Step(.2);
            em.SetComponentData(victim,LocalTransform.Identity); State(GroundHazardPhase.Inactive); Step(.4); Assert.That(Hp,Is.EqualTo(88));
            em.DestroyEntity(second); State(GroundHazardPhase.Warning); Step(.6); Assert.That(Hp,Is.EqualTo(88));
            State(GroundHazardPhase.Active); Step(.7); Assert.That(Hp,Is.EqualTo(88)); Step(.75); Assert.That(Hp,Is.EqualTo(76));
        }
        [TestCase(GroundHazardPhase.Inactive)] [TestCase(GroundHazardPhase.Warning)]
        public void Ground001_SafePhasesDoNotConsumeFirstContact(GroundHazardPhase phase)
        { State(phase); Step(0); Assert.That(Hp,Is.EqualTo(100)); State(GroundHazardPhase.Active); Step(.01); Assert.That(Hp,Is.EqualTo(88)); }
        [Test] public void Ground004_ArmorCannotLoseHealthOrStages()
        {
            em.AddComponentData(victim,EnemyArmor.Fresh); Step(0); Assert.That(Hp,Is.EqualTo(100));
            Assert.That(em.GetComponentData<EnemyArmor>(victim).Stages,Is.EqualTo(EnemyArmor.Fresh.Stages));
            em.SetComponentData(victim,new EnemyArmor { ProtectedUntil = .2 }); Step(.1); Assert.That(Hp,Is.EqualTo(100));
            Step(.21); Assert.That(Hp,Is.EqualTo(88));
        }
        [TestCase(EnemyLaunchPhase.Launched,EnemyLaunchOwner.Player,EnemyLaunchOwner.Player)]
        [TestCase(EnemyLaunchPhase.Recovering,EnemyLaunchOwner.Player,EnemyLaunchOwner.Environment)]
        [TestCase(EnemyLaunchPhase.Launched,EnemyLaunchOwner.Boss,EnemyLaunchOwner.Environment)]
        [TestCase(EnemyLaunchPhase.Active,EnemyLaunchOwner.None,EnemyLaunchOwner.Environment)]
        public void Ground004_LethalCreditUsesCurrentLaunchAndDoesNotDetonate(EnemyLaunchPhase phase, EnemyLaunchOwner owner, EnemyLaunchOwner credit)
        {
            em.SetComponentData(victim,new Health { Current = 12,Max = 100 });
            em.SetComponentData(victim,new EnemyLaunchState { Phase = phase, Owner = owner, FeedbackChainDepth = 4 });
            em.AddComponent<ExplosiveDetonationRequest>(victim); em.SetComponentEnabled<ExplosiveDetonationRequest>(victim,false); Step(0);
            var damage = em.GetComponentData<EnemyDamageState>(victim); Assert.That(damage.DefeatOwner,Is.EqualTo(credit));
            Assert.That(damage.DefeatChainDepth,Is.EqualTo(credit == EnemyLaunchOwner.Player ? 4 : 0));
            Assert.That(damage.IsDefeatDeferred,Is.EqualTo(phase == EnemyLaunchPhase.Launched ? 1 : 0));
            Assert.That(em.IsComponentEnabled<ExplosiveDetonationRequest>(victim),Is.False);
        }
        [Test] public void Ground004_PendingLethalDamageCannotAcquireHazardCredit()
        {
            em.SetComponentData(victim,new Health { Current = 5,Max = 100 });
            em.SetComponentData(victim,new DamageRequest { Amount = 10 }); em.SetComponentEnabled<DamageRequest>(victim,true); Step(0);
            Assert.That(em.GetComponentData<EnemyDamageState>(victim).DefeatOwner,Is.EqualTo(EnemyLaunchOwner.None));
        }
        [Test] public void Ground003_PooledReuseGetsFreshVictimClock()
        { Step(0); em.SetComponentData(victim,new EnemyLifetime { Generation = 2 }); Step(.01); Assert.That(Hp,Is.EqualTo(76)); }
        [Test] public void Ground003_PlayerUsesSharedClockAcrossTicks()
        {
            em.SetComponentData(player,new PlayerSnapshot { IsAvailable = true,Radius = .5f });
            Step(0); using var query = em.CreateEntityQuery(typeof(GroundHazardPlayerHit));
            Assert.That(query.GetSingletonBuffer<GroundHazardPlayerHit>().Length,Is.EqualTo(1));
            Step(.1); Assert.That(query.GetSingletonBuffer<GroundHazardPlayerHit>().Length,Is.Zero);
            Step(.75); Assert.That(query.GetSingletonBuffer<GroundHazardPlayerHit>().Length,Is.EqualTo(1));
        }
        [Test] public void Ground001_CyclesResetToOffsetAtEveryWaveAndRetry()
        {
            var sequence = em.CreateEntity(typeof(EnemyWaveSequence)); var h = em.GetComponentData<GroundHazard>(patch);
            h.Operation = GroundHazardOperation.Periodic; h.CycleOffset = 3.25f; h.Sequence = sequence; em.SetComponentData(patch,h);
            em.SetComponentData(patch,default(GroundHazardState));
            var system = world.GetOrCreateSystem<GroundHazardCycleSystem>();
            void Tick(double t) { world.SetTime(new TimeData(t,.02f)); system.Update(world.Unmanaged); }
            Tick(0); Assert.That(em.GetComponentData<GroundHazardState>(patch).Phase,Is.EqualTo(GroundHazardPhase.Warning));
            Tick(2); Assert.That(em.GetComponentData<GroundHazardState>(patch).Phase,Is.EqualTo(GroundHazardPhase.Active));
            em.SetComponentData(sequence,new EnemyWaveSequence { CurrentWaveIndex = 1 }); Tick(2.1);
            Assert.That(em.GetComponentData<GroundHazardState>(patch).Phase,Is.EqualTo(GroundHazardPhase.Warning));
            em.SetComponentData(sequence,new EnemyWaveSequence { RunGeneration = 1 }); Tick(6);
            Assert.That(em.GetComponentData<GroundHazardState>(patch).Phase,Is.EqualTo(GroundHazardPhase.Warning));
        }
        [Test] public void Ground006_SpawnRejectsWarningAndActiveFootprintsIncludingNewWaveOffset()
        {
            State(GroundHazardPhase.Warning); Assert.False(GroundHazardSpawnClearance.Allowed(em,float2.zero,.5f));
            State(GroundHazardPhase.Inactive); Assert.True(GroundHazardSpawnClearance.Allowed(em,float2.zero,.5f));
            var sequence = em.CreateEntity(typeof(EnemyWaveSequence)); var h = em.GetComponentData<GroundHazard>(patch);
            h.Sequence = sequence; h.Operation = GroundHazardOperation.Periodic; h.FirstWave = 1; h.CycleOffset = 4; em.SetComponentData(patch,h);
            Assert.True(GroundHazardSpawnClearance.Allowed(em,float2.zero,.5f));
            em.SetComponentData(sequence,new EnemyWaveSequence { CurrentWaveIndex = 1 });
            Assert.False(GroundHazardSpawnClearance.Allowed(em,float2.zero,.5f),"Advancing the wave cannot spawn before the cycle system updates.");
        }
        [TestCase(GroundHazardShape.Rectangle)] [TestCase(GroundHazardShape.Circle)]
        public void Ground001_OverlapIncludesVictimRadiusAndHasNoHeightGate(GroundHazardShape shape)
        {
            var h = em.GetComponentData<GroundHazard>(patch); h.Shape = shape;
            Assert.True(GroundHazardGeometry.Overlaps(h,new float2(2.4f,0),.5f));
            Assert.False(GroundHazardGeometry.Overlaps(h,new float2(2.6f,0),.5f));
            Assert.True(GroundHazardGeometry.Intersects(h,new float2(-10,0),new float2(10,0),.5f));
        }
        [Test] public void Ground001_RotatedRectangleUsesMatchingWorldFootprint()
        {
            var h = em.GetComponentData<GroundHazard>(patch); h.HalfSize = new float2(1,4); h.Angle = math.PI * .5f;
            Assert.True(GroundHazardGeometry.Overlaps(h,new float2(3,0),0)); Assert.False(GroundHazardGeometry.Overlaps(h,new float2(0,3),0));
        }
        [Test] public void Ground007_UnexpiredPatchesDoNotDelayFiniteWaveCompletion()
        {
            using var physics = new PhysicsWorld(0,0,0); em.AddComponentData(em.CreateEntity(),new PhysicsWorldSingleton { PhysicsWorld = physics });
            var sequence = em.CreateEntity(typeof(EnemyWaveSequence),typeof(EnemyWaveEncounterComplete));
            em.SetComponentData(sequence,new EnemyWaveSequence { Initialized = 1,Phase = EnemyWaveRuntimePhase.AwaitingActivation,DefeatedCount = 1 });
            em.SetComponentEnabled<EnemyWaveEncounterComplete>(sequence,false);
            em.AddBuffer<EnemyWaveDefinition>(sequence).Add(new EnemyWaveDefinition { ActivationMode = 2,TotalEnemyCount = 1,WaitForPersistentHazards = 1 });
            em.AddBuffer<EnemyWaveProfile>(sequence); em.AddBuffer<EnemyWaveEliteProfile>(sequence); em.AddBuffer<EnemyWaveSpawnRange>(sequence);
            world.GetOrCreateSystem<EnemyWaveSpawnSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyWaveSequence>(sequence).Phase,Is.EqualTo(EnemyWaveRuntimePhase.Complete));
        }
    }
}
