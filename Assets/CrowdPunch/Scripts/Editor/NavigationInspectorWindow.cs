using CrowdPunch.Components;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
namespace CrowdPunch.Editor
{
    public sealed class NavigationInspectorWindow : EditorWindow
    {
        private bool draw; private int clearanceClass; private double nextPaint;
        [MenuItem("Crowd Punch/Navigation/Inspect")]
        public static void Open() => GetWindow<NavigationInspectorWindow>("Navigation");
        private void OnEnable() { SceneView.duringSceneGui += Draw; EditorApplication.update += Tick; }
        private void OnDisable() { SceneView.duringSceneGui -= Draw; EditorApplication.update -= Tick; }
        private void Tick() { if (EditorApplication.timeSinceStartup < nextPaint) return; nextPaint = EditorApplication.timeSinceStartup + .2; Repaint(); if (draw) SceneView.RepaintAll(); }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Asset settings are baked. Only the runtime assistance and drawing toggles below are live; they reset on reload. Grid / search capacities require rebaking.", MessageType.Info);
            draw = EditorGUILayout.Toggle("Draw Scene view", draw); clearanceClass = EditorGUILayout.IntSlider("Clearance class", clearanceClass, 0, 2);
            var world = World.DefaultGameObjectInjectionWorld; if (world == null || !world.IsCreated) return;
            using var q = world.EntityManager.CreateEntityQuery(typeof(NavigationGrid), typeof(NavigationRuntimeSettings), typeof(NavigationDiagnostics)); if (q.CalculateEntityCount() != 1) return;
            var settings = q.GetSingleton<NavigationRuntimeSettings>(); bool enabled = EditorGUILayout.Toggle("Runtime assistance", settings.Enabled != 0);
            if (enabled != (settings.Enabled != 0)) { settings.Enabled = enabled ? (byte)1 : (byte)0; world.EntityManager.SetComponentData(q.GetSingletonEntity(), settings); }
            var d = q.GetSingleton<NavigationDiagnostics>();
            EditorGUILayout.LabelField($"Queued {d.Queued} / searching {d.Searching} / expanded {d.Expanded}");
            EditorGUILayout.LabelField($"Direct {d.Direct} / path {d.Following} / waiting {d.Waiting}");
            EditorGUILayout.LabelField($"Failures {d.Failures} / resource limits {d.LimitReached} / replans {d.Replans}");
            EditorGUILayout.LabelField($"Discarded stale {d.StaleResults} / rejected spawns {d.RejectedSpawns}");
            EditorGUILayout.LabelField($"Navigation CPU {d.Milliseconds:F3} ms (last recorded frame)");
            EditorGUILayout.HelpBox("Green: direct. Cyan: path and waypoint. Yellow: pending. Magenta: failed. Cells are colored by reachable region; red cells are blocked for the selected radius.", MessageType.None);
        }
        private void Draw(SceneView view)
        {
            var world = World.DefaultGameObjectInjectionWorld; if (world == null || !world.IsCreated) return;
            var em = world.EntityManager; using var q = em.CreateEntityQuery(typeof(NavigationGrid)); if (q.CalculateEntityCount() != 1) return;
            if (!draw && em.GetComponentData<NavigationRuntimeSettings>(q.GetSingletonEntity()).DebugDrawing == 0) return;
            ref var g = ref q.GetSingleton<NavigationGrid>().Data.Value;
            int count = g.Size.x * g.Size.y;
            for (int i = 0; i < count; i++)
            {
                int region = NavigationGeometry.Region(ref g, i, clearanceClass); float2 c = NavigationGeometry.Center(ref g, i);
                Handles.color = region == 0 ? new Color(1, .1f, .1f, .2f) : Color.HSVToRGB((region * .217f) % 1, .45f, .8f);
                Handles.DrawWireCube(new Vector3(c.x, -.94f, c.y), new Vector3(g.CellSize, .01f, g.CellSize));
            }
            using var actors = em.CreateEntityQuery(typeof(NavigationPathState), typeof(NavigationIntent), typeof(LocalTransform));
            using var entities = actors.ToEntityArray(Allocator.Temp);
            foreach (var e in entities)
            {
                var n = em.GetComponentData<NavigationPathState>(e); if (n.Initialized == 0) continue;
                var pos = (Vector3)em.GetComponentData<LocalTransform>(e).Position; var goal = em.GetComponentData<NavigationIntent>(e).Destination;
                Handles.color = n.Pending != 0 ? Color.yellow : n.TravelState == NavigationTravelState.Failed ? Color.magenta : n.TravelState == NavigationTravelState.Direct ? Color.green : Color.cyan;
                Handles.DrawDottedLine(pos, (Vector3)goal, 5); Handles.DrawWireDisc(new Vector3(n.ResolvedGoal.x, pos.y, n.ResolvedGoal.y), Vector3.up, .3f);
                var path = em.GetBuffer<NavigationWaypoint>(e);
                for (int i = n.Waypoint; i < path.Length; i++) { var p = new Vector3(path[i].Position.x, pos.y, path[i].Position.y); Handles.DrawLine(pos, p); if (i == n.Waypoint) Handles.DrawWireDisc(p, Vector3.up, .2f); pos = p; }
            }
        }
    }
}
