using System;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static class GatekeeperMagmaBuilder
    {
        public const string Source = "Assets/Polygonal Magma/Prefabs/Polygonal Magma Orange.prefab";
        public const string Art = "Assets/CrowdPunch/Data/Boss/";
        public static readonly string[] Scenes = {
            "Assets/CrowdPunch/Scenes/Campaign/Campaign_10/Campaign_10 Sub Scene.unity",
            "Assets/CrowdPunch/Scenes/Campaign/Campaign_50/Campaign_50 Sub Scene.unity",
            "Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_11/Gauntlet_11 Sub Scene.unity"
        };

        [MenuItem("Crowd Punch/Bosses/Apply Magma Gatekeeper")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in Scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var boss in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossEncounterAuthoring>(true)))
                        AddModel(boss.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static void AddModel(GameObject root)
        {
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (child.name == "Gatekeeper Model" || child.name == "Orc Head Visual") UnityEngine.Object.DestroyImmediate(child.gameObject);
            var encounter = root.GetComponent<BossEncounterAuthoring>();
            foreach (var hand in new[] { encounter.leftHand, encounter.rightHand })
                if (hand != null)
                    foreach (var handRenderer in hand.GetComponentsInChildren<MeshRenderer>(true)) UnityEngine.Object.DestroyImmediate(handRenderer.gameObject);

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source), root.transform);
            visual.name = "Gatekeeper Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            var animator = visual.GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var renderer = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            // Keep the previous head's 4.4m authored silhouette; hands are fitted independently to their existing hit volumes.
            visual.transform.localScale *= 4.4f / renderer.bounds.size.y;
            string[] names = { "Idle", "Move Forward WO Root", "Explode", "Take Damage", "Die",
                "Cast Spell", "Punch Attack", "Swing Attack", "Charge", "Spawn", "Idle" };
            var all = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Polygonal Magma/FBX" })
                .Select(AssetDatabase.GUIDToAssetPath).SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__")).ToArray();
            var clips = names.Select(n => all.First(c => c.name == n)).ToArray();
            string controllerPath = Art + "Gatekeeper.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            foreach (var clip in clips.Distinct()) machine.AddState(clip.name).motion = clip;
            EditorUtility.SetDirty(controller);
            animator.runtimeAnimatorController = controller;
            renderer.rootBone = animator.transform;
            renderer.quality = SkinQuality.Bone4;
            renderer.sharedMaterials = renderer.sharedMaterials.Select(Material).ToArray();
            AssetDatabase.SaveAssets();
            string samples = Art + "GatekeeperMovement.bytes";
            GauntletProgressionBuilder.WriteBossSamples(animator, renderer, clips, samples, controllerPath);
            var animation = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
            animation.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(samples);
            BossAppearanceBuilder.GroundModel(visual, root.GetComponent<BossPartAuthoring>().height);
            renderer.gameObject.AddComponent<GatekeeperRigAuthoring>();
            FitHands(encounter);
            // The ECS hands can travel throughout the encounter. Culling bounds must include that entire envelope.
            var tuning = encounter.settings.Bake();
            float radius = new Vector2(tuning.BoundsExtents.x, tuning.BoundsExtents.y).magnitude * 2 + 10;
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * radius * 2 / visual.transform.localScale.x);
            root.name = "Boss Head - Magma";
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
            GatekeeperEffectsBuilder.ConfigureAmbient(root);
        }

        private static Material Material(Material source)
        {
            string path = Art + "Gatekeeper Magma Cel.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("CrowdPunch/Cel/Enemy")) { name = "Gatekeeper Magma Cel" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("CrowdPunch/Cel/Enemy");
            material.SetTexture("_ColorMap", source.mainTexture);
            material.SetColor("_BaseColor", new Color(3, 3, 3, 1));
            foreach (string property in new[] { "_SkinColorAmount", "_DarkAmount", "_DirtAmount", "_EyelinerAmount" })
                material.SetFloat(property, 0);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void FitHands(BossEncounterAuthoring encounter)
        {
            var rig = encounter.GetComponentInChildren<GatekeeperRigAuthoring>(true);
            var renderer = rig.GetComponent<SkinnedMeshRenderer>();
            var arena = encounter.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaAuthoring>(true)).SingleOrDefault();
            float floor = (arena != null ? arena.GroundSurfaceHeight : -1f) + rig.groundClearance;
            var tuning = encounter.settings.Bake();
            var hands = new[] { encounter.leftHand, encounter.rightHand };
            var sizes = new[] { rig.leftFistScale, rig.rightFistScale };
            for (int i = 0; i < hands.Length; i++)
                hands[i].transform.position = CrowdPunch.Systems.Movement.BossPerimeterRoute.ClampHand(hands[i].transform.position, tuning,
                    Mathf.Max(hands[i].radius, hands[i].radius * sizes[i]) + .05f);
            FitArm(renderer, "RigLArm1", "RigLArmPalm", encounter.leftHand, floor, rig.leftFistScale);
            FitArm(renderer, "RigRArm1", "RigRArmPalm", encounter.rightHand, floor, rig.rightFistScale);
        }

        private static void FitArm(SkinnedMeshRenderer renderer, string rootName, string palmName, BossPartAuthoring hand, float floor, float sizeMultiplier)
        {
            if (hand == null) throw new InvalidOperationException("The Magma rig requires both physical hand owners.");
            var arm = renderer.bones.First(b => b.name == rootName);
            var palm = renderer.bones.First(b => b.name == palmName);
            var vertices = renderer.sharedMesh.vertices;
            var weights = renderer.sharedMesh.boneWeights;
            var poses = renderer.sharedMesh.bindposes;
            var matrices = renderer.bones.Select((b, i) => b.localToWorldMatrix * poses[i]).ToArray();
            var mask = renderer.bones.Select(b => b == arm || b.IsChildOf(arm)).ToArray();
            var points = new System.Collections.Generic.List<Vector3>();
            bool any = false; Bounds bounds = default;
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                if ((mask[w.boneIndex0] ? w.weight0 : 0) + (mask[w.boneIndex1] ? w.weight1 : 0)
                    + (mask[w.boneIndex2] ? w.weight2 : 0) + (mask[w.boneIndex3] ? w.weight3 : 0) < .5f) continue;
                var v = vertices[i];
                Vector3 p = matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                    + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                points.Add(p);
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
            }
            float radius = points.Max(p => new Vector2(p.x - bounds.center.x, p.z - bounds.center.z).magnitude);
            float scale = hand.radius * Mathf.Max(.1f, sizeMultiplier) / Mathf.Max(.01f, radius);
            Vector3 fittedCenter = arm.position + (bounds.center - arm.position) * scale;
            float fittedBottom = arm.position.y + (bounds.min.y - arm.position.y) * scale;
            arm.localScale *= scale;
            Vector3 offset = new Vector3(hand.transform.position.x - fittedCenter.x,
                hand.transform.position.y - palm.position.y, hand.transform.position.z - fittedCenter.z);
            offset.y += Mathf.Max(0, floor - (fittedBottom + offset.y));
            arm.position += offset;
            PrefabUtility.RecordPrefabInstancePropertyModifications(arm);
        }
    }
}
