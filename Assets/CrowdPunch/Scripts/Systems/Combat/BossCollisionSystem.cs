using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(EnemyLaunchCollisionSystem)), UpdateBefore(typeof(DasherEnemyImpactSystem))]
    [UpdateBefore(typeof(ExplosiveCollisionTriggerSystem))]
    public partial struct BossCollisionSystem : ISystem
    {
        private struct Contact { public Entity Part, Body; public float Impulse; public float3 Point, Normal; public byte ActiveHand; }
        public void OnCreate(ref SystemState state)
        { state.RequireForUpdate<BossEncounter>(); state.RequireForUpdate<SimulationSingleton>(); state.RequireForUpdate<EnemyLaunchSettings>(); }

        public void OnUpdate(ref SystemState state)
        {
            var contacts=new NativeList<Contact>(Allocator.TempJob);
            var job=new Collect { Parts=SystemAPI.GetComponentLookup<BossPart>(true),
                Hands=SystemAPI.GetComponentLookup<BossHand>(true), Enemies=SystemAPI.GetComponentLookup<Enemy>(true),
                World=SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld, Contacts=contacts };
            state.Dependency=job.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(),state.Dependency); state.Dependency.Complete();
            var em=state.EntityManager; double now=SystemAPI.Time.ElapsedTime;
            var settings=SystemAPI.GetSingleton<EnemyLaunchSettings>();
            // Resolve incoming stagger before an active strike deliberately takes ownership.
            foreach(var c in contacts) if(em.GetComponentData<BossPart>(c.Part).Kind!=BossPartKind.Head) Hit(em,c,settings,now);
            foreach(var c in contacts) if(c.ActiveHand!=0) Scatter(em,c);
            // Every boss-caused ownership change is visible before head damage or ordinary propagation.
            foreach(var c in contacts) if(em.GetComponentData<BossPart>(c.Part).Kind==BossPartKind.Head) Hit(em,c,settings,now);
            contacts.Dispose();
        }

        private static void Hit(EntityManager em,Contact c,EnemyLaunchSettings settings,double now)
        {
            if(!em.HasComponent<EnemyLaunchState>(c.Body)) return;
            if(em.HasComponent<DasherSettings>(c.Body))
            {
                DasherEnemyImpactSystem.ResolveBossContact(em,c.Body,c.Part,c.Impulse,settings.MinimumDamageImpulse,now,c.Point,c.Normal);
                return;
            }
            float damage=EnemyCollisionDamage.Calculate(em.GetComponentData<EnemyLaunchState>(c.Body).LaunchDamage,c.Impulse,settings);
            BossImpactResolution.TryHit(em,c.Body,c.Part,c.Impulse,damage,now,c.Point,c.Normal);
        }

        private static void Scatter(EntityManager em,Contact c)
        {
            var part=em.GetComponentData<BossPart>(c.Part);
            var boss=em.GetComponentData<BossEncounter>(part.Encounter);
            if(boss.Cycle==BossCycle.Defeated || boss.Cycle==BossCycle.Transition
                || !em.HasComponent<EnemyLaunchState>(c.Body) || !em.HasComponent<EnemyTier>(c.Body)
                || !EnemyLaunchTransition.IsLaunchable(em.GetComponentData<EnemyTier>(c.Body))
                || em.IsComponentEnabled<RespawnRequest>(c.Body)) return;
            var launch=em.GetComponentData<EnemyLaunchState>(c.Body);
            if(launch.Phase==EnemyLaunchPhase.Defeated) return;
            var hand=em.GetComponentData<BossHand>(c.Part);
            var history=em.GetBuffer<BossScatterHistory>(c.Part);
            for(int i=0;i<history.Length;i++) if(history[i].Body==c.Body && history[i].AttackSequence==hand.AttackSequence) return;
            history.Add(new BossScatterHistory { Body=c.Body, AttackSequence=hand.AttackSequence });
            var t=em.GetComponentData<BossTuning>(part.Encounter);
            // Active interception starts a new boss launch even if this body was already player-owned.
            EnemyLaunchTransition.Begin(ref launch,EnemyLaunchCause.BossAttack,t.ScatterDamage);
            em.SetComponentData(c.Body,launch);
            var velocity=em.GetComponentData<PhysicsVelocity>(c.Body);
            float3 direction=em.GetComponentData<LocalTransform>(c.Body).Position-em.GetComponentData<LocalTransform>(c.Part).Position;
            direction.y=0; direction=math.normalizesafe(direction,hand.Direction);
            velocity.Linear.xz=direction.xz*math.max(t.ScatterSpeed,math.length(velocity.Linear.xz));
            velocity.Angular=0; em.SetComponentData(c.Body,velocity);
            if(em.HasComponent<DasherState>(c.Body))
            {
                var dash=em.GetComponentData<DasherState>(c.Body);
                dash.Phase=DasherPhase.Positioning; dash.PreservedLaunchedVelocity=velocity.Linear;
                em.SetComponentData(c.Body,dash);
            }
        }

        private struct Collect : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<BossPart> Parts;
            [ReadOnly] public ComponentLookup<BossHand> Hands;
            [ReadOnly] public ComponentLookup<Enemy> Enemies;
            [ReadOnly] public PhysicsWorld World;
            public NativeList<Contact> Contacts;
            public void Execute(CollisionEvent e)
            {
                Entity p=Parts.HasComponent(e.EntityA)?e.EntityA:e.EntityB;
                Entity b=p==e.EntityA?e.EntityB:e.EntityA;
                if(!Parts.HasComponent(p)||!Enemies.HasComponent(b)) return;
                var d=e.CalculateDetails(ref World);
                Contacts.Add(new Contact { Part=p, Body=b, Impulse=math.max(0,d.EstimatedImpulse), Point=d.AverageContactPointPosition,
                    Normal=p==e.EntityA?-e.Normal:e.Normal,
                    ActiveHand=(byte)(Hands.HasComponent(p)&&Hands[p].Phase==BossHandPhase.Active?1:0) });
            }
        }
    }
}
