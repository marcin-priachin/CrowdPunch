using System;
using System.Linq;
using CrowdPunch.Mono.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static class PlayerAppearanceBuilder
    {
        private const string ModelPath = "Assets/CrowdPunch/Models/Player/rig.fbx";
        private const string PrefabPath = "Assets/CrowdPunch/Prefabs/Models/PlayerModel.prefab";
        private const string AnimationFolder = "Assets/CrowdPunch/Animation/HeavyFighter";
        private const string SourceFolder = "Assets/ExplosiveLLC/Fighter Pack Bundle FREE/Fighters/Heavy Fighter Mecanim Animation Pack FREE/Animations/";
        private const string MaterialPath = "Assets/CrowdPunch/Data/Presentation/Materials/PlayerFighter.mat";

        [MenuItem("Crowd Punch/Presentation/Rebuild Player Appearance")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Rebuild the saved player outside Play mode.");
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
                AssetDatabase.CreateFolder("Assets/CrowdPunch/Animation", "HeavyFighter");
            var idle = CopyClip("Idle", true);
            var forward = CopyClip("WalkForward", true);
            var backward = CopyClip("WalkBackward", true);
            var punch = CopyClip("Punch", false);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/CrowdPunch/Animation/PlayerMovement.controller");
            PlayerIdleFootPlanting.Apply(idle, controller);
            var tree = (BlendTree)controller.layers[0].stateMachine.defaultState.motion;
            var children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].position == Vector2.zero) children[i].motion = idle;
                else if (children[i].position == Vector2.up) { children[i].motion = forward; children[i].timeScale = 1.35f; }
                else if (children[i].position == Vector2.down) { children[i].motion = backward; children[i].timeScale = 1.35f; }
            }
            tree.children = children;
            controller.layers.First(l => l.name == "Upper Body Punch").stateMachine.defaultState.motion = punch;
            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(controller);

            var material = BuildMaterial();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // Retain the prefab root/components and their file IDs for scene references.
                foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                var rig = UnityEngine.Object.Instantiate(source);
                var avatar = rig.GetComponent<Animator>().avatar;
                foreach (Transform child in rig.transform.Cast<Transform>().ToArray())
                    child.SetParent(root.transform, false);
                UnityEngine.Object.DestroyImmediate(rig);
                root.transform.localScale = Vector3.one * 2.35f;
                var animator = root.GetComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // VISION-005/006: authored silhouette accents, with no physics/root changes.
                SetScale(root, "mixamorig:Spine2", new Vector3(1.12f, 1.04f, 1.08f));
                SetScale(root, "mixamorig:Head", Vector3.one * 1.16f);
                SetScale(root, "mixamorig:LeftHand", Vector3.one * 1.4f);
                SetScale(root, "mixamorig:RightHand", Vector3.one * 1.4f);
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    renderer.localBounds = new Bounds(new Vector3(0f, .45f, .08f), new Vector3(1.8f, 1.4f, 1.8f));
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                var serialized = new SerializedObject(root.GetComponent<PlayerPunchAnimation>());
                serialized.FindProperty("punchClip").objectReferenceValue = punch;
                serialized.FindProperty("readyFrame").floatValue = 2f;
                serialized.FindProperty("contactFrame").floatValue = 6f;
                serialized.FindProperty("punchRotationEndFrame").floatValue = 8f;
                serialized.FindProperty("punchSpeedMultiplier").floatValue = 1.35f;
                serialized.FindProperty("strikeSpeed").floatValue = 1.85f;
                serialized.FindProperty("followThroughSpeed").floatValue = .85f;
                serialized.FindProperty("readyYaw").floatValue = -8f;
                serialized.FindProperty("punchYaw").floatValue = 5f;
                BakeSpineReference(root, punch, serialized.FindProperty("spineReferenceRotations"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            PlayerDashAnimationBuilder.Apply();
        }

        private static AnimationClip CopyClip(string name, bool loop)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(SourceFolder + "Heavy@" + name + ".FBX")
                .OfType<AnimationClip>().First(c => c.name == name);
            var path = AnimationFolder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
            else EditorUtility.CopySerialized(source, clip);
            clip.name = "Heavy Fighter " + name;
            // Own the in-place clip configuration; preserve the supplied FBXs/importers.
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            // Extract horizontal travel; applyRootMotion=false discards it instead of baking a lunge into the skin.
            settings.loopBlendPositionXZ = false;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static Material BuildMaterial()
        {
            const string textures = "Assets/CrowdPunch/Models/Player/texture/";
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(textures + "normal.png");
            if (normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("CrowdPunch/Cel/Environment")) { name = "Player Fighter Cel" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textures + "base.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textures + "normal.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_BumpScale", .22f);
            material.SetFloat("_Smoothness", .08f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetScale(GameObject root, string bone, Vector3 scale)
        {
            root.GetComponentsInChildren<Transform>().First(t => t.name == bone).localScale = scale;
        }

        private static void BakeSpineReference(GameObject root, AnimationClip punch, SerializedProperty rotations)
        {
            var preview = UnityEngine.Object.Instantiate(root);
            try
            {
                foreach (var behaviour in preview.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                var animator = preview.GetComponent<Animator>();
                animator.Rebind();
                animator.SetFloat("MoveX", 0f);
                animator.SetFloat("MoveZ", 0f);
                var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                int samples = Mathf.CeilToInt(punch.length * 60f) + 1;
                rotations.arraySize = samples;
                for (int i = 0; i < samples; i++)
                {
                    animator.SetFloat("PunchTime", (float)i / (samples - 1));
                    animator.Play("Base Layer.Locomotion", 0, 0f);
                    animator.Update(.0001f);
                    rotations.GetArrayElementAtIndex(i).quaternionValue =
                        Quaternion.Inverse(animator.transform.rotation) * spine.rotation;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(preview); }
        }
    }
}
