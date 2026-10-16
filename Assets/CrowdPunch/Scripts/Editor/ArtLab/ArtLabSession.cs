using System.Collections.Generic;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Systems.Presentation;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Editor.ArtLab
{
    [InitializeOnLoad]
    public static class ArtLabSession
    {
        public static bool Requested { get; private set; }
        public static bool HasArena => environment!=null;
        public static bool Active => HasArena && Requested;
        public static bool Frozen => ownsPause;
        public static string Status => !EditorApplication.isPlaying ? "Enter Play mode in an example to preview." :
            environment==null ? "Waiting for Gauntlet 28-31. Other levels use their original look." :
            Requested ? "Preview active - source materials and scenes are unchanged." : "Original chapter presentation.";
        private static ExampleEnvironmentLighting environment;
        private static readonly Dictionary<Renderer,Material[]> originals=new();
        private static Volume overlay;
        private static Color sunColor,sky,equator,ground;
        private static float sunIntensity,fogStart,fogEnd;
        private static int environmentIndex,characterIndex,lightingIndex,postIndex;
        private static double nextScan;
        private static bool ownsPause,previousPause;
        private static UnityEngine.Camera camera;
        private static Vector3 originalPosition,bookmarkPosition;
        private static Quaternion originalRotation,bookmarkRotation;
        private static float originalFov,bookmarkFov;
        private static CrowdPunch.Mono.Camera.CameraFollow follow;
        private static bool followEnabled,hasBookmark;

        static ArtLabSession()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=> {if(state==PlayModeStateChange.ExitingPlayMode) Reset();};
            AssemblyReloadEvents.beforeAssemblyReload+=Reset;
        }

        public static void Configure(bool enabled,int environmentStyle,int characterStyle,int lighting,int post)
        {
            bool changed=Requested!=enabled||environmentStyle!=environmentIndex||characterStyle!=characterIndex||lighting!=lightingIndex||post!=postIndex;
            Requested=enabled;environmentIndex=environmentStyle;characterIndex=characterStyle;lightingIndex=lighting;postIndex=post;
            if(changed) {RestoreObjects();ArtLabPreview.Revision++;Apply();}
        }

        private static void Tick()
        {
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            double now=EditorApplication.timeSinceStartup;
            if(now<nextScan) return;
            nextScan=now+.25;
            var current=Object.FindFirstObjectByType<ExampleEnvironmentLighting>();
            if(!ReferenceEquals(environment,current))
            {
                SetFrozen(false);RestoreObjects();RestoreCamera();DestroyOverlay();RestoreEcs();
                environment=current;
                if(environment!=null)
                {
                    sunColor=environment.sun.color;sunIntensity=environment.sun.intensity;
                    sky=environment.ambientSky;equator=environment.ambientEquator;ground=environment.ambientGround;
                    fogStart=environment.hazeStart;fogEnd=environment.hazeEnd;
                }
                ArtLabPreview.Revision++;
                Apply();
            }
            // Destroyed Unity objects compare equal to null; still gate the bridge every tick.
            ArtLabPreview.Enabled=Active;
            if(!Active) {RestoreEcs();return;}
            ApplyObjects();
            if(EditorApplication.isPaused) UpdateEcs();
        }

        private static void Apply()
        {
            ArtLabPreview.Enabled=Active;
            ArtLabPreview.EnvironmentStyle=(int)(ArtLabAssets.Load(environmentIndex)?.shading ?? ArtLabStyle.Current);
            ArtLabPreview.CharacterStyle=(int)(ArtLabAssets.Load(characterIndex)?.shading ?? ArtLabStyle.Current);
            Shader.SetGlobalFloat("_ArtLabEnvironmentStyle",Active?ArtLabPreview.EnvironmentStyle:0);
            Shader.SetGlobalFloat("_ArtLabCharacterStyle",Active?ArtLabPreview.CharacterStyle:0);
            if(environment==null) return;
            var lighting=ArtLabAssets.Load(Active?lightingIndex:0);
            environment.sun.color=sunColor*(lighting!=null?lighting.sunTint:Color.white);
            environment.sun.intensity=sunIntensity*(lighting!=null?lighting.sunIntensityMultiplier:1);
            float ambient=lighting!=null?lighting.ambientMultiplier:1, distance=lighting!=null?lighting.fogDistanceMultiplier:1;
            if(!Active || lightingIndex==0) ambient=environment.ambientMultiplier;
            RenderSettings.ambientSkyColor=sky*ambient;RenderSettings.ambientEquatorColor=equator*ambient;RenderSettings.ambientGroundColor=ground*ambient;
            RenderSettings.fogStartDistance=fogStart*distance;RenderSettings.fogEndDistance=fogEnd*distance;
            DynamicGI.UpdateEnvironment();
            if(overlay==null)
            {
                var go=new GameObject("Art Lab preview volume") {hideFlags=HideFlags.HideAndDontSave};
                go.transform.SetParent(environment.transform,false);overlay=go.AddComponent<Volume>();overlay.isGlobal=true;overlay.priority=200;
            }
            overlay.weight=Active&&postIndex!=0?1:0;
            overlay.sharedProfile=ArtLabAssets.Load(postIndex)?.postProcessing;
            ApplyObjects();UpdateEcs();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void ApplyObjects()
        {
            if(!Active) return;
            foreach(var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(renderer is ParticleSystemRenderer || originals.ContainsKey(renderer)) continue;
                bool player=renderer.GetComponentInParent<CrowdPunch.Mono.Player.PlayerController>()!=null;
                if(renderer.gameObject.scene!=environment.gameObject.scene && !player) continue;
                var source=renderer.sharedMaterials;var replacement=(Material[])source.Clone();bool changed=false;
                for(int i=0;i<source.Length;i++) {replacement[i]=ArtLabPreview.Variant(source[i]);changed|=replacement[i]!=source[i];}
                if(changed) {originals[renderer]=source;renderer.sharedMaterials=replacement;}
            }
        }

        private static void RestoreObjects()
        {
            foreach(var pair in originals) if(pair.Key!=null) pair.Key.sharedMaterials=pair.Value;
            originals.Clear();
        }
        private static void UpdateEcs() => World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<ArtLabMaterialSystem>()?.Update();
        private static void RestoreEcs() => World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<ArtLabMaterialSystem>()?.Restore();
        private static void DestroyOverlay() {if(overlay!=null) Object.DestroyImmediate(overlay.gameObject);overlay=null;}

        public static void SetFrozen(bool frozen)
        {
            if(frozen && (!HasArena || Object.FindFirstObjectByType<GauntletSequence>()?.TransitionInProgress==true)) return;
            // Stop simulation time while leaving the presentation loop and material registration live.
            if(frozen && !ownsPause) {previousPause=CrowdPunch.Mono.Player.FeedbackTimeController.IsSuspended;ownsPause=true;CrowdPunch.Mono.Player.FeedbackTimeController.SetPaused(true);}
            else if(!frozen && ownsPause) {CrowdPunch.Mono.Player.FeedbackTimeController.SetPaused(previousPause);ownsPause=false;}
        }

        public static void Refresh()
        {
            RestoreObjects();ArtLabPreview.Revision++;Apply();
        }

        public static void SaveCamera()
        {
            var c=UnityEngine.Camera.main;if(c==null)return;
            bookmarkPosition=c.transform.position;bookmarkRotation=c.transform.rotation;bookmarkFov=c.fieldOfView;hasBookmark=true;
        }
        public static void Frame(bool overview)
        {
            var c=UnityEngine.Camera.main;if(c==null || (!overview&&!hasBookmark))return;
            if(camera==null)
            {
                camera=c;originalPosition=c.transform.position;originalRotation=c.transform.rotation;originalFov=c.fieldOfView;
                follow=c.GetComponent<CrowdPunch.Mono.Camera.CameraFollow>();followEnabled=follow!=null&&follow.enabled;
            }
            if(follow!=null)follow.enabled=false;
            c.transform.SetPositionAndRotation(overview?new Vector3(0,18,-43):bookmarkPosition,overview?Quaternion.Euler(27,0,0):bookmarkRotation);
            c.fieldOfView=overview?60:bookmarkFov;
        }
        public static void RestoreCamera()
        {
            if(camera!=null) {camera.transform.SetPositionAndRotation(originalPosition,originalRotation);camera.fieldOfView=originalFov;}
            if(follow!=null)follow.enabled=followEnabled;
            camera=null;follow=null;
        }

        public static void Reset()
        {
            Requested=false;RestoreObjects();Apply();ArtLabPreview.Enabled=false;RestoreEcs();DestroyOverlay();
            RestoreCamera();SetFrozen(false);ArtLabPreview.DisposeMaterials();environment=null;
            Shader.SetGlobalFloat("_ArtLabEnvironmentStyle",0);Shader.SetGlobalFloat("_ArtLabCharacterStyle",0);
        }
    }
}
