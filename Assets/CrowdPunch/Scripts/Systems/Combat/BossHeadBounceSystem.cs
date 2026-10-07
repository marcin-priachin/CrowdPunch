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
            state.RequireForUpdate(new EntityQueryBuilder(Allocator.Temp)
                .WithAny<BossEncounter, ChickenBoss, RollingBoss>().Build(ref state));
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
                Chickens = SystemAPI.GetComponentLookup<ChickenBoss>(true),
                Rollers = SystemAPI.GetComponentLookup<RollingBoss>(true),
                Enemies = SystemAPI.GetComponentLookup<Enemy>(true),
                Contacts = contacts
            };
            state.Dependency = collect.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
            state.Dependency.Complete();

            var em = state.EntityManager;
            foreach (var contact in contacts)
                ResolveContact(em, contact.Head, contact.Body, player.Position);
            contacts.Dispose();
        }

        internal static void ResolveContact(EntityManager em, Entity boss, Entity body, float3 playerPosition)
        {
            if (!em.HasComponent<EnemyLaunchState>(body)
                || !em.HasComponent<PhysicsVelocity>(body)
                || em.HasComponent<RespawnRequest>(body)
                    && em.IsComponentEnabled<RespawnRequest>(body)) return;

            var launch = em.GetComponentData<EnemyLaunchState>(body);
            if (launch.Phase != EnemyLaunchPhase.Launched) return;

            // Clear the boss lock so homing cannot undo the solver-contact deflection next step.
            if (launch.HomingTarget == boss)
            {
                launch.HomingTarget = Entity.Null;
                em.SetComponentData(body, launch);
            }

            var velocity = em.GetComponentData<PhysicsVelocity>(body);
            var redirected = Redirect(velocity.Linear,
                em.GetComponentData<LocalTransform>(body).Position,
                em.GetComponentData<LocalTransform>(boss).Position,
                playerPosition, body.Index);
            if (math.all(redirected == velocity.Linear)) return;
            velocity.Linear = redirected;
            em.SetComponentData(body, velocity);
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
            [ReadOnly] public ComponentLookup<ChickenBoss> Chickens;
            [ReadOnly] public ComponentLookup<RollingBoss> Rollers;
            [ReadOnly] public ComponentLookup<Enemy> Enemies;
            public NativeList<HeadContact> Contacts;

            public void Execute(CollisionEvent collision)
            {
                Entity head = Parts.HasComponent(collision.EntityA) || Chickens.HasComponent(collision.EntityA) || Rollers.HasComponent(collision.EntityA)
                    ? collision.EntityA : collision.EntityB;
                Entity body = head == collision.EntityA ? collision.EntityB : collision.EntityA;
                if ((Rollers.HasComponent(head) || Chickens.HasComponent(head) || Parts.HasComponent(head) && Parts[head].Kind == BossPartKind.Head)
                    && Enemies.HasComponent(body))
                    Contacts.Add(new HeadContact { Head = head, Body = body });
            }
        }
    }
}
