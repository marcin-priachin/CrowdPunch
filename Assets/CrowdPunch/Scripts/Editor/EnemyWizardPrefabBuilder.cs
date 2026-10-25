using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static class EnemyWizardPrefabBuilder
    {
        public const string ModelPath = "Assets/Bud Cute Series/Prefabs/Bud.prefab";
        private const string AnimationFolder = "Assets/Bud Cute Series/FBX";
        private const string PrefabPath = "Assets/CrowdPunch/Prefabs/EnemyWizard.prefab";
        private const string ControllerPath = "Assets/CrowdPunch/Animation/EnemyWizard.controller";
        private const string SettingsPath = "Assets/CrowdPunch/Data/Settings/Enemies/WizardEnemySpawnSettings.asset";
        public const string MaterialFolder = "Assets/CrowdPunch/Materials/Enemies/Wizard";
        private const string SkinningMaterialPath = "Assets/CrowdPunch/Animation/EnemySkinning.mat";
        private const string PhysicsMaterialPath = "Assets/CrowdPunch/PhysicsMaterial/Enemy.physicMaterial";
        private const int FrameCount = 32;

        [MenuItem("Crowd Punch/Enemies/Rebuild Wizard Prefab")]
        public static void Rebuild()
        {
            EnsureFolder("Assets/CrowdPunch/Materials/Enemies", "Wizard");
            EnsureZoneMaterial();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rebuild the Wizard outside Play Mode.");
            ConfigureAnimationImport();

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException($"Missing Wizard model: {ModelPath}");
            AnimationClip[] clips = FindClips();
            AnimatorController controller = BuildController(clips);

            GameObject root = new GameObject("EnemyWizard") { layer = 7 };
            try
            {
                ConfigurePhysics(root);
                root.AddComponent<EnemyAuthoring>();
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                Transform[] authoredTransforms = visual.GetComponentsInChildren<Transform>(true);
                Vector3[] authoredScales = authoredTransforms.Select(transform => transform.localScale).ToArray();
                visual.name = "Bud";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                SetLayerRecursively(visual, 7);

                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("Bud.prefab contains no skinned renderers.");
                clips[0].SampleAnimation(animator.gameObject, 0f);
                // Keep every authored scale; only place the model origin at the capsule floor.
                visual.transform.localPosition = Vector3.down;
                for (int index = 0; index < renderers.Length; index++)
                {
                    SkinnedMeshRenderer renderer = renderers[index];
                    renderer.quality = SkinQuality.Bone4;
                    renderer.rootBone = animator.transform;
                    renderer.sharedMaterials = BuildMaterials(renderer.sharedMaterials);
                    string samplesPath = $"Assets/CrowdPunch/Animation/EnemyWizardMovement_{index:00}_{SafeName(renderer.name)}.bytes";
                    WriteSamples(samplesPath, animator, renderer, clips);
                    EnemyAnimationAuthoring authoring = renderer.GetComponent<EnemyAnimationAuthoring>();
                    if (authoring == null) authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                    authoring.Profile = EnemyAnimationProfile.Wizard;
                    authoring.SpawnMotion = 7;
                    authoring.RotateLaunchAroundOwner = false;
                    authoring.LaunchPivotHeight = .6f;
                    authoring.BlendResponse = 12f;
                    authoring.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(samplesPath);
                }
                clips[0].SampleAnimation(animator.gameObject, 0f);
                // Sampling introduces tiny floating-point scale drift. Save the exact source prefab values.
                for (int index = 0; index < authoredTransforms.Length; index++)
                    authoredTransforms[index].localScale = authoredScales[index];
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssignSpawnSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created {PrefabPath} from Bud.prefab and assigned it to {SettingsPath}.");
        }

        private static void EnsureZoneMaterial()
        {
            const string path = "Assets/CrowdPunch/Resources/WizardZone.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            EnsureFolder("Assets/CrowdPunch", "Resources");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/CrowdPunch/Materials/WizardZone.shader");
            if (shader == null) throw new InvalidOperationException("Missing Wizard zone shader.");
            AssetDatabase.CreateAsset(new Material(shader) { name = "WizardZone" }, path);
        }

        // WIZARD-006: authored motion owns body readability; ECS keeps movement and cast timing.
        private static AnimationClip[] FindClips() => new[] {
            FindClip("Fly Idle"), FindClip("Fly Forward In Place"), FindClip("Fly Projectile Upward"),
            FindClip("Fly Take Damage"), FindClip("Ground Idle"), FindClip("Fly Take Damage"),
            FindClip("Ground Idle to Fly Idle"), FindClip("Spawn to Ground Idle"), FindClip("Ground Idle"),
            FindClip("Fly Spin Attack"), FindClip("Fly Die")
        };

        private static void ConfigureAnimationImport()
        {
            string path = $"{AnimationFolder}/Bud@Fly Projectile Upward.FBX";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            if (clips.All(clip => clip.loopTime)) return;
            foreach (var clip in clips) clip.loopTime = true;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static AnimationClip FindClip(string name)
        {
            string path = $"{AnimationFolder}/Bud@{name}.FBX";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.name == name);
            if (clip == null) throw new InvalidOperationException($"Missing Bud animation '{name}' at {path}.");
            return clip;
        }

        private static AnimatorController BuildController(AnimationClip[] clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            for (int motion = 0; motion < clips.Length; motion++)
            {
                var state = machine.AddState("Motion" + motion);
                state.motion = clips[motion];
                if (motion == 0) machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static Material[] BuildMaterials(Material[] sources)
        {
            Material template = AssetDatabase.LoadAssetAtPath<Material>(SkinningMaterialPath);
            if (template == null) throw new InvalidOperationException($"Missing skinning material: {SkinningMaterialPath}");
            var results = new Material[sources.Length];
            for (int index = 0; index < sources.Length; index++)
            {
                Material source = sources[index];
                string name = string.IsNullOrWhiteSpace(source?.name) ? $"Wizard_{index}" : source.name;
                string path = $"{MaterialFolder}/{SafeName(name)}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(template) { name = name };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = template.shader;
                Texture texture = source != null ? source.mainTexture : null;
                Color color = source != null && source.HasProperty("_BaseColor")
                    ? source.GetColor("_BaseColor")
                    : source != null && source.HasProperty("_Color") ? source.color : Color.white;
                if (material.HasProperty("_ColorMap")) material.SetTexture("_ColorMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                foreach (string property in new[] { "_SkinColorAmount", "_DarkAmount", "_DirtAmount", "_EyelinerAmount", "_ExploderPulse" })
                    if (material.HasProperty(property)) material.SetFloat(property, 0f);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                results[index] = material;
            }
            AssetDatabase.SaveAssets();
            return results;
        }

        private static void WriteSamples(string path, Animator animator, SkinnedMeshRenderer renderer,
            AnimationClip[] clips)
        {
            animator.Rebind();
            var matrices = new Matrix4x4[EnemyAnimationSamples.MotionCount * FrameCount * renderer.bones.Length];
            var durations = new float[EnemyAnimationSamples.MotionCount];
            Matrix4x4[] bindposes = renderer.sharedMesh.bindposes;
            Vector3[] sourceVertices = renderer.sharedMesh.vertices;
            BoneWeight[] boneWeights = renderer.sharedMesh.boneWeights;
            Bounds bounds = default;
            bool hasBounds = false;
            var positions = new Vector3[sourceVertices.Length];
            for (int motion = 0; motion < clips.Length; motion++)
            {
                AnimationClip clip = clips[motion];
                durations[motion] = clip.length;
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    bool oneShot = motion == 3 || motion == 5 || motion == 6 || motion == 7
                        || motion == EnemyAnimationSamples.ImpactMotion;
                    float normalized = oneShot ? frame / (float)(FrameCount - 1) : frame / (float)FrameCount;
                    clip.SampleAnimation(animator.gameObject, normalized * clip.length);
                    Matrix4x4 inverse = animator.transform.worldToLocalMatrix;
                    int poseStart = (motion * FrameCount + frame) * renderer.bones.Length;
                    for (int bone = 0; bone < renderer.bones.Length; bone++)
                        matrices[poseStart + bone] = inverse * renderer.bones[bone].localToWorldMatrix * bindposes[bone];
                    float minimum = float.PositiveInfinity;
                    for (int vertex = 0; vertex < sourceVertices.Length; vertex++)
                    {
                        BoneWeight weight = boneWeights[vertex];
                        Vector3 source = sourceVertices[vertex];
                        Vector3 position = matrices[poseStart + weight.boneIndex0].MultiplyPoint3x4(source) * weight.weight0
                            + matrices[poseStart + weight.boneIndex1].MultiplyPoint3x4(source) * weight.weight1
                            + matrices[poseStart + weight.boneIndex2].MultiplyPoint3x4(source) * weight.weight2
                            + matrices[poseStart + weight.boneIndex3].MultiplyPoint3x4(source) * weight.weight3;
                        positions[vertex] = position;
                        minimum = Mathf.Min(minimum, position.y);
                    }
                    // The supplied death dips slightly below its origin. Preserve emergence only for Spawn.
                    float floorOffset = motion == 7 ? 0f : Mathf.Max(0f, -minimum);
                    for (int bone = 0; bone < renderer.bones.Length; bone++)
                        matrices[poseStart + bone][1, 3] += floorOffset;
                    for (int vertex = 0; vertex < positions.Length; vertex++)
                    {
                        Vector3 position = positions[vertex] + Vector3.up * floorOffset;
                        if (!hasBounds) { bounds = new Bounds(position, Vector3.zero); hasBounds = true; }
                        else bounds.Encapsulate(position);
                    }
                }
            }

            string hash = AssetDatabase.GetAssetDependencyHash(ControllerPath).ToString()
                + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(0x43504133);
                writer.Write(hash);
                writer.Write(renderer.bones.Length);
                writer.Write(FrameCount);
                foreach (Transform bone in renderer.bones) writer.Write(bone.name);
                foreach (float duration in durations) writer.Write(duration);
                foreach (Matrix4x4 matrix in matrices)
                    for (int column = 0; column < 4; column++)
                        for (int row = 0; row < 3; row++) writer.Write(matrix[row, column]);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            bounds.Expand(0.1f);
            renderer.localBounds = bounds;
        }

        private static void ConfigurePhysics(GameObject root)
        {
            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.5f;
            collider.height = 2f;
            collider.providesContacts = true;
            collider.material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsMaterialPath);
            var body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = true;
            body.constraints = (RigidbodyConstraints)84;
        }

        private static void AssignSpawnSettings()
        {
            EnemySpawnSettings settings = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(SettingsPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<EnemySpawnSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                var initial = new SerializedObject(settings);
                initial.FindProperty("archetype").enumValueIndex = (int)Configuration.EnemyArchetype.Wizard;
                initial.FindProperty("respawnEnabled").boolValue = false;
                var baseline = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/EnemySpawnSettings.asset");
                initial.FindProperty("maxHealth").floatValue = baseline.Health.Max;
                initial.ApplyModifiedPropertiesWithoutUndo();
            }
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("enemyPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("spawnFromGround").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private static string SafeName(string value) => value.Replace('.', '_').Replace('|', '_').Replace('/', '_');

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
