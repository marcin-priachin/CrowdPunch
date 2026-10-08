using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(InputBridge.PlayerBridgeSystem))]
    public partial struct DinoCycleSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            if(!SystemAPI.HasSingleton<PlayerSnapshot>() || !SystemAPI.GetSingleton<PlayerSnapshot>().IsAvailable) return;
            foreach(var (boss,tuning) in SystemAPI.Query<RefRW<DinoBoss>,RefRO<DinoTuning>>())
                Advance(ref boss.ValueRW,tuning.ValueRO,SystemAPI.Time.DeltaTime);
        }
        internal static void Advance(ref DinoBoss b,in DinoTuning t,float dt)
        {
            if(b.Phase==DinoPhase.Defeated) return;
            b.Remaining-=dt; if(b.Remaining>0) return;
            if(b.Phase==DinoPhase.Chase) { b.Phase=DinoPhase.Warning; b.Remaining=t.WarningDuration; }
            else if(b.Phase==DinoPhase.Warning) { b.Phase=DinoPhase.Burst; b.Remaining=t.BurstDuration; }
            else { b.Phase=DinoPhase.Chase; b.Remaining=t.ChaseDurations[Unity.Mathematics.math.clamp(b.Stage-1,0,2)]; }
        }
    }
}
