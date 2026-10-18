using CrowdPunch.Components;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class VillageGatePresentationTests
    {
        private World world;
        private EntityManager em;
        private Entity gate;

        [SetUp] public void Setup()
        {
            world=new World("Village gate presentation");em=world.EntityManager;
            gate=em.CreateEntity(typeof(Barricade),typeof(LocalTransform));
            em.SetComponentData(gate,LocalTransform.Identity);
            em.SetComponentData(gate,new Barricade {RequiredHits=3,HitsRemaining=3,Size=new float3(22,4,1.2f)});
        }
        [TearDown] public void Cleanup()=>world.Dispose();

        private Entity Piece(byte kind,byte stage=0)
        {
            var e=em.CreateEntity(typeof(VillageGatePiece),typeof(BarricadeVisual),typeof(LocalTransform),typeof(URPMaterialPropertyBaseColor));
            em.SetComponentData(e,LocalTransform.Identity);
            em.SetComponentData(e,new BarricadeVisual {Barricade=gate,Color=new float4(.6f,.4f,.2f,1)});
            em.SetComponentData(e,new VillageGatePiece {Gate=gate,Kind=kind,DamageStage=stage,
                Duration=1.5f,Velocity=new float3(1,3,4),Spin=new float3(2,3,1),RestingHeight=.1f,Seed=.4f});
            return e;
        }
        private void Tick(double time)
        {
            world.SetTime(new TimeData(time,.016f));
            world.GetOrCreateSystem<VillageGatePresentationSystem>().Update(world.Unmanaged);
        }
        private void Hit(int remaining)
        {
            var b=em.GetComponentData<Barricade>(gate);b.HitsRemaining=remaining;
            b.HitSequence=(uint)(3-remaining);b.LastHitTime=10;b.LastHitPosition=new float3(0,0,-.6f);
            em.SetComponentData(gate,b);
        }

        [Test] public void Barricade004_IntactGateHasNoPreplayedCracksOrParticlesEvenOnLateEntry()
        {
            var board=Piece(0);var crack=Piece(1,1);var chip=Piece(2);var dust=Piece(3);
            Tick(5);
            Assert.That(em.GetComponentData<LocalTransform>(board).Scale,Is.EqualTo(1));
            foreach(var e in new[]{crack,chip,dust}) Assert.That(em.GetComponentData<LocalTransform>(e).Scale,Is.Zero);
        }

        [Test] public void Barricade004_DamageStagesRemainVisibleBetweenHitsWithoutOpeningTheGate()
        {
            var first=Piece(1,1);var second=Piece(1,2);Tick(1);Hit(2);Tick(10.3);
            Assert.That(em.GetComponentData<LocalTransform>(first).Scale,Is.GreaterThan(0));
            Assert.That(em.GetComponentData<LocalTransform>(second).Scale,Is.Zero);
            Hit(1);Tick(10.3);
            Assert.That(em.GetComponentData<LocalTransform>(second).Scale,Is.GreaterThan(0));
            Assert.That(em.GetComponentData<Barricade>(gate).HitsRemaining,Is.EqualTo(1));
        }

        [TestCase((byte)2)] [TestCase((byte)3)]
        public void Barricade004_HitDebrisExitsTheStruckFaceOfAnIntactGate(byte kind)
        {
            var e=Piece(kind);Tick(1);Hit(2);Tick(10.1);
            var t=em.GetComponentData<LocalTransform>(e);
            Assert.That(t.Scale,Is.GreaterThan(0));
            Assert.That(t.Position.z,Is.LessThan(-.6f),"Debris must be visible in front of the solid gate.");
        }

        [Test] public void Barricade004_BrokenTimbersTumbleThenDisappearCompletely()
        {
            var e=Piece(0);Tick(1);Hit(0);Tick(10.35);
            var during=em.GetComponentData<LocalTransform>(e);
            Assert.That(during.Scale,Is.GreaterThan(0));Assert.That(math.length(during.Position),Is.GreaterThan(.1f));
            Assert.That(math.abs(math.dot(during.Rotation,quaternion.identity)),Is.LessThan(.99f));
            Tick(12);Assert.That(em.GetComponentData<LocalTransform>(e).Scale,Is.Zero);
            Tick(13);Assert.That(em.GetComponentData<LocalTransform>(e).Scale,Is.Zero);
        }
    }
}
