using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.InputBridge;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(PlayerBridgeSystem))]
    public partial struct ChickenAttackSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<ChickenBoss>(); state.RequireForUpdate<PlayerSnapshot>(); }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player=SystemAPI.GetSingleton<PlayerSnapshot>();
            if(!player.IsAvailable) return;
            float dt=SystemAPI.Time.DeltaTime;
            var commands=new EntityCommandBuffer(Allocator.Temp);
            foreach(var (boss,tuning,transform,entity) in SystemAPI.Query<RefRW<ChickenBoss>,RefRO<ChickenTuning>,RefRO<LocalTransform>>().WithEntityAccess())
            {
                ref var b=ref boss.ValueRW; var t=tuning.ValueRO; var p=transform.ValueRO.Position;
                b.PreviousPosition=p; b.SinceShot+=dt; b.Remaining-=dt;
                if(b.Phase==ChickenPhase.Defeated) continue;
                if(b.Phase==ChickenPhase.Stagger)
                {
                    if(b.Remaining<=0) { b.Phase=ChickenPhase.Pause; b.Remaining=PauseDuration(b.Stage,t); }
                    continue;
                }
                if(b.Phase==ChickenPhase.Rush)
                {
                    if(math.distancesq(p.xz,b.Destination.xz)>.01f) continue;
                    if(b.ShotsRemaining>0) { b.Phase=ChickenPhase.WindUp; b.Remaining=math.max(t.WindUp,t.ShotSpacing-b.SinceShot); }
                    else { b.Phase=ChickenPhase.Pause; b.Remaining=PauseDuration(b.Stage,t); }
                }
                if(b.Phase==ChickenPhase.Pause)
                {
                    if(t.PauseResponse==ChickenPauseResponse.InterruptAndFlee
                        && math.distancesq(p.xz,player.Position.xz)<t.ProximityDistance*t.ProximityDistance)
                    { b.ShotsRemaining=0; CommitRush(ref b,p,player.Position,t); continue; }
                    if(b.Remaining>0) continue;
                    b.ShotsRemaining=Pattern(b.Stage,t)==ChickenPattern.Paired?2:1;
                    b.Phase=ChickenPhase.WindUp; b.Remaining=t.WindUp;
                }
                if(b.Phase!=ChickenPhase.WindUp) continue;
                float3 aim=player.Position;
                if(t.ShotAim==ChickenShotAim.MovementLead)
                    aim+=player.Velocity*math.min(3,math.distance(p.xz,aim.xz)/t.FireSpeed);
                float3 direction=math.normalizesafe(new float3(aim.x-p.x,0,aim.z-p.z),new float3(0,0,-1));
                b.Facing=quaternion.LookRotationSafe(direction,math.up());
                if(b.Remaining>0) continue;
                var shot=commands.Instantiate(t.ProjectilePrefab);
                float3 start=p+direction*(t.BodyRadius+t.ProjectileRadius+.15f); start.y=t.ProjectileHeight;
                commands.SetComponent(shot,LocalTransform.FromPositionRotationScale(start,quaternion.identity,t.ProjectileRadius*2));
                commands.SetComponent(shot,new ChickenProjectile { Boss=entity,Velocity=direction*t.FireSpeed,Launch=1 });
                b.ShotsRemaining--; b.SinceShot=0;
                // Release and committed rush happen in the same pre-physics step.
                CommitRush(ref b,p,player.Position,t);
            }
            commands.Playback(state.EntityManager); commands.Dispose();
        }

        internal static ChickenPattern Pattern(int stage,in ChickenTuning t) => stage==1?t.StageOnePattern:stage==2?t.StageTwoPattern:t.StageThreePattern;
        internal static float PauseDuration(int stage,in ChickenTuning t) => t.Pause*(stage==3?t.FinalPauseMultiplier:1);
        internal static void CommitRush(ref ChickenBoss b,float3 position,float3 player,in ChickenTuning t)
        {
            float2 away=math.normalizesafe(position.xz-player.xz,new float2(0,1));
            float2 side=new float2(-away.y,away.x)*(b.AlternateSide==0?1:-1);
            float2 limit=math.max(new float2(.1f),t.ArenaHalfSize-t.BodyRadius-.15f);
            float2 best=position.xz; float score=-1;
            // Both side choices are bounded in the convex open court, so every segment is reachable.
            for(int i=0;i<4;i++)
            {
                float2 d=i<2?math.normalizesafe(away+side*t.SidewaysWeight*(i==0?1:-1)):side*(i==2?1:-1);
                float2 candidate=math.clamp(position.xz+d*t.RushDistance,t.ArenaCenter-limit,t.ArenaCenter+limit);
                float distance=math.distancesq(candidate,position.xz);
                if(distance>score) { best=candidate; score=distance; }
            }
            b.Destination=new float3(best.x,position.y,best.y); b.Phase=ChickenPhase.Rush;
            b.Facing=quaternion.LookRotationSafe(math.normalizesafe(b.Destination-position,new float3(0,0,1)),math.up());
            b.RushSequence++; b.RushHitPlayer=0; b.AlternateSide^=1;
        }
    }
}
