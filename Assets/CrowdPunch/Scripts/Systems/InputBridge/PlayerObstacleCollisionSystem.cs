using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.InputBridge
{
    /// <summary>Resolves hybrid player movement against baked static geometry.</summary>
    [UpdateInGroup(typeof(GamePrePhysicsGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PlayerBridgeSystem))]
    public partial struct PlayerObstacleCollisionSystem : ISystem
    {
        private const uint EnemyCategory = 1u << 7;
        private const float Skin = 0.02f;
        private const int MaxDepenetrationPasses = 4;
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
            if (!PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge) || !bridge.HasPendingMovement)
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

            EnsureSphere(radius, bridge.CollisionLayer);

            float3 castOffset = new float3(0f, radius + Skin, 0f);
            CollisionWorld world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            start = Depenetrate(world, sphere, start, castOffset);
            if (math.lengthsq(displacement) <= 1e-8f)
            {
                start.y = end.y;
                bridge.ResolveMovement(sequence, start);
                return;
            }

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
                if (!world.CalculateDistance(input, out DistanceHit hit) || hit.Distance >= 0f)
                {
                    break;
                }

                float3 normal = hit.SurfaceNormal;
                normal.y = 0f;
                if (math.lengthsq(normal) <= 1e-8f)
                {
                    break;
                }

                position += math.normalize(normal) * (-hit.Distance + Skin);
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

            CollisionFilter filter = new CollisionFilter
            {
                BelongsTo = collisionLayer,
                CollidesWith = ~EnemyCategory,
                GroupIndex = 0
            };
            sphere = SphereCollider.Create(
                new SphereGeometry { Center = float3.zero, Radius = radius }, filter);
            sphereRadius = radius;
            sphereLayer = collisionLayer;
        }

        private static float3 CastWithSlide(
            CollisionWorld world,
            BlobAssetReference<Collider> sphere,
            float3 start,
            float3 displacement,
            float3 castOffset)
        {
            if (!Cast(world, sphere, start + castOffset, displacement, out ColliderCastHit hit))
            {
                return start + displacement;
            }

            float distance = math.length(displacement);
            float safeFraction = math.max(0f, hit.Fraction - Skin / math.max(distance, Skin));
            float3 resolved = start + displacement * safeFraction;
            float3 remainder = displacement * (1f - safeFraction);
            float3 normal = hit.SurfaceNormal;
            normal.y = 0f;
            if (math.lengthsq(normal) <= 1e-8f)
            {
                return resolved;
            }

            normal = math.normalize(normal);
            if (hit.Fraction <= 1e-5f && math.dot(displacement, normal) >= 0f)
            {
                return start + displacement;
            }

            float3 slide = remainder - normal * math.dot(remainder, normal);
            if (math.lengthsq(slide) <= 1e-8f)
            {
                return resolved;
            }

            if (Cast(world, sphere, resolved + castOffset, slide, out ColliderCastHit slideHit))
            {
                float slideDistance = math.length(slide);
                float slideFraction = math.max(0f, slideHit.Fraction - Skin / math.max(slideDistance, Skin));
                return resolved + slide * slideFraction;
            }

            return resolved + slide;
        }

        private static bool Cast(
            CollisionWorld world,
            BlobAssetReference<Collider> sphere,
            float3 start,
            float3 displacement,
            out ColliderCastHit hit)
        {
            ColliderCastInput input = new ColliderCastInput(sphere, start, start + displacement);
            return world.CastCollider(input, out hit);
        }
    }
}
