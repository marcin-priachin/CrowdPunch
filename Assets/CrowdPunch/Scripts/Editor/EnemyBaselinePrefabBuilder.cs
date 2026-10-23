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
    public static class EnemyBaselinePrefabBuilder
    {
        private const string ModelPath = "Assets/CrowdPunch/Prefabs/Enemies/Goblin Trooper Variant.prefab";
        private const string PrefabPath = "Assets/CrowdPunch/Prefabs/EnemyBaseline.prefab";
        private const string ControllerPath = "Assets/CrowdPunch/Animation/EnemyBaseline.controller";
        private const string SettingsPath = "Assets/CrowdPunch/Data/Settings/Enemies/EnemySpawnSettings.asset";
        public const string MaterialFolder = "Assets/CrowdPunch/Materials/Enemies/GoblinBaseline";
        private const string SkinningMaterialPath = "Assets/CrowdPunch/Animation/EnemySkinning.mat";
        private const string PhysicsMaterialPath = "Assets/CrowdPunch/PhysicsMaterial/Enemy.physicMaterial";
        private const int FrameCount = 32;
        private const string AnimationFolder = "Assets/Goblin Trooper Giant Evolution Modular Pack Cute Series/Goblin Trooper Modular Pack Cute Series/Animations";

        [MenuItem("Crowd Punch/Enemies/Rebuild Baseline Prefab")]
        public static void Rebuild()
        {
            EnsureFolder("Assets/CrowdPunch/Materials/Enemies", "GoblinBaseline");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rebuild the baseline outside Play Mode.");

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException($"Missing baseline model: {ModelPath}");
            var clips = new[] { FindClip("Idle"), FindClip("Walk Forward In Place"),
                FindClip("Run Forward In Place"), FindClip("Cut Attack"), FindClip("Cut Attack"),
                FindClip("Take Damage"), FindClip("Die"), FindClip("Spawn"), FindClip("Idle"),
                FindClip("Jump In Place"), FindClip("Die") };
            AnimatorController controller = BuildController(clips);

            GameObject root = new GameObject("EnemyBaseline") { layer = 7 };
            try
            {
                ConfigurePhysics(root);
                root.AddComponent<EnemyAuthoring>();
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Goblin Trooper";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = Vector3.one;
                SetLayerRecursively(visual, 7);

                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                ConvertAccessories(visual);
                foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(lod);
                SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("Goblin prefab contains no skinned renderers.");
                FitVisualToEnemy(root.transform, visual.transform, renderers);
                for (int index = 0; index < renderers.Length; index++)
                {
                    SkinnedMeshRenderer renderer = renderers[index];
                    renderer.quality = SkinQuality.Bone4;
                    renderer.forceMeshLod = 0;
                    renderer.rootBone = animator.transform;
                    renderer.sharedMaterials = BuildMaterials(renderer.sharedMaterials);
                    string samplesPath = $"Assets/CrowdPunch/Animation/EnemyBaselineMovement_{index:00}_{SafeName(renderer.name)}.bytes";
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    WriteSamples(samplesPath, animator, renderer, renderers[0], clips);
                    EnemyAnimationAuthoring authoring = renderer.GetComponent<EnemyAnimationAuthoring>();
                    if (authoring == null) authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                    authoring.Profile = EnemyAnimationProfile.Goblin;
                    authoring.SpawnMotion = 7;
                    authoring.BlendResponse = 12f;
                    authoring.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(samplesPath);
                }
                animator.Rebind();
                animator.Play("Motion0", 0, 0);
                animator.Update(0f);
                GoblinJogSampling.Configure(root, "EnemyBaseline");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssignSpawnSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created {PrefabPath} from Goblin Trooper Variant.prefab and assigned it to {SettingsPath}.");
        }

        private static AnimationClip FindClip(string name)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath($"{AnimationFolder}/Char@{name}.FBX").OfType<AnimationClip>()
                .FirstOrDefault(candidate => MatchesClipName(candidate.name, name));
            if (clip != null) return clip;
            string available = string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>().Select(candidate => candidate.name));
            throw new InvalidOperationException($"Goblin pack has no animation named '{name}'. Available: {available}");
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
                var state = machine.AddState("Motion" + i);
                state.motion = clips[i];
                if (i == 0) machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        // Rigid equipment must follow the sampled hand bone after Animator is removed by baking.
        private static void ConvertAccessories(GameObject visual)
        {
            int index = 0;
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var bone = renderer.transform.parent;
                var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                mesh.name = $"GoblinAccessory_{index:00}";
                mesh.bindposes = new[] { bone.worldToLocalMatrix * renderer.transform.localToWorldMatrix };
                mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, mesh.vertexCount).ToArray();
                string path = $"Assets/CrowdPunch/Animation/GoblinAccessory_{index++:00}.asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                var materials = renderer.sharedMaterials;
                var target = renderer.gameObject;
                UnityEngine.Object.DestroyImmediate(renderer);
                UnityEngine.Object.DestroyImmediate(filter);
                var skin = target.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = saved;
                skin.bones = new[] { bone };
                skin.sharedMaterials = materials;
            }
            AssetDatabase.SaveAssets();
        }

        private static Material[] BuildMaterials(Material[] sources)
        {
            Material template = AssetDatabase.LoadAssetAtPath<Material>(SkinningMaterialPath);
            if (template == null) throw new InvalidOperationException($"Missing skinning material: {SkinningMaterialPath}");
            var results = new Material[sources.Length];
            for (int index = 0; index < sources.Length; index++)
            {
                Material source = sources[index];
                string name = string.IsNullOrWhiteSpace(source?.name) ? $"Goblin_{index}" : source.name;
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
                foreach (string property in new[] { "_SkinColorAmount", "_DarkAmount", "_DirtAmount", "_EyelinerAmount" })
                    if (material.HasProperty(property)) material.SetFloat(property, 0f);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                results[index] = material;
            }
            AssetDatabase.SaveAssets();
            return results;
        }

        private static void WriteSamples(string path, Animator animator, SkinnedMeshRenderer renderer, SkinnedMeshRenderer body,
            AnimationClip[] clips)
        {
            animator.Rebind();
            var matrices = new Matrix4x4[EnemyAnimationSamples.MotionCount * FrameCount * renderer.bones.Length];
            var durations = new float[EnemyAnimationSamples.MotionCount];
            Matrix4x4[] bindposes = renderer.sharedMesh.bindposes;
            Vector3[] sourceVertices = renderer.sharedMesh.vertices;
            BoneWeight[] boneWeights = renderer.sharedMesh.boneWeights;
            var bodyVertices = body.sharedMesh.vertices;
            var bodyWeights = body.sharedMesh.boneWeights;
            var bodyBindposes = body.sharedMesh.bindposes;
            var bodyMatrices = new Matrix4x4[body.bones.Length];
            Bounds bounds = default;
            bool hasBounds = false;
            for (int motion = 0; motion < clips.Length; motion++)
            {
                AnimationClip clip = clips[motion];
                float start = motion == 4 ? .38f : motion == 9 ? .3f : motion == 6 ? 1f : 0f;
                float end = motion == 3 ? .38f : motion == 9 ? .65f : motion == 6 ? 0f : 1f;
                durations[motion] = clip.length * Mathf.Abs(end - start);
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    bool oneShot = motion != 0 && motion != 1 && motion != 2 && motion != 9;
                    float normalized = oneShot ? frame / (float)(FrameCount - 1) : frame / (float)FrameCount;
                    animator.Play("Motion" + motion, 0, Mathf.Lerp(start, end, normalized));
                    animator.Update(0f);
                    Matrix4x4 inverse = animator.transform.worldToLocalMatrix;
                    for (int bone = 0; bone < bodyMatrices.Length; bone++)
                        bodyMatrices[bone] = inverse * body.bones[bone].localToWorldMatrix * bodyBindposes[bone];
                    float groundOffset = 0f;
                    for (int vertex = 0; vertex < bodyVertices.Length; vertex++)
                    {
                        BoneWeight w = bodyWeights[vertex];
                        Vector3 v = bodyVertices[vertex];
                        Vector3 p = bodyMatrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                            + bodyMatrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                            + bodyMatrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                            + bodyMatrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                        groundOffset = Mathf.Max(groundOffset, -p.y);
                    }
                    // Spawn intentionally emerges from below the floor; keep its authored vertical motion.
                    if (motion == 7) groundOffset = 0f;
                    int poseStart = (motion * FrameCount + frame) * renderer.bones.Length;
                    for (int bone = 0; bone < renderer.bones.Length; bone++)
                    {
                        matrices[poseStart + bone] = inverse * renderer.bones[bone].localToWorldMatrix * bindposes[bone];
                        // Use the body's floor correction for equipment too, preserving hand attachment.
                        matrices[poseStart + bone][1, 3] += groundOffset;
                    }
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
            float largest = bounds.size.y;
            if (largest <= Mathf.Epsilon) throw new InvalidOperationException("Goblin renderers have empty bounds.");
            visual.localScale *= 2f / largest;
            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            Vector3 floor = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            visual.localPosition -= enemy.InverseTransformPoint(floor) + Vector3.up;
            visual.localPosition = new Vector3(visual.localPosition.x, -1f, visual.localPosition.z);
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
            if (settings == null || prefab == null) throw new InvalidOperationException("Could not load baseline settings or prefab.");
            if (settings.EnemyPrefab == prefab && settings.SpawnFromGround) return;
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

