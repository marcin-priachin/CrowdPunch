using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(InputBridge.PlayerBridgeSystem))]
    [UpdateBefore(typeof(Combat.PunchAimAssistSystem))]
    public partial struct RollingCycleSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var p=SystemAPI.HasSingleton<PlayerSnapshot>()?SystemAPI.GetSingleton<PlayerSnapshot>():default;
            if(!p.IsAvailable) return;
            foreach(var (boss,tuning,pose) in SystemAPI.Query<RefRW<RollingBoss>,RefRO<RollingTuning>,RefRO<LocalTransform>>())
                Advance(ref boss.ValueRW,tuning.ValueRO,pose.ValueRO.Position,p.Position,SystemAPI.Time.DeltaTime);
        }

        internal static void Advance(ref RollingBoss b,in RollingTuning t,float3 position,float3 player,float dt)
        {
            if(b.Phase==RollingPhase.Defeated) return;
            b.PreviousPosition=position;
            if(b.Phase==RollingPhase.WindUp)
                b.Direction=math.normalizesafe(new float3(player.x-position.x,0,player.z-position.z),b.Direction);
            b.Remaining-=dt;
            bool ended=b.Remaining<=0 || b.Phase==RollingPhase.Roll && t.End==RollingEnd.BounceCount && b.Bounces>=t.BounceLimit;
            if(!ended) return;
            if(b.Phase==RollingPhase.Pause) { b.Phase=RollingPhase.WindUp; b.Remaining=t.WindUp; }
            else if(b.Phase==RollingPhase.WindUp)
            {
                b.Phase=RollingPhase.Roll;
                b.Remaining=t.End==RollingEnd.Duration?t.RollDuration:t.MaximumRollDuration;
                b.Speed=t.RollSpeeds[math.clamp(b.CycleStage-1,0,2)];
                b.Bounces=0; b.RollSequence++;
            }
            else
            {
                b.CycleStage=b.Stage;
                b.Phase=RollingPhase.Pause; b.Remaining=t.PauseDurations[math.clamp(b.CycleStage-1,0,2)];
            }
        }
    }
}
