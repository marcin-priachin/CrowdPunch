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
                float frame=playback.Phase*(loop?data.FrameCount:data.FrameCount-1);
                int a=math.min((int)frame,data.FrameCount-1), next=loop?(a+1)%data.FrameCount:math.min(a+1,data.FrameCount-1);
                // Rotate only the sampled skin around the body's centre. The physics capsule stays upright.
                pivot.Angle=b.Phase==RollingPhase.Roll?math.fmod(pivot.Angle+math.radians(t.VisualRollDegreesPerSecond)*Delta,2*math.PI):0;
                float3x3 spin=new float3x3(quaternion.AxisAngle(new float3(1,0,0),pivot.Angle));
                for(int bone=0;bone<skin.Length;bone++)
                {
                    var from=data.Matrices[(motion*data.FrameCount+a)*data.BoneCount+bone];
                    var to=data.Matrices[(motion*data.FrameCount+next)*data.BoneCount+bone];
                    float f=math.frac(frame);
                    skin[bone]=new SkinMatrix { Value=new float3x4(math.mul(spin,math.lerp(from.c0,to.c0,f)),
                        math.mul(spin,math.lerp(from.c1,to.c1,f)),math.mul(spin,math.lerp(from.c2,to.c2,f)),
                        math.mul(spin,math.lerp(from.c3,to.c3,f)-pivot.Center)+pivot.Center) };
                }
            }
        }
    }
}
