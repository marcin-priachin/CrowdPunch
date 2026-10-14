using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup), OrderFirst = true)]
    public partial struct FinaleCycleSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var player = SystemAPI.HasSingleton<PlayerSnapshot>() ? SystemAPI.GetSingleton<PlayerSnapshot>() : default;
            foreach (var (boss, tuning, pose, wall, collider, entity) in SystemAPI.Query<RefRW<FinaleBoss>,RefRO<FinaleTuning>,
                RefRW<LocalTransform>,RefRO<Barricade>,RefRW<PhysicsCollider>>().WithEntityAccess())
            {
                ref var b = ref boss.ValueRW; var t = tuning.ValueRO;
                var history = SystemAPI.GetBuffer<FinaleHit>(entity);
                for (int i=history.Length-1;i>=0;i--) {
                    var h=history[i];
                    if (!SystemAPI.HasComponent<EnemyLaunchState>(h.Source) || SystemAPI.GetComponent<EnemyLaunchState>(h.Source).LaunchSequence!=h.Sequence)
                        history.RemoveAtSwapBack(i);
                }
                b.Flash = math.max(0,b.Flash-dt);
                b.Stage = b.Health <= t.Health*t.StageThree ? 3 : b.Health <= t.Health*t.StageTwo ? 2 : 1;
                collider.ValueRW.Value = b.Phase == FinalePhase.Downed ? wall.ValueRO.IntactCollider : wall.ValueRO.BrokenCollider;
                if (b.Phase == FinalePhase.Defeated) { b.Remaining -= dt; continue; }
                if (b.Phase == FinalePhase.Flying)
                {
                    float3 direction = math.normalizesafe(new float3(player.Forward.x,0,player.Forward.z),new float3(0,0,1));
                    float3 goal = player.Position + direction*t.FlightOffset;
                    goal.y = t.FlightHeight; goal.xz = math.clamp(goal.xz,-38,38);
                    if (b.Attack == FinaleAttack.Sweep) goal = b.WarningCenter + b.WarningDirection*t.SweepLength*.5f + new float3(0,t.FlightHeight+1,0);
                    float3 delta = goal-pose.ValueRO.Position;
                    pose.ValueRW.Position += math.normalizesafe(delta)*math.min(math.length(delta),t.FlightSpeed*dt);
                    float3 facing = math.normalizesafe(new float3(delta.x,0,delta.z),direction);
                    quaternion wanted = quaternion.LookRotationSafe(facing,math.up());
                    float angle = math.acos(math.min(1,math.abs(math.dot(pose.ValueRO.Rotation.value,wanted.value))))*2;
                    pose.ValueRW.Rotation = math.slerp(pose.ValueRO.Rotation,wanted,math.min(1,t.TurnSpeed*dt/math.max(.0001f,angle)));
                }
                else if (b.Phase == FinalePhase.Falling)
                {
                    b.Remaining -= dt; b.AttackRemaining = b.Remaining;
                    pose.ValueRW.Position = math.lerp(b.TransitionStart,b.Landing,math.smoothstep(0,1,1-b.Remaining/t.FallSeconds));
                    if (b.Remaining <= 0) {
                        FinaleAreaAttack.Resolve(state.EntityManager,entity,b,t,player);
                        b.Phase = FinalePhase.Downed; b.Remaining=t.DownedSeconds;
                        b.Attack=FinaleAttack.None; b.AttackRemaining=t.StunSeconds; b.GroundAttackIndex=0;
                        pose.ValueRW.Position=b.Landing;
                        collider.ValueRW.Value=wall.ValueRO.IntactCollider;
                    }
                    continue;
                }
                else if (b.Phase == FinalePhase.TakingOff)
                {
                    b.Remaining -= dt;
                    pose.ValueRW.Position = math.lerp(b.Landing,new float3(b.Landing.x,t.FlightHeight,b.Landing.z),math.smoothstep(0,1,1-b.Remaining/t.TakeoffSeconds));
                    if (b.Remaining <= 0) {
                        b.Phase=FinalePhase.Flying; b.AttackRemaining=t.AirIntervals[b.Stage-1];
                        if (b.SpentCount == 4) { Initialization.FinaleEncounterReset.ResetAnchors(state.EntityManager,entity); b.SpentCount=0; }
                    }
                    continue;
                }
                else
                {
                    b.Remaining -= dt;
                    if (b.Remaining <= 0) {
                        b.Phase=FinalePhase.TakingOff; b.Remaining=t.TakeoffSeconds; b.Attack=FinaleAttack.None;
                        collider.ValueRW.Value=wall.ValueRO.BrokenCollider; continue;
                    }
                }
                b.AttackRemaining -= dt;
                if (b.AttackRemaining > 0 || !player.IsAvailable) continue;
                if (b.Attack != FinaleAttack.None) {
                    FinaleAreaAttack.Resolve(state.EntityManager,entity,b,t,player);
                    b.Attack=FinaleAttack.None;
                    b.AttackRemaining=b.Phase==FinalePhase.Flying?t.AirIntervals[b.Stage-1]:t.GroundInterval;
                }
                else {
                    b.AttackSequence++;
                    b.Attack=b.Phase==FinalePhase.Flying ? (++b.AttackIndex%3==0?FinaleAttack.Sweep:FinaleAttack.Strike)
                        : (++b.GroundAttackIndex%2==1?FinaleAttack.Slam:FinaleAttack.Shockwave);
                    b.WarningCenter=b.Phase==FinalePhase.Flying?player.Position:pose.ValueRO.Position;
                    b.WarningCenter.y=-.9f;
                    b.WarningDirection=math.normalizesafe(new float3(player.Position.x-pose.ValueRO.Position.x,0,player.Position.z-pose.ValueRO.Position.z),new float3(0,0,1));
                    b.AttackRemaining=t.WarningSeconds[b.Stage-1];
                }
            }
        }
    }
}
