using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(LaunchedEnemyPlayerImpactSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct BossHeadBounceSystem : ISystem
    {
        private struct HeadContact { public Entity Head, Body; }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BossEncounter>();
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<SimulationSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (!player.IsAvailable) return;

            var contacts = new NativeList<HeadContact>(Allocator.TempJob);
            var collect = new CollectHeadContacts
            {
                Parts = SystemAPI.GetComponentLookup<BossPart>(true),
                Enemies = SystemAPI.GetComponentLookup<Enemy>(true),
                Contacts = contacts
            };
            state.Dependency = collect.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
            state.Dependency.Complete();

            var em = state.EntityManager;
            foreach (var contact in contacts)
            {
                if (!em.HasComponent<EnemyLaunchState>(contact.Body)
                    || !em.HasComponent<PhysicsVelocity>(contact.Body)
                    || em.HasComponent<RespawnRequest>(contact.Body)
                        && em.IsComponentEnabled<RespawnRequest>(contact.Body)) continue;

                var launch = em.GetComponentData<EnemyLaunchState>(contact.Body);
                if (launch.Phase != EnemyLaunchPhase.Launched) continue;

                // The shot's head lock must not steer the body back into the head next step.
                if (launch.HomingTarget == contact.Head)
                {
                    launch.HomingTarget = Entity.Null;
                    em.SetComponentData(contact.Body, launch);
                }

                var velocity = em.GetComponentData<PhysicsVelocity>(contact.Body);
                var redirected = Redirect(velocity.Linear,
                    em.GetComponentData<LocalTransform>(contact.Body).Position,
                    em.GetComponentData<LocalTransform>(contact.Head).Position,
                    player.Position, contact.Body.Index);
                if (math.all(redirected == velocity.Linear)) continue;
                velocity.Linear = redirected;
                em.SetComponentData(contact.Body, velocity);
            }
            contacts.Dispose();
        }

        // Keep the solver's speed and vertical motion; remove only its player-bound component.
        internal static float3 Redirect(float3 solvedVelocity, float3 bodyPosition,
            float3 headPosition, float3 playerPosition, int bodyIndex)
        {
            float speed = math.length(solvedVelocity.xz);
            float2 towardPlayer = math.normalizesafe((playerPosition - bodyPosition).xz);
            if (speed < 0.001f || math.lengthsq(towardPlayer) < 0.0001f) return solvedVelocity;

            float2 outgoing = solvedVelocity.xz / speed;
            float playerComponent = math.dot(outgoing, towardPlayer);
            if (playerComponent <= 0f) return solvedVelocity;

            float2 sideways = outgoing - towardPlayer * playerComponent;
            if (math.lengthsq(sideways) < 0.0001f)
                sideways = new float2(-towardPlayer.y, towardPlayer.x) * (bodyIndex % 2 == 0 ? 1 : -1);
            sideways = math.normalize(sideways);

            float2 outward = math.normalizesafe((bodyPosition - headPosition).xz);
            if (math.dot(sideways, outward) < 0f) sideways = -sideways;
            solvedVelocity.xz = sideways * speed;
            return solvedVelocity;
        }

        [BurstCompile]
        private struct CollectHeadContacts : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<BossPart> Parts;
            [ReadOnly] public ComponentLookup<Enemy> Enemies;
            public NativeList<HeadContact> Contacts;

            public void Execute(CollisionEvent collision)
            {
                Entity head = Parts.HasComponent(collision.EntityA) ? collision.EntityA : collision.EntityB;
                Entity body = head == collision.EntityA ? collision.EntityB : collision.EntityA;
                if (Parts.HasComponent(head) && Parts[head].Kind == BossPartKind.Head
                    && Enemies.HasComponent(body))
                    Contacts.Add(new HeadContact { Head = head, Body = body });
            }
        }
    }
}
