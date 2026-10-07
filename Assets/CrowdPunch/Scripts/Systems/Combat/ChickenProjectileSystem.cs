using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    // Bounded swept segments preserve pass-through contacts, including multiple wall bounces in one step.
    [UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(ExplosionResolutionSystem))]
    [UpdateBefore(typeof(ChickenDamageSystem)), UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct ChickenProjectileSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em=state.EntityManager; float dt=SystemAPI.Time.DeltaTime;
            var player=SystemAPI.HasSingleton<PlayerSnapshot>()?SystemAPI.GetSingleton<PlayerSnapshot>():default;
            var commands=new EntityCommandBuffer(Allocator.Temp);
            float homing=SystemAPI.HasSingleton<EnemyLaunchSettings>()?SystemAPI.GetSingleton<EnemyLaunchSettings>().LaunchHomingDegreesPerSecond:0;
            foreach(var (projectile,pose,history,entity) in SystemAPI.Query<RefRW<ChickenProjectile>,RefRW<LocalTransform>,DynamicBuffer<ChickenProjectileHit>>().WithEntityAccess())
            {
                ref var p=ref projectile.ValueRW;
                if(!em.HasComponent<ChickenBoss>(p.Boss) || em.GetComponentData<ChickenBoss>(p.Boss).Phase==ChickenPhase.Defeated)
                { commands.DestroyEntity(entity); continue; }
                var t=em.GetComponentData<ChickenTuning>(p.Boss);
                if(p.Redirected!=0 && PunchAimAssist.IsValidTarget(em,entity,p.HomingTarget))
                    p.Velocity=EnemyLaunchHoming.RotateHorizontalVelocity(p.Velocity,
                        em.GetComponentData<LocalTransform>(p.HomingTarget).Position-pose.ValueRO.Position,math.radians(homing)*dt);
                float remaining=math.min(dt,math.max(0,t.Lifetime-p.Age)); p.Age+=dt;
                bool destroy=false; float3 position=pose.ValueRO.Position;
                for(int segment=0;segment<=t.BounceLimit && remaining>.000001f;segment++)
                {
                    float fraction=WallFraction(position,p.Velocity*remaining,t,out float3 normal);
                    float3 end=position+p.Velocity*remaining*fraction;
                    float stop=1; bool playerHit=false,bossHit=false;
                    if(player.IsAvailable && Sweep(position,end,player.Position,t.ProjectileRadius+player.Radius,out float playerAt))
                    { stop=playerAt; playerHit=true; }
                    var bossPose=em.GetComponentData<LocalTransform>(p.Boss).Position;
                    if(p.Redirected!=0 && Sweep(position,end,bossPose,t.ProjectileRadius+t.BodyRadius,out float bossAt) && bossAt<stop)
                    { stop=bossAt; playerHit=false; bossHit=true; }
                    end=math.lerp(position,end,stop);
                    foreach(var (enemyPose,launch,tier,contact,enemy) in
                        SystemAPI.Query<RefRO<LocalTransform>,RefRO<EnemyLaunchState>,RefRO<EnemyTier>,RefRO<EnemyContactDamageSettings>>()
                            .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
                    {
                        if(tier.ValueRO.Value!=EnemyCombatTier.Normal || launch.ValueRO.Phase==EnemyLaunchPhase.Defeated) continue;
                        // Projectile height is fixed. Vertical separation can make an airborne body miss.
                        if(math.abs(enemyPose.ValueRO.Position.y-position.y)>t.ProjectileRadius+contact.ValueRO.ContactRadius) continue;
                        if(!Sweep(position,end,enemyPose.ValueRO.Position,t.ProjectileRadius+contact.ValueRO.ContactRadius,out _)) continue;
                        uint lifetime=em.HasComponent<EnemyLifetime>(enemy)?em.GetComponentData<EnemyLifetime>(enemy).Generation:0;
                        bool seen=false; foreach(var hit in history) if(hit.Target==enemy && hit.Lifetime==lifetime) { seen=true; break; }
                        if(seen) continue;
                        history.Add(new ChickenProjectileHit { Target=enemy,Lifetime=lifetime });
                        HitEnemy(em,enemy,entity,p,t,SystemAPI.Time.ElapsedTime);
                    }
                    position=end;
                    if(playerHit)
                    {
                        if(PlayerBridgeRegistry.TryGetBridge(out var bridge)) bridge.ReceiveEnemyHit(t.ProjectileDamage,t.PlayerProtection,math.normalizesafe(p.Velocity)*t.PlayerPush);
                        destroy=true; break; // Consumed even when player invulnerability rejects the hit.
                    }
                    if(bossHit)
                    { ChickenDamageResolution.Queue(em,p.Boss,entity,p.Launch,t.ReturnedBossDamage); destroy=true; break; }
                    if(fraction>=1) break;
                    remaining*=1-fraction;
                    Reflect(ref p,normal); position+=normal*.001f;
                    if(p.Bounces>=t.BounceLimit) { destroy=true; break; }
                }
                pose.ValueRW.Position=position;
                if(destroy || p.Age>=t.Lifetime) commands.DestroyEntity(entity);
            }
            commands.Playback(em); commands.Dispose();
        }

        internal static void Reflect(ref ChickenProjectile p,float3 normal)
        { p.Velocity=math.reflect(p.Velocity,normal); p.Bounces++; p.HomingTarget=Entity.Null; }

        internal static float WallFraction(float3 start,float3 displacement,in ChickenTuning t,out float3 normal)
        {
            float2 limit=math.max(new float2(.01f),t.ArenaHalfSize-t.ProjectileRadius);
            float2 min=t.ArenaCenter-limit,max=t.ArenaCenter+limit;
            float fraction=1; normal=float3.zero;
            if(math.abs(displacement.x)>.00001f)
            {
                float f=((displacement.x>0?max.x:min.x)-start.x)/displacement.x;
                if(f>=0 && f<fraction) { fraction=f; normal=new float3(displacement.x>0?-1:1,0,0); }
            }
            if(math.abs(displacement.z)>.00001f)
            {
                float f=((displacement.z>0?max.y:min.y)-start.z)/displacement.z;
                if(f>=0 && f<fraction) { fraction=f; normal=new float3(0,0,displacement.z>0?-1:1); }
            }
            return fraction;
        }
        internal static bool Sweep(float3 start,float3 end,float3 center,float radius,out float fraction)
        {
            float2 delta=end.xz-start.xz, offset=start.xz-center.xz;
            float c=math.lengthsq(offset)-radius*radius; fraction=0;
            if(c<=0) return true;
            float a=math.lengthsq(delta), b=math.dot(offset,delta);
            float disc=b*b-a*c;
            if(a<.000001f || disc<0) return false;
            fraction=(-b-math.sqrt(disc))/a;
            return fraction>=0 && fraction<=1;
        }
        private static void HitEnemy(EntityManager em,Entity enemy,Entity source,in ChickenProjectile p,in ChickenTuning t,double now)
        {
            var outcome=ArmorHitResolution.Resolve(em,enemy,source,p.Launch,now,t.ProjectileDamage);
            if(outcome==ArmorHitOutcome.Blocked) return;
            var velocity=em.GetComponentData<PhysicsVelocity>(enemy);
            float3 direction=math.normalizesafe(p.Velocity,new float3(0,0,1));
            if(outcome==ArmorHitOutcome.Absorbed)
            { velocity.Linear.xz=direction.xz*em.GetComponentData<EnemyArmorSettings>(enemy).KnockbackSpeed; em.SetComponentData(enemy,velocity); return; }
            var launch=em.GetComponentData<EnemyLaunchState>(enemy);
            var owner=p.Redirected!=0?EnemyLaunchOwner.Player:EnemyLaunchOwner.Boss;
            EnemyLaunchTransition.Begin(ref launch,EnemyLaunchCause.BossAttack,t.ProjectileDamage,owner);
            em.SetComponentData(enemy,launch);
            velocity.Linear=direction*t.ProjectileLaunchSpeed; velocity.Angular=0; em.SetComponentData(enemy,velocity);
            if(em.HasComponent<DasherState>(enemy))
            { var dash=em.GetComponentData<DasherState>(enemy); dash.Phase=DasherPhase.Positioning; dash.SecondsRemaining=0; em.SetComponentData(enemy,dash); }
            EnemyDamageResolution.ApplyPending(em,enemy,now);
            em.SetComponentData(enemy,new DamageRequest { Amount=t.ProjectileDamage }); em.SetComponentEnabled<DamageRequest>(enemy,true);
            EnemyDamageResolution.ApplyPending(em,enemy,now,owner,source,owner==EnemyLaunchOwner.Player?1:0);
        }
    }
}
