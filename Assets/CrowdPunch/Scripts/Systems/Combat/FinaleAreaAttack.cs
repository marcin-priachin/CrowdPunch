using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    internal static class FinaleAreaAttack
    {
        public static bool Contains(in FinaleBoss b, in FinaleTuning t, float3 p, float radius)
        {
            float2 d=p.xz-b.WarningCenter.xz;
            if (b.Attack==FinaleAttack.Sweep || b.Attack==FinaleAttack.Slam) {
                float length=b.Attack==FinaleAttack.Sweep?t.SweepLength:t.SlamLength;
                float width=b.Attack==FinaleAttack.Sweep?t.SweepWidth:t.SlamWidth;
                float along=math.dot(d,b.WarningDirection.xz);
                return along >= (b.Attack==FinaleAttack.Sweep?-length*.5f:0)-radius
                    && along <= (b.Attack==FinaleAttack.Sweep?length*.5f:length)+radius
                    && math.abs(math.dot(d,new float2(-b.WarningDirection.z,b.WarningDirection.x)))<=width*.5f+radius;
            }
            float reach=b.Attack==FinaleAttack.Landing?t.LandingRadius:b.Attack==FinaleAttack.Shockwave?t.ShockwaveRadius:t.StrikeRadius;
            return math.lengthsq(d)<=math.square(reach+radius);
        }

        public static void Resolve(EntityManager em, Entity boss, in FinaleBoss b, in FinaleTuning t, in PlayerSnapshot player)
        {
            if (player.IsAvailable && Contains(b,t,player.Position,player.Radius) && PlayerBridgeRegistry.TryGetBridge(out var bridge))
                bridge.ReceiveEnemyHit(t.PlayerDamage,.6f,Direction(b,player.Position)*t.PushSpeed);
            using var query=em.CreateEntityQuery(typeof(Enemy),typeof(EnemyLaunchState),typeof(LocalTransform),typeof(PhysicsVelocity));
            using var enemies=query.ToEntityArray(Allocator.Temp);
            using var pairs=new NativeHashSet<Entity>(math.max(1,enemies.Length),Allocator.Temp);
            foreach (var e in enemies) {
                var launch=em.GetComponentData<EnemyLaunchState>(e);
                var p=em.GetComponentData<LocalTransform>(e).Position;
                if (launch.Phase==EnemyLaunchPhase.Defeated || em.IsComponentEnabled<RespawnRequest>(e) || !Contains(b,t,p,.6f)) continue;
                // Attached snake bodies and protected armor retain their specialized launch rules.
                if (SnakeHitRules.Attached(em,e)) continue;
                var armor=ArmorHitResolution.Resolve(em,e,boss,b.AttackSequence,em.World.Time.ElapsedTime,t.EnemyDamage);
                if (armor==ArmorHitOutcome.Blocked) continue;
                if (armor==ArmorHitOutcome.Ordinary && em.HasComponent<DamageRequest>(e)) {
                    var damage=em.GetComponentData<DamageRequest>(e); damage.Amount+=t.EnemyDamage;
                    em.SetComponentData(e,damage); em.SetComponentEnabled<DamageRequest>(e,true);
                }
                float3 velocity=Direction(b,p)*t.PushSpeed;
                bool normal=em.GetComponentData<EnemyTier>(e).Value==EnemyCombatTier.Normal;
                if (normal && armor==ArmorHitOutcome.Ordinary && !ArmorHitResolution.IsProtected(em,e)) {
                    EnemyLaunchTransition.Begin(ref launch,EnemyLaunchCause.BossAttack,t.EnemyDamage);
                    if (LinkedPairLaunchResolution.TryGet(em,e,out var pair,out _)) {
                        if (!pairs.Add(pair)) continue;
                        LinkedPairLaunchResolution.Begin(em,e,launch,velocity); continue;
                    }
                    em.SetComponentData(e,launch);
                }
                var v=em.GetComponentData<PhysicsVelocity>(e); v.Linear.xz=velocity.xz; em.SetComponentData(e,v);
            }
        }
        private static float3 Direction(in FinaleBoss b,float3 p) =>
            math.normalizesafe(new float3(p.x-b.WarningCenter.x,0,p.z-b.WarningCenter.z),b.WarningDirection+new float3(0,0,.001f));
    }
}
