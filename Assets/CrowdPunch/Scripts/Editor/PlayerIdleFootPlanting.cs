using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static class PlayerIdleFootPlanting
    {
        [MenuItem("Crowd Punch/Presentation/Plant Player Idle Feet")]
        public static void ApplyToPlayer()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Apply the saved idle stance outside Play mode.");
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/CrowdPunch/Animation/HeavyFighter/Idle.anim");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/CrowdPunch/Animation/PlayerMovement.controller");
            Apply(idle, controller);
            AssetDatabase.SaveAssetIfDirty(idle);
            AssetDatabase.SaveAssetIfDirty(controller);
        }

        internal static void Apply(AnimationClip idle, AnimatorController controller)
        {
            // VISION-005, PLAYER-002: root/body motion changes foot IK's reference frame.
            // Stabilize that frame and both goals together; keeping only the goals static
            // still makes the feet orbit as the animated pelvis turns.
            foreach (var binding in AnimationUtility.GetCurveBindings(idle))
            {
                string property = binding.propertyName;
                if (!property.StartsWith("RootT.", StringComparison.Ordinal)
                    && !property.StartsWith("RootQ.", StringComparison.Ordinal)
                    && !property.StartsWith("LeftFootT.", StringComparison.Ordinal)
                    && !property.StartsWith("LeftFootQ.", StringComparison.Ordinal)
                    && !property.StartsWith("RightFootT.", StringComparison.Ordinal)
                    && !property.StartsWith("RightFootQ.", StringComparison.Ordinal))
                    continue;
                var curve = AnimationUtility.GetEditorCurve(idle, binding);
                AnimationUtility.SetEditorCurve(idle, binding,
                    AnimationCurve.Constant(0f, idle.length, curve.Evaluate(0f)));
            }

            var settings = AnimationUtility.GetAnimationClipSettings(idle);
            settings.loopBlendPositionY = false;
            settings.keepOriginalPositionY = false;
            settings.heightFromFeet = true;
            AnimationUtility.SetAnimationClipSettings(idle, settings);
            var locomotion = controller.layers[0].stateMachine.defaultState;
            locomotion.iKOnFeet = true;
            EditorUtility.SetDirty(idle);
            EditorUtility.SetDirty(locomotion);
            EditorUtility.SetDirty(controller);
        }
    }
}
