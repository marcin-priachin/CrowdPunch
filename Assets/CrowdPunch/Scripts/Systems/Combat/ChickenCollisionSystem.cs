using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(EnemyLaunchCollisionSystem)), UpdateBefore(typeof(ExplosiveCollisionTriggerSystem))]
    [UpdateBefore(typeof(ExplosionResolutionSystem))]
    public partial struct ChickenCollisionSystem : ISystem
    {
        private struct Contact { public Entity Boss,Body; public float Impulse; }
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<ChickenBoss>(); state.RequireForUpdate<SimulationSingleton>(); state.RequireForUpdate<EnemyLaunchSettings>(); }
        public void OnUpdate(ref SystemState state)
        {
            var contacts=new NativeList<Contact>(Allocator.TempJob);
            state.Dependency=new Collect { Bosses=SystemAPI.GetComponentLookup<ChickenBoss>(true),
                Enemies=SystemAPI.GetComponentLookup<Enemy>(true), World=SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                Contacts=contacts }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(),state.Dependency);
            state.Dependency.Complete();
            var em=state.EntityManager; var settings=SystemAPI.GetSingleton<EnemyLaunchSettings>();
            foreach(var c in contacts)
            {
                if(!em.HasComponent<EnemyLaunchState>(c.Body) || em.IsComponentEnabled<RespawnRequest>(c.Body)) continue;
                var launch=em.GetComponentData<EnemyLaunchState>(c.Body);
                if(launch.Phase!=EnemyLaunchPhase.Launched) continue;
                ChickenDamageResolution.QueueBody(em,c.Boss,c.Body,c.Impulse,settings);
                // A boss-owned Exploder body is ineligible, but its ensuing blast remains eligible.
                BossCollisionSystem.RequestExplosiveContact(em,c.Body);
            }
            contacts.Dispose();
        }
        private struct Collect : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<ChickenBoss> Bosses;
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
