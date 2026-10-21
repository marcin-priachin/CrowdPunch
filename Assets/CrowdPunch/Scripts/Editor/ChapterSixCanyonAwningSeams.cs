using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // VISION-004/006: canvas linework needs real depth clearance along its sloped normal.
    public static class ChapterSixCanyonAwningSeams
    {
        internal static void Align(Transform canvas,Transform seam,float horizontalOffset)
        {
            seam.localRotation=canvas.localRotation;
            seam.localPosition=canvas.localPosition+canvas.localRotation*new Vector3(horizontalOffset,
                canvas.localScale.y*.5f+seam.localScale.y*.5f+.015f,0);
        }

        [MenuItem("Crowd Punch/Art Direction/Fix Chapter Six Store Joints and Awning Seams")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scenes and stop Play/baking before correcting the awnings.");
            var setup=EditorSceneManager.GetSceneManagerSetup();int count=0;
            try
            {
                for(int stage=51;stage<=60;stage++)
                {
                    var scene=EditorSceneManager.OpenScene(ChapterSixCanyonBuilder.MainPath(stage));
                    var root=scene.GetRootGameObjects().Single(g=>g.name==ChapterSixCanyonBuilder.RootName);
                    foreach(var canvas in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Faded blue canvas awning"))
                    {
                        ChapterSixCanyonScenery.CloseStoreWallJoints(canvas.parent);
                        foreach(var seam in canvas.parent.GetComponentsInChildren<Transform>().Where(t=>t.name=="Drawn canvas seam"))
                        {
                            float x=seam.localPosition.x-canvas.localPosition.x;
                            Align(canvas,seam,x);EditorUtility.SetDirty(seam);count++;
                        }
                    }
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                Debug.Log($"Corrected {count} canyon awning seams. Fixed-geometry shadow rebake required.");
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
    }
}
