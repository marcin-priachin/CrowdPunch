using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>Enemy bodies carry role, attack commitment, launch, recovery and impact feedback (INFO-004).</summary>
    [BurstCompile, UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateBefore(typeof(DasherPresentationSystem))]
    public partial struct EnemyReadabilitySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ReadabilityJob
            {
                Archetypes = SystemAPI.GetComponentLookup<EnemyArchetype>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Contacts = SystemAPI.GetComponentLookup<EnemyContactAttemptState>(true),
                Ranged = SystemAPI.GetComponentLookup<RangedAttackState>(true),
                Elites = SystemAPI.GetComponentLookup<ElitePunchState>(true),
                Damage = SystemAPI.GetComponentLookup<EnemyHealthBarVisibility>(true),
                Time = (float)SystemAPI.Time.ElapsedTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct ReadabilityJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<EnemyArchetype> Archetypes;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<EnemyContactAttemptState> Contacts;
            [ReadOnly] public ComponentLookup<RangedAttackState> Ranged;
            [ReadOnly] public ComponentLookup<ElitePunchState> Elites;
            [ReadOnly] public ComponentLookup<EnemyHealthBarVisibility> Damage;
            public float Time;

            private void Execute(in EnemyVisualOwner owner, ref URPMaterialPropertyBaseColor color)
            {
                Entity enemy = owner.Value;
                if (!Archetypes.HasComponent(enemy) || !Launches.HasComponent(enemy)) return;
                EnemyArchetypeKind kind = Archetypes[enemy].Value;
                if (kind == EnemyArchetypeKind.Dasher) return; // Existing streak silhouette owns this role.
                float3 body = kind switch
                {
                    EnemyArchetypeKind.Ranged => new float3(0.06f, 0.55f, 1f),
                    EnemyArchetypeKind.Explosive => new float3(1f, 0.3f, 0.035f),
                    EnemyArchetypeKind.Elite => new float3(0.62f, 0.13f, 0.85f),
                    _ => new float3(0.32f, 0.52f, 0.46f)
                };
                EnemyLaunchPhase phase = Launches[enemy].Phase;
                bool preparing = kind == EnemyArchetypeKind.Baseline && Contacts.HasComponent(enemy)
                    && Contacts[enemy].IsWindingUp != 0
                    || Ranged.HasComponent(enemy) && Ranged[enemy].Phase == RangedAttackPhase.WindUp
                    || Elites.HasComponent(enemy) && Elites[enemy].Phase == ElitePunchPhase.WindUp;
                if (phase == EnemyLaunchPhase.Recovering || phase == EnemyLaunchPhase.Defeated)
                    body *= 0.45f;
                else if (preparing)
                    body = math.lerp(body, new float3(1f, 0.82f, 0.2f), 0.55f + 0.3f * math.sin(Time * 12f));
                else if (kind == EnemyArchetypeKind.Baseline && Contacts.HasComponent(enemy)
                    && Contacts[enemy].IsAttempting != 0)
                    body = new float3(1f, 0.3f, 0.12f);

                if (Damage.HasComponent(enemy) && Damage.IsComponentEnabled(enemy)
                    && Damage[enemy].SecondsRemaining > 0.88f)
                    body = new float3(1f); // Short actual-damage flash; elite health bar shows the amount.
                color.Value = new float4(body, 1f);
            }
        }
    }
}
