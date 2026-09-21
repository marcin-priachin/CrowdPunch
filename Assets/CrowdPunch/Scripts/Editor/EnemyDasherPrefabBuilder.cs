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
        private const string ModelPath = "Assets/CrowdPunch/Models/UltimateMonsters/Flying/Dragon.fbx";
        private const string PrefabPath = "Assets/CrowdPunch/Prefabs/EnemyDasher.prefab";
        private const string ControllerPath = "Assets/CrowdPunch/Animation/EnemyDasher.controller";
        private const string SamplesPath = "Assets/CrowdPunch/Animation/EnemyDasherMovement.bytes";
        private const string SettingsPath = "Assets/CrowdPunch/Data/Settings/Enemies/EnemyDasherSpawnSettings.asset";
        private const string MaterialFolder = "Assets/CrowdPunch/Materials/Enemies/Dasher";
        private const string SkinningMaterialPath = "Assets/CrowdPunch/Animation/EnemySkinning.mat";
        private const string PhysicsMaterialPath = "Assets/CrowdPunch/PhysicsMaterial/Enemy.physicMaterial";
        private const string TelegraphPrefabPath = "Assets/CrowdPunch/Prefabs/Feedback/DasherTelegraph.prefab";
        private const string TelegraphMaterialPath = "Assets/CrowdPunch/Materials/Feedback/DasherTelegraph.mat";
        private const string FeedbackSettingsPath = "Assets/CrowdPunch/Resources/CombatFeedbackSettings.asset";
        private const int FrameCount = 32;

        [MenuItem("Crowd Punch/Enemies/Rebuild Dasher Prefab")]
        public static void Rebuild()
        {
            EnsureFolder("Assets/CrowdPunch/Materials", "Enemies");
            EnsureFolder("Assets/CrowdPunch/Materials/Enemies", "Dasher");
            BuildTelegraphParticles();
            ConfigureAnimationImport();

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException($"Missing Dasher model: {ModelPath}");
            AnimationClip idle = FindClip("Flying_Idle");
            AnimationClip movement = FindClip("Fast_Flying");
            AnimationClip death = FindClip("Death");
            AnimatorController controller = BuildController(idle, movement, death);

            GameObject root = new GameObject("EnemyDasher") { layer = 7 };
            try
            {
                ConfigurePhysics(root);
                root.AddComponent<EnemyAuthoring>();
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Dragon";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = Vector3.one;
                SetLayerRecursively(visual, 7);

                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length != 1)
                    throw new InvalidOperationException($"Dragon.fbx must contain exactly one skinned renderer; found {renderers.Length}.");
                SkinnedMeshRenderer renderer = renderers[0];
                FitVisualToEnemy(root.transform, visual.transform, renderer);
                renderer.quality = SkinQuality.Bone4;
                renderer.rootBone = animator.transform;
                renderer.sharedMaterials = BuildMaterials(renderer.sharedMaterials);

                EnemyAnimationAuthoring authoring = renderer.gameObject.GetComponent<EnemyAnimationAuthoring>();
                if (authoring == null) authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                authoring.Profile = EnemyAnimationProfile.Dasher;
                authoring.BlendResponse = 12f;

                WriteSamples(animator, renderer, idle, movement, death);
                authoring.Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(SamplesPath);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssignSpawnSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created {PrefabPath} from Dragon.fbx and assigned it to {SettingsPath}.");
        }

        private static void ConfigureAnimationImport()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException($"Expected a model importer at {ModelPath}.");
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool shouldLoop = MatchesClipName(clip.name, "Flying_Idle")
                    || MatchesClipName(clip.name, "Fast_Flying");
                if (clip.loopTime == shouldLoop) continue;
                clip.loopTime = shouldLoop;
                changed = true;
            }
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip FindClip(string name)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => MatchesClipName(candidate.name, name));
            if (clip != null) return clip;
            string available = string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>().Select(candidate => candidate.name));
            throw new InvalidOperationException($"Dragon.fbx has no animation named '{name}'. Available: {available}");
        }

        private static bool MatchesClipName(string candidate, string requested)
        {
            return candidate == requested || candidate.EndsWith("|" + requested, StringComparison.Ordinal);
        }

        private static AnimatorController BuildController(AnimationClip idle, AnimationClip movement, AnimationClip death)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState state in machine.states) machine.RemoveState(state.state);
            AnimatorState idleState = machine.AddState("Flying_Idle");
            idleState.motion = idle;
            machine.defaultState = idleState;
            machine.AddState("Fast_Flying").motion = movement;
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
            for (int i = 0; i < sources.Length; i++)
            {
                Material source = sources[i];
                string safeName = string.IsNullOrWhiteSpace(source?.name) ? $"Dragon_{i}" : source.name;
                string path = $"{MaterialFolder}/{safeName}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(template.shader) { name = safeName };
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
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                results[i] = material;
            }
            AssetDatabase.SaveAssets();
            return results;
        }

        private static void WriteSamples(Animator animator, SkinnedMeshRenderer renderer,
            AnimationClip idle, AnimationClip movement, AnimationClip death)
        {
            animator.Rebind();
            var clips = new AnimationClip[EnemyAnimationSamples.MotionCount];
            clips[0] = idle;
            for (int i = 1; i < EnemyAnimationSamples.ImpactMotion; i++) clips[i] = movement;
            clips[EnemyAnimationSamples.ImpactMotion] = death;
            var matrices = new Matrix4x4[EnemyAnimationSamples.MotionCount * FrameCount * renderer.bones.Length];
            var durations = new float[EnemyAnimationSamples.MotionCount];
            Matrix4x4[] bindposes = renderer.sharedMesh.bindposes;
            Bounds bounds = default;
            bool hasBounds = false;
            var sampledMesh = new Mesh();
            try
            {
                for (int motion = 0; motion < clips.Length; motion++)
                {
                    AnimationClip clip = clips[motion];
                    durations[motion] = clip.length;
                    for (int frame = 0; frame < FrameCount; frame++)
                    {
                        float normalized = motion == EnemyAnimationSamples.ImpactMotion
                            ? frame / (float)(FrameCount - 1)
                            : frame / (float)FrameCount;
                        clip.SampleAnimation(animator.gameObject, normalized * clip.length);
                        Matrix4x4 inverse = animator.transform.worldToLocalMatrix;
                        int poseStart = (motion * FrameCount + frame) * renderer.bones.Length;
                        for (int bone = 0; bone < renderer.bones.Length; bone++)
                            matrices[poseStart + bone] = inverse * renderer.bones[bone].localToWorldMatrix * bindposes[bone];

                        renderer.BakeMesh(sampledMesh);
                        Matrix4x4 toRoot = inverse * renderer.transform.localToWorldMatrix;
                        foreach (Vector3 vertex in sampledMesh.vertices)
                        {
                            Vector3 position = toRoot.MultiplyPoint3x4(vertex);
                            if (!hasBounds) { bounds = new Bounds(position, Vector3.zero); hasBounds = true; }
                            else bounds.Encapsulate(position);
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sampledMesh);
            }

            string hash = AssetDatabase.GetAssetDependencyHash(ControllerPath).ToString()
                + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar));
            using (var writer = new BinaryWriter(File.Create(SamplesPath)))
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
            AssetDatabase.ImportAsset(SamplesPath, ImportAssetOptions.ForceSynchronousImport);
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

        private static void BuildTelegraphParticles()
        {
            Shader shader = Shader.Find("CrowdPunch/CombatFeedback");
            if (shader == null) throw new InvalidOperationException("Missing CrowdPunch/CombatFeedback shader.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(TelegraphMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "DasherTelegraph" };
                AssetDatabase.CreateAsset(material, TelegraphMaterialPath);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);

            var root = new GameObject("DasherTelegraph");
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = particles.main;
                main.loop = true;
                main.playOnAwake = false;
                main.duration = 0.5f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.42f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(-2.2f, -1.15f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.12f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.12f, 0.015f, 0.5f),
                    new Color(1f, 0.62f, 0.08f, 0.95f));
                main.maxParticles = 64;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.stopAction = ParticleSystemStopAction.None;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 42f;
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 1.15f;
                shape.radiusThickness = 0.32f;
                shape.rotation = new Vector3(90f, 0f, 0f);

                ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.18f, 0.02f), 0f), new GradientColorKey(new Color(1f, 0.78f, 0.16f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.2f), new GradientAlphaKey(0f, 1f) });
                color.color = gradient;
                ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f,
                    AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));

                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.16f;
                renderer.lengthScale = 1.8f;
                renderer.sortingOrder = 3;

                PrefabUtility.SaveAsPrefabAsset(root, TelegraphPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            CombatFeedbackSettings feedback = AssetDatabase.LoadAssetAtPath<CombatFeedbackSettings>(FeedbackSettingsPath);
            ParticleSystem prefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(TelegraphPrefabPath);
            if (feedback == null || prefab == null)
                throw new InvalidOperationException("Could not load Dasher telegraph feedback assets.");
            feedback.DasherTelegraphParticles = prefab;
            EditorUtility.SetDirty(feedback);
        }

        private static void FitVisualToEnemy(Transform enemy, Transform visual, SkinnedMeshRenderer renderer)
        {
            Bounds bounds = renderer.bounds;
            float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largestDimension <= Mathf.Epsilon)
                throw new InvalidOperationException("Dragon renderer has empty bounds.");
            const float targetLargestDimension = 2.5f;
            visual.localScale *= targetLargestDimension / largestDimension;
            bounds = renderer.bounds;
            Vector3 centerInEnemy = enemy.InverseTransformPoint(bounds.center);
            visual.localPosition -= centerInEnemy;
        }

        private static void AssignSpawnSettings()
        {
            EnemySpawnSettings settings = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(SettingsPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (settings == null || prefab == null) throw new InvalidOperationException("Could not load Dasher settings or prefab.");
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

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
