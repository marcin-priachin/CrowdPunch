using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    public sealed class BossEncounterBaker : Baker<BossEncounterAuthoring>
    {
        public override void Bake(BossEncounterAuthoring a)
        {
            if (a.settings == null || a.leftHand == null || a.rightHand == null || a.crowd == null)
                throw new System.InvalidOperationException("Boss requires settings, two hands and its supporting wave sequence.");
            DependsOn(a.settings);
            var e=GetEntity(TransformUsageFlags.Dynamic);
            var tuning=a.settings.Bake();
            AddComponent(e,tuning);
            AddComponent(e,new BossEncounter { LeftHand=GetEntity(a.leftHand,TransformUsageFlags.Dynamic),
                RightHand=GetEntity(a.rightHand,TransformUsageFlags.Dynamic), Stage=1, Cycle=BossCycle.Opening,
                Remaining=tuning.OpeningDuration });
            AddComponent(e,new Health { Current=tuning.Health, Max=tuning.Health });
        }
    }
}
