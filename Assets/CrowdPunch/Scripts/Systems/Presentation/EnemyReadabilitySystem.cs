using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
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
            state.Dependency = new ReadabilityJob
            {
                SnakeHeads = SystemAPI.GetComponentLookup<SnakeHead>(true),
                SnakeSegments = SystemAPI.GetComponentLookup<SnakeSegment>(true),
                SnakeTunings = SystemAPI.GetComponentLookup<SnakeTuning>(true),
                Wizards = SystemAPI.GetComponentLookup<WizardCastState>(true),
                Ninjas = SystemAPI.GetComponentLookup<NinjaState>(true),
                Archetypes = SystemAPI.GetComponentLookup<EnemyArchetype>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Contacts = SystemAPI.GetComponentLookup<EnemyContactAttemptState>(true),
                Ranged = SystemAPI.GetComponentLookup<RangedAttackState>(true),
                Elites = SystemAPI.GetComponentLookup<ElitePunchState>(true),
                Time = (float)SystemAPI.Time.ElapsedTime
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct ReadabilityJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<SnakeHead> SnakeHeads;
            [ReadOnly] public ComponentLookup<SnakeSegment> SnakeSegments;
            [ReadOnly] public ComponentLookup<SnakeTuning> SnakeTunings;
            [ReadOnly] public ComponentLookup<WizardCastState> Wizards;
            [ReadOnly] public ComponentLookup<NinjaState> Ninjas;
            [ReadOnly] public ComponentLookup<EnemyArchetype> Archetypes;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<EnemyContactAttemptState> Contacts;
            [ReadOnly] public ComponentLookup<RangedAttackState> Ranged;
            [ReadOnly] public ComponentLookup<ElitePunchState> Elites;
            public float Time;

            private void Execute(in EnemyVisualOwner owner, ref URPMaterialPropertyBaseColor color)
            {
                Entity enemy = owner.Value;
                if (!Archetypes.HasComponent(enemy) || !Launches.HasComponent(enemy)) return;
                EnemyArchetypeKind kind = Archetypes[enemy].Value;
                if (kind == EnemyArchetypeKind.Dasher) return; // Existing streak silhouette owns this role.
                float3 body = kind switch
                {
                    EnemyArchetypeKind.Wizard => new float3(.65f, .15f, 1f),
                    EnemyArchetypeKind.Trail => new float3(.12f, .85f, .5f),
                    EnemyArchetypeKind.Ninja => new float3(.48f, .38f, .7f),
                    EnemyArchetypeKind.Ranged => new float3(0.06f, 0.55f, 1f),
                    EnemyArchetypeKind.Explosive => new float3(1f, 0.3f, 0.035f),
                    EnemyArchetypeKind.Elite => new float3(0.62f, 0.13f, 0.85f),
                    EnemyArchetypeKind.Armored => new float3(0.55f, 0.60f, 0.66f),
                    EnemyArchetypeKind.Dragon => new float3(1,0,0),
                    _ => new float3(0.32f, 0.52f, 0.46f)
                };
                Entity snake = SnakeHeads.HasComponent(enemy) ? enemy
                    : SnakeSegments.HasComponent(enemy) ? SnakeSegments[enemy].Head : Entity.Null;
                bool isSnake = SnakeTunings.HasComponent(snake);
                if (isSnake)
                {
                    var tuning = SnakeTunings[snake];
                    bool vulnerable = SnakeHeads.HasComponent(enemy) ? !SnakeHitRules.Protected(SnakeHeads[enemy])
                        : SnakeHeads.HasComponent(snake) && SnakeHitRules.IsTip(SnakeSegments[enemy], SnakeHeads[snake]);
                    body = vulnerable ? tuning.VulnerableColor.xyz : tuning.ProtectedColor.xyz;
                }
                EnemyLaunchPhase phase = Launches[enemy].Phase;
                bool preparing = kind == EnemyArchetypeKind.Baseline && Contacts.HasComponent(enemy)
                    && Contacts[enemy].IsWindingUp != 0
                    || Ranged.HasComponent(enemy) && Ranged[enemy].Phase == RangedAttackPhase.WindUp
                    || Elites.HasComponent(enemy) && Elites[enemy].Phase == ElitePunchPhase.WindUp
                    || Ninjas.HasComponent(enemy) && Ninjas[enemy].Phase == NinjaPhase.Preparing;
                if (phase == EnemyLaunchPhase.Recovering || phase == EnemyLaunchPhase.Defeated)
                    body *= 0.45f;
                else if (!isSnake && preparing)
                    body = math.lerp(body, new float3(1f, 0.82f, 0.2f), 0.55f + 0.3f * math.sin(Time * 12f));
                else if (!isSnake && kind == EnemyArchetypeKind.Baseline && Contacts.HasComponent(enemy)
                    && Contacts[enemy].IsAttempting != 0)
                    body = new float3(1f, 0.3f, 0.12f);

                if (phase == EnemyLaunchPhase.Active && Wizards.HasComponent(enemy) && Wizards[enemy].IsCasting)
                    body *= (Wizards[enemy].Phase == WizardCastPhase.Active ? 1.7f : 1.2f) + .2f * math.sin(Time * 10);
                if (phase == EnemyLaunchPhase.Active && Ninjas.HasComponent(enemy)
                    && (Ninjas[enemy].Phase == NinjaPhase.ArrivalWarning || Ninjas[enemy].Phase == NinjaPhase.Dashing))
                    body = new float3(1, .45f, .18f);
                color.Value = new float4(body, 1f);
            }
        }
    }
}
