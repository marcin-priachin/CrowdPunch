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
    public sealed class ShellFragmentPresentationTests
    {
        private World world;
        private EntityManager em;
        private Entity target;
        [SetUp] public void Setup()
        {
            world=new World("Shell fragment presentation");em=world.EntityManager;
            target=em.CreateEntity(typeof(Barricade),typeof(ShellTarget));
            em.SetComponentData(target,new Barricade{RequiredHits=1,HitsRemaining=1,LastHitTime=10});
            ResetShell();
        }
        [TearDown] public void Cleanup()=>world.Dispose();

        [Test] public void Shell006_PlatesTumbleThenClearAndRetryRestoresTheirPose()
        {
            var plate=Part(0);
            Tick(9);
            var shell=em.GetComponentData<ShellTarget>(target);shell.ExplosionsRemaining=0;shell.ShellBrokenAt=10;em.SetComponentData(target,shell);
            Tick(10.2);
            var pose=em.GetComponentData<LocalTransform>(plate);
            Assert.That(pose.Position.x,Is.GreaterThan(.4f));
            Assert.That(math.length(pose.Rotation.value-quaternion.identity.value),Is.GreaterThan(.1f));
            Assert.That(pose.Scale,Is.EqualTo(1));
            Tick(11.5);Assert.That(em.GetComponentData<LocalTransform>(plate).Scale,Is.Zero);
            ResetShell();Tick(12);
            pose=em.GetComponentData<LocalTransform>(plate);
            Assert.That(pose.Position,Is.EqualTo(float3.zero));
            Assert.That(pose.Rotation,Is.EqualTo(quaternion.identity));
            Assert.That(pose.Scale,Is.EqualTo(1));
        }

        [Test] public void Shell006_DamageSeamsFollowShellProgressAndBlockedHitsDoNotEmitSparks()
        {
            var seam=Part(1,2);var spark=Part(2);
            Tick(9);Assert.That(em.GetComponentData<LocalTransform>(seam).Scale,Is.Zero);
            var shell=em.GetComponentData<ShellTarget>(target);shell.ExplosionsRemaining=1;shell.LastHitBlocked=1;em.SetComponentData(target,shell);
            var solid=em.GetComponentData<Barricade>(target);solid.HitSequence=1;em.SetComponentData(target,solid);
            Tick(10.1);
            Assert.That(em.GetComponentData<LocalTransform>(seam).Scale,Is.EqualTo(1));
            Assert.That(em.GetComponentData<LocalTransform>(spark).Scale,Is.Zero);
            shell.LastHitBlocked=0;em.SetComponentData(target,shell);Tick(10.12);
            Assert.That(em.GetComponentData<LocalTransform>(spark).Scale,Is.GreaterThan(0));
        }

        [Test] public void Shell006_ArmorBreakKeepsCoreVisibleUntilCoreDeath()
        {
            var core=Part(0,0,true);var shell=em.GetComponentData<ShellTarget>(target);
            shell.ExplosionsRemaining=0;shell.ShellBrokenAt=10;em.SetComponentData(target,shell);Tick(11.5);
            Assert.That(em.GetComponentData<LocalTransform>(core).Scale,Is.EqualTo(1));
            shell.CoreHealth=0;em.SetComponentData(target,shell);Tick(11.5);
            Assert.That(em.GetComponentData<LocalTransform>(core).Scale,Is.Zero);
        }

        private Entity Part(byte kind,byte damage=0,bool core=false)
        {
            var e=em.CreateEntity(typeof(ShellFragmentMotion),typeof(ShellVisual),typeof(BarricadeVisual),typeof(LocalTransform),typeof(URPMaterialPropertyBaseColor));
            em.SetComponentData(e,new ShellFragmentMotion{Kind=kind,Velocity=new float3(3,4,2),Spin=new float3(2,3,4),Duration=1.2f,RestingY=-1});
            em.SetComponentData(e,new ShellVisual{IsCore=core?(byte)1:(byte)0});
            em.SetComponentData(e,new BarricadeVisual{Barricade=target,Color=new float4(1),CrackStage=damage});
            em.SetComponentData(e,LocalTransform.Identity);return e;
        }
        private void ResetShell()=>em.SetComponentData(target,new ShellTarget{RequiredExplosions=3,ExplosionsRemaining=3,CoreHealth=5,CoreMaxHealth=5});
        private void Tick(double now)
        {
            world.SetTime(new TimeData(now,.016f));
            world.GetOrCreateSystem<ShellFragmentPresentationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
        }
    }
}
