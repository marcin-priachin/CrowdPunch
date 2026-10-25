using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
namespace CrowdPunch.Systems.Presentation
{
    [BurstCompile,UpdateInGroup(typeof(GamePresentationGroup)),UpdateAfter(typeof(EnemyAnimationSystem))]
    public partial struct DinoAnimationSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            state.Dependency=new Animate { Bosses=SystemAPI.GetComponentLookup<DinoBoss>(true),
                Tuning=SystemAPI.GetComponentLookup<DinoTuning>(true),
                Velocities=SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                Transforms=SystemAPI.GetComponentLookup<LocalTransform>(true),Delta=SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
        }
        [BurstCompile] private partial struct Animate : IJobEntity
        {
            [ReadOnly] public ComponentLookup<DinoBoss> Bosses;
            [ReadOnly] public ComponentLookup<DinoTuning> Tuning;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            public float Delta;
            public void Execute(in EnemyAnimation animation,in BossAnimationAnchor anchor,ref EnemyAnimationPlayback playback,ref DynamicBuffer<SkinMatrix> skin)
            {
                if(!Bosses.HasComponent(animation.Owner) || !animation.Samples.IsCreated) return;
                var b=Bosses[animation.Owner]; var t=Tuning[animation.Owner]; ref var data=ref animation.Samples.Value;
                if(skin.Length!=data.BoneCount) return;
                int motion=b.Phase==DinoPhase.Burst?1:b.Phase==DinoPhase.Warning?2:b.Phase==DinoPhase.Stagger?3:b.Phase==DinoPhase.Defeated?4:0;
                bool initialized=playback.Initialized!=0;
                if(playback.Initialized!=(byte)(motion+1)) { playback.Phase=0; playback.Initialized=(byte)(motion+1); }
                bool loop=motion<2;
                if(motion==2) playback.Phase=1-math.saturate(b.Remaining/math.max(.01f,t.WarningDuration));
                else if(motion==3) playback.Phase=1-math.saturate(b.Remaining/math.max(.01f,t.StaggerDuration));
                else playback.Phase=loop?math.frac(playback.Phase+Delta/math.max(.01f,data.Durations[motion]))
                    :math.saturate(playback.Phase+Delta/math.max(.01f,data.Durations[motion]));
                float blend=!initialized || animation.BlendResponse<=0?1:1-math.exp(-animation.BlendResponse*Delta);
                ChickenSkinPose.Sample(ref data,ref skin,motion,playback.Phase,loop,blend);
                if(motion<3 && animation.JogSamples.IsCreated && Velocities.HasComponent(animation.Owner) && Transforms.HasComponent(animation.Owner))
                {
                    // Physics owns movement. Compose directional jogs below the authored upper-body pose.
                    float3 local=math.rotate(math.inverse(Transforms[animation.Owner].Rotation),Velocities[animation.Owner].Linear);
                    float speed=math.length(local.xz);
                    playback.Movement=speed>.03f?local.xz/speed:float2.zero;
                    ChickenSkinPose.Jog(ref animation.JogSamples.Value,ref skin,ref playback,Delta,speed,animation.MaxLegAnimationSpeed);
                }
                else { playback.Movement=0;playback.JogPhase=0; }
            }
        }
    }
}
