using CrowdPunch.Components;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Mathematics;

namespace CrowdPunch.Tests
{
    public sealed class GroundHazardParticleTests
    {
        private static GroundHazard Patch(GroundHazardShape shape)=>new GroundHazard
        {Shape=shape,Position=new float3(12,-.97f,-4),HalfSize=new float2(1.5f,4),Radius=2.5f,Angle=.73f,
            Operation=GroundHazardOperation.Periodic,InactiveDuration=4,WarningDuration=1.5f,ActiveDuration=2.5f};

        [TestCase(GroundHazardPhase.Inactive,1.0)] [TestCase(GroundHazardPhase.Warning,4.8)] [TestCase(GroundHazardPhase.Active,6.5)]
        public void Ground007_OnlyActivePatchesEmitFlameOrEmbers(GroundHazardPhase phase,double time)
        {
            var patch=Patch(GroundHazardShape.Circle);var state=new GroundHazardState{Introduced=1,Phase=phase};int visible=0;
            for(int lane=0;lane<GroundHazardParticleGeometry.Count(patch,state);lane++)
                if(GroundHazardParticleGeometry.TrySample(patch,state,time,lane,out var p))
                {visible++;if(phase!=GroundHazardPhase.Active)Assert.That(p.Kind,Is.EqualTo(GroundHazardParticleKind.Smoke));}
            Assert.That(visible,Is.GreaterThan(0));
            state.Introduced=0;Assert.That(GroundHazardParticleGeometry.Count(patch,state),Is.Zero);
            Assert.That(GroundHazardParticleGeometry.TrySample(patch,state,time,0,out _),Is.False);
        }

        [TestCase(GroundHazardShape.Circle)] [TestCase(GroundHazardShape.Rectangle)]
        public void Ground007_ParticleExtentStaysInsideTheExactRotatedFootprint(GroundHazardShape shape)
        {
            var patch=Patch(shape);
            for(double time=.01;time<24;time+=.037)
            {
                var state=new GroundHazardState{Introduced=1,Phase=GroundHazardGeometry.Phase(patch,time)};
                for(int lane=0;lane<GroundHazardParticleGeometry.Count(patch,state);lane++)
                    if(GroundHazardParticleGeometry.TrySample(patch,state,time,lane,out var p))
                    {
                        Assert.That(math.all(math.isfinite(p.Position)),Is.True);
                        float2 d=p.Position.xz-patch.Position.xz;
                        if(shape==GroundHazardShape.Circle)Assert.That(math.length(d)+p.Radius*1.5f,Is.LessThanOrEqualTo(patch.Radius+.001f));
                        else
                        {float c=math.cos(patch.Angle),s=math.sin(patch.Angle);float2 local=math.abs(new float2(c*d.x-s*d.y,s*d.x+c*d.y))+p.Radius*1.5f;Assert.That(math.all(local<=patch.HalfSize+.001f),Is.True);}
                    }
            }
        }

        [Test] public void Ground007_RetryUsesTheNewWaveClockAndHasABoundedBudget()
        {
            var patch=Patch(GroundHazardShape.Rectangle);patch.HalfSize=new float2(100);var state=new GroundHazardState{Introduced=1,Phase=GroundHazardPhase.Active};
            Assert.That(GroundHazardParticleGeometry.Count(patch,state),Is.EqualTo(20));
            Assert.That(GroundHazardParticleGeometry.TrySample(patch,state,6.4,3,out var before),Is.True);
            state.WaveStartedAt=100;
            Assert.That(GroundHazardParticleGeometry.TrySample(patch,state,106.4,3,out var after),Is.True);
            Assert.That(math.distance(before.Position,after.Position),Is.LessThan(.0001f));
            state.Phase=GroundHazardPhase.Inactive;
            Assert.That(GroundHazardParticleGeometry.Count(patch,state),Is.EqualTo(2));
            Assert.That(GroundHazardParticleGeometry.TrySample(patch,state,101,3,out _),Is.False,"Active particle lanes cannot survive a safe-state transition.");
        }
    }
}
