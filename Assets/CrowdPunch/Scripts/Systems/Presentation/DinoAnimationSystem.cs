using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile,UpdateInGroup(typeof(GamePresentationGroup)),UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial struct DinoAnimationSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            state.Dependency=new Animate { Bosses=SystemAPI.GetComponentLookup<DinoBoss>(true),
                Tuning=SystemAPI.GetComponentLookup<DinoTuning>(true),Delta=SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
        }
        [BurstCompile] private partial struct Animate : IJobEntity
        {
            [ReadOnly] public ComponentLookup<DinoBoss> Bosses;
            [ReadOnly] public ComponentLookup<DinoTuning> Tuning;
            public float Delta;
            public void Execute(in EnemyAnimation animation,in BossAnimationAnchor anchor,ref EnemyAnimationPlayback playback,ref DynamicBuffer<SkinMatrix> skin)
            {
                if(!Bosses.HasComponent(animation.Owner) || !animation.Samples.IsCreated) return;
                var b=Bosses[animation.Owner]; var t=Tuning[animation.Owner]; ref var data=ref animation.Samples.Value;
                if(skin.Length!=data.BoneCount) return;
                int motion=b.Phase==DinoPhase.Burst?1:b.Phase==DinoPhase.Stagger?3:b.Phase==DinoPhase.Defeated?4:0;
                bool initialized=playback.Initialized!=0;
                if(playback.Initialized!=(byte)(motion+1)) { playback.Phase=0; playback.Initialized=(byte)(motion+1); }
                bool loop=motion<2;
                float strideRate=motion==1?1.25f+.12f*(b.Stage-1):math.clamp(t.ChaseSpeeds[math.clamp(b.Stage-1,0,2)]/math.max(.01f,t.ChaseSpeeds.x),.8f,1.6f);
                float duration=motion==3?t.StaggerDuration:data.Durations[motion]/(motion<2?strideRate:1);
                playback.Phase=loop?math.frac(playback.Phase+Delta/math.max(.01f,duration)):math.saturate(playback.Phase+Delta/math.max(.01f,duration));
                float warning=b.Phase==DinoPhase.Warning?1-math.saturate(b.Remaining/math.max(.01f,t.WarningDuration)):0;
                // A grounded crouch loads the charge while locomotion continues underneath.
                float squash=warning*.15f;
                float stretch=b.Phase==DinoPhase.Burst?.07f:0;
                BossSkinPose.Sample(ref data,ref skin,motion,playback.Phase,loop,anchor.Floor,
                    new float3(1+squash*.5f,1-squash-stretch*.4f,1+stretch),quaternion.identity,
                    initialized?1-math.exp(-18*Delta):1);
            }
        }
    }
}
