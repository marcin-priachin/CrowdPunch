using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    public sealed class FinaleAnchorBaker : Baker<FinaleAnchorAuthoring>
    {
        public override void Bake(FinaleAnchorAuthoring a)
        {
            if (a.boss == null) throw new System.InvalidOperationException("Finale anchor requires a boss.");
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new FinaleAnchor {
                Boss = GetEntity(a.boss, TransformUsageFlags.Dynamic), Landing = a.landing });
        }
    }
}
