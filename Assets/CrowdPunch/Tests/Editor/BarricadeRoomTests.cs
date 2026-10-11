using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Initialization;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class BarricadeRoomTests
    {
        private World world;
        private EntityManager em;
        [SetUp] public void Setup(){world=new World("BARRICADE-007 rooms");em=world.EntityManager;}
        [TearDown] public void Cleanup()=>world.Dispose();
        private Entity Gate(bool final=false)
        {
            var e=em.CreateEntity(typeof(Barricade),typeof(BarricadeFeedbackState));
            em.SetComponentData(e,new Barricade{RequiredHits=3,HitsRemaining=3,CompleteOnDestruction=(byte)(final?0:1),ExitPosition=new float3(0,0,100),ExitRadius=2});return e;
        }
        private Entity Room(Entity previous,float minimum,float maximum)
        {
            var e=em.CreateEntity(typeof(BarricadeRoom));em.SetComponentData(e,new BarricadeRoom{PreviousGate=previous,Minimum=new float2(-10,minimum),Maximum=new float2(10,maximum)});return e;
        }
        private void Break(Entity gate){var wall=em.GetComponentData<Barricade>(gate);wall.HitsRemaining=0;em.SetComponentData(gate,wall);}

        [TestCase(false)] [TestCase(true)] public void Barricade007_FinalExitCannotSkipEarlierIntactGates(bool finalFirst)
        {
            Entity a,b,last;
            if(finalFirst){last=Gate(true);a=Gate();b=Gate();}else{a=Gate();b=Gate();last=Gate(true);}
            var player=em.CreateEntity(typeof(PlayerSnapshot));em.SetComponentData(player,new PlayerSnapshot{Position=new float3(0,0,100),IsAvailable=true});
            var survivor=em.CreateEntity(typeof(Enemy));
            var system=world.GetOrCreateSystemManaged<GauntletCompletionSystem>();uint before=GauntletCompletionRegistry.Sequence;
            Break(last);Break(b);system.Update();Assert.That(GauntletCompletionRegistry.Sequence,Is.EqualTo(before));
            Break(a);em.SetComponentData(player,new PlayerSnapshot{Position=float3.zero,IsAvailable=true});system.Update();Assert.That(GauntletCompletionRegistry.Sequence,Is.EqualTo(before));
            em.SetComponentData(player,new PlayerSnapshot{Position=new float3(0,0,100),IsAvailable=true});system.Update();system.Update();
            Assert.That(GauntletCompletionRegistry.Sequence,Is.EqualTo(before+1));Assert.That(em.Exists(survivor),Is.True);
        }

        [Test] public void Barricade007_RoomRequiresBrokenPredecessorAndPlayerEntryThenStaysActive()
        {
            var gate=Gate();var first=Room(Entity.Null,-10,10);var next=Room(gate,10,30);
            var player=em.CreateEntity(typeof(PlayerSnapshot));var system=world.GetOrCreateSystem<BarricadeRoomActivationSystem>();
            em.SetComponentData(player,new PlayerSnapshot{Position=new float3(0,0,20),IsAvailable=true});system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<BarricadeRoom>(next).Activated,Is.Zero);
            Break(gate);system.Update(world.Unmanaged);Assert.That(em.GetComponentData<BarricadeRoom>(next).Activated,Is.EqualTo(1));
            em.SetComponentData(player,new PlayerSnapshot{Position=float3.zero,IsAvailable=true});system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<BarricadeRoom>(first).Activated,Is.EqualTo(1));Assert.That(em.GetComponentData<BarricadeRoom>(next).Activated,Is.EqualTo(1));
        }

        [Test] public void Barricade007_DormantRoomStartsItsDelayOnlyAfterActivation()
        {
            var physics=new PhysicsWorld(0,0,0);
            try
            {
                var p=em.CreateEntity(typeof(PhysicsWorldSingleton));em.SetComponentData(p,new PhysicsWorldSingleton{PhysicsWorld=physics});
                var room=Room(Entity.Null,-10,10);em.AddComponentData(room,new EnemyWaveSequence{Phase=EnemyWaveRuntimePhase.PreWaveDelay});
                em.AddBuffer<EnemyWaveDefinition>(room).Add(new EnemyWaveDefinition{IsValid=1,DelayBeforeWave=5});
                em.AddBuffer<EnemyWaveProfile>(room);em.AddBuffer<EnemyWaveEliteProfile>(room);em.AddBuffer<EnemyWaveSpawnRange>(room);
                em.AddComponent<EnemyWaveEncounterComplete>(room);em.SetComponentEnabled<EnemyWaveEncounterComplete>(room,false);
                var system=world.GetOrCreateSystem<EnemyWaveSpawnSystem>();world.SetTime(new TimeData(50,.02f));system.Update(world.Unmanaged);
                Assert.That(em.GetComponentData<EnemyWaveSequence>(room).Initialized,Is.Zero);
                var state=em.GetComponentData<BarricadeRoom>(room);state.Activated=1;em.SetComponentData(room,state);
                world.SetTime(new TimeData(70,.02f));system.Update(world.Unmanaged);
                Assert.That(em.GetComponentData<EnemyWaveSequence>(room).Initialized,Is.EqualTo(1));
                Assert.That(em.GetComponentData<EnemyWaveSequence>(room).NextActionAt,Is.EqualTo(75));
            }
            finally{physics.Dispose();}
        }

        [Test] public void Barricade007_BreakStopsOnlyItsOwnRoomsReplenishment()
        {
            var a=Gate();var b=Gate();var one=em.CreateEntity(typeof(BarricadeCrowdMember),typeof(EnemyRespawnSettings));var two=em.CreateEntity(typeof(BarricadeCrowdMember),typeof(EnemyRespawnSettings));
            em.SetComponentData(one,new BarricadeCrowdMember{Barricade=a});em.SetComponentData(two,new BarricadeCrowdMember{Barricade=b});Break(a);
            world.GetOrCreateSystem<BarricadeCrowdReplenishmentSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyRespawnSettings>(one).Enabled,Is.Zero);Assert.That(em.GetComponentData<EnemyRespawnSettings>(two).Enabled,Is.EqualTo(1));
        }

        [Test] public void Barricade007_MultipleGatesEmitEachImpactFeedbackOnce()
        {
            var gameObject=new GameObject("Barricade room feedback test");var bridge=gameObject.AddComponent<PlayerEcsBridge>();PlayerBridgeRegistry.TryGetBridge(out var old);PlayerBridgeRegistry.Register(bridge);
            try
            {
                int count=0;bridge.ImpactReceived+=_=>count++;
                var a=Gate();var b=Gate();foreach(var e in new[]{a,b}){var wall=em.GetComponentData<Barricade>(e);wall.HitSequence=1;em.SetComponentData(e,wall);}
                var system=world.GetOrCreateSystem<BarricadePresentationSystem>();system.Update(world.Unmanaged);system.Update(world.Unmanaged);Assert.That(count,Is.EqualTo(2));
                var change=em.GetComponentData<Barricade>(a);change.HitSequence=2;em.SetComponentData(a,change);system.Update(world.Unmanaged);system.Update(world.Unmanaged);Assert.That(count,Is.EqualTo(3));
            }
            finally{PlayerBridgeRegistry.Unregister(bridge);if(old!=null)PlayerBridgeRegistry.Register(old);Object.DestroyImmediate(gameObject);}
        }
    }
}
