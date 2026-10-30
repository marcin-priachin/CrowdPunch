using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Mono.Player;
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
            em.AddComponentData(boss,new ChickenCastTuning { Duration=1 });
            em.AddComponent<ChickenTurnState>(boss);
            em.AddComponentData(boss,ChickenAnimationTuning.Default);
            em.AddComponent<ChickenTurnPlayback>(boss);
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
            Assert.AreEqual(ChickenPauseResponse.InterruptAndCast,t.PauseResponse); Assert.AreEqual(ChickenShotAim.PlayerPosition,t.ShotAim);
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
        [TestCase(-8f,2f)] [TestCase(5f,-8f)] [TestCase(0f,10f)]
        public void Chicken001_RushFacesCurrentPlayerWithoutChangingCommittedTravel(float x,float z)
        {
            var pose=em.GetComponentData<LocalTransform>(boss);
            var b=new ChickenBoss { Stage=1,Facing=quaternion.identity };
            var initialPlayer=em.GetComponentData<PlayerSnapshot>(player).Position;
            ChickenAttackSystem.CommitRush(ref b,pose.Position,initialPlayer,t);
            Assert.Greater(math.dot(math.forward(b.Facing),math.normalizesafe(new float3(initialPlayer.x-pose.Position.x,0,initialPlayer.z-pose.Position.z))),.9999f);
            var destination=b.Destination; var facing=math.forward(b.Facing); var sequence=b.RushSequence;
            em.SetComponentData(boss,b);
            var velocity=new float3(2,0,3); em.SetComponentData(boss,new PhysicsVelocity { Linear=velocity });
            var currentPlayer=new float3(x,5,z);
            em.SetComponentData(player,new PlayerSnapshot { IsAvailable=true,Position=currentPlayer });
            world.GetOrCreateSystem<ChickenAttackSystem>().Update(world.Unmanaged);
            b=em.GetComponentData<ChickenBoss>(boss);
            var expected=math.normalizesafe(new float3(currentPlayer.x-pose.Position.x,0,currentPlayer.z-pose.Position.z),facing);
            Assert.Greater(math.dot(math.forward(b.Facing),expected),.9999f);
            Assert.AreEqual(destination,b.Destination); Assert.AreEqual(sequence,b.RushSequence);
            Assert.AreEqual(ChickenPhase.Rush,b.Phase); Assert.AreEqual(pose,em.GetComponentData<LocalTransform>(boss));
            Assert.AreEqual(velocity,em.GetComponentData<PhysicsVelocity>(boss).Linear);
        }

        [TestCase(-12f,ChickenPhase.Pause,-1)] [TestCase(12f,ChickenPhase.Pause,1)]
        [TestCase(-12f,ChickenPhase.Cast,0)] [TestCase(12f,ChickenPhase.Cast,0)]
        public void Chicken001_StationaryTrackingFacesPlayerAndPublishesIdleTurnDirection(float x,ChickenPhase phase,int sign)
        {
            var pose=em.GetComponentData<LocalTransform>(boss);
            em.AddComponentData(boss,new PhysicsMass { Transform=RigidTransform.identity });
            em.SetComponentData(boss,new ChickenBoss { Phase=phase,Stage=1,Remaining=1,Facing=quaternion.identity });
            em.SetComponentData(player,new PlayerSnapshot { IsAvailable=true,Position=pose.Position+new float3(x,5,0) });
            world.GetOrCreateSystem<ChickenAttackSystem>().Update(world.Unmanaged);
            var b=em.GetComponentData<ChickenBoss>(boss);
            Assert.Greater(math.dot(math.forward(b.Facing),new float3(math.sign(x),0,0)),.9999f);
            Assert.AreEqual(phase,b.Phase);
            world.GetOrCreateSystem<ChickenMotionSystem>().Update(world.Unmanaged);
            Assert.AreEqual(sign,math.sign(em.GetComponentData<ChickenTurnState>(boss).YawRate));
            var velocity=em.GetComponentData<PhysicsVelocity>(boss);
            Assert.AreEqual(float3.zero,velocity.Linear);
            Assert.AreEqual(math.sign(x),math.sign(velocity.Angular.y));
            Assert.AreEqual(pose,em.GetComponentData<LocalTransform>(boss));
        }

        [TestCase(0)] [TestCase(15)] [TestCase(30)]
        public void Chicken007_ReleasesExactlyOnceAtConfiguredSweepFrame(int frame)
        {
            var settings = ScriptableObject.CreateInstance<ChickenBossSettings>();
            settings.windUp = 1; settings.projectileLaunchFrame = frame; settings.firingAnimationLastFrame = 30;
            var shotTuning = settings.Bake(); Object.DestroyImmediate(settings);
            t.WindUp = shotTuning.WindUp; t.ShotReleasePhase = shotTuning.ShotReleasePhase;
            em.SetComponentData(boss, t);
            em.SetComponentData(boss, new ChickenBoss { Stage=1, Phase=ChickenPhase.WindUp,
                Remaining=t.ShotReleaseDelay, ShotsRemaining=1, Facing=quaternion.identity });
            var ai=world.GetOrCreateSystem<ChickenAttackSystem>();
            if(frame>0)
            {
                world.SetTime(new TimeData(10+t.ShotReleaseDelay-.01f,t.ShotReleaseDelay-.01f)); ai.Update(world.Unmanaged);
                Assert.AreEqual(ChickenPhase.WindUp,em.GetComponentData<ChickenBoss>(boss).Phase);
                Assert.AreEqual(0,em.GetComponentData<ChickenBoss>(boss).ShotSequence);
            }
            world.SetTime(new TimeData(11,.02f)); ai.Update(world.Unmanaged);
            var b=em.GetComponentData<ChickenBoss>(boss);
            Assert.AreEqual(ChickenPhase.Rush,b.Phase); Assert.AreEqual(1,b.ShotSequence);
            ai.Update(world.Unmanaged);
            Assert.AreEqual(1,em.GetComponentData<ChickenBoss>(boss).ShotSequence);
            using var shots=em.CreateEntityQuery(typeof(ChickenProjectile));
            Assert.AreEqual(1,shots.CalculateEntityCount());
        }

        [TestCase(ChickenPauseResponse.InterruptAndCast,ChickenPhase.Cast)]
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
        [Test]
        public void Chicken002_CastsStationaryThenTeleportsAtEndUsingCurrentPlayerPosition()
        {
            var start=em.GetComponentData<LocalTransform>(boss);
            start.Scale=1.25f; em.SetComponentData(boss,start);
            em.AddComponentData(boss,new PhysicsMass());
            em.SetComponentData(player,new PlayerSnapshot { IsAvailable=true,Position=start.Position+new float3(1,0,0) });
            em.SetComponentData(boss,new ChickenBoss { Phase=ChickenPhase.Pause,Stage=1,Remaining=1,Facing=quaternion.identity });
            var ai=world.GetOrCreateSystem<ChickenAttackSystem>(); ai.Update(world.Unmanaged);
            Assert.AreEqual(ChickenPhase.Cast,em.GetComponentData<ChickenBoss>(boss).Phase);
            Assert.AreEqual(1,em.GetComponentData<ChickenBoss>(boss).Remaining,.0001f);
            em.SetComponentData(boss,new ChickenBoss { Phase=ChickenPhase.Cast,Stage=1,Remaining=1,Facing=quaternion.identity });
            world.SetTime(new TimeData(10.5,.5f)); ai.Update(world.Unmanaged);
            world.GetOrCreateSystem<ChickenMotionSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<ChickenTeleportSystem>().Update(world.Unmanaged);
            Assert.AreEqual(start.Position,em.GetComponentData<LocalTransform>(boss).Position);
            Assert.AreEqual(float3.zero,em.GetComponentData<PhysicsVelocity>(boss).Linear);
            Assert.AreEqual(ChickenPhase.Cast,em.GetComponentData<ChickenBoss>(boss).Phase);
            var latestPlayer=new float3(-12,0,-11);
            em.SetComponentData(player,new PlayerSnapshot { IsAvailable=true,Position=latestPlayer });
            world.SetTime(new TimeData(11,.5f)); ai.Update(world.Unmanaged);
            Assert.AreEqual(ChickenPhase.Teleport,em.GetComponentData<ChickenBoss>(boss).Phase);
            world.GetOrCreateSystem<ChickenTeleportSystem>().Update(world.Unmanaged);
            var after=em.GetComponentData<LocalTransform>(boss); var b=em.GetComponentData<ChickenBoss>(boss);
            Assert.AreEqual(ChickenTeleportSystem.FarthestPosition(latestPlayer,start.Position.y,t),after.Position);
            Assert.AreEqual(start.Scale,after.Scale); Assert.AreEqual(start.Position.y,after.Position.y);
            Assert.AreEqual(after.Position,b.PreviousPosition); Assert.AreEqual(after.Position,b.Destination);
            Assert.AreEqual(ChickenPhase.Pause,b.Phase); Assert.AreEqual(0,b.ShotSequence);
            using var shots=em.CreateEntityQuery(typeof(ChickenProjectile)); Assert.AreEqual(0,shots.CalculateEntityCount());
            ChickenEncounterReset.Reset(em); Assert.AreEqual(ChickenPhase.Pause,em.GetComponentData<ChickenBoss>(boss).Phase);
        }

        [TestCase(12f,8f)] [TestCase(-12f,8f)] [TestCase(12f,-8f)] [TestCase(-12f,-8f)] [TestCase(0f,0f)]
        public void Chicken002_TeleportMaximizesDistanceWithinColliderInset(float x,float z)
        {
            t.ArenaCenter=new float2(3,-2); t.ArenaHalfSize=new float2(18,12);
            var p=new float3(x,0,z); var target=ChickenTeleportSystem.FarthestPosition(p,2,t);
            var inset=t.ArenaHalfSize-t.BodyRadius-.15f;
            Assert.IsTrue(math.all(math.abs(target.xz-t.ArenaCenter)<=inset+.0001f));
            Assert.AreEqual(2,target.y);
            for(int i=0;i<=10;i++) for(int j=0;j<=10;j++)
            {
                var candidate=t.ArenaCenter+inset*new float2(i/5f-1,j/5f-1);
                Assert.LessOrEqual(math.distancesq(candidate,p.xz),math.distancesq(target.xz,p.xz)+.001f);
            }
        }

        [TestCase(ChickenPhase.Cast)] [TestCase(ChickenPhase.Teleport)]
        public void Chicken002_AcceptedDamageCancelsCastAndTeleport(ChickenPhase phase)
        {
            var start=em.GetComponentData<LocalTransform>(boss).Position;
            em.SetComponentData(boss,new ChickenBoss { Phase=phase,Stage=1,Remaining=0 });
            ChickenDamageResolution.Queue(em,boss,source,1,20); Damage();
            world.GetOrCreateSystem<ChickenTeleportSystem>().Update(world.Unmanaged);
            Assert.AreEqual(ChickenPhase.Stagger,em.GetComponentData<ChickenBoss>(boss).Phase);
            Assert.AreEqual(start,em.GetComponentData<LocalTransform>(boss).Position);
        }

        [Test]
        public void Chicken002_CastContactDamagesPushesAndRespectsPlayerProtection()
        {
            var go=new GameObject("Chicken cast player"); var bridge=go.AddComponent<PlayerEcsBridge>();
            var health=go.AddComponent<PlayerHealth>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            foreach(var method in new[] { "OnDisable", "Awake", "OnEnable" })
                typeof(PlayerHealth).GetMethod(method,flags).Invoke(health,null);
            PlayerBridgeRegistry.Register(bridge);
            int accepted=0; Vector3 push=default; health.DamageAccepted+=impulse=>{accepted++;push=impulse;};
            try
            {
                var position=em.GetComponentData<LocalTransform>(boss).Position;
                em.SetComponentData(player,new PlayerSnapshot { IsAvailable=true,Position=position+new float3(1,0,0),Radius=.5f });
                em.SetComponentData(boss,new ChickenBoss { Phase=ChickenPhase.Cast,Remaining=.5f,PreviousPosition=position,Facing=quaternion.identity });
                var contact=world.GetOrCreateSystem<ChickenPlayerImpactSystem>(); contact.Update(world.Unmanaged);
                Assert.AreEqual(100-t.PlayerDamage,health.CurrentHealth,.0001f);
                Assert.AreEqual(1,accepted); Assert.AreEqual(t.PlayerPush,push.x,.0001f); Assert.AreEqual(0,push.y);
                contact.Update(world.Unmanaged); Assert.AreEqual(1,accepted);
                health.ResetHealth();
                em.SetComponentData(boss,new ChickenBoss { Phase=ChickenPhase.Pause,PreviousPosition=position });
                contact.Update(world.Unmanaged); Assert.AreEqual(100,health.CurrentHealth);
                em.SetComponentData(boss,new ChickenBoss { Phase=ChickenPhase.Cast,Remaining=.5f,PreviousPosition=position });
                em.SetComponentData(player,new PlayerSnapshot { IsAvailable=true,Position=position+new float3(10,0,0),Radius=.5f });
                contact.Update(world.Unmanaged); Assert.AreEqual(100,health.CurrentHealth);
            }
            finally { PlayerBridgeRegistry.Unregister(bridge); Object.DestroyImmediate(go); }
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
            var visual=em.CreateEntity(typeof(EnemyAnimation),typeof(EnemyAnimationPlayback));
            em.SetComponentData(visual,new EnemyAnimation { Owner=boss });
            em.SetComponentData(visual,new EnemyAnimationPlayback { Initialized=7,Phase=.5f });
            em.SetComponentData(boss,new ChickenTurnState { YawRate=1 });
            em.SetComponentData(boss,new ChickenTurnPlayback { Motion=6,Weight=1,AccumulatedYaw=.2f });
            ChickenEncounterReset.Reset(em); b=em.GetComponentData<ChickenBoss>(boss);
            Assert.AreEqual(0,em.GetComponentData<ChickenTurnState>(boss).YawRate);
            Assert.AreEqual(0,em.GetComponentData<ChickenTurnPlayback>(boss).Motion);
            Assert.AreEqual(0,em.GetComponentData<ChickenTurnPlayback>(boss).Weight);
            Assert.AreEqual(0,em.GetComponentData<EnemyAnimationPlayback>(visual).Initialized);
            Assert.AreEqual(ChickenPhase.Pause,b.Phase); Assert.AreEqual(1,b.Stage); Assert.AreEqual(0,b.AcceptedHits);
            Assert.AreEqual(t.Health,em.GetComponentData<Health>(boss).Current); Assert.AreEqual(0,em.GetBuffer<ChickenHitHistory>(boss).Length);
        }
    }
}
