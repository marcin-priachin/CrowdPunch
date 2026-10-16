using System;
using System.IO;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor.ArtLab
{
    [InitializeOnLoad]
    public sealed class ArtLabWindow : EditorWindow
    {
        private const string LaunchKey="CrowdPunch.ArtLab.PreviousStart";
        [SerializeField] private int preset=2,environmentStyle=2,characterStyle=2,lighting=2,post=2,level;
        [SerializeField] private bool preview=true;
        [SerializeField] private int[] a={0,0,0,0},b={1,1,1,1};
        private bool showingB;
        private Vector2 scroll;
        private string error;
        private static readonly string[] Levels={"28 - Sunlit Village Gate","29 - Emberworks Foundry","30 - Overgrown Observatory","31 - Frostbound Vault"};

        static ArtLabWindow()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(LaunchKey+".Active",false))return;
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(LaunchKey,""));
                SessionState.EraseString(LaunchKey);SessionState.EraseBool(LaunchKey+".Active");
            };
        }
        [MenuItem("Crowd Punch/Art Lab/Open Art Lab")]
        [MenuItem("Window/Crowd Punch/Art Lab")]
        public static void Open() => GetWindow<ArtLabWindow>("Art Lab").Show();
        private void OnEnable() => minSize=new Vector2(410,560);
        private void OnDisable() => ArtLabSession.Reset();
        private void OnInspectorUpdate() { ArtLabSession.Configure(preview,environmentStyle,characterStyle,lighting,post);Repaint(); }

        private void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Label("CROWD PUNCH  /  ART LAB",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Compare five looks in the same four arenas. Preview changes are temporary and restore when you close Art Lab or exit Play mode.",MessageType.Info);
            if(ArtLabAssets.Load(4)==null)
            {
                EditorGUILayout.HelpBox("Art Lab assets have not been generated yet.",MessageType.Warning);
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if(GUILayout.Button("Create missing presets and compatible shaders")) Run(ArtLabAssets.CreateMissing);
                EditorGUILayout.EndScrollView();return;
            }
            level=EditorGUILayout.Popup("Example arena",level,Levels);
            using(new EditorGUI.DisabledScope(EditorApplication.isCompiling))
                if(GUILayout.Button(EditorApplication.isPlaying?"Load selected arena":"Play selected arena",GUILayout.Height(28))) Run(()=>Launch(level));

            EditorGUILayout.Space(8);
            EditorGUI.BeginChangeCheck();preset=EditorGUILayout.Popup("Complete style",preset,ArtLabAssets.Names);
            if(EditorGUI.EndChangeCheck()) environmentStyle=characterStyle=lighting=post=preset;
            EditorGUILayout.LabelField(ArtLabAssets.Load(preset).description,EditorStyles.wordWrappedLabel);
            preview=EditorGUILayout.Toggle("Enable preview",preview);
            environmentStyle=EditorGUILayout.Popup("Environment shading",environmentStyle,ArtLabAssets.Names);
            characterStyle=EditorGUILayout.Popup("Player + enemy shading",characterStyle,ArtLabAssets.Names);
            lighting=EditorGUILayout.Popup("Lighting",lighting,ArtLabAssets.Names);
            post=EditorGUILayout.Popup("Post-processing",post,ArtLabAssets.Names);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Inspect preset")) Selection.activeObject=ArtLabAssets.Load(preset);
                if(GUILayout.Button("Refresh preview")) Run(ArtLabSession.Refresh);
            }
            EditorGUILayout.Space(8);GUILayout.Label("A / B comparison",EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Store A")) a=new[]{environmentStyle,characterStyle,lighting,post};
                if(GUILayout.Button("Store B")) b=new[]{environmentStyle,characterStyle,lighting,post};
                if(GUILayout.Button(showingB?"Show A":"Show B")) {showingB=!showingB;var s=showingB?b:a;environmentStyle=s[0];characterStyle=s[1];lighting=s[2];post=s[3];}
            }
            EditorGUILayout.LabelField("A",Summary(a),EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("B",Summary(b),EditorStyles.wordWrappedLabel);
            using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying || !ArtLabSession.HasArena))
            {
                EditorGUI.BeginChangeCheck();bool frozen=EditorGUILayout.Toggle("Freeze action",ArtLabSession.Frozen);
                if(EditorGUI.EndChangeCheck()) ArtLabSession.SetFrozen(frozen);
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Save camera"))ArtLabSession.SaveCamera();
                    if(GUILayout.Button("Recall camera"))ArtLabSession.Frame(false);
                    if(GUILayout.Button("Arena overview"))ArtLabSession.Frame(true);
                }
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Resume camera"))ArtLabSession.RestoreCamera();
                    if(GUILayout.Button("Restart encounter"))Run(()=> {ArtLabSession.SetFrozen(false);Object.FindFirstObjectByType<GauntletSequence>()?.RestartCurrentLevel();});
                    if(GUILayout.Button("Save screenshot"))Run(Capture);
                }
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(ArtLabSession.Status,MessageType.None);
            if(EditorApplication.isPlaying)
                EditorGUILayout.LabelField($"Editor: {UnityStats.batches:N0} batches | {UnityStats.triangles:N0} triangles | {Time.unscaledDeltaTime*1000:F1} ms/frame",EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Comic uses shading-based silhouette ink and shadow dots. No motion blur or depth of field. Chapter colours remain in the lighting and grading.",EditorStyles.wordWrappedMiniLabel);
            if(!string.IsNullOrEmpty(error))EditorGUILayout.HelpBox(error,MessageType.Error);
            ArtLabSession.Configure(preview,environmentStyle,characterStyle,lighting,post);
            EditorGUILayout.EndScrollView();
        }

        private static string Summary(int[] s) => $"World: {ArtLabAssets.Names[s[0]]}; Characters: {ArtLabAssets.Names[s[1]]}; Light: {ArtLabAssets.Names[s[2]]}; Post: {ArtLabAssets.Names[s[3]]}";
        private void Run(Action action) {try {error=null;action();}catch(Exception exception){error=exception.Message;Debug.LogException(exception);}}

        public static void Launch(int index)
        {
            if(index<0||index>3)throw new ArgumentOutOfRangeException(nameof(index));
            ArtLabSession.SetFrozen(false);
            if(EditorApplication.isPlaying)
            {
                var sequence=Object.FindFirstObjectByType<GauntletSequence>();
                if(sequence==null||sequence.HasCampaign)throw new InvalidOperationException("Exit Play mode, then launch an Art Lab arena to enter Legacy mode.");
                sequence.SelectLevel(27+index);return;
            }
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            SessionState.SetString(LaunchKey,AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));SessionState.SetBool(LaunchKey+".Active",true);
            SessionState.SetBool(GauntletSequence.EditorLegacyKey,true);SessionState.SetString(GauntletSequence.EditorStartLevelKey,"Gauntlet_"+(28+index));
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/CrowdPunch/Scenes/Bootstrap.unity");EditorApplication.isPlaying=true;
        }

        public static string CaptureTo(string path)
        {
            var c=UnityEngine.Camera.main;if(c==null)throw new InvalidOperationException("No gameplay camera.");
            var old=c.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try {c.targetTexture=target;c.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());}
            finally {c.targetTexture=old;RenderTexture.active=active;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);}
            return path;
        }
        private void Capture()
        {
            string path=EditorUtility.SaveFilePanel("Save Art Lab comparison",Path.GetFullPath("Temp"),$"ArtLab_{28+level}_{DateTime.Now:HHmmss}","png");
            if(!string.IsNullOrEmpty(path))CaptureTo(path);
        }
    }
}
