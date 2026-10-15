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
    public partial struct ChickenAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency=new Animate { Bosses=SystemAPI.GetComponentLookup<ChickenBoss>(true),
                Tuning=SystemAPI.GetComponentLookup<ChickenTuning>(true), Delta=SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
        }
        [BurstCompile]
        private partial struct Animate : IJobEntity
        {
            [ReadOnly] public ComponentLookup<ChickenBoss> Bosses;
            [ReadOnly] public ComponentLookup<ChickenTuning> Tuning;
            public float Delta;
            public void Execute(in EnemyAnimation animation,in BossAnimationAnchor anchor,ref EnemyAnimationPlayback playback,ref DynamicBuffer<SkinMatrix> skin)
            {
                if(!Bosses.HasComponent(animation.Owner) || !animation.Samples.IsCreated) return;
                var b=Bosses[animation.Owner]; var t=Tuning[animation.Owner];
                ref var data=ref animation.Samples.Value;
                if(skin.Length!=data.BoneCount) return;
                int motion=b.Phase==ChickenPhase.Rush?1:b.Phase==ChickenPhase.WindUp?2:b.Phase==ChickenPhase.Stagger?3:b.Phase==ChickenPhase.Defeated?4:0;
                bool initialized=playback.Initialized!=0;
                if(playback.Initialized!=(byte)(motion+1)) { playback.Phase=0; playback.Initialized=(byte)(motion+1); }
                bool loop=motion<2;
                float duration=motion==2?t.WindUp:motion==3?t.Stagger:motion==1?data.Durations[motion]/(b.Stage==3?1.65f:1.3f):data.Durations[motion];
                playback.Phase=loop?math.frac(playback.Phase+Delta/math.max(.01f,duration)):math.saturate(playback.Phase+Delta/math.max(.01f,duration));
                // The last part of the beak wind-up follows the real release timer, including paired-shot waits.
                if(motion==2) playback.Phase=.75f*(1-math.saturate(b.Remaining/math.max(.01f,t.WindUp)));
                float charge=motion==2?1-math.saturate(b.Remaining/math.max(.01f,t.WindUp)):0;
                float recoil=motion==1?math.exp(-b.SinceShot*12)*.12f:0;
                var scale=new float3(1+charge*.1f-recoil*.25f,1-charge*.13f+recoil,1+charge*.06f);
                BossSkinPose.Sample(ref data,ref skin,motion,playback.Phase,loop,anchor.Floor,scale,quaternion.identity,
                    initialized?1-math.exp(-24*Delta):1);
            }
        }
    }
}
