using CrowdPunch.Authoring;
using CrowdPunch.Utilities;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;
namespace CrowdPunch.Bakers
{
    public sealed class SolidObstacleBaker : Baker<SolidObstacleAuthoring>
    {
        public override void Bake(SolidObstacleAuthoring authoring)
        {
            // Navigation footprints stay world aligned; collision follows the actual visible surfaces.
            if (authoring.transform.rotation != UnityEngine.Quaternion.identity || authoring.transform.lossyScale != UnityEngine.Vector3.one)
                throw new System.InvalidOperationException("Solid obstacles require world identity rotation/scale. Use footprint dimensions and Snap.");
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var surfaces = new List<MeshFilter>();
            foreach (var filter in GetComponentsInChildren<MeshFilter>())
            {
                var renderer = GetComponent<MeshRenderer>(filter);
                if (renderer == null) continue;
                DependsOn(renderer);
                if (!renderer.enabled || renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                DependsOn(filter);
                DependsOn(filter.transform);
                DependsOn(filter.sharedMesh);
                surfaces.Add(filter);
            }
            var collider = SolidObstacleGeometry.CreateCollider(authoring.transform, surfaces.ToArray());
            AddBlobAsset(ref collider, out _);
            AddComponent(entity, new PhysicsCollider { Value = collider });
            AddSharedComponent(entity, new PhysicsWorldIndex());
        }
    }
}
