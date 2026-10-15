using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup)), UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial struct RollingAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency=new Animate { Bosses=SystemAPI.GetComponentLookup<RollingBoss>(true),
                Tuning=SystemAPI.GetComponentLookup<RollingTuning>(true),Delta=SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
        }
        [BurstCompile]
        private partial struct Animate : IJobEntity
        {
            [ReadOnly] public ComponentLookup<RollingBoss> Bosses;
            [ReadOnly] public ComponentLookup<RollingTuning> Tuning;
            public float Delta;
            public void Execute(in EnemyAnimation animation,ref EnemyAnimationPlayback playback,ref RollingAnimationPivot pivot,ref DynamicBuffer<SkinMatrix> skin)
            {
                if(!Bosses.HasComponent(animation.Owner) || !animation.Samples.IsCreated) return;
                var b=Bosses[animation.Owner]; var t=Tuning[animation.Owner];
                ref var data=ref animation.Samples.Value;
                if(skin.Length!=data.BoneCount) return;
                int motion=b.Phase==RollingPhase.Roll?1:b.Phase==RollingPhase.WindUp?2:b.Phase==RollingPhase.Defeated?4:0;
                if(playback.Initialized!=(byte)(motion+1)) { playback.Phase=0; playback.Initialized=(byte)(motion+1); }
                bool loop=motion<2;
                float duration=motion==2?t.WindUp:data.Durations[motion];
                playback.Phase=loop?math.frac(playback.Phase+Delta/math.max(.01f,duration)):math.saturate(playback.Phase+Delta/math.max(.01f,duration));
                // Wind-up compresses into the collider; rolling uses the calm pose, not a walking cycle.
                float charge=b.Phase==RollingPhase.WindUp?1-math.saturate(b.Remaining/math.max(.01f,t.WindUp)):0;
                pivot.Angle=b.Phase==RollingPhase.Roll?math.fmod(pivot.Angle+math.radians(t.VisualRollDegreesPerSecond)*Delta,2*math.PI)
                    :math.atan2(math.sin(pivot.Angle),math.cos(pivot.Angle))*math.exp(-14*Delta);
                var scale=new float3(1,1-charge*.18f,1-charge*.06f);
                BossSkinPose.Sample(ref data,ref skin,motion,playback.Phase,loop,pivot.Center,scale,
                    quaternion.RotateX(pivot.Angle),1);
                if(pivot.Floor.IsCreated && b.Phase!=RollingPhase.Defeated)
                {
                    // A sampled support curve keeps the asymmetric spikes on the floor throughout the roll.
                    // This is only a skin translation; the physical capsule and its trajectory are unchanged.
                    ref var offsets=ref pivot.Floor.Value.Offsets;
                    float coordinate=math.frac(pivot.Angle/(2*math.PI))*offsets.Length;
                    // A tiny negative angle can round frac to exactly 1. Wrap that endpoint
                    // to the first sample, just like the neighbouring sample at the seam.
                    int index=(int)coordinate%offsets.Length;
                    float offset=math.lerp(offsets[index],offsets[(index+1)%offsets.Length],math.frac(coordinate));
                    for(int bone=0;bone<skin.Length;bone++) { var matrix=skin[bone].Value; matrix.c3.y+=offset; skin[bone]=new SkinMatrix { Value=matrix }; }
                }
            }
        }
    }
}
