using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Physics
{
    // The motor's velocity lands on a vertex, which can be much slower than its flight speed.
    // Transfer the incoming momentum in the existing solver rather than shortening a chain.
    [BurstCompile, UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateAfter(typeof(PhysicsCreateJacobiansGroup)), UpdateBefore(typeof(PhysicsSolveAndIntegrateGroup))]
    public partial struct AuthoritativeLaunchContactSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AuthoritativeLaunchPath>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<SimulationSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            state.Dependency = new TransferMomentum {
                Paths = SystemAPI.GetComponentLookup<AuthoritativeLaunchPath>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Enemies = SystemAPI.GetComponentLookup<Enemy>(true),
                Tiers = SystemAPI.GetComponentLookup<EnemyTier>(true),
                Armors = SystemAPI.GetComponentLookup<EnemyArmor>(true),
                Dashers = SystemAPI.GetComponentLookup<DasherState>(true),
                SnakeHeads = SystemAPI.GetComponentLookup<SnakeHead>(true),
                SnakeSegments = SystemAPI.GetComponentLookup<SnakeSegment>(true),
                Now = SystemAPI.Time.ElapsedTime,
                Masses = SystemAPI.GetComponentLookup<PhysicsMass>(true),
                Overrides = SystemAPI.GetComponentLookup<PhysicsMassOverride>(true),
                Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                Colliders = SystemAPI.GetComponentLookup<PhysicsCollider>(true)
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), ref world, state.Dependency);
        }

        [BurstCompile]
        private struct TransferMomentum : IJacobiansJob
        {
            [ReadOnly] public ComponentLookup<AuthoritativeLaunchPath> Paths;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<Enemy> Enemies;
            [ReadOnly] public ComponentLookup<EnemyTier> Tiers;
            [ReadOnly] public ComponentLookup<EnemyArmor> Armors;
            [ReadOnly] public ComponentLookup<DasherState> Dashers;
            [ReadOnly] public ComponentLookup<SnakeHead> SnakeHeads;
            [ReadOnly] public ComponentLookup<SnakeSegment> SnakeSegments;
            public double Now;
            [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
            [ReadOnly] public ComponentLookup<PhysicsMassOverride> Overrides;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<PhysicsCollider> Colliders;

            public void Execute(ref ModifiableJacobianHeader header, ref ModifiableContactJacobian contact)
            {
                if ((header.Flags & JacobianFlags.Disabled) != 0 || math.abs(contact.Normal.y) > .7f) return;
                bool sourceA = Owns(header.EntityA), sourceB = Owns(header.EntityB);
                if (sourceA == sourceB) return;
                Entity source = sourceA ? header.EntityA : header.EntityB;
                Entity target = sourceA ? header.EntityB : header.EntityA;
                if (!Enemies.HasComponent(target) || !Launches.HasComponent(target) || !Masses.HasComponent(target)
                    || !Velocities.HasComponent(target) || !Colliders.HasComponent(target)
                    || Overrides.HasComponent(target) && Overrides[target].IsKinematic != 0) return;
                var phase = Launches[target].Phase;
                if (phase != EnemyLaunchPhase.Active && phase != EnemyLaunchPhase.Recovering) return;
                if (!Tiers.HasComponent(target) || !EnemyLaunchTransition.IsLaunchable(Tiers[target])
                    || Armors.HasComponent(target) && (Armors[target].Stages > 0 || Now < Armors[target].ProtectedUntil)
                    || Dashers.HasComponent(target) && Dashers[target].Phase == DasherPhase.Dashing
                    || SnakeHitRules.Protected(target, SnakeHeads, SnakeSegments)) return;
                float targetInverseMass = Masses[target].InverseMass;
                if (targetInverseMass <= 0f) return;
                var path = Paths[source];
                float3 incoming = path.CrowdSurface == target ? path.CrowdIncomingVelocity : path.IncomingVelocity;
                float3 towardTarget = sourceA ? -contact.Normal : contact.Normal;
                float3 targetVelocity = Velocities[target].Linear;
                float closingSpeed = math.dot(incoming - targetVelocity, towardTarget);
                if (closingSpeed <= 0f) return;
                float restitution = Material.GetCombinedRestitution(ContactMaterial(source), ContactMaterial(target));
                float deltaSpeed = (1f + restitution) * closingSpeed * targetInverseMass
                    / math.max(.0001f, Masses[source].InverseMass + targetInverseMass);
                float requiredSeparation = math.dot(targetVelocity - Velocities[source].Linear, towardTarget) + deltaSpeed;
                for (int i = 0; i < contact.NumContacts; i++)
                {
                    var point = header.GetAngularJacobian(i);
                    // Keep stronger real solver responses and depenetration. Only repair momentum
                    // lost to quantizing a partial contact segment into a complete fixed step.
                    point.VelToReachCp = math.max(point.VelToReachCp, requiredSeparation);
                    header.SetAngularJacobian(i, point);
                }
            }

            private bool Owns(Entity entity)
            {
                if (!Paths.HasComponent(entity) || !Paths.IsComponentEnabled(entity)
                    || !Launches.HasComponent(entity) || !Masses.HasComponent(entity)
                    || !Velocities.HasComponent(entity) || !Colliders.HasComponent(entity)
                    || !Overrides.HasComponent(entity) || Overrides[entity].IsKinematic == 0) return false;
                var launch = Launches[entity];
                return launch.Phase == EnemyLaunchPhase.Launched && launch.Owner == EnemyLaunchOwner.Player
                    && launch.LaunchSequence == Paths[entity].LaunchSequence;
            }

            private Material ContactMaterial(Entity entity)
            {
                var collider = Colliders[entity].Value;
                if (collider.Value.CollisionType == CollisionType.Convex)
                    return UnsafeUtility.As<Collider, ConvexCollider>(ref collider.Value).Material;
                var material = Material.Default;
                material.Restitution = collider.Value.GetRestitution();
                return material;
            }

            public void Execute(ref ModifiableJacobianHeader header, ref ModifiableTriggerJacobian trigger) { }
        }
    }
}
