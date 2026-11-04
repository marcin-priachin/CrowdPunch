using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace CrowdPunch.Bakers
{
    public sealed class NavigationArenaBaker : Baker<NavigationArenaAuthoring>
    {
        public override void Bake(NavigationArenaAuthoring authoring)
        {
            if (authoring.settings == null) throw new System.InvalidOperationException("Navigation arena needs NavigationSettings.");
            DependsOn(authoring.settings); var arena = GetComponent<ArenaAuthoring>();
            var obstacles = GetComponentsInChildren<SolidObstacleAuthoring>();
            var covers = GetComponentsInChildren<RotatingCoverAuthoring>();
            var shells = GetComponentsInChildren<ShellTargetAuthoring>();
            var perimeter = authoring.perimeterColliders;
            int perimeterStart = obstacles.Length + covers.Length + shells.Length;
            using var rectangleStorage = new NativeList<NavigationRectangle>(perimeterStart + (perimeter?.Length ?? 0), Allocator.Temp);
            var rectangles = rectangleStorage;
            rectangles.ResizeUninitialized(perimeterStart);
            for (int i = 0; i < obstacles.Length; i++)
            {
                var obstacle = obstacles[i]; DependsOn(obstacle); DependsOn(obstacle.transform);
                float2 p = new float2(obstacle.transform.position.x, obstacle.transform.position.z);
                float2 half = new float2(obstacle.Size.x, obstacle.Size.y) * .5f;
                rectangles[i] = new NavigationRectangle { Minimum = p - half, Maximum = p + half };
                foreach (var filter in GetComponentsInChildren<UnityEngine.MeshFilter>(obstacle))
                {
                    var renderer = GetComponent<UnityEngine.MeshRenderer>(filter);
                    if (renderer == null) continue;
                    DependsOn(renderer);
                    if (!IsActiveAndEnabled(renderer) || renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly || filter.sharedMesh == null) continue;
                    DependsOn(filter); DependsOn(filter.transform); DependsOn(filter.sharedMesh);
                    // Include protruding low geometry without allowing an overhead tree canopy
                    // to close a ground-level route. The authored footprint remains the minimum.
                    rectangles[i] = SolidObstacleGeometry.ExpandNavigationFootprint(rectangles[i], filter,
                        obstacle.transform.position.y, obstacle.transform.position.y + obstacle.height);
                }
            }
            for (int i = 0; i < covers.Length; i++)
            {
                var cover = covers[i]; DependsOn(cover); DependsOn(cover.transform);
                if (cover.settings == null) throw new System.InvalidOperationException("Cover navigation requires settings.");
                DependsOn(cover.settings);
                float2 centerOfCover = new float2(cover.transform.position.x, cover.transform.position.z);
                float radius = math.max(3, cover.settings.radius) + math.clamp(cover.settings.thickness, .1f, 1);
                rectangles[obstacles.Length + i] = new NavigationRectangle { Minimum = centerOfCover - radius, Maximum = centerOfCover + radius };
            }
            for (int i = 0; i < shells.Length; i++)
            {
                var shell = shells[i]; DependsOn(shell.transform);
                var solid = GetComponent<BarricadeAuthoring>(shell); DependsOn(solid);
                float2 p = ((float3)shell.transform.position).xz;
                float2 half = ((float3)solid.size).xz * .5f;
                rectangles[obstacles.Length + covers.Length + i] = new NavigationRectangle { Minimum = p - half, Maximum = p + half };
            }
            if (perimeter != null)
            {
                foreach (var collider in perimeter)
                {
                    if (collider == null) continue;
                    DependsOn(collider);
                    DependsOn(collider.transform);
                    var mesh = collider.sharedMesh;
                    if (!IsActiveAndEnabled(collider) || !collider.enabled || collider.isTrigger || mesh == null) continue;
                    DependsOn(mesh);
                    rectangles.Add(PerimeterFootprint(collider.transform, mesh.bounds));
                }
            }
            float2 center = new float2(authoring.transform.position.x, authoring.transform.position.z) + arena.SpacingCenterOffset.xz;
            float3 radii = (float3)authoring.settings.clearanceRadii + authoring.settings.clearanceMargin;
            var blob = NavigationGridConstruction.Build(center - arena.SpacingSize.xz * .5f, center + arena.SpacingSize.xz * .5f,
                authoring.settings.cellSize, radii, rectangles.AsArray(), Allocator.Persistent);
            blob.Value.Margin = authoring.settings.clearanceMargin;
            bool anchorValid = NavigationGeometry.TryResolveParticipationAnchor(ref blob.Value,
                authoring.overrideParticipationAnchor, authoring.participationAnchor, out float2 anchor);
            AddBlobAsset(ref blob, out _); var entity = GetEntity(TransformUsageFlags.WorldSpace);
            AddComponent(entity, new NavigationGrid { Data = blob, ParticipationAnchor = anchor });
            AddComponent(entity, authoring.settings.Runtime); AddComponent<NavigationDiagnostics>(entity);
            if (!anchorValid)
                UnityEngine.Debug.LogError(authoring.overrideParticipationAnchor
                    ? "Navigation participation anchor override is outside the spacing bounds or lacks clearance. Move it to open ground clear for every configured radius class, or disable the override."
                    : "Navigation spacing bounds contain no participation anchor clear for every configured radius class. Check obstacle footprints, bounds, cell size, and clearance settings.", authoring);
        }

        private static NavigationRectangle PerimeterFootprint(UnityEngine.Transform transform, UnityEngine.Bounds bounds)
        {
            // Project the collider mesh bounds in the wall's frame. A world AABB would
            // cover usable floor on the inside of a long diagonal wall.
            var footprint = new NavigationRectangle
            {
                Axis = math.normalizesafe(((float3)transform.right).xz, new float2(1f, 0f)),
                Minimum = new float2(float.PositiveInfinity),
                Maximum = new float2(float.NegativeInfinity)
            };
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var corner = bounds.center + UnityEngine.Vector3.Scale(bounds.extents, new UnityEngine.Vector3(x, y, z));
                float2 projected = footprint.Project(((float3)transform.TransformPoint(corner)).xz);
                footprint.Minimum = math.min(footprint.Minimum, projected);
                footprint.Maximum = math.max(footprint.Maximum, projected);
            }
            return footprint;
        }
    }
}
