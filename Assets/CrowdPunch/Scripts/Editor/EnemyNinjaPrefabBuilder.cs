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
        public const string ModelPath = "Assets/CrowdPunch/Prefabs/Enemies/Fair Black Male Ninja 03 Variant.prefab";
        private const string AnimationFolder = "Assets/Male Ninja Pack Cute Series/Standard Assets Files/Animators and Animations";
        private const string HumanoidFolder = "Assets/Goblin Trooper Giant Evolution Modular Pack Cute Series/Goblin Trooper Modular Pack Cute Series/Animations";
        private const string PrefabPath = "Assets/CrowdPunch/Prefabs/EnemyNinja.prefab";
        private const string ControllerPath = "Assets/CrowdPunch/Animation/EnemyNinja.controller";
        private const string SettingsPath = "Assets/CrowdPunch/Data/Settings/Enemies/NinjaEnemySpawnSettings.asset";
        public const string MaterialFolder = "Assets/CrowdPunch/Materials/Enemies/Ninja";
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

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException($"Missing Ninja model: {ModelPath}");
            AnimationClip[] clips = FindClips();
            AnimatorController controller = BuildController(clips);

            GameObject root = new GameObject("EnemyNinja") { layer = 7 };
            try
            {
                ConfigurePhysics(root);
                root.AddComponent<EnemyAuthoring>();
                var ninja = root.AddComponent<NinjaAuthoring>(); ninja.settings = AssetDatabase.LoadAssetAtPath<NinjaSettings>("Assets/CrowdPunch/Data/Settings/NinjaSettings.asset");
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Ninja";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                SetLayerRecursively(visual, 7);

                foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(lod);
                ConvertAccessories(visual);
                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                if (animator.avatar == null || !animator.avatar.isHuman)
                    throw new InvalidOperationException("The Ninja prefab needs its authored humanoid avatar.");
                animator.Rebind();
                animator.Play("Motion0", 0, 0f);
                animator.Update(0f);
                SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("The Ninja prefab contains no skinned renderers.");
                // The prefab's 1.2 scale and all child proportions remain authored.
                AlignVisualToFloor(visual.transform, renderers[0]);
                Vector3 visualPosition = visual.transform.localPosition;
                for (int index = 0; index < renderers.Length; index++)
                {
                    SkinnedMeshRenderer renderer = renderers[index];
                    renderer.quality = SkinQuality.Bone4;
                    renderer.forceMeshLod = 0;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.rootBone = animator.transform;
                    renderer.sharedMaterials = BuildMaterials(renderer.sharedMaterials);
                    string samplesPath = $"Assets/CrowdPunch/Animation/EnemyNinjaMovement_{index:00}_{SafeName(renderer.name)}.bytes";
                    WriteSamples(samplesPath, animator, renderer, renderers[0], clips);
                    EnemyAnimationAuthoring authoring = renderer.GetComponent<EnemyAnimationAuthoring>();
                    if (authoring == null) authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                    authoring.Profile = EnemyAnimationProfile.Ninja;
                    authoring.RotateLaunchAroundOwner = false;
                    authoring.SpawnMotion = -1;
                    authoring.BlendResponse = 12f;
                    authoring.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(samplesPath);
                }
                animator.Rebind();
                animator.Play("Motion0", 0, 0f);
                animator.Update(0f);
                visual.transform.localPosition = visualPosition;
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssignSpawnSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created {PrefabPath} from the authored Ninja variant and assigned it to {SettingsPath}.");
        }

        // NINJA-005: retarget existing humanoid clips to the Ninja avatar.
        // Cut Attack is split continuously into windup, committed slash and follow-through.
        private static AnimationClip[] FindClips() => new[] {
            FindClip($"{AnimationFolder}/Char@Idle.FBX"),
            FindClip($"{AnimationFolder}/Char@Walk Forward In Place.FBX"),
            FindClip($"{AnimationFolder}/Char@Run Forward W Weapons In Place.FBX"),
            FindClip($"{HumanoidFolder}/Char@Cut Attack.FBX"),
            FindClip($"{HumanoidFolder}/Char@Cut Attack.FBX"),
            FindClip($"{HumanoidFolder}/Char@Take Damage.FBX"),
            FindClip($"{HumanoidFolder}/Char@Die.FBX"),
            FindClip($"{AnimationFolder}/Char@Idle.FBX"),
            FindClip($"{HumanoidFolder}/Char@Cut Attack.FBX"),
            FindClip("Assets/CrowdPunch/Animation/Flying.fbx"),
            FindClip($"{HumanoidFolder}/Char@Die.FBX") };

        private static AnimationClip FindClip(string path)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal) && c.humanMotion);
            return clip != null ? clip : throw new InvalidOperationException($"Missing humanoid animation: {path}");
        }

        private static AnimatorController BuildController(AnimationClip[] clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
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

        // Rigid hood, mask and sword must follow their bones after ECS baking strips Animator.
        private static void ConvertAccessories(GameObject visual)
        {
            int index = 0;
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var bone = renderer.transform.parent;
                var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                mesh.name = $"NinjaAccessory_{index:00}";
                mesh.bindposes = new[] { bone.worldToLocalMatrix * renderer.transform.localToWorldMatrix };
                mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, mesh.vertexCount).ToArray();
                string path = $"Assets/CrowdPunch/Animation/NinjaAccessory_{index++:00}.asset";
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
                foreach (string property in new[] { "_SkinColorAmount", "_DarkAmount", "_DirtAmount", "_EyelinerAmount" })
                    if (material.HasProperty(property)) material.SetFloat(property, 0f);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                results[index] = material;
            }
            AssetDatabase.SaveAssets();
            return results;
        }

        private static void WriteSamples(string path, Animator animator, SkinnedMeshRenderer renderer,
            SkinnedMeshRenderer body, AnimationClip[] clips)
        {
            animator.Rebind();
            var matrices = new Matrix4x4[EnemyAnimationSamples.MotionCount * FrameCount * renderer.bones.Length];
            var durations = new float[EnemyAnimationSamples.MotionCount];
            var vertices = renderer.sharedMesh.vertices;
            var weights = renderer.sharedMesh.boneWeights;
            var bindposes = renderer.sharedMesh.bindposes;
            var bodyVertices = body.sharedMesh.vertices;
            var bodyWeights = body.sharedMesh.boneWeights;
            var bodyBindposes = body.sharedMesh.bindposes;
            var bodyMatrices = new Matrix4x4[body.bones.Length];
            Bounds bounds = default;
            bool hasBounds = false;
            float floor = animator.transform.InverseTransformPoint(animator.transform.parent.TransformPoint(Vector3.down)).y;
            for (int motion = 0; motion < clips.Length; motion++)
            {
                float start = motion == 4 ? .38f : motion == 8 ? .68f : motion == 6 ? 1f : 0f;
                float end = motion == 3 ? .38f : motion == 4 ? .68f : motion == 6 ? 0f : 1f;
                durations[motion] = clips[motion].length * Mathf.Abs(end - start);
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    bool loop = motion <= 2;
                    float phase = Mathf.Lerp(start, end, frame / (float)(loop ? FrameCount : FrameCount - 1));
                    animator.Play("Motion" + motion, 0, phase);
                    animator.Update(0f);
                    Matrix4x4 inverse = animator.transform.worldToLocalMatrix;
                    for (int bone = 0; bone < body.bones.Length; bone++)
                        bodyMatrices[bone] = inverse * body.bones[bone].localToWorldMatrix * bodyBindposes[bone];
                    float minimum = float.PositiveInfinity;
                    for (int vertex = 0; vertex < bodyVertices.Length; vertex++)
                        minimum = Mathf.Min(minimum, SkinVertex(bodyVertices[vertex], bodyWeights[vertex], bodyMatrices).y);
                    // Translation only, shared with equipment; retargeted death poses stay above the capsule floor.
                    float lift = Mathf.Max(0, floor - minimum);
                    int poseStart = (motion * FrameCount + frame) * renderer.bones.Length;
                    var pose = new Matrix4x4[renderer.bones.Length];
                    for (int bone = 0; bone < pose.Length; bone++)
                    {
                        pose[bone] = inverse * renderer.bones[bone].localToWorldMatrix * bindposes[bone];
                        pose[bone][1, 3] += lift;
                        matrices[poseStart + bone] = pose[bone];
                    }
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        Vector3 position = SkinVertex(vertices[vertex], weights[vertex], pose);
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
            bounds.Expand(.1f);
            renderer.localBounds = bounds;
        }

        private static Vector3 SkinVertex(Vector3 vertex, BoneWeight w, Matrix4x4[] matrices) =>
            matrices[w.boneIndex0].MultiplyPoint3x4(vertex) * w.weight0
            + matrices[w.boneIndex1].MultiplyPoint3x4(vertex) * w.weight1
            + matrices[w.boneIndex2].MultiplyPoint3x4(vertex) * w.weight2
            + matrices[w.boneIndex3].MultiplyPoint3x4(vertex) * w.weight3;

        private static void AlignVisualToFloor(Transform visual, SkinnedMeshRenderer body)
        {
            var matrices = body.bones.Select((bone, i) => visual.worldToLocalMatrix * bone.localToWorldMatrix * body.sharedMesh.bindposes[i]).ToArray();
            float minimum = float.PositiveInfinity;
            var vertices = body.sharedMesh.vertices;
            var weights = body.sharedMesh.boneWeights;
            for (int i = 0; i < vertices.Length; i++) minimum = Mathf.Min(minimum, SkinVertex(vertices[i], weights[i], matrices).y);
            visual.localPosition = Vector3.down * (1f + minimum * visual.localScale.y);
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



