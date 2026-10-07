using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
namespace CrowdPunch.Tests
{
    public sealed class ChickenBossTests
    {
        private World world;
        private EntityManager em;
        private Entity boss,source,player,prefab;
        private ChickenTuning t;
        [SetUp] public void Setup()
        {
            world=new World("Chicken regressions"); em=world.EntityManager;
            var asset=ScriptableObject.CreateInstance<ChickenBossSettings>(); t=asset.Bake(); Object.DestroyImmediate(asset);
            t.ArenaHalfSize=new float2(18); t.InitialRotation=quaternion.identity;
            prefab=em.CreateEntity(typeof(ChickenProjectile),typeof(LocalTransform),typeof(PunchAimAssistTarget),typeof(Prefab));
            em.AddBuffer<ChickenProjectileHit>(prefab); t.ProjectilePrefab=prefab;
            boss=em.CreateEntity(typeof(ChickenBoss),typeof(ChickenTuning),typeof(Health),typeof(LocalTransform),typeof(PhysicsVelocity));
            em.SetComponentData(boss,t); em.SetComponentData(boss,LocalTransform.FromPosition(new float3(0,.7f,10)));
            em.SetComponentData(boss,new ChickenBoss { Stage=1,Phase=ChickenPhase.Pause,Facing=quaternion.identity });
            em.SetComponentData(boss,new Health { Current=t.Health,Max=t.Health }); em.AddBuffer<ChickenHit>(boss); em.AddBuffer<ChickenHitHistory>(boss);
            player=em.CreateEntity(typeof(PlayerSnapshot)); em.SetComponentData(player,new PlayerSnapshot { Position=new float3(0,.5f,-10),IsAvailable=true,Radius=.5f });
            source=em.CreateEntity(typeof(EnemyLaunchState),typeof(EnemyLifetime),typeof(RespawnRequest)); em.SetComponentEnabled<RespawnRequest>(source,false);
            world.SetTime(new TimeData(10,.02f));
        }
        [TearDown] public void Cleanup() { world.Dispose(); }
        private void Damage() => world.GetOrCreateSystem<ChickenDamageSystem>().Update(world.Unmanaged);
        private Entity Shot(float3 position,float3 velocity,byte redirected=0)
        {
            var s=em.Instantiate(prefab); em.SetComponentData(s,LocalTransform.FromPosition(position));
            em.SetComponentData(s,new ChickenProjectile { Boss=boss,Velocity=velocity,Launch=1,Redirected=redirected }); return s;
        }
        private void Projectiles() => world.GetOrCreateSystem<ChickenProjectileSystem>().Update(world.Unmanaged);
        [TestCase(EnemyLaunchOwner.Player)] [TestCase(EnemyLaunchOwner.Boss)]
        public void Chicken004_BossContactDeflectsPlayerBoundBodyAndClearsBossHoming(EnemyLaunchOwner owner)
        {
            var position=new float3(0,1,8);
            var before=new float3(2,3,-15);
            em.AddComponentData(source,LocalTransform.FromPosition(position));
            em.AddComponentData(source,new PhysicsVelocity { Linear=before });
            em.SetComponentData(source,new EnemyLaunchState { Phase=EnemyLaunchPhase.Launched,Owner=owner,
                HomingTarget=boss,LaunchSequence=7,LaunchDamage=12 });
            var playerPosition=em.GetComponentData<PlayerSnapshot>(player).Position;
            BossHeadBounceSystem.ResolveContact(em,boss,source,playerPosition);
            var after=em.GetComponentData<PhysicsVelocity>(source).Linear;
            var launch=em.GetComponentData<EnemyLaunchState>(source);
            Assert.LessOrEqual(math.dot(after.xz,math.normalizesafe((playerPosition-position).xz)),.0001f);
            Assert.AreEqual(math.length(before.xz),math.length(after.xz),.0001f);
            Assert.AreEqual(before.y,after.y); Assert.AreEqual(Entity.Null,launch.HomingTarget);
            Assert.AreEqual(owner,launch.Owner); Assert.AreEqual(7,launch.LaunchSequence); Assert.AreEqual(12,launch.LaunchDamage);
            Assert.IsTrue(LaunchedEnemyPlayerImpactSystem.CanDamagePlayer(launch));
        }
        [Test] public void Chicken001_DefaultsAndAllStagePatterns()
        {
            Assert.AreEqual(ChickenPauseResponse.InterruptAndFlee,t.PauseResponse); Assert.AreEqual(ChickenShotAim.PlayerPosition,t.ShotAim);
            Assert.AreEqual(ChickenPattern.Single,ChickenAttackSystem.Pattern(1,t));
            Assert.AreEqual(ChickenPattern.Paired,ChickenAttackSystem.Pattern(2,t)); Assert.AreEqual(ChickenPattern.Paired,ChickenAttackSystem.Pattern(3,t));
            Assert.Less(ChickenAttackSystem.PauseDuration(3,t),ChickenAttackSystem.PauseDuration(2,t));
        }
        [TestCase(ChickenPattern.Single,1)] [TestCase(ChickenPattern.Paired,2)]
        public void Chicken001_ReleaseAlwaysCommitsRushAndSpacesSecondShot(ChickenPattern pattern,int count)
        {
            t.StageOnePattern=pattern; em.SetComponentData(boss,t);
            var ai=world.GetOrCreateSystem<ChickenAttackSystem>(); ai.Update(world.Unmanaged);
            var b=em.GetComponentData<ChickenBoss>(boss); Assert.AreEqual(ChickenPhase.WindUp,b.Phase);
            world.SetTime(new TimeData(11,1)); ai.Update(world.Unmanaged);
            b=em.GetComponentData<ChickenBoss>(boss); Assert.AreEqual(ChickenPhase.Rush,b.Phase); Assert.AreEqual(count-1,b.ShotsRemaining);
            var start=em.GetComponentData<LocalTransform>(boss).Position;
            Assert.Greater(math.distance(start,b.Destination),1);
            em.SetComponentData(boss,LocalTransform.FromPosition(b.Destination));
            world.SetTime(new TimeData(11.02,.02f)); ai.Update(world.Unmanaged);
            b=em.GetComponentData<ChickenBoss>(boss);
            Assert.AreEqual(count==2?ChickenPhase.WindUp:ChickenPhase.Pause,b.Phase);
            if(count==2) Assert.GreaterOrEqual(b.Remaining,t.ShotSpacing-.02f-.001f);
        }
        [TestCase(ChickenPauseResponse.InterruptAndFlee,ChickenPhase.Rush)]
        [TestCase(ChickenPauseResponse.FinishPause,ChickenPhase.Pause)]
        public void Chicken002_ProximityOnlyInterruptsPause(ChickenPauseResponse response,ChickenPhase expected)
        {
            t.PauseResponse=response; em.SetComponentData(boss,t);
            em.SetComponentData(player,new PlayerSnapshot { IsAvailable=true,Position=new float3(0,.5f,8) });
            em.SetComponentData(boss,new ChickenBoss { Phase=ChickenPhase.Pause,Remaining=1,Stage=1 });
            var ai=world.GetOrCreateSystem<ChickenAttackSystem>(); ai.Update(world.Unmanaged);
            Assert.AreEqual(expected,em.GetComponentData<ChickenBoss>(boss).Phase);
            em.SetComponentData(boss,new ChickenBoss { Phase=ChickenPhase.WindUp,Remaining=1,Stage=1,ShotsRemaining=1 }); ai.Update(world.Unmanaged);
            Assert.AreEqual(ChickenPhase.WindUp,em.GetComponentData<ChickenBoss>(boss).Phase);
        }
        [Test] public void Chicken003_ReflectionPreservesSpeedClearsHomingAndDoesNotResetLifetime()
        {
            var p=new ChickenProjectile { Velocity=new float3(15,0,4),HomingTarget=boss,Age=4,Bounces=2 };
            float speed=math.length(p.Velocity); ChickenProjectileSystem.Reflect(ref p,new float3(-1,0,0));
            Assert.AreEqual(speed,math.length(p.Velocity),.0001f); Assert.AreEqual(Entity.Null,p.HomingTarget); Assert.AreEqual(3,p.Bounces); Assert.AreEqual(4,p.Age);
        }
        [TestCase(ChickenShotAim.PlayerPosition,false)] [TestCase(ChickenShotAim.MovementLead,true)]
        public void Chicken003_AimAlternativeUsesVelocityOnlyForLeading(ChickenShotAim mode,bool leads)
        {
            t.ShotAim=mode; em.SetComponentData(boss,t);
            em.SetComponentData(player,new PlayerSnapshot { Position=new float3(0,.5f,-10),Velocity=new float3(5,0,0),IsAvailable=true });
            em.SetComponentData(boss,new ChickenBoss { Stage=1,Phase=ChickenPhase.WindUp,Remaining=0,ShotsRemaining=1 });
            world.GetOrCreateSystem<ChickenAttackSystem>().Update(world.Unmanaged);
            using var q=em.CreateEntityQuery(typeof(ChickenProjectile));
            var p=q.GetSingleton<ChickenProjectile>();
            if(leads) Assert.Greater(p.Velocity.x,1); else Assert.AreEqual(0,p.Velocity.x,.0001f);
            Assert.AreEqual(t.FireSpeed,math.length(p.Velocity),.0001f);
        }
        [Test] public void Chicken003_MultipleSweptBouncesConsumeAtLimitInOneStep()
        {
            t.BounceLimit=2; em.SetComponentData(boss,t);
            var s=Shot(new float3(17.3f,1,0),new float3(22,0,0));
            world.SetTime(new TimeData(12,2)); Projectiles(); Assert.IsFalse(em.Exists(s));
        }
        [Test] public void Chicken003_RealPunchConfirmsCooldownAndResetsLaunchLimits()
        {
            var s=Shot(new float3(0,1,-8),new float3(3,0,-2));
            var p=em.GetComponentData<ChickenProjectile>(s); p.Age=8; p.Bounces=4; em.SetComponentData(s,p);
            em.GetBuffer<ChickenProjectileHit>(s).Add(new ChickenProjectileHit { Target=source });
            em.AddComponentData(player,new PunchRequest { Origin=new float3(0,1,-10),Direction=new float3(0,0,1),Range=5,Radius=1,Sequence=1 });
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            p=em.GetComponentData<ChickenProjectile>(s);
            Assert.AreEqual(t.ReturnSpeed,math.length(p.Velocity),.001f); Assert.AreEqual(2,p.Launch); Assert.AreEqual(1,p.Redirected);
            Assert.AreEqual(0,p.Age); Assert.AreEqual(0,p.Bounces); Assert.AreEqual(0,em.GetBuffer<ChickenProjectileHit>(s).Length);
            Assert.IsTrue(em.GetComponentData<PunchRequest>(player).HitEnemy); Assert.IsFalse(em.IsComponentEnabled<PunchRequest>(player));
        }
        [TestCase((byte)0)] [TestCase((byte)1)] public void Chicken004_AllOwnershipHitsPlayerAndConsumesShot(byte redirected)
        {
            var s=Shot(new float3(0,1,-9),new float3(0,0,-100),redirected); Projectiles(); Assert.IsFalse(em.Exists(s));
        }
        [Test] public void Chicken004_ReturnConsumedDuringBossInvulnerability()
        {
            var b=em.GetComponentData<ChickenBoss>(boss); b.InvulnerableUntil=20; em.SetComponentData(boss,b);
            var s=Shot(new float3(0,1,8),new float3(0,0,100),1); Projectiles(); Damage();
            Assert.IsFalse(em.Exists(s)); Assert.AreEqual(t.Health,em.GetComponentData<Health>(boss).Current);
        }
        [Test] public void Chicken005_UntouchedShotCannotDamageBoss()
        {
            var s=Shot(new float3(0,1,8),new float3(0,0,100)); Projectiles(); Damage();
            Assert.IsTrue(em.Exists(s)); Assert.AreEqual(t.Health,em.GetComponentData<Health>(boss).Current);
        }
        [TestCase(EnemyLaunchOwner.Boss,0)] [TestCase(EnemyLaunchOwner.Player,10)]
        public void Chicken005_BodyEligibilityPreservesOwnership(EnemyLaunchOwner owner,float damage)
        {
            em.SetComponentData(source,new EnemyLaunchState { Phase=EnemyLaunchPhase.Launched,Owner=owner,LaunchSequence=1,LaunchDamage=10 });
            var settings=new EnemyLaunchSettings { MinimumDamageImpulse=1,BaseCollisionDamageMultiplier=1,MaximumCollisionDamageMultiplier=1 };
            ChickenDamageResolution.QueueBody(em,boss,source,20,settings); Damage();
            Assert.AreEqual(t.Health-damage,em.GetComponentData<Health>(boss).Current);
        }
        [Test] public void Chicken005_ImpactAndBlastResolveHigherOnceAndProtectedContactIsConsumed()
        {
            em.SetComponentData(source,new EnemyLaunchState { LaunchSequence=1 });
            ChickenDamageResolution.Queue(em,boss,source,1,10); ChickenDamageResolution.Queue(em,boss,source,1,35); Damage();
            Assert.AreEqual(t.Health-35,em.GetComponentData<Health>(boss).Current);
            ChickenDamageResolution.Queue(em,boss,source,1,100); world.SetTime(new TimeData(20,.02f)); Damage();
            Assert.AreEqual(t.Health-35,em.GetComponentData<Health>(boss).Current);
            var b=em.GetComponentData<ChickenBoss>(boss); b.InvulnerableUntil=30; em.SetComponentData(boss,b);
            em.SetComponentData(source,new EnemyLaunchState { LaunchSequence=2 });
            ChickenDamageResolution.Queue(em,boss,source,2,20); Damage();
            world.SetTime(new TimeData(31,.02f)); ChickenDamageResolution.Queue(em,boss,source,2,20); Damage();
            Assert.AreEqual(t.Health-35,em.GetComponentData<Health>(boss).Current);
        }
        [TestCase((byte)0,EnemyLaunchOwner.Boss)] [TestCase((byte)1,EnemyLaunchOwner.Player)]
        public void Chicken004_PassesThroughEnemyOncePerLaunchAndRepunchResetsEligibility(byte redirected,EnemyLaunchOwner owner)
        {
            em.AddComponent<Enemy>(source); em.AddComponentData(source,new EnemyTier { Value=EnemyCombatTier.Normal });
            em.AddComponentData(source,LocalTransform.FromPosition(new float3(0,1,0))); em.AddComponent<PhysicsVelocity>(source);
            em.AddComponentData(source,new Health { Current=100,Max=100 }); em.AddComponent<EnemyDamageState>(source);
            em.AddComponent<DamageRequest>(source); em.SetComponentEnabled<DamageRequest>(source,false);
            em.AddComponent<DeathRequest>(source); em.SetComponentEnabled<DeathRequest>(source,false);
            em.AddComponent<EnemyHealthBarVisibility>(source); em.SetComponentEnabled<EnemyHealthBarVisibility>(source,false);
            em.AddComponentData(source,new EnemyContactDamageSettings { ContactRadius=.5f });
            var s=Shot(new float3(0,1,-1),new float3(0,0,100),redirected); Projectiles();
            Assert.IsTrue(em.Exists(s)); Assert.AreEqual(owner,em.GetComponentData<EnemyLaunchState>(source).Owner);
            Assert.AreEqual(100-t.ProjectileDamage,em.GetComponentData<Health>(source).Current);
            var p=em.GetComponentData<ChickenProjectile>(s); p.Velocity=-p.Velocity; em.SetComponentData(s,p); Projectiles();
            Assert.AreEqual(100-t.ProjectileDamage,em.GetComponentData<Health>(source).Current);
            ChickenProjectilePunch.Redirect(em,s,ref p,em.GetComponentData<LocalTransform>(s).Position,
                new PunchSpecification { Origin=new float3(0,1,-2),Direction=new float3(0,0,1) },t);
            p.Velocity=new float3(0,0,100); em.SetComponentData(s,p); Projectiles();
            Assert.AreEqual(100-2*t.ProjectileDamage,em.GetComponentData<Health>(source).Current);
        }
        [Test] public void Chicken006_HitCancelsRushAndPendingShotThenDefeatCleansShotsAndCompletes()
        {
            var b=new ChickenBoss { Stage=2,Phase=ChickenPhase.Rush,ShotsRemaining=1 }; em.SetComponentData(boss,b);
            ChickenDamageResolution.Queue(em,boss,source,1,20); Damage(); b=em.GetComponentData<ChickenBoss>(boss);
            Assert.AreEqual(ChickenPhase.Stagger,b.Phase); Assert.AreEqual(0,b.ShotsRemaining); Assert.Greater(b.InvulnerableUntil,10);
            var s=Shot(float3.zero,new float3(0,0,1));
            world.SetTime(new TimeData(20,.02f)); ChickenDamageResolution.Queue(em,boss,source,2,10000); Damage();
            world.GetOrCreateSystem<ChickenProjectileCleanupSystem>().Update(world.Unmanaged); Assert.IsFalse(em.Exists(s));
            uint before=GauntletCompletionRegistry.Sequence; world.GetOrCreateSystemManaged<GauntletCompletionSystem>().Update();
            Assert.AreEqual(before+1,GauntletCompletionRegistry.Sequence);
            ChickenEncounterReset.Reset(em); b=em.GetComponentData<ChickenBoss>(boss);
            Assert.AreEqual(ChickenPhase.Pause,b.Phase); Assert.AreEqual(1,b.Stage); Assert.AreEqual(0,b.AcceptedHits);
            Assert.AreEqual(t.Health,em.GetComponentData<Health>(boss).Current); Assert.AreEqual(0,em.GetBuffer<ChickenHitHistory>(boss).Length);
        }
    }
}
