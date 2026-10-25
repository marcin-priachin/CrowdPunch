using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // Appearance-only migration. Encounter geometry, settings, waves and scene identities stay authored.
    public static class BossAppearanceBuilder
    {
        private const string Root = "Assets/CrowdPunch/";
        private const string GateArt = Root + "Data/Boss/";
        public static readonly string[] ScenePaths = Enumerable.Range(1, 8)
            .Select(i => $"{Root}Scenes/Campaign/Campaign_{i * 10:00}/Campaign_{i * 10:00} Sub Scene.unity")
            .Concat(new[] { 11, 20, 21, 22 }.Select(i => $"{Root}Scenes/Gauntlets/Gauntlet_{i:00}/Gauntlet_{i:00} Sub Scene.unity"))
            .Append(Root + "Scenes/Campaign/Campaign_81.unity").ToArray();

        [MenuItem("Crowd Punch/Bosses/Apply Boss Appearance")]
        public static void ApplyAll()
        {
            foreach (string path in ScenePaths) ApplyScene(path);
        }

        public static void ApplyScene(string path)
        {
            if (!ScenePaths.Contains(path)) throw new ArgumentException("Expected an existing boss scene.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var head in root.GetComponentsInChildren<BossEncounterAuthoring>(true))
                    {
                        RemoveModel(head.transform, "Orc Head Visual", "Gatekeeper Model");
                        AddGatekeeper(head.gameObject);
                    }
                    foreach (var boss in root.GetComponentsInChildren<ChickenBossAuthoring>(true))
                    {
                        RemoveModel(boss.transform, "Chicken Model");
                        GauntletProgressionBuilder.AddChickenModel(boss.gameObject, boss.settings);
                    }
                    foreach (var boss in root.GetComponentsInChildren<RollingBossAuthoring>(true))
                    {
                        RemoveModel(boss.transform, "Green Spiky Blob Model", "Spike Model");
                        GauntletProgressionBuilder.AddRollingModel(boss.gameObject, boss.settings);
                    }
                    foreach (var boss in root.GetComponentsInChildren<DinoBossAuthoring>(true))
                    {
                        RemoveModel(boss.transform, "Dino Model");
                        GauntletProgressionBuilder.AddDinoModel(boss.gameObject, boss.settings);
                    }
                    foreach (var finale in root.GetComponentsInChildren<FinalePresentation>(true)) ConfigureFinale(finale);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static void RemoveModel(Transform root, params string[] names)
        {
            foreach (Transform child in root.Cast<Transform>().ToArray())
                if (names.Contains(child.name)) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        public static void AddGatekeeper(GameObject root)
        {
            GatekeeperMagmaBuilder.AddModel(root);
        }

        // Use the exported skin positions, not BakeMesh bounds (FBX armatures can carry 100x scale).
        internal static void VisitSamples(GameObject visual, Action<Vector3> visit, int motions = 4)
        {
            foreach (var authoring in visual.GetComponentsInChildren<EnemyAnimationAuthoring>())
            {
                var renderer = authoring.GetComponent<SkinnedMeshRenderer>();
                using var reader = new BinaryReader(new MemoryStream(authoring.Samples.bytes));
                reader.ReadInt32(); reader.ReadString();
                int bones = reader.ReadInt32(), frames = reader.ReadInt32();
                for (int i = 0; i < bones; i++) reader.ReadString();
                for (int i = 0; i < EnemyAnimationSamples.MotionCount; i++) reader.ReadSingle();
                var matrices = new Matrix4x4[bones];
                var vertices = renderer.sharedMesh.vertices; var weights = renderer.sharedMesh.boneWeights;
                for (int frame = 0; frame < frames * motions; frame++)
                {
                    for (int bone = 0; bone < bones; bone++)
                    {
                        var matrix = Matrix4x4.identity;
                        for (int col = 0; col < 4; col++) for (int row = 0; row < 3; row++) matrix[row, col] = reader.ReadSingle();
                        matrices[bone] = matrix;
                    }
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var w = weights[i]; var v = vertices[i];
                        visit(matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                            + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3);
                    }
                }
            }
        }

        public static void GroundModel(GameObject visual, float bodyHeight)
        {
            var toOwner = visual.transform.localToWorldMatrix;
            toOwner = visual.transform.parent.worldToLocalMatrix * toOwner;
            float minimum = float.PositiveInfinity;
            VisitSamples(visual, point => minimum = Mathf.Min(minimum, toOwner.MultiplyPoint3x4(point).y));
            visual.transform.localPosition += Vector3.up * (-bodyHeight * .5f + .08f - minimum);
            // Reserve space for the largest procedural silhouette accent and transitions.
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var bounds = renderer.localBounds; bounds.Expand(bounds.size * .4f + Vector3.one * .2f); renderer.localBounds = bounds;
            }
        }

        public static void ConfigureFinale(FinalePresentation finale) => FinaleWraithBuilder.Configure(finale);
    }
}
