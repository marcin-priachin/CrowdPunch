using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // BARRICADE-004/006: replace only the skin of the existing two-metre gate.
    public static class FoundryBarrierVisuals
    {
        private const string Output = "Assets/CrowdPunch/Data/ChapterTwoFoundry/Barrier13";

        [MenuItem("Crowd Punch/Art Direction/Refresh Level 13 Barrier Visuals")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before authoring the barrier.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("Save open scenes first.");
            try
            {
                var scene = EditorSceneManager.OpenScene(ChapterTwoFoundryBuilder.SubPath(13));
                Build(Object.FindFirstObjectByType<BarricadeAuthoring>());
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        internal static void Build(BarricadeAuthoring gate)
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            var plate = FoundryShellMeshes.Plate();
            var chip = FoundryShellMeshes.Accent();
            foreach (var old in gate.GetComponentsInChildren<BarricadeVisualAuthoring>(true)) Object.DestroyImmediate(old.gameObject);
            var previous = gate.transform.Find("Foundry barrier assembly");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var root = new GameObject("Foundry barrier assembly").transform;
            root.SetParent(gate.transform, false);

            var copper = Paint("Barrier copper enamel", new Color(.66f,.38f,.23f), new Color(.78f,.49f,.29f));
            var steel = Paint("Barrier blue steel", new Color(.31f,.42f,.49f), new Color(.42f,.53f,.57f));
            var teal = Paint("Barrier teal braces", new Color(.25f,.47f,.45f), new Color(.40f,.57f,.51f));
            var brass = Paint("Barrier brass bolts", new Color(.77f,.57f,.29f), new Color(.61f,.43f,.23f));
            var ivory = Paint("Barrier enamel badge", new Color(.79f,.81f,.69f), new Color(.68f,.76f,.70f));
            var ink = Paint("Barrier fracture ink", new Color(.075f,.12f,.17f), new Color(.12f,.17f,.22f));
            var spark = Paint("Barrier amber flecks", new Color(1,.74f,.29f), new Color(1,.87f,.53f));
            var powder = Paint("Barrier powder wash", new Color(.60f,.66f,.65f), new Color(.51f,.58f,.62f));
            int index = 0;
            float width = gate.size.x, height = gate.size.y, depth = gate.size.z;
            void Piece(string name, Mesh mesh, Vector3 p, Vector3 size, Material material,
                int kind = 0, int stage = 0, float angle = 0)
            {
                float seed = (index++ * 37 % 101) / 100f;
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.transform.localPosition = p;
                go.transform.localScale = size;
                go.transform.localRotation = Quaternion.Euler(0,0,angle);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.forceMeshLod = 0;
                GameObjectUtility.SetStaticEditorFlags(go, 0);
                var visual = go.AddComponent<BarricadeVisualAuthoring>();
                visual.barricade = gate; visual.crackStage = stage;
                // Reuse the bounded recoil/tumble presentation already used by chapter gates.
                var motion = go.AddComponent<VillageGatePieceAuthoring>();
                motion.kind = kind; motion.damageStage = stage; motion.seed = seed;
                motion.duration = 1.10f + seed * .25f;
                motion.velocity = new Vector3((seed-.5f)*3.8f, 3.1f+seed*1.9f, 2.3f+seed*1.6f);
                motion.spin = new Vector3(2+seed*3, seed*4-2, 2.5f-seed*5);
                // The collider extends below this court's zero-height floor. Land visible debris on the floor.
                motion.restingHeight = height*.5f-gate.transform.position.y+size.z*.5f+.03f;
            }

            // Thin metal skins and separate returns expose a hollow welded construction as it tears apart.
            for (int row = 0; row < 4; row++)
            {
                float y = (row-1.5f)*height/4;
                foreach (int face in new[] {-1,1}) for (int col = -1; col <= 1; col += 2)
                {
                    float x = col*width*.25f;
                    Piece("Breakaway copper plate",plate,new Vector3(x,y,face*(depth*.5f-.065f)),new Vector3(width*.5f-.025f,height/4-.025f,.13f),copper);
                    foreach (int side in new[] {-1,1})
                        Piece("Captive brass rivet",plate,new Vector3(x+side*width*.155f,y+.28f,face*(depth*.5f+.035f)),new Vector3(.11f,.11f,.07f),brass);
                }
                foreach (int side in new[] {-1,1})
                    Piece("Split steel edge return",plate,new Vector3(side*(width*.5f-.065f),y,0),new Vector3(.13f,height/4-.025f,depth-.08f),steel);
            }
            for (int col = -1; col <= 1; col += 2)
                Piece("Split steel crown",plate,new Vector3(col*width*.25f,height*.5f-.055f,0),new Vector3(width*.5f-.025f,.11f,depth-.02f),steel);
            foreach (int face in new[] {-1,1})
            {
                foreach (int row in new[] {-1,1}) for (int col = -1; col <= 1; col += 2)
                    Piece("Teal reinforcing cleat",plate,new Vector3(col*width*.25f,row*height*.205f,face*(depth*.5f+.04f)),new Vector3(width*.5f-.05f,.15f,.08f),teal);
                Piece("Ivory foundry strike badge",plate,new Vector3(0,.22f,face*(depth*.5f+.075f)),new Vector3(.63f,.63f,.06f),ivory,angle:45);
                Piece("Teal badge inset",plate,new Vector3(0,.22f,face*(depth*.5f+.11f)),new Vector3(.34f,.34f,.025f),teal,angle:45);
                for (int damage = 1; damage <= 2; damage++)
                {
                    var path = damage==1
                        ? new[] {new Vector2(-.15f,1.45f),new Vector2(-.40f,1.02f),new Vector2(-.22f,.71f),new Vector2(-.53f,.31f)}
                        : new[] {new Vector2(.60f,1.13f),new Vector2(.25f,.91f),new Vector2(.43f,.54f),new Vector2(.12f,.19f)};
                    void Seam(Vector2 a, Vector2 b)
                    {
                        var delta = b-a; var center = (a+b)*.5f;
                        Piece("Progressive torn metal seam",plate,new Vector3(center.x,center.y,face*(depth*.5f+.015f)),
                            new Vector3(.045f,delta.magnitude+.025f,.025f),ink,1,damage,-Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg);
                    }
                    for (int segment = 0; segment < path.Length-1; segment++) Seam(path[segment],path[segment+1]);
                    Seam(path[damage==1?2:1],damage==1?new Vector2(-.64f,.58f):new Vector2(.09f,1.17f));
                }
            }
            for (int i = 0; i < 8; i++)
                Piece("Brief amber impact fleck",chip,new Vector3((i%4-1.5f)*.25f,(i/4)*.4f,0),new Vector3(.07f,.23f,.06f),spark,2);
            for (int i = 0; i < 5; i++)
                Piece("Brief painted powder puff",chip,new Vector3((i-2)*.28f,.15f,0),new Vector3(.25f,.34f,.23f),powder,3);
            if (root.GetComponentsInChildren<Collider>().Length != 0) throw new InvalidOperationException("Barrier decoration must not add collision.");
        }

        private static Material Paint(string name, Color color, Color wash)
        {
            string path = Output+"/"+name+".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("CrowdPunch/Cel/Painted Foundry"));
                AssetDatabase.CreateAsset(material,path);
            }
            material.enableInstancing = true;
            material.SetColor("_BaseColor",color); material.SetColor("_WashColor",wash);
            material.SetColor("_ShadowColor",new Color(.43f,.49f,.65f)); material.SetColor("_LightColor",new Color(1,.95f,.86f));
            material.SetFloat("_WashStrength",.18f); material.SetFloat("_Variation",.12f); material.SetFloat("_GrainStrength",.065f);
            material.SetFloat("_TextureStrength",0); material.SetFloat("_Contrast",.78f);
            material.SetFloat("_ObjectSpacePaint",1); material.SetFloat("_UseEntityColor",1);
            material.SetFloat("_CameraCutaway",0); material.SetFloat("_ReceiveShadows",1);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
