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
    public static class EnemySnakePrefabBuilder
    {
        public const string ModelPath = "Assets/CrowdPunch/Prefabs/Enemies/Slime Variant.prefab";
        public const string AnimationFolder = "Assets/Slime Cute Series/FBX";
        private const string PrefabPath = "Assets/CrowdPunch/Prefabs/EnemySnake.prefab";
        private const string ControllerPath = "Assets/CrowdPunch/Animation/EnemySnake.controller";
        private const string SettingsPath = "Assets/CrowdPunch/Data/Settings/Enemies/SnakeEnemySpawnSettings.asset";
        public const string MaterialFolder = "Assets/CrowdPunch/Materials/Enemies/Snake";
        private const string SkinningMaterialPath = "Assets/CrowdPunch/Animation/EnemySkinning.mat";
        private const string PhysicsMaterialPath = "Assets/CrowdPunch/PhysicsMaterial/Enemy.physicMaterial";
        private const int FrameCount = 32;

        [MenuItem("Crowd Punch/Enemies/Rebuild Snake Prefab")]
        public static void Rebuild()
        {
            EnsureFolder("Assets/CrowdPunch/Materials/Enemies", "Snake");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rebuild the Snake outside Play Mode.");

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException($"Missing Snake model: {ModelPath}");
            AnimationClip[] clips = FindClips();
            AnimatorController controller = BuildController(clips);

            GameObject root = new GameObject("EnemySnake") { layer = 7 };
            try
            {
                ConfigurePhysics(root);
                root.AddComponent<EnemyAuthoring>();
                var snake = root.AddComponent<SnakeAuthoring>();
                snake.settings = AssetDatabase.LoadAssetAtPath<SnakeSettings>("Assets/CrowdPunch/Data/Settings/SnakeSettings.asset");
                snake.orbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/SnakeOrb.prefab");
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Slime";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                SetLayerRecursively(visual, 7);
                // Keep one authored mesh detail level for stable crowd silhouettes.
                foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(lod);
                foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>(true))
                    if (particle != null) UnityEngine.Object.DestroyImmediate(particle.gameObject);

                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("Slime Variant.prefab contains no skinned renderers.");
                clips[0].SampleAnimation(animator.gameObject, 0f);
                AlignVisualToFloor(visual.transform, renderers);
                for (int index = 0; index < renderers.Length; index++)
                {
                    SkinnedMeshRenderer renderer = renderers[index];
                    renderer.quality = SkinQuality.Bone4;
                    renderer.forceMeshLod = 0;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.rootBone = animator.transform;
                    renderer.sharedMaterials = BuildMaterials(renderer.sharedMaterials);
                    string samplesPath = $"Assets/CrowdPunch/Animation/EnemySnakeMovement_{index:00}_{SafeName(renderer.name)}.bytes";
                    WriteSamples(samplesPath, animator, renderer, clips);
                    EnemyAnimationAuthoring authoring = renderer.GetComponent<EnemyAnimationAuthoring>();
                    if (authoring == null) authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                    authoring.Profile = EnemyAnimationProfile.Slime;
                    authoring.SpawnMotion = 7;
                    authoring.BlendResponse = 12f;
                    authoring.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(samplesPath);
                }
                // Restore Idle at the collider floor; keep the variant's authored scale.
                animator.Rebind();
                clips[0].SampleAnimation(animator.gameObject, 0f);
                AlignVisualToFloor(visual.transform, renderers);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            RebuildSegments();
            AssignSpawnSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created {PrefabPath} from Slime Variant.prefab and assigned it to {SettingsPath}.");
        }

        [MenuItem("Crowd Punch/Animation/Rebuild Snake Samples")]
        public static void RebuildSamples()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rebuild Snake samples outside Play Mode.");
            AnimationClip[] clips = FindClips();
            var controller = BuildController(clips);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);
                Vector3 position = animator.transform.localPosition;
                Quaternion rotation = animator.transform.localRotation;
                animator.runtimeAnimatorController = controller;
                foreach (var authoring in root.GetComponentsInChildren<EnemyAnimationAuthoring>(true))
                    WriteSamples(AssetDatabase.GetAssetPath(authoring.Samples), animator,
                        authoring.GetComponent<SkinnedMeshRenderer>(), clips);
                animator.Rebind(); clips[0].SampleAnimation(animator.gameObject, 0f);
                animator.transform.SetLocalPositionAndRotation(position, rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator.transform);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            RebuildSegments();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Crowd Punch/Enemies/Rebuild Snake Segment Models")]
        public static void RebuildSegments()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rebuild segment models outside Play Mode.");
            const string path = "Assets/CrowdPunch/Prefabs/SnakeOrb.prefab";
            var head = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (head == null || source == null) throw new InvalidOperationException("Build the Slime head before its segments.");
            var headAnimator = head.GetComponentInChildren<Animator>(true);
            var headSkins = head.GetComponentsInChildren<EnemyAnimationAuthoring>(true);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Keep the existing sphere collider, enemy authoring and prefab GUID.
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                visual.name = "Slime"; visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                SetLayerRecursively(visual, 7);
                foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(lod);
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                var animator = visual.GetComponent<Animator>();
                animator.runtimeAnimatorController = headAnimator.runtimeAnimatorController;
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind(); FindClip("Idle").SampleAnimation(visual, 0f);
                var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length != headSkins.Length) throw new InvalidOperationException("Slime segment and head skins differ.");
                Bounds bounds = default; bool initialized = false;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i]; var headSkin = headSkins[i].GetComponent<SkinnedMeshRenderer>();
                    if (renderer.sharedMesh != headSkin.sharedMesh) throw new InvalidOperationException("Segment must use the head mesh.");
                    renderer.rootBone = animator.transform; renderer.quality = SkinQuality.Bone4; renderer.forceMeshLod = 0;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.sharedMaterials = headSkin.sharedMaterials; renderer.localBounds = headSkin.localBounds;
                    var bindposes = renderer.sharedMesh.bindposes;
                    var matrices = renderer.bones.Select((b, index) => visual.transform.worldToLocalMatrix * b.localToWorldMatrix * bindposes[index]).ToArray();
                    var vertices = renderer.sharedMesh.vertices; var weights = renderer.sharedMesh.boneWeights;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        var w = weights[v]; var point = vertices[v];
                        var p = matrices[w.boneIndex0].MultiplyPoint3x4(point) * w.weight0
                            + matrices[w.boneIndex1].MultiplyPoint3x4(point) * w.weight1
                            + matrices[w.boneIndex2].MultiplyPoint3x4(point) * w.weight2
                            + matrices[w.boneIndex3].MultiplyPoint3x4(point) * w.weight3;
                        if (!initialized) { bounds = new Bounds(p, Vector3.zero); initialized = true; }
                        else bounds.Encapsulate(p);
                    }
                    var authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                    authoring.Profile = EnemyAnimationProfile.SlimeSegment; authoring.SpawnMotion = 7;
                    authoring.Samples = headSkins[i].Samples;
                }
                // Centre the authored model on the segment body, preserving its source scale.
                visual.transform.localPosition = -Vector3.Scale(bounds.center, visual.transform.localScale);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        // SNAKE-001/004/006, INFO-004: presentation follows authoritative gameplay clocks.
        private static AnimationClip[] FindClips() => new[] {
            FindClip("Idle"), FindClip("Move Forward In Place"), FindClip("Move Forward In Place"),
            FindClip("Head Attack"), FindClip("Head Attack"), FindClip("Take Damage"),
            FindClip("Die"), FindClip("Spawn"), FindClip("Die"),
            FindClip("Roll Forward In Place"), FindClip("Die") };

        private static AnimationClip FindClip(string name)
        {
            string path = $"{AnimationFolder}/Slime@{name}.FBX";
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(candidate => MatchesClipName(candidate.name, name));
            if (clip != null) return clip;
            string available = string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>().Select(candidate => candidate.name));
            throw new InvalidOperationException($"Slime pack has no animation named '{name}'. Available: {available}");
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
                string name = string.IsNullOrWhiteSpace(source?.name) ? $"Slime_{index}" : source.name;
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
                ConfigureBodyMask(material);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                results[index] = material;
            }
            AssetDatabase.SaveAssets();
            return results;
        }

        [MenuItem("Crowd Punch/Enemies/Apply Snake Colors")]
        public static void ConfigureMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material.GetTexture("_ColorMap") != null
                    && AssetDatabase.GetAssetPath(material.GetTexture("_ColorMap")).StartsWith("Assets/Slime Cute Series/", StringComparison.Ordinal))
                    ConfigureBodyMask(material);
            }
            var settings = AssetDatabase.LoadAssetAtPath<SnakeSettings>("Assets/CrowdPunch/Data/Settings/SnakeSettings.asset");
            var tail = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrowdPunch/Materials/Enemies/SnakeOrb.mat");
            if (tail != null && settings != null)
            {
                tail.SetColor("_BaseColor", settings.protectedColor);
                EditorUtility.SetDirty(tail);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureBodyMask(Material material)
        {
            Texture texture = material.GetTexture("_ColorMap");
            if (texture == null) throw new InvalidOperationException("Slime body mask requires the source texture.");
            const string maskPath = MaterialFolder + "/SlimeBodyMask.asset";
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if (mask == null)
            {
                mask = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true)
                { name = "SlimeBodyMask", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                AssetDatabase.CreateAsset(mask, maskPath);
            }
            // Read back the imported texture without changing its importer or supplied pixels.
            var target = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply();
                Color[] pixels = copy.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    pixels[i] = c.g > c.r * 1.2f && c.g > c.b * 1.5f && c.g > .02f
                        ? Color.black : Color.white;
                }
                mask.SetPixels(pixels); mask.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(copy);
            }
            material.SetTexture("_SkinMaskTexture", mask);
            material.SetFloat("_SkinColorAmount", 0f);
            var settings = AssetDatabase.LoadAssetAtPath<SnakeSettings>("Assets/CrowdPunch/Data/Settings/SnakeSettings.asset");
            if (settings != null) material.SetColor("_SkinColor", settings.vulnerableColor);
            EditorUtility.SetDirty(mask); EditorUtility.SetDirty(material);
        }

        private static void WriteSamples(string path, Animator animator, SkinnedMeshRenderer renderer,
            AnimationClip[] clips)
        {
            animator.Rebind();
            clips[0].SampleAnimation(animator.gameObject, 0f);
            Matrix4x4 sampleSpace = animator.transform.worldToLocalMatrix;
            Vector3 rootPosition = animator.transform.localPosition;
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
                    animator.Rebind();
                    animator.transform.localPosition = rootPosition;
                    clip.SampleAnimation(animator.gameObject, (motion == 6 ? 1f - normalized : normalized) * clip.length);
                    // Preserve extracted vertical root motion in fixed-root skin samples.
                    // ECS owns horizontal travel; no runtime model transform is animated.
                    Matrix4x4 inverse = sampleSpace;
                    Vector3 rootDelta = sampleSpace.MultiplyVector(animator.transform.parent.TransformVector(
                        animator.transform.localPosition - rootPosition));
                    rootDelta.y = 0;
                    int poseStart = (motion * FrameCount + frame) * renderer.bones.Length;
                    for (int bone = 0; bone < renderer.bones.Length; bone++)
                    {
                        matrices[poseStart + bone] = inverse * renderer.bones[bone].localToWorldMatrix * bindposes[bone];
                        for (int axis = 0; axis < 3; axis++) matrices[poseStart + bone][axis, 3] -= rootDelta[axis];
                    }
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
                    // INFO-004: only Spawn may pass below the floor. Translate
                    // the whole authored pose; retain its bone rotations and proportions.
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
            if (largest <= Mathf.Epsilon) throw new InvalidOperationException("Slime renderers have empty bounds.");
            // Keep the visual's feet on the unchanged physics capsule floor.
            visual.localPosition = -Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), visual.localScale) - Vector3.up;
        }

        private static void ConfigurePhysics(GameObject root)
        {
            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.75f;
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
            if (settings == null || prefab == null) throw new InvalidOperationException("Could not load Snake settings or prefab.");
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
