using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.InputBridge;
using CrowdPunch.Systems.Movement;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(PlayerBridgeSystem))]
    public partial struct BossHandCoordinationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if(!SystemAPI.HasSingleton<PlayerSnapshot>()) return;
            var player=SystemAPI.GetSingleton<PlayerSnapshot>();
            float dt=SystemAPI.Time.DeltaTime;
            foreach(var (bossRef,tuning,head,entity) in SystemAPI.Query<RefRW<BossEncounter>,RefRO<BossTuning>,RefRO<LocalTransform>>().WithEntityAccess())
            {
                ref var boss=ref bossRef.ValueRW; var t=tuning.ValueRO;
                var em=state.EntityManager;
                if(!em.Exists(boss.LeftHand)||!em.Exists(boss.RightHand)) continue;
                var left=em.GetComponentData<BossHand>(boss.LeftHand); var right=em.GetComponentData<BossHand>(boss.RightHand);
                var lp=em.GetComponentData<LocalTransform>(boss.LeftHand).Position;
                var rp=em.GetComponentData<LocalTransform>(boss.RightHand).Position;
                float3 forward=math.normalizesafe(new float3(t.Center.x-head.ValueRO.Position.x,0,t.Center.y-head.ValueRO.Position.z),new float3(0,0,-1));
                float3 side=math.cross(math.up(),forward);
                quaternion rotation=quaternion.LookRotationSafe(forward,math.up());
                var headTarget=em.GetComponentData<BossMotionTarget>(entity);
                headTarget.Rotation=rotation;
                bool running=player.IsAvailable && boss.Cycle!=BossCycle.Defeated;
                if(!running)
                {
                    Hold(em,boss.LeftHand,lp,rotation); Hold(em,boss.RightHand,rp,rotation);
                    headTarget.Position=head.ValueRO.Position; em.SetComponentData(entity,headTarget); continue;
                }
                float factor=boss.Stage==3?t.StageThreeTiming:1;
                AdvanceHand(ref left,dt); AdvanceHand(ref right,dt);
                if(boss.Cycle==BossCycle.Transition)
                {
                    left.Phase=right.Phase=BossHandPhase.Returning;
                    boss.Remaining-=dt;
                    if(boss.Remaining<=0) { boss.Cycle=BossCycle.Opening; boss.Remaining=t.OpeningDuration; }
                }
                else if(boss.Cycle==BossCycle.Relocating)
                {
                    float step=math.min(t.MoveSpeed*dt,boss.TravelRemaining);
                    boss.RouteDistance+=step; boss.TravelRemaining-=step;
                    headTarget.Position=BossPerimeterRoute.Position(boss.RouteDistance,t);
                    if(boss.TravelRemaining<=.001f) { boss.Cycle=BossCycle.Opening; boss.Remaining=t.OpeningDuration; }
                }
                else if(boss.Cycle==BossCycle.Opening)
                {
                    // A promised opening starts after both hands have actually left the head.
                    if(Settled(left)&&Settled(right)) boss.Remaining-=dt;
                    if(boss.Remaining<=0)
                    {
                        Entity attacking=boss.NextHand==0?boss.LeftHand:boss.RightHand;
                        if(boss.NextHand==0) BeginAttack(ref left,lp,player.Position,t,boss.AttackIndex,factor,head.ValueRO.Position,forward);
                        else BeginAttack(ref right,rp,player.Position,t,boss.AttackIndex,factor,head.ValueRO.Position,forward);
                        boss.AttackIndex++; boss.Cycle=BossCycle.Attacking; boss.Remaining=t.CoordinationDelay;
                        boss.SecondAttackPending=(byte)(boss.Stage==3?1:0);
                    }
                }
                else if(boss.Cycle==BossCycle.Attacking)
                {
                    if(boss.SecondAttackPending!=0)
                    {
                        boss.Remaining-=dt;
                        if(boss.Remaining<=0)
                        {
                            if(boss.NextHand==0 && Settled(right)) { BeginAttack(ref right,rp,player.Position,t,boss.AttackIndex++,factor,head.ValueRO.Position,forward); boss.SecondAttackPending=0; }
                            else if(boss.NextHand!=0 && Settled(left)) { BeginAttack(ref left,lp,player.Position,t,boss.AttackIndex++,factor,head.ValueRO.Position,forward); boss.SecondAttackPending=0; }
                        }
                    }
                    if(boss.SecondAttackPending==0 && Settled(left)&&Settled(right))
                    {
                        boss.NextHand=1-boss.NextHand;
                        // Every attack gives its complete recovery before relocation is allowed.
                        boss.Cycle=BossCycle.Relocating; boss.TravelRemaining=t.MoveDistance;
                        left.Phase=right.Phase=BossHandPhase.Returning;
                    }
                }
                UpdateTarget(ref left,lp,boss,head.ValueRO.Position,forward,side,-1,t,factor,em,boss.LeftHand,rotation);
                UpdateTarget(ref right,rp,boss,head.ValueRO.Position,forward,side,1,t,factor,em,boss.RightHand,rotation);
                em.SetComponentData(boss.LeftHand,left); em.SetComponentData(boss.RightHand,right);
                em.SetComponentData(entity,headTarget);
            }
        }

        internal static bool Settled(BossHand h) => h.Phase==BossHandPhase.Ready || h.Phase==BossHandPhase.Shielding;
        internal static BossAttackTuning AttackTuning(BossAttack a,in BossTuning t) => a==BossAttack.Slam?t.Slam:a==BossAttack.Lunge?t.Lunge:t.Sweep;
        private static void AdvanceHand(ref BossHand h,float dt)
        {
            if(h.Phase==BossHandPhase.Anticipation || h.Phase==BossHandPhase.Active || h.Phase==BossHandPhase.Recovery || h.Phase==BossHandPhase.Staggered)
                h.Remaining-=dt;
            if(h.Phase==BossHandPhase.Staggered && h.Remaining<=0) h.Phase=BossHandPhase.Returning;
            if(h.Phase==BossHandPhase.Recovery && h.Remaining<=0) h.Phase=BossHandPhase.Returning;
        }

        private static void BeginAttack(ref BossHand h,float3 position,float3 player,in BossTuning t,int index,float factor,float3 head,float3 front)
        {
            h.Attack=(BossAttack)(index%3); var a=AttackTuning(h.Attack,t);
            h.Direction=math.normalizesafe(new float3(player.x-position.x,0,player.z-position.z),new float3(0,0,-1));
            float distance=math.min(a.Reach,math.distance(position.xz,player.xz));
            h.Start=position; h.Start.y=t.HandHeight;
            h.Target=BossPerimeterRoute.Clamp(h.Start+h.Direction*distance,t); h.Target.y=t.HandHeight;
            // Keep committed paths on the arena-facing side of the head, including a player behind it.
            float depth=math.dot(h.Target-head,front);
            h.Target+=front*math.max(0,5-depth);
            h.Target=BossPerimeterRoute.Clamp(h.Target,t);
            if(h.Attack==BossAttack.Sweep) h.Target=BossPerimeterRoute.Clamp(h.Target,t,a.Width+1.5f);
            h.Direction=math.normalizesafe(new float3(h.Target.x-position.x,0,h.Target.z-position.z),front);
            h.Phase=BossHandPhase.Anticipation; h.Duration=h.Remaining=a.Anticipation*factor;
            h.AttackSequence++; h.PlayerHit=0;
        }

        private static void UpdateTarget(ref BossHand h,float3 position,BossEncounter boss,float3 head,float3 forward,float3 side,
            int sign,in BossTuning t,float factor,EntityManager em,Entity entity,quaternion rotation)
        {
            var a=AttackTuning(h.Attack,t);
            if(h.Phase==BossHandPhase.Anticipation && h.Remaining<=0)
            { h.Phase=BossHandPhase.Active; h.Duration=h.Remaining=a.Active*factor; h.PreviousPosition=position; }
            else if(h.Phase==BossHandPhase.Active && h.Remaining<=0
                && math.distancesq(position,BossPerimeterRoute.Clamp(StrikeEnd(h,a),t))<.09f)
            { h.Phase=BossHandPhase.Recovery; h.Duration=h.Remaining=a.Recovery; }
            float3 target=position;
            float3 attackSide=math.cross(math.up(),h.Direction);
            if(h.Phase==BossHandPhase.Anticipation)
            {
                if(h.Attack==BossAttack.Slam) target=h.Target+math.up()*6;
                else if(h.Attack==BossAttack.Sweep) target=h.Target-attackSide*a.Width;
                else target=h.Start-h.Direction*1.5f;
            }
            else if(h.Phase==BossHandPhase.Active)
            {
                float progress=1-math.saturate(h.Remaining/h.Duration);
                if(h.Attack==BossAttack.Slam) target=h.Target+math.up()*6*(1-progress);
                else if(h.Attack==BossAttack.Lunge) target=math.lerp(h.Start-h.Direction*1.5f,h.Target,progress);
                else target=h.Target+attackSide*math.lerp(-a.Width,a.Width,progress)-h.Direction*(math.sin(progress*math.PI)*2);
            }
            else if(h.Phase==BossHandPhase.Recovery || h.Phase==BossHandPhase.Staggered)
            {
                // Retract outward during recovery; the head stays stationary and another hand can commit safely.
                target=h.Phase==BossHandPhase.Staggered?position:head+side*t.OpenOffset*sign+forward;
                if(h.Phase==BossHandPhase.Recovery) target.y=t.HandHeight;
            }
            else
            {
                bool shield=boss.Stage>=2 && boss.Cycle==BossCycle.Attacking
                    && (boss.NextHand==0 ? sign>0 : sign<0);
                if(boss.Stage==3 && boss.Cycle==BossCycle.Relocating) shield=true;
                target=head+side*(shield?t.ShieldOffset:t.OpenOffset)*sign+forward*(shield?t.ShieldForward:1);
                target.y=t.HandHeight;
                target=BossPerimeterRoute.Clamp(target,t);
                if(math.distancesq(position,target)<.16f) h.Phase=shield?BossHandPhase.Shielding:BossHandPhase.Ready;
                else h.Phase=BossHandPhase.Returning;
            }
            target=BossPerimeterRoute.Clamp(target,t);
            em.SetComponentData(entity,new BossMotionTarget { Position=target, Rotation=rotation });
        }
        private static void Hold(EntityManager em,Entity e,float3 p,quaternion q) => em.SetComponentData(e,new BossMotionTarget { Position=p,Rotation=q });
        private static float3 StrikeEnd(BossHand h,BossAttackTuning a) => h.Attack==BossAttack.Sweep
            ? h.Target+math.cross(math.up(),h.Direction)*a.Width : h.Target;
    }
}
