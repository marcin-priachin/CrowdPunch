using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    internal static class ChapterSevenVaultHazards
    {
        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Seven Ground Hazards")]
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play mode and baking before refreshing hazards.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene edits before refreshing hazards.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int stage=61;stage<=70;stage++)
                {
                    var scene=EditorSceneManager.OpenScene(ChapterSevenVaultBuilder.SubPath(stage));
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
                if(appearance.style!=GroundHazardStyle.Vault){appearance.style=GroundHazardStyle.Vault;changed=true;}
                EditorUtility.SetDirty(appearance);
            }
            return changed;
        }
    }
}

