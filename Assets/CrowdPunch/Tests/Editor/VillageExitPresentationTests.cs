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
    public sealed class VillageExitPresentationTests
    {
        private World world;
        private EntityManager em;
        private Entity gate;

        [SetUp] public void Setup()
        {
            world=new World("Village exit cues");em=world.EntityManager;
            gate=em.CreateEntity(typeof(Barricade));
            em.SetComponentData(gate,new Barricade {HitsRemaining=3,RequiredHits=3,ExitRadius=2,ExitPosition=new float3(0,.5f,16)});
        }
        [TearDown] public void Cleanup()=>world.Dispose();
        private Entity Cue(bool arrow)
        {
            var e=em.CreateEntity(typeof(VillageExitCue),typeof(LocalTransform),typeof(URPMaterialPropertyBaseColor));
            em.SetComponentData(e,LocalTransform.FromPosition(new float3(0,.08f,16)));
            em.SetComponentData(e,new VillageExitCue {Gate=gate,Arrow=arrow?(byte)1:(byte)0,
                LockedColor=new float4(.2f,.3f,.4f,1),OpenColor=new float4(.4f,1,.7f,1)});
            return e;
        }
        private void Tick(double time)
        {
            world.SetTime(new TimeData(time,.016f));
            world.GetOrCreateSystem<VillageExitPresentationSystem>().Update(world.Unmanaged);
        }
        private void Open()
        {
            var b=em.GetComponentData<Barricade>(gate);b.HitsRemaining=0;b.LastHitTime=10;em.SetComponentData(gate,b);
        }

        [Test] public void Barricade001_ArrowsAreHiddenBeforeBreakIncludingLateLevelEntry()
        {
            var arrow=Cue(true);var ring=Cue(false);Tick(400);
            Assert.That(em.GetComponentData<LocalTransform>(arrow).Scale,Is.Zero);
            Assert.That(em.GetComponentData<LocalTransform>(ring).Scale,Is.EqualTo(1));
            Assert.That(em.GetComponentData<URPMaterialPropertyBaseColor>(ring).Value,
                Is.EqualTo(em.GetComponentData<VillageExitCue>(ring).LockedColor));
        }
        [Test] public void Barricade003_OpenCueRevealsWithoutMovingDestinationOrChangingCompletionRadius()
        {
            var arrow=Cue(true);var ring=Cue(false);Tick(1);Open();Tick(10.5);
            Assert.That(em.GetComponentData<LocalTransform>(arrow).Scale,Is.EqualTo(1));
            for(int i=0;i<100;i++) Tick(11+i*.05);
            var transform=em.GetComponentData<LocalTransform>(ring);
            Assert.That(transform.Position,Is.EqualTo(new float3(0,.08f,16)));
            Assert.That(transform.Scale,Is.EqualTo(1));
            var b=em.GetComponentData<Barricade>(gate);
            Assert.That(b.ExitRadius,Is.EqualTo(2));Assert.That(b.ExitPosition,Is.EqualTo(new float3(0,.5f,16)));
            Assert.That(b.CompleteOnDestruction,Is.Zero);
        }
        [Test] public void Barricade001_ResetHidesOpenRouteAndRestoresQuietColor()
        {
            var arrow=Cue(true);Tick(1);Open();Tick(11);
            var b=em.GetComponentData<Barricade>(gate);b.HitsRemaining=3;em.SetComponentData(gate,b);Tick(12);
            Assert.That(em.GetComponentData<LocalTransform>(arrow).Scale,Is.Zero);
            Assert.That(em.GetComponentData<URPMaterialPropertyBaseColor>(arrow).Value,
                Is.EqualTo(em.GetComponentData<VillageExitCue>(arrow).LockedColor));
        }
    }
}
