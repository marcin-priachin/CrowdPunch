using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Per-enemy preferred spacing selected from the authored range at spawn time.
    /// </summary>
    public struct EnemySeparationDistance : IComponentData
    {
        public float Value;
    }

    /// <summary>Optional authored ranges selected according to the nearby enemy's archetype.</summary>
    public struct EnemyArchetypeSeparationSettings
    {
        public byte OverrideMask;
        public float BaselineMin;
        public float BaselineMax;
        public float BaselineWeight;
        public float RangedMin;
        public float RangedMax;
        public float RangedWeight;
        public float ExplosiveMin;
        public float ExplosiveMax;
        public float ExplosiveWeight;
        public float DasherMin;
        public float DasherMax;
        public float DasherWeight;

        public void Set(EnemyArchetypeKind archetype, float minimum, float maximum, float weight)
        {
            OverrideMask |= (byte)(1 << (int)archetype);
            switch (archetype)
            {
                case EnemyArchetypeKind.Ranged:
                    RangedMin = minimum; RangedMax = maximum; RangedWeight = weight;
                    break;
                case EnemyArchetypeKind.Explosive:
                    ExplosiveMin = minimum; ExplosiveMax = maximum; ExplosiveWeight = weight;
                    break;
                case EnemyArchetypeKind.Dasher:
                    DasherMin = minimum; DasherMax = maximum; DasherWeight = weight;
                    break;
                case EnemyArchetypeKind.Elite:
                    BaselineMin = minimum; BaselineMax = maximum; BaselineWeight = weight;
                    break;
                case EnemyArchetypeKind.Baseline:
                default:
                    BaselineMin = minimum; BaselineMax = maximum; BaselineWeight = weight;
                    break;
            }
        }
    }

    /// <summary>Per-enemy archetype distances randomized from the spawn profile's enabled ranges.</summary>
    public struct EnemyArchetypeSeparationDistances : IComponentData
    {
        public byte OverrideMask;
        public float Baseline;
        public float BaselineWeight;
        public float Ranged;
        public float RangedWeight;
        public float Explosive;
        public float ExplosiveWeight;
        public float Dasher;
        public float DasherWeight;

        public float GetDistance(EnemyArchetypeKind archetype, float fallback)
        {
            int bit = 1 << (int)archetype;
            if ((OverrideMask & bit) == 0)
            {
                return fallback;
            }

            return archetype switch
            {
                EnemyArchetypeKind.Ranged => Ranged,
                EnemyArchetypeKind.Explosive => Explosive,
                EnemyArchetypeKind.Dasher => Dasher,
                EnemyArchetypeKind.Elite => Baseline,
                _ => Baseline
            };
        }

        public float GetWeight(EnemyArchetypeKind archetype, float fallback)
        {
            int bit = 1 << (int)archetype;
            if ((OverrideMask & bit) == 0)
            {
                return fallback;
            }

            return archetype switch
            {
                EnemyArchetypeKind.Ranged => RangedWeight,
                EnemyArchetypeKind.Explosive => ExplosiveWeight,
                EnemyArchetypeKind.Dasher => DasherWeight,
                EnemyArchetypeKind.Elite => BaselineWeight,
                _ => BaselineWeight
            };
        }
    }

    public struct EnemySeparationNeighbor
    {
        public Unity.Mathematics.float3 Position;
        public EnemyArchetypeKind Archetype;
    }
}
