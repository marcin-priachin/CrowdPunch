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
                Dashers = SystemAPI.GetComponentLookup<DasherState>(true),
                Time = (float)SystemAPI.Time.ElapsedTime
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new SnakeBodyColorJob {
                Heads = SystemAPI.GetComponentLookup<SnakeHead>(true),
                Segments = SystemAPI.GetComponentLookup<SnakeSegment>(true),
                Tunings = SystemAPI.GetComponentLookup<SnakeTuning>(true)
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct SnakeBodyColorJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<SnakeHead> Heads;
            [ReadOnly] public ComponentLookup<SnakeSegment> Segments;
            [ReadOnly] public ComponentLookup<SnakeTuning> Tunings;

            private void Execute(in EnemyVisualOwner owner, ref SnakeBodyColor color, ref SnakeBodyTintAmount amount)
            {
                bool segment = Segments.HasComponent(owner.Value);
                Entity head = segment ? Segments[owner.Value].Head : owner.Value;
                if (!Heads.HasComponent(head) || !Tunings.HasComponent(head)) return;
                var tuning = Tunings[head];
                bool exposed = segment ? Segments[owner.Value].Detached != 0
                    || SnakeHitRules.IsTip(Segments[owner.Value], Heads[head]) : !SnakeHitRules.Protected(Heads[head]);
                // SNAKE-006: preserve the textured green body until the final tail part is gone.
                // The mask keeps eyes and markings intact; animation still owns other feedback.
                color.Value = tuning.VulnerableColor;
                amount.Value = exposed ? 1f : 0f;
            }
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
            [ReadOnly] public ComponentLookup<DasherState> Dashers;
            public float Time;

            private void Execute(in EnemyVisualOwner owner, ref URPMaterialPropertyBaseColor color)
            {
                if (owner.AnimationDriven != 0) { color.Value = new float4(1f); return; }
                Entity enemy = owner.Value;
                if (!Archetypes.HasComponent(enemy) || !Launches.HasComponent(enemy)) return;
                EnemyArchetypeKind kind = Archetypes[enemy].Value;
                if (kind == EnemyArchetypeKind.Dasher && owner.UsesAtlas == 0) return;
                float3 body = kind switch
                {
                    EnemyArchetypeKind.LinkedPair => new float3(.45f, 1f, .85f),
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
                // A neutral multiplier preserves eyes, teeth, clothes and skin from the atlas.
                if (owner.UsesAtlas != 0) body = new float3(1.15f);
                Entity snake = SnakeHeads.HasComponent(enemy) ? enemy
                    : SnakeSegments.HasComponent(enemy) ? SnakeSegments[enemy].Head : Entity.Null;
                bool isSnake = SnakeTunings.HasComponent(snake);
                if (isSnake)
                {
                    var tuning = SnakeTunings[snake];
                    bool vulnerable = SnakeHeads.HasComponent(enemy) ? !SnakeHitRules.Protected(SnakeHeads[enemy])
                        : SnakeHeads.HasComponent(snake) && SnakeHitRules.IsTip(SnakeSegments[enemy], SnakeHeads[snake]);
                    body = vulnerable ? tuning.VulnerableColor.xyz : tuning.ProtectedColor.xyz;
                    if (owner.UsesAtlas != 0) body = new float3(1.15f);
                }
                EnemyLaunchPhase phase = Launches[enemy].Phase;
                bool preparing = kind == EnemyArchetypeKind.Baseline && Contacts.HasComponent(enemy)
                    && Contacts[enemy].IsWindingUp != 0
                    || Ranged.HasComponent(enemy) && Ranged[enemy].Phase == RangedAttackPhase.WindUp
                    || Elites.HasComponent(enemy) && Elites[enemy].Phase == ElitePunchPhase.WindUp
                    || Ninjas.HasComponent(enemy) && Ninjas[enemy].Phase == NinjaPhase.Preparing;
                if (phase == EnemyLaunchPhase.Recovering || phase == EnemyLaunchPhase.Defeated)
                    body *= phase == EnemyLaunchPhase.Defeated ? .5f : .8f;
                else if (!isSnake && preparing)
                    body = math.lerp(body, new float3(1.65f, 1.3f, .75f), 0.35f + 0.15f * math.sin(Time * 12f));
                else if (!isSnake && kind == EnemyArchetypeKind.Baseline && Contacts.HasComponent(enemy)
                    && Contacts[enemy].IsAttempting != 0)
                    body = new float3(1f, 0.3f, 0.12f);

                if (phase == EnemyLaunchPhase.Active && Wizards.HasComponent(enemy) && Wizards[enemy].IsCasting)
                    body *= (Wizards[enemy].Phase == WizardCastPhase.Active ? 1.7f : 1.2f) + .2f * math.sin(Time * 10);
                if (phase == EnemyLaunchPhase.Active && Ninjas.HasComponent(enemy)
                    && (Ninjas[enemy].Phase == NinjaPhase.ArrivalWarning || Ninjas[enemy].Phase == NinjaPhase.Dashing))
                    body = new float3(1, .45f, .18f);
                if (phase == EnemyLaunchPhase.Active && Dashers.HasComponent(enemy))
                {
                    var dash = Dashers[enemy];
                    if (dash.Phase == DasherPhase.Preparing)
                        body *= new float3(1.35f + .2f * math.sin(Time * 18f), 1.1f, .75f);
                    else if (dash.Phase == DasherPhase.Dashing) body *= 1.25f;
                }
                color.Value = new float4(body, 1f);
            }
        }
    }
}
