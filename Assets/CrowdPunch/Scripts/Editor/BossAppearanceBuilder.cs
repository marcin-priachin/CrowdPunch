using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // Appearance-only migration. Encounter geometry, settings, waves and scene identities stay authored.
    public static class BossAppearanceBuilder
    {
        private const string Root = "Assets/CrowdPunch/";
        private const string GateArt = Root + "Data/Boss/";
        public static readonly string[] ScenePaths = Enumerable.Range(1, 8)
            .Select(i => $"{Root}Scenes/Campaign/Campaign_{i * 10:00}/Campaign_{i * 10:00} Sub Scene.unity")
            .Concat(new[] { 11, 20, 21, 22 }.Select(i => $"{Root}Scenes/Gauntlets/Gauntlet_{i:00}/Gauntlet_{i:00} Sub Scene.unity"))
            .Append(Root + "Scenes/Campaign/Campaign_81.unity").ToArray();

        [MenuItem("Crowd Punch/Bosses/Apply Boss Appearance")]
        public static void ApplyAll()
        {
            foreach (string path in ScenePaths) ApplyScene(path);
        }

        public static void ApplyScene(string path)
        {
            if (!ScenePaths.Contains(path)) throw new ArgumentException("Expected an existing boss scene.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var head in root.GetComponentsInChildren<BossEncounterAuthoring>(true))
                    {
                        RemoveModel(head.transform, "Orc Head Visual", "Gatekeeper Model");
                        AddGatekeeper(head.gameObject);
                    }
                    foreach (var boss in root.GetComponentsInChildren<ChickenBossAuthoring>(true))
                    {
                        RemoveModel(boss.transform, "Chicken Model");
                        GauntletProgressionBuilder.AddChickenModel(boss.gameObject, boss.settings);
                    }
                    foreach (var boss in root.GetComponentsInChildren<RollingBossAuthoring>(true))
                    {
                        RemoveModel(boss.transform, "Green Spiky Blob Model");
                        GauntletProgressionBuilder.AddRollingModel(boss.gameObject, boss.settings);
                    }
                    foreach (var boss in root.GetComponentsInChildren<DinoBossAuthoring>(true))
                    {
                        RemoveModel(boss.transform, "Dino Model");
                        GauntletProgressionBuilder.AddDinoModel(boss.gameObject, boss.settings);
                    }
                    foreach (var finale in root.GetComponentsInChildren<FinalePresentation>(true)) ConfigureFinale(finale);
                }
                ConfigureHandMaterials();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static void RemoveModel(Transform root, params string[] names)
        {
            foreach (Transform child in root.Cast<Transform>().ToArray())
                if (names.Contains(child.name)) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        public static void AddGatekeeper(GameObject root)
        {
            const string source = Root + "Models/UltimateMonsters/Blob/Orc.fbx";
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source), root.transform);
            visual.name = "Gatekeeper Model";
            var renderer = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            visual.transform.localScale *= 4.4f / renderer.bounds.size.y;
            visual.transform.localPosition -= root.transform.InverseTransformPoint(renderer.bounds.center) - Vector3.up * .2f;
            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            string[] names = { "Idle", "Walk", "No", "HitRecieve", "Death" };
            var clips = names.Select(n => AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__") && c.name.EndsWith("|" + n))).ToArray();
            string controllerPath = GateArt + "Gatekeeper.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                foreach (var clip in clips) controller.layers[0].stateMachine.AddState(clip.name.Split('|').Last()).motion = clip;
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            renderer.rootBone = animator.transform;
            renderer.quality = SkinQuality.Bone4;
            string materialPath = GateArt + "Gatekeeper Atlas.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Root + "Animation/EnemySkinning.mat"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            EnemyAppearanceMaterials.Configure(material);
            renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            AssetDatabase.SaveAssets();
            string samples = GateArt + "GatekeeperMovement.bytes";
            GauntletProgressionBuilder.WriteBossSamples(animator, renderer, clips, samples, controllerPath);
            renderer.gameObject.AddComponent<EnemyAnimationAuthoring>().Samples = AssetDatabase.LoadAssetAtPath<TextAsset>(samples);
            GroundModel(visual, root.GetComponent<BossPartAuthoring>().height);
        }

        // Use the exported skin positions, not BakeMesh bounds (FBX armatures can carry 100x scale).
        internal static void VisitSamples(GameObject visual, Action<Vector3> visit, int motions = 4)
        {
            foreach (var authoring in visual.GetComponentsInChildren<EnemyAnimationAuthoring>())
            {
                var renderer = authoring.GetComponent<SkinnedMeshRenderer>();
                using var reader = new BinaryReader(new MemoryStream(authoring.Samples.bytes));
                reader.ReadInt32(); reader.ReadString();
                int bones = reader.ReadInt32(), frames = reader.ReadInt32();
                for (int i = 0; i < bones; i++) reader.ReadString();
                for (int i = 0; i < EnemyAnimationSamples.MotionCount; i++) reader.ReadSingle();
                var matrices = new Matrix4x4[bones];
                var vertices = renderer.sharedMesh.vertices; var weights = renderer.sharedMesh.boneWeights;
                for (int frame = 0; frame < frames * motions; frame++)
                {
                    for (int bone = 0; bone < bones; bone++)
                    {
                        var matrix = Matrix4x4.identity;
                        for (int col = 0; col < 4; col++) for (int row = 0; row < 3; row++) matrix[row, col] = reader.ReadSingle();
                        matrices[bone] = matrix;
                    }
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var w = weights[i]; var v = vertices[i];
                        visit(matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                            + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3);
                    }
                }
            }
        }

        public static void GroundModel(GameObject visual, float bodyHeight)
        {
            var toOwner = visual.transform.localToWorldMatrix;
            toOwner = visual.transform.parent.worldToLocalMatrix * toOwner;
            float minimum = float.PositiveInfinity;
            VisitSamples(visual, point => minimum = Mathf.Min(minimum, toOwner.MultiplyPoint3x4(point).y));
            visual.transform.localPosition += Vector3.up * (-bodyHeight * .5f + .08f - minimum);
            // Reserve space for the largest procedural silhouette accent and transitions.
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var bounds = renderer.localBounds; bounds.Expand(bounds.size * .4f + Vector3.one * .2f); renderer.localBounds = bounds;
            }
        }

        public static void FitRollingSphere(GameObject visual, RollingBossAuthoring owner, RollingBossSettings settings)
        {
            Vector3 pivot = owner.animationPivot;
            float radius = 0;
            VisitSamples(visual, p => radius = Mathf.Max(radius, Vector3.Distance(p, pivot)));
            float worldRadius = radius * visual.transform.localScale.x;
            float fit = Mathf.Min(1, (Mathf.Min(settings.bodyRadius, settings.bodyHeight * .5f) - .1f) / Mathf.Max(.01f, worldRadius));
            visual.transform.localScale *= fit;
            visual.transform.localPosition = -pivot * visual.transform.localScale.x;
            const int angles = 64;
            var lowest = Enumerable.Repeat(float.PositiveInfinity, angles).ToArray();
            var sine = new float[angles]; var cosine = new float[angles];
            for (int i=0; i<angles; i++) { sine[i]=Mathf.Sin(i*Mathf.PI*2/angles); cosine[i]=Mathf.Cos(i*Mathf.PI*2/angles); }
            VisitSamples(visual, p => {
                var d=p-pivot;
                for(int i=0; i<angles; i++) lowest[i]=Mathf.Min(lowest[i],cosine[i]*d.y-sine[i]*d.z);
            });
            owner.animationFloorOffsets = lowest.Select(y => (-settings.bodyHeight*.5f+.08f)/visual.transform.localScale.x-y).ToArray();
            float extra = owner.animationFloorOffsets.Max(v => Mathf.Abs(v));
            foreach(var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var bounds=renderer.localBounds; bounds.Expand(Vector3.up*extra*2); renderer.localBounds=bounds;
            }
        }

        public static void ConfigureFinale(FinalePresentation finale)
        {
            const string path = Root + "Data/Campaign/Finale/Goleling Atlas.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Goleling Atlas", enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
            }
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(EnemyAppearanceMaterials.AtlasPath);
            material.SetTexture("_BaseMap", atlas);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .18f);
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_EmissionColor", Color.white * .04f);
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            foreach (var renderer in finale.model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                ApplyFinalePalette(renderer);
            }
            finale.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            string source = Root + "Models/UltimateMonsters/Flying/Goleling_Evolved.fbx";
            var clips = AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
            finale.animator.Rebind();
            foreach(string clipName in new[] { "Flying_Idle", "HitReact", "Punch", "Headbutt" })
            {
                float minimum=float.PositiveInfinity;
                var clip=clips.First(c=>c.name.EndsWith("|"+clipName));
                foreach(var renderer in finale.model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var vertices=renderer.sharedMesh.vertices; var weights=renderer.sharedMesh.boneWeights; var bindposes=renderer.sharedMesh.bindposes;
                    var matrices=new Matrix4x4[renderer.bones.Length];
                    for(int frame=0;frame<32;frame++)
                    {
                        clip.SampleAnimation(finale.model.gameObject,clip.length*frame/31);
                        for(int bone=0;bone<matrices.Length;bone++) matrices[bone]=finale.model.worldToLocalMatrix*renderer.bones[bone].localToWorldMatrix*bindposes[bone];
                        for(int i=0;i<vertices.Length;i++)
                        {
                            var w=weights[i]; var v=vertices[i];
                            var p=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1
                                +matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                            minimum=Mathf.Min(minimum,p.y);
                        }
                    }
                }
                float offset=minimum-.03f;
                if(clipName=="Flying_Idle") finale.groundedModelOffset=offset;
                else if(clipName=="HitReact") finale.groundedHitOffset=offset;
                else if(clipName=="Punch") finale.groundedPunchOffset=offset;
                else finale.groundedHeadbuttOffset=offset;
            }
            clips.First(c=>c.name.EndsWith("|Fast_Flying")).SampleAnimation(finale.model.gameObject,0);
        }

        private static void ApplyFinalePalette(SkinnedMeshRenderer renderer)
        {
            // Keep the crown, eyes, mouth and wing membranes; only the green body becomes royal blue.
            const string path=Root+"Data/Campaign/Finale/Goleling Sapphire Palette.asset";
            var mesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);
            mesh.name="Goleling Sapphire Palette";
            var uv=mesh.uv;
            for(int i=0;i<uv.Length;i++)
                if(Mathf.Abs(uv[i].x-.078591f)<.008f && Mathf.Abs(uv[i].y-.140570f)<.008f)
                    uv[i]=new Vector2(.078125f,.953125f);
            mesh.uv=uv;
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null) { AssetDatabase.CreateAsset(mesh,path); saved=mesh; }
            else { EditorUtility.CopySerialized(mesh,saved); UnityEngine.Object.DestroyImmediate(mesh); }
            EditorUtility.SetDirty(saved);
            renderer.sharedMesh=saved;
        }

        private static void ConfigureHandMaterials()
        {
            foreach (string name in new[] { "Hand Gold", "Hand Cuffs" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(GateArt + name + ".mat");
                if (material == null) continue;
                material.SetColor("_BaseColor", name == "Hand Gold" ? new Color(.88f, .65f, .28f) : new Color(.24f, .16f, .38f));
                material.SetFloat("_Smoothness", .24f); material.SetFloat("_Metallic", .12f);
                material.SetColor("_EmissionColor", material.color * .09f); material.EnableKeyword("_EMISSION");
                EditorUtility.SetDirty(material);
            }
        }
    }
}
