using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(EnemyLaunchCollisionSystem)), UpdateBefore(typeof(ExplosiveCollisionTriggerSystem))]
    [UpdateBefore(typeof(ExplosionResolutionSystem)), UpdateBefore(typeof(Physics.EnemyRecoverySystem))]
    public partial struct RollingCollisionSystem : ISystem
    {
        private struct Contact { public Entity Boss,Body; public float Impulse; }
        public void OnCreate(ref SystemState state)
        { state.RequireForUpdate<RollingBoss>(); state.RequireForUpdate<SimulationSingleton>(); state.RequireForUpdate<EnemyLaunchSettings>(); }

        public void OnUpdate(ref SystemState state)
        {
            using var contacts=new NativeList<Contact>(Allocator.TempJob);
            state.Dependency=new Collect { Bosses=SystemAPI.GetComponentLookup<RollingBoss>(true),
                Enemies=SystemAPI.GetComponentLookup<Enemy>(true),World=SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                Contacts=contacts }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(),state.Dependency);
            state.Dependency.Complete();
            var em=state.EntityManager; var settings=SystemAPI.GetSingleton<EnemyLaunchSettings>();
            double now=SystemAPI.Time.ElapsedTime;
            foreach(var c in contacts)
            {
                if(em.IsComponentEnabled<RespawnRequest>(c.Body)) continue;
                var b=em.GetComponentData<RollingBoss>(c.Boss);
                var launch=em.GetComponentData<EnemyLaunchState>(c.Body);
                if(b.Phase==RollingPhase.Defeated || launch.Phase==EnemyLaunchPhase.Defeated) continue;
                RollingDamageResolution.QueueBody(em,c.Boss,c.Body,c.Impulse,settings);
                if(launch.Phase==EnemyLaunchPhase.Launched) BossCollisionSystem.RequestExplosiveContact(em,c.Body);
                if(b.Phase!=RollingPhase.Roll) continue;
                var history=em.GetBuffer<RollingCrowdContact>(c.Boss);
                uint lifetime=em.GetComponentData<EnemyLifetime>(c.Body).Generation;
                if(RegisterContact(em,history,c.Body,lifetime,b.RollSequence,now))
                    HitCrowd(em,c.Boss,c.Body,b,em.GetComponentData<RollingTuning>(c.Boss),now);
            }
        }

        internal static bool RegisterContact(EntityManager em,DynamicBuffer<RollingCrowdContact> history,Entity body,uint lifetime,uint roll,double now)
        {
            // Prune first: removing an earlier entry must not invalidate a matched contact index.
            for(int i=history.Length-1;i>=0;i--)
                if(now-history[i].LastSeen>.1 || !em.Exists(history[i].Body)) history.RemoveAt(i);
            var entry=new RollingCrowdContact { Body=body,Lifetime=lifetime,Roll=roll,LastSeen=now };
            for(int i=0;i<history.Length;i++)
                if(history[i].Body==body && history[i].Lifetime==lifetime && history[i].Roll==roll)
                { history[i]=entry; return false; }
            history.Add(entry); return true;
        }

        internal static void HitCrowd(EntityManager em,Entity boss,Entity enemy,in RollingBoss b,in RollingTuning t,double now)
        {
            var launch=em.GetComponentData<EnemyLaunchState>(enemy);
            var velocity=em.GetComponentData<PhysicsVelocity>(enemy);
            float3 away=em.GetComponentData<LocalTransform>(enemy).Position-em.GetComponentData<LocalTransform>(boss).Position;
            away.y=0;
            float3 direction=t.CrowdDirection==RollingCrowdDirection.RollDirection?b.Direction:math.normalizesafe(away,b.Direction);
            bool armored=false;
            if(em.HasComponent<EnemyArmor>(enemy))
            { var armor=em.GetComponentData<EnemyArmor>(enemy); armored=armor.Stages>0 || now<armor.ProtectedUntil; }
            bool launchable=EnemyLaunchTransition.IsLaunchable(em.GetComponentData<EnemyTier>(enemy)) && !armored;
            if(launchable)
            {
                EnemyLaunchTransition.Begin(ref launch,EnemyLaunchCause.BossAttack,t.CrowdDamage,EnemyLaunchOwner.Boss);
                em.SetComponentData(enemy,launch);
                em.SetComponentEnabled<ExternalImpulse>(enemy,false);
                em.SetComponentEnabled<KnockbackRecovery>(enemy,false);
                velocity.Linear=direction*t.LaunchSpeed; velocity.Angular=float3.zero;
            }
            else velocity.Linear.xz=direction.xz*t.ResistantPush;
            em.SetComponentData(enemy,velocity);
            if(armored) return; // Ordinary rolling must never spend shield stages or protection.
            EnemyDamageResolution.ApplyPending(em,enemy,now);
            em.SetComponentData(enemy,new DamageRequest { Amount=t.CrowdDamage });
            em.SetComponentEnabled<DamageRequest>(enemy,true);
            EnemyDamageResolution.ApplyPending(em,enemy,now,EnemyLaunchOwner.Boss,boss);
        }

        [BurstCompile]
        private struct Collect : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<RollingBoss> Bosses;
            [ReadOnly] public ComponentLookup<Enemy> Enemies;
            [ReadOnly] public PhysicsWorld World;
            public NativeList<Contact> Contacts;
            public void Execute(CollisionEvent e)
            {
                Entity boss=Bosses.HasComponent(e.EntityA)?e.EntityA:e.EntityB;
                Entity body=boss==e.EntityA?e.EntityB:e.EntityA;
                if(!Bosses.HasComponent(boss)||!Enemies.HasComponent(body)) return;
                var details=e.CalculateDetails(ref World);
                Contacts.Add(new Contact { Boss=boss,Body=body,Impulse=math.max(0,details.EstimatedImpulse) });
            }
        }
    }
}
