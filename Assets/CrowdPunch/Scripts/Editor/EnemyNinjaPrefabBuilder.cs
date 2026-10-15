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
    public static class EnemyNinjaPrefabBuilder
    {
        private const string ModelPath = "Assets/CrowdPunch/Models/UltimateMonsters/Big/Ninja.fbx";
        private const string PrefabPath = "Assets/CrowdPunch/Prefabs/EnemyNinja.prefab";
        private const string ControllerPath = "Assets/CrowdPunch/Animation/EnemyNinja.controller";
        private const string SettingsPath = "Assets/CrowdPunch/Data/Settings/Enemies/NinjaEnemySpawnSettings.asset";
        private const string MaterialFolder = "Assets/CrowdPunch/Materials/Enemies/Ninja";
        private const string SkinningMaterialPath = "Assets/CrowdPunch/Animation/EnemySkinning.mat";
        private const string PhysicsMaterialPath = "Assets/CrowdPunch/PhysicsMaterial/Enemy.physicMaterial";
        private const int FrameCount = 32;

        [MenuItem("Crowd Punch/Enemies/Rebuild Ninja Prefab")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring.");
            var tuning = AssetDatabase.LoadAssetAtPath<NinjaSettings>("Assets/CrowdPunch/Data/Settings/NinjaSettings.asset");
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<NinjaSettings>();
                tuning.playerMovement = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>("Assets/CrowdPunch/Data/Settings/PlayerMovementSettings.asset");
                AssetDatabase.CreateAsset(tuning, "Assets/CrowdPunch/Data/Settings/NinjaSettings.asset");
            }
            EnsureFolder("Assets/CrowdPunch/Materials/Enemies", "Ninja");
            ConfigureAnimationImport();

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException($"Missing baseline model: {ModelPath}");
            AnimationClip idle = FindClip("Idle");
            AnimationClip walk = FindClip("Run");
            AnimationClip death = FindClip("Death");

            AnimatorController controller = BuildController(idle, walk, death);

            GameObject root = new GameObject("EnemyNinja") { layer = 7 };
            try
            {
                ConfigurePhysics(root);
                root.AddComponent<EnemyAuthoring>();
                var ninja = root.AddComponent<NinjaAuthoring>(); ninja.settings = AssetDatabase.LoadAssetAtPath<NinjaSettings>("Assets/CrowdPunch/Data/Settings/NinjaSettings.asset");
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Ninja";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = Vector3.one;
                SetLayerRecursively(visual, 7);

                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("Ninja.fbx contains no skinned renderers.");
                FitVisualToEnemy(root.transform, visual.transform, renderers);
                for (int index = 0; index < renderers.Length; index++)
                {
                    SkinnedMeshRenderer renderer = renderers[index];
                    renderer.quality = SkinQuality.Bone4;
                    renderer.rootBone = animator.transform;
                    renderer.sharedMaterials = BuildMaterials(renderer.sharedMaterials);
                    string samplesPath = $"Assets/CrowdPunch/Animation/EnemyNinjaMovement_{index:00}_{SafeName(renderer.name)}.bytes";
                    WriteSamples(samplesPath, animator, renderer, idle, walk, death, FindClip("Duck"), FindClip("Punch"));
                    EnemyAnimationAuthoring authoring = renderer.GetComponent<EnemyAnimationAuthoring>();
                    if (authoring == null) authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                    authoring.Profile = EnemyAnimationProfile.Ninja;
                    authoring.RotateLaunchAroundOwner = true;
                    authoring.LaunchPivotHeight = .6f;
                    authoring.BlendResponse = 12f;
                    authoring.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(samplesPath);
                }
                idle.SampleAnimation(animator.gameObject, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssignSpawnSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created {PrefabPath} from Ninja.fbx and assigned it to {SettingsPath}.");
        }

        private static void ConfigureAnimationImport()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException($"Expected a model importer at {ModelPath}.");
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool shouldLoop = MatchesClipName(clip.name, "Idle") || MatchesClipName(clip.name, "Walk");
                if (clip.loopTime == shouldLoop) continue;
                clip.loopTime = shouldLoop;
                changed = true;
            }
            if (!changed) return;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static AnimationClip FindClip(string name)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .FirstOrDefault(candidate => MatchesClipName(candidate.name, name));
            if (clip != null) return clip;
            string available = string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>().Select(candidate => candidate.name));
            throw new InvalidOperationException($"Ninja.fbx has no animation named '{name}'. Available: {available}");
        }

        private static bool MatchesClipName(string candidate, string requested) =>
            candidate == requested || candidate.EndsWith("|" + requested, StringComparison.Ordinal);

        private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk,
            AnimationClip death)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState state in machine.states) machine.RemoveState(state.state);
            AnimatorState idleState = machine.AddState("Idle");
            idleState.motion = idle;
            machine.defaultState = idleState;
            machine.AddState("Walk").motion = walk;
            machine.AddState("Death").motion = death;
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
                string name = string.IsNullOrWhiteSpace(source?.name) ? $"Ninja_{index}" : source.name;
                string path = $"{MaterialFolder}/{SafeName(name)}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(template.shader) { name = name };
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
                EnemyAppearanceMaterials.Configure(material);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                results[index] = material;
            }
            AssetDatabase.SaveAssets();
            return results;
        }

        private static void WriteSamples(string path, Animator animator, SkinnedMeshRenderer renderer,
            AnimationClip idle, AnimationClip walk, AnimationClip death, AnimationClip preparation, AnimationClip attack)
        {
            animator.Rebind();
            var clips = new AnimationClip[EnemyAnimationSamples.MotionCount];
            clips[0] = idle;
            for (int index = 1; index < EnemyAnimationSamples.ImpactMotion; index++) clips[index] = walk;
            clips[2] = preparation; clips[3] = attack; clips[EnemyAnimationSamples.FlyingMotion] = idle;
            clips[EnemyAnimationSamples.ImpactMotion] = death;
            var matrices = new Matrix4x4[EnemyAnimationSamples.MotionCount * FrameCount * renderer.bones.Length];
            var durations = new float[EnemyAnimationSamples.MotionCount];
            Matrix4x4[] bindposes = renderer.sharedMesh.bindposes;
            Vector3[] sourceVertices = renderer.sharedMesh.vertices;
            BoneWeight[] boneWeights = renderer.sharedMesh.boneWeights;
            Bounds bounds = default;
            bool hasBounds = false;
            for (int motion = 0; motion < clips.Length; motion++)
            {
                AnimationClip clip = clips[motion];
                durations[motion] = clip.length;
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    bool oneShot = motion == EnemyAnimationSamples.ImpactMotion || motion == 2 || motion == 3;
                    float normalized = oneShot ? frame / (float)(FrameCount - 1) : frame / (float)FrameCount;
                    clip.SampleAnimation(animator.gameObject, normalized * clip.length);
                    Matrix4x4 inverse = animator.transform.worldToLocalMatrix;
                    int poseStart = (motion * FrameCount + frame) * renderer.bones.Length;
                    for (int bone = 0; bone < renderer.bones.Length; bone++)
                        matrices[poseStart + bone] = inverse * renderer.bones[bone].localToWorldMatrix * bindposes[bone];
                    for (int vertex = 0; vertex < sourceVertices.Length; vertex++)
                    {
                        BoneWeight weight = boneWeights[vertex];
                        Vector3 source = sourceVertices[vertex];
                        Vector3 position = matrices[poseStart + weight.boneIndex0].MultiplyPoint3x4(source) * weight.weight0
                            + matrices[poseStart + weight.boneIndex1].MultiplyPoint3x4(source) * weight.weight1
                            + matrices[poseStart + weight.boneIndex2].MultiplyPoint3x4(source) * weight.weight2
                            + matrices[poseStart + weight.boneIndex3].MultiplyPoint3x4(source) * weight.weight3;
                        if (!hasBounds) { bounds = new Bounds(position, Vector3.zero); hasBounds = true; }
                        else bounds.Encapsulate(position);
                        if (motion == 0 || motion == EnemyAnimationSamples.FlyingMotion)
                        {
                            Vector3 pivot = animator.transform.InverseTransformPoint(animator.transform.parent.position + Vector3.up * .6f);
                            // Cover the centered launch pitch and the full recovery turn.
                            bounds.Encapsulate(new Bounds(pivot, Vector3.one * (position - pivot).magnitude * 2f));
                        }
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

        private static void FitVisualToEnemy(Transform enemy, Transform visual, SkinnedMeshRenderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest <= Mathf.Epsilon) throw new InvalidOperationException("Ninja renderers have empty bounds.");
            visual.localScale *= 2.5f / largest;
            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            visual.localPosition -= enemy.InverseTransformPoint(bounds.center);
            visual.localPosition += Vector3.up * .05f;
        }

        private static void ConfigurePhysics(GameObject root)
        {
            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.45f;
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
            if (settings == null)
            {
                AssetDatabase.CopyAsset("Assets/CrowdPunch/Data/Settings/Enemies/EnemySpawnSettings.asset", SettingsPath);
                settings = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(SettingsPath);
                var initial = new SerializedObject(settings);
                initial.FindProperty("archetype").enumValueIndex = (int)Configuration.EnemyArchetype.Ninja;
                initial.FindProperty("respawnEnabled").boolValue = false;
                initial.FindProperty("contactDamagePercent").floatValue = 0;
                initial.FindProperty("contactPushStrength").floatValue = 0;
                initial.FindProperty("contactRadius").floatValue = .45f;
                initial.ApplyModifiedPropertiesWithoutUndo();
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (settings == null || prefab == null) throw new InvalidOperationException("Could not load baseline settings or prefab.");
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("enemyPrefab").objectReferenceValue = prefab;
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



