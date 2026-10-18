using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    // BARRICADE-004: presentation only; the shared gate collider and hit rules stay authoritative.
    [BurstCompile]
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct VillageGatePresentationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double now=SystemAPI.Time.ElapsedTime;
            foreach(var (piece,visual,transform,color) in SystemAPI.Query<RefRW<VillageGatePiece>,
                RefRO<BarricadeVisual>,RefRW<LocalTransform>,RefRW<URPMaterialPropertyBaseColor>>())
            {
                if(!SystemAPI.HasComponent<Barricade>(piece.ValueRO.Gate)) continue;
                var wall=SystemAPI.GetComponent<Barricade>(piece.ValueRO.Gate);
                if(piece.ValueRO.Initialized==0)
                {
                    piece.ValueRW.Position=transform.ValueRO.Position;
                    piece.ValueRW.Rotation=transform.ValueRO.Rotation;
                    piece.ValueRW.Scale=transform.ValueRO.Scale;
                    piece.ValueRW.Initialized=1;
                }
                float t=math.max(0,(float)(now-wall.LastHitTime));
                if(piece.ValueRO.LastSequence==wall.HitSequence && t>piece.ValueRO.Duration && piece.ValueRO.Settled!=0) continue;
                piece.ValueRW.LastSequence=wall.HitSequence;
                piece.ValueRW.Settled=t>=piece.ValueRO.Duration?(byte)1:(byte)0;
                var p=piece.ValueRO;
                int damage=wall.RequiredHits-wall.HitsRemaining;
                bool broken=wall.HitsRemaining==0;
                float3 position=p.Position;
                quaternion rotation=p.Rotation;
                float scale=p.Scale;
                var gate=SystemAPI.GetComponent<LocalTransform>(p.Gate);
                float3 hit=math.rotate(math.inverse(gate.Rotation),wall.LastHitPosition-gate.Position)/gate.Scale;
                hit=math.clamp(hit,-wall.Size*.5f,wall.Size*.5f);
                float direction=hit.z>0?-1:1;
                float flash=0;
                if(p.Kind>=2)
                {
                    float lifetime=p.Kind==3?.6f:.65f;
                    float u=math.saturate(t/lifetime);
                    scale*=wall.HitSequence==0?0:1-math.smoothstep(.65f,1,u);
                    float3 origin=broken?new float3(p.Position.x,hit.y,hit.z):hit+p.Position*.12f;
                    // On intact gates the debris must leave the struck face, rather
                    // than disappearing inside the still-solid timber.
                    if(!broken) origin.z=-direction*(wall.Size.z*.5f+.12f);
                    position=origin+p.Velocity*new float3(1,1,broken?direction:-direction)*t+new float3(0,-5*t*t,0);
                    position.y=math.max(-wall.Size.y*.5f+p.RestingHeight,position.y);
                    rotation=math.mul(p.Rotation,quaternion.EulerXYZ(p.Spin*t));
                    if(p.Kind==3) scale*=math.lerp(.4f,2.3f,u);
                }
                else if(broken)
                {
                    float u=math.saturate(t/p.Duration);
                    position+=p.Velocity*new float3(1,1,direction)*t+new float3(0,-7*t*t,0);
                    position.y=math.max(-wall.Size.y*.5f+p.RestingHeight,position.y);
                    var tumble=math.mul(p.Rotation,quaternion.EulerXYZ(p.Spin*t));
                    var landed=quaternion.EulerXYZ(new float3(math.PI*.5f,p.Seed*math.PI*2,0));
                    rotation=math.slerp(tumble,landed,math.smoothstep(.25f,.85f,u));
                    scale*=1-math.smoothstep(.72f,1,u);
                    if(p.Kind==1) scale=0;
                    flash=math.saturate(1-t/.09f)*.5f;
                }
                else
                {
                    if(p.Kind==1 && damage<p.DamageStage) scale=0;
                    float proximity=math.saturate(1-math.abs(p.Position.x-hit.x)/5);
                    float pulse=wall.HitSequence==0?0:math.sin(t*46)*math.exp(-t*13);
                    float sign=p.Seed>.5f?1:-1;
                    position+=new float3(sign*.025f*damage,-.025f*damage,pulse*(.06f+.15f*proximity));
                    rotation=math.mul(p.Rotation,quaternion.EulerXYZ(new float3(pulse*.07f,0,sign*damage*.018f)));
                    flash=wall.HitSequence==0?0:math.saturate(1-t/.13f)*(.25f+.5f*proximity);
                }
                transform.ValueRW.Position=position;
                transform.ValueRW.Rotation=rotation;
                transform.ValueRW.Scale=scale;
                color.ValueRW.Value=math.lerp(visual.ValueRO.Color,new float4(1,.91f,.63f,1),flash);
            }
        }
    }
}
