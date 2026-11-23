using CrowdPunch.Mono.Player;
using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.InputBridge
{
    /// <summary>Resolves hybrid player movement against baked static geometry.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    public partial struct PlayerObstacleCollisionSystem : ISystem
    {
        private const uint EnemyCategory = 1u << 7;
        private const float Skin = 0.02f;
        private const float MinBlockingDot = 1e-5f;
        private const int MaxDepenetrationPasses = 4;
        private const int MaxSlidePasses = 4;
        private BlobAssetReference<Collider> sphere;
        private float sphereRadius;
        private uint sphereLayer;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (sphere.IsCreated)
            {
                sphere.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (FeedbackTimeController.IsTransitioning
                || !PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge) || !bridge.HasPendingMovement)
            {
                return;
            }

            uint sequence = bridge.MovementSequence;
            float3 start = bridge.MovementStart;
            float3 end = bridge.MovementEnd;
            float radius = bridge.MovementRadius;
            float3 displacement = end - start;
            displacement.y = 0f;

            if (radius <= 0f)
            {
                bridge.ResolveMovement(sequence, end);
                return;
            }

            // A restart or collider destruction can invalidate the previous step's broadphase.
            // Hold this step until Unity Physics has rebuilt it.
            if (state.World.GetExistingSystemManaged<GamePrePhysicsGroup>() is { CanQueryPhysicsWorld: false })
            {
                bridge.ResolveMovement(sequence, start);
                return;
            }

            EnsureSphere(radius, bridge.CollisionLayer);

            // The visual/player root can be above the floor. Sweep at the actual grounded
            // body height so low visible rocks block movement without invisible tall walls.
            float groundHeight = SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var ground)
                ? ground.SurfaceHeight : start.y;
            float3 castOffset = new float3(0f, groundHeight - start.y + radius + Skin, 0f);
            state.EntityManager.CompleteDependencyBeforeRO<PhysicsWorldSingleton>();
            CollisionWorld world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            start = Depenetrate(world, sphere, start, castOffset);

            float3 resolved = CastWithSlide(
                world,
                sphere,
                start,
                displacement,
                castOffset);

            resolved.y = end.y;
            bridge.ResolveMovement(sequence, resolved);
        }

        private static float3 Depenetrate(
            CollisionWorld world,
            BlobAssetReference<Collider> sphere,
            float3 position,
            float3 castOffset)
        {
            for (int pass = 0; pass < MaxDepenetrationPasses; pass++)
            {
                ColliderDistanceInput input = new ColliderDistanceInput(
                    sphere,
                    0f,
                    new RigidTransform(quaternion.identity, position + castOffset));
                var collector = new HorizontalOverlapCollector();
                if (!world.CalculateDistance(input, ref collector))
                {
                    break;
                }

                position += collector.Normal * (Skin - collector.MaxFraction);
            }

            return position;
        }

        private void EnsureSphere(float radius, uint collisionLayer)
        {
            if (sphere.IsCreated && sphereRadius == radius && sphereLayer == collisionLayer)
            {
                return;
            }

            if (sphere.IsCreated)
            {
                sphere.Dispose();
            }

            sphere = SphereCollider.Create(
                new SphereGeometry { Center = float3.zero, Radius = radius }, MovementFilter(collisionLayer));
            sphereRadius = radius;
            sphereLayer = collisionLayer;
        }

        internal static float3 LimitPunchApproach(in CollisionWorld world, in PlayerSnapshot player,
            float groundHeight, float3 displacement)
        {
            float distance = math.length(displacement);
            if (distance <= 1e-5f || player.Radius <= 0f) return float3.zero;

            float3 direction = displacement / distance;
            float3 start = player.Position;
            start.y = groundHeight + player.Radius + Skin;
            if (!world.SphereCast(start, player.Radius, direction, distance, out var hit,
                MovementFilter(player.CollisionLayer), QueryInteraction.IgnoreTriggers))
                return displacement;

            // Stop along the approach line. Sliding here would move the fist off its target.
            return direction * math.max(0f, hit.Fraction * distance - Skin);
        }

        private static CollisionFilter MovementFilter(uint collisionLayer) => new CollisionFilter
        {
            BelongsTo = collisionLayer,
            CollidesWith = ~EnemyCategory
        };

        private static float3 CastWithSlide(
            CollisionWorld world,
            BlobAssetReference<Collider> sphere,
            float3 start,
            float3 displacement,
            float3 castOffset)
        {
            for (int pass = 0; pass < MaxSlidePasses && math.lengthsq(displacement) > 1e-8f; pass++)
            {
                float3 castStart = start + castOffset;
                var input = new ColliderCastInput(sphere, castStart, castStart + displacement);
                var collector = new BlockingHitCollector(displacement);
                if (!world.CastCollider(input, ref collector))
                {
                    return start + displacement;
                }

                // Measure the clearance across the wall, so shallow approaches keep their full slide.
                float closingDistance = -math.dot(displacement, collector.Normal);
                float safeFraction = math.max(0f, collector.MaxFraction - Skin / closingDistance);
                start += displacement * safeFraction;
                displacement = (displacement + collector.Normal * closingDistance) * (1f - safeFraction);
            }

            return start;
        }

        private static bool IsSolid(Material material) =>
            material.CollisionResponse != CollisionResponsePolicy.RaiseTriggerEvents
            && material.CollisionResponse != CollisionResponsePolicy.None;

        private static float3 HorizontalNormal(float3 normal)
        {
            normal.y = 0f;
            return math.lengthsq(normal) > 1e-8f ? math.normalize(normal) : float3.zero;
        }

        private struct BlockingHitCollector : ICollector<ColliderCastHit>
        {
            private readonly float3 direction;

            public float3 Normal { get; private set; }
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; private set; }
            public int NumHits { get; private set; }

            public BlockingHitCollector(float3 displacement)
            {
                this = default;
                direction = math.normalize(displacement);
                MaxFraction = 1f;
            }

            public bool AddHit(ColliderCastHit hit)
            {
                float3 normal = HorizontalNormal(hit.SurfaceNormal);
                // Keep searching after floor, tangent or separating contacts. Another face can still block.
                if (!IsSolid(hit.Material) || math.dot(direction, normal) >= -MinBlockingDot)
                {
                    return false;
                }

                Normal = normal;
                MaxFraction = hit.Fraction;
                NumHits = 1;
                return true;
            }
        }

        private struct HorizontalOverlapCollector : ICollector<DistanceHit>
        {
            public float3 Normal { get; private set; }
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; private set; }
            public int NumHits { get; private set; }

            public bool AddHit(DistanceHit hit)
            {
                float3 normal = HorizontalNormal(hit.SurfaceNormal);
                if (!IsSolid(hit.Material) || hit.Distance >= 0f || math.all(normal == float3.zero))
                {
                    return false;
                }

                Normal = normal;
                MaxFraction = hit.Distance;
                NumHits = 1;
                return true;
            }
        }
    }
}
