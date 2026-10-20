using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    internal static class ChapterFiveNecropolisHazards
    {
        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Five Ground Hazards")]
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play mode and baking before refreshing hazards.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene edits before refreshing hazards.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int stage=41;stage<=50;stage++)
                {
                    var scene=EditorSceneManager.OpenScene(ChapterFiveNecropolisBuilder.SubPath(stage));
                    if(!Apply(scene))continue;
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        internal static bool Apply(Scene scene)
        {
            bool changed=false;
            foreach(var hazard in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GroundHazardAuthoring>(true)))
            {
                var appearance=hazard.GetComponent<GroundHazardAppearanceAuthoring>();
                if(appearance==null){appearance=hazard.gameObject.AddComponent<GroundHazardAppearanceAuthoring>();changed=true;}
                if(appearance.style!=GroundHazardStyle.Necropolis){appearance.style=GroundHazardStyle.Necropolis;changed=true;}
                EditorUtility.SetDirty(appearance);
            }
            return changed;
        }
    }
}

