using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Extensions;

namespace CrowdPunch.Systems.Physics
{
    /// <summary>Removes only enemy solver contacts while a Dasher is a committed projectile.</summary>
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(DasherVelocityCaptureSystem))]
    public partial struct DasherColliderModeSystem : ISystem
    {
        private const uint EnemyCategory = 1u << 7;

        public void OnUpdate(ref SystemState state)
        {
            EntityQuery uninitializedQuery = SystemAPI.QueryBuilder()
                .WithAll<DasherColliderState, PhysicsCollider>().Build();
            NativeArray<Entity> entities = uninitializedQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                DasherColliderState mode = state.EntityManager.GetComponentData<DasherColliderState>(entity);
                if (mode.IsInitialized != 0) continue;
                PhysicsCollider collider = state.EntityManager.GetComponentData<PhysicsCollider>(entity);
                if (!collider.IsValid) continue;
                collider.MakeUnique(entity, state.EntityManager);
                state.EntityManager.SetComponentData(entity, collider);
            }
            entities.Dispose();

            foreach ((RefRW<PhysicsCollider> collider, RefRW<DasherColliderState> mode,
                         RefRO<DasherState> dash, RefRO<EnemyLaunchState> launch) in
                     SystemAPI.Query<RefRW<PhysicsCollider>, RefRW<DasherColliderState>, RefRO<DasherState>, RefRO<EnemyLaunchState>>())
            {
                if (!collider.ValueRO.IsValid) continue;
                if (mode.ValueRO.IsInitialized == 0)
                {
                    mode.ValueRW.SolidFilter = collider.ValueRO.Value.Value.GetCollisionFilter();
                    mode.ValueRW.IsInitialized = 1;
                }
                bool ignoreEnemies = dash.ValueRO.Phase == DasherPhase.Dashing
                    || launch.ValueRO.Phase == EnemyLaunchPhase.Launched
                        && launch.ValueRO.Owner != EnemyLaunchOwner.Player;
                if ((mode.ValueRO.IsIgnoringEnemies != 0) == ignoreEnemies) continue;
                CollisionFilter filter = mode.ValueRO.SolidFilter;
                if (ignoreEnemies) filter.CollidesWith &= ~EnemyCategory;
                collider.ValueRW.Value.Value.SetCollisionFilter(filter);
                mode.ValueRW.IsIgnoringEnemies = ignoreEnemies ? (byte)1 : (byte)0;
            }
        }
    }
}
