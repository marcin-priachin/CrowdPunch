using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Bakers
{
    public sealed class BarricadeRoomBaker : Baker<BarricadeRoomAuthoring>
    {
        public override void Bake(BarricadeRoomAuthoring authoring)
        {
            var crowd = GetComponent<EnemyWaveSequenceAuthoring>();
            if (crowd.barricade == null || crowd.Waves.Count != 1 || math.any((float2)authoring.maximum <= (float2)authoring.minimum))
                throw new System.InvalidOperationException("Barricade room needs one gate-owned crowd and valid bounds.");
            AddComponent(GetEntity(TransformUsageFlags.None), new BarricadeRoom {
                PreviousGate = authoring.previousGate == null ? Entity.Null : GetEntity(authoring.previousGate, TransformUsageFlags.Dynamic),
                Minimum = authoring.minimum, Maximum = authoring.maximum });
        }
    }
}
