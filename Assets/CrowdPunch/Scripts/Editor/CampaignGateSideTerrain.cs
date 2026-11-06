using System;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static class CampaignGateSideTerrain
    {
        [MenuItem("Crowd Punch/Campaign/Seal Gate Side Terrain")]
        public static void ApplyAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before authoring gate terrain.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before authoring gate terrain.");

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (int level in GauntletProgressionBuilder.CampaignBarricadeLevels)
                {
                    var scene = EditorSceneManager.OpenScene(
                        $"Assets/CrowdPunch/Scenes/Campaign/Campaign_{level:00}/Campaign_{level:00} Sub Scene.unity");
                    foreach (var side in scene.GetRootGameObjects()
                        .SelectMany(g => g.GetComponentsInChildren<SolidObstacleAuthoring>())
                        .Where(o => o.name.StartsWith("Gate terrain")))
                        SquareSurface(side);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        internal static void SquareSurface(SolidObstacleAuthoring side)
        {
            // BARRICADE-003/006/007: tapered rocks do not fill the navigation rectangle.
            // A visible cuboid seals its full footprint; the existing baker derives collision
            // from this same surface. Keep the chapter material and the gate's exposed front.
            var surface = side.GetComponentsInChildren<MeshFilter>().Single();
            surface.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            surface.transform.localPosition = Vector3.up * side.height * .5f;
            surface.transform.localRotation = Quaternion.identity;
            surface.transform.localScale = new Vector3(side.Size.x, side.height, side.Size.y);
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { renderer.sharedMaterial };
            EditorUtility.SetDirty(surface);
            EditorUtility.SetDirty(surface.transform);
            EditorUtility.SetDirty(renderer);
        }
    }
}
