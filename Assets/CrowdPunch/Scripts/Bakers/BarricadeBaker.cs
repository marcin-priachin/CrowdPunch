using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Bakers
{
    public sealed class BarricadeBaker : Baker<BarricadeAuthoring>
    {
        public override void Bake(BarricadeAuthoring a)
        {
            if (a.settings == null || !a.completeOnDestruction && a.exit == null)
                throw new System.InvalidOperationException("Barricade requires settings and an exit.");
            DependsOn(a.settings);
            if (a.exit != null) DependsOn(a.exit);
            var e = GetEntity(TransformUsageFlags.Dynamic);
            var geometry = new BoxGeometry { Size = math.max((float3)a.size, .1f), Orientation = quaternion.identity, BevelRadius = .02f };
            var material = Material.Default;
            material.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
            material.Friction = 0;
            var intact = BoxCollider.Create(geometry, CollisionFilter.Default, material);
            var broken = BoxCollider.Create(geometry, CollisionFilter.Zero, material);
            AddBlobAsset(ref intact, out _);
            AddBlobAsset(ref broken, out _);
            AddComponent(e, new PhysicsCollider { Value = intact });
            AddSharedComponent(e, new PhysicsWorldIndex());
            int requiredHits = math.max(a.cover == null ? 1 : 2, a.settings.requiredHits);
            AddComponent(e, new Barricade {
                HitsRemaining = requiredHits, RequiredHits = requiredHits,
                Sources = a.settings.eligibleLaunchSources, ReboundMultiplier = math.clamp(a.settings.reboundMultiplier, .1f, 1.5f),
                ReplenishDelay = math.max(0, a.settings.replenishDelay), FlashDuration = math.max(.01f, a.settings.flashDuration),
                DebrisDuration = math.max(.01f, a.settings.debrisDuration), Size = geometry.Size,
                ExitPosition = a.exit == null ? float3.zero : (float3)a.exit.position, ExitRadius = math.max(.1f, a.exitRadius),
                CompleteOnDestruction = a.completeOnDestruction ? (byte)1 : (byte)0,
                Cover = a.cover == null ? Entity.Null : GetEntity(a.cover, TransformUsageFlags.Dynamic),
                IntactCollider = intact, BrokenCollider = broken
            });
            AddBuffer<BarricadeHitHistory>(e);
            AddBuffer<BarricadeRebound>(e);
        }
    }
}
