using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Bakers
{
    /// <summary>
    /// Converts arena authoring data into ECS bounds.
    /// </summary>
    public sealed class ArenaBaker : Baker<ArenaAuthoring>
    {
        public override void Bake(ArenaAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.WorldSpace);
            UnityEngine.Vector3 position = authoring.transform.position;
            float3 center = new float3(position.x, position.y, position.z);

            AddComponent(entity, new ArenaBounds
            {
                Center = center,
                Extents = authoring.Size * 0.5f
            });
        }
    }
}
