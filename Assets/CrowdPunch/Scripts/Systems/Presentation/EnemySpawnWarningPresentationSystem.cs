using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class EnemySpawnWarningPresentationSystem : SystemBase
    {
        private EnemySpawnBurrowMesh burrows;
        private Material soil;
        private CombatVfxLibrary library;

        protected override void OnCreate()
        {
            burrows = new EnemySpawnBurrowMesh();
        }

        protected override void OnUpdate()
        {
            if (soil == null) soil = Resources.Load<Material>("EnemySpawnSoil");
            if (library == null) library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            if (soil == null) return;
            float height = SystemAPI.HasSingleton<ArenaGroundPlane>()
                ? SystemAPI.GetSingleton<ArenaGroundPlane>().SurfaceHeight : 0f;
            var camera = Camera.main;
            var right = camera != null ? camera.transform.right : Vector3.right;
            var up = camera != null ? camera.transform.up : Vector3.up;
            burrows.Clear();
            foreach (var (warning, entity) in SystemAPI.Query<RefRO<EnemySpawnWarning>>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess())
            {
                var w = warning.ValueRO;
                // INFO-006: simulation time keeps digging alive while clearance delays activation.
                float age = math.max(0f, EnemySpawnWarning.Duration - w.SecondsRemaining);
                uint seed = math.hash(new uint2((uint)entity.Index, (uint)entity.Version));
                burrows.Add(new Vector3(w.Position.x, height + .025f, w.Position.z),
                    w.Radius, age, seed, right, up);
            }
            burrows.Draw(soil, library != null ? library.SporeCloud : null);
        }

        protected override void OnStopRunning() => burrows.Clear();
        protected override void OnDestroy() => burrows.Dispose();
    }
}
