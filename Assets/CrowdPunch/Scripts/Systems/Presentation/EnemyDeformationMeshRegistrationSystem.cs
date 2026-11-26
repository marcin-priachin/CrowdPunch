#if UNITY_6000_6
using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    [CreateAfter(typeof(EntitiesGraphicsSystem))]
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class EnemyDeformationMeshRegistrationSystem : SystemBase
    {
        private readonly Dictionary<EntityId, BatchMeshID> registeredMeshes = new();
        // World system lookups are unavailable during teardown.
        private EntitiesGraphicsSystem graphics;

        protected override void OnCreate()
        {
            RequireForUpdate<EnemyDeformationMeshPending>();
            graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        }

        protected override void OnUpdate()
        {
            if (graphics == null || !graphics.Enabled)
                return;

            // Entities Graphics 6.6.0 skips negative RenderMeshArray shared indices in
            // deformation. A runtime mesh ID bypasses that lookup and keeps the baked material.
            foreach (var (info, pending, entity) in SystemAPI
                         .Query<RefRW<MaterialMeshInfo>, EnabledRefRW<EnemyDeformationMeshPending>>()
                         .WithAll<RenderMeshArray>().WithEntityAccess())
            {
                if (info.ValueRO.Mesh < 0 && !info.ValueRO.HasMaterialMeshIndexRange)
                {
                    var mesh = EntityManager.GetSharedComponent<RenderMeshArray>(entity).GetMesh(info.ValueRO);
                    if (mesh == null)
                        continue;

                    EntityId key = mesh.GetEntityId();
                    if (!registeredMeshes.TryGetValue(key, out BatchMeshID meshId))
                    {
                        meshId = graphics.RegisterMesh(mesh);
                        if (meshId == BatchMeshID.Null)
                            continue;
                        registeredMeshes.Add(key, meshId);
                    }
                    info.ValueRW.MeshID = meshId;
                }

                pending.ValueRW = false;
            }
        }

        protected override void OnDestroy()
        {
            if (graphics != null)
                foreach (BatchMeshID meshId in registeredMeshes.Values)
                    graphics.UnregisterMesh(meshId);
        }
    }
}
#endif
