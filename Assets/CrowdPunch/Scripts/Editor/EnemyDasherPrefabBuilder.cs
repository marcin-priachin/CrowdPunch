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
    public static class EnemyDasherPrefabBuilder
    {
        public const string ModelPath = "Assets/Goblin Trooper Giant Evolution Modular Pack Cute Series/Goblin Minion Cute Series/Prefabs/Goblin Minion.prefab";
        private const string AnimationFolder = "Assets/Goblin Trooper Giant Evolution Modular Pack Cute Series/Goblin Minion Cute Series/FBX";
        private const string PrefabPath = "Assets/CrowdPunch/Prefabs/EnemyDasher.prefab";
        private const string ControllerPath = "Assets/CrowdPunch/Animation/EnemyDasher.controller";
        private const string SettingsPath = "Assets/CrowdPunch/Data/Settings/Enemies/EnemyDasherSpawnSettings.asset";
        public const string MaterialFolder = "Assets/CrowdPunch/Materials/Enemies/Dasher";
        private const string SkinningMaterialPath = "Assets/CrowdPunch/Animation/EnemySkinning.mat";
        private const string PhysicsMaterialPath = "Assets/CrowdPunch/PhysicsMaterial/Enemy.physicMaterial";
        private const int FrameCount = 32;

        [MenuItem("Crowd Punch/Enemies/Rebuild Dasher Prefab")]
        public static void Rebuild()
        {
            EnsureFolder("Assets/CrowdPunch/Materials/Enemies", "Dasher");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rebuild the Dasher outside Play Mode.");

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException($"Missing Dasher model: {ModelPath}");
            AnimationClip[] clips = FindClips();
            AnimatorController controller = BuildController(clips);

            GameObject root = new GameObject("EnemyDasher") { layer = 7 };
            try
            {
                ConfigurePhysics(root);
                root.AddComponent<EnemyAuthoring>();
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Goblin Minion";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                SetLayerRecursively(visual, 7);
                foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(lod);

                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length != 1)
                    throw new InvalidOperationException("Goblin Minion.prefab must contain one skinned renderer.");
                animator.Rebind();
                animator.Play("Motion0", 0, 0f);
                animator.Update(0f);
                AlignVisualToFloor(visual.transform, renderers);
                for (int index = 0; index < renderers.Length; index++)
                {
                    SkinnedMeshRenderer renderer = renderers[index];
                    renderer.quality = SkinQuality.Bone4;
                    renderer.forceMeshLod = 0;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.rootBone = animator.transform;
                    renderer.sharedMaterials = BuildMaterials(renderer.sharedMaterials);
                    string samplesPath = "Assets/CrowdPunch/Animation/EnemyDasherMovement.bytes";
                    WriteSamples(samplesPath, animator, renderer, clips);
                    EnemyAnimationAuthoring authoring = renderer.GetComponent<EnemyAnimationAuthoring>();
                    if (authoring == null) authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                    authoring.Profile = EnemyAnimationProfile.Dasher;
                    authoring.SpawnMotion = 7;
                    authoring.BlendResponse = 12f;
                    authoring.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(samplesPath);
                }
                // Restore the upright authored pose after sampling Die.
                animator.Rebind();
                animator.Play("Motion0", 0, 0f);
                animator.Update(0f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssignSpawnSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created {PrefabPath} from Goblin Minion.prefab and assigned it to {SettingsPath}.");
        }

        [MenuItem("Crowd Punch/Animation/Rebuild Dasher Samples")]
        public static void RebuildSamples()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rebuild animation samples outside Play Mode.");

            AnimationClip[] clips = FindClips();
            BuildController(clips);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new InvalidOperationException("Dasher prefab has no Animator.");
                foreach (EnemyAnimationAuthoring authoring in root.GetComponentsInChildren<EnemyAnimationAuthoring>(true))
                {
                    SkinnedMeshRenderer renderer = authoring.GetComponent<SkinnedMeshRenderer>();
                    string samplesPath = AssetDatabase.GetAssetPath(authoring.Samples);
                    if (renderer == null || string.IsNullOrEmpty(samplesPath))
                        throw new InvalidOperationException("Dasher renderer has no generated sample asset.");
                    WriteSamples(samplesPath, animator, renderer, clips);
                }
                animator.Rebind();
                animator.Play("Motion0", 0, 0f);
                animator.Update(0f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rebuilt Dasher animation samples while preserving authored transforms.");
        }

        // INFO-004: authored motion replaces tint pulses and procedural deformation.
        // ENEMY-005..008: pose playback never owns dash timing, movement, scale or contacts.
        private static AnimationClip[] FindClips() => new[] {
            FindClip("Idle"), FindClip("Walk Forward In Place"), FindClip("Run Forward In Place"),
            FindClip("Roll Forward In Place"), FindClip("Roll Forward In Place"), FindClip("Take Damage"),
            FindClip("Die"), FindClip("Spawn"), FindClip("Look Around"),
            FindClip("Roll Forward In Place"), FindClip("Die") };

        private static AnimationClip FindClip(string name)
        {
            string path = $"{AnimationFolder}/Goblin Minion@{name}.FBX";
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(candidate => MatchesClipName(candidate.name, name));
            if (clip != null) return clip;
            string available = string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>().Select(candidate => candidate.name));
            throw new InvalidOperationException($"Goblin Minion pack has no animation named '{name}'. Available: {available}");
        }

        private static bool MatchesClipName(string candidate, string requested) =>
            candidate == requested || candidate.EndsWith("|" + requested, StringComparison.Ordinal);

        private static AnimatorController BuildController(AnimationClip[] clips)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState state in machine.states) machine.RemoveState(state.state);
            for (int i = 0; i < clips.Length; i++)
            {
                AnimatorState state = machine.AddState("Motion" + i);
                state.motion = clips[i];
                if (i == 0) machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceSynchronousImport);
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
                string name = string.IsNullOrWhiteSpace(source?.name) ? $"Goblin Minion_{index}" : source.name;
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
                foreach (string property in new[] { "_SkinColorAmount", "_DarkAmount", "_DirtAmount", "_EyelinerAmount", "_DasherPulse" })
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
            Bounds bounds = default;
            bool hasBounds = false;
            Vector3[] vertices = renderer.sharedMesh.vertices;
            BoneWeight[] weights = renderer.sharedMesh.boneWeights;
            var positions = new Vector3[vertices.Length];
            float floor = animator.transform.InverseTransformPoint(
                animator.transform.parent.TransformPoint(Vector3.down)).y;
            for (int motion = 0; motion < clips.Length; motion++)
            {
                AnimationClip clip = clips[motion];
                durations[motion] = clip.length;
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    bool loop = motion == 0 || motion == 1 || motion == 2 || motion == 9;
                    float normalized = frame / (float)(loop ? FrameCount : FrameCount - 1);
                    if (motion == 6) normalized = 1f - normalized;
                    animator.Play("Motion" + motion, 0, normalized);
                    animator.Update(0f);
                    Matrix4x4 inverse = animator.transform.worldToLocalMatrix;
                    int poseStart = (motion * FrameCount + frame) * renderer.bones.Length;
                    for (int bone = 0; bone < renderer.bones.Length; bone++)
                        matrices[poseStart + bone] = inverse * renderer.bones[bone].localToWorldMatrix * bindposes[bone];
                    // Match GPU root-space skinning without reapplying the imported scale.
                    float minimum = float.PositiveInfinity;
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        BoneWeight weight = weights[vertex];
                        Vector3 point = vertices[vertex];
                        Vector3 position = matrices[poseStart + weight.boneIndex0].MultiplyPoint3x4(point) * weight.weight0
                            + matrices[poseStart + weight.boneIndex1].MultiplyPoint3x4(point) * weight.weight1
                            + matrices[poseStart + weight.boneIndex2].MultiplyPoint3x4(point) * weight.weight2
                            + matrices[poseStart + weight.boneIndex3].MultiplyPoint3x4(point) * weight.weight3;
                        positions[vertex] = position;
                        minimum = Mathf.Min(minimum, position.y);
                    }
                    // INFO-004: keep authored roll/fall poses above the capsule floor.
                    // Only Spawn intentionally emerges from below it. This is a sampled
                    // translation; retain authored bone rotations, scale and proportions.
                    float lift = motion != 7 ? Mathf.Max(0f, floor - minimum) : 0f;
                    if (lift > 0f)
                        for (int bone = 0; bone < renderer.bones.Length; bone++)
                            matrices[poseStart + bone][1, 3] += lift;
                    for (int vertex = 0; vertex < positions.Length; vertex++)
                    {
                        Vector3 position = positions[vertex] + Vector3.up * lift;
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

        private static void AlignVisualToFloor(Transform visual, SkinnedMeshRenderer[] renderers)
        {
            // Imported renderer bounds can remain cached in the source root-bone space.
            // Align the sampled Idle vertices without changing the prefab's authored scale.
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var renderer in renderers)
            {
                var bindposes = renderer.sharedMesh.bindposes;
                var vertices = renderer.sharedMesh.vertices;
                var weights = renderer.sharedMesh.boneWeights;
                var matrices = renderer.bones.Select((bone, i) =>
                    visual.worldToLocalMatrix * bone.localToWorldMatrix * bindposes[i]).ToArray();
                for (int i = 0; i < vertices.Length; i++)
                {
                    var weight = weights[i];
                    var vertex = vertices[i];
                    Vector3 point = matrices[weight.boneIndex0].MultiplyPoint3x4(vertex) * weight.weight0
                        + matrices[weight.boneIndex1].MultiplyPoint3x4(vertex) * weight.weight1
                        + matrices[weight.boneIndex2].MultiplyPoint3x4(vertex) * weight.weight2
                        + matrices[weight.boneIndex3].MultiplyPoint3x4(vertex) * weight.weight3;
                    if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                    else bounds.Encapsulate(point);
                }
            }
            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest <= Mathf.Epsilon) throw new InvalidOperationException("Goblin Minion renderers have empty bounds.");
            // Keep the visual's feet on the unchanged physics capsule floor.
            visual.localPosition = -Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), visual.localScale) - Vector3.up;
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
            if (settings == null || prefab == null) throw new InvalidOperationException("Could not load Dasher settings or prefab.");
            if (settings.EnemyPrefab == prefab) return;
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
