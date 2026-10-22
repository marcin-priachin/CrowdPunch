using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static class PlayerDashAnimationBuilder
    {
        [MenuItem("Crowd Punch/Presentation/Apply Player Dash Animations")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Apply dash animations outside Play mode.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/CrowdPunch/Animation/PlayerMovement.controller");
            for (int i = controller.parameters.Length - 1; i >= 0; i--)
                if (controller.parameters[i].name == "DashTime")
                    controller.RemoveParameter(i);
            foreach (string parameter in new[] { "DashX", "DashZ", "DashSpeed" })
                if (!controller.parameters.Any(p => p.name == parameter))
                    controller.AddParameter(parameter, AnimatorControllerParameterType.Float);

            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Dash")
                ?? machine.AddState("Dash", new Vector3(420f, 180f));
            var tree = state.motion as BlendTree;
            if (tree == null)
            {
                tree = new BlendTree { name = "Directional Dash" };
                AssetDatabase.AddObjectToAsset(tree, controller);
            }
            tree.blendType = BlendTreeType.SimpleDirectional2D;
            tree.blendParameter = "DashX";
            tree.blendParameterY = "DashZ";
            string[] directions = { "Forward", "Backward", "Left", "Right" };
            Vector2[] positions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            var children = new ChildMotion[4];
            for (int i = 0; i < directions.Length; i++)
            {
                string sourcePath = "Assets/CrowdPunch/Animation/Dash/Standing Dodge " + directions[i] + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(sourcePath);
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.SaveAndReimport();
                }
                var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
                    .First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
                string path = "Assets/CrowdPunch/Animation/Dash/Dash" + directions[i] + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    clip = UnityEngine.Object.Instantiate(source);
                    AssetDatabase.CreateAsset(clip, path);
                }
                else EditorUtility.CopySerialized(source, clip);
                clip.name = "Dash " + directions[i];
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                settings.loopBlend = false;
                settings.loopBlendOrientation = false;
                settings.loopBlendPositionXZ = false;
                settings.loopBlendPositionY = true;
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionXZ = true;
                settings.keepOriginalPositionY = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                EditorUtility.SetDirty(clip);
                // Equal one-second child durations keep diagonals synchronized even
                // when the supplied forward/backward/side clips have different lengths.
                children[i] = new ChildMotion { motion = clip, position = positions[i], timeScale = clip.length };
            }
            tree.children = children;
            state.motion = tree;
            state.timeParameter = string.Empty;
            state.timeParameterActive = false;
            state.speed = 1f;
            state.speedParameter = "DashSpeed";
            state.speedParameterActive = true;
            state.writeDefaultValues = false;
            state.iKOnFeet = true;
            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(controller), ImportAssetOptions.ForceUpdate);
        }
    }
}
