using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Bakers
{
    public sealed class RotatingCoverBaker : Baker<RotatingCoverAuthoring>
    {
        public override void Bake(RotatingCoverAuthoring a)
        {
            if (a.target == null || a.settings == null)
                throw new System.InvalidOperationException("Rotating cover requires its target and settings.");
            DependsOn(a.settings);
            var s = a.settings;
            var e = GetEntity(TransformUsageFlags.Dynamic);
            var cover = new RotatingCover {
                Target = GetEntity(a.target, TransformUsageFlags.Dynamic),
                Mode = s.rotationMode, HitResponse = s.hitResponse,
                Radius = math.max(3, s.radius), Height = math.max(2, s.height), Thickness = math.clamp(s.thickness, .1f, 1),
                OpeningRadians = math.radians(math.clamp(s.openingDegrees, 30, 160)), InitialAngle = math.radians(s.initialOpeningDegrees),
                RadiansPerSecond = math.radians(math.max(0, s.degreesPerSecond)), RotateSeconds = math.max(.1f, s.rotateSeconds),
                PauseSeconds = math.max(0, s.pauseSeconds), ReverseSeconds = math.max(.1f, s.reverseSeconds),
                HitPauseSeconds = math.max(0, s.hitPauseSeconds), SpeedIncreasePerHit = math.max(0, s.speedIncreasePerHit),
                ReflectionMultiplier = math.max(0, s.reflectionSpeedMultiplier) };
            AddComponent(e, cover);
            AddComponent(e, new RotatingCoverState { Angle = cover.InitialAngle });
            AddBuffer<CoverReflection>(e);
            var material = Material.Default;
            material.Friction = 0;
            material.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
            var parts = new NativeArray<CompoundCollider.ColliderBlobInstance>(CoverGeometry.PanelCount, Allocator.Temp);
            for (int i = 0; i < parts.Length; i++)
            {
                CoverGeometry.Panel(cover, i, out var position, out var rotation, out var size);
                var blob = BoxCollider.Create(new BoxGeometry { Size = size, Orientation = quaternion.identity, BevelRadius = .01f },
                    CollisionFilter.Default, material);
                parts[i] = new CompoundCollider.ColliderBlobInstance { Collider = blob, CompoundFromChild = new RigidTransform(rotation, position) };
            }
            var compound = CompoundCollider.Create(parts);
            foreach (var part in parts) part.Collider.Dispose();
            parts.Dispose();
            AddBlobAsset(ref compound, out _);
            AddComponent(e, new PhysicsCollider { Value = compound });
            AddSharedComponent(e, new PhysicsWorldIndex());

            // A stationary, query-visible volume blocks the hybrid player and walking enemies.
            // Only launched-body solver contacts are disabled by CoverEnclosureContactSystem.
            var enclosure = CreateAdditionalEntity(TransformUsageFlags.ManualOverride);
            var solid = CylinderCollider.Create(new CylinderGeometry { Height = cover.Height, Radius = cover.Radius - cover.Thickness,
                Orientation = quaternion.RotateX(math.PI * .5f), SideCount = 32, BevelRadius = .01f }, CollisionFilter.Default, material);
            AddBlobAsset(ref solid, out _);
            AddComponent<CoverEnclosure>(enclosure);
            AddComponent(enclosure, LocalTransform.FromPosition(a.transform.position));
            AddComponent(enclosure, new LocalToWorld { Value = float4x4.Translate(a.transform.position) });
            AddComponent(enclosure, new PhysicsCollider { Value = solid });
            AddSharedComponent(enclosure, new PhysicsWorldIndex());
        }
    }
}
