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
    public static class EnemyDragonPrefabBuilder
    {
        private const string ModelPath = "Assets/Dragon Fire Cute Series/Prefabs/Dragon Fire.prefab";
        private const string AnimationFolder = "Assets/Dragon Fire Cute Series/FBX";
        public const string MaterialFolder = "Assets/CrowdPunch/Materials/Enemies/Dragon";
        private const string PrefabPath = "Assets/CrowdPunch/Prefabs/EnemyDragon.prefab";
        private const string ControllerPath = "Assets/CrowdPunch/Animation/EnemyDragon.controller";
        private const string SettingsPath = "Assets/CrowdPunch/Data/Settings/Enemies/DragonEnemySpawnSettings.asset";
        private const string SkinningMaterialPath = "Assets/CrowdPunch/Animation/EnemySkinning.mat";
        private const string PhysicsMaterialPath = "Assets/CrowdPunch/PhysicsMaterial/Enemy.physicMaterial";
        private const int FrameCount = 32;

        [MenuItem("Crowd Punch/Enemies/Rebuild Dragon Prefab")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring.");
            var tuning = AssetDatabase.LoadAssetAtPath<DragonSettings>("Assets/CrowdPunch/Data/Settings/DragonSettings.asset");
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<DragonSettings>();
                AssetDatabase.CreateAsset(tuning, "Assets/CrowdPunch/Data/Settings/DragonSettings.asset");
            }
            EnsureFolder("Assets/CrowdPunch/Materials/Enemies", "Dragon");

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException($"Missing Dragon model: {ModelPath}");
            AnimationClip idle = FindClip("Fly Idle");
            AnimationClip walk = FindClip("Fly Forward In Place");
            AnimationClip death = FindClip("Die");

            var clips = new[] { idle, walk, FindClip("Cast Spell"), FindClip("Cast Spell"), idle,
                FindClip("Take Damage"), FindClip("Take Damage"), FindClip("Cast Spell"), idle,
                FindClip("Cast Spell"), death };
            AnimatorController controller = BuildController(clips);

            GameObject root = new GameObject("EnemyDragon") { layer = 7 };
            try
            {
                ConfigurePhysics(root);
                root.AddComponent<EnemyAuthoring>();
                var dragon = root.AddComponent<DragonAuthoring>(); dragon.settings = AssetDatabase.LoadAssetAtPath<DragonSettings>("Assets/CrowdPunch/Data/Settings/DragonSettings.asset");
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                var authoredTransforms = visual.GetComponentsInChildren<Transform>(true);
                var authoredScales = authoredTransforms.Select(t => t.localScale).ToArray();
                visual.name = "Dragon Fire";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                SetLayerRecursively(visual, 7);

                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("Dragon Fire.prefab contains no skinned renderers.");
                // The prefab origin is at its feet; the unchanged capsule floor is one metre below its root.
                visual.transform.localPosition = Vector3.down;
                idle.SampleAnimation(visual, 0);
                var head = visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RigJaw");
                if (head == null) throw new InvalidOperationException("Dragon Fire has no RigJaw attachment.");
                tuning.mouthOffset = root.transform.InverseTransformPoint(head.position) + Vector3.forward * .15f;
                tuning.idleDuration = idle.length; tuning.moveDuration = walk.length; tuning.castDuration = clips[2].length;
                dragon.mouthFrames = new Vector3[96];
                var mouthClips = new[] { idle, walk, FindClip("Cast Spell") };
                for (int motion = 0; motion < 3; motion++) for (int frame = 0; frame < FrameCount; frame++)
                {
                    mouthClips[motion].SampleAnimation(visual, mouthClips[motion].length * frame / (motion == 2 ? 31f : 32f));
                    dragon.mouthFrames[motion * 32 + frame] = root.transform.InverseTransformPoint(head.position) + Vector3.forward * .15f;
                }
                EditorUtility.SetDirty(tuning);
                for (int index = 0; index < renderers.Length; index++)
                {
                    SkinnedMeshRenderer renderer = renderers[index];
                    renderer.quality = SkinQuality.Bone4;
                    renderer.rootBone = animator.transform;
                    renderer.sharedMaterials = BuildMaterials(renderer.sharedMaterials);
                    string samplesPath = $"Assets/CrowdPunch/Animation/EnemyDragonMovement_{index:00}_{SafeName(renderer.name)}.bytes";
                    WriteSamples(samplesPath, animator, renderer, clips);
                    EnemyAnimationAuthoring authoring = renderer.GetComponent<EnemyAnimationAuthoring>();
                    if (authoring == null) authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                    authoring.Profile = EnemyAnimationProfile.Dragon;
                    authoring.SpawnMotion = 7;
                    authoring.RotateLaunchAroundOwner = false;
                    authoring.BlendResponse = 12f;
                    authoring.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(samplesPath);
                }
                idle.SampleAnimation(visual, 0f);
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
            Debug.Log($"Created {PrefabPath} from Dragon Fire.prefab and assigned it to {SettingsPath}.");
        }

        private static AnimationClip FindClip(string name)
        {
            string path = $"{AnimationFolder}/Dragon Fire@{name}.FBX";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.name == name);
            if (clip == null) throw new InvalidOperationException($"Missing Dragon Fire animation '{name}' at {path}.");
            return clip;
        }

        private static AnimatorController BuildController(AnimationClip[] clips)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState state in machine.states) machine.RemoveState(state.state);
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
                string name = string.IsNullOrWhiteSpace(source?.name) ? $"Dragon_{index}" : source.name;
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
                    bool oneShot = motion == EnemyAnimationSamples.ImpactMotion || motion == 2 || motion == 3
                        || motion == 5 || motion == 6 || motion == 7 || motion == EnemyAnimationSamples.FlyingMotion;
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
                    // Keep Die above the capsule floor by translation only; preserve the authored bone scale/rotation.
                    float lift = motion == EnemyAnimationSamples.ImpactMotion ? Mathf.Max(0f, -minimum) : 0f;
                    for (int bone = 0; bone < renderer.bones.Length; bone++) matrices[poseStart + bone][1, 3] += lift;
                    foreach (Vector3 position in positions)
                    {
                        Vector3 grounded = position + Vector3.up * lift;
                        if (!hasBounds) { bounds = new Bounds(grounded, Vector3.zero); hasBounds = true; }
                        else bounds.Encapsulate(grounded);
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
                initial.FindProperty("archetype").enumValueIndex = (int)Configuration.EnemyArchetype.Dragon;
                initial.FindProperty("respawnEnabled").boolValue = false;
                initial.FindProperty("contactDamagePercent").floatValue = 0;
                initial.FindProperty("contactPushStrength").floatValue = 0;
                initial.FindProperty("contactRadius").floatValue = .45f;
                initial.ApplyModifiedPropertiesWithoutUndo();
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (settings == null || prefab == null) throw new InvalidOperationException("Could not load Dragon settings or prefab.");
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



