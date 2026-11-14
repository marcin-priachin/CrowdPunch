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
    internal static class EnemyMushroomShooterPrefabBuilder
    {
        private const string Root = "Assets/CrowdPunch/";
        private const string Source = "Assets/Mushroom Shooter/";
        private const string Animations = Root + "Animation/";
        private const string ControllerPath = Animations + "EnemyMushroomShooter.controller";
        private const string MaterialPath = Root + "Materials/Enemies/MushroomShooter.mat";
        private const string SettingsPath = Root + "Data/Settings/Enemies/MushroomShooterSpawnSettings.asset";
        private const string PrefabPath = Root + "Prefabs/EnemyMushroomShooter.prefab";
        private const int Frames = 48;

        internal static EnemySpawnSettings Rebuild()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "Prefabs/No Root/Mushroom Shooter 01.prefab");
            if (model == null) throw new InvalidOperationException("Mushroom Shooter model is missing.");
            var idle = Clip("Idle");
            var run = Clip("Run Forward In Place");
            var attack = Clip("Projectile Attack");
            var damage = Clip("Take Damage");
            var spawn = Clip("Spawn");
            var death = Clip("Die");
            var clips = new[] { idle, run, attack, damage, idle, idle, idle, spawn, idle, damage, death };
            var controller = BuildController(clips);

            var root = new GameObject("EnemyMushroomShooter") { layer = 7 };
            try
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = .55f;
                capsule.height = 2f;
                capsule.providesContacts = true;
                capsule.material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Root + "PhysicsMaterial/Enemy.physicMaterial");
                root.AddComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation;
                root.AddComponent<EnemyAuthoring>();
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                foreach (var bone in visual.GetComponentsInChildren<Transform>(true)) bone.gameObject.layer = 7;
                var animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Mushroom Shooter has no skinned mesh.");
                var material = BuildMaterial();
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    animator.Rebind();
                    renderer.rootBone = visual.transform;
                    renderer.quality = SkinQuality.Bone4;
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                    var authoring = renderer.gameObject.AddComponent<EnemyAnimationAuthoring>();
                    authoring.Profile = EnemyAnimationProfile.MushroomShooter;
                    authoring.SpawnMotion = 7;
                    authoring.RotateLaunchAroundOwner = false;
                    authoring.Samples = WriteSamples(visual, animator, renderer, clips, i);
                }
                idle.SampleAnimation(visual, 0f);
                var bounds = renderers[0].localBounds;
                // Sampled vertices are in model space. The capsule floor is one metre below its root.
                visual.transform.SetLocalPositionAndRotation(Vector3.down, Quaternion.identity);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"Mushroom Shooter sampled bounds: {bounds}; Projectile Attack: {attack.length:0.###} s at {attack.frameRate} fps.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            bool created = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(SettingsPath) == null;
            if (created) AssetDatabase.CopyAsset(Root + "Data/Settings/Enemies/RangedEnemySpawnSettings.asset", SettingsPath);
            var settings = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(SettingsPath);
            using var data = new SerializedObject(settings);
            data.FindProperty("archetype").intValue = (int)Configuration.EnemyArchetype.MushroomShooter;
            data.FindProperty("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            data.FindProperty("rangedProjectilePrefab").objectReferenceValue = null;
            int last = Mathf.Max(1, Mathf.RoundToInt(attack.length * attack.frameRate));
            data.FindProperty("firingAnimationLastFrame").intValue = last;
            if (created)
            {
                data.FindProperty("spawnFromGround").boolValue = true;
                data.FindProperty("respawnEnabled").boolValue = false;
                data.FindProperty("initialCount").intValue = 0;
                data.FindProperty("defeatAnimationFramesToSkip").intValue = 0;
                data.FindProperty("contactDamagePercent").floatValue = 0;
                data.FindProperty("contactPushStrength").floatValue = 0;
                data.FindProperty("preferredMinimumDistance").floatValue = 12f;
                data.FindProperty("preferredMaximumDistance").floatValue = 22f;
                data.FindProperty("engagementRange").floatValue = 32f;
                data.FindProperty("approachSpeed").floatValue = 6f;
                data.FindProperty("cooldown").floatValue = 2f;
                data.FindProperty("cooldownVariation").floatValue = 1f;
                data.FindProperty("projectileSpeed").floatValue = 32f;
                data.FindProperty("projectileDamage").floatValue = 10f;
                data.FindProperty("playerInvincibilitySeconds").floatValue = .5f;
                data.FindProperty("projectileRadius").floatValue = .55f;
                var tuning = data.FindProperty("mushroomShooter");
                var defaults = MushroomShooterSettings.Default;
                tuning.FindPropertyRelative("TrackingSeconds").floatValue = defaults.TrackingSeconds;
                tuning.FindPropertyRelative("LockedSeconds").floatValue = defaults.LockedSeconds;
                tuning.FindPropertyRelative("TravelRange").floatValue = defaults.TravelRange;
                tuning.FindPropertyRelative("Height").floatValue = defaults.Height;
                tuning.FindPropertyRelative("EnemyDamage").floatValue = defaults.EnemyDamage;
                data.FindProperty("projectileLaunchFrame").intValue = Mathf.Clamp(ReleaseFrame(attack), 1, last);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static int ReleaseFrame(AnimationClip attack)
        {
            foreach (var e in attack.events)
                if (e.functionName.IndexOf("projectile", StringComparison.OrdinalIgnoreCase) >= 0
                    || e.functionName.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0)
                    return Mathf.RoundToInt(e.time * attack.frameRate);
            // The source has no release event. RigMouth reaches the first forward
            // extremum at frame 11 of the 30 fps No Root Projectile Attack.
            return 11;
        }

        private static AnimationClip Clip(string name)
        {
            string path = Source + $"FBX/No Root/Mushroom Shooter@{name}.fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name)
                ?? throw new InvalidOperationException($"Missing authored clip {name} at {path}.");
        }

        private static AnimatorController BuildController(AnimationClip[] clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            for (int i = 0; i < clips.Length; i++)
            {
                var state = machine.AddState($"Sample {i} - {clips[i].name}");
                state.motion = clips[i];
                if (i == 0) machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static Material BuildMaterial()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(Source + "Materials/Mushroom Shooter 01.mat");
            var template = AssetDatabase.LoadAssetAtPath<Material>(Animations + "EnemySkinning.mat");
            if (source == null || source.mainTexture == null || template == null)
                throw new InvalidOperationException("Mushroom Shooter texture or Full Cel template is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(template) { name = "Mushroom Shooter Full Cel" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_ColorMap", source.mainTexture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", source.mainTexture);
            material.SetColor("_BaseColor", Color.white);
            foreach (var property in new[] { "_SkinColorAmount", "_DarkAmount", "_DirtAmount", "_EyelinerAmount", "_ExploderPulse" })
                if (material.HasProperty(property)) material.SetFloat(property, 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static TextAsset WriteSamples(GameObject visual, Animator animator, SkinnedMeshRenderer renderer,
            AnimationClip[] clips, int index)
        {
            string path = Animations + $"EnemyMushroomShooter_{index:00}.bytes";
            var bones = renderer.bones;
            var bind = renderer.sharedMesh.bindposes;
            var vertices = renderer.sharedMesh.vertices;
            var weights = renderer.sharedMesh.boneWeights;
            var poses = new Matrix4x4[bones.Length];
            var frameVertices = new Vector3[vertices.Length];
            Bounds bounds = default;
            bool hasBounds = false;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(0x43504133);
                writer.Write(AssetDatabase.GetAssetDependencyHash(ControllerPath).ToString()
                    + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                    + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar)));
                writer.Write(bones.Length);
                writer.Write(Frames);
                foreach (var bone in bones) writer.Write(bone.name);
                foreach (var clip in clips) writer.Write(clip.length);
                for (int motion = 0; motion < clips.Length; motion++)
                for (int frame = 0; frame < Frames; frame++)
                {
                    var clip = clips[motion];
                    clip.SampleAnimation(visual, clip.length * frame / (motion < 2 ? Frames : Frames - 1));
                    var inverse = visual.transform.worldToLocalMatrix;
                    for (int bone = 0; bone < bones.Length; bone++) poses[bone] = inverse * bones[bone].localToWorldMatrix * bind[bone];
                    float lowest = float.PositiveInfinity;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var w = weights[i];
                        var point = vertices[i];
                        frameVertices[i] = poses[w.boneIndex0].MultiplyPoint3x4(point) * w.weight0
                            + poses[w.boneIndex1].MultiplyPoint3x4(point) * w.weight1
                            + poses[w.boneIndex2].MultiplyPoint3x4(point) * w.weight2
                            + poses[w.boneIndex3].MultiplyPoint3x4(point) * w.weight3;
                        lowest = Mathf.Min(lowest, frameVertices[i].y);
                    }
                    // Preserve the authored rise from the floor in Spawn.
                    float lift = motion == 7 ? 0f : Mathf.Max(0f, -lowest);
                    foreach (var vertex in frameVertices)
                    {
                        var point = vertex + Vector3.up * lift;
                        if (hasBounds) bounds.Encapsulate(point);
                        else { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                    }
                    for (int bone = 0; bone < poses.Length; bone++)
                    {
                        poses[bone][1, 3] += lift;
                        for (int column = 0; column < 4; column++)
                        for (int row = 0; row < 3; row++) writer.Write(poses[bone][row, column]);
                    }
                }
            }
            bounds.Expand(.1f);
            renderer.localBounds = bounds;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }
    }
}
