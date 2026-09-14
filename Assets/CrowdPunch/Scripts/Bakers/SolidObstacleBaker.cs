using CrowdPunch.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
namespace CrowdPunch.Bakers
{
    public sealed class SolidObstacleBaker : Baker<SolidObstacleAuthoring>
    {
        public override void Bake(SolidObstacleAuthoring authoring)
        {
            // World-space footprint is authoritative. Transform rotation/scale is deliberately unsupported.
            if (authoring.transform.rotation != UnityEngine.Quaternion.identity || authoring.transform.lossyScale != UnityEngine.Vector3.one)
                throw new System.InvalidOperationException("Solid obstacles require world identity rotation/scale. Use footprint dimensions and Snap.");
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var collider = BoxCollider.Create(new BoxGeometry { Center = new float3(0,authoring.height*.5f,0),
                Size = new float3(authoring.Size.x,authoring.height,authoring.Size.y), Orientation = quaternion.identity, BevelRadius = 0 },
                new CollisionFilter { BelongsTo = 1u, CollidesWith = uint.MaxValue }, Unity.Physics.Material.Default);
            AddBlobAsset(ref collider, out _);
            AddComponent(entity, new PhysicsCollider { Value = collider });
            AddSharedComponent(entity, new PhysicsWorldIndex());
        }
    }
}
