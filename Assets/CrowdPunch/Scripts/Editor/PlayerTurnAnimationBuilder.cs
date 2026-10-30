using System;
using System.Linq;
using CrowdPunch.Mono.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static class PlayerTurnAnimationBuilder
    {
        private const string Folder = "Assets/CrowdPunch/Animation/";

        [MenuItem("Crowd Punch/Presentation/Apply Player Standing Turns")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Apply standing turns outside Play mode.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "PlayerMovement.controller");
            if (!controller.parameters.Any(p => p.name == "TurnSpeed"))
                controller.AddParameter("TurnSpeed", AnimatorControllerParameterType.Float);
            // Leg-only masks discard the source pelvis while retaining its foot IK goals.
            // Evaluate complete lower-body poses on the base layer; existing upper layers
            // and PlayerPunchAnimation's spine correction preserve independent punching.
            for (int i = controller.layers.Length - 1; i >= 0; i--)
                if (controller.layers[i].name == "Standing Turn")
                    controller.RemoveLayer(i);
            var machine = controller.layers[0].stateMachine;

            foreach (string name in new[] { "Left Turn", "Right Turn" })
            {
                string sourcePath = Folder + name + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(sourcePath);
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.SaveAndReimport();
                }
                var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
                    .First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
                string path = Folder + "Player" + name.Replace(" ", string.Empty) + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    clip = UnityEngine.Object.Instantiate(source);
                    AssetDatabase.CreateAsset(clip, path);
                }
                else EditorUtility.CopySerialized(source, clip);
                clip.name = name;
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                settings.loopBlend = false;
                settings.loopBlendOrientation = false;
                settings.loopBlendPositionXZ = false;
                settings.loopBlendPositionY = true;
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionXZ = true;
                settings.keepOriginalPositionY = false;
                settings.heightFromFeet = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                EditorUtility.SetDirty(clip);

                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name)
                    ?? machine.AddState(name);
                state.motion = clip;
                state.speedParameter = "TurnSpeed";
                state.speedParameterActive = true;
                state.writeDefaultValues = false;
                state.iKOnFeet = true;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(controller);
            const string prefabPath = "Assets/CrowdPunch/Prefabs/Models/PlayerModel.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponent<PlayerTurnAnimation>() == null)
                    root.AddComponent<PlayerTurnAnimation>();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
    }
}
