using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Initialization;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
namespace CrowdPunch.Systems.AI
{
    [BurstCompile,UpdateInGroup(typeof(GamePrePhysicsGroup)),UpdateAfter(typeof(EnemyChaseSystem))]
    [UpdateBefore(typeof(EnemyNavigationSystem))]
    public partial struct PillarCrowdPositioningSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            foreach(var (member,pose,launch,movement,desired,intent) in SystemAPI.Query<RefRO<PillarCrowdMember>,RefRO<LocalTransform>,RefRO<EnemyLaunchState>,
                RefRO<EnemyMovementSettings>,RefRW<DesiredMovement>,RefRW<NavigationIntent>>().WithNone<RespawnRequest>())
            {
                if(launch.ValueRO.Phase!=EnemyLaunchPhase.Active || !SystemAPI.HasComponent<FallingPillar>(member.ValueRO.Pillar)) continue;
                var pillar=SystemAPI.GetComponent<FallingPillar>(member.ValueRO.Pillar);
                if(!SystemAPI.HasComponent<DinoBoss>(pillar.Boss)) continue;
                var boss=SystemAPI.GetComponent<DinoBoss>(pillar.Boss); var tuning=SystemAPI.GetComponent<DinoTuning>(pillar.Boss);
                if(!PillarCrowdPlacement.Needed(pillar,boss,tuning)) continue;
                var pt=SystemAPI.GetComponent<PillarTuning>(member.ValueRO.Pillar);
                var previous=intent.ValueRO;
                float3 target=Destination(pt,tuning,pose.ValueRO.Position,previous.Destination,pillar.Phase==PillarPhase.Upright);
                bool outside=math.distancesq(pose.ValueRO.Position.xz,pt.InitialPosition.xz)>math.square(tuning.PillarCrowdRadius);
                bool redirected=outside || math.distancesq(target.xz,previous.Destination.xz)>.0001f;
                if(!redirected) continue; // Normal chase, speed, separation and contact wind-up stay authoritative.
                float3 offset=target-pose.ValueRO.Position; offset.y=0;
                previous.Destination=target;
                if(outside) { previous.Speed=movement.ValueRO.MoveSpeed; previous.ArrivalDistance=.25f; }
                // A detour/return uses navigation rather than a committed straight dash through the shaft or boundary.
                previous.Mode=NavigationMode.Travel;
                intent.ValueRW=previous;
                desired.ValueRW=new DesiredMovement { Direction=math.normalizesafe(math.normalizesafe(offset)+previous.Separation),
                    Speed=math.lengthsq(offset)>math.square(previous.ArrivalDistance)?previous.Speed:0 };
            }
        }
        internal static float3 Destination(in PillarTuning pillar,in DinoTuning boss,float3 position,float3 desired,bool solid)
        {
            float radius=boss.PillarCrowdRadius;
            float3 target=desired; target.y=position.y;
            float2 offset=target.xz-pillar.InitialPosition.xz;
            target.xz=pillar.InitialPosition.xz+math.normalizesafe(offset)*math.min(radius,math.length(offset));
            float2 current=position.xz-pillar.InitialPosition.xz,goal=target.xz-pillar.InitialPosition.xz;
            if(math.lengthsq(current)>radius*radius)
                return new float3(pillar.InitialPosition.x+math.normalizesafe(current).x*radius*.9f,position.y,
                    pillar.InitialPosition.z+math.normalizesafe(current).y*radius*.9f);
            if(!solid) return target;
            float clearance=math.min(radius,pillar.Width*.71f+.8f);
            if(math.lengthsq(goal)<clearance*clearance)
            { goal=math.normalizesafe(goal,math.normalizesafe(current,new float2(1,0)))*clearance; target.xz=pillar.InitialPosition.xz+goal; }
            float angle=math.atan2(current.x*goal.y-current.y*goal.x,math.dot(current,goal));
            float2 delta=goal-current;
            float fraction=math.saturate(-math.dot(current,delta)/math.max(.0001f,math.lengthsq(delta)));
            if(math.lengthsq(current+delta*fraction)<clearance*clearance && math.abs(angle)>math.radians(35))
            {
                float turn=math.clamp(angle,-math.radians(30),math.radians(30));
                var dir=math.normalizesafe(current,new float2(1,0));
                float detourRadius=math.min(radius,math.max(clearance+.25f,math.length(current)));
                target.xz=pillar.InitialPosition.xz+new float2(dir.x*math.cos(turn)-dir.y*math.sin(turn),dir.x*math.sin(turn)+dir.y*math.cos(turn))*detourRadius;
            }
            return target;
        }
    }
}
