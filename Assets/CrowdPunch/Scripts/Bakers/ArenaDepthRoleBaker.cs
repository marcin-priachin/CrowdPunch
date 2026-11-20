using System;
using CrowdPunch.Components;
using Unity.Entities;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    public sealed class ArenaDepthRoleBaker : Baker<MeshRenderer>
    {
        public override void Bake(MeshRenderer authoring)
        {
            string name = GetName();
            float role;
            if (name.StartsWith("Perimeter ", StringComparison.Ordinal))
                role = ArenaDepthRole.Background;
            else if (name == "Nature rock" || name.StartsWith("Interior Obstacle ", StringComparison.Ordinal))
                role = ArenaDepthRole.Rock;
            else return;

            AddComponent(GetEntity(TransformUsageFlags.Renderable), new ArenaDepthRoleBakingData
            {
                Role = new ArenaDepthRole { Value = role }
            });
        }
    }
}
