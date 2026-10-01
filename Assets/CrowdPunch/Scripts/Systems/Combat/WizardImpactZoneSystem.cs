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
    [UpdateBefore(typeof(DasherEnemyImpactSystem))]
    [UpdateBefore(typeof(ExplosiveCollisionTriggerSystem))]
    public partial struct WizardImpactZoneSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<SimulationSingleton>(); state.RequireForUpdate<WizardSettings>(); }

        public void OnUpdate(ref SystemState state)
        {
            using var hits = new NativeList<WizardImpactContact>(Allocator.TempJob);
            state.Dependency = new Contacts
            {
                Wizards = SystemAPI.GetComponentLookup<WizardSettings>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                BossParts = SystemAPI.GetComponentLookup<BossPart>(true),
                Respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                Hits = hits
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
            state.Dependency.Complete();
            // Launched Dashers disable enemy solver contacts; match their existing swept hit path.
            foreach (var (dash, pose, radius, launch, incoming) in
                SystemAPI.Query<RefRO<DasherState>, RefRO<LocalTransform>, RefRO<EnemyContactDamageSettings>,
                    RefRO<EnemyLaunchState>>().WithAll<DasherSettings>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Launched) continue;
                float3 start = dash.ValueRO.PreviousPosition;
                float3 end = pose.ValueRO.Position;
                start.y = end.y;
                foreach (var (wizardPose, wizardRadius, wizardLaunch, wizard) in
                    SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyContactDamageSettings>,
                        RefRO<EnemyLaunchState>>().WithAll<WizardSettings>().WithNone<RespawnRequest>().WithEntityAccess())
                {
                    if (wizardLaunch.ValueRO.Phase != EnemyLaunchPhase.Active &&
                        wizardLaunch.ValueRO.Phase != EnemyLaunchPhase.Recovering) continue;
                    float combinedRadius = math.max(0, radius.ValueRO.ContactRadius) +
                        math.max(0, wizardRadius.ValueRO.ContactRadius);
                    if (SweptImpact(wizardPose.ValueRO.Position, start, end, combinedRadius))
                        hits.Add(new WizardImpactContact { Wizard = wizard, Incoming = incoming,
                            IncomingFlight = launch.ValueRO.ContinuousFlight });
                }
            }
            var player = SystemAPI.HasSingleton<PlayerSnapshot>() ? SystemAPI.GetSingleton<PlayerSnapshot>() : default;
            if (player.IsAvailable)
            {
                foreach (var (pose, velocity, launch, contact, entity) in
                    SystemAPI.Query<RefRO<LocalTransform>, RefRO<PhysicsVelocity>, RefRO<EnemyLaunchState>, RefRO<EnemyContactDamageSettings>>()
                        .WithAll<WizardSettings>().WithNone<RespawnRequest>().WithEntityAccess())
                    if (launch.ValueRO.Phase == EnemyLaunchPhase.Launched &&
                        LaunchedEnemyPlayerImpactSystem.SegmentIntersectsSphere(pose.ValueRO.Position - velocity.ValueRO.Linear * SystemAPI.Time.DeltaTime,
                            pose.ValueRO.Position, player.Position, player.Radius + contact.ValueRO.ContactRadius))
                        hits.Add(new WizardImpactContact { Wizard = entity });
            }
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var hit in hits)
            {
                Entity source = hit.Wizard;
                var cast = SystemAPI.GetComponent<WizardCastState>(source);
                if (hit.Incoming != Entity.Null)
                {
                    if (!RegisterIncoming(SystemAPI.GetBuffer<WizardIncomingImpactHistory>(source),
                        hit.Incoming, hit.IncomingFlight)) continue;
                }
                else
                {
                    var launch = SystemAPI.GetComponent<EnemyLaunchState>(source);
                    if (cast.ImpactFlight == launch.ContinuousFlight) continue;
                    cast.ImpactFlight = launch.ContinuousFlight;
                    SystemAPI.SetComponent(source, cast);
                }
                WizardZoneCreation.Create(commands, state.EntityManager, source,
                    SystemAPI.GetComponent<LocalTransform>(source).Position, SystemAPI.GetComponent<WizardSettings>(source),
                    SystemAPI.Time.ElapsedTime, false, true);
            }
            commands.Playback(state.EntityManager);
        }

        // A source can remain in contact or be punched again during one continuous flight.
        public static bool RegisterIncoming(DynamicBuffer<WizardIncomingImpactHistory> history,
            Entity incoming, uint flight)
        {
            for (int i = 0; i < history.Length; i++)
            {
                if (history[i].Source != incoming) continue;
                if (history[i].ContinuousFlight == flight) return false;
                history[i] = new WizardIncomingImpactHistory { Source = incoming, ContinuousFlight = flight };
                return true;
            }
            history.Add(new WizardIncomingImpactHistory { Source = incoming, ContinuousFlight = flight });
            return true;
        }

        public static bool SweptImpact(float3 point, float3 start, float3 end, float radius)
        {
            point.y = start.y;
            float3 segment = end - start;
            float lengthSq = math.lengthsq(segment);
            float t = lengthSq <= .0001f ? 0 : math.saturate(math.dot(point - start, segment) / lengthSq);
            return math.lengthsq(point - (start + segment * t)) <= radius * radius;
        }

        private struct WizardImpactContact
        {
            public Entity Wizard;
            public Entity Incoming;
            public uint IncomingFlight;
        }

        private struct Contacts : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<WizardSettings> Wizards;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<BossPart> BossParts;
            [ReadOnly] public ComponentLookup<RespawnRequest> Respawns;
            public NativeList<WizardImpactContact> Hits;
            public void Execute(CollisionEvent e) { Check(e.EntityA, e.EntityB); Check(e.EntityB, e.EntityA); }
            private void Check(Entity wizard, Entity other)
            {
                if (!Wizards.HasComponent(wizard) || !Launches.HasComponent(wizard) ||
                    Respawns.HasComponent(wizard) && Respawns.IsComponentEnabled(wizard) ||
                    Respawns.HasComponent(other) && Respawns.IsComponentEnabled(other)) return;
                var phase = Launches[wizard].Phase;
                if (phase == EnemyLaunchPhase.Launched)
                {
                    if (BossParts.HasComponent(other) ||
                        Launches.HasComponent(other) && Launches[other].Phase != EnemyLaunchPhase.Defeated)
                        Hits.Add(new WizardImpactContact { Wizard = wizard });
                }
                else if ((phase == EnemyLaunchPhase.Active || phase == EnemyLaunchPhase.Recovering) &&
                    Launches.HasComponent(other) && Launches[other].Phase == EnemyLaunchPhase.Launched)
                {
                    Hits.Add(new WizardImpactContact { Wizard = wizard, Incoming = other,
                        IncomingFlight = Launches[other].ContinuousFlight });
                }
            }
        }
    }
}
