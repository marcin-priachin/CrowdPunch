using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    // Observe qualifying contacts before ordinary collision damage changes target life state.
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(EnemyLaunchCollisionSystem))]
    [UpdateBefore(typeof(ExplosiveCollisionTriggerSystem))]
    public partial struct WizardImpactZoneSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<SimulationSingleton>(); state.RequireForUpdate<WizardSettings>(); }

        public void OnUpdate(ref SystemState state)
        {
            using var hits = new NativeList<Entity>(Allocator.TempJob);
            state.Dependency = new Contacts
            {
                Wizards = SystemAPI.GetComponentLookup<WizardSettings>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                BossParts = SystemAPI.GetComponentLookup<BossPart>(true),
                Respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                Hits = hits
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
            state.Dependency.Complete();
            var player = SystemAPI.HasSingleton<PlayerSnapshot>() ? SystemAPI.GetSingleton<PlayerSnapshot>() : default;
            if (player.IsAvailable)
            {
                foreach (var (pose, velocity, launch, contact, entity) in
                    SystemAPI.Query<RefRO<LocalTransform>, RefRO<PhysicsVelocity>, RefRO<EnemyLaunchState>, RefRO<EnemyContactDamageSettings>>()
                        .WithAll<WizardSettings>().WithNone<RespawnRequest>().WithEntityAccess())
                    if (launch.ValueRO.Phase == EnemyLaunchPhase.Launched &&
                        LaunchedEnemyPlayerImpactSystem.SegmentIntersectsSphere(pose.ValueRO.Position - velocity.ValueRO.Linear * SystemAPI.Time.DeltaTime,
                            pose.ValueRO.Position, player.Position, player.Radius + contact.ValueRO.ContactRadius)) hits.Add(entity);
            }
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var source in hits)
            {
                var cast = SystemAPI.GetComponent<WizardCastState>(source);
                var launch = SystemAPI.GetComponent<EnemyLaunchState>(source);
                if (cast.ImpactFlight == launch.ContinuousFlight) continue;
                cast.ImpactFlight = launch.ContinuousFlight;
                SystemAPI.SetComponent(source, cast);
                WizardZoneCreation.Create(commands, state.EntityManager, source,
                    SystemAPI.GetComponent<LocalTransform>(source).Position, SystemAPI.GetComponent<WizardSettings>(source),
                    SystemAPI.Time.ElapsedTime, false, true);
            }
            commands.Playback(state.EntityManager);
        }

        private struct Contacts : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<WizardSettings> Wizards;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<BossPart> BossParts;
            [ReadOnly] public ComponentLookup<RespawnRequest> Respawns;
            public NativeList<Entity> Hits;
            public void Execute(CollisionEvent e) { Check(e.EntityA, e.EntityB); Check(e.EntityB, e.EntityA); }
            private void Check(Entity source, Entity target)
            {
                if (!Wizards.HasComponent(source) || !Launches.HasComponent(source) ||
                    Launches[source].Phase != EnemyLaunchPhase.Launched) return;
                if (Respawns.HasComponent(target) && Respawns.IsComponentEnabled(target)) return;
                if (BossParts.HasComponent(target) || Launches.HasComponent(target) && Launches[target].Phase != EnemyLaunchPhase.Defeated)
                    Hits.Add(source);
            }
        }
    }
}
