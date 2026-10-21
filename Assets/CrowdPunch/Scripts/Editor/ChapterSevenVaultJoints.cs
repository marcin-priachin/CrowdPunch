using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // VISION-004: fitted return walls meet front/rear slabs without overlapping their exposed faces.
    public static class ChapterSevenVaultJoints
    {
        [MenuItem("Crowd Punch/Art Direction/Repair Chapter Seven Vault Joints")]
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play/baking and save scenes before repairing vault joints.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int stage=61;stage<=70;stage++)
                {
                    var scene=EditorSceneManager.OpenScene(ChapterSevenVaultBuilder.MainPath(stage));
                    var root=scene.GetRootGameObjects().Single(g=>g.name==ChapterSevenVaultBuilder.RootName);
                    foreach(var vault in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Complete frozen vault entrance"))Apply(vault);
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        internal static void Apply(Transform vault)
        {
            var rear=vault.Find("Closed vault rear");
            var door=vault.GetComponentsInChildren<Transform>().First(t=>t.name=="Vault doorway pier");
            float front=door.localPosition.z+door.localScale.z*.5f;
            float back=rear.localPosition.z-rear.localScale.z*.5f;
            foreach(var wall in vault.GetComponentsInChildren<Transform>().Where(t=>t.name=="Vault returning wall"))
            {
                var p=wall.localPosition;p.z=(front+back)*.5f;wall.localPosition=p;
                var size=wall.localScale;size.z=back-front;wall.localScale=size;
            }
        }
    }
}
